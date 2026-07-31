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
```

## Libraries

- `Lib.Everything.Framework`
  - .NET Framework wrapper.
  - Keeps the public namespace and assembly name as `Lib.Everything` for compatibility.
- `Lib.Everything.Net`
  - Multi-target .NET wrapper.
  - Current targets are `net6.0`, `net8.0`, and `net10.0`.
  - Keeps the public namespace and assembly name as `Lib.Everything`.
- `Lib.Everything.VC`
  - C++ wrapper for native and VC/Win32 consumers.
  - Minimum language level is C++11.
  - Public header: `src/libs/Lib.Everything.VC/include/Lib/Everything/Everything.h`.
  - Dynamically loads the matching native DLL at runtime.

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

## Build

### Managed projects

```powershell
dotnet build .\EverythingSDK.sln -c Debug
dotnet test .\tests\Test.Everything.Framework\Test.Everything.Framework.csproj -c Debug
dotnet test .\tests\Test.Everything.Net\Test.Everything.Net.csproj -c Debug
```

Notes:

- The solution contains `net10.0` targets. Building those targets requires a .NET SDK that supports `.NET 10`.
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
