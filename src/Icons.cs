using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace TaskbarNet {
internal sealed class IconChoice {
    public string Id,Name;
    public override string ToString(){return Id=="flows"?Ui.T("1 — Потоки"):Id=="speedometer"?Ui.T("3 — Спидометр"):Name;}
}
internal static class AppIcons {
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    public static readonly string Folder=Path.Combine(Settings.Folder,"Icons");
    public static bool ValidId(string id){return id=="flows"||id=="speedometer"||id!=null&&System.Text.RegularExpressions.Regex.IsMatch(id,@"^custom:[0-9a-fA-F]{64}$");}
    static string CustomFile(string id,string folder){return Path.Combine(folder,id.Substring(7)+".ico");}
    public static Icon Create(string id,int size){
        if(ValidId(id)&&id.StartsWith("custom:",StringComparison.Ordinal))try{return FromBytes(File.ReadAllBytes(CustomFile(id,Folder)),size);}catch(Exception e){Log.Write("Custom icon fallback: "+e.Message);}
        return Builtin(id=="speedometer"?"speedometer":"flows",size);
    }
    static Icon Builtin(string id,int size){
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TaskbarNet.Icons."+id+".ico")){
            if(stream==null)throw new InvalidOperationException("Missing built-in icon resource: "+id);
            using(var buffer=new MemoryStream()){stream.CopyTo(buffer);return FromBytes(buffer.ToArray(),size);}
        }
    }
    internal static Icon FromBytes(byte[] bytes,int size){
        // Decode PNG-compressed ICO frames explicitly: .NET Framework's legacy
        // Icon.ToBitmap path can otherwise produce uninitialized preview pixels.
        int chosen=-1,best=Int32.MaxValue,count=bytes.Length>=6?BitConverter.ToUInt16(bytes,4):0;
        for(int i=0;i<count&&6+(i+1)*16<=bytes.Length;i++){
            int at=6+i*16,width=bytes[at]==0?256:bytes[at],length=BitConverter.ToInt32(bytes,at+8),offset=BitConverter.ToInt32(bytes,at+12);
            if(offset<0||length<8||offset>bytes.Length-length||bytes[offset]!=137||bytes[offset+1]!=80||bytes[offset+2]!=78||bytes[offset+3]!=71)continue;
            int score=width>=size?width-size:10000+size-width;if(score<best){best=score;chosen=at;}
        }
        Bitmap image=null;
        try{
            if(chosen>=0){int length=BitConverter.ToInt32(bytes,chosen+8),offset=BitConverter.ToInt32(bytes,chosen+12);using(var stream=new MemoryStream(bytes,offset,length))using(var png=Image.FromStream(stream))image=new Bitmap(png);}
            else using(var stream=new MemoryStream(bytes))using(var original=new Icon(stream,size,size))image=original.ToBitmap();
            using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb)){
                using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,new Rectangle(0,0,size,size));}
                IntPtr handle=bitmap.GetHicon();try{using(var borrowed=Icon.FromHandle(handle))return (Icon)borrowed.Clone();}finally{DestroyIcon(handle);}
            }
        }finally{if(image!=null)image.Dispose();}
    }
    public static List<IconChoice> Choices(string selected){
        var choices=new List<IconChoice>{new IconChoice{Id="flows"},new IconChoice{Id="speedometer"}};bool found=selected=="flows"||selected=="speedometer";
        if(Directory.Exists(Folder))foreach(string file in Directory.GetFiles(Folder,"*.ico")){
            string hash=Path.GetFileNameWithoutExtension(file),id="custom:"+hash;if(!ValidId(id))continue;
            string name=hash.Substring(0,8);try{string metadata=Path.ChangeExtension(file,".name");if(File.Exists(metadata))name=File.ReadAllText(metadata).Trim();}catch(IOException){}
            choices.Add(new IconChoice{Id=id,Name=name});if(id==selected)found=true;
        }
        if(!found&&ValidId(selected))choices.Add(new IconChoice{Id=selected,Name=Ui.T("Своя иконка (файл недоступен)")});return choices;
    }
    public static string Import(string source,string destination=null){
        string extension=Path.GetExtension(source).ToLowerInvariant();if(extension!=".ico"&&extension!=".png")throw new ArgumentException(Ui.T("Выберите файл ICO или PNG."));
        var info=new FileInfo(source);if(!info.Exists||info.Length==0||info.Length>10*1024*1024)throw new ArgumentException(Ui.T("Файл иконки пуст, недоступен или больше 10 МБ."));
        byte[] converted;
        if(extension==".ico"){
            using(var stream=File.OpenRead(source))using(var check=new Icon(stream,256,256)){if(check.Width<1)throw new ArgumentException(Ui.T("Не удалось загрузить иконку."));}
            converted=File.ReadAllBytes(source);
        }else{
            using(var stream=File.OpenRead(source))using(var image=Image.FromStream(stream,true,true)){
                if(image.RawFormat.Guid!=ImageFormat.Png.Guid||image.Width>4096||image.Height>4096)throw new ArgumentException(Ui.T("Используйте PNG размером не более 4096 × 4096."));
                converted=ToIco(image);
            }
        }
        string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(converted)).Replace("-","").ToLowerInvariant();
        string folder=destination??Folder;Directory.CreateDirectory(folder);string target=Path.Combine(folder,hash+".ico");if(!File.Exists(target))File.WriteAllBytes(target,converted);
        File.WriteAllText(Path.Combine(folder,hash+".name"),Path.GetFileNameWithoutExtension(source),new UTF8Encoding(false));return "custom:"+hash;
    }
    internal static byte[] ToIco(Image source){
        int[] sizes={16,20,24,32,40,48,64,128,256};var images=new List<byte[]>();
        foreach(int size in sizes)using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb)){
            using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.CompositingQuality=CompositingQuality.HighQuality;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                double factor=Math.Min((double)size/source.Width,(double)size/source.Height);float width=(float)(source.Width*factor),height=(float)(source.Height*factor);
                using(var attributes=new ImageAttributes()){attributes.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(source,Rectangle.Round(new RectangleF((size-width)/2,(size-height)/2,width,height)),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);}
            }
            using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());}
        }
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
            foreach(var image in images)writer.Write(image);writer.Flush();return stream.ToArray();
        }
    }
    public static Bitmap Preview(string id,int width,int height,float scale){
        var bitmap=new Bitmap(width,height);using(var g=Graphics.FromImage(bitmap))using(var font=new Font("Segoe UI",9*scale,FontStyle.Regular,GraphicsUnit.Pixel))using(var format=new StringFormat{Alignment=StringAlignment.Center}){
            g.Clear(Color.White);int[] sizes={16,24,32,64};for(int i=0;i<sizes.Length;i++){
                int pixels=(int)Math.Round(sizes[i]*scale),x=(int)Math.Round(width*(i+0.5)/4),middle=(int)Math.Round(50*scale);
                using(var icon=Create(id,pixels))using(var image=icon.ToBitmap())g.DrawImageUnscaled(image,x-pixels/2,middle-pixels/2);
                g.DrawString(sizes[i].ToString(CultureInfo.InvariantCulture),font,Brushes.DimGray,new PointF(x,98*scale),format);
            }
        }return bitmap;
    }
}
}
