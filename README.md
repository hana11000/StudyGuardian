# Study Guardian

Windows 学习专注工具。当前交付阶段：**M6：申诉、应急通行、休息、报告和设置**。

## 使用

双击根目录 `Start.cmd`，或 `dist/StudyGuardian-M6/StudyGuardian.App.exe`。

1. 填写学习目标，选择 25/50/90 分钟或自定义时长、严格度。
2. 在“允许 / 辅助 / 禁止”中点 `+` 添加规则，点标签的 `×` 删除；配置 API 后也可点“生成规则”，检查并修改标签，再开始。
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

浏览器安装并配对 [MV3 扩展](extension/README.md) 后，当前标签 URL 用于域名/网址规则和 AI。未配对或标题无法严格对应时，URL 为空，仍可使用标题正则或 AI。扩展不读取正文、不进入无痕窗口。

普通窗口停留 5 秒后判定；之前显示“待判定”。绿色表示专注，琥珀色表示辅助、待判定或初级提醒，红色表示警告或拦截。暂停为灰色。依次使用本地规则、缓存、AI；未启用 AI 时，无匹配保持 unknown。

标准模式连续分心 10 秒提醒、40 秒警告、90 秒遮罩；温和为 1.5 倍、严格为 0.5 倍。遮罩普通关闭锁定 60 秒，“回到学习”始终可用；找不到有效学习窗口时回主界面。30 分钟累计分心超过 5 分钟，下一轮从警告开始；连续专注 20 秒清零当前等级。同一辅助窗口超时后从提醒开始。系统窗口、暂停、未知、uncertain 和 API 失败都不会触发干预。

## 浏览器配对

桌面设置点击“复制配对码”，浏览器加载发布目录中的 extension，在扩展配对页输入端口和配对码。默认端口 18731，只监听 127.0.0.1；端口冲突时本地会话仍可使用。未开始、手动暂停、锁屏或离开时，扩展只检查状态，不读标签。配置改动后重新发布并重新加载扩展。桌面“重置”立即废止旧配对码。

拦截命令在执行前核对完整 URL（包括 # 路由）和当前/待导航标签；网页网址变化会重新防抖，AI 缓存也区分路由。本地手写 URL 规则仍按表中方式忽略片段。

## AI 设置

点右上角齿轮，输入自己的 API Key 和模型，保存。默认模型在 `config/defaults.json`，可在设置中修改。密钥用当前 Windows 用户的 DPAPI 加密到 `data/api-key.dpapi`，界面不回显已有密钥；清除后重新保存即可移除。

AI 通过 Responses API 的 strict JSON Schema 返回结果。每次调用最多等待 3 秒；超时、断网、拒答或无效返回降级为 supportive/fallback，不触发后续拦截。uncertain 和低信心结论也不触发拦截。缓存按目标、规则和模型隔离，混合网站只按具体 URL 或标题复用；切换窗口后旧回复会丢弃。

专注率 = 专注时长 /（专注 + 辅助 + 分心时长），不包括未知、系统窗口和暂停；没有已判定时间时显示“—”。手动暂停、锁屏、睡眠、系统窗口及空闲超过 5 分钟后不扣除会话时间。

## 休息、申诉和报告

运行中可休息 5 分钟，期间停止前台监听和标签上报，学习倒计时暂停；可提前结束。每会话有 3 次应急通行，每次 5 分钟。申诉需要配置 AI，明确通过后只放行对应进程与完整网址/标题 10 分钟；到期重新判定。临时放行不触发辅助超时。遮罩锁定期间申诉和应急入口不可用，回到学习始终可用。

会话结束自动显示报告，也可用“报告”选择历史会话。时间轴区分专注、辅助、分心和暂停，辅助超出上限的部分计入分心。新会话保存当次阈值和实际等级清零边界；旧记录缺少阈值时采用默认值。

设置中可调整干预秒数、全局规则、提示音，以及是否向 AI 发送窗口标题和网址。设置保存在 data/preferences.json。

## 数据

SQLite 保存在对应程序目录的 `data/study-guardian.db`。开发运行的数据位于 `bin/.../data`。

- 会话保存目标、时长、严格度和本次规则；事件保存窗口、停留、暂停原因、判定、来源和干预等级 level。
- 每 15 秒保存当前片段，切换、结束和正常关闭时收尾。异常退出后，下次启动将未结束会话标记为 interrupted。
- M1 的旧事件保持 `verdict=NULL`、`source=monitor`；当前来源为 `rule`、`cache`、`llm`、`fallback` 或 `override`。
- 未配置或关闭 AI 时无 API 请求。启用后，仅将目标、规则和进程名/标题/URL、近期判定及停留时长发送给 OpenAI，设置 `store=false`；不截图、不读取正文、不记录按键内容。
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

[M6 手动验收](docs/m6-manual-test.md) · [M6 改动文件](docs/m6-files.md) · [M5 手动验收](docs/m5-manual-test.md) · [M5 改动文件](docs/m5-files.md) · [M4 手动验收](docs/m4-manual-test.md) · [M3 手动验收](docs/m3-manual-test.md) · [设计](docs/design.md) · [开发约束](AGENTS.md)

用户已授权后续逐阶段推进。严格退出/守护模式留给 M7。尚未提供真实 API Key，在线调用待人工验收。M6 的 174 项 .NET 测试和 11 项扩展测试通过。真实浏览器加载、在线判定、多显示器/多 DPI 和遮罩按钮交互保留人工验收。

BoardUI MCP 已在开发机安装并验证。它的 React/Tailwind 组件不直接用于 WPF；本项目参考布局层级，使用原生 WPF 实现，未添加 UI 框架依赖。
