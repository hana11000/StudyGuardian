# M8 正式界面改动文件

本清单基于已验收的主题基础版本，范围仅为正式 UI、相关验证和交付入口。

## 文件

| 文件 | 类别 |
| --- | --- |
| [README.md](../README.md) | 说明与计划 |
| [Start.cmd](../Start.cmd) | 发布与启动入口 |
| [docs/m8-production-files.md](../docs/m8-production-files.md) | 说明与计划 |
| [docs/m8-production-verification.md](../docs/m8-production-verification.md) | 说明与计划 |
| [docs/superpowers/plans/2026-11-03-m8-production-ui.md](../docs/superpowers/plans/2026-11-03-m8-production-ui.md) | 说明与计划 |
| [packaging/Install.ps1](../packaging/Install.ps1) | 发布与启动入口 |
| [scripts/publish.ps1](../scripts/publish.ps1) | 发布与启动入口 |
| [src/StudyGuardian.App/AppealWindow.cs](../src/StudyGuardian.App/AppealWindow.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/Controls/EdgeGlow.cs](../src/StudyGuardian.App/Controls/EdgeGlow.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/FocusRing.cs](../src/StudyGuardian.App/Controls/FocusRing.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/FrostWindow.cs](../src/StudyGuardian.App/Controls/FrostWindow.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/NoticePlacement.cs](../src/StudyGuardian.App/Controls/NoticePlacement.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/StatusLight.cs](../src/StudyGuardian.App/Controls/StatusLight.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/ThemeResources.cs](../src/StudyGuardian.App/Controls/ThemeResources.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/UiVisuals.cs](../src/StudyGuardian.App/Controls/UiVisuals.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/WindowHeader.xaml](../src/StudyGuardian.App/Controls/WindowHeader.xaml) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/WindowHeader.xaml.cs](../src/StudyGuardian.App/Controls/WindowHeader.xaml.cs) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Controls/WindowPlacement.cs](../src/StudyGuardian.App/Controls/WindowPlacement.cs) | 浮窗边界与显示器选择 |
| [src/StudyGuardian.App/FloatingWindow.xaml](../src/StudyGuardian.App/FloatingWindow.xaml) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/FloatingWindow.xaml.cs](../src/StudyGuardian.App/FloatingWindow.xaml.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/InterventionPresenter.cs](../src/StudyGuardian.App/InterventionPresenter.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/InterventionWindow.cs](../src/StudyGuardian.App/InterventionWindow.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/MainWindow.xaml](../src/StudyGuardian.App/MainWindow.xaml) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/MainWindow.xaml.cs](../src/StudyGuardian.App/MainWindow.xaml.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/Properties/AssemblyInfo.cs](../src/StudyGuardian.App/Properties/AssemblyInfo.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/ReportWindow.cs](../src/StudyGuardian.App/ReportWindow.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/RuleGroupControl.xaml](../src/StudyGuardian.App/RuleGroupControl.xaml) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/RuleGroupControl.xaml.cs](../src/StudyGuardian.App/RuleGroupControl.xaml.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/RulePickerWindow.xaml](../src/StudyGuardian.App/RulePickerWindow.xaml) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/SessionSetupControl.xaml](../src/StudyGuardian.App/SessionSetupControl.xaml) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/SettingsWindow.xaml](../src/StudyGuardian.App/SettingsWindow.xaml) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/StrictExitWindow.cs](../src/StudyGuardian.App/StrictExitWindow.cs) | 正式窗口与 UI 事件连接 |
| [src/StudyGuardian.App/Theme.xaml](../src/StudyGuardian.App/Theme.xaml) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Themes/Controls.xaml](../src/StudyGuardian.App/Themes/Controls.xaml) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Themes/Dark.xaml](../src/StudyGuardian.App/Themes/Dark.xaml) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Themes/Light.xaml](../src/StudyGuardian.App/Themes/Light.xaml) | 主题与 UI 专用控件 |
| [src/StudyGuardian.App/Themes/Tokens.xaml](../src/StudyGuardian.App/Themes/Tokens.xaml) | 主题与 UI 专用控件 |
| [src/StudyGuardian.UiPreview/App.xaml.cs](../src/StudyGuardian.UiPreview/App.xaml.cs) | 合成数据预览工具 |
| [src/StudyGuardian.UiPreview/ProductionPreview.cs](../src/StudyGuardian.UiPreview/ProductionPreview.cs) | 合成数据预览工具 |
| [tests/StudyGuardian.Windows.Tests/AmbientBackgroundTests.cs](../tests/StudyGuardian.Windows.Tests/AmbientBackgroundTests.cs) | UI 回归测试 |
| [tests/StudyGuardian.Windows.Tests/FloatingPresentationTests.cs](../tests/StudyGuardian.Windows.Tests/FloatingPresentationTests.cs) | UI 回归测试 |
| [tests/StudyGuardian.Windows.Tests/FocusRingTests.cs](../tests/StudyGuardian.Windows.Tests/FocusRingTests.cs) | UI 回归测试 |
| [tests/StudyGuardian.Windows.Tests/OverlayGeometryTests.cs](../tests/StudyGuardian.Windows.Tests/OverlayGeometryTests.cs) | UI 回归测试 |
| [tests/StudyGuardian.Windows.Tests/ProductionThemeTests.cs](../tests/StudyGuardian.Windows.Tests/ProductionThemeTests.cs) | UI 回归测试 |

## UI 接口

- `ThemeResources.Current.IsDark` / `SetDark(bool)`：为已打开的界面切换资源；不改写业务设置。
- `FrostWindow` 附加属性：背景材质、深色模式、状态、方角与原生裁切半径；`HasBackdrop` 决定回退背景。
- `UiVisuals`：状态颜色映射、卡片、圆角和出现动画；`StatusLight`、`EdgeGlow`、`FocusRing.Progress` 负责视觉。
- `NoticePlacement.Calculate`：按物理坐标将提醒卡片放入浮窗所在屏幕工作区。
- `FloatingWindow.EndRequested`：连接既有停止会话入口；其余已有事件与更新接口保留。
- `FloatingWindow.FocusControls`：用户明确打开浮窗时激活并进入键盘焦点；自动显示仍不抢焦点。`WindowPlacement` 按相交面积选择显示器，浮窗展开及拖动后保持在工作区内。
- `InterventionPresenter` 新增可选浮窗位置提供者与 `RefreshPlacement`；监控、判定、干预条件不变。
- `UiPreview --production-render <目录>`：渲染实际 UI 的合成数据样张；`--floating-smoke <目录>`：150 秒独立原生浮窗验证。
- `UiPreview --floating-interaction-smoke <目录>`：验证原生浮窗底部展开和键盘进入暂停按钮，不启动学习采集。

未改动 Core、Monitor、AI、Data、SessionController、ViewModel 和设置存储模型。MiSans、噪点图片和授权文件沿用已验收的主题基础。
