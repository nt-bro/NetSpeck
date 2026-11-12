using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

namespace TaskbarNet {
internal sealed class Placement {
    public IntPtr Bar;public Native.Rect Rect;public uint Dpi;public int X,Width,Height,MinimumX,MinimumWidth;
    public bool Safe;public string Status;
    public Placement WithWidth(int width){int right=X+Width;width=Math.Max(MinimumWidth,Math.Min(width,right-MinimumX));return new Placement{Bar=Bar,Rect=Rect,Dpi=Dpi,X=right-width,Width=width,Height=Height,MinimumX=MinimumX,MinimumWidth=MinimumWidth,Safe=Safe,Status=Status};}
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
            int minDip=AppFonts.MinimumWidth(settings);
            int desired=settings.PanelWidth>0?settings.PanelWidth:Math.Max(settings.FontSize*11+26,minDip+12);
            p.MinimumWidth=(int)Math.Ceiling(minDip*scale);p.Width=Math.Max(p.MinimumWidth,(int)Math.Ceiling(desired*scale));p.Height=p.Rect.Height;
            if(p.Rect.Width<p.Rect.Height||p.Rect.Height<20*scale){p.Status="Неподдерживаемая геометрия панели задач";return p;}
            var blocked=new List<int[]>();
            var root=AutomationElement.FromHandle(p.Bar);
            var elements=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
            int visibleInteractive=0;
            int ownPid=System.Diagnostics.Process.GetCurrentProcess().Id;
            foreach(AutomationElement el in elements) {
                try {
                    var c=el.Current;if(c.ProcessId==ownPid||c.IsOffscreen)continue;
                    var t=c.ControlType;
                    bool interactive=t==ControlType.Button||t==ControlType.ListItem||t==ControlType.MenuItem||t==ControlType.SplitButton||t==ControlType.CheckBox||t==ControlType.ComboBox||t==ControlType.Edit||t==ControlType.Hyperlink||t==ControlType.Slider;
                    if(!interactive)continue;var r=c.BoundingRectangle;
                    if(r.IsEmpty||r.Width<=0||r.Bottom<=p.Rect.Top||r.Top>=p.Rect.Bottom)continue;
                    int l=(int)Math.Floor(r.Left)-p.Rect.Left,rr=(int)Math.Ceiling(r.Right)-p.Rect.Left;
                    blocked.Add(new[]{l,rr});visibleInteractive++;
                }catch(ElementNotAvailableException){}
            }
            // Fail closed if the shell's UIA provider cannot expose its layout.
            if(visibleInteractive==0){p.Status="Не удалось проверить свободное место панели задач";return p;}
            IntPtr tray=Native.FindWindowEx(p.Bar,IntPtr.Zero,"TrayNotifyWnd",null);Native.Rect tr;
            int right=p.Rect.Width;
            if(tray!=IntPtr.Zero&&Native.GetWindowRect(tray,out tr)&&tr.Left>p.Rect.Left)right=Math.Min(right,tr.Left-p.Rect.Left);
            p.X=Gaps.Find(0,right,p.Width,(int)Math.Ceiling(6*scale),blocked);
            int margin=(int)Math.Ceiling(6*scale);p.MinimumX=margin;
            foreach(var b in blocked)if(b[1]+margin<=p.X)p.MinimumX=Math.Max(p.MinimumX,b[1]+margin);
            p.Safe=p.X>=0;p.Status=p.Safe?"Встроен в панель задач":"Недостаточно места — индикатор временно скрыт";
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
            CreateHandle(new CreateParams{Caption="TaskbarNet.Meter",Parent=bar,X=0,Y=0,Width=1,Height=1,Style=0x44000000,ExStyle=0x08080080});
            if(Native.GetParent(Handle)!=bar)throw new InvalidOperationException("Taskbar parent verification failed");
        }finally{Native.SetThreadDpiAwarenessContext(old);}
    }
    public void Render(Placement p,Reading r,Settings s,bool dark,SpeedPoint[] points,double now) {
        if(!Native.IsWindow(Handle))return;
        if(dragging&&(!s.ResizeEnabled||p.Dpi!=dpi))CancelDrag();
        resizeEnabled=s.ResizeEnabled;dpi=p.Dpi;last=p;
        if(!p.Safe){Native.ShowWindow(Handle,0);return;}
        IntPtr old=Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(Bar));
        try {
            Native.SetWindowPos(Handle,IntPtr.Zero,p.X,0,p.Width,p.Height,0x10|0x40);
            using(var bm=MeterRenderer.Draw(p,r,s,dark,points,now))Upload(bm);
        }finally{Native.SetThreadDpiAwarenessContext(old);}
    }
    void NotifyWidth(int pixels,ResizePhase phase){var h=WidthRequested;if(h!=null)h(ResizeMath.ToDip(pixels,dpi),phase);}
    void CancelDrag(){if(!dragging)return;dragging=false;if(ownsCapture){ownsCapture=false;Native.ReleaseCapture();}NotifyWidth(0,ResizePhase.Cancel);}
    internal bool BeginResize(int x,bool capture){
        if(!resizeEnabled||last==null||!last.Safe||x<0||x>=7*dpi/96.0)return false;
        Native.Rect rect;Native.GetWindowRect(Handle,out rect);anchorRight=rect.Right;dragMinimum=last.MinimumWidth;dragMaximum=last.X+last.Width-last.MinimumX;pendingWidth=last.Width;dragging=true;ownsCapture=capture;if(capture)Native.SetCapture(Handle);return true;
    }
    internal void MoveResize(int x){if(!dragging)return;Native.Rect rect;Native.GetWindowRect(Handle,out rect);int width=ResizeMath.WidthFromLeft(anchorRight,rect.Left+x,dragMinimum,dragMaximum);if(width!=pendingWidth){pendingWidth=width;NotifyWidth(width,ResizePhase.Preview);}}
    internal void EndResize(){if(!dragging)return;dragging=false;if(ownsCapture){ownsCapture=false;Native.ReleaseCapture();}NotifyWidth(pendingWidth,ResizePhase.Commit);}
    static int MouseX(IntPtr parameter){return (short)((long)parameter&0xFFFF);}
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
        if(m.Msg==0x20){Native.Rect rect;Native.GetWindowRect(Handle,out rect);bool edge=resizeEnabled&&Cursor.Position.X-rect.Left<(int)Math.Ceiling(7*dpi/96.0);Cursor.Current=dragging||edge?Cursors.SizeWE:Cursors.Default;m.Result=new IntPtr(1);return;}
        if(m.Msg==0x201&&BeginResize(MouseX(m.LParam),true)){m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x200&&dragging){MoveResize(MouseX(m.LParam));m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x202&&dragging){EndResize();m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x215&&dragging&&ownsCapture){CancelDrag();}
        if(m.Msg==0x7B){CancelDrag();var h=MenuRequested;if(h!=null)h(Cursor.Position);m.Result=IntPtr.Zero;return;}
        if(m.Msg==0x14){m.Result=new IntPtr(1);return;}
        base.WndProc(ref m);
    }
    public void Dispose(){if(Handle!=IntPtr.Zero)DestroyHandle();}
}
}
