# Runtime AI and rule selection implementation plan

> **For agentic workers:** Use superpowers:executing-plans task by task. Preserve the user's already-approved autonomous scope.

**Goal:** Make runtime AI operation observable and recoverable, and let users add rules by selecting open applications.

**Architecture:** Keep the existing dwell → rule → cache → Responses pipeline. Preserve safe fallback, expose its reason and retry deadline, and requeue the same eligible visit after backoff. Enumerate visible windows only when the user opens/refreshes the rule picker; convert them to explicit process or exact-title rules in Core.

**Tech Stack:** Existing .NET 8 / WPF / SQLite; no added dependencies.

**Spec:** `AGENTS.md`, `docs/design.md`, and the user's 2026-11-03 report about runtime AI and cumbersome manual rules.

## Global constraints

- Keep 5-second dwell, 3-second request timeout, configured 30-second failure backoff, protected windows, cancellation and privacy boundaries.
- Unknown, uncertain and fallback never enforce; completed local rules and cache skip API.
- No private activity or credentials in source, test output, commits or packages.
- Compact state labels/buttons; no explanatory UI paragraphs.
- Existing M7 portable directory contains user data: package into a new directory and preserve it.

## Review focus

- API cancellation after leaving a window must not poison backoff or apply stale results.
- Retrying fallback must settle time once and respect pause, system windows, overrides and new visits.
- Uncertain is a successful API reply, distinct from connection failure.
- Browser selection must never silently make a process-wide rule; literal title punctuation must be escaped.
- Settings connection test must use entered/saved key only in memory, stop on close and not save edits implicitly.

### Task 1: Runtime recovery and status

**Files:** Core/DecisionPipeline.cs, Core/StudySession.cs, App/SessionController.cs, App/ActivityRow.cs, App/FloatingWindow.xaml(.cs), App/MainWindow.xaml.cs; Core and Windows tests.

**Interfaces:** DecisionPipeline exposes attempt count, last failure and remaining backoff; StudySession exposes dwell remaining and RetryFallbackDecision(window,idle). SessionController accepts optional IStudyAi for runtime tests and exposes compact AI status/detail.

- [x] Add failing tests for failure reason/backoff, same-window retry, no stale application and controller calls after dwell.
- [x] Run focused tests: expect failure before implementation.
- [x] Implement recovery and status; preserve local/cache precedence and safe fallback.
- [x] Run focused tests: expect pass. Commit.

### Task 2: Select rules and test connection

**Files:** new Core/RuleCandidate.cs, Monitor/WindowCatalog.cs, App/RulePickerWindow.xaml(.cs); Monitor/NativeMethods.cs, App/RuleGroupControl.xaml.cs, App/SessionSetupControl.xaml.cs, App/SettingsWindow.xaml(.cs), App/SessionController.cs, App/MainWindow.xaml.cs; tests.

**Interfaces:** RuleCandidate.FromWindows(windows,protectedProcesses,browserProcesses,ownProcessId), WindowCatalog.ReadVisibleWindows(), RuleGroupControl.CandidateProvider. Connection test uses existing OpenAiStudyClient with synthetic metadata.

- [x] Add failing selection tests for deduplication, protected processes, exact browser title and URL choices.
- [x] Run focused tests: expect failure; implement picker and shared group integration.
- [x] Add compact settings connection-test control with cancellation and safe error display.
- [x] Run tests: expect pass. Commit.

### Task 3: Verify and deliver

- [x] Run build, .NET tests and extension tests; perform a bounded live API check using synthetic metadata only.
- [x] Obtain one fresh whole-branch review; fix important findings with regression tests.
- [x] Publish a clean M7.1 package, verify archive excludes data, document changed files and manual steps.
- [x] Commit and sync verified source to hana11000/StudyGuardian; point Start.cmd at updated local version, preserving user data separately from the archive.
