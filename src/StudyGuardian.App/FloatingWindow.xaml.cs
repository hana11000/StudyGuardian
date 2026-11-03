using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using StudyGuardian.Core;

namespace StudyGuardian.App;

public partial class FloatingWindow : Window
{
    private bool _allowClose;
    public event EventHandler? PauseRequested;
    public event EventHandler? OpenMainRequested;
    public FloatingWindow(double opacity)
    {
        InitializeComponent();
        Opacity = opacity;
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
    }
    public void Update(StudySessionSnapshot snapshot, string goal, EscalationSnapshot? escalation = null)
    {
        StatusDot.Fill = ActivityRow.BrushForState(snapshot, escalation);
        StatusText.Text = ActivityRow.DisplayState(snapshot);
        TimeText.Text = ActivityRow.Countdown(snapshot.Remaining);
        GoalText.Text = goal.ReplaceLineEndings(" ");
        GoalText.ToolTip = goal;
        FocusText.Text = "专注率 " + (snapshot.FocusRate is { } rate ? rate.ToString("P0") : "—");
        PauseButton.Content = snapshot.IsManuallyPaused ? "▶" : "Ⅱ";
        PauseButton.ToolTip = snapshot.IsManuallyPaused ? "继续" : "暂停";
        AutomationProperties.SetName(PauseButton, (string)PauseButton.ToolTip);
    }
    public void ClosePermanently() { _allowClose = true; Close(); }
    private void PauseClick(object sender, RoutedEventArgs e) => PauseRequested?.Invoke(this, EventArgs.Empty);
    private void OpenMainClick(object sender, RoutedEventArgs e) => OpenMainRequested?.Invoke(this, EventArgs.Empty);
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }
    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is ButtonBase) return;
        if (e.ClickCount == 2) OpenMainRequested?.Invoke(this, EventArgs.Empty);
        else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
