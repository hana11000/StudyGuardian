using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace StudyGuardian.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var configuration = ConfigurationLoader.Load();
            var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration.DatabasePath.ToUpperInvariant())));
            _instanceMutex = new Mutex(true, @"Local\StudyGuardian-" + identity, out _ownsMutex);
            if (!_ownsMutex)
            {
                MessageBox.Show("这个目录的 Study Guardian 已经打开，请使用现有窗口。", "Study Guardian", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            MainWindow = new MainWindow(configuration.Options, configuration.Session, configuration.Ai, configuration.Intervention, configuration.Browser,configuration.SessionTools,configuration.DatabasePath);
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("程序未能启动。请检查配置和目录权限。\n\n" + ex.Message, "Study Guardian", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
