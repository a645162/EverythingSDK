# Everything SDK

这里整理的是一个重新组织过的 Everything SDK 工作区，包含托管封装、适合原生项目复用的 C++ 封装、桌面示例和测试项目。

## 目录结构

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

## 库项目

- `Lib.Everything.Framework`
  - .NET Framework 封装。
  - 为了兼容旧调用方，公开命名空间和程序集名仍然保持为 `Lib.Everything`。
- `Lib.Everything.Net`
  - 多目标 .NET 封装。
  - 当前目标框架为 `net6.0`、`net8.0`、`net10.0`。
  - 公开命名空间和程序集名同样保持为 `Lib.Everything`。
- `Lib.Everything.VC`
  - 面向 VC / Win32 / 原生 C++ 项目的封装。
  - 最低语言标准为 C++11。
  - 公共头文件位于 `src/libs/Lib.Everything.VC/include/Lib/Everything/Everything.h`。
  - 运行时动态加载对应架构的原生 DLL。

## .NET 用法

两个托管版本保持了相同的公开命名空间：

```csharp
using Lib.Everything;
```

这意味着调用方通常可以从 `Lib.Everything.Framework` 平滑切换到 `Lib.Everything.Net`，业务代码基本不用改，只需要调整项目引用来源。

### 项目引用

如果是 .NET Framework：

```xml
<ProjectReference Include="src\libs\Lib.Everything.Framework\Lib.Everything.Framework.csproj" />
```

如果是 .NET 6 / 8 / 10：

```xml
<ProjectReference Include="src\libs\Lib.Everything.Net\Lib.Everything.Net.csproj" />
```

### 最小搜索示例

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

### 常用 API

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

### 运行时说明

托管封装会在运行时动态加载对应架构的原生 DLL。通常只要把正确的 `Everything*.dll` 放在可执行文件目录、托管程序集目录，或者保证它位于正常 DLL 搜索路径中即可。

## 示例项目

- `Example.Everything.WinForms.Framework`
- `Example.Everything.WinForms.Net`
- `Example.Everything.WPF.Framework`
- `Example.Everything.WPF.Net`
- `Example.Everything.Win32.VC`

其中 `Example.Everything.Win32.VC` 现在已经补齐到接近 WPF 示例的功能：

- 搜索框，支持回车搜索
- 匹配选项
- 排序选择
- 最大结果数
- 结果列表
- 日志输出区
- 双击结果后通过 Explorer 打开

## 原生 DLL 位置

原来的原生 DLL 已经统一抽到 `native/`，方便 .NET 和 C++ 项目共同复用，也方便后续 C++ 项目直接引用。

VC 封装按以下顺序查找 `Everything*.dll`：

1. 环境变量 `EVERYTHING_SDK_NATIVE_DIR`
2. 可执行文件所在目录
3. `<exe-dir>/native`
4. Windows 默认 DLL 搜索路径

## 构建

### 托管项目

```powershell
dotnet build .\EverythingSDK.sln -c Debug
dotnet test .\tests\Test.Everything.Framework\Test.Everything.Framework.csproj -c Debug
dotnet test .\tests\Test.Everything.Net\Test.Everything.Net.csproj -c Debug
```

说明：

- 解决方案里包含 `net10.0` 目标，构建这些目标需要支持 `.NET 10` 的 SDK。
- `dotnet msbuild` 不适合构建本解决方案中的 VC 项目，`.vcxproj` 请使用 Visual Studio 或 `MSBuild.exe`。

如果你只想单独构建托管库，也可以直接构建对应项目：

```powershell
dotnet build .\src\libs\Lib.Everything.Framework\Lib.Everything.Framework.csproj -c Debug
dotnet build .\src\libs\Lib.Everything.Net\Lib.Everything.Net.csproj -c Debug
```

### 使用 Visual Studio / MSBuild 构建 VC 项目

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  .\src\libs\Lib.Everything.VC\Lib.Everything.VC.vcxproj `
  /t:Build /p:Configuration=Debug /p:Platform=x64

& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  .\src\examples\Example.Everything.Win32.VC\Example.Everything.Win32.VC.vcxproj `
  /t:Build /p:Configuration=Debug /p:Platform=x64
```

## CMake

### 配置与编译

```powershell
cmake -S . -B .\build\cmake-x64 -G "Visual Studio 17 2022" -A x64
cmake --build .\build\cmake-x64 --config Debug
```

### 安装

```powershell
cmake --install .\build\cmake-x64 --config Debug --prefix .\build\install
```

安装后会导出：

- `Lib.Everything.VC`
- 公共头文件
- 原生 DLL
- CMake 包配置与 targets 导出文件

### 通过 `find_package` 使用

```cmake
find_package(EverythingSDK CONFIG REQUIRED)

add_executable(MyApp main.cpp)
target_link_libraries(MyApp PRIVATE EverythingSDK::Lib.Everything.VC)
```

安装包还会导出以下变量：

- `EverythingSDK_NATIVE_X86`
- `EverythingSDK_NATIVE_X64`
- `EverythingSDK_NATIVE_ARM`
- `EverythingSDK_NATIVE_ARM64`
- `EverythingSDK_NATIVE_DIR`

## 参考文献

- https://www.voidtools.com/zh-cn/support/everything/sdk/
