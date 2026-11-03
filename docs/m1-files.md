# M1 改动文件

初始工作区为空，以下均为本轮新建文件；本轮内也已修正独立检查发现的计时问题，并按用户要求精简界面文案。

## 工程、约束和说明

- `AGENTS.md`
- `.gitignore`
- `Directory.Build.props`
- `global.json`
- `NuGet.Config`
- `StudyGuardian.sln`
- `README.md`
- `Start.cmd`
- `config/defaults.json`
- `docs/design.md`
- `docs/m1-plan.md`
- `docs/m1-progress.md`
- `docs/m1-files.md`
- `scripts/dotnet.ps1`
- `scripts/setup.ps1`
- `scripts/publish.ps1`

## 纯逻辑

- `src/StudyGuardian.Core/StudyGuardian.Core.csproj`
- `src/StudyGuardian.Core/IClock.cs`
- `src/StudyGuardian.Core/SystemClock.cs`
- `src/StudyGuardian.Core/WindowSnapshot.cs`
- `src/StudyGuardian.Core/ActivitySegment.cs`
- `src/StudyGuardian.Core/ActivityRecorder.cs`
- `src/StudyGuardian.Core/SystemPauseState.cs`
- `src/StudyGuardian.Core/IForegroundWindowMonitor.cs`
- `src/StudyGuardian.Core/IActivityRepository.cs`

## Windows 采集

- `src/StudyGuardian.Monitor/StudyGuardian.Monitor.csproj`
- `src/StudyGuardian.Monitor/NativeMethods.cs`
- `src/StudyGuardian.Monitor/ForegroundWindowMonitor.cs`
- `src/StudyGuardian.Monitor/IdleTimeProvider.cs`

## SQLite

- `src/StudyGuardian.Data/StudyGuardian.Data.csproj`
- `src/StudyGuardian.Data/SqliteActivityRepository.cs`
- `src/StudyGuardian.Data/schema.sql`

## WPF 界面

- `src/StudyGuardian.App/StudyGuardian.App.csproj`
- `src/StudyGuardian.App/app.manifest`
- `src/StudyGuardian.App/App.xaml`
- `src/StudyGuardian.App/App.xaml.cs`
- `src/StudyGuardian.App/ConfigurationLoader.cs`
- `src/StudyGuardian.App/Theme.xaml`
- `src/StudyGuardian.App/MainWindow.xaml`
- `src/StudyGuardian.App/MainWindow.xaml.cs`
- `src/StudyGuardian.App/ActivityRow.cs`

## 自动测试

- `tests/StudyGuardian.Core.Tests/StudyGuardian.Core.Tests.csproj`
- `tests/StudyGuardian.Core.Tests/FakeClock.cs`
- `tests/StudyGuardian.Core.Tests/ActivityRecorderTests.cs`
- `tests/StudyGuardian.Core.Tests/SqliteActivityRepositoryTests.cs`
- `tests/StudyGuardian.Core.Tests/SystemPauseStateTests.cs`

## 本机产物与另行授权的安装

- `dist/StudyGuardian-M1/`：包含 .NET 运行时的最新可运行程序，目录整体保留。`dist/StudyGuardian/` 为早期预览，因文件被占用而保留。
- `.tools/`：本地 .NET SDK、CLI 工作目录、BoardUI 握手检查脚本；`.nuget/`：依赖缓存。
- `.artifacts/`：构建、测试、发布与 BoardUI 验证结果；均不作为源代码提交。
- `C:/Users/10032/.codex/config.toml`：通过 `codex mcp add` 新增 `boardui` 服务器配置。
- `C:/Users/10032/.codex/tools/boardui/`：npx 支持工具及 BoardUI 下载缓存。

没有初始化 Git 仓库、创建分支、提交或推送。
