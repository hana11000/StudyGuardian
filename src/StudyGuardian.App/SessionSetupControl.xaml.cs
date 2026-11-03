using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using StudyGuardian.Core;

namespace StudyGuardian.App;

public partial class SessionSetupControl : UserControl
{
    private SessionUiOptions _options = null!;
    private AiOptions _aiOptions = null!;
    private StudyPolicy? _generatedPolicy;
    private string? _generatedGoal;
    public event EventHandler? GeneratePolicyRequested;
    public string Goal => GoalBox.Text.Trim();
    public SessionSetupControl() => InitializeComponent();

    public void Initialize(SessionUiOptions options, AiOptions aiOptions, Func<IReadOnlyList<RuleCandidate>>? candidateProvider = null)
    {
        _options = options;
        _aiOptions = aiOptions;
        GoalBox.MaxLength = aiOptions.MaximumGoalCharacters;
        SupportiveMinutesBox.Text = aiOptions.DefaultSupportiveMinutes.ToString(CultureInfo.CurrentCulture);
        AllowRules.Initialize(RuleKind.Allow, "允许", "SuccessBrush");
        SupportiveRules.Initialize(RuleKind.Supportive, "辅助", "AmberBrush");
        BlockRules.Initialize(RuleKind.Block, "禁止", "DangerBrush");
        AllowRules.CandidateProvider = SupportiveRules.CandidateProvider = BlockRules.CandidateProvider = candidateProvider;
        foreach (var minutes in options.PresetMinutes)
        {
            var choice = new RadioButton { Content = minutes.ToString(CultureInfo.CurrentCulture), Tag = minutes,
                GroupName = "Duration", Style = (Style)FindResource("SegmentChoice") };
            choice.Checked += DurationChanged;
            DurationChoices.Children.Add(choice);
            choice.IsChecked = minutes == options.DefaultDurationMinutes;
        }
        var custom = new RadioButton { Content = "自定义", GroupName = "Duration", Style = (Style)FindResource("SegmentChoice"), Margin = new Thickness(0) };
        custom.Checked += DurationChanged;
        DurationChoices.Children.Add(custom);
        custom.IsChecked = !options.PresetMinutes.Contains(options.DefaultDurationMinutes);
        CustomMinutesBox.Text = options.DefaultDurationMinutes.ToString(CultureInfo.CurrentCulture);
        var strictness = Enum.Parse<SessionStrictness>(options.DefaultStrictness);
        (strictness == SessionStrictness.Gentle ? GentleChoice : strictness == SessionStrictness.Strict ? StrictChoice : StandardChoice).IsChecked = true;
    }

    public bool TryGetOptions(out StudySessionOptions? options)
    {
        options = null;
        GoalError.Visibility = DurationError.Visibility = Visibility.Collapsed;
        var goal = GoalBox.Text.Trim();
        if (goal.Length == 0)
        {
            GoalError.Text = "请输入学习目标";
            GoalError.Visibility = Visibility.Visible;
            GoalBox.Focus();
            return false;
        }
        var choice = DurationChoices.Children.OfType<RadioButton>().Single(c => c.IsChecked == true);
        var minutes = choice.Tag is int selected ? selected : int.TryParse(CustomMinutesBox.Text, out var custom) ? custom : 0;
        if (minutes < _options.MinimumDurationMinutes || minutes > _options.MaximumDurationMinutes)
        {
            DurationError.Text = $"请输入 {_options.MinimumDurationMinutes}–{_options.MaximumDurationMinutes} 分钟";
            DurationError.Visibility = Visibility.Visible;
            CustomMinutesBox.Focus();
            return false;
        }
        if (!AllowRules.TryGetRules(out var allow) || !SupportiveRules.TryGetRules(out var supportive) || !BlockRules.TryGetRules(out var block)) return false;
        if (!int.TryParse(SupportiveMinutesBox.Text, out var supportiveMinutes) || supportiveMinutes <= 0 || supportiveMinutes > _aiOptions.MaximumSupportiveMinutes)
        { ShowPolicyError($"辅助上限为 1–{_aiOptions.MaximumSupportiveMinutes} 分钟"); return false; }
        var strictness = GentleChoice.IsChecked == true ? SessionStrictness.Gentle : StrictChoice.IsChecked == true ? SessionStrictness.Strict : SessionStrictness.Standard;
        StudyRule[] rules = [.. allow, .. supportive, .. block];
        options = new(goal, TimeSpan.FromMinutes(minutes), strictness, rules,
            new(_generatedGoal == goal ? _generatedPolicy?.GoalSummary ?? goal : goal, rules, supportiveMinutes));
        PolicyError.Visibility = Visibility.Collapsed;
        return true;
    }
    public void ApplyPolicy(StudyPolicy policy)
    {
        _generatedPolicy = policy; _generatedGoal = Goal;
        AllowRules.SetRules(policy.Rules); SupportiveRules.SetRules(policy.Rules); BlockRules.SetRules(policy.Rules);
        SupportiveMinutesBox.Text = policy.SupportiveTimeLimitMinutes.ToString(CultureInfo.CurrentCulture);
        PolicyError.Visibility = Visibility.Collapsed;
    }
    public void SetGenerating(bool generating) => GenerateButton.Content = generating ? "生成中…" : "生成规则";
    public void ShowPolicyError(string message) { PolicyError.Text = message; PolicyError.Visibility = Visibility.Visible; }
    private void GenerateClick(object sender, RoutedEventArgs e) => GeneratePolicyRequested?.Invoke(this, EventArgs.Empty);
    private void DurationChanged(object sender, RoutedEventArgs e) => CustomDurationRow.Visibility = ((RadioButton)sender).Tag is null ? Visibility.Visible : Visibility.Collapsed;
}
