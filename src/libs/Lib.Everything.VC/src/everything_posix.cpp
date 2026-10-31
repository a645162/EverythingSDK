/*
 * File: libs/EverythingSDK/src/libs/Lib.Everything.VC/src/everything_posix.cpp
 * Brief: Everything SDK 的 POSIX 实现：以 plocate/locate、find 或进程内扫描
 *        提供与 Windows Everything 相同的客户端 API。
 * Author: Haomin Kong
 * Email: konghm@beim.com.cn / a645162@163.com
 * Created: 2026-10-31
 *
 * 设计说明：
 * - Linux 上没有 Everything 服务，最接近的是 locate 索引数据库；
 *   索引缺失时退回 find，find 也不可用时用进程内目录扫描。
 * - 公开头文件（含 std::wstring 字段）保持不变，调用方无需区分平台；
 *   仅在需要时用 EverythingClient::GetBackendName() 展示实际后端。
 * - 与 Everything SDK 一样，客户端不保证线程安全；但设置项保存在
 *   EverythingClient 实例里，与 Windows 版本语义一致。
 */

#include "Lib/Everything/Everything.h"

#include "posix/file_metadata.h"
#include "posix/search_backend.h"
#include "posix/search_query.h"
#include "posix/wide_string.h"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace {
using Lib::Everything::EverythingClient;
using Lib::Everything::EverythingError;
using Lib::Everything::EverythingRequestFlags;
using Lib::Everything::EverythingResult;
using Lib::Everything::EverythingSort;
using Lib::Everything::EverythingTargetMachine;
using Lib::Everything::Posix::FileMetadata;
using Lib::Everything::Posix::ReadFileMetadata;
using Lib::Everything::Posix::SearchQuery;

constexpr std::size_t DefaultCandidateCap = 200000;

std::size_t ReadCandidateCap() {
  const char *value = std::getenv("EVERYTHING_SDK_MAX_CANDIDATES");
  if (value == nullptr) {
    return DefaultCandidateCap;
  }

  const long parsed = std::strtol(value, nullptr, 10);
  if (parsed <= 0) {
    return DefaultCandidateCap;
  }
  return static_cast<std::size_t>(parsed);
}

std::size_t EffectiveCandidateCap(const EverythingClient &client) {
  std::size_t cap = ReadCandidateCap();
  if (client.Max > 0) {
    const std::size_t requested = static_cast<std::size_t>(client.Offset) +
                                  static_cast<std::size_t>(client.Max);
    cap = std::max(cap, requested);
  }
  return cap;
}

std::string DirectoryName(const std::string &path) {
  if (path.empty()) {
    return {};
  }

  std::size_t end = path.size();
  while (end > 1 && path[end - 1] == '/') {
    --end;
  }

  const std::size_t separator = path.rfind('/', end - 1);
  if (separator == std::string::npos) {
    return ".";
  }
  if (separator == 0) {
    return "/";
  }
  return path.substr(0, separator);
}

bool SortNeedsMetadata(const EverythingSort sort) {
  switch (sort) {
  case EverythingSort::SizeAscending:
  case EverythingSort::SizeDescending:
  case EverythingSort::DateCreatedAscending:
  case EverythingSort::DateCreatedDescending:
  case EverythingSort::DateModifiedAscending:
  case EverythingSort::DateModifiedDescending:
  case EverythingSort::DateAccessedAscending:
  case EverythingSort::DateAccessedDescending:
  case EverythingSort::DateRunAscending:
  case EverythingSort::DateRunDescending:
  case EverythingSort::DateRecentlyChangedAscending:
  case EverythingSort::DateRecentlyChangedDescending:
  case EverythingSort::AttributesAscending:
  case EverythingSort::AttributesDescending:
    return true;
  default:
    return false;
  }
}

