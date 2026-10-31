/*
 * File: Everything.cpp
 * Brief: Everything.
 * Author: Haomin Kong
 * Email: a645162@163.com
 * Created: 2026-07-31
 */

#include "Lib/Everything/Everything.h"

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <Windows.h>
#include <TlHelp32.h>

#include <cwchar>
#include <cstring>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace {
using SetSearchFn = void(WINAPI *)(LPCWSTR);
using SetBoolFn = void(WINAPI *)(BOOL);
using SetDwordFn = void(WINAPI *)(DWORD);
using QueryFn = BOOL(WINAPI *)(BOOL);
using GetDwordFn = DWORD(WINAPI *)();
using GetBoolFn = BOOL(WINAPI *)();
using GetResultStringFn = LPCWSTR(WINAPI *)(DWORD);
using GetResultFullPathNameFn = DWORD(WINAPI *)(DWORD, LPWSTR, DWORD);
using GetResultSizeFn = BOOL(WINAPI *)(DWORD, LARGE_INTEGER *);
using GetResultFileTimeFn = BOOL(WINAPI *)(DWORD, FILETIME *);
using GetResultAttributesFn = DWORD(WINAPI *)(DWORD);
using GetResultRunCountFn = DWORD(WINAPI *)(DWORD);
using GetResultTypeFn = BOOL(WINAPI *)(DWORD);
using ResetFn = void(WINAPI *)();
using CleanUpFn = void(WINAPI *)();

std::wstring GetDllFileNameForCurrentProcess() {
#if defined(_M_IX86)
  return L"Everything32.dll";
#elif defined(_M_X64)
  return L"Everything64.dll";
#elif defined(_M_ARM)
  return L"EverythingARM.dll";
#elif defined(_M_ARM64)
  return L"EverythingARM64.dll";
#else
#error Unsupported architecture.
#endif
}

std::wstring GetEnvironmentVariableValue(const wchar_t *name) {
  const DWORD length = GetEnvironmentVariableW(name, nullptr, 0);
  if (length == 0) {
    return {};
  }

  std::wstring buffer(length, L'\0');
  const DWORD written = GetEnvironmentVariableW(name, &buffer[0], length);
  if (written == 0) {
    return {};
  }

  buffer.resize(written);
  return buffer;
}

std::wstring GetExecutableDirectory() {
  std::wstring buffer(MAX_PATH, L'\0');

  for (;;) {
    const DWORD written = GetModuleFileNameW(nullptr, &buffer[0],
                                             static_cast<DWORD>(buffer.size()));
    if (written == 0) {
      throw std::runtime_error(
          "Failed to resolve the current executable path.");
    }

    if (written < buffer.size() - 1) {
      buffer.resize(written);
      const std::wstring::size_type separator = buffer.find_last_of(L"\\/");
      if (separator == std::wstring::npos) {
        return L".";
      }

      return buffer.substr(0, separator);
    }

    buffer.resize(buffer.size() * 2);
  }
}

std::wstring CombinePath(const std::wstring &left, const std::wstring &right) {
  if (left.empty()) {
    return right;
  }

  const wchar_t last = left[left.size() - 1];
  if (last == L'\\' || last == L'/') {
    return left + right;
  }

  return left + L'\\' + right;
}

std::vector<std::wstring> CollectCandidateDirectories() {
  std::vector<std::wstring> directories;

  const std::wstring envDirectory =
      GetEnvironmentVariableValue(L"EVERYTHING_SDK_NATIVE_DIR");
  if (!envDirectory.empty()) {
    directories.emplace_back(envDirectory);
  }

  const std::wstring executableDirectory = GetExecutableDirectory();
  directories.push_back(executableDirectory);
  directories.push_back(CombinePath(executableDirectory, L"native"));

  return directories;
}

HMODULE LoadEverythingModule() {
  const std::wstring dllName = GetDllFileNameForCurrentProcess();

  for (const auto &directory : CollectCandidateDirectories()) {
    const std::wstring candidate = CombinePath(directory, dllName);
    const HMODULE module = LoadLibraryW(candidate.c_str());
    if (module != nullptr) {
      return module;
    }
  }

  const HMODULE module = LoadLibraryW(dllName.c_str());
  if (module != nullptr) {
    return module;
  }

  throw std::runtime_error("Failed to load the Everything SDK native library.");
}

std::wstring PtrToString(const wchar_t *value) {
  return value != nullptr ? std::wstring(value) : std::wstring{};
}

