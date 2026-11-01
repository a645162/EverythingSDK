/*
 * File: Everything.h
 * Brief: Everything.
 * Author: Haomin Kong
 * Email: a645162@163.com
 * Created: 2026-07-31
 */

#pragma once

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

#include <Lib/Everything/version.h>

#ifdef RegisterClassEx
#undef RegisterClassEx
#endif

#ifdef CreateWindow
#undef CreateWindow
#endif

namespace Lib {
namespace Everything {
enum class EverythingError : std::uint32_t {
  Ok = 0,
  Memory = 1,
  Ipc = 2,
  RegisterClassExError = 3,
  CreateWindowError = 4,
  CreateThread = 5,
  InvalidIndex = 6,
  InvalidCall = 7,
  InvalidRequest = 8,
  InvalidParameter = 9
};

enum class EverythingSort : std::uint32_t {
  NameAscending = 1,
  NameDescending = 2,
  PathAscending = 3,
  PathDescending = 4,
  SizeAscending = 5,
  SizeDescending = 6,
  ExtensionAscending = 7,
  ExtensionDescending = 8,
  TypeNameAscending = 9,
  TypeNameDescending = 10,
  DateCreatedAscending = 11,
  DateCreatedDescending = 12,
  DateModifiedAscending = 13,
  DateModifiedDescending = 14,
  AttributesAscending = 15,
  AttributesDescending = 16,
  FileListFileNameAscending = 17,
  FileListFileNameDescending = 18,
  RunCountAscending = 19,
  RunCountDescending = 20,
  DateRecentlyChangedAscending = 21,
  DateRecentlyChangedDescending = 22,
  DateAccessedAscending = 23,
  DateAccessedDescending = 24,
  DateRunAscending = 25,
  DateRunDescending = 26
};

enum class EverythingRequestFlags : std::uint32_t {
  None = 0,
  FileName = 0x00000001,
  Path = 0x00000002,
  FullPathAndFileName = 0x00000004,
  Extension = 0x00000008,
  Size = 0x00000010,
  DateCreated = 0x00000020,
  DateModified = 0x00000040,
  DateAccessed = 0x00000080,
  Attributes = 0x00000100,
  FileListFileName = 0x00000200,
  RunCount = 0x00000400,
  DateRun = 0x00000800,
  DateRecentlyChanged = 0x00001000,
  HighlightedFileName = 0x00002000,
  HighlightedPath = 0x00004000,
  HighlightedFullPathAndFileName = 0x00008000
};

enum class EverythingTargetMachine : std::uint32_t {
  Unknown = 0,
  X86 = 1,
  X64 = 2,
  Arm = 3,
  Arm64 = 4
};

constexpr EverythingRequestFlags
operator|(const EverythingRequestFlags lhs,
          const EverythingRequestFlags rhs) noexcept {
  return static_cast<EverythingRequestFlags>(static_cast<std::uint32_t>(lhs) |
                                             static_cast<std::uint32_t>(rhs));
}

constexpr EverythingRequestFlags
operator&(const EverythingRequestFlags lhs,
          const EverythingRequestFlags rhs) noexcept {
  return static_cast<EverythingRequestFlags>(static_cast<std::uint32_t>(lhs) &
                                             static_cast<std::uint32_t>(rhs));
}

// C++11 要求 constexpr 函数体只有一条 return 语句，而这里必须先赋值，
// 因此改成 inline；GCC/Clang 在 -std=c++11 下会拒绝 constexpr 版本。
inline EverythingRequestFlags &
operator|=(EverythingRequestFlags &lhs,
           const EverythingRequestFlags rhs) noexcept {
  lhs = lhs | rhs;
  return lhs;
}

constexpr bool HasFlag(const EverythingRequestFlags value,
                       const EverythingRequestFlags flag) noexcept {
  return (value & flag) == flag;
}

struct EverythingVersion {
  std::uint32_t Major{};
  std::uint32_t Minor{};
  std::uint32_t Revision{};
  std::uint32_t Build{};

