# M8 第一步验证与试用

日期：2026-11-03。交付范围为主题、基础控件、弥散背景和独立演示窗口。

## 已完成验证

- `scripts/dotnet.ps1 build StudyGuardian.sln -c Release --no-restore`：0 警告，0 错误。
- `scripts/dotnet.ps1 test StudyGuardian.sln -c Release --no-build --no-restore`：202 项通过，其中 Core/AI/Data 178 项，Windows 24 项。
- 新增 5 项 UI 回归覆盖：动画关闭/隐藏/卸载后停止并能恢复；局部主题切换不影响原程序；动画停止可被依赖界面实时观察；浅深主题下选中胶囊有独立键盘焦点环；输入框选中文字对比度至少 4.5:1。
- 复核发现的后三项先用测试复现失败，再修复并全部通过；复核为一次，修复为一次。
- 独立 `win-x64` 自包含预览发布成功；运行包内导图模式生成 8 张主题/状态组合图，以及 960×720 小尺寸图，实际检查浅色专注、深色拦截及小尺寸布局。
- 导图读取 WPF 自身的合成内容，不捕获桌面。`render-info.json` 验证 Regular/Semibold 均来自打包的 MiSans 资源。
- 预览包包含字体许可与版权说明，运行导图后未创建 `data` 目录。

本地记录位于 `.artifacts/m8/`：`build.log`、`tests.log`、`review-red.log`、`review-green.log`、`publish.log` 及 `preview/`。这些产物与正式程序数据均不提交。

## 打开预览

本机直接打开 `dist/StudyGuardian-M8-Preview/StudyGuardian.UiPreview.exe`。

从源码构建并打开：

```powershell
.\scripts\preview-ui.ps1
```

只生成图片：

```powershell
.\scripts\preview-ui.ps1 -Render
```

生成自包含演示包：

```powershell
.\scripts\dotnet.ps1 publish src/StudyGuardian.UiPreview/StudyGuardian.UiPreview.csproj -c Release -r win-x64 --self-contained true -o dist/StudyGuardian-M8-Preview
```

## 手动测试

1. 切换右上角“浅色 / 深色”，检查输入框、按钮、下拉菜单和文字同步换色。
2. 依次点击下方“专注 / 提醒 / 拦截 / 休息”，检查背景光、小圆点和状态文字随之切换。
3. 勾选“动态光”，观察光团缓慢漂移及专注圆点呼吸；取消后应静止。最小化后恢复，动态光应能继续。系统关闭动画效果时应停止动画。
4. 用 Tab 移到时长胶囊，再用方向键切换；选中项应有与填充分开的焦点环。编辑目标和标签样本，全选文字，检查浅深主题下选中文字清晰。
5. 打开严格度下拉框、切换勾选框；“已暂停”按钮保持禁用。
6. 拖动标题区域移动窗口，点击最小化后从任务栏恢复；关闭预览后正式学习程序不受影响。

目标、规则标签、计时数字和“开始专注”等均为样式样本；本演示不启动学习会话。系统 Mica/Acrylic、正式悬浮窗、提醒遮罩和正式窗口迁移在视觉确认后实施。悬浮窗空闲 CPU < 1% 的指标留待实际悬浮窗实现后测量。

改动文件见 [m8-files.md](m8-files.md)。
