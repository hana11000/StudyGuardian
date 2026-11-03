# M8 Production UI Implementation Plan

> Use superpowers:executing-plans inline, then one fresh whole-branch review.

**Goal:** Apply the approved Quiet Frost design to the actual study workflow in the requested window order.
**Architecture:** WPF resources, window chrome and UI controls adapt existing session/controller outputs. Existing inputs and handlers stay connected. No changes to Core, Monitor, AI, Data, SessionController or settings storage models.
**Tech Stack:** Existing .NET 8 WPF; DWM through UI-only P/Invoke; no new dependencies.
**Spec:** User's Quiet Frost design, explicit visual approval on 2026-11-03, AGENTS.md, docs/m8-theme-design.md.

## Global Constraints

- Warm light default; approved light/dark tokens, black/white/gray controls, state color in light and indicators. Packaged MiSans and tabular Segoe UI digits.
- Preserve goal, duration, strictness, rule picker, AI generation, pause/break/end, appeal, emergency and report behavior.
- Floating capsule about 260×44, hover expansion 300ms, idle opacity 50% after 120s, breathing 4s; UI constants in Tokens.xaml.
- L1 attached below the floating window; L2 inward peach edge light and floating countdown; L3 full-monitor material with a permanently available return button and existing unlock restrictions.
- No desktop pixels captured, following AGENTS.md. Use system Acrylic for background blur; unsupported systems use tinted ambient surfaces.
- Main/native dialogs Mica; transient floating/notice/block Acrylic where supported. Fall back cleanly on unsupported systems.
- No real keys or activity in screenshots, tests, source or packages. Production visual renders use synthetic data without showing native interception surfaces.
- One milestone, no extra explanatory UI copy, no new NuGet packages, no monitoring on startup.

## Review Focus

- Native chrome preserves close/resize and modal ownership; unsupported DWM never leaves transparent unreadable content.
- Theme changes preserve in-progress goal/rules and update existing controls/dialogs.
- Floating controls remain reachable with mouse and keyboard, timers stop when hidden/closed, AI status remains available.
- Warning/notice positions remain in the correct monitor work area; return/appeal/emergency gating is unchanged.
- Reports handle empty, null focus rate, full focus and many timeline slices without invalid geometry or stale async results.

### Task 1: Shared chrome and actual start screen

**Files:** Theme.xaml; Themes/{Tokens,Light,Dark,Controls}.xaml; Controls/{ThemeResources,FrostWindow,WindowHeader,UiVisuals}.cs or xaml; MainWindow.xaml(.cs); SessionSetupControl.xaml; RuleGroupControl.xaml(.cs); RulePickerWindow.xaml(.cs); tests/StudyGuardian.Windows.Tests/ProductionThemeTests.cs.
**Interfaces:** ThemeResources.Current.IsDark + SetDark(bool); FrostWindow attached Material/State/HasBackdrop; existing setup and rule interfaces retained.

- [x] Regression: changing theme preserves typed goal and generated policy; existing controls and legacy brushes update. Run focused Windows tests before/after.
- [x] Implement shared Mica/chrome, dynamic theme resources, single glass setup card, capsule choices and rule tags with existing picker.
- [x] Verify build and current Windows regression suite; commit task.

### Task 2: Floating and intervention surfaces

**Files:** FloatingWindow.xaml(.cs); InterventionWindow.cs; InterventionPresenter.cs; Controls/{StatusLight,EdgeGlow,NoticePlacement}.cs or xaml; MainWindow UI event wiring; Windows tests.
**Interfaces:** Existing Update signatures retained; EndRequested wired to existing stop handler; optional notice anchor supplied by floating window; UI-only State mapping from existing snapshots.

- [x] Tests: reminder/warning countdown and neutral state mapping; placement around screen edges/negative monitor coordinates; return controls and locked tools remain gated.
- [x] Build 260×44 capsule with hover/focus controls and idle fade; stop UI timers/animations when hidden. Keep AI detail in expanded panel/tooltip.
- [x] Replace border/banner with soft edge light and compact anchored card; full monitor Acrylic block with large lock timer and return button. Keep interception policy intact.
- [x] Run focused tests and build; commit task.

### Task 3: Reports, settings and delivery

**Files:** ReportWindow.cs; SettingsWindow.xaml; AppealWindow.cs; StrictExitWindow.cs; Controls/FocusRing.cs; UiPreview production render support; UI tests; docs/m8-production-{files,verification}.md; publish/launch paths.
**Interfaces:** Existing report loader and settings handlers retained; FocusRing.Progress coerced to [0,1]; synthetic rendering loads production UI only.

- [x] Ring geometry covers zero/full/null values; settings controls keep names and handlers; restyle dialog surfaces using shared resources.
- [x] Render production start, floating, warning/block, report and settings from synthetic inputs; inspect light/dark and compact layouts.
- [x] Run dotnet build + dotnet test; one fresh review and one regression-tested fix pass if required.
- [x] Publish clean M8 package, verify startup stopped, preserve existing local data when upgrading the launcher, sync verified source to public GitHub, provide screenshots and manual checks.
