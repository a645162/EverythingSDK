# Everything SDK

This directory contains a repackaged Everything SDK workspace with managed wrappers, a native-friendly C++ wrapper, desktop examples, and tests.

## Layout

```text
native/
  Everything32.dll
  Everything64.dll
  EverythingARM.dll
  EverythingARM64.dll

src/
  libs/
    Lib.Everything.Framework/
    Lib.Everything.Net/
    Lib.Everything.VC/
  examples/
    Example.Everything.WinForms.Framework/
    Example.Everything.WinForms.Net/
    Example.Everything.WPF.Framework/
    Example.Everything.WPF.Net/
    Example.Everything.Win32.VC/

tests/
  Test.Everything.Framework/
  Test.Everything.Net/
  Test.Everything.VC/
```

## Libraries

- `Lib.Everything.Framework`
  - .NET Framework wrapper.
  - Keeps the public namespace and assembly name as `Lib.Everything` for compatibility.
  - Targets `net48`, so it remains Windows-only.
- `Lib.Everything.Net`
  - Multi-target .NET wrapper.
  - Current targets are `netstandard2.0`, `net6.0`, `net8.0`, and `net10.0`.
  - Keeps the public namespace and assembly name as `Lib.Everything`.
  - On Linux/macOS it uses the portable POSIX backend described below.
- `Lib.Everything.VC`
  - C++ wrapper for native and VC/Win32 consumers.
  - Minimum language level is C++11.
  - Public header: `src/libs/Lib.Everything.VC/include/Lib/Everything/Everything.h`.
  - On Windows it dynamically loads the matching native DLL at runtime.
  - On POSIX it compiles `src/everything_posix.cpp` plus `src/posix/` instead of the DLL binding.

## .NET usage

Both managed packages keep the same public namespace:

```csharp
using Lib.Everything;
```

That means callers can usually move from `Lib.Everything.Framework` to `Lib.Everything.Net` without changing business code, and only update the project reference or package source.

### Reference the project

For .NET Framework:

```xml
<ProjectReference Include="src\libs\Lib.Everything.Framework\Lib.Everything.Framework.csproj" />
```

For .NET 6 / 8 / 10:

```xml
<ProjectReference Include="src\libs\Lib.Everything.Net\Lib.Everything.Net.csproj" />
```

### Minimal search example

```csharp
using Lib.Everything;

using var client = new EverythingClient
{
    SearchText = "*.png",
    Max = 1000,
    Sort = EverythingSort.NameAscending
};

foreach (var item in client.Query())
{
    Console.WriteLine(item.FullPath);
}
```

### Common APIs

- `EverythingClient.SearchText`
- `EverythingClient.MatchCase`
- `EverythingClient.MatchWholeWord`
- `EverythingClient.Regex`
- `EverythingClient.MatchPath`
- `EverythingClient.Max`
- `EverythingClient.Offset`
- `EverythingClient.Sort`
- `EverythingClient.RequestFlags`
- `EverythingClient.Query()`
- `EverythingClient.GetVersion()`
- `EverythingClient.GetTargetMachine()`
- `EverythingClient.IsDBLoaded()`
- `EverythingClient.IsEverythingRunning()`
- `EverythingClient.GetBackendName()`

### Runtime note

The managed wrappers dynamically load the matching native DLL. In normal usage, keep the proper `Everything*.dll` beside the executable, beside the managed assembly, or provide it through a normal DLL search path.

## Examples

- `Example.Everything.WinForms.Framework`
- `Example.Everything.WinForms.Net`
- `Example.Everything.WPF.Framework`
- `Example.Everything.WPF.Net`
- `Example.Everything.Win32.VC`

The Win32 VC example mirrors the WPF example more closely than the initial prototype:

- search box + Enter to search
- match flags
- sort selection
- max result count
- result list
- log output
- double-click to open the selected result in Explorer

## Native DLL location

Native DLLs were moved to `native/` so they can be reused by non-.NET projects.

The VC wrapper looks for the proper `Everything*.dll` in this order:

1. `EVERYTHING_SDK_NATIVE_DIR`
2. the executable directory
3. `<exe-dir>/native`
4. normal Windows DLL search path

## Linux and other POSIX systems

