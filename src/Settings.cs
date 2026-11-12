using System;
using System.IO;
using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace TaskbarNet {
internal sealed class Settings {
    public string AdapterId="";public bool Bits;public int FontSize=9,Interval=1000;
    public string Language="auto";
    public string IconId="flows";
    public string FontName="Segoe UI";public int FontStyle=0,PanelWidth=0;public bool FontForUi=false,ResizeEnabled=false;
    public int SmoothingSeconds=3;
    public bool GraphEnabled=false,GraphAutoScale=true,GraphFill=true;
    public double GraphSmoothingSeconds=0.6;
    public bool GraphRaw {get{return GraphSmoothingSeconds<=0;}set{if(value)GraphSmoothingSeconds=0;else if(GraphSmoothingSeconds<=0)GraphSmoothingSeconds=0.6;}}
    public int GraphSeconds=30,GraphOpacity=24,GraphFps=20;
    public double GraphLineWidth=1,GraphMaximum=10000000;
    public int DownloadColor=0x38BDF8,UploadColor=0x34D399;
    public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TaskbarNet");
    public static readonly string FilePath=Path.Combine(Folder,"settings.ini");
    public static Settings Load() {
        var s=new Settings();bool legacyRaw=false,hasGraphSmoothing=false;
        try {if(File.Exists(FilePath))foreach(string line in File.ReadAllLines(FilePath)){
            int at=line.IndexOf('=');if(at<0)continue;string k=line.Substring(0,at),v=line.Substring(at+1);int n;Guid g;double d;
            if(k=="Adapter"&&(v==""||Guid.TryParse(v,out g)))s.AdapterId=v;
            if(k=="Language"&&(v=="auto"||v=="ru"||v=="en"))s.Language=v;
            if(k=="Icon"&&(v=="flows"||v=="speedometer"||System.Text.RegularExpressions.Regex.IsMatch(v,@"^custom:[0-9a-fA-F]{64}$")))s.IconId=v;
            if(k=="Bits")s.Bits=v=="1";
            if(k=="Font"&&int.TryParse(v,out n)&&n>=6&&n<=24)s.FontSize=n;
            if(k=="FontName"&&v.Length>0&&v.Length<=128)s.FontName=v;
            if(k=="FontStyle"&&int.TryParse(v,out n)&&n>=0&&n<=3)s.FontStyle=n;
            if(k=="FontForUi")s.FontForUi=v=="1";
            if(k=="PanelWidth"&&int.TryParse(v,out n)&&(n==0||n>=60&&n<=1200))s.PanelWidth=n;
            if(k=="ResizeEnabled")s.ResizeEnabled=v=="1";
            if(k=="Interval"&&int.TryParse(v,out n)&&(n==500||n==1000||n==2000))s.Interval=n;
            if(k=="Smoothing"&&int.TryParse(v,out n)&&(n==0||n==2||n==3||n==5||n==10))s.SmoothingSeconds=n;
            if(k=="Graph")s.GraphEnabled=v=="1";
            if(k=="GraphAutoScale")s.GraphAutoScale=v!="0";
            if(k=="GraphFill")s.GraphFill=v!="0";
            if(k=="GraphRaw")legacyRaw=v=="1";
            if(k=="GraphSmoothing"&&double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out d)&&d>=0&&d<=5){s.GraphSmoothingSeconds=d;hasGraphSmoothing=true;}
            if(k=="GraphSeconds"&&int.TryParse(v,out n)&&(n==15||n==30||n==60||n==120))s.GraphSeconds=n;
            if(k=="GraphOpacity"&&int.TryParse(v,out n)&&n>=5&&n<=60)s.GraphOpacity=n;
            if(k=="GraphFps"&&int.TryParse(v,out n)&&(n==5||n==10||n==20||n==30))s.GraphFps=n;
            if(k=="GraphLineWidth"&&double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out d)&&d>=0.5&&d<=3)s.GraphLineWidth=d;
            if(k=="GraphMaximum"&&double.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out d)&&d>=1000&&d<=1e12)s.GraphMaximum=d;
            if(k=="DownloadColor"&&v.Length==6&&int.TryParse(v,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out n))s.DownloadColor=n;
            if(k=="UploadColor"&&v.Length==6&&int.TryParse(v,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out n))s.UploadColor=n;
        }}catch(Exception e){Log.Write("Settings load: "+e.Message);}if(!hasGraphSmoothing&&legacyRaw)s.GraphSmoothingSeconds=0;return s;
    }
    public void Save() {
        Directory.CreateDirectory(Folder);
        string temp=FilePath+".tmp";
        string text="Adapter="+AdapterId+"\r\nBits="+(Bits?1:0)+"\r\nFont="+FontSize+"\r\nInterval="+Interval+"\r\n";
        text+="Language="+Language+"\r\n";
        text+="Icon="+IconId+"\r\n";
        text+="FontName="+FontName.Replace("\r","").Replace("\n","")+"\r\nFontStyle="+FontStyle+"\r\nFontForUi="+(FontForUi?1:0)+"\r\nPanelWidth="+PanelWidth+"\r\nResizeEnabled="+(ResizeEnabled?1:0)+"\r\n";
        text+="Smoothing="+SmoothingSeconds+"\r\nGraph="+(GraphEnabled?1:0)+"\r\nGraphSeconds="+GraphSeconds+"\r\nGraphOpacity="+GraphOpacity+"\r\n";
        text+="GraphAutoScale="+(GraphAutoScale?1:0)+"\r\nGraphFill="+(GraphFill?1:0)+"\r\nGraphRaw="+(GraphRaw?1:0)+"\r\nGraphFps="+GraphFps+"\r\n";
        text+="GraphSmoothing="+GraphSmoothingSeconds.ToString(CultureInfo.InvariantCulture)+"\r\n";
        text+="GraphLineWidth="+GraphLineWidth.ToString(CultureInfo.InvariantCulture)+"\r\nGraphMaximum="+GraphMaximum.ToString(CultureInfo.InvariantCulture)+"\r\n";
        text+="DownloadColor="+DownloadColor.ToString("X6")+"\r\nUploadColor="+UploadColor.ToString("X6")+"\r\n";
        File.WriteAllText(temp,text,Encoding.UTF8);
        if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);
    }
    public static bool AutoStart {
        get {using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){return key!=null&&key.GetValue("TaskbarNet")!=null;}}
        set {using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){
            if(value)key.SetValue("TaskbarNet","\""+System.Windows.Forms.Application.ExecutablePath+"\"");else key.DeleteValue("TaskbarNet",false);
        }}
    }
}
internal static class Log {
    static readonly object gate=new object();
    public static void Write(string text) {
        try {lock(gate){Directory.CreateDirectory(Settings.Folder);string p=Path.Combine(Settings.Folder,"taskbarnet.log");if(File.Exists(p)&&new FileInfo(p).Length>200000)File.WriteAllText(p,"");File.AppendAllText(p,DateTime.Now.ToString("s",CultureInfo.InvariantCulture)+" "+text+Environment.NewLine);}}catch{}
    }
}
}
