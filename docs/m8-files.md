# M8 第一步改动文件

本轮共 26 个文件；业务功能不变。新主题只由独立 UiPreview 加载。

| 文件 | 用途 |
| --- | --- |
| `StudyGuardian.sln` | 注册独立预览项目 |
| `src/StudyGuardian.App/StudyGuardian.App.csproj` | 嵌入字体与颗粒，随包复制许可 |
| `src/StudyGuardian.App/Themes/Tokens.xaml` | 字号、间距、圆角、字体、材质、动效参数 |
| `src/StudyGuardian.App/Themes/Light.xaml` | 浅色配色与阴影 |
| `src/StudyGuardian.App/Themes/Dark.xaml` | 深色配色与阴影 |
| `src/StudyGuardian.App/Themes/Controls.xaml` | 卡片、按钮、胶囊选择、标签、输入框、下拉框、勾选框 |
| `src/StudyGuardian.App/Controls/AmbientBackground.xaml` | 三团模糊光与缓存层 |
| `src/StudyGuardian.App/Controls/AmbientBackground.xaml.cs` | 四种状态、颜色过渡、动画启停及系统动画偏好 |
| `src/StudyGuardian.App/Controls/PillBorder.cs` | 按控件高度形成胶囊圆角 |
| `src/StudyGuardian.App/Assets/Textures/noise-128.png` | 128×128 平铺颗粒 |
| `src/StudyGuardian.App/Assets/Fonts/MiSans-Regular.ttf` | 官方原版常规字体 |
| `src/StudyGuardian.App/Assets/Fonts/MiSans-Semibold.ttf` | 官方原版半粗字体 |
| `src/StudyGuardian.App/Assets/Fonts/LICENSE.pdf` | 官方字体许可 |
| `src/StudyGuardian.App/Assets/Fonts/NOTICE.md` | 字体来源与版权说明 |
| `src/StudyGuardian.UiPreview/StudyGuardian.UiPreview.csproj` | 独立 WPF 演示程序 |
| `src/StudyGuardian.UiPreview/App.xaml` | 预览程序入口 |
| `src/StudyGuardian.UiPreview/App.xaml.cs` | 交互启动、仅渲染预览内容的导图模式 |
| `src/StudyGuardian.UiPreview/DemoWindow.xaml` | 主题、状态光和全部基础控件样本 |
| `src/StudyGuardian.UiPreview/DemoWindow.xaml.cs` | 预览专用的主题切换、状态切换、拖动及窗口动效 |
| `scripts/preview-ui.ps1` | 构建并打开预览，或生成 PNG |
| `tests/StudyGuardian.Windows.Tests/AmbientBackgroundTests.cs` | 动画生命周期、主题隔离、焦点与文字对比度回归 |
| `tests/StudyGuardian.Windows.Tests/ExitWorkflowTests.cs` | 仅将已有 UI 测试调度器开放为 internal 供同项目复用 |
| `docs/m8-theme-design.md` | 本轮设计范围 |
| `docs/m8-verification.md` | 验证结果与手动测试步骤 |
| `docs/m8-files.md` | 本清单 |
| `docs/superpowers/plans/2026-11-03-m8-theme-foundation.md` | 实施计划 |

UI 接口：`AmbientState` 提供 `Focus/Warn/Block/Idle`；`AmbientBackground` 提供 `State`、`IsDark`、`MotionEnabled` 依赖属性与只读 `IsAnimating` 依赖属性。资源名均使用 `M8.` 前缀，主题字典在预览窗口局部加载。