bool TryConvertFileTimeToTimePoint(
    const FILETIME &fileTime, std::chrono::system_clock::time_point &output) {
  ULARGE_INTEGER fileTimeValue{};
  fileTimeValue.LowPart = fileTime.dwLowDateTime;
  fileTimeValue.HighPart = fileTime.dwHighDateTime;

  if (fileTimeValue.QuadPart == 0) {
    return false;
  }

  constexpr std::int64_t WindowsToUnixEpochTicks = 11644473600LL * 10000000LL;
  const auto ticks = static_cast<std::int64_t>(fileTimeValue.QuadPart);
  if (ticks < WindowsToUnixEpochTicks) {
    return false;
  }

  using FileTimeDuration =
      std::chrono::duration<std::int64_t, std::ratio<1, 10000000>>;
  const auto unixDuration = FileTimeDuration(ticks - WindowsToUnixEpochTicks);
  output = std::chrono::system_clock::time_point(
      std::chrono::duration_cast<std::chrono::system_clock::duration>(
          unixDuration));
  return true;
}

class NativeApi final {
public:
  static NativeApi &Instance() {
    static NativeApi instance;
    return instance;
  }

  NativeApi(const NativeApi &) = delete;
  NativeApi &operator=(const NativeApi &) = delete;

  ~NativeApi() noexcept {
    if (module_ != nullptr) {
      FreeLibrary(module_);
    }
  }

  void Reset() const noexcept { everythingReset_(); }

  void CleanUp() const noexcept { everythingCleanUp_(); }

  void SetSearch(const std::wstring &searchText) const {
    everythingSetSearchW_(searchText.c_str());
  }

  void SetMatchPath(const bool enabled) const noexcept {
    everythingSetMatchPath_(enabled ? TRUE : FALSE);
  }

  void SetMatchCase(const bool enabled) const noexcept {
    everythingSetMatchCase_(enabled ? TRUE : FALSE);
  }

  void SetMatchWholeWord(const bool enabled) const noexcept {
    everythingSetMatchWholeWord_(enabled ? TRUE : FALSE);
  }

  void SetRegex(const bool enabled) const noexcept {
    everythingSetRegex_(enabled ? TRUE : FALSE);
  }

  void SetMax(const std::uint32_t value) const noexcept {
    everythingSetMax_(value);
  }

  void SetOffset(const std::uint32_t value) const noexcept {
    everythingSetOffset_(value);
  }

  void SetSort(const std::uint32_t value) const noexcept {
    everythingSetSort_(value);
  }

  void SetRequestFlags(const std::uint32_t value) const noexcept {
    everythingSetRequestFlags_(value);
  }

  bool Query(const bool wait) const noexcept {
    return everythingQueryW_(wait ? TRUE : FALSE) != FALSE;
  }

  std::uint32_t GetLastError() const noexcept {
    return everythingGetLastError_();
  }

  std::uint32_t GetTotalResults() const noexcept {
    return everythingGetTotResults_();
  }

  std::uint32_t GetNumResults() const noexcept {
    return everythingGetNumResults_();
  }

  bool IsVolumeResult(const std::uint32_t index) const noexcept {
    return everythingIsVolumeResult_(index) != FALSE;
  }

  bool IsFolderResult(const std::uint32_t index) const noexcept {
    return everythingIsFolderResult_(index) != FALSE;
  }

  bool IsFileResult(const std::uint32_t index) const noexcept {
    return everythingIsFileResult_(index) != FALSE;
  }

  const wchar_t *GetResultFileName(const std::uint32_t index) const noexcept {
    return everythingGetResultFileNameW_(index);
  }

  const wchar_t *GetResultPath(const std::uint32_t index) const noexcept {
    return everythingGetResultPathW_(index);
  }

  std::uint32_t
  GetResultFullPathName(const std::uint32_t index, wchar_t *buffer,
                        const std::uint32_t bufferSize) const noexcept {
    return everythingGetResultFullPathNameW_(index, buffer, bufferSize);
  }

  const wchar_t *GetResultExtension(const std::uint32_t index) const noexcept {
    return everythingGetResultExtensionW_(index);
  }

  bool GetResultSize(const std::uint32_t index,
                     LARGE_INTEGER &value) const noexcept {
    return everythingGetResultSize_(index, &value) != FALSE;
  }

  bool GetResultDateCreated(const std::uint32_t index,
                            FILETIME &value) const noexcept {
    return everythingGetResultDateCreated_(index, &value) != FALSE;
  }

  bool GetResultDateModified(const std::uint32_t index,
                             FILETIME &value) const noexcept {
    return everythingGetResultDateModified_(index, &value) != FALSE;
  }