  std::wstring ToString() const;
};

struct EverythingResult {
  std::uint32_t Index{};
  std::wstring FileName;
  std::wstring Path;
  std::wstring FullPath;
  std::wstring Extension;
  bool HasSize{false};
  std::int64_t Size{};
  bool HasDateCreated{false};
  std::chrono::system_clock::time_point DateCreated;
  bool HasDateModified{false};
  std::chrono::system_clock::time_point DateModified;
  bool HasDateAccessed{false};
  std::chrono::system_clock::time_point DateAccessed;
  std::uint32_t Attributes{};
  std::wstring FileListFileName;
  std::uint32_t RunCount{};
  bool HasDateRun{false};
  std::chrono::system_clock::time_point DateRun;
  bool HasDateRecentlyChanged{false};
  std::chrono::system_clock::time_point DateRecentlyChanged;
  bool IsVolume{};
  bool IsFolder{};
  bool IsFile{};

  std::wstring ToString() const;
};

class EverythingClient {
public:
  static constexpr std::size_t DefaultBufferSize = 260;

  std::wstring SearchText;
  bool MatchPath{false};
  bool MatchCase{false};
  bool MatchWholeWord{false};
  bool Regex{false};
  // 结果上限。0 表示由后端决定上限；Windows 上 Everything 的 IPC 会把 0 当成
  // “返回 0 条”，所以 0 实际按安全上限 200000 下发（与 POSIX 后端候选上限一致），
  // 避免一次拉取百万级结果把 Everything 进程撑爆。
  std::uint32_t Max{0};
  std::uint32_t Offset{0};
  EverythingSort Sort{EverythingSort::NameAscending};
  // 默认把 FileName / Attributes 一起请求回来：
  // 不请求 FileName 时 Everything_GetResultFileNameW 返回 NULL；
  // 不请求 Attributes 时返回 0xFFFFFFFF，调用方都无法区分“没值”和“没请求”。
  EverythingRequestFlags RequestFlags{
      EverythingRequestFlags::FileName |
      EverythingRequestFlags::FullPathAndFileName |
      EverythingRequestFlags::Path | EverythingRequestFlags::Extension |
      EverythingRequestFlags::Size | EverythingRequestFlags::DateModified |
      EverythingRequestFlags::DateCreated | EverythingRequestFlags::Attributes};
  EverythingError LastError{EverythingError::Ok};
  std::uint32_t TotalResults{0};
  std::uint32_t NumResults{0};

  EverythingClient() = default;
  ~EverythingClient() noexcept;

  EverythingClient(const EverythingClient &) = default;
  EverythingClient &operator=(const EverythingClient &) = default;
  EverythingClient(EverythingClient &&) noexcept = default;
  EverythingClient &operator=(EverythingClient &&) noexcept = default;

  std::vector<EverythingResult> Query();
  static std::vector<EverythingResult> Search(const std::wstring &searchText);
  static EverythingVersion GetVersion();
  static EverythingTargetMachine GetTargetMachine();
  static bool IsDBLoaded();
  static bool IsEverythingRunning();

  // 实际使用的搜索后端名称。Windows 返回 "Everything"；
  // POSIX 返回 "locate" / "fd" / "find" / "scan"（见 README 的 Linux 章节）。
  static std::wstring GetBackendName();

  // 本平台可用的搜索后端列表。Windows 只有 "Everything"；
  // POSIX 为 locate/fd/find/scan 中本机实际可用的子集（scan 始终可用）。
  static std::vector<std::wstring> AvailableBackends();

  // 手动指定后端，下一次 Query() 生效；空字符串表示自动（内置优先级）。
  // Windows 只接受空字符串或 "Everything"，其它值会被忽略。
  static void SetPreferredBackend(const std::wstring &name);

  // 当前手动指定的后端；空字符串表示自动。
  static std::wstring GetPreferredBackend();
};

std::wstring ToWideString(EverythingError error);
} // namespace Everything
} // namespace Lib
