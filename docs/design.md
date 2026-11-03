# Study Guardian 设计基线

来源：用户于 2026-11-02 提供的设计与实现方案。M1 已验收；用户于 2026-11-03 确认 M2 清单，随后授权按阶段继续推进至可发布版本。

## 产品

Windows 本地学习监督工具：本地监听当前窗口，依次使用规则、缓存、OpenAI 判断与学习目标的关系，按连续分心时长提醒和拦截。隐私默认仅进程名、标题和 URL，不截图、不读正文。不是安全软件，不阻止用户控制系统。

## 固定架构

- C# / .NET 8 / WPF；Win32 SetWinEventHook(EVENT_SYSTEM_FOREGROUND)、GetWindowText、GetWindowThreadProcessId、GetLastInputInfo。
- Core 纯逻辑，不引用 Windows UI 或原生调用，以 IClock 注入时间。
- Monitor 采集窗口和空闲；Data 使用 Microsoft.Data.Sqlite；App 负责 UI 和连接各模块。
- 后续 AI 使用 HttpClient / Responses API / strict json_schema，密钥使用 DPAPI；浏览器为 MV3 扩展，通过带令牌的 127.0.0.1 HTTP 服务通信。

## 里程碑

1. **M1**：前台窗口、空闲、SQLite。验收：切换窗口后能看到进程名、标题和停留时长。
2. M2：开始会话、本地规则、悬浮窗。
3. M3：AI 策略、判定、缓存、3 秒超时降级。
4. M4：0–10 秒宽限，10 秒提醒，40 秒警告，90 秒拦截，遮罩锁定 60 秒；假时钟测试、多显示器。
5. M5：浏览器扩展及配对；混合内容站点按 URL 或标题区分。
6. M6：10 分钟申诉放行、每会话 3 次 5 分钟应急通行、休息、报告、设置。
7. M7：可选严格模式、守护进程、自启、安装包。

## 运行规则（后续阶段遵守）

窗口停留不足 5 秒不判定；系统窗口暂停；规则→缓存→AI；失败或 uncertain 不触发拦截。辅助默认允许 5 分钟。30 分钟累计分心超过 5 分钟，下一次从 L2 开始；连续专注 20 秒等级清零。学习时键鼠不动不视作分心；超过 5 分钟无输入标记离开并暂停计时。

## M1 的具体约定

- 启动界面默认不记录；手动开始一次采集会话，手动停止或正常关闭时结算。
- 持续采集所有前台切换，包括短切换；5 秒防抖属于之后的“判定”，不丢弃原始采集。
- 每片段记录窗口句柄、进程 ID、进程名、标题、起止 UTC、单调时钟停留秒数、暂停原因；URL 暂为空。
- 系统窗口、自身窗口、读不到的窗口、锁屏、睡眠及超过阈值的空闲，不计入“有效记录”；它不等于“专注”。
- 空闲越过阈值时，按实际边界拆分片段；恢复输入后继续记录。键鼠输入仅取最后输入时间，不记录按键。
- 定时保存同一片段快照（按 ID 更新），降低异常关闭时丢失时长；切换、停止时立即保存。
- SQLite 扩充原 events 表的 end、duration_seconds、pause_reason 等字段。M1 verdict 为空、source 为 monitor，不伪造 AI 判定。
- 异常退出留下的会话在下次启动时标记 interrupted，结束点取最后成功写入的记录，不把关机时间算入。
- .NET SDK 和 NuGet 缓存放在本项目；数据保存在程序的 data 目录。用户批准生产依赖 Microsoft.Data.Sqlite 和 MSTest 测试依赖。

## 界面

中文；背景 #0F1115、卡片 #181B21、主字 #E8EAED、次字 #9AA0A6、绿 #34C77B、琥珀 #F5B841、红 #E5484D、按钮 #5B8DEF；Microsoft YaHei UI，数字 Consolas，圆角 12。提供开始/停止、当前窗口、停留时间、无输入时间、有效记录和最近记录。状态不能称为“专注率”，因为 M1 没有判断学习关联性。

用户后续明确要求：删除非必要解释文案，以状态、按钮、数字和空白状态表达功能。界面因此不显示口号、功能说明段落或常驻教学提示；只在错误时显示短提示。记录路径改为可点击的“记录文件”入口。

## M2 的具体约定

- 用户手动填写目标、时长、严格度及允许/辅助/禁止规则；不调用 OpenAI。
- 系统名单、自身 PID、不可读取的窗口为 neutral。其他输出 on_task、supportive、distracted 或 unknown。
- 匹配优先级 url > title_regex > domain > process；同级为禁止 > 允许 > 辅助。
- 普通窗口停留满 5 秒才判定；前 5 秒 unknown，不追溯。域名和 URL 需要真实 URL，M2 不猜测网址。
- 手动暂停、系统窗口、离开、锁屏及睡眠暂停倒计时；到时自动保存并结束。严格度保存但在 M2 不执行干预。
- 专注率为 on_task / (on_task + supportive + distracted) 的时长比例；排除 unknown、neutral 和暂停，无样本显示“—”。
- 绿/琥珀/红分别表示学习、辅助或未知、分心；暂停为灰。浮窗置顶、半透明、可拖动，提供暂停/继续和返回主窗口。
- 目标、严格度、时长与规则写 sessions；判定与来源写 events。原 M1 记录仍为 NULL/monitor。

## 技术资料

- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook （注册线程必须保持消息循环，保留委托引用）
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo （当前登录会话的输入时间）
- https://learn.microsoft.com/en-us/dotnet/core/install/windows （本地 SDK 同时包含桌面运行时）