  bool GetResultDateAccessed(const std::uint32_t index,
                             FILETIME &value) const noexcept {
    return everythingGetResultDateAccessed_(index, &value) != FALSE;
  }

  std::uint32_t GetResultAttributes(const std::uint32_t index) const noexcept {
    return everythingGetResultAttributes_(index);
  }

  const wchar_t *
  GetResultFileListFileName(const std::uint32_t index) const noexcept {
    return everythingGetResultFileListFileNameW_(index);
  }

  std::uint32_t GetResultRunCount(const std::uint32_t index) const noexcept {
    return everythingGetResultRunCount_(index);
  }

  bool GetResultDateRun(const std::uint32_t index,
                        FILETIME &value) const noexcept {
    return everythingGetResultDateRun_(index, &value) != FALSE;
  }

  bool GetResultDateRecentlyChanged(const std::uint32_t index,
                                    FILETIME &value) const noexcept {
    return everythingGetResultDateRecentlyChanged_(index, &value) != FALSE;
  }

  std::uint32_t GetMajorVersion() const noexcept {
    return everythingGetMajorVersion_();
  }

  std::uint32_t GetMinorVersion() const noexcept {
    return everythingGetMinorVersion_();
  }

  std::uint32_t GetRevision() const noexcept {
    return everythingGetRevision_();
  }

  std::uint32_t GetBuildNumber() const noexcept {
    return everythingGetBuildNumber_();
  }

  std::uint32_t GetTargetMachine() const noexcept {
    return everythingGetTargetMachine_();
  }

  bool IsDBLoaded() const noexcept { return everythingIsDBLoaded_() != FALSE; }

private:
  NativeApi() : module_(LoadEverythingModule()) {
    Bind(everythingSetSearchW_, "Everything_SetSearchW");
    Bind(everythingSetMatchPath_, "Everything_SetMatchPath");
    Bind(everythingSetMatchCase_, "Everything_SetMatchCase");
    Bind(everythingSetMatchWholeWord_, "Everything_SetMatchWholeWord");
    Bind(everythingSetRegex_, "Everything_SetRegex");
    Bind(everythingSetMax_, "Everything_SetMax");
    Bind(everythingSetOffset_, "Everything_SetOffset");
    Bind(everythingSetSort_, "Everything_SetSort");
    Bind(everythingSetRequestFlags_, "Everything_SetRequestFlags");
    Bind(everythingQueryW_, "Everything_QueryW");
    Bind(everythingGetLastError_, "Everything_GetLastError");
    Bind(everythingGetTotResults_, "Everything_GetTotResults");
    Bind(everythingGetNumResults_, "Everything_GetNumResults");
    Bind(everythingIsVolumeResult_, "Everything_IsVolumeResult");
    Bind(everythingIsFolderResult_, "Everything_IsFolderResult");
    Bind(everythingIsFileResult_, "Everything_IsFileResult");
    Bind(everythingGetResultFileNameW_, "Everything_GetResultFileNameW");
    Bind(everythingGetResultPathW_, "Everything_GetResultPathW");
    Bind(everythingGetResultFullPathNameW_,
         "Everything_GetResultFullPathNameW");
    Bind(everythingGetResultExtensionW_, "Everything_GetResultExtensionW");
    Bind(everythingGetResultSize_, "Everything_GetResultSize");
    Bind(everythingGetResultDateCreated_, "Everything_GetResultDateCreated");
    Bind(everythingGetResultDateModified_, "Everything_GetResultDateModified");
    Bind(everythingGetResultDateAccessed_, "Everything_GetResultDateAccessed");
    Bind(everythingGetResultAttributes_, "Everything_GetResultAttributes");
    Bind(everythingGetResultFileListFileNameW_,
         "Everything_GetResultFileListFileNameW");
    Bind(everythingGetResultRunCount_, "Everything_GetResultRunCount");
    Bind(everythingGetResultDateRun_, "Everything_GetResultDateRun");
    Bind(everythingGetResultDateRecentlyChanged_,
         "Everything_GetResultDateRecentlyChanged");
    Bind(everythingReset_, "Everything_Reset");
    Bind(everythingCleanUp_, "Everything_CleanUp");
    Bind(everythingGetMajorVersion_, "Everything_GetMajorVersion");
    Bind(everythingGetMinorVersion_, "Everything_GetMinorVersion");
    Bind(everythingGetRevision_, "Everything_GetRevision");
    Bind(everythingGetBuildNumber_, "Everything_GetBuildNumber");
    Bind(everythingGetTargetMachine_, "Everything_GetTargetMachine");
    Bind(everythingIsDBLoaded_, "Everything_IsDBLoaded");
  }

