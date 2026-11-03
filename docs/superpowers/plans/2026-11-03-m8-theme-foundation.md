# M8 Theme Foundation Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans task by task; this is only the first approved M8 step.

**Goal:** Deliver a standalone WPF preview of Quiet Frost light/dark themes, controls and four ambient states for visual approval.

**Architecture:** New, explicitly scoped resource dictionaries live in the App UI assembly but are loaded only by UiPreview. AmbientBackground owns UI-only dependency properties and compositor animations; no session state or services. Fonts and deterministic noise are embedded UI resources. The preview has synthetic values and no monitoring/database/API startup.

**Tech Stack:** Existing .NET 8 WPF, no new NuGet dependencies.

**Spec:** User's “静谧磨砂” design and approved file plan in this chat; AGENTS.md.

## Global Constraints

- Only first step; production App.xaml, windows, ViewModels, Core, Monitor, AI, Data and config remain unchanged.
- Light #F4F3F0, glass white 55%, highlight white 80%, text #1C1C1E/#8E8E93; dark #0E0E12, white glass 6%, ambient opacity 35%.
- Focus mint/blue, Warn apricot/peach, Block coral/lilac, Idle lavender/gray. Color confined to ambient light and small indicators.
- Tokens own sizes, colors (theme variants), spacings, effects and durations. Chinese MiSans Regular/Semibold, numeric Segoe UI Variable Display with tabular alignment.
- Grain 128×128 tiled at 3%; card radius 20, window 24, pill controls. Drift 20–30s, color transition 1.2s, hover 150ms, appearance 280ms, breathing 4s. Nonlinear easing only.
- Respect disabled motion/system animation preferences; stop clocks when hidden/unloaded. UI captures contain only synthetic preview content, no desktop screenshots.
- Preserve original font files/license; no font modification or system installation.

## Review Focus

- Theme swaps update existing controls and current ambient state without leaking global resources.
- Disabling motion or unloading stops repeat animations and permits later reload.
- Keyboard focus, selected/disabled text and dropdowns remain legible in both themes.
- Preview starts no production services and adds no real data to source or screenshots.
- Packaged resources resolve on machines with no installed MiSans; fonts/license are distributed as part of the application.

### Task 1: Theme resources and controls

**Files:** App/Themes/{Tokens,Light,Dark,Controls}.xaml; App/Assets/{Fonts,Textures}; App.csproj; docs/m8-theme-design.md.

**Interfaces:** Named M8.* resource keys, M8.GlassCard ContentControl style, M8.PrimaryButton/TextButton/PillChoice/Input/GoalInput/ComboBox/CheckBox styles. No implicit styles applied to legacy windows.

- [x] Fetch original MiSans Regular/Semibold and license from official source; add font attribution.
- [x] Implement scoped tokens and complete theme key parity; create styles and procedural noise.
- [x] Build and inspect rendered control states in Task 3; no tests that merely repeat styling values.

### Task 2: Ambient background

**Files:** App/Controls/AmbientBackground.xaml(.cs); Windows.Tests/AmbientBackgroundTests.cs; existing UI dispatcher test helper access only.

**Interfaces:** AmbientState {Focus,Warn,Block,Idle}; State, IsDark, MotionEnabled dependency properties; IsAnimating read-only diagnostic. Local cloned brushes and transforms; independent of Core.

- [x] Write lifecycle/theme change tests before implementation; observe failure.
- [x] Add three blurred ellipses with nonlinear drift, tokenized state transitions and visibility/unload cleanup.
- [x] Run focused lifecycle tests then whole solution tests; expect green.

### Task 3: Isolated preview and delivery

**Files:** src/StudyGuardian.UiPreview/{csproj,App.xaml(.cs),DemoWindow.xaml(.cs)}; StudyGuardian.sln; scripts/preview-ui.ps1; docs/m8-files.md and verification.

**Interfaces:** Scoped theme switch, state switch and motion toggle. Preview renders actual UI content to PNG in --render mode, without showing/capturing desktop; standalone preview also runnable interactively.

- [x] Build demo with warm spacious main card, pills, goal field, four-state ambient swatches, glass timer sample and control state samples; light/dark live switching.
- [x] Run dotnet build and dotnet test, render eight theme/state PNGs, inspect light/dark compositions and layout at smaller size.
- [x] Obtain one fresh code review, fix important findings in one regression-tested pass.
- [x] Publish preview separately, document changed files and launch steps, sync verified source under existing authorization.
- [x] Present screenshots and await user approval before redesigning production windows.
