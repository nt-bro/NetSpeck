using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
namespace NetSpeck {
class Tests {
    static int count;
    static void Check(bool result,string name){if(!result)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
    static bool Near(double a,double b){return Math.Abs(a-b)<0.0001;}
    static int Main(){try{
        string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"migration-"+Guid.NewGuid().ToString("N"));
        string legacyFolder=Path.Combine(fixture,"old"),migratedFolder=Path.Combine(fixture,"new");
        try {
            PreferencesMigration.CopyIfNeeded(legacyFolder,migratedFolder);
            Check(!Directory.Exists(migratedFolder),"first install does not create legacy preferences");
            Directory.CreateDirectory(Path.Combine(legacyFolder,"Fonts"));Directory.CreateDirectory(Path.Combine(legacyFolder,"Icons"));
            File.WriteAllText(Path.Combine(legacyFolder,"settings.ini"),"Language=ru\nFont=11\nIcon=speedometer\n");
            File.WriteAllText(Path.Combine(legacyFolder,"Fonts","custom.ttf"),"font");File.WriteAllText(Path.Combine(legacyFolder,"Icons","custom.ico"),"icon");
            File.WriteAllText(Path.Combine(legacyFolder,"taskbarnet.log"),"private log");File.WriteAllText(Path.Combine(legacyFolder,"Fonts","unrelated.txt"),"private file");
            PreferencesMigration.CopyIfNeeded(legacyFolder,migratedFolder);
            Check(File.ReadAllText(Path.Combine(migratedFolder,"settings.ini"))==File.ReadAllText(Path.Combine(legacyFolder,"settings.ini")),"rename preserves preferences and the original settings");
            Check(File.Exists(Path.Combine(migratedFolder,"Fonts","custom.ttf"))&&File.Exists(Path.Combine(migratedFolder,"Icons","custom.ico"))&&!File.Exists(Path.Combine(migratedFolder,"taskbarnet.log"))&&!File.Exists(Path.Combine(migratedFolder,"Fonts","unrelated.txt")),"rename carries imported assets without unrelated files or logs");
            File.WriteAllText(Path.Combine(migratedFolder,"settings.ini"),"Language=en\n");PreferencesMigration.CopyIfNeeded(legacyFolder,migratedFolder);
            Check(File.ReadAllText(Path.Combine(migratedFolder,"settings.ini"))=="Language=en\n","later launches retain new preferences");
        } finally {if(Directory.Exists(fixture))Directory.Delete(fixture,true);}
        Check(Ui.Resolve("auto",new System.Globalization.CultureInfo("ru-RU"))=="ru","first-run automatic language follows Russian Windows UI");
        Check(Ui.Resolve("auto",new System.Globalization.CultureInfo("en-US"))=="en"&&Ui.Resolve("auto",new System.Globalization.CultureInfo("de-DE"))=="en","automatic language defaults to English for other Windows UI languages");
        Check(Ui.Resolve("en",new System.Globalization.CultureInfo("ru-RU"))=="en"&&Ui.Resolve("ru",new System.Globalization.CultureInfo("en-US"))=="ru","explicit language overrides system UI language");
        Ui.SetLanguage("en");Check(Ui.T("Настройки…")=="Settings…"&&Ui.Help("graphSmoothing").Contains("Graph-only"),"English labels and explanatory hints are available");Ui.SetLanguage("ru");
        var c=new RateCounter();
        Check(Near(c.Sample("a",1000,2000,1,10)[0],0),"first sample establishes baseline");
        var r=c.Sample("a",3500,3250,3.5,10);Check(Near(r[0],1000)&&Near(r[1],500),"actual elapsed time, both directions");
        Check(Near(c.Sample("a",5,5,4,10)[0],0),"counter reset");
        Check(Near(c.Sample("b",999999,999999,5,10)[0],0),"adapter switch resets baseline");
        Check(Near(c.Sample("b",1999999,1999999,100,10)[0],0),"wake/stale sample resets baseline");
        Check(Near(c.Sample("b",2000099,2000199,101,10)[0],100),"normal sampling resumes");
        c.Reset();Check(Near(c.Sample("b",3000000,3000000,102,10)[0],0),"explicit resume/reset");
        Check(Rates.Format(1000000,false)=="1.0 MB/s","decimal bytes");
        Check(Rates.Format(125000,true)=="1.0 Mbit/s","bytes-to-bits conversion");
        Check(Rates.Format(0,false)=="0 B/s","idle");
        Check(Rates.Format(999999,false)=="1.0 MB/s","rounded unit boundary");
        Check(Rates.Format(double.NaN,false)=="0 B/s","invalid rate");
        Check(Marshal.SizeOf(typeof(Native.IfRow))==1352,"native interface row size");
        Check(Marshal.OffsetOf(typeof(Native.IfRow),"InOctets").ToInt32()==1208,"64-bit inbound counter offset");
        Check(Marshal.OffsetOf(typeof(Native.IfRow),"OutOctets").ToInt32()==1280,"64-bit outbound counter offset");
        Check(Gaps.Find(0,1000,100,10,new List<int[]>{new[]{0,500},new[]{800,1000}})==690,"rightmost safe gap");
        Check(Gaps.Find(0,1000,100,10,new List<int[]>{new[]{0,450},new[]{400,950}})==-1,"full taskbar hides indicator");
        Check(Gaps.Find(0,1000,100,10,new List<int[]>{new[]{500,550},new[]{0,100}})==890,"centered taskbar gap");
        var avg=new SpeedAverage();
        Check(Near(avg.Add(1,1,100000,200000,3)[0],100000),"average warms up without invented zero samples");
        avg.Add(2,1,7000000,400000,3);var smoothed=avg.Add(3,1,100000,600000,3);
        Check(Near(smoothed[0],2400000)&&Near(smoothed[1],400000),"three-second average damps spikes in both directions");
        avg.Add(4,1,100000,800000,3);Check(Near(avg.Add(5,1,100000,1000000,3)[0],100000),"old peak expires from trailing window");
        avg.Reset();avg.Add(2,2,100,200,3);var weighted=avg.Add(4,2,700,800,3);
        Check(Near(weighted[0],500)&&Near(weighted[1],600),"partial interval uses time weighting");
        var half=new SpeedAverage();half.Add(0.5,0.5,100,200,3);half.Add(1,0.5,100,200,3);
        half.Add(1.5,0.5,100,200,3);half.Add(2,0.5,100,200,3);half.Add(2.5,0.5,700,800,3);half.Add(3,0.5,700,800,3);half.Add(3.5,0.5,700,800,3);
        Check(Near(half.Add(4,0.5,700,800,3)[0],weighted[0]),"smoothing independent of polling interval");
        Check(Near(avg.Add(5,1,12345,67890,0)[0],12345),"smoothing can be disabled");
        avg.Reset();Check(Near(avg.Add(100,1,10,20,3)[0],10),"reset does not retain pre-sleep or old-adapter peaks");
        var history=new SpeedHistory();for(int i=0;i<300;i++)history.Add(new Reading{SampleTime=i,Down=i,Up=i*2});
        Check(history.Snapshot().Length<=SpeedHistory.KeepSeconds+1,"graph history and filter warm-up are bounded");history.Clear();Check(history.Snapshot().Length==0,"graph reset clears history");
        var samples=new[]{new SpeedPoint{Time=1,Down=100,Up=200,RawDown=7000000},new SpeedPoint{Time=30,Down=1000,Up=2000,RawDown=9000}};
        Check(Near(GraphMath.Maximum(samples,30,15,false,true,1),2200),"automatic graph scale ignores expired peaks");
        Check(Near(GraphMath.Maximum(samples,30,30,true,true,1),7700000),"raw graph source retains actual peaks");
        Check(Near(GraphMath.Maximum(samples,30,30,false,false,10000000),10000000),"fixed graph scale respected");
        Check(Near(GraphMath.Fraction(999,100),1)&&Near(GraphMath.Fraction(-10,100),0),"graph clips peaks safely");
        Check(Near(GraphMath.EdgeOpacity(0),0)&&Near(GraphMath.EdgeOpacity(1),0),"graph ends fully transparent");
        Check(Near(GraphMath.EdgeOpacity(0.05),0.5)&&Near(GraphMath.EdgeOpacity(0.95),0.5),"both ten-percent fades are linear");
        Check(Near(GraphMath.EdgeOpacity(0.1),1)&&Near(GraphMath.EdgeOpacity(0.5),1)&&Near(GraphMath.EdgeOpacity(0.9),1),"middle eighty percent keeps configured opacity");
        var filter=new GraphFilter();filter.Add(0,0,0);double peak=filter.Add(0.2,7000000,3500000)[0],maximumPulse=peak,tail=peak;
        for(int i=2;i<=10;i++){tail=filter.Add(i*0.2,0,0)[0];maximumPulse=Math.Max(maximumPulse,tail);}
        Check(peak>0&&maximumPulse<7000000&&tail<maximumPulse*0.05,"graph pulse forms a rounded peak and decays without a flat cap");
        var burstFilter=new GraphFilter();double legacy=0,minOld=Double.MaxValue,maxOld=0,minNew=Double.MaxValue,maxNew=0;burstFilter.Add(0,0,0,5);
        for(int i=1;i<=500;i++){double speed=i%5==0?15000000:0;legacy+=(speed-legacy)*(1-Math.Exp(-0.2/5));double value=burstFilter.Add(i*0.2,speed,speed/2,5)[0];if(i>400){minOld=Math.Min(minOld,legacy);maxOld=Math.Max(maxOld,legacy);minNew=Math.Min(minNew,value);maxNew=Math.Max(maxNew,value);}}
        Check(maxNew-minNew<(maxOld-minOld)*0.06,"five-second smoothing rejects periodic packet-batch sawtooth over sixteen times better");
        Check((maxNew-minNew)/(3000000*1.1)*68<0.5,"residual sawtooth is below half a physical pixel on a 72px panel");
        var monotone=new GraphFilter();monotone.Add(0,0,0,5);double lastStep=0;bool noOvershoot=true;for(int i=1;i<=250;i++){double value=monotone.Add(i*0.2,1000000,500000,5)[0];noOvershoot&=value>=lastStep-0.0001&&value<=1000000.0001;lastStep=value;}
        Check(noOvershoot&&lastStep>999999,"strong smoothing preserves steady rates without ringing or overshoot");
        var fa=new GraphFilter();var fb=new GraphFilter();fa.Add(0,0,0);fb.Add(0,0,0);double a=0,b=0;
        for(int i=1;i<=5;i++)a=fa.Add(i*0.2,1000,500)[0];for(int i=1;i<=10;i++)b=fb.Add(i*0.1,1000,500)[0];
        Check(Near(a,b),"graph smoothing uses elapsed time rather than sample count");
        filter.Reset();Check(Near(filter.Add(100,100,200)[0],100),"graph filter reset discards the previous peak");
        var direct=new GraphFilter();direct.Add(0,0,0,0);var raw=direct.Add(0.2,7000,3500,0);
        Check(Near(raw[0],7000)&&Near(raw[1],3500),"zero graph smoothing returns raw rates in both directions");
        var weak=new GraphFilter();var strong=new GraphFilter();weak.Add(0,0,0,0.1);strong.Add(0,0,0,2.5);
        Check(weak.Add(0.2,7000,3500,0.1)[0]>strong.Add(0.2,7000,3500,2.5)[0],"higher graph smoothing damps the same burst more strongly");
        var adjustable=new TrafficSampler();
        for(int i=0;i<=25;i++)adjustable.Update(new Adapter{Id="adjust",Name="Test",Received=(ulong)(i>=5?700000:0),Up=true},i*0.2,1000,5,true,"",0.6);
        var beforeSmoothing=adjustable.History.Snapshot();var previousCurve=new double[beforeSmoothing.Length];for(int i=0;i<previousCurve.Length;i++)previousCurve[i]=beforeSmoothing[i].Down;
        var fixedNumbers=adjustable.Current;adjustable.SetGraphSmoothing(2.5);var rebuilt=adjustable.History.Snapshot();
        Check(Math.Abs(rebuilt[4].Down-previousCurve[4])>1&&rebuilt.Length==previousCurve.Length,"changing smoothing rebuilds the existing graph history immediately");
        Check(Object.ReferenceEquals(adjustable.Current,fixedNumbers),"changing graph smoothing does not reset or recalculate numeric readings");
        adjustable.SetGraphSmoothing(0);bool unfiltered=true;foreach(var point in adjustable.History.Snapshot())unfiltered&=Near(point.Down,point.RawDown)&&Near(point.Up,point.RawUp);
        Check(unfiltered,"zero smoothing also restores raw values across the visible history");
        adjustable.SetGraphSmoothing(0.6);bool reversible=true;var restored=adjustable.History.Snapshot();for(int i=0;i<restored.Length;i++)reversible&=Near(restored[i].Down,previousCurve[i]);
        Check(reversible,"restoring smoothing reproduces the original curve from retained raw samples");
        var replay=new GraphFilter();foreach(var point in restored)replay.Add(point.Time,point.RawDown,point.RawUp,0.6);
        double continuation=replay.Add(5.2,0,0,0.6)[0];adjustable.Update(new Adapter{Id="adjust",Name="Test",Received=700000,Up=true},5.2,1000,5,true,"",0.6);var continued=adjustable.History.Snapshot();
        Check(Near(continued[continued.Length-1].Down,continuation),"live sampling continues from rebuilt filter state without a seam");
        var sampler=new TrafficSampler();var withoutGraph=new TrafficSampler();bool stable=true,equal=true;int changes=0;Reading previous=null;
        for(int i=0;i<=50;i++){
            var adapter=new Adapter{Id="a",Name="Test",Received=(ulong)(i*1000+(i>=11?700000:0)),Sent=(ulong)(i*2000),Up=true};
            var number=sampler.Update(adapter,i*0.2,1000,5,true,"");
            if(i%5==0){var reference=withoutGraph.Update(adapter,i*0.2,1000,5,false,"");equal&=Near(number.Down,reference.Down)&&Near(number.Up,reference.Up);}
            if(!Object.ReferenceEquals(previous,number)){changes++;if(i%5!=0)stable=false;}previous=number;
        }
        Check(stable&&changes==11&&equal,"fast graph sampling preserves numeric cadence and five-second averages");
        Check(sampler.History.Snapshot().Length==50,"graph records five independent samples per second");
        sampler.Update(new Adapter{Id="b",Name="Other",Received=9999999,Up=true},10.2,1000,5,true,"");
        Check(sampler.History.Snapshot().Length==0&&Near(sampler.Current.Down,0),"adapter change resets both independent sampling streams");
        sampler.Update(null,11,1000,5,true,"Offline");Check(!sampler.Current.Connected&&sampler.Current.AdapterName=="Offline","disconnect clears graph and numeric state");
        Check(GraphMath.PollInterval(true,2000)==200&&GraphMath.PollInterval(false,2000)==2000,"fast polling stops when the graph is disabled");
        Check(GraphMath.FrameInterval(true,20)==50&&GraphMath.FrameInterval(true,30)==33&&GraphMath.FrameInterval(false,30)==250,"graph frame rate is independent of numeric refresh");
        double[] tx={0,0.2,0.8,1.0,1.6},vy={0,50,50,100,0};var slopes=GraphMath.CurveSlopes(tx,vy);bool bounded=true,endpoints=true;
        for(int i=0;i<tx.Length-1;i++)for(int j=0;j<=100;j++){
            double value=GraphMath.CurveValue(tx[i],tx[i+1],vy[i],vy[i+1],slopes[i],slopes[i+1],tx[i]+(tx[i+1]-tx[i])*j/100);
            bounded&=value>=Math.Min(vy[i],vy[i+1])-0.000001&&value<=Math.Max(vy[i],vy[i+1])+0.000001;
            if(j==0)endpoints&=Near(value,vy[i]);if(j==100)endpoints&=Near(value,vy[i+1]);
        }
        Check(bounded&&endpoints,"smooth curve passes through samples without negative values or invented peaks");
        Check(Near(slopes[1],0)&&Near(slopes[2],0)&&Near(slopes[3],0),"curve joins flatten at plateaus and local extrema");
        Check(ResizeMath.WidthFromLeft(500,300,100,400)==200,"dragging left edge keeps right anchor");
        Check(ResizeMath.WidthFromLeft(500,490,100,400)==100&&ResizeMath.WidthFromLeft(500,0,100,400)==400,"resize clamps to text and free-space bounds");
        Check(ResizeMath.ToDip(300,144)==200&&ResizeMath.ToDip(400,192)==200,"width remains consistent across DPI");
        Console.WriteLine("All "+count+" checks passed.");return 0;
    }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
}
}
