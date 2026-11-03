using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Globalization;
using System.Diagnostics;
using StudyGuardian.Core;
using StudyGuardian.Data;
using StudyGuardian.Monitor;

namespace StudyGuardian.App;

public partial class SettingsWindow : Window
{
    private readonly AiSettingsStore _store;
    private bool _clearKey;
    private readonly AutostartRegistration _autostart;
    private bool _initialAutostart;
    private readonly SessionController _controller;
    private readonly UserSettingsStore _preferences;
    private CancellationTokenSource? _connectionTest;
    public SettingsWindow(AiSettingsStore store, SessionController controller,UserSettingsStore preferences,AutostartRegistration autostart)
    {
        InitializeComponent(); _store = store; _controller=controller;_preferences=preferences;
        EnabledBox.IsChecked = store.Current.Enabled; ModelBox.Text = store.Current.Model;
        KeyStatus.Text = store.HasKey ? "已保存" : "未设置";
        ClearKeyButton.IsEnabled = store.HasKey;
        BrowserPortText.Text=controller.BrowserPort.ToString(); PairingStatusText.Text=controller.BrowserConnectionStatus;
        _autostart=autostart;
        try {AutostartBox.IsChecked=_initialAutostart=autostart.IsEnabled;}
        catch(Exception) {AutostartBox.IsEnabled=false;ShowError("无法读取自启设置");}
        var current=preferences.Current;
        StrictExitBox.IsChecked=current.Desktop?.StrictExit==true;WatchdogBox.IsChecked=current.Desktop?.Watchdog==true;
        ReminderBox.Text=current.Intervention.ReminderSeconds.ToString(CultureInfo.InvariantCulture);
        WarningBox.Text=current.Intervention.WarningSeconds.ToString(CultureInfo.InvariantCulture);
        BlockBox.Text=current.Intervention.BlockSeconds.ToString(CultureInfo.InvariantCulture);
        LockBox.Text=current.Intervention.BlockLockSeconds.ToString(CultureInfo.InvariantCulture);
        SoundBox.IsChecked=current.Intervention.SoundEnabled;SendTitleBox.IsChecked=current.Privacy.SendWindowTitle;SendUrlBox.IsChecked=current.Privacy.SendUrl;
        GlobalAllowRules.Initialize(RuleKind.Allow,"允许","SuccessBrush");GlobalSupportiveRules.Initialize(RuleKind.Supportive,"辅助","AmberBrush");GlobalBlockRules.Initialize(RuleKind.Block,"禁止","DangerBrush");
        GlobalAllowRules.SetRules(current.GlobalRules);GlobalSupportiveRules.SetRules(current.GlobalRules);GlobalBlockRules.SetRules(current.GlobalRules);
        GlobalAllowRules.CandidateProvider = GlobalSupportiveRules.CandidateProvider = GlobalBlockRules.CandidateProvider = controller.GetRuleCandidates;
        KeyBox.PasswordChanged += (_, _) => { if (_connectionTest is null) ConnectionStatusText.Text = ""; };
        ModelBox.TextChanged += (_, _) => { if (_connectionTest is null) ConnectionStatusText.Text = ""; };
        Closed += (_, _) => { _connectionTest?.Cancel(); KeyBox.Clear(); };
    }

    private async void TestConnectionClick(object sender, RoutedEventArgs e)
    {
        if (_connectionTest is not null) return;
        using var cancellation = new CancellationTokenSource();
        _connectionTest = cancellation;
        var canClear = ClearKeyButton.IsEnabled;
        TestConnectionButton.IsEnabled = SaveButton.IsEnabled = KeyBox.IsEnabled = ModelBox.IsEnabled = ClearKeyButton.IsEnabled = false;
        ConnectionStatusText.Text = "连接中…";
        var watch = Stopwatch.StartNew();
        try
        {
            await _controller.TestAiConnectionAsync(KeyBox.Password, ModelBox.Text, !_clearKey, cancellation.Token);
            if (!cancellation.IsCancellationRequested) ConnectionStatusText.Text = $"连接成功 · {watch.Elapsed.TotalSeconds:0.0} 秒";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (AiUnavailableException ex) { if (!cancellation.IsCancellationRequested) ConnectionStatusText.Text = ex.Message; }
        catch (Exception) { if (!cancellation.IsCancellationRequested) ConnectionStatusText.Text = "连接失败"; }
        finally
        {
            _connectionTest = null;
            TestConnectionButton.IsEnabled = SaveButton.IsEnabled = KeyBox.IsEnabled = ModelBox.IsEnabled = true;
            ClearKeyButton.IsEnabled = canClear;
        }
    }
    private void CopyPairingClick(object sender,RoutedEventArgs e)
    {
        try { Clipboard.SetText(_controller.BrowserPairing.ReadOrCreate()); PairingStatusText.Text="已复制"; }
        catch(Exception) { ShowError("配对码复制失败"); }
    }
    private void ResetPairingClick(object sender,RoutedEventArgs e)
    {
        try { _controller.ResetBrowserPairing(); PairingStatusText.Text="已重置，请重新配对"; }
        catch(Exception) { ShowError("配对码重置失败"); }
    }
    private void ClearKeyClick(object sender, RoutedEventArgs e)
    { _clearKey = true; KeyBox.Clear(); KeyStatus.Text = "待清除"; ClearKeyButton.IsEnabled = false; }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(ModelBox.Text)) { ShowError("请输入模型名称"); return; }
        if(!GlobalAllowRules.TryGetRules(out var allow)||!GlobalSupportiveRules.TryGetRules(out var supportive)||!GlobalBlockRules.TryGetRules(out var block)) return;
        if(!double.TryParse(ReminderBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var reminder)||
            !double.TryParse(WarningBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var warning)||
            !double.TryParse(BlockBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var blocked)||
            !double.TryParse(LockBox.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var locked)) { ShowError("请输入有效秒数");return; }
        var next=_preferences.Current with
        {
            Intervention=_preferences.Current.Intervention with {ReminderSeconds=reminder,WarningSeconds=warning,BlockSeconds=blocked,BlockLockSeconds=locked,SoundEnabled=SoundBox.IsChecked==true},
            Desktop=new(StrictExitBox.IsChecked==true,WatchdogBox.IsChecked==true),
            GlobalRules=[..allow,..supportive,..block],Privacy=new(SendTitleBox.IsChecked==true,SendUrlBox.IsChecked==true)
        };
        try {next.Validate();if(blocked*Math.Max(next.Intervention.GentleMultiplier,next.Intervention.StrictMultiplier)>=TimeSpan.MaxValue.TotalSeconds || locked>=TimeSpan.MaxValue.TotalSeconds) throw new ArgumentException();}
        catch(ArgumentException) {ShowError("阈值需满足：0 < 提醒 < 警告 < 拦截");return;}
        try
        {
            _store.Save(new(EnabledBox.IsChecked == true, ModelBox.Text.Trim()), KeyBox.Password, _clearKey);
            _preferences.Save(next);
            if(AutostartBox.IsEnabled && (AutostartBox.IsChecked==true)!=_initialAutostart) {_autostart.SetEnabled(AutostartBox.IsChecked==true);_initialAutostart=AutostartBox.IsChecked==true;}
            KeyBox.Clear(); DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or System.Security.SecurityException) { ShowError("保存失败，请检查目录权限"); }
    }
    private void ShowError(string text) { ErrorText.Text = text; ErrorText.Visibility = Visibility.Visible; }
}
