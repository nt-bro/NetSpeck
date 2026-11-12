using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

namespace NetSpeck {
internal sealed class Placement {
    public IntPtr Bar;public Native.Rect Rect;public uint Dpi;public int X,Y,Width,Height,MinimumX,MinimumY,MinimumWidth,MinimumHeight;public bool Vertical;
    public bool Safe;public string Status;
    public int Start {get{return Vertical?Y:X;}}
    public int Extent {get{return Vertical?Height:Width;}}
    public int MinimumStart {get{return Vertical?MinimumY:MinimumX;}}
    public int MinimumExtent {get{return Vertical?MinimumHeight:MinimumWidth;}}
    public bool SameBar(Placement other){return other!=null&&Bar==other.Bar&&Dpi==other.Dpi&&Rect.Left==other.Rect.Left&&Rect.Top==other.Rect.Top&&Rect.Right==other.Rect.Right&&Rect.Bottom==other.Rect.Bottom;}
    public Placement WithExtent(int extent){int end=Start+Extent;extent=Math.Max(MinimumExtent,Math.Min(extent,end-MinimumStart));var p=(Placement)MemberwiseClone();if(Vertical){p.Y=end-extent;p.Height=extent;}else{p.X=end-extent;p.Width=extent;}return p;}
    public Placement WithWidth(int width){return WithExtent(width);}
}
internal enum ResizePhase { Preview,Commit,Cancel }
internal static class TaskbarLayout {
    public static Placement Inspect(Settings settings) {
        var p=new Placement{Bar=Native.FindWindow("Shell_TrayWnd",null),Status="Ожидание панели задач"};
        if(p.Bar==IntPtr.Zero||!Native.GetWindowRect(p.Bar,out p.Rect))return p;
        // UIA and HWND coordinates must use the SAME physical coordinate system.
        IntPtr old=Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(p.Bar));
        try {
            Native.GetWindowRect(p.Bar,out p.Rect);p.Dpi=Native.GetDpiForWindow(p.Bar);if(p.Dpi==0)p.Dpi=96;
            double scale=p.Dpi/96.0;
            p.Vertical=p.Rect.Width<p.Rect.Height;
            int minDip=p.Vertical?Math.Max(64,settings.FontSize*6+16):AppFonts.MinimumWidth(settings);
            int saved=p.Vertical?settings.PanelHeight:settings.PanelWidth;
            int desired=saved>0?saved:(p.Vertical?minDip+16:Math.Max(settings.FontSize*11+26,minDip+12));
            int minimum=(int)Math.Ceiling(minDip*scale),extent=Math.Max(minimum,(int)Math.Ceiling(desired*scale));
            if(p.Vertical){p.MinimumHeight=minimum;p.Width=p.Rect.Width;p.Height=extent;}else{p.MinimumWidth=minimum;p.Width=extent;p.Height=p.Rect.Height;}
            if((p.Vertical?p.Rect.Width:p.Rect.Height)<20*scale){p.Status="Неподдерживаемая геометрия панели задач";return p;}
            var blocked=new List<int[]>();
            var root=AutomationElement.FromHandle(p.Bar);
            var elements=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
            int visibleInteractive=0;
            int ownPid=System.Diagnostics.Process.GetCurrentProcess().Id;
            foreach(AutomationElement el in elements) {
                try {
                    var c=el.Current;if(c.ProcessId==ownPid||c.IsOffscreen)continue;
                    var t=c.ControlType;
                    bool interactive=t==ControlType.Button||t==ControlType.ListItem||t==ControlType.SplitButton||t==ControlType.CheckBox||t==ControlType.ComboBox||t==ControlType.Edit||t==ControlType.Hyperlink||t==ControlType.Slider;
                    if(!interactive)continue;
                    // GetParent includes owners for popup windows; GA_ROOT does not.
                    IntPtr hwnd=new IntPtr(c.NativeWindowHandle);
                    if(hwnd!=IntPtr.Zero&&Native.GetAncestor(hwnd,2)!=p.Bar)continue;
                    var r=c.BoundingRectangle;int first,last;
                    if(r.IsEmpty||!TaskbarGeometry.Project(p.Rect,p.Vertical,r.Left,r.Top,r.Width,r.Height,out first,out last))continue;
                    blocked.Add(new[]{first,last});visibleInteractive++;
                }catch(ElementNotAvailableException){}
            }
            // Fail closed if the shell's UIA provider cannot expose its layout.
            if(visibleInteractive==0){p.Status="Не удалось проверить свободное место панели задач";return p;}
            IntPtr tray=Native.FindWindowEx(p.Bar,IntPtr.Zero,"TrayNotifyWnd",null);Native.Rect tr;
            int end=p.Vertical?p.Rect.Height:p.Rect.Width;
            if(tray!=IntPtr.Zero&&Native.GetWindowRect(tray,out tr)){
                int trayStart=p.Vertical?tr.Top-p.Rect.Top:tr.Left-p.Rect.Left;
                if(trayStart>0)end=Math.Min(end,trayStart);
            }
            int margin=(int)Math.Ceiling(6*scale),start=Gaps.Find(0,end,p.Extent,margin,blocked),minimumStart=margin;
            foreach(var b in blocked)if(b[1]+margin<=start)minimumStart=Math.Max(minimumStart,b[1]+margin);
            if(p.Vertical){p.Y=start;p.MinimumY=minimumStart;}else{p.X=start;p.MinimumX=minimumStart;}
            p.Safe=start>=0;p.Status=p.Safe?"Встроен в панель задач":"Недостаточно места — индикатор временно скрыт";
            return p;
        } finally {Native.SetThreadDpiAwarenessContext(old);}
    }
}
internal sealed class MeterWindow : NativeWindow,IDisposable {
    public readonly IntPtr Bar;public event Action<Point> MenuRequested;
    public event Action<int,ResizePhase> WidthRequested;
    bool resizeEnabled,dragging,ownsCapture;uint dpi=96;Placement last;
    int anchorRight,dragMinimum,dragMaximum,pendingWidth;
    public bool IsResizing {get{return dragging;}}
    public MeterWindow(IntPtr bar) {
        Bar=bar;
        IntPtr old=Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(bar));
        try {
            // Native child HWND from creation; never a desktop/topmost overlay.
            CreateHandle(new CreateParams{Caption="NetSpeck.Meter",Parent=bar,X=0,Y=0,Width=1,Height=1,Style=0x44000000,ExStyle=0x08080080});
            if(Native.GetParent(Handle)!=bar)throw new InvalidOperationException("Taskbar parent verification failed");
        }finally{Native.SetThreadDpiAwarenessContext(old);}
    }
    public void Render(Placement p,Reading r,Settings s,bool dark,SpeedPoint[] points,double now) {
        if(!Native.IsWindow(Handle))return;
        if(dragging&&(!s.ResizeEnabled||last==null||!p.SameBar(last)))CancelDrag();
        resizeEnabled=s.ResizeEnabled;dpi=p.Dpi;last=p;
        if(!p.Safe){Native.ShowWindow(Handle,0);return;}
        IntPtr old=Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(Bar));
        try {
            Native.SetWindowPos(Handle,IntPtr.Zero,p.X,p.Y,p.Width,p.Height,0x10|0x40);
            using(var bm=MeterRenderer.Draw(p,r,s,dark,points,now))Upload(bm);
        }finally{Native.SetThreadDpiAwarenessContext(old);}
    }
    void NotifyWidth(int pixels,ResizePhase phase){var h=WidthRequested;if(h!=null)h(ResizeMath.ToDip(pixels,dpi),phase);}
    void CancelDrag(){if(!dragging)return;dragging=false;if(ownsCapture){ownsCapture=false;Native.ReleaseCapture();}NotifyWidth(0,ResizePhase.Cancel);}
    internal bool BeginResize(int x,bool capture){
        if(!resizeEnabled||last==null||!last.Safe||x<0||x>=7*dpi/96.0)return false;
        Native.Rect rect;Native.GetWindowRect(Handle,out rect);anchorRight=last.Vertical?rect.Bottom:rect.Right;dragMinimum=last.MinimumExtent;dragMaximum=last.Start+last.Extent-last.MinimumStart;pendingWidth=last.Extent;dragging=true;ownsCapture=capture;if(capture)Native.SetCapture(Handle);return true;
    }
    internal void MoveResize(int x){if(!dragging)return;Native.Rect rect;Native.GetWindowRect(Handle,out rect);int width=ResizeMath.WidthFromLeft(anchorRight,(last.Vertical?rect.Top:rect.Left)+x,dragMinimum,dragMaximum);if(width!=pendingWidth){pendingWidth=width;NotifyWidth(width,ResizePhase.Preview);}}
    internal void EndResize(){if(!dragging)return;dragging=false;if(ownsCapture){ownsCapture=false;Native.ReleaseCapture();}NotifyWidth(pendingWidth,ResizePhase.Commit);}
    int MouseAxis(IntPtr parameter){return (short)(((long)parameter>>(last!=null&&last.Vertical?16:0))&0xFFFF);}
    void Upload(Bitmap bitmap) {
        IntPtr screen=Native.GetDC(IntPtr.Zero),mem=IntPtr.Zero,hb=IntPtr.Zero,previous=IntPtr.Zero;
        try {
            mem=Native.CreateCompatibleDC(screen);if(mem==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
            hb=bitmap.GetHbitmap(Color.FromArgb(0));previous=Native.SelectObject(mem,hb);
            Size size=bitmap.Size;Point point=Point.Empty;var blend=new Native.Blend{Alpha=255,Format=1};
            if(!Native.UpdateLayeredWindow(Handle,screen,IntPtr.Zero,ref size,mem,ref point,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"UpdateLayeredWindow");
        }finally {
            if(previous!=IntPtr.Zero)Native.SelectObject(mem,previous);
            if(hb!=IntPtr.Zero)Native.DeleteObject(hb);if(mem!=IntPtr.Zero)Native.DeleteDC(mem);
            if(screen!=IntPtr.Zero)Native.ReleaseDC(IntPtr.Zero,screen);
        }
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x21){m.Result=new IntPtr(3);return;} // MA_NOACTIVATE
        if(m.Msg==0x20){Native.Rect rect;Native.GetWindowRect(Handle,out rect);bool vertical=last!=null&&last.Vertical;int at=vertical?Cursor.Position.Y-rect.Top:Cursor.Position.X-rect.Left;bool edge=resizeEnabled&&at>=0&&at<7*dpi/96.0;Cursor.Current=dragging||edge?(vertical?Cursors.SizeNS:Cursors.SizeWE):Cursors.Default;m.Result=new IntPtr(1);return;}
        if(m.Msg==0x201&&BeginResize(MouseAxis(m.LParam),true)){m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x200&&dragging){MoveResize(MouseAxis(m.LParam));m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x202&&dragging){EndResize();m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x215&&dragging&&ownsCapture){CancelDrag();}
        if(m.Msg==0x7B){CancelDrag();var h=MenuRequested;if(h!=null)h(Cursor.Position);m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x14){m.Result=new IntPtr(1);return;}
        base.WndProc(ref m);
    }
    public void Dispose(){if(Handle!=IntPtr.Zero)DestroyHandle();}
}
}
