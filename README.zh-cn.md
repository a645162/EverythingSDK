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
  Test.Everything.VC/
```

## 库项目

- `Lib.Everything.Framework`
  - .NET Framework 封装。
  - 为了兼容旧调用方，公开命名空间和程序集名仍然保持为 `Lib.Everything`。
  - 目标框架是 `net48`，因此仍然只适用于 Windows。
- `Lib.Everything.Net`
  - 多目标 .NET 封装。
  - 当前目标框架为 `netstandard2.0`、`net6.0`、`net8.0`、`net10.0`。
  - 公开命名空间和程序集名同样保持为 `Lib.Everything`。
  - 在 Linux / macOS 上自动使用下文介绍的可移植 POSIX 后端。
- `Lib.Everything.VC`
  - 面向 VC / Win32 / 原生 C++ 项目的封装。
  - 最低语言标准为 C++11。
  - 公共头文件位于 `src/libs/Lib.Everything.VC/include/Lib/Everything/Everything.h`。
  - Windows 下运行时动态加载对应架构的原生 DLL。
  - POSIX 下编译 `src/everything_posix.cpp` 与 `src/posix/`，不加载任何 DLL。

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
- `EverythingClient.GetBackendName()`

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

## Linux 及其它 POSIX 系统

Everything 本身只支持 Windows，也没有提供 POSIX 版 `Everything*.dll`，因此两个封装在
Linux / macOS 上会自动改用可移植后端。公开 API 完全不变：同一个 `EverythingClient`、
同样的属性、同样的 `EverythingResult` 字段。

### 后端选择

后端在每次查询时确定，优先级如下：

1. `plocate` / `locate`，但仅当本机确实存在 locate 数据库时。
2. `fd`（Debian/Ubuntu 上 `fd-find` 包的可执行文件叫 `fdfind`，两个名字都会探测）。
3. `find`。
4. 进程内目录扫描（永远可用，不依赖外部命令）。

也可以用环境变量固定选择；被固定的后端不可用时会继续按顺序回退。

可用下面的调用查看当前实际后端，便于排查问题：

```csharp
Console.WriteLine(EverythingClient.GetBackendName()); // "locate" / "fd" / "find" / "scan"
```

```cpp
std::wcout << Lib::Everything::EverythingClient::GetBackendName() << L"\n";
```

Windows 上同一个调用返回 `"Everything"`。

### 在代码里列出并固定后端

两个封装的调用完全对称，界面可以直接据此做一个后端选择器，
不需要去读环境变量：

```csharp
string[] backends = EverythingClient.AvailableBackends(); // 例如 { "locate", "fd", "find", "scan" }
EverythingClient.SetPreferredBackend("fd");               // 手动指定
EverythingClient.SetPreferredBackend(string.Empty);       // 恢复自动
Console.WriteLine(EverythingClient.GetBackendName());     // 当前真正生效的后端
```

```cpp
for (const std::wstring& name : Lib::Everything::EverythingClient::AvailableBackends()) {
    std::wcout << name << L"\n";
}
Lib::Everything::EverythingClient::SetPreferredBackend(L"fd");
Lib::Everything::EverythingClient::SetPreferredBackend(L""); // 自动
```

优先级：**代码里的显式偏好 > `EVERYTHING_SDK_SEARCH_BACKEND` > 自动探测**。
指定的后端本机没装时不会直接失败，而是按固定顺序回退；`scan` 永远可用，
因此 `AvailableBackends()` 至少有一项。

### 环境变量

| 变量 | 含义 |
| --- | --- |
| `EVERYTHING_SDK_SEARCH_BACKEND` | 固定使用 `locate`、`fd`、`find` 或 `scan`；无法识别的值会被忽略。 |
| `EVERYTHING_SDK_LOCATE_DB` | 额外的 locate 数据库路径，优先于标准位置。 |
| `EVERYTHING_SDK_SEARCH_ROOTS` | 以冒号分隔的搜索根目录，默认 `$HOME`；填 `/` 表示全盘。 |
| `EVERYTHING_SDK_MAX_CANDIDATES` | 候选路径上限（默认 200000），避免全盘扫描把内存吃光。 |

根目录按路径分量边界匹配，`/data` 不会误匹配到 `/database`。

### 支持的查询语法

POSIX 后端实现了 Everything 语法中常用的子集：

- `ext:png;jpg` —— 扩展名集合（除非 `MatchCase`，否则大小写不敏感）。
- `!term` —— 排除条件，例如 `ext:png !screenshot`。
- `path:"Program Files"` —— 完整路径包含。
- `file:"IMG_0001"` —— 文件名包含。
- `"带空格的短语"` —— 引号分组。
- `*.png`、`IMG_????.jpg` —— `*` 与 `?` 通配符，匹配整个文件名（`MatchPath` 打开时匹配整个路径）；`[` 与 `]` 按字面量处理。
- 裸词 —— 子串匹配，遵循 `MatchPath`、`MatchCase`、`MatchWholeWord`。
- `Regex` —— ECMAScript 正则；非法正则退化为字面量匹配，而不是让整次查询失败。

Everything 中在 POSIX 上没有对应语义的高级过滤词（`size:`、`dm:`、`rc:`、
`content:`、文件列表、`$` 宏等）会按普通文本处理。

### 与 Everything 的行为差异

- 没有实时索引。`locate` 结果反映上一次 `updatedb` 的快照；`fd` 与 `find` 每次查询都会实际遍历。
- `fd` 除了显式包含隐藏项、禁用 ignore 文件外，其余仍遵循 fd 自身默认行为；这两个开关必须与 Everything 语义对齐。
- `GetVersion()` 返回本 SDK 自身版本（没有 Everything 服务可问），实际后端请用 `GetBackendName()`。
- `IsEverythingRunning()` 表示后端是否可用，而不是常驻进程是否存在；`IsDBLoaded()` 表示是否找到 locate 数据库。
- `DateCreated` 取自 Linux `statx` 的 birth time；文件系统不支持时回退到 `ctime`，这是一个近似值（元数据变更时间）。
- `RunCount`、`DateRun`、`DateRecentlyChanged` 以及文件列表结果始终为空，`IsVolumeResult()` 始终为 `false`。
- `Attributes` 暴露的是 POSIX 模式位，不是 Windows 文件属性。
- 与 Windows 版本一样，单个客户端实例不是线程安全的。

### POSIX 上的构建与测试

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

`tests/Test.Everything.VC` 只在非 Windows 构建中存在；POSIX 测试会强制
`EVERYTHING_SDK_SEARCH_BACKEND=scan`，因此不依赖本机 locate 数据库的内容。

## 构建

### 托管项目

```powershell
dotnet build .\EverythingSDK.sln -c Debug
dotnet test .\tests\Test.Everything.Framework\Test.Everything.Framework.csproj -c Debug
dotnet test .\tests\Test.Everything.Net\Test.Everything.Net.csproj -c Debug
```

说明：

- 解决方案里包含 `net10.0` 目标，构建这些目标需要支持 `.NET 10` 的 SDK。
- `Lib.Everything.Net` 还会产出 `netstandard2.0` 程序集以兼容较老的调用方；测试项目仍跑在 `net8.0` 上。
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
