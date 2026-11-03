using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Globalization;
using StudyGuardian.Core;
using StudyGuardian.Data;

namespace StudyGuardian.App;

public partial class SettingsWindow : Window
{
    private readonly AiSettingsStore _store;
    private bool _clearKey;
    private readonly SessionController _controller;
    private readonly UserSettingsStore _preferences;
    public SettingsWindow(AiSettingsStore store, SessionController controller,UserSettingsStore preferences)
    {
        InitializeComponent(); _store = store; _controller=controller;_preferences=preferences;
        EnabledBox.IsChecked = store.Current.Enabled; ModelBox.Text = store.Current.Model;
        KeyStatus.Text = store.HasKey ? "已保存" : "未设置";
        ClearKeyButton.IsEnabled = store.HasKey;
        BrowserPortText.Text=controller.BrowserPort.ToString(); PairingStatusText.Text=controller.BrowserConnectionStatus;
        var current=preferences.Current;
        ReminderBox.Text=current.Intervention.ReminderSeconds.ToString(CultureInfo.InvariantCulture);
        WarningBox.Text=current.Intervention.WarningSeconds.ToString(CultureInfo.InvariantCulture);
        BlockBox.Text=current.Intervention.BlockSeconds.ToString(CultureInfo.InvariantCulture);
        LockBox.Text=current.Intervention.BlockLockSeconds.ToString(CultureInfo.InvariantCulture);
        SoundBox.IsChecked=current.Intervention.SoundEnabled;SendTitleBox.IsChecked=current.Privacy.SendWindowTitle;SendUrlBox.IsChecked=current.Privacy.SendUrl;
        GlobalAllowRules.Initialize(RuleKind.Allow,"允许","SuccessBrush");GlobalSupportiveRules.Initialize(RuleKind.Supportive,"辅助","AmberBrush");GlobalBlockRules.Initialize(RuleKind.Block,"禁止","DangerBrush");
        GlobalAllowRules.SetRules(current.GlobalRules);GlobalSupportiveRules.SetRules(current.GlobalRules);GlobalBlockRules.SetRules(current.GlobalRules);
        Closed += (_, _) => KeyBox.Clear();
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
            GlobalRules=[..allow,..supportive,..block],Privacy=new(SendTitleBox.IsChecked==true,SendUrlBox.IsChecked==true)
        };
        try {next.Validate();if(blocked*Math.Max(next.Intervention.GentleMultiplier,next.Intervention.StrictMultiplier)>=TimeSpan.MaxValue.TotalSeconds || locked>=TimeSpan.MaxValue.TotalSeconds) throw new ArgumentException();}
        catch(ArgumentException) {ShowError("阈值需满足：0 < 提醒 < 警告 < 拦截");return;}
        try
        {
            _store.Save(new(EnabledBox.IsChecked == true, ModelBox.Text.Trim()), KeyBox.Password, _clearKey);
            _preferences.Save(next);
            KeyBox.Clear(); DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { ShowError("保存失败，请检查目录权限"); }
    }
    private void ShowError(string text) { ErrorText.Text = text; ErrorText.Visibility = Visibility.Visible; }
}
