# Study Guardian

Windows 学习专注工具。当前交付阶段：**M2：开始会话、本地规则、悬浮窗**。

## 使用

双击根目录 `Start.cmd`，或 `dist/StudyGuardian-M2/StudyGuardian.App.exe`。

1. 填写学习目标，选择 25/50/90 分钟或自定义时长、严格度。
2. 在“允许 / 辅助 / 禁止”中点 `+` 添加规则，点标签的 `×` 删除。
3. 点 **开始专注**。主窗口收起，悬浮窗显示状态、剩余时间和专注率。
4. 悬浮窗的暂停按钮可暂停/继续，`↗` 返回主窗口；主窗口可结束会话。
5. 点 **活动记录** 查看最近记录，点 **记录文件** 打开数据库所在目录。

发布目录附带运行时，无需另装 .NET。移动时请保留整个发布文件夹。旧 M1 目录和数据保留在原处，不自动搬迁。

## 规则

| 类型 | 示例 | 匹配方式 |
| --- | --- | --- |
| 进程 | `Notion.exe` | 忽略大小写，可省略 `.exe` |
| 域名 | `notion.so` | 本域及其子域 |
| 网址 | `https://example.com/learn?q=tax` | 精确路径和查询，忽略 URL 片段 |
| 标题正则 | `增值税\|进项税` | 对窗口标题进行正则匹配，忽略大小写 |

系统窗口和本程序始终暂停。其他规则优先级为：网址 > 标题正则 > 域名 > 进程；同级禁止 > 允许 > 辅助。

M2 尚无网址采集，域名/网址规则已实现并测试，实际窗口 URL 为空时不会命中。浏览器可使用标题正则。本阶段严格度只保存选择，不执行提醒或拦截。

普通窗口停留 5 秒后判定；之前显示“待判定”。绿色表示专注，琥珀色表示辅助或待判定，红色表示分心。暂停为灰色。无匹配保持 unknown，不擅自当作分心。

专注率 = 专注时长 /（专注 + 辅助 + 分心时长），不包括未知、系统窗口和暂停；没有已判定时间时显示“—”。手动暂停、锁屏、睡眠、系统窗口及空闲超过 5 分钟后不扣除会话时间。

## 数据

SQLite 保存在对应程序目录的 `data/study-guardian.db`。开发运行的数据位于 `bin/.../data`。

- 会话保存目标、时长、严格度和本次规则；事件保存窗口、停留、暂停原因、判定和来源。
- 每 15 秒保存当前片段，切换、结束和正常关闭时收尾。异常退出后，下次启动将未结束会话标记为 interrupted。
- M1 的旧事件保持 `verdict=NULL`、`source=monitor`；M2 为 `rule` 或 `fallback`。
- M2 不发送活动信息到网络，不截图、不读取正文、不记录按键内容。
- 数据、SDK、缓存和发布文件不进入 Git 提交。

```sql
SELECT ts, process, title, verdict, source, duration_seconds, pause_reason
FROM events ORDER BY ts DESC LIMIT 20;
SELECT goal, policy_json, strictness, status FROM sessions ORDER BY start DESC;
```

## 开发

固定 .NET 8 + WPF + Microsoft.Data.Sqlite；使用 MSTest。新机器执行：

```powershell
.\scripts\setup.ps1
.\scripts\dotnet.ps1 restore StudyGuardian.sln
.\scripts\dotnet.ps1 build StudyGuardian.sln -c Release
.\scripts\dotnet.ps1 test StudyGuardian.sln -c Release
.\scripts\publish.ps1
```

SDK 和缓存位于项目目录，不修改系统 PATH。受限环境可能需要允许测试宿主的本机 IPC。构建前关闭正在运行的同目录程序；发布包另放在 `dist`。

阈值、系统名单、默认时长和浮窗透明度位于 `config/defaults.json`。修改源码配置后重新发布，或在程序关闭时修改发布目录内的配置。

## 验证和后续开发

[M2 手动验收](docs/m2-manual-test.md) · [M2 改动文件](docs/m2-files.md) · [设计](docs/design.md) · [开发约束](AGENTS.md)

用户已授权后续按 M3–M7 逐阶段推进。当前 M2 不包含 OpenAI、提醒/拦截、扩展、申诉或严格模式。

BoardUI MCP 已在开发机安装并验证。它的 React/Tailwind 组件不直接用于 WPF；本项目参考布局层级，使用原生 WPF 实现，未添加 UI 框架依赖。
