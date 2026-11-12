using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetSpeck {
internal static class Program {
    [STAThread] static int Main(string[] args) {
        try {
            if(args.Length>0&&args[0]=="--diagnose") {Diagnostics.Save(args.Length>1?args[1]:Settings.Folder);return 0;}
            if(args.Length>0&&(args[0]=="--exit"||args[0]=="--recreate"||args[0]=="--menu")) {
                IntPtr w=Native.FindWindow(null,"NetSpeck.Controller");
                if(w==IntPtr.Zero)return 1;
                string command=args[0]=="--exit"?"NetSpeck.Exit":args[0]=="--menu"?"NetSpeck.Menu":"NetSpeck.Recreate";
                return Native.PostMessage(w,Native.RegisterWindowMessage(command),IntPtr.Zero,IntPtr.Zero)?0:1;
            }
            bool first;
            using(var mutex=new Mutex(true,@"Local\NetSpeck.SingleInstance",out first)) {
                if(!first) {
                    IntPtr w=Native.FindWindow(null,"NetSpeck.Controller");
                    if(w!=IntPtr.Zero)Native.PostMessage(w,Native.RegisterWindowMessage("NetSpeck.Settings"),IntPtr.Zero,IntPtr.Zero);
                    return 0;
                }
                // Match Explorer's DPI mode BEFORE any HWND is created. Never force a
                // different DPI mode on the cross-process parent/child relationship.
                IntPtr bar=Native.FindWindow("Shell_TrayWnd",null);
                Native.SetThreadDpiAwarenessContext(bar==IntPtr.Zero?new IntPtr(-4):Native.GetWindowDpiAwarenessContext(bar));
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException+=(s,e)=>Log.Write("UI error: "+e.Exception);
                try{Settings.MigrateAutoStart();}catch(Exception e){Log.Write("Startup migration: "+e.Message);}
                using(var context=new MeterContext(args))Application.Run(context);
                mutex.ReleaseMutex();
            }
            return 0;
        }catch(Exception e){Log.Write("Fatal: "+e);return 1;}
    }
}
internal sealed class Controller : NativeWindow,IDisposable {
    readonly MeterContext owner;
    readonly uint created=Native.RegisterWindowMessage("TaskbarCreated"),settings=Native.RegisterWindowMessage("NetSpeck.Settings"),exit=Native.RegisterWindowMessage("NetSpeck.Exit"),recreate=Native.RegisterWindowMessage("NetSpeck.Recreate"),menu=Native.RegisterWindowMessage("NetSpeck.Menu");
    public Controller(MeterContext context){owner=context;CreateHandle(new CreateParams{Caption="NetSpeck.Controller",Style=0,ExStyle=0x80});}
    protected override void WndProc(ref Message m){
        if((uint)m.Msg==created){owner.Recreate();owner.RefreshTray();}
        if((uint)m.Msg==settings)owner.OpenSettings();
        if((uint)m.Msg==exit)owner.Quit();
        if((uint)m.Msg==recreate)owner.Recreate();
        if((uint)m.Msg==menu)owner.OpenMeterMenu();
        if(m.Msg==0x218){Log.Write("Power event: "+m.WParam.ToInt64());owner.ResetSampling();} // WM_POWERBROADCAST
        if(m.Msg==0x11){m.Result=new IntPtr(1);return;}
        if(m.Msg==0x16&&m.WParam!=IntPtr.Zero)owner.Quit();
        base.WndProc(ref m);
    }
    public void Dispose(){DestroyHandle();}
}
internal sealed class MeterContext : ApplicationContext {
    readonly Settings config=Settings.Load();readonly Controller controller;
    readonly object gate=new object();readonly TrafficSampler sampler=new TrafficSampler();
    readonly Stopwatch clock=Stopwatch.StartNew();
    System.Threading.Timer networkTimer,layoutTimer;
    readonly System.Windows.Forms.Timer uiTimer=new System.Windows.Forms.Timer();
    readonly NotifyIcon tray=new NotifyIcon();readonly ContextMenuStrip menu=new ContextMenuStrip();
    Reading reading=new Reading();List<Adapter> adapters=new List<Adapter>();Placement placement,resizedPlacement;
    readonly PlacementTracker placementTracker=new PlacementTracker();
    MeterWindow meter;SettingsForm dialog;bool stopped,dark=true,needsRedraw=true;
    int networkBusy,layoutBusy;long lastReadingTicks;DateTime lastRender=DateTime.MinValue,lastChromeRefresh=DateTime.MinValue;
    string lastStatus="",lastLayoutSignal="",savedAdapter;readonly string smokeFolder;readonly DateTime started=DateTime.UtcNow;bool openSettingsWhenReady;
    public MeterContext(string[] args) {
        Ui.SetLanguage(config.Language);reading=new Reading();menu.ShowItemToolTips=true;
        savedAdapter=config.AdapterId;
        controller=new Controller(this);
        tray.Icon=AppIcons.Create(config.IconId,32);tray.Text=Ui.T("NetSpeck — запуск");tray.Visible=true;
        menu.Font=AppFonts.Ui(config);
        tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>OpenSettings();
        menu.Opening+=(s,e)=>{BuildMenu();e.Cancel=false;};
        if(args.Length>1&&args[0]=="--smoke-test")smokeFolder=args[1];
        networkTimer=new System.Threading.Timer(Sample,null,0,GraphMath.PollInterval(config.GraphEnabled,config.Interval));
        layoutTimer=new System.Threading.Timer(Layout,null,0,1000);
        uiTimer.Interval=GraphMath.FrameInterval(config.GraphEnabled,config.GraphFps);uiTimer.Tick+=(s,e)=>Tick();uiTimer.Start();
        Log.Write("Start "+Diagnostics.OsVersion()+"; x64="+Environment.Is64BitProcess);
        openSettingsWhenReady=args.Length>0&&args[0]=="--settings";
    }
    void Sample(object state) {
        if(stopped||Interlocked.Exchange(ref networkBusy,1)!=0)return;
        try {
            var list=Network.ReadAdapters();string selected=config.AdapterId;var adapter=Network.Select(list,selected);
            lock(gate) {
                adapters=list;
                reading=sampler.Update(adapter,clock.Elapsed.TotalSeconds,config.Interval,config.SmoothingSeconds,config.GraphEnabled,Ui.T(selected==""?"Нет подключения":"Выбранный адаптер отключён"),config.GraphSmoothingSeconds);
            }
        }catch(Exception e){lock(gate){sampler.Reset();reading=new Reading{AdapterName=Ui.T("Ошибка чтения сети"),Error=e.Message};}Log.Write("Network: "+e.Message);}
        finally{Interlocked.Exchange(ref networkBusy,0);}
    }
    void Layout(object state) {
        if(stopped||Interlocked.Exchange(ref layoutBusy,1)!=0)return;
        try{var p=TaskbarLayout.Inspect(config);string signal="safe="+p.Safe+", unavailable="+p.Unavailable+", shellPopup="+p.TransientShellUi;lock(gate)placement=placementTracker.Resolve(p,clock.Elapsed.TotalSeconds);if(signal!=lastLayoutSignal){lastLayoutSignal=signal;Log.Write("Layout provider: "+signal);}}
        catch(Exception e){lock(gate){placementTracker.Reset();placement=new Placement{Status="Не удалось проверить расположение панели задач"};}Log.Write("Layout: "+e.Message);}
        finally{Interlocked.Exchange(ref layoutBusy,0);}
    }
    void Tick() {
        if(stopped)return;
        Placement p;Reading r;SpeedPoint[] points;lock(gate){p=placement;r=reading;points=sampler.History.Snapshot();}
        if(p==null)return;
        if(resizedPlacement!=null){if(resizedPlacement.SameBar(p))p=resizedPlacement;else resizedPlacement=null;}
        if(openSettingsWhenReady){openSettingsWhenReady=false;OpenSettings();}
        if(p.Status!=lastStatus){lastStatus=p.Status;Log.Write(p.Status+"; bar="+p.Bar+"; rect="+p.Rect+"; x="+p.X+"; dpi="+p.Dpi);}
        try {
            if(meter!=null&&(!Native.IsWindow(meter.Handle)||meter.Bar!=p.Bar||Native.GetParent(meter.Handle)!=p.Bar)){meter.Dispose();meter=null;needsRedraw=true;}
            if(meter==null&&p.Bar!=IntPtr.Zero&&Native.IsWindow(p.Bar)) {
                meter=new MeterWindow(p.Bar);meter.MenuRequested+=ShowMenu;meter.WidthRequested+=ResizePanel;needsRedraw=true;
                Log.Write("Child created; hwnd="+meter.Handle+"; parent="+Native.GetParent(meter.Handle));
            }
            bool refreshChrome=DateTime.UtcNow-lastChromeRefresh>=TimeSpan.FromMilliseconds(250);
            if(refreshChrome){bool nextDark=IsDark();if(nextDark!=dark){dark=nextDark;needsRedraw=true;}}
            if(meter!=null&&(needsRedraw||r.Time.Ticks!=lastReadingTicks||(config.GraphEnabled&&points.Length>1)||DateTime.UtcNow-lastRender>TimeSpan.FromSeconds(1))) {
                meter.Render(p,r,config,dark,points,clock.Elapsed.TotalSeconds);needsRedraw=false;lastReadingTicks=r.Time.Ticks;lastRender=DateTime.UtcNow;
            }
            // The child inherits native taskbar visibility and clipping automatically.
            if(refreshChrome){lastChromeRefresh=DateTime.UtcNow;
            string tip=r.Connected?r.AdapterName+" | ↓ "+Rates.Format(r.Down,config.Bits)+" ↑ "+Rates.Format(r.Up,config.Bits):r.AdapterName;
            if(!p.Safe)tip="NetSpeck: "+Ui.T(p.Status);
            tray.Text=tip.Length>63?tip.Substring(0,60)+"…":tip;
            if(dialog!=null&&!dialog.IsDisposed)dialog.UpdateStatus(r,p);
            }
            if(smokeFolder!=null&&DateTime.UtcNow-started>TimeSpan.FromSeconds(8)) {
                Diagnostics.Save(smokeFolder);Quit();
            }
        }catch(Exception e){Log.Write("Render: "+e.Message);if(meter!=null){meter.Dispose();meter=null;}}
    }
    bool IsDark(){try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){return key==null||Convert.ToInt32(key.GetValue("SystemUsesLightTheme",0))==0;}}catch{return true;}}
    void ShowMenu(Point where){
        BuildMenu();Native.SetForegroundWindow(controller.Handle);
        if(dialog!=null&&!dialog.IsDisposed&&dialog.Visible)menu.Show(dialog,dialog.PointToClient(where));
        else menu.Show(where);
        Log.Write("Context menu opened; visible="+menu.Visible);
    }
    public void OpenMeterMenu(){
        if(meter!=null&&Native.IsWindow(meter.Handle))Native.PostMessage(meter.Handle,0x7B,meter.Handle,new IntPtr(-1));
        else ShowMenu(Cursor.Position);
    }
    void BuildMenu() {
        menu.Items.Clear();
        var info=MenuItem("NetSpeck 1.0.2","version");info.Enabled=false;menu.Items.Add(info);
        Reading r;List<Adapter> list;lock(gate){r=reading;list=new List<Adapter>(adapters);}
        var statusItem=MenuItem(r.AdapterName,"status",false);statusItem.Enabled=false;menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());
        var choose=MenuItem("Сетевой адаптер","adapter");
        var auto=MenuItem("Автоматически","adapterAuto");auto.Checked=config.AdapterId=="";
        auto.Click+=(s,e)=>ChangeAdapter("");choose.DropDownItems.Add(auto);
        foreach(var adapter in list){var a=adapter;var item=MenuItem(a.ToString(),"adapterManual",false);item.Checked=config.AdapterId==a.Id;item.ToolTipText+="\n"+Ui.Wrap(a.ToString());item.Click+=(s,e)=>ChangeAdapter(a.Id);choose.DropDownItems.Add(item);}
        menu.Items.Add(choose);
        var units=MenuItem("Единицы","units");
        var bytes=MenuItem("Байты/с (kB/s, MB/s)","units");bytes.Checked=!config.Bits;bytes.Click+=(s,e)=>{config.Bits=false;Save();};
        var bits=MenuItem("Биты/с (kbit/s, Mbit/s)","units");bits.Checked=config.Bits;bits.Click+=(s,e)=>{config.Bits=true;Save();};
        units.DropDownItems.Add(bytes);units.DropDownItems.Add(bits);menu.Items.Add(units);
        var smoothing=MenuItem("Усреднение","smoothing");
        foreach(int value in new[]{0,2,3,5,10}){int seconds=value;var item=MenuItem(Ui.Seconds(seconds),"smoothing",false);item.Checked=config.SmoothingSeconds==seconds;item.ToolTipText+="\n"+Ui.T("Текущее значение: ")+Ui.Seconds(seconds);item.Click+=(s,e)=>{config.SmoothingSeconds=seconds;Save();};smoothing.DropDownItems.Add(item);}menu.Items.Add(smoothing);
        var graph=MenuItem("Фоновый график","graphEnabled");graph.Checked=config.GraphEnabled;graph.Click+=(s,e)=>{config.GraphEnabled=!config.GraphEnabled;Save();};menu.Items.Add(graph);
        Placement layout;lock(gate)layout=placement;bool vertical=layout!=null&&layout.Vertical;
        var resize=MenuItem(vertical?"Изменять высоту":"Изменять ширину",vertical?"resizeHeight":"resize");resize.Checked=config.ResizeEnabled;resize.Click+=(s,e)=>{config.ResizeEnabled=!config.ResizeEnabled;Save();};menu.Items.Add(resize);
        var reset=MenuItem(vertical?"Сбросить высоту":"Сбросить ширину",vertical?"resetHeight":"resetWidth");reset.Click+=(s,e)=>{if(vertical)config.PanelHeight=0;else config.PanelWidth=0;resizedPlacement=null;Save();};menu.Items.Add(reset);
        var languages=MenuItem("Язык","language");string[] codes={"auto","ru","en"};string[] names={Ui.T("Автоматически (Windows)"),"Русский","English"};
        for(int i=0;i<codes.Length;i++){string code=codes[i];var item=MenuItem(names[i],"language",false);item.Checked=config.Language==code;item.Click+=(s,e)=>{menu.Close();config.Language=code;Save();};languages.DropDownItems.Add(item);}menu.Items.Add(languages);
        var settings=MenuItem("Настройки…","settings");settings.Click+=(s,e)=>OpenSettings();menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());var exit=MenuItem("Выход","exit");exit.Click+=(s,e)=>Quit();menu.Items.Add(exit);
    }
    static ToolStripMenuItem MenuItem(string text,string help,bool translate=true){var item=new ToolStripMenuItem(translate?Ui.T(text):text){Tag=text,AutoToolTip=false,ToolTipText=Ui.Help(help)};item.DropDown.ShowItemToolTips=true;return item;}
    void ChangeAdapter(string id){config.AdapterId=id;ResetSampling();Save();}
    void Save(){try{if(savedAdapter!=config.AdapterId)ResetSampling();savedAdapter=config.AdapterId;config.Save();Ui.SetLanguage(config.Language);lock(gate)sampler.SetGraphSmoothing(config.GraphSmoothingSeconds);var nextIcon=AppIcons.Create(config.IconId,32);var previousIcon=tray.Icon;tray.Icon=nextIcon;if(previousIcon!=null)previousIcon.Dispose();menu.Font=AppFonts.Ui(config);needsRedraw=true;networkTimer.Change(0,GraphMath.PollInterval(config.GraphEnabled,config.Interval));uiTimer.Interval=GraphMath.FrameInterval(config.GraphEnabled,config.GraphFps);layoutTimer.Change(0,1000);if(dialog!=null&&!dialog.IsDisposed)dialog.SyncFromConfig();}catch(Exception e){MessageBox.Show(Ui.T("Не удалось сохранить настройки: ")+e.Message,"NetSpeck");}}
    void ResizePanel(int dip,ResizePhase phase){
        if(stopped)return;
        if(phase==ResizePhase.Cancel){resizedPlacement=null;needsRedraw=true;layoutTimer.Change(0,1000);return;}
        Placement current;lock(gate)current=placement;
        if(current==null||!current.Safe)return;
        resizedPlacement=current.WithExtent((int)Math.Round(dip*current.Dpi/96.0));needsRedraw=true;
        if(phase==ResizePhase.Commit){if(current.Vertical)config.PanelHeight=ResizeMath.ToDip(resizedPlacement.Height,current.Dpi);else config.PanelWidth=ResizeMath.ToDip(resizedPlacement.Width,current.Dpi);lock(gate)placement=resizedPlacement;resizedPlacement=null;Save();}
        Tick();
    }
    public void OpenSettings(){
        IntPtr bar=Native.FindWindow("Shell_TrayWnd",null),old=IntPtr.Zero;if(bar!=IntPtr.Zero)old=Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(bar));
        try{
        if(dialog!=null&&!dialog.IsDisposed){dialog.Show();Native.ShowWindow(dialog.Handle,9);dialog.Activate();return;}
        List<Adapter> list;lock(gate)list=new List<Adapter>(adapters);
        dialog=new SettingsForm(config,list);
        dialog.ContextMenuStrip=menu;
        dialog.ApplyRequested+=()=>Save();
        dialog.Show();
        // STARTUPINFO can request SW_HIDE for a background/autostart launch.
        // Consume that first ShowWindow call, then explicitly restore the dialog.
        Native.ShowWindow(dialog.Handle,9);dialog.Activate();
        }finally{if(old!=IntPtr.Zero)Native.SetThreadDpiAwarenessContext(old);}
    }
    public void ResetSampling(){lock(gate){sampler.Reset();}needsRedraw=true;}
    public void Recreate(){lock(gate)placementTracker.Reset();resizedPlacement=null;if(meter!=null){meter.Dispose();meter=null;}needsRedraw=true;layoutTimer.Change(0,1000);ResetSampling();Log.Write("Recreate requested");}
    public void RefreshTray(){tray.Visible=false;tray.Visible=true;}
    public void Quit(){if(stopped)return;stopped=true;uiTimer.Stop();networkTimer.Dispose();layoutTimer.Dispose();if(dialog!=null)dialog.Close();if(meter!=null){meter.Dispose();meter=null;}tray.Visible=false;ExitThread();}
    protected override void Dispose(bool disposing){if(disposing){Quit();uiTimer.Dispose();var icon=tray.Icon;tray.Dispose();if(icon!=null)icon.Dispose();menu.Dispose();controller.Dispose();}base.Dispose(disposing);}
}
internal static class Diagnostics {
    public static string OsVersion(){using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")){return "Windows "+key.GetValue("DisplayVersion")+" "+key.GetValue("CurrentBuild")+"."+key.GetValue("UBR");}}
    public static void Save(string folder){
        Directory.CreateDirectory(folder);var sb=new StringBuilder();sb.AppendLine(OsVersion());sb.AppendLine("UTC: "+DateTime.UtcNow.ToString("o"));
        IntPtr bar=Native.FindWindow("Shell_TrayWnd",null);
        if(bar!=IntPtr.Zero)Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(bar));
        Native.Rect rect;Native.GetWindowRect(bar,out rect);
        sb.AppendLine("Taskbar HWND: "+bar+"; rect: "+rect+"; DPI: "+Native.GetDpiForWindow(bar));
        uint shellPid;Native.GetWindowThreadProcessId(bar,out shellPid);sb.AppendLine("Explorer PID: "+shellPid);
        IntPtr child=Native.FindWindowEx(bar,IntPtr.Zero,null,"NetSpeck.Meter");
        sb.AppendLine("Meter HWND: "+child+"; parent: "+Native.GetParent(child)+"; visible: "+(child!=IntPtr.Zero&&Native.IsWindowVisible(child)));
        if(child!=IntPtr.Zero){int style=Native.GetWindowLong(child,-16),ex=Native.GetWindowLong(child,-20);sb.AppendLine("WS_CHILD="+((style&0x40000000)!=0)+"; WS_EX_TOPMOST="+((ex&8)!=0)+"; WS_EX_NOACTIVATE="+((ex&0x08000000)!=0));}
        Native.Rect cr;Native.GetWindowRect(child,out cr);sb.AppendLine("Meter rect: "+cr);
        var settings=Settings.Load();var list=Network.ReadAdapters();var selected=Network.Select(list,settings.AdapterId);
        sb.AppendLine("Selected interface: "+(selected==null?"none":selected.Name+"; index="+selected.Index));
        foreach(var a in list)sb.AppendLine(a.Name+"; index="+a.Index+"; up="+a.Up+"; in="+a.Received+"; out="+a.Sent);
        File.WriteAllText(Path.Combine(folder,"diagnostics.txt"),sb.ToString(),Encoding.UTF8);
        if(rect.Width>0&&rect.Height>0&&Native.IsWindowVisible(bar))using(var bitmap=new Bitmap(rect.Width,rect.Height)){
            using(var g=Graphics.FromImage(bitmap))g.CopyFromScreen(rect.Left,rect.Top,0,0,bitmap.Size);
            bitmap.Save(Path.Combine(folder,"taskbar.png"),System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
}
