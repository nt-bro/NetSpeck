using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NetSpeck {
internal sealed class Adapter {
    public string Id, Name; public uint Index; public bool Up; public ulong Received,Sent;
    public override string ToString() { return Name+(Up?"":Ui.T(" (отключён)")); }
}
internal sealed class Reading {
    public string AdapterId="",AdapterName=Ui.T("Нет подключения"),Error="";
    public double Down=0,Up=0,RawDown=0,RawUp=0,SampleTime=0; public bool Connected=false;
    public DateTime Time=DateTime.UtcNow;
}
internal sealed class RateCounter {
    string id; ulong down,up; double time;
    public bool Valid { get; private set; }
    public double Interval { get; private set; }
    public void Reset() { id=null;Valid=false;Interval=0; }
    public double[] Sample(string next,ulong input,ulong output,double now,double maxGap) {
        double dt=now-time; double[] rate={0,0};
        Valid=id==next && dt>0 && dt<=maxGap && input>=down && output>=up;
        Interval=Valid?dt:0;
        if(Valid) {
            rate[0]=(input-down)/dt; rate[1]=(output-up)/dt;
        }
        id=next;down=input;up=output;time=now;return rate;
    }
}
// A trailing time-weighted mean. Partial intervals at the window boundary
// contribute only their overlap, so 0.5s and 2s polling have the same meaning.
internal sealed class SpeedAverage {
    sealed class Part {public double Start,End,Down,Up;}
    readonly List<Part> parts=new List<Part>();
    public void Reset(){parts.Clear();}
    public double[] Add(double now,double duration,double down,double up,int seconds){
        if(duration<=0||double.IsNaN(duration)||double.IsInfinity(duration))return new[]{0.0,0.0};
        if(parts.Count>0&&now<=parts[parts.Count-1].End)parts.Clear();
        down=Clean(down);up=Clean(up);
        parts.Add(new Part{Start=now-duration,End=now,Down=down,Up=up});
        parts.RemoveAll(p=>p.End<=now-10);
        if(seconds<=0)return new[]{down,up};
        double start=now-seconds,weight=0,input=0,output=0;
        foreach(var p in parts){double overlap=Math.Max(0,p.End-Math.Max(start,p.Start));weight+=overlap;input+=p.Down*overlap;output+=p.Up*overlap;}
        return weight>0?new[]{input/weight,output/weight}:new[]{0.0,0.0};
    }
    static double Clean(double n){return double.IsNaN(n)||double.IsInfinity(n)||n<0?0:n;}
}
internal sealed class SpeedPoint {
    public double Time,Down,Up,RawDown,RawUp;
}
internal sealed class SpeedHistory {
    public const int KeepSeconds=150; // 120s viewport + warm-up for the strongest graph filter.
    readonly List<SpeedPoint> points=new List<SpeedPoint>();
    public void Clear(){points.Clear();}
    public void Add(Reading r){
        if(points.Count>0&&r.SampleTime<=points[points.Count-1].Time)points.Clear();
        points.Add(new SpeedPoint{Time=r.SampleTime,Down=r.Down,Up=r.Up,RawDown=r.RawDown,RawUp=r.RawUp});
        points.RemoveAll(p=>p.Time<r.SampleTime-KeepSeconds);
        if(points.Count>1024)points.RemoveRange(0,points.Count-1024);
    }
    public SpeedPoint[] Snapshot(){return points.ToArray();}
    public void Resmooth(GraphFilter filter,double seconds){
        filter.Reset();foreach(var point in points){var value=filter.Add(point.Time,point.RawDown,point.RawUp,seconds);point.Down=value[0];point.Up=value[1];}
    }
}
// Three cascaded low-pass stages suppress packet-batch ripple. Their time
// constants sum to the selected response time. The exact zero-order-hold
// solution keeps the response independent of timer jitter and sample cadence.
internal sealed class GraphFilter {
    bool ready;double time,down,up,down1,up1,down2,up2;
    public void Reset(){ready=false;}
    public double[] Add(double now,double input,double output,double seconds=0.6){
        double dt=now-time;
        if(!ready||dt<=0||dt>10||seconds<=0){down=down1=down2=input;up=up1=up2=output;ready=true;}
        else{
            double q=3*dt/seconds,e=Math.Exp(-q),d1=down1-input,d2=down2-input,u1=up1-output,u2=up2-output;
            down=Math.Max(0,input+(down-input+d2*q+d1*q*q/2)*e);
            up=Math.Max(0,output+(up-output+u2*q+u1*q*q/2)*e);
            down2=Math.Max(0,input+(d2+d1*q)*e);up2=Math.Max(0,output+(u2+u1*q)*e);
            down1=Math.Max(0,input+d1*e);up1=Math.Max(0,output+u1*e);
        }
        time=now;return new[]{down,up};
    }
}
internal sealed class SampleCadence {
    double next;int interval;bool ready;
    public void Reset(){ready=false;}
    public bool Take(double now,int milliseconds){
        double seconds=milliseconds/1000.0;
        if(!ready||interval!=milliseconds){ready=true;interval=milliseconds;next=now+seconds;return true;}
        if(now+0.000001<next)return false;
        next+=(Math.Floor(Math.Max(0,now-next)/seconds)+1)*seconds;return true;
    }
}
internal sealed class TrafficSampler {
    readonly RateCounter numbers=new RateCounter(),graph=new RateCounter();
    readonly SpeedAverage average=new SpeedAverage();readonly GraphFilter filter=new GraphFilter();
    readonly SampleCadence cadence=new SampleCadence();
    double graphSmoothing=double.NaN;
    public readonly SpeedHistory History=new SpeedHistory();public Reading Current=new Reading();
    public void Reset(){numbers.Reset();graph.Reset();average.Reset();filter.Reset();cadence.Reset();History.Clear();Current=new Reading();graphSmoothing=double.NaN;}
    public void SetGraphSmoothing(double seconds){if(seconds==graphSmoothing)return;graphSmoothing=seconds;History.Resmooth(filter,seconds);}
    public Reading Update(Adapter adapter,double now,int interval,int smoothing,bool graphEnabled,string missingName,double graphSmoothingSeconds=0.6){
        if(adapter==null){Reset();Current.AdapterName=missingName;return Current;}
        if(Current.AdapterId!=adapter.Id)Reset();
        SetGraphSmoothing(graphSmoothingSeconds);
        if(cadence.Take(now,interval)){
            var rates=numbers.Sample(adapter.Id,adapter.Received,adapter.Sent,now,Math.Max(10,interval/1000.0*4));
            var next=new Reading{AdapterId=adapter.Id,AdapterName=adapter.Name,Connected=true,SampleTime=now,RawDown=rates[0],RawUp=rates[1]};
            if(numbers.Valid){var mean=average.Add(now,numbers.Interval,rates[0],rates[1],smoothing);next.Down=mean[0];next.Up=mean[1];}else average.Reset();
            Current=next;
        }
        if(graphEnabled){
            var rates=graph.Sample(adapter.Id,adapter.Received,adapter.Sent,now,10);
            if(graph.Valid){var smooth=filter.Add(now,rates[0],rates[1],graphSmoothing);History.Add(new Reading{SampleTime=now,Down=smooth[0],Up=smooth[1],RawDown=rates[0],RawUp=rates[1]});}
            else{History.Clear();filter.Reset();}
        }else{graph.Reset();filter.Reset();History.Clear();}
        return Current;
    }
}
internal static class GraphMath {
    public const int SampleInterval=200;
    public static int PollInterval(bool graphEnabled,int numberInterval){return graphEnabled?Math.Min(SampleInterval,numberInterval):numberInterval;}
    public static int FrameInterval(bool graphEnabled,int fps){return graphEnabled?Math.Max(15,(int)Math.Round(1000.0/fps)):250;}
    public static double EdgeOpacity(double position){return Math.Max(0,Math.Min(1,Math.Min(position/0.10,(1-position)/0.10)));}
    public static double[] CurveSlopes(double[] times,double[] values){
        int n=values.Length;var slopes=new double[n];if(n<2)return slopes;
        var h=new double[n-1];var secant=new double[n-1];
        for(int i=0;i<n-1;i++){h[i]=Math.Max(0.000001,times[i+1]-times[i]);secant[i]=(values[i+1]-values[i])/h[i];}
        slopes[0]=secant[0];slopes[n-1]=secant[n-2];
        for(int i=1;i<n-1;i++)if(secant[i-1]*secant[i]>0){double a=2*h[i]+h[i-1],b=h[i]+2*h[i-1];slopes[i]=(a+b)/(a/secant[i-1]+b/secant[i]);}
        return slopes;
    }
    public static double CurveValue(double left,double right,double first,double last,double startSlope,double endSlope,double at){
        double h=Math.Max(0.000001,right-left),t=Math.Max(0,Math.Min(1,(at-left)/h)),t2=t*t,t3=t2*t;
        double value=(2*t3-3*t2+1)*first+(t3-2*t2+t)*h*startSlope+(-2*t3+3*t2)*last+(t3-t2)*h*endSlope;
        return Math.Max(Math.Min(first,last),Math.Min(Math.Max(first,last),value));
    }
    public static double Maximum(SpeedPoint[] points,double now,int seconds,bool raw,bool automatic,double fixedMaximum){
        if(!automatic)return Math.Max(1,fixedMaximum);
        double max=1000;
        foreach(var p in points)if(p.Time>=now-seconds&&p.Time<=now){max=Math.Max(max,Math.Max(raw?p.RawDown:p.Down,raw?p.RawUp:p.Up));}
        return max*1.1;
    }
    public static double Fraction(double speed,double maximum){return Math.Max(0,Math.Min(1,speed/Math.Max(1,maximum)));}
}
internal static class ResizeMath {
    public static int WidthFromLeft(int right,int mouseX,int minimum,int maximum){return Math.Max(minimum,Math.Min(maximum,right-mouseX));}
    public static int ToDip(int pixels,uint dpi){return (int)Math.Round(pixels*96.0/Math.Max(1,dpi));}
}
internal static class Rates {
    public static string Format(double bytes,bool bits) {
        double value=Math.Max(0,double.IsNaN(bytes)||double.IsInfinity(bytes)?0:bytes)*(bits?8:1);
        string[] units=bits?new[]{"bit/s","kbit/s","Mbit/s","Gbit/s","Tbit/s"}:new[]{"B/s","kB/s","MB/s","GB/s","TB/s"};
        int i=0;while(value>=1000 && i<units.Length-1){value/=1000;i++;}
        // Prevent the rounded display from reading "1000 kB/s".
        if(i<units.Length-1 && Math.Round(value,value<100?1:0)>=1000){value/=1000;i++;}
        return value.ToString(value<100 && i>0?"0.0":"0",CultureInfo.InvariantCulture)+" "+units[i];
    }
}
internal static class Network {
    public static List<Adapter> ReadAdapters() {
        IntPtr table;uint error=Native.GetIfTable2(out table);
        if(error!=0)throw new Win32Exception((int)error,"GetIfTable2");
        var list=new List<Adapter>();
        try {
            int count=Marshal.ReadInt32(table);int stride=Marshal.SizeOf(typeof(Native.IfRow));
            if(count<0||count>65536)throw new InvalidOperationException("Invalid interface count");
            for(int i=0;i<count;i++) {
                var row=(Native.IfRow)Marshal.PtrToStructure(IntPtr.Add(table,8+i*stride),typeof(Native.IfRow));
                if(row.Type==24 || (row.Flags&2)!=0)continue;
                list.Add(new Adapter{Id=row.Guid.ToString("D"),Index=row.Index,Name=String.IsNullOrEmpty(row.Alias)?row.Description:row.Alias,Up=row.OperStatus==1,Received=row.InOctets,Sent=row.OutOctets});
            }
        } finally {Native.FreeMibTable(table);}
        return list;
    }
    public static Adapter Select(List<Adapter> list,string selected) {
        if(!String.IsNullOrEmpty(selected))return list.Find(a=>a.Id==selected&&a.Up);
        // Local route lookup only: these calls send no packets. One interface only.
        byte[] ipv4=new byte[16];ipv4[0]=2;ipv4[4]=1;ipv4[5]=1;ipv4[6]=1;ipv4[7]=1;
        byte[] ipv6=new byte[28];ipv6[0]=23;ipv6[8]=0x26;ipv6[9]=0x06;ipv6[10]=0x47;ipv6[11]=0;
        ipv6[12]=0x47;ipv6[13]=0;ipv6[22]=0x11;ipv6[23]=0x11;
        uint index;
        if(Native.GetBestInterfaceEx(ipv4,out index)==0){var a=list.Find(x=>x.Index==index&&x.Up);if(a!=null)return a;}
        if(Native.GetBestInterfaceEx(ipv6,out index)==0){var a=list.Find(x=>x.Index==index&&x.Up);if(a!=null)return a;}
        return null;
    }
}
// Pure interval arithmetic, shared by runtime and deterministic layout tests.
internal static class Gaps {
    public static int Find(int left,int right,int width,int margin,List<int[]> blocked) {
        if(right-left<width+margin*2)return -1;
        blocked.Sort((a,b)=>a[0].CompareTo(b[0]));
        int cursor=left+margin,best=-1;
        foreach(var b in blocked) {
            int start=Math.Max(left,b[0]-margin),end=Math.Min(right,b[1]+margin);
            if(end<=cursor)continue;
            if(start-cursor>=width)best=start-width;
            cursor=Math.Max(cursor,end);
        }
        if(right-margin-cursor>=width)best=right-margin-width;
        return best;
    }
}
}
