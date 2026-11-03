using System.ComponentModel;
using System.IO;
using System.Windows;

namespace StudyGuardian.App;

public partial class SettingsWindow : Window
{
    private readonly AiSettingsStore _store;
    private bool _clearKey;
    public SettingsWindow(AiSettingsStore store)
    {
        InitializeComponent(); _store = store;
        EnabledBox.IsChecked = store.Current.Enabled; ModelBox.Text = store.Current.Model;
        KeyStatus.Text = store.HasKey ? "已保存" : "未设置";
        ClearKeyButton.IsEnabled = store.HasKey;
        Closed += (_, _) => KeyBox.Clear();
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
