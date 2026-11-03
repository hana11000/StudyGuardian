# M2 改动文件

基线：已验收 M1（3f8c33b）。

```text
AGENTS.md
README.md
Start.cmd
config/defaults.json
docs/design.md
docs/m2-manual-test.md
scripts/publish.ps1
src/StudyGuardian.App/ActivityRow.cs
src/StudyGuardian.App/App.xaml.cs
src/StudyGuardian.App/ConfigurationLoader.cs
src/StudyGuardian.App/FloatingWindow.xaml
src/StudyGuardian.App/FloatingWindow.xaml.cs
src/StudyGuardian.App/MainWindow.xaml
src/StudyGuardian.App/MainWindow.xaml.cs
src/StudyGuardian.App/RuleGroupControl.xaml
src/StudyGuardian.App/RuleGroupControl.xaml.cs
src/StudyGuardian.App/SessionController.cs
src/StudyGuardian.App/SessionSetupControl.xaml
src/StudyGuardian.App/SessionSetupControl.xaml.cs
src/StudyGuardian.App/Theme.xaml
src/StudyGuardian.Core/ActivityRecorder.cs
src/StudyGuardian.Core/ActivitySegment.cs
src/StudyGuardian.Core/IActivityRepository.cs
src/StudyGuardian.Core/RuleDecision.cs
src/StudyGuardian.Core/RuleEngine.cs
src/StudyGuardian.Core/StudyRule.cs
src/StudyGuardian.Core/StudySession.cs
src/StudyGuardian.Core/StudySessionOptions.cs
src/StudyGuardian.Core/StudySessionSnapshot.cs
src/StudyGuardian.Core/WindowSnapshot.cs
src/StudyGuardian.Data/SqliteActivityRepository.cs
tests/StudyGuardian.Core.Tests/RuleEngineTests.cs
tests/StudyGuardian.Core.Tests/SqliteActivityRepositoryTests.cs
tests/StudyGuardian.Core.Tests/StudySessionTests.cs
docs/m2-files.md
```

保留已有依赖及 SQLite schema；新增测试在 RuleEngineTests 和 StudySessionTests，数据库回归在原测试文件内。