Everything itself is Windows-only and ships no `Everything*.dll` for POSIX, so both
wrappers fall back to a portable backend on Linux and macOS. The public API is
unchanged: the same `EverythingClient`, the same properties, the same
`EverythingResult` fields.

### Backend selection

The backend is picked on every query, in this order:

1. `plocate` or `locate`, but only when a locate database actually exists.
2. `fd` (or `fdfind`, the binary name of the `fd-find` package on Debian/Ubuntu).
3. `find`.
4. In-process directory scan (always available, no external dependency).

An environment variable can pin the choice instead, and an unavailable forced
backend still falls back to the next one in the list.

The selected backend can be reported for diagnostics:

```csharp
Console.WriteLine(EverythingClient.GetBackendName()); // "locate" / "fd" / "find" / "scan"
```

```cpp
std::wcout << Lib::Everything::EverythingClient::GetBackendName() << L"\n";
```

On Windows the same call returns `"Everything"`.

### Listing and pinning backends from code

Both wrappers expose the same three calls, so a UI can render a backend picker
without shelling out or reading environment variables:

```csharp
string[] backends = EverythingClient.AvailableBackends(); // e.g. { "locate", "fd", "find", "scan" }
EverythingClient.SetPreferredBackend("fd");               // pin
EverythingClient.SetPreferredBackend(string.Empty);       // back to automatic
Console.WriteLine(EverythingClient.GetBackendName());     // what is actually in use
```

```cpp
for (const std::wstring& name : Lib::Everything::EverythingClient::AvailableBackends()) {
    std::wcout << name << L"\n";
}
Lib::Everything::EverythingClient::SetPreferredBackend(L"fd");
Lib::Everything::EverythingClient::SetPreferredBackend(L""); // automatic
```

Precedence is `explicit code preference` > `EVERYTHING_SDK_SEARCH_BACKEND` >
automatic detection. A pinned backend that is not installed falls back in a
fixed order instead of failing; `scan` is always available, so
`AvailableBackends()` is never empty.

### Environment variables

| Variable | Meaning |
| --- | --- |
| `EVERYTHING_SDK_SEARCH_BACKEND` | Pin `locate`, `fd`, `find`, or `scan`. Unknown values are ignored. |
| `EVERYTHING_SDK_LOCATE_DB` | Extra locate database path, tried before the standard locations. |
| `EVERYTHING_SDK_SEARCH_ROOTS` | Colon-separated search roots. Defaults to `$HOME`; use `/` to search everything. |
| `EVERYTHING_SDK_MAX_CANDIDATES` | Hard cap on collected candidate paths (default 200000), so a scan of `/` cannot exhaust memory. |

Roots are matched on path-component boundaries, so `/data` never matches `/database`.

### Supported query syntax

The POSIX backend implements the commonly used subset of the Everything grammar:

- `ext:png;jpg` — extension set (case-insensitive unless `MatchCase`).
- `ext:png;!jpg` style negation via `!term`.
- `path:"Program Files"` — substring match on the full path.
- `file:"IMG_0001"` — substring match on the file name.
- `size:>1mb` — size comparison; supports `<`, `<=`, `=`, `>=`, `>` and
  `b`/`kb`/`mb`/`gb`/`tb` units (binary, 1 kb = 1024 bytes).
- `size:1mb..2gb` — inclusive size range. Folders count as 0 bytes, like Everything.
- `dm:>=2024-01-01`, `dc:<2024-06-01 12:30`, `da:2024-01-01..2024-12-31` —
  modified / created / accessed time filters (`datemodified:`, `datecreated:`,
  `dateaccessed:` also work). A date without a time means the whole day.
- `content:"error"` — the file contains the text. Only the first 256 KiB are
  read, so this is a prefix match; quoting keeps spaces inside one term.
- `"quoted phrase"` — grouped phrase, spaces included.
- `*.png`, `IMG_????.jpg` — `*` and `?` wildcards, matched against the whole
  name (or whole path when `MatchPath` is set). `[` and `]` are literal.
- Bare words — substring match, honouring `MatchPath`, `MatchCase`, and
  `MatchWholeWord`.
- `Regex` — ECMAScript regex; an invalid pattern degrades to literal matching
  instead of failing the whole query.

`size:`/`dm:`/`dc:`/`da:`/`content:` need file metadata or content, so they are
applied after the backend has produced candidates. Everything filters without
a POSIX equivalent (`rc:`, file lists, `$` macros) are treated as plain text.
A filter whose value cannot be parsed (e.g. `size:abc`) also degrades to plain
text.

