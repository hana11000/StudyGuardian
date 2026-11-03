# M1 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** 可启动的 Windows 程序记录前台软件、标题、停留与空闲，并保存 SQLite。

**Architecture:** Core 生成有确定起止与暂停原因的活动片段；Monitor 提供 Win32 事件和空闲数据；WPF 在消息循环线程协调采集，串行保存到 SQLite，显示记录。系统锁定和休眠使计时暂停。

**Tech Stack:** .NET 8、WPF、Microsoft.Data.Sqlite 8.0.33、MSTest 3.11.1、Microsoft.NET.Test.Sdk 17.14.1。

**Spec:** docs/design.md；AGENTS.md。

## Global Constraints

- 只做 M1，Core 不引用 WPF/Win32，阈值与名单只在 config/defaults.json。
- 不截图、不读正文、不联网发送活动、不干预其他进程。
- 用户已于本轮批准开始；按已确认清单直接实施，不重复请求确认。

## Review Focus

- 时钟校正、短切换、重复事件：停留不为负、不双计；Core 假时钟验证。
- 跨越空闲阈值、离开后恢复：准确拆分，恢复后继续；Core 边界验证。
- 进程已退出、读取被拒绝、空句柄：显示不可读并暂停，不崩溃；Monitor 保守处理与人工验收。
- 写入失败或异常关闭：明确报错并停止采集；SQLite 恢复未结束会话验证。
- 自己/系统窗口、锁屏、睡眠：保存但暂停，不误归于之前的软件；Core 暂停测试及 Windows 手动验收。

## Task 1: 环境与时间记录

**Files:** config/defaults.json；src/StudyGuardian.Core/*；tests/StudyGuardian.Core.Tests/*；解决方案和构建脚本。

**Interfaces:** IClock.UtcNow、Elapsed；WindowSnapshot；ActivitySegment；ActivityRecorder.Start(sessionId, window, idle)、Observe(window, idle)、Suspend(reason)、Resume(window, idle)、Checkpoint()、Stop()。Observe 等返回需要保存的片段；所有调用在同一协调线程。

- [x] 准备项目内 SDK，校验官方下载 SHA-512，创建解决方案。
- [x] 先写测试并观察失败：切换 12 秒、重复事件、停止最后一段、299/300/301 秒、恢复输入、系统窗口和单调时钟。
- [x] 实现最小时间记录与配置校验，运行测试。

## Task 2: 持久化

**Files:** src/StudyGuardian.Data/SqliteActivityRepository.cs、schema.sql；tests/StudyGuardian.Core.Tests/SqliteActivityRepositoryTests.cs。

**Interfaces:** IActivityRepository.InitializeAsync、StartSessionAsync、SaveSegmentAsync、EndSessionAsync、ReadRecentAsync。

- [x] 使用临时真实 SQLite 写失败测试：中文/引号标题往返、同片段更新、会话隔离、未结束会话恢复。
- [x] 实现参数化 SQL、事务和单写入者，运行测试。

## Task 3: Windows 采集与界面

**Files:** src/StudyGuardian.Monitor/*；src/StudyGuardian.App/*；scripts/*；README.md。

**Interfaces:** IForegroundWindowMonitor.Current、Changed、Start、Stop、Dispose；IIdleTimeProvider.GetIdleTime；WindowSnapshot 原生句柄只作为纯数据穿过 Core。

- [x] 实现前台与标题事件监听、原生空闲检测；保留回调、同线程卸载钩子。
- [x] WPF 按钮开启/停止，定时器仅查询空闲与保存快照，不轮询窗口；锁屏/睡眠暂停。
- [ ] 首次显示/停止/关闭生命周期验收，数据库故障可见且停止采集。
- [x] 运行完整 dotnet build、dotnet test、发布便于直接启动的本地版本。
- [ ] 试运行 WPF，核对真正窗口切换和 SQLite 行，写手动验收步骤和改动文件。

## Execution Notes

- 工作区初始为空且无 Git 仓库；直接在用户指定目录实现，不创建无意义的工作树或提交。
- 运行数据和开发工具已排除；保留 docs/m1-progress.md 记录验证结果。
- 用户按 Esc 停止桌面操作后，不再进行 GUI 自动交互；实际切换、锁屏和睡眠验收留作手动步骤。初始窗口启动成功，视觉检查发现并修复根窗口未套用深色样式。
