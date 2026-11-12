using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace NetSpeck {
internal static class Native {
    [StructLayout(LayoutKind.Sequential)] internal struct Rect {
        public int Left, Top, Right, Bottom;
        public int Width { get { return Right-Left; } }
        public int Height { get { return Bottom-Top; } }
        public override string ToString() { return Left+","+Top+","+Right+","+Bottom; }
    }
    [StructLayout(LayoutKind.Sequential, Pack=1)] internal struct Blend { public byte Operation, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] internal struct GuiThreadInfo {
        public uint Size,Flags;public IntPtr Active,Focus,Capture,MenuOwner,MoveSize,Caret;public Rect CaretRect;
    }
    internal delegate bool EnumWindowCallback(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint thread,ref GuiThreadInfo info);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr SetCapture(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ReleaseCapture();
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] internal static extern int AddFontResourceEx(string file,uint flags,IntPtr reserved);
    [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window,IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr window,IntPtr dest,IntPtr dst,ref Size size,IntPtr src,ref Point point,uint key,ref Blend blend,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool PostMessage(IntPtr window,uint msg,IntPtr w,IntPtr l);
    [DllImport("iphlpapi.dll")] internal static extern uint GetIfTable2(out IntPtr table);
    [DllImport("iphlpapi.dll")] internal static extern void FreeMibTable(IntPtr table);
    [DllImport("iphlpapi.dll")] internal static extern uint GetBestInterfaceEx(byte[] address,out uint index);

    // SDK netioapi.h: native default packing (8). The table's first row is at offset 8.
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct IfRow {
        public ulong Luid; public uint Index; public Guid Guid;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)] public string Alias;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=257)] public string Description;
        public uint AddressLength;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=32)] public byte[] Address;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=32)] public byte[] PermanentAddress;
        public uint Mtu,Type,TunnelType,MediaType,PhysicalMedium,AccessType,Direction;
        public byte Flags;
        public uint OperStatus,AdminStatus,MediaState;
        public Guid NetworkGuid; public uint ConnectionType;
        public ulong TransmitLinkSpeed,ReceiveLinkSpeed;
        public ulong InOctets,InUcastPkts,InNUcastPkts,InDiscards,InErrors,InUnknownProtos;
        public ulong InUcastOctets,InMulticastOctets,InBroadcastOctets;
        public ulong OutOctets,OutUcastPkts,OutNUcastPkts,OutDiscards,OutErrors;
        public ulong OutUcastOctets,OutMulticastOctets,OutBroadcastOctets,OutQLen;
    }
}
}