### Behaviour differences from Everything

- There is no live index. `locate` results reflect the last `updatedb` run;
  `fd` and `find` walk the roots on every query.
- `fd` respects its own defaults except that hidden files are included and ignore
  files are disabled; both are overridden explicitly so results match Everything.
- `GetVersion()` returns this SDK's own version, because there is no Everything
  service to ask. Use `GetBackendName()` for the actual backend.
- `IsEverythingRunning()` reports whether a usable backend exists, not whether
  a daemon is running. `IsDBLoaded()` reports whether a locate database was found.
- `DateCreated` comes from Linux `statx` birth time. Filesystems without birth
  time fall back to `ctime`, which is an approximation (metadata change time).
- `RunCount`, `DateRun`, `DateRecentlyChanged`, and file-list results are always
  empty. `IsVolumeResult()` is always `false`.
- `Attributes` exposes the POSIX mode bits, not Windows file attributes.
- Like the Windows wrapper, a client instance is not thread-safe.

### Build and test on POSIX

```bash
cmake -S . -B build -DEVERYTHINGSDK_BUILD_TESTS=ON \
      -DEVERYTHINGSDK_BUILD_WIN32_EXAMPLE=OFF -DCMAKE_BUILD_TYPE=Debug
cmake --build build -j
ctest --test-dir build --output-on-failure
```

```bash
dotnet build src/libs/Lib.Everything.Net/Lib.Everything.Net.csproj -c Debug
dotnet test tests/Test.Everything.Net/Test.Everything.Net.csproj -c Debug
```

`tests/Test.Everything.VC` only exists on non-Windows builds; the POSIX tests
force `EVERYTHING_SDK_SEARCH_BACKEND=scan` so they never depend on the contents
of the machine's locate database.

## Build

### Managed projects

```powershell
dotnet build .\EverythingSDK.sln -c Debug
dotnet test .\tests\Test.Everything.Framework\Test.Everything.Framework.csproj -c Debug
dotnet test .\tests\Test.Everything.Net\Test.Everything.Net.csproj -c Debug
```

Notes:

- The solution contains `net10.0` targets. Building those targets requires a .NET SDK that supports `.NET 10`.
- `Lib.Everything.Net` also produces a `netstandard2.0` assembly for older consumers; the test project runs on `net8.0`.
- `dotnet msbuild` is not suitable for the VC projects in this solution. Use Visual Studio or `MSBuild.exe` for `.vcxproj` builds.

If you only want the managed wrapper, you can build the individual project directly:

```powershell
dotnet build .\src\libs\Lib.Everything.Framework\Lib.Everything.Framework.csproj -c Debug
dotnet build .\src\libs\Lib.Everything.Net\Lib.Everything.Net.csproj -c Debug
```

### VC projects with Visual Studio / MSBuild

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  .\src\libs\Lib.Everything.VC\Lib.Everything.VC.vcxproj `
  /t:Build /p:Configuration=Debug /p:Platform=x64

& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  .\src\examples\Example.Everything.Win32.VC\Example.Everything.Win32.VC.vcxproj `
  /t:Build /p:Configuration=Debug /p:Platform=x64
```

## CMake

### Configure and build

```powershell
cmake -S . -B .\build\cmake-x64 -G "Visual Studio 17 2022" -A x64
cmake --build .\build\cmake-x64 --config Debug
```

### Install

```powershell
cmake --install .\build\cmake-x64 --config Debug --prefix .\build\install
```

This installs:

- `Lib.Everything.VC`
- public headers
- native DLLs
- exported CMake package files

### Consume with `find_package`

```cmake
find_package(EverythingSDK CONFIG REQUIRED)

add_executable(MyApp main.cpp)
target_link_libraries(MyApp PRIVATE EverythingSDK::Lib.Everything.VC)
```

The installed package also exposes:

- `EverythingSDK_NATIVE_X86`
- `EverythingSDK_NATIVE_X64`
- `EverythingSDK_NATIVE_ARM`
- `EverythingSDK_NATIVE_ARM64`
- `EverythingSDK_NATIVE_DIR`

## References

- https://www.voidtools.com/zh-cn/support/everything/sdk/