  template <typename T> void Bind(T &slot, const char *exportName) {
    const FARPROC proc = GetProcAddress(module_, exportName);
    if (proc == nullptr) {
      throw std::runtime_error("Failed to resolve an Everything SDK export.");
    }

    slot = reinterpret_cast<T>(proc);
  }

  HMODULE module_{};
  SetSearchFn everythingSetSearchW_{};
  SetBoolFn everythingSetMatchPath_{};
  SetBoolFn everythingSetMatchCase_{};
  SetBoolFn everythingSetMatchWholeWord_{};
  SetBoolFn everythingSetRegex_{};
  SetDwordFn everythingSetMax_{};
  SetDwordFn everythingSetOffset_{};
  SetDwordFn everythingSetSort_{};
  SetDwordFn everythingSetRequestFlags_{};
  QueryFn everythingQueryW_{};
  GetDwordFn everythingGetLastError_{};
  GetDwordFn everythingGetTotResults_{};
  GetDwordFn everythingGetNumResults_{};
  GetResultTypeFn everythingIsVolumeResult_{};
  GetResultTypeFn everythingIsFolderResult_{};
  GetResultTypeFn everythingIsFileResult_{};
  GetResultStringFn everythingGetResultFileNameW_{};
  GetResultStringFn everythingGetResultPathW_{};
  GetResultFullPathNameFn everythingGetResultFullPathNameW_{};
  GetResultStringFn everythingGetResultExtensionW_{};
  GetResultSizeFn everythingGetResultSize_{};
  GetResultFileTimeFn everythingGetResultDateCreated_{};
  GetResultFileTimeFn everythingGetResultDateModified_{};
  GetResultFileTimeFn everythingGetResultDateAccessed_{};
  GetResultAttributesFn everythingGetResultAttributes_{};
  GetResultStringFn everythingGetResultFileListFileNameW_{};
  GetResultRunCountFn everythingGetResultRunCount_{};
  GetResultFileTimeFn everythingGetResultDateRun_{};
  GetResultFileTimeFn everythingGetResultDateRecentlyChanged_{};
  ResetFn everythingReset_{};
  CleanUpFn everythingCleanUp_{};
  GetDwordFn everythingGetMajorVersion_{};
  GetDwordFn everythingGetMinorVersion_{};
  GetDwordFn everythingGetRevision_{};
  GetDwordFn everythingGetBuildNumber_{};
  GetDwordFn everythingGetTargetMachine_{};
  GetBoolFn everythingIsDBLoaded_{};
};

