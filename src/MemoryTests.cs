using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;

namespace NetSpeck {
internal static class MemoryTests {
    static void Snapshot(StreamWriter output,string stage,int iteration){using(var p=Process.GetCurrentProcess()){
        output.WriteLine(String.Join(",",new[]{stage,iteration.ToString(),p.WorkingSet64.ToString(),p.PrivateMemorySize64.ToString(),GC.GetTotalMemory(false).ToString(),p.HandleCount.ToString()}));output.Flush();
    }}
    [MTAThread] static int Main(string[] args){
        try{
            string mode=args[0];int count=Int32.Parse(args[1],CultureInfo.InvariantCulture);string report=args[2];
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
            var settings=Settings.Load();settings.GraphEnabled=true;var watch=Stopwatch.StartNew();
            using(var output=new StreamWriter(report)){
                output.WriteLine("stage,iterations,resident_bytes,private_bytes,managed_bytes,handles");
                Action step;IDisposable resource=null;int safe=0;
                if(mode=="layout"){
#if LEGACY
                    step=()=>{if(TaskbarLayout.Inspect(settings).Safe)safe++;};
#else
                    var automation=new TaskbarAutomation();resource=automation;step=()=>{if(TaskbarLayout.Inspect(settings,automation).Safe)safe++;};
#endif
                }else if(mode=="render"){
                    var placement=new Placement{Width=240,Height=72,Dpi=144,Safe=true};
                    var points=new SpeedPoint[750];for(int i=0;i<points.Length;i++)points[i]=new SpeedPoint{Time=i*0.2,Down=1000000+Math.Sin(i*0.05)*300000,Up=200000};
                    var reading=new Reading{Connected=true,Down=1300000,Up=200000};
                    step=()=>{using(var image=MeterRenderer.Draw(placement,reading,settings,true,points,150)){};};
                }else throw new ArgumentException("Expected layout or render");
                try{
                    for(int i=0;i<200;i++)step();
                    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
                    long before;int handles;using(var p=Process.GetCurrentProcess()){before=p.PrivateMemorySize64;handles=p.HandleCount;}
                    Snapshot(output,"warm",0);
                    for(int i=1;i<=count;i++){step();if(i%100==0)Snapshot(output,"running",i);}
                    Snapshot(output,"before_test_gc",count);
                    GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Snapshot(output,"after_test_gc",count);
                    long growth;int handleGrowth;using(var p=Process.GetCurrentProcess()){growth=p.PrivateMemorySize64-before;handleGrowth=p.HandleCount-handles;}
                    Console.WriteLine(mode+": "+count+" iterations, "+watch.Elapsed.TotalSeconds.ToString("F1",CultureInfo.InvariantCulture)+"s, retained private growth "+(growth/1048576.0).ToString("F2",CultureInfo.InvariantCulture)+" MiB; handles "+handleGrowth+"; valid layouts "+safe);
#if !LEGACY
                    foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="UIAutomationClient"||assembly.GetName().Name=="UIAutomationClientsideProviders")throw new Exception("Leaking managed automation factory was loaded");
                    if(growth>16*1024*1024||handleGrowth>50)throw new Exception("Retained native memory/handle growth exceeds the regression limit");
#endif
                    if(mode=="layout"&&safe==0)throw new Exception("No valid live taskbar layouts during memory test");
                }finally{if(resource!=null)resource.Dispose();}
            }
            return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
}