bool IsDescending(const EverythingSort sort) {
  switch (sort) {
  case EverythingSort::NameDescending:
  case EverythingSort::PathDescending:
  case EverythingSort::SizeDescending:
  case EverythingSort::ExtensionDescending:
  case EverythingSort::TypeNameDescending:
  case EverythingSort::DateCreatedDescending:
  case EverythingSort::DateModifiedDescending:
  case EverythingSort::AttributesDescending:
  case EverythingSort::FileListFileNameDescending:
  case EverythingSort::RunCountDescending:
  case EverythingSort::DateRecentlyChangedDescending:
  case EverythingSort::DateAccessedDescending:
  case EverythingSort::DateRunDescending:
    return true;
  default:
    return false;
  }
}

struct Entry {
  std::string path;
  FileMetadata metadata;
  bool metadata_loaded{false};
};

void EnsureMetadata(Entry &entry) {
  if (!entry.metadata_loaded) {
    entry.metadata = ReadFileMetadata(entry.path);
    entry.metadata_loaded = true;
  }
}

bool CompareEntries(const Entry &left, const Entry &right,
                    const EverythingSort sort) {
  const bool descending = IsDescending(sort);
  int comparison = 0;

  switch (sort) {
  case EverythingSort::PathAscending:
  case EverythingSort::PathDescending:
    comparison = left.path.compare(right.path);
    break;
  case EverythingSort::SizeAscending:
  case EverythingSort::SizeDescending:
    comparison = left.metadata.size < right.metadata.size
                     ? -1
                     : (left.metadata.size > right.metadata.size ? 1 : 0);
    break;
  case EverythingSort::ExtensionAscending:
  case EverythingSort::ExtensionDescending:
  case EverythingSort::TypeNameAscending:
  case EverythingSort::TypeNameDescending:
    comparison = Lib::Everything::Posix::AsciiLower(
                     Lib::Everything::Posix::PathExtension(left.path))
                     .compare(Lib::Everything::Posix::AsciiLower(
                         Lib::Everything::Posix::PathExtension(right.path)));
    break;
  case EverythingSort::DateCreatedAscending:
  case EverythingSort::DateCreatedDescending:
    comparison =
        left.metadata.has_created && right.metadata.has_created
            ? (left.metadata.created < right.metadata.created
                   ? -1
                   : (left.metadata.created > right.metadata.created ? 1 : 0))
            : (left.metadata.has_created
                   ? 1
                   : (right.metadata.has_created ? -1 : 0));
    break;
  case EverythingSort::DateModifiedAscending:
  case EverythingSort::DateModifiedDescending:
    comparison =
        left.metadata.has_modified && right.metadata.has_modified
            ? (left.metadata.modified < right.metadata.modified
                   ? -1
                   : (left.metadata.modified > right.metadata.modified ? 1 : 0))
            : (left.metadata.has_modified
                   ? 1
                   : (right.metadata.has_modified ? -1 : 0));
    break;
  case EverythingSort::DateAccessedAscending:
  case EverythingSort::DateAccessedDescending:
    comparison =
        left.metadata.has_accessed && right.metadata.has_accessed
            ? (left.metadata.accessed < right.metadata.accessed
                   ? -1
                   : (left.metadata.accessed > right.metadata.accessed ? 1 : 0))
            : (left.metadata.has_accessed
                   ? 1
                   : (right.metadata.has_accessed ? -1 : 0));
    break;
  case EverythingSort::AttributesAscending:
  case EverythingSort::AttributesDescending:
    comparison =
        left.metadata.attributes < right.metadata.attributes
            ? -1
            : (left.metadata.attributes > right.metadata.attributes ? 1 : 0);
    break;
  case EverythingSort::RunCountAscending:
  case EverythingSort::RunCountDescending:
  case EverythingSort::DateRecentlyChangedAscending:
  case EverythingSort::DateRecentlyChangedDescending:
  case EverythingSort::DateRunAscending:
  case EverythingSort::DateRunDescending:
  case EverythingSort::FileListFileNameAscending:
  case EverythingSort::FileListFileNameDescending:
  default:
    comparison = Lib::Everything::Posix::AsciiLower(
                     Lib::Everything::Posix::BaseName(left.path))
                     .compare(Lib::Everything::Posix::AsciiLower(
                         Lib::Everything::Posix::BaseName(right.path)));
    break;
  }

  if (comparison == 0) {
    // 同键值用完整路径兜底，保证多次查询顺序稳定。
    comparison = left.path.compare(right.path);
  }

  if (descending) {
    comparison = -comparison;
  }
  return comparison < 0;
}

