using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StudyGuardian.Core;

namespace StudyGuardian.App;

public partial class RuleGroupControl : UserControl
{
    private RuleKind _kind;
    private readonly List<StudyRule> _rules = [];
    public Func<IReadOnlyList<RuleCandidate>>? CandidateProvider { get; set; }
    public RuleGroupControl() => InitializeComponent();

    public void Initialize(RuleKind kind, string label, string brushKey)
    {
        _kind = kind;
        KindLabel.Text = label;
        KindDot.Fill = (Brush)FindResource(brushKey);
        RenderTags();
    }

    public bool TryGetRules(out IReadOnlyList<StudyRule> rules)
    {
        if (EditorPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(PatternBox.Text) && !AddRule())
        {
            rules = [];
            return false;
        }
        rules = _rules.ToArray();
        return true;
    }

    public void SetRules(IEnumerable<StudyRule> rules)
    {
        _rules.Clear(); _rules.AddRange(rules.Where(r => r.Kind == _kind));
        CancelEditor(); RenderTags();
    }

    private void RenderTags()
    {
        TagsPanel.Children.Clear();
        foreach (var rule in _rules)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = TypeLabel(rule.MatchType) + " · " + rule.Pattern, MaxWidth = 255,
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = rule.Pattern });
            var remove = new Button { Content = "×", Padding = new Thickness(6, 0, 0, 0), Margin = new Thickness(6, 0, 0, 0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = (Brush)FindResource("MutedBrush") };
            AutomationProperties.SetName(remove, "删除规则 " + rule.Pattern);
            remove.Click += (_, _) => { _rules.Remove(rule); RenderTags(); };
            panel.Children.Add(remove);
            TagsPanel.Children.Add(new Border { Child = panel, CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 7, 8, 7),
                Background = (Brush)FindResource("RaisedBrush"), Margin = new Thickness(0, 0, 6, 6) });
        }
        var add = new Button { Content = "+", Width = 34, Height = 34, Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 6),
            FontSize = 19, ToolTip = "添加" + KindLabel.Text + "规则" };
        AutomationProperties.SetName(add, "添加" + KindLabel.Text + "规则");
        add.Click += (_, _) => OpenPicker();
        TagsPanel.Children.Add(add);
    }

    private void OpenPicker()
    {
        if (CandidateProvider is null) { EditorPanel.Visibility = Visibility.Visible; PatternBox.Focus(); return; }
        var picker = new RulePickerWindow(KindLabel.Text, CandidateProvider) { Owner = Window.GetWindow(this) };
        if (picker.ShowDialog() == true)
        {
            foreach (var candidate in picker.Selected)
            {
                var rule = candidate.ToRule(_kind);
                var comparison = rule.MatchType is RuleMatchType.Process or RuleMatchType.Domain ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!_rules.Any(r => r.MatchType == rule.MatchType && r.Pattern.Equals(rule.Pattern, comparison))) _rules.Add(rule);
            }
            RenderTags();
        }
        else if (picker.UseManualEntry) { EditorPanel.Visibility = Visibility.Visible; PatternBox.Focus(); }
    }

    private bool AddRule()
    {
        var type = (RuleMatchType)MatchTypeBox.SelectedIndex;
        var pattern = type == RuleMatchType.TitleRegex ? PatternBox.Text : PatternBox.Text.Trim();
        var rule = new StudyRule(Guid.NewGuid().ToString("N"), _kind, type, pattern);
        if (RuleEngine.Validate(rule) is { } error)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            PatternBox.Focus();
            return false;
        }
        if (!_rules.Any(r => r.MatchType == type && r.Pattern == pattern)) _rules.Add(rule);
        CancelEditor();
        RenderTags();
        return true;
    }

    private void CancelEditor()
    {
        PatternBox.Clear();
        ErrorText.Visibility = Visibility.Collapsed;
        EditorPanel.Visibility = Visibility.Collapsed;
    }
    private void ConfirmClick(object sender, RoutedEventArgs e) => AddRule();
    private void CancelClick(object sender, RoutedEventArgs e) => CancelEditor();
    private void PatternKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { AddRule(); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelEditor(); e.Handled = true; }
    }
    private void TypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PatternBox is null) return;
        PatternBox.ToolTip = MatchTypeBox.SelectedIndex switch { 0 => "Notion.exe", 1 => "notion.so", 2 => "https://example.com/page", _ => "增值税|进项税" };
    }
    private static string TypeLabel(RuleMatchType type) => type switch
    { RuleMatchType.Process => "进程", RuleMatchType.Domain => "域名", RuleMatchType.Url => "网址", _ => "标题" };
}
