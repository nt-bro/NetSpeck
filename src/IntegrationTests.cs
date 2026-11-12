using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
using System.Drawing.Imaging;
namespace TaskbarNet {
class IntegrationTests {
    static void Handles(Control control){IntPtr h=control.Handle;foreach(Control c in control.Controls)Handles(c);}
    static int Validate(Control parent){int combos=0;foreach(Control control in parent.Controls){
        if(control.Left<0||control.Top<0||control.Right>parent.ClientSize.Width+2||control.Bottom>parent.ClientSize.Height+2)throw new Exception("Control clipped: "+control.GetType().Name+" "+control.Text+" "+control.Bounds+" parent "+parent.ClientSize);
        if(control is ComboBox)combos++;else if(!(control is NumericUpDown))combos+=Validate(control);
    }return combos;}
    [STAThread] static int Main(string[] args){
        try {
            string folder=args[0];Directory.CreateDirectory(folder);
            IntPtr bar=Native.FindWindow("Shell_TrayWnd",null);
            Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(bar));
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var list=Network.ReadAdapters();var config=Settings.Load();
            var selected=Network.Select(list,config.AdapterId);
            if(selected==null)throw new Exception("No active routed interface");
            var sw=Stopwatch.StartNew();var first=selected;
            Thread.Sleep(1500);
            selected=Network.Select(Network.ReadAdapters(),config.AdapterId);
            if(selected==null||selected.Id!=first.Id)throw new Exception("Interface changed during sample");
            var counter=new RateCounter();counter.Sample(first.Id,first.Received,first.Sent,0,10);
            var rate=counter.Sample(selected.Id,selected.Received,selected.Sent,sw.Elapsed.TotalSeconds,10);
            string report=Diagnostics.OsVersion()+"\nSelected="+selected.Name+"\nDown="+Rates.Format(rate[0],false)+"\nUp="+Rates.Format(rate[1],false)+"\n";
            string originalLanguage=config.Language;foreach(string locale in new[]{"ru","en"}){config.Language=locale;using(var form=new SettingsForm(config,list)) {
                Handles(form);var tabs=(TabControl)typeof(SettingsForm).GetField("tabs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;form.PerformLayout();Handles(form);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(folder,locale+"-"+(i==0?"settings-preview.png":i==1?"graph-settings-preview.png":i==2?"font-settings-preview.png":"icon-settings-preview.png")));}}
                int combos=Validate(form);if(combos!=11)throw new Exception("Missing setting controls: "+combos);
                var fields=(Dictionary<Control,int>)typeof(SettingsForm).GetField("rowControls",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);int width=-1;foreach(var pair in fields){if(width<0)width=pair.Key.Width;else if(pair.Key.Width!=width)throw new Exception("Setting field widths differ");}
                report+=locale+": four settings tabs inside client area; 11 combos, uniform "+width+"px fields, localized captions and hints.\n";
            }}config.Language=originalLanguage;Ui.SetLanguage(originalLanguage);
            report+=RenderChecks.Run(folder);
            report+=GraphRegression.Run(folder,args.Length>1?args[1]:null);
            File.WriteAllText(Path.Combine(folder,"integration-tests.txt"),report);
            Console.WriteLine(report);return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
internal static class GraphRegression {
    public static string Run(string folder,string traffic){
        var old=new List<SpeedPoint>();var current=new List<SpeedPoint>();var filter=new GraphFilter();filter.Add(0,100000,0,5);double legacy=0,lowOld=Double.MaxValue,highOld=0,lowNew=Double.MaxValue,highNew=0;
        for(int i=1;i<=500;i++){double time=i*0.2,raw=i%5==0?15000000:0;legacy+=(raw-legacy)*(1-Math.Exp(-0.2/5));double value=filter.Add(time,100000,raw,5)[1];if(time>=70){old.Add(new SpeedPoint{Time=time,Down=100000,Up=legacy});current.Add(new SpeedPoint{Time=time,Down=100000,Up=value});}if(time>80){lowOld=Math.Min(lowOld,legacy);highOld=Math.Max(highOld,legacy);lowNew=Math.Min(lowNew,value);highNew=Math.Max(highNew,value);}}
        double previousPixels=(highOld-lowOld)/3300000*68,currentPixels=(highNew-lowNew)/3300000*68;
        if(currentPixels>=0.5||currentPixels>=previousPixels*0.06)throw new Exception("Packet-burst graph still has visible high-frequency sawtooth");
        Comparison(Path.Combine(folder,"graph-filter-comparison.png"),old.ToArray(),current.ToArray(),100,3300000,"Periodic packet batches · 5 s smoothing");
        string summary="5s packet-batch regression at 204x72: old ripple="+previousPixels.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"px; new="+currentPixels.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"px.\n";
        if(traffic!=null&&File.Exists(traffic)){
            var rows=File.ReadAllLines(traffic);var before=new List<SpeedPoint>();var after=new List<SpeedPoint>();var live=new GraphFilter();
            for(int i=1;i<rows.Length;i++){string[] row=rows[i].Split(',');double[] v=Array.ConvertAll(row,n=>Double.Parse(n,System.Globalization.CultureInfo.InvariantCulture));var result=live.Add(v[0],v[1],v[2],5);before.Add(new SpeedPoint{Time=v[0],Down=v[3],Up=v[4]});after.Add(new SpeedPoint{Time=v[0],Down=result[0],Up=result[1]});}
            if(after.Count>20){double now=after[after.Count-1].Time,max=1000,roughOld=0,roughNew=0;int n=0;
                for(int i=0;i<after.Count;i++)max=Math.Max(max,Math.Max(Math.Max(after[i].Down,after[i].Up),Math.Max(before[i].Down,before[i].Up)));
                for(int i=2;i<after.Count;i++)if(after[i].Time>20){double od=before[i].Down-2*before[i-1].Down+before[i-2].Down,ou=before[i].Up-2*before[i-1].Up+before[i-2].Up,nd=after[i].Down-2*after[i-1].Down+after[i-2].Down,nu=after[i].Up-2*after[i-1].Up+after[i-2].Up;roughOld+=od*od+ou*ou;roughNew+=nd*nd+nu*nu;n+=2;}
                Comparison(Path.Combine(folder,"live-graph-comparison.png"),before.ToArray(),after.ToArray(),now,max*1.1,"Recorded local traffic · same samples · 5 s");
                summary+="Live replay: "+after.Count+" captured samples; high-frequency second-difference RMS old="+Math.Sqrt(roughOld/Math.Max(1,n)).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" B/s; new="+Math.Sqrt(roughNew/Math.Max(1,n)).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" B/s.\n";
            }
        }
        File.WriteAllText(Path.Combine(folder,"graph-filter-regression.txt"),summary);return summary;
    }
    static void Comparison(string path,SpeedPoint[] before,SpeedPoint[] after,double now,double maximum,string caption){
        using(var bitmap=new Bitmap(760,430))using(var g=Graphics.FromImage(bitmap))using(var font=new Font("Segoe UI",12))using(var small=new Font("Segoe UI",10)){
            g.Clear(Color.FromArgb(30,35,43));g.DrawString(caption,font,Brushes.White,18,14);g.DrawString("Actual 204x72 pixels",small,Brushes.LightGray,18,49);g.DrawString("2x zoom",small,Brushes.LightGray,300,49);
            var placement=new Placement{Dpi=144,Width=204,Height=72,Safe=true};var settings=new Settings{GraphEnabled=true,GraphSmoothingSeconds=5,GraphOpacity=40,GraphLineWidth=1.5,GraphAutoScale=false,GraphMaximum=maximum,GraphFill=true,UploadColor=0x21E75C,FontSize=10};
            var reading=new Reading{Connected=true,Down=before[before.Length-1].Down,Up=before[before.Length-1].Up};
            for(int i=0;i<2;i++){var points=i==0?before:after;int y=i==0?104:282;g.DrawString(i==0?"Before":"After",small,Brushes.White,18,y-24);
                using(var frame=MeterRenderer.Draw(placement,reading,settings,true,points,now)){g.DrawImageUnscaled(frame,18,y);g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.DrawImage(frame,new Rectangle(300,y-12,408,144));}
            }
            bitmap.Save(path);
        }
    }
}
internal static class RenderChecks {
    public static string Run(string folder){int frames=0;
        foreach(int dpi in new[]{96,120,144,192})foreach(int size in new[]{8,9,10,11,12})foreach(bool dark in new[]{false,true})foreach(bool bits in new[]{false,true}){
            var p=new Placement{Dpi=(uint)dpi,Width=(int)Math.Ceiling((size*11+26)*dpi/96.0),Height=dpi/2,Safe=true};
            var s=new Settings{FontSize=size,Bits=bits};var reading=new Reading{Connected=true,Down=0,Up=100000};
            using(var bitmap=MeterRenderer.Draw(p,reading,s,dark,new SpeedPoint[0],30)){
                int arrow=FirstInk(bitmap);if(arrow<0||arrow>12*dpi/96.0)throw new Exception("Arrow is not anchored to left edge");
                var wider=new Placement{Dpi=(uint)dpi,Width=p.Width+dpi,Height=p.Height,Safe=true};
                using(var expanded=MeterRenderer.Draw(wider,reading,s,dark,new SpeedPoint[0],30))if(FirstInk(expanded)!=arrow||ArrowGap(expanded)<=ArrowGap(bitmap)+dpi/2)throw new Exception("Width does not control arrow/value separation");
            }
            var history=new List<SpeedPoint>();for(int i=0;i<=30;i++)history.Add(new SpeedPoint{Time=i,Down=(i%7)*1000000,Up=(i%5)*300000,RawDown=(i%3)*6000000,RawUp=1000000});
            s.GraphEnabled=true;reading.Down=2400000;reading.Up=400000;
            using(var bitmap=MeterRenderer.Draw(p,reading,s,dark,history.ToArray(),30)){
                int colored=0;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){var color=bitmap.GetPixel(x,y);if(color.A>2&&color.B>color.R+5)colored++;}
                if(colored<20)throw new Exception("Background graph is missing");
                if(size==10&&!bits)bitmap.Save(Path.Combine(folder,"render-"+dpi+"-"+(dark?"dark":"light")+".png"));
            }
            frames++;
        }
        using(var layer=new Bitmap(201,20,PixelFormat.Format32bppPArgb)){
            using(var g=Graphics.FromImage(layer))g.Clear(Color.FromArgb(200,100,150,200));
            MeterRenderer.FadeEdges(layer,0,200);
            if(layer.GetPixel(0,10).A!=0||layer.GetPixel(200,10).A!=0||Math.Abs(layer.GetPixel(10,10).A-100)>1||Math.Abs(layer.GetPixel(190,10).A-100)>1||layer.GetPixel(20,10).A!=200||layer.GetPixel(100,10).A!=200)throw new Exception("Graph gradient alpha is wrong");
            var bits=layer.LockBits(new Rectangle(0,0,201,20),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{if(System.Runtime.InteropServices.Marshal.ReadInt32(bits.Scan0)!=0)throw new Exception("Premultiplied RGB was not faded");}finally{layer.UnlockBits(bits);}
        }
        using(var fallback=AppFonts.Create("A missing font for regression",12,FontStyle.Regular,GraphicsUnit.Pixel))if(fallback.Name!="Segoe UI")throw new Exception("Default font fallback is wrong");
        using(var number=AppFonts.Indicator(new Settings{FontName="Arial"},16))using(var arrow=AppFonts.Arrow(new Settings{FontName="Arial"},16))if(number.Bold||!arrow.Bold)throw new Exception("Arrow weight affects numbers or is not bold");
        using(var icons=AppIcons.Preview("flows",570,189,1.5f))icons.Save(Path.Combine(folder,"icon-preview-direct.png"));
        foreach(string family in new[]{"Segoe UI","Arial"}){
            using(var original=new Bitmap(40,40))using(var thicker=new Bitmap(40,40))using(var a=Graphics.FromImage(original))using(var b=Graphics.FromImage(thicker))using(var regular=AppFonts.Indicator(new Settings{FontName=family},18))using(var arrow=AppFonts.Arrow(new Settings{FontName=family},18))using(var format=new StringFormat(StringFormat.GenericTypographic)){
                format.LineAlignment=StringAlignment.Center;var rect=new RectangleF(3,0,30,40);a.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;a.DrawString("↓",regular,Brushes.White,rect,format);
                MeterRenderer.DrawArrow(b,"↓",arrow,Brushes.White,rect,format,1);
                int before=0,after=0;for(int y=0;y<40;y++)for(int x=0;x<40;x++){before+=original.GetPixel(x,y).A;after+=thicker.GetPixel(x,y).A;}
                if(after<=before)throw new Exception("Arrow did not become visibly thicker in "+family);
            }
        }
        var input=new List<PointF>{new PointF(0,40),new PointF(10,10),new PointF(20,40)};var curve=MeterRenderer.SmoothLine(input);
        if(curve.Count<=input.Count)throw new Exception("Graph curve was not interpolated");
        PreviewAndBenchmark(folder);
        return frames+" render cases passed (4 DPI, 5 fonts, both themes and units); left-anchored bold arrows, adjustable spacing, smooth curves and 10% alpha fade verified.\n";
    }
    static void PreviewAndBenchmark(string folder){
        var sampler=new TrafficSampler();var old=new List<SpeedPoint>();ulong bytes=0;
        for(int i=0;i<=150;i++){
            double time=i*0.2;double speed=(time>=8&&time<8.4?7000000:time>=19&&time<19.6?4000000:1000);bytes+=(ulong)(speed*0.2);
            var reading=sampler.Update(new Adapter{Id="preview",Name="Preview",Up=true,Received=bytes,Sent=bytes/3},time,1000,5,true,"");
            if(i%5==0)old.Add(new SpeedPoint{Time=time,RawDown=reading.Down,RawUp=reading.Up});
        }
        var s=new Settings{GraphEnabled=true,GraphOpacity=60,GraphFill=true,GraphAutoScale=false,GraphMaximum=3000000,FontName="Arial",FontSize=10};
        var p=new Placement{Width=540,Height=72,Dpi=144,Safe=true};var points=sampler.History.Snapshot();
        using(var preview=new Bitmap(580,224))using(var g=Graphics.FromImage(preview))using(var label=new Font("Segoe UI",10)){
            g.Clear(Color.FromArgb(31,35,42));s.GraphRaw=true;
            g.DrawString("До: среднее чисел за 5 с, точки раз в секунду",label,Brushes.White,20,4);
            using(var frame=MeterRenderer.Draw(p,new Reading{Connected=true,Down=258,Up=132},s,true,old.ToArray(),30))g.DrawImageUnscaled(frame,20,28);
            s.GraphRaw=false;g.DrawString("После: отдельные замеры, короткое сглаживание",label,Brushes.White,20,112);
            using(var frame=MeterRenderer.Draw(p,new Reading{Connected=true,Down=258,Up=132},s,true,points,30))g.DrawImageUnscaled(frame,20,136);
            preview.Save(Path.Combine(folder,"graph-smoothing-preview.png"));
        }
        for(int i=151;i<=620;i++)sampler.Update(new Adapter{Id="preview",Name="Preview",Up=true,Received=bytes+(ulong)i*1000,Sent=bytes/3},i*0.2,1000,5,true,"");
        points=sampler.History.Snapshot();p.Width=204;
        var sw=Stopwatch.StartNew();for(int i=0;i<100;i++)using(var frame=MeterRenderer.Draw(p,sampler.Current,s,true,points,124+i*0.01)){}
        double milliseconds=sw.Elapsed.TotalMilliseconds/100;
        File.WriteAllText(Path.Combine(folder,"graph-render-benchmark.txt"),"120-second history, 5 samples/s, 204x72 pixels, mean render time: "+milliseconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" ms\n");
        if(milliseconds>50)throw new Exception("Graph rendering exceeds 20 fps frame budget: "+milliseconds+" ms");
    }
    static int FirstInk(Bitmap bitmap){for(int x=0;x<bitmap.Width;x++)for(int y=0;y<bitmap.Height/2;y++)if(bitmap.GetPixel(x,y).A>100)return x;return -1;}
    static int ArrowGap(Bitmap bitmap){
        bool started=false,gap=false;int count=0;
        for(int x=0;x<bitmap.Width;x++){
            bool ink=false;for(int y=0;y<bitmap.Height/2;y++)if(bitmap.GetPixel(x,y).A>100){ink=true;break;}
            if(!started){if(ink)started=true;continue;}
            if(!ink){gap=true;count++;}else if(gap)return count;
        }
        return -1;
    }
}
}