EverythingResult BuildResult(const Entry &entry, const std::uint32_t index) {
  const std::string file_name = Lib::Everything::Posix::BaseName(entry.path);

  EverythingResult result;
  result.Index = index;
  result.FileName = Lib::Everything::Posix::WideFromUtf8(file_name);
  result.Path = Lib::Everything::Posix::WideFromUtf8(DirectoryName(entry.path));
  result.FullPath = Lib::Everything::Posix::WideFromUtf8(entry.path);
  result.Extension = Lib::Everything::Posix::WideFromUtf8(
      Lib::Everything::Posix::PathExtension(entry.path));
  result.Attributes = entry.metadata.attributes;
  result.HasSize = entry.metadata.exists;
  result.Size = entry.metadata.size;
  result.IsFile = entry.metadata.is_file;
  result.IsFolder = entry.metadata.is_folder;
  result.HasDateCreated = entry.metadata.has_created;
  result.DateCreated = entry.metadata.created;
  result.HasDateModified = entry.metadata.has_modified;
  result.DateModified = entry.metadata.modified;
  result.HasDateAccessed = entry.metadata.has_accessed;
  result.DateAccessed = entry.metadata.accessed;
  return result;
}

EverythingTargetMachine CurrentTargetMachine() {
#if defined(_M_ARM64) || defined(__aarch64__)
  return EverythingTargetMachine::Arm64;
#elif defined(_M_ARM) || defined(__arm__)
  return EverythingTargetMachine::Arm;
#elif defined(__x86_64__) || defined(_M_X64)
  return EverythingTargetMachine::X64;
#elif defined(__i386__) || defined(_M_IX86)
  return EverythingTargetMachine::X86;
#else
  return EverythingTargetMachine::Unknown;
#endif
}
} // namespace

