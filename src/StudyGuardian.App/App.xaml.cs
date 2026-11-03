using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.IO;
using StudyGuardian.Monitor;

namespace StudyGuardian.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private WatchdogClient? _watchdog;
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
            var inherited=e.Args.Length==2 && e.Args[0]=="--watchdog-managed"?e.Args[1]:null;
            _watchdog=new(Path.Combine(AppContext.BaseDirectory,"StudyGuardian.App.exe"),inherited);
            if(_watchdog.IsStopping) {Shutdown();return;}
            ShutdownMode=ShutdownMode.OnMainWindowClose;
            MainWindow = new MainWindow(configuration.Options, configuration.Session, configuration.Ai, configuration.Intervention, configuration.Browser,configuration.SessionTools,configuration.Desktop,_watchdog,configuration.DatabasePath);
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("程序未能启动。请检查配置和目录权限。\n\n" + ex.Message, "Study Guardian", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _watchdog?.Stop();
        if(MainWindow is MainWindow window) window.AllowSystemExit();
        base.OnSessionEnding(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _watchdog?.Dispose();
        if (_ownsMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