std::wstring GetFullPath(const NativeApi &api, const std::uint32_t index) {
  std::wstring buffer(Lib::Everything::EverythingClient::DefaultBufferSize,
                      L'\0');
  std::uint32_t needed = api.GetResultFullPathName(
      index, &buffer[0], static_cast<std::uint32_t>(buffer.size()));
  if (needed > buffer.size()) {
    buffer.assign(needed, L'\0');
    needed = api.GetResultFullPathName(
        index, &buffer[0], static_cast<std::uint32_t>(buffer.size()));
  }

  if (needed == 0) {
    return {};
  }

  const std::size_t actualLength = ::wcsnlen(buffer.c_str(), buffer.size());
  buffer.resize(actualLength);
  return buffer;
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

EverythingClient::~EverythingClient() noexcept {
  try {
    NativeApi::Instance().CleanUp();
  } catch (...) {}
}

std::vector<EverythingResult> EverythingClient::Query() {
  auto &api = NativeApi::Instance();

  api.Reset();
  api.SetSearch(SearchText);
  api.SetMatchPath(MatchPath);
  api.SetMatchCase(MatchCase);
  api.SetMatchWholeWord(MatchWholeWord);
  api.SetRegex(Regex);
  api.SetMax(Max);
  api.SetOffset(Offset);
  api.SetSort(static_cast<std::uint32_t>(Sort));
  api.SetRequestFlags(static_cast<std::uint32_t>(RequestFlags));

  if (!api.Query(true)) {
    LastError = static_cast<EverythingError>(api.GetLastError());
    throw std::runtime_error("Everything query failed.");
  }

  LastError = EverythingError::Ok;
  TotalResults = api.GetTotalResults();
  NumResults = api.GetNumResults();

  std::vector<EverythingResult> results;
  results.reserve(NumResults);

  for (std::uint32_t index = 0; index < NumResults; ++index) {
    EverythingResult result;
    result.Index = index;
    result.FileName = PtrToString(api.GetResultFileName(index));
    result.Path = PtrToString(api.GetResultPath(index));
    result.FullPath = GetFullPath(api, index);
    result.Extension = PtrToString(api.GetResultExtension(index));
    result.FileListFileName = PtrToString(api.GetResultFileListFileName(index));
    result.Attributes = api.GetResultAttributes(index);
    result.RunCount = api.GetResultRunCount(index);
    result.IsVolume = api.IsVolumeResult(index);
    result.IsFolder = api.IsFolderResult(index);
    result.IsFile = api.IsFileResult(index);

    if (HasFlag(RequestFlags, EverythingRequestFlags::Size)) {
      LARGE_INTEGER size{};
      if (api.GetResultSize(index, size)) {
        result.HasSize = true;
        result.Size = size.QuadPart;
      }
    }

    if (HasFlag(RequestFlags, EverythingRequestFlags::DateCreated)) {
      FILETIME fileTime{};
      if (api.GetResultDateCreated(index, fileTime)) {
        result.HasDateCreated =
            TryConvertFileTimeToTimePoint(fileTime, result.DateCreated);
      }
    }

    if (HasFlag(RequestFlags, EverythingRequestFlags::DateModified)) {
      FILETIME fileTime{};
      if (api.GetResultDateModified(index, fileTime)) {
        result.HasDateModified =
            TryConvertFileTimeToTimePoint(fileTime, result.DateModified);
      }
    }

    if (HasFlag(RequestFlags, EverythingRequestFlags::DateAccessed)) {
      FILETIME fileTime{};
      if (api.GetResultDateAccessed(index, fileTime)) {
        result.HasDateAccessed =
            TryConvertFileTimeToTimePoint(fileTime, result.DateAccessed);
      }
    }

    if (HasFlag(RequestFlags, EverythingRequestFlags::DateRun)) {
      FILETIME fileTime{};
      if (api.GetResultDateRun(index, fileTime)) {
        result.HasDateRun =
            TryConvertFileTimeToTimePoint(fileTime, result.DateRun);
      }
    }

    if (HasFlag(RequestFlags, EverythingRequestFlags::DateRecentlyChanged)) {
      FILETIME fileTime{};
      if (api.GetResultDateRecentlyChanged(index, fileTime)) {
        result.HasDateRecentlyChanged =
            TryConvertFileTimeToTimePoint(fileTime, result.DateRecentlyChanged);
      }
    }

    results.push_back(std::move(result));
  }

  return results;
}

std::vector<EverythingResult>
EverythingClient::Search(const std::wstring &searchText) {
  EverythingClient client;
  client.SearchText = searchText;
  return client.Query();
}

EverythingVersion EverythingClient::GetVersion() {
  auto &api = NativeApi::Instance();

  // EverythingVersion 的成员带默认成员初始化器（C++11 NSDMI），
  // 在 C++11 下就不再是聚合类型，不能用花括号聚合初始化，只能逐个赋值。
  EverythingVersion version;
  version.Major = api.GetMajorVersion();
  version.Minor = api.GetMinorVersion();
  version.Revision = api.GetRevision();
  version.Build = api.GetBuildNumber();
  return version;
}

EverythingTargetMachine EverythingClient::GetTargetMachine() {
  return static_cast<EverythingTargetMachine>(
      NativeApi::Instance().GetTargetMachine());
}

bool EverythingClient::IsDBLoaded() {
  return NativeApi::Instance().IsDBLoaded();
}

std::wstring EverythingClient::GetBackendName() { return L"Everything"; }

std::vector<std::wstring> EverythingClient::AvailableBackends() {
  // Windows 上只有 Everything 一个后端，没有可切换的项。
  return {L"Everything"};
}

void EverythingClient::SetPreferredBackend(const std::wstring &name) {
  // 唯一后端没有「切换到别处」的语义；非法值同样忽略。
  (void)name;
}

std::wstring EverythingClient::GetPreferredBackend() { return L"Everything"; }

bool EverythingClient::IsEverythingRunning() {
  HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
  if (snapshot == INVALID_HANDLE_VALUE) {
    return false;
  }

  PROCESSENTRY32W entry{};
  entry.dwSize = sizeof(entry);

  bool found = false;
  if (Process32FirstW(snapshot, &entry) != FALSE) {
    do {
      if (_wcsicmp(entry.szExeFile, L"Everything.exe") == 0 ||
          _wcsicmp(entry.szExeFile, L"Everything") == 0) {
        found = true;
        break;
      }
    } while (Process32NextW(snapshot, &entry) != FALSE);
  }

  CloseHandle(snapshot);
  return found;
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