namespace Lib {
namespace Everything {
std::wstring EverythingVersion::ToString() const {
  return std::to_wstring(Major) + L"." + std::to_wstring(Minor) + L"." +
         std::to_wstring(Revision) + L"." + std::to_wstring(Build);
}

std::wstring EverythingResult::ToString() const {
  if (!FullPath.empty()) {
    return FullPath;
  }
  if (!FileName.empty()) {
    return FileName;
  }
  return L"Result " + std::to_wstring(Index);
}

EverythingClient::~EverythingClient() noexcept = default;

std::vector<EverythingResult> EverythingClient::Query() {
  const std::string search_text = Posix::Utf8FromWide(SearchText);
  const SearchQuery query = Posix::ParseSearchQuery(
      search_text, MatchPath, MatchCase, MatchWholeWord, Regex);

  const Posix::CandidateCollection candidates =
      Posix::CollectCandidates(query, EffectiveCandidateCap(*this));
  if (!candidates.error.empty() && candidates.paths.empty()) {
    LastError = EverythingError::Ipc;
    TotalResults = 0;
    NumResults = 0;
    throw std::runtime_error("Everything POSIX query failed: " +
                             candidates.error);
  }

  std::vector<Entry> entries;
  entries.reserve(candidates.paths.size());
  for (const std::string &path : candidates.paths) {
    // locate/find 只做粗筛（glob），这里用真实语法做最终判定。
    if (!query.matches(path)) {
      continue;
    }

    Entry entry;
    entry.path = path;
    entries.push_back(std::move(entry));
  }

  if (SortNeedsMetadata(Sort)) {
    for (Entry &entry : entries) {
      EnsureMetadata(entry);
    }
  }

  std::stable_sort(entries.begin(), entries.end(),
                   [this](const Entry &left, const Entry &right) {
                     return CompareEntries(left, right, Sort);
                   });

  TotalResults = static_cast<std::uint32_t>(
      std::min<std::size_t>(entries.size(), 0xFFFFFFFFU));

  const std::size_t begin =
      std::min<std::size_t>(static_cast<std::size_t>(Offset), entries.size());
  std::size_t end = entries.size();
  if (Max > 0) {
    end = std::min<std::size_t>(begin + static_cast<std::size_t>(Max),
                                entries.size());
  }

  std::vector<EverythingResult> results;
  results.reserve(end - begin);
  for (std::size_t index = begin; index < end; ++index) {
    Entry &entry = entries[index];
    EnsureMetadata(entry);

    EverythingResult result =
        BuildResult(entry, static_cast<std::uint32_t>(results.size()));

    // 与 SDK 一致：未请求的字段不返回，避免调用方误以为拿到了权威值。
    if (!HasFlag(RequestFlags, EverythingRequestFlags::Size)) {
      result.HasSize = false;
      result.Size = 0;
    }
    if (!HasFlag(RequestFlags, EverythingRequestFlags::DateCreated)) {
      result.HasDateCreated = false;
    }
    if (!HasFlag(RequestFlags, EverythingRequestFlags::DateModified)) {
      result.HasDateModified = false;
    }
    if (!HasFlag(RequestFlags, EverythingRequestFlags::DateAccessed)) {
      result.HasDateAccessed = false;
    }

    results.push_back(std::move(result));
  }

  LastError = EverythingError::Ok;
  NumResults = static_cast<std::uint32_t>(results.size());
  return results;
}

std::vector<EverythingResult>
EverythingClient::Search(const std::wstring &searchText) {
  EverythingClient client;
  client.SearchText = searchText;
  return client.Query();
}

EverythingVersion EverythingClient::GetVersion() {
  // POSIX 后端没有 Everything 服务版本，返回本 SDK 自身版本，
  // 具体使用哪种搜索后端请用 GetBackendName() 查询。
  EverythingVersion version;
  version.Major = SdkVersionMajor();
  version.Minor = SdkVersionMinor();
  version.Revision = SdkVersionPatch();
  version.Build = 0;
  return version;
}

EverythingTargetMachine EverythingClient::GetTargetMachine() {
  return CurrentTargetMachine();
}

bool EverythingClient::IsDBLoaded() {
  return Posix::DetectBackend().database_loaded;
}

bool EverythingClient::IsEverythingRunning() {
  // POSIX 上没有常驻服务；只要还能用 locate/find/scan 搜索就返回 true，
  // 这样才能让调用方的“搜索可用性”判断在 Linux 上成立。
  return true;
}

std::wstring EverythingClient::GetBackendName() {
  return Posix::WideFromUtf8(Posix::BackendName(Posix::DetectBackend().kind));
}

std::wstring ToWideString(const EverythingError error) {
  switch (error) {
  case EverythingError::Ok:
    return L"Ok";
  case EverythingError::Memory:
    return L"Memory";
  case EverythingError::Ipc:
    return L"Ipc";
  case EverythingError::RegisterClassExError:
    return L"RegisterClassEx";
  case EverythingError::CreateWindowError:
    return L"CreateWindow";
  case EverythingError::CreateThread:
    return L"CreateThread";
  case EverythingError::InvalidIndex:
    return L"InvalidIndex";
  case EverythingError::InvalidCall:
    return L"InvalidCall";
  case EverythingError::InvalidRequest:
    return L"InvalidRequest";
  case EverythingError::InvalidParameter:
    return L"InvalidParameter";
  default:
    return L"Unknown";
  }
}
} // namespace Everything
} // namespace Lib
