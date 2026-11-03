# M3 改动文件

基线：M2 公开提交 0ee7604cb20f9923c670c3721ad33d3db4594d65。

```text
config/defaults.json
docs/design.md
docs/m3-manual-test.md
docs/superpowers/plans/2026-11-03-m2.md
docs/superpowers/plans/2026-11-03-m3.md
README.md
scripts/publish.ps1
src/StudyGuardian.AI/OpenAiStudyClient.cs
src/StudyGuardian.AI/Schemas.cs
src/StudyGuardian.AI/StudyGuardian.AI.csproj
src/StudyGuardian.App/ActivityRow.cs
src/StudyGuardian.App/AiSettingsStore.cs
src/StudyGuardian.App/App.xaml.cs
src/StudyGuardian.App/ConfigurationLoader.cs
src/StudyGuardian.App/MainWindow.xaml
src/StudyGuardian.App/MainWindow.xaml.cs
src/StudyGuardian.App/RuleGroupControl.xaml.cs
src/StudyGuardian.App/SessionController.cs
src/StudyGuardian.App/SessionSetupControl.xaml
src/StudyGuardian.App/SessionSetupControl.xaml.cs
src/StudyGuardian.App/SettingsWindow.xaml
src/StudyGuardian.App/SettingsWindow.xaml.cs
src/StudyGuardian.App/StudyGuardian.App.csproj
src/StudyGuardian.Core/AiContracts.cs
src/StudyGuardian.Core/DecisionPipeline.cs
src/StudyGuardian.Core/RuleDecision.cs
src/StudyGuardian.Core/StudySession.cs
src/StudyGuardian.Core/StudySessionOptions.cs
src/StudyGuardian.Data/schema.sql
src/StudyGuardian.Data/SqliteActivityRepository.cs
src/StudyGuardian.Data/SqliteDecisionCache.cs
src/StudyGuardian.Monitor/ProtectedSecretStore.cs
Start.cmd
StudyGuardian.sln
tests/StudyGuardian.Core.Tests/AiTestOptions.cs
tests/StudyGuardian.Core.Tests/DecisionPipelineTests.cs
tests/StudyGuardian.Core.Tests/ExternalDecisionTests.cs
tests/StudyGuardian.Core.Tests/OpenAiStudyClientTests.cs
tests/StudyGuardian.Core.Tests/SqliteActivityRepositoryTests.cs
tests/StudyGuardian.Core.Tests/StudyGuardian.Core.Tests.csproj
tests/StudyGuardian.Windows.Tests/ProtectedSecretStoreTests.cs
tests/StudyGuardian.Windows.Tests/StudyGuardian.Windows.Tests.csproj
docs/m3-files.md
```

新增生产 NuGet 依赖：无。Windows 测试沿用已批准的 MSTest 与 Test SDK。
