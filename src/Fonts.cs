using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Security.Cryptography;

namespace TaskbarNet {
internal sealed class FontChoice {
    public string Name;public bool Custom;
    public override string ToString(){return Name+(Custom?Ui.T(" (свой)"):"");}
}
internal static class AppFonts {
    static readonly object gate=new object();
    static readonly PrivateFontCollection custom=new PrivateFontCollection();
    static readonly HashSet<string> loaded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    static bool initialized;
    public static readonly string Folder=Path.Combine(Settings.Folder,"Fonts");
    public static void Initialize(){lock(gate){if(initialized)return;initialized=true;if(Directory.Exists(Folder))foreach(string file in Directory.GetFiles(Folder)){try{Load(file);}catch(Exception e){Log.Write("Font load: "+e.Message);}}}}
    static string[] Load(string path){
        if(!loaded.Contains(path)){
            custom.AddFontFile(path);
            // FR_PRIVATE: makes the face available to this process's native UI
            // controls; neither installs it in Windows nor changes the registry.
            Native.AddFontResourceEx(path,0x10,IntPtr.Zero);loaded.Add(path);
        }
        var names=new List<string>();foreach(var family in custom.Families){names.Add(family.Name);family.Dispose();}return names.ToArray();
    }
    public static string[] Import(string source,string destination=null){
        string ext=Path.GetExtension(source).ToLowerInvariant();
        if(ext!=".ttf"&&ext!=".otf"&&ext!=".ttc")throw new ArgumentException(TaskbarNet.Ui.T("Выберите файл TTF, OTF или TTC."));
        var info=new FileInfo(source);if(!info.Exists||info.Length==0||info.Length>50*1024*1024)throw new ArgumentException(TaskbarNet.Ui.T("Не удалось прочитать файл шрифта или он слишком большой."));
        var names=new List<string>();
        try{using(var check=new PrivateFontCollection()){check.AddFontFile(source);foreach(var family in check.Families){names.Add(family.Name);family.Dispose();}if(names.Count==0)throw new ArgumentException();}}
        catch(ArgumentException){throw new ArgumentException(TaskbarNet.Ui.T("Windows не смогла загрузить этот шрифт. Используйте TTF/TTC или OpenType с TrueType-контурами."));}
        string hash;using(var stream=File.OpenRead(source))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        string directory=destination??Folder;Directory.CreateDirectory(directory);string target=Path.Combine(directory,hash+ext);
        if(!File.Exists(target))File.Copy(source,target,false);
        Initialize();lock(gate)Load(target);return names.ToArray();
    }
    public static List<FontChoice> Choices(){
        Initialize();var all=new Dictionary<string,FontChoice>(StringComparer.OrdinalIgnoreCase);
        using(var installed=new InstalledFontCollection())foreach(var family in installed.Families){all[family.Name]=new FontChoice{Name=family.Name};family.Dispose();}
        lock(gate)foreach(var family in custom.Families){all[family.Name]=new FontChoice{Name=family.Name,Custom=true};family.Dispose();}
        var choices=new List<FontChoice>(all.Values);choices.Sort((a,b)=>StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name));return choices;
    }
    public static Font Create(string name,float size,FontStyle style,GraphicsUnit unit){
        Initialize();
        lock(gate){var families=custom.Families;try{
            foreach(var family in families)if(String.Equals(family.Name,name,StringComparison.OrdinalIgnoreCase)){
                if(family.IsStyleAvailable(style))return NewFont(family,size,style,unit);
                try{using(var installed=new FontFamily(name))if(installed.IsStyleAvailable(style))return NewFont(installed,size,style,unit);}catch(ArgumentException){}
                return NewFont(family,size,style,unit);
            }
        }finally{foreach(var family in families)family.Dispose();}}
        try{using(var family=new FontFamily(name))return NewFont(family,size,style,unit);}
        catch(ArgumentException){return new Font("Segoe UI",size,FontStyle.Regular,unit);}
    }
    static Font NewFont(FontFamily family,float size,FontStyle style,GraphicsUnit unit){
        if(!family.IsStyleAvailable(style)){foreach(var fallback in new[]{FontStyle.Regular,FontStyle.Bold,FontStyle.Italic,FontStyle.Bold|FontStyle.Italic})if(family.IsStyleAvailable(fallback)){style=fallback;break;}}
        return new Font(family,size,style,unit);
    }
    public static Font Indicator(Settings s,float pixels){return Create(s.FontName,pixels,(FontStyle)s.FontStyle,GraphicsUnit.Pixel);}
    public static Font Arrow(Settings s,float pixels){return Create(s.FontName,pixels,(FontStyle)s.FontStyle|FontStyle.Bold,GraphicsUnit.Pixel);}
    public static Font Ui(Settings s){return Create(s.FontForUi?s.FontName:"Segoe UI",9,s.FontForUi?(FontStyle)s.FontStyle:FontStyle.Regular,GraphicsUnit.Point);}
    public static Font Heading(Settings s){return Create(s.FontForUi?s.FontName:"Segoe UI",12,FontStyle.Bold,GraphicsUnit.Point);}
    public static int MinimumWidth(Settings s){
        using(var bitmap=new Bitmap(1,1))using(var g=Graphics.FromImage(bitmap))using(var f=Indicator(s,Math.Min(s.FontSize*96f/72f,44f/2.35f)))using(var arrow=Arrow(s,f.Size))using(var format=new StringFormat(StringFormat.GenericTypographic)){
            float max=0;foreach(string text in new[]{"999 MB/s","99.9 MB/s","999 Mbit/s","99.9 Mbit/s"})max=Math.Max(max,g.MeasureString(text,f,10000,format).Width);
            return Math.Max(60,(int)Math.Ceiling(max+g.MeasureString("↓",arrow,10000,format).Width+14));
        }
    }
}
}
