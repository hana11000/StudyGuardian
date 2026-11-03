using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using StudyGuardian.Core;

namespace StudyGuardian.App;

public partial class RulePickerWindow : Window
{
    private readonly Func<IReadOnlyList<RuleCandidate>> _provider;
    private IReadOnlyList<RuleCandidate> _choices = [];
    private readonly HashSet<RuleCandidate> _selected = [];
    public IReadOnlyList<RuleCandidate> Selected => _choices.Where(_selected.Contains).ToArray();
    public bool UseManualEntry { get; private set; }

    public RulePickerWindow(string kind, Func<IReadOnlyList<RuleCandidate>> provider)
    {
        InitializeComponent(); _provider = provider;
        Title = "添加" + kind + "规则";
        RefreshChoices();
    }

    private void RefreshChoices()
    {
        try { _choices = _provider(); _selected.IntersectWith(_choices); RenderChoices(); }
        catch (Exception)
        {
            _choices = []; _selected.Clear(); UpdateCount();
            ChoicesPanel.Children.Clear();
            ChoicesPanel.Children.Add(new TextBlock { Text = "无法读取应用列表，请刷新", Foreground = (Brush)FindResource("DangerBrush") });
        }
    }
    private void RenderChoices()
    {
        ChoicesPanel.Children.Clear();
        foreach (var choice in _choices.Where(c => (c.Label + " " + c.Detail).Contains(SearchBox.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
        {
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = choice.Label, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 430 });
            text.Children.Add(new TextBlock { Text = choice.TypeLabel + " · " + choice.Detail, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 11,
                Margin = new Thickness(0,5,0,0), MaxWidth = 430, TextTrimming = TextTrimming.CharacterEllipsis });
            var check = new CheckBox { Content = text, IsChecked = _selected.Contains(choice), Foreground = (Brush)FindResource("TextBrush"),
                Padding = new Thickness(8), Margin = new Thickness(0,0,0,8), ToolTip = choice.Label + "\n" + choice.Detail };
            AutomationProperties.SetName(check, choice.TypeLabel + " " + choice.Label);
            check.Checked += (_,_) => { _selected.Add(choice); UpdateCount(); };
            check.Unchecked += (_,_) => { _selected.Remove(choice); UpdateCount(); };
            ChoicesPanel.Children.Add(check);
        }
        if (ChoicesPanel.Children.Count == 0) ChoicesPanel.Children.Add(new TextBlock { Text = "暂无匹配的应用", Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0,16,0,0) });
        UpdateCount();
    }
    private void UpdateCount() { AddButton.IsEnabled = _selected.Count > 0; AddButton.Content = _selected.Count == 0 ? "添加" : $"添加（{_selected.Count}）"; }
    private void SearchChanged(object sender, TextChangedEventArgs e) { if (ChoicesPanel is not null) RenderChoices(); }
    private void RefreshClick(object sender, RoutedEventArgs e) => RefreshChoices();
    private void ManualClick(object sender, RoutedEventArgs e) { UseManualEntry = true; DialogResult = false; }
    private void AddClick(object sender, RoutedEventArgs e) { if (Selected.Count > 0) DialogResult = true; }
}
