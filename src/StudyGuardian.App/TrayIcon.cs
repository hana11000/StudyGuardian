using Forms=System.Windows.Forms;
namespace StudyGuardian.App;
public sealed class TrayIcon:IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu=new();
    private readonly Forms.ToolStripMenuItem _pause,_rest,_float;
    public TrayIcon(Action open,Action showFloat,Action pause,Action rest,Action exit)
    {
        _menu.Items.Add("打开",null,(_,_)=>open());_float=new("悬浮窗",null,(_,_)=>showFloat());_menu.Items.Add(_float);
        _pause=new("暂停",null,(_,_)=>pause());_menu.Items.Add(_pause);_rest=new("休息",null,(_,_)=>rest());_menu.Items.Add(_rest);
        _menu.Items.Add(new Forms.ToolStripSeparator());_menu.Items.Add("退出",null,(_,_)=>exit());
        _icon=new() {Icon=System.Drawing.SystemIcons.Shield,Text="Study Guardian",ContextMenuStrip=_menu,Visible=true};_icon.DoubleClick+=(_,_)=>open();
    }
    public void Update(bool running,bool paused,bool canRest)
    {_pause.Enabled=_float.Enabled=running;_pause.Text=paused?"继续":"暂停";_rest.Enabled=canRest;}
    public void Dispose(){_icon.Visible=false;_icon.Dispose();_menu.Dispose();}
}
