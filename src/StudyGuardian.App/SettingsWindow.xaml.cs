using System.ComponentModel;
using System.IO;
using System.Windows;

namespace StudyGuardian.App;

public partial class SettingsWindow : Window
{
    private readonly AiSettingsStore _store;
    private bool _clearKey;
    private readonly SessionController _controller;
    public SettingsWindow(AiSettingsStore store, SessionController controller)
    {
        InitializeComponent(); _store = store; _controller=controller;
        EnabledBox.IsChecked = store.Current.Enabled; ModelBox.Text = store.Current.Model;
        KeyStatus.Text = store.HasKey ? "已保存" : "未设置";
        ClearKeyButton.IsEnabled = store.HasKey;
        BrowserPortText.Text=controller.BrowserPort.ToString(); PairingStatusText.Text=controller.BrowserConnectionStatus;
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
        try
        {
            _store.Save(new(EnabledBox.IsChecked == true, ModelBox.Text.Trim()), KeyBox.Password, _clearKey);
            KeyBox.Clear(); DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception) { ShowError("保存失败，请检查目录权限"); }
    }
    private void ShowError(string text) { ErrorText.Text = text; ErrorText.Visibility = Visibility.Visible; }
}
