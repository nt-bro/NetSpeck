using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace NetSpeck {
internal struct TaskbarElement {
    public int ProcessId,ControlType,Window;public bool Offscreen;
    public double Left,Top,Width,Height;
    public bool Interactive {get{return ControlType==50000||ControlType==50002||ControlType==50003||ControlType==50004||ControlType==50005||ControlType==50007||ControlType==50015||ControlType==50031;}}
}

// Native UIA avoids the managed legacy proxy factory, which retained hundreds
// of thousands of WindowsRebar/NonClientArea providers in a long-running client.
// Only cached scalar data leaves Read; every returned COM reference is released.
internal sealed class TaskbarAutomation : IDisposable {
    IUiAutomation client;IUiCacheRequest cache;IUiCondition condition;
    public TaskbarAutomation(){
        try{
            client=(IUiAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("ff48dba4-60ef-4201-aa87-54103eef594e")));
            client.CreateCacheRequest(out cache);cache.SetTreeScope(1);cache.SetElementMode(0);
            foreach(int property in new[]{30001,30002,30003,30020,30022})cache.AddProperty(property);
            client.CreateTrueCondition(out condition);
        }catch{Dispose();throw;}
    }
    static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    static object Value(IUiElement element,int property){object value;element.GetCachedPropertyValue(property,out value);if(value!=null&&Marshal.IsComObject(value)){Release(value);return null;}return value;}
    public List<TaskbarElement> Read(IntPtr window){
        IUiElement root=null;IUiElementArray elements=null;var result=new List<TaskbarElement>();
        try{
            client.ElementFromHandle(window,out root);root.FindAllBuildCache(4,condition,cache,out elements);
            int count;elements.GetLength(out count);if(count<0||count>16384)throw new InvalidOperationException("Invalid taskbar element count");
            for(int i=0;i<count;i++){
                IUiElement element=null;
                try{
                    elements.GetElement(i,out element);
                    var process=Value(element,30002);var type=Value(element,30003);var offscreen=Value(element,30022);
                    if(!(process is int)||!(type is int)||!(offscreen is bool))continue;
                    var item=new TaskbarElement{ProcessId=(int)process,ControlType=(int)type,Offscreen=(bool)offscreen};
                    if(!item.Interactive||item.Offscreen)continue;
                    var bounds=Value(element,30001) as double[];if(bounds==null||bounds.Length!=4)continue;
                    var handle=Value(element,30020);item.Window=handle is int?(int)handle:0;
                    item.Left=bounds[0];item.Top=bounds[1];item.Width=bounds[2];item.Height=bounds[3];result.Add(item);
                }catch(COMException){}finally{Release(element);}
            }
            return result;
        }finally{Release(elements);Release(root);}
    }
    public void Dispose(){Release(condition);condition=null;Release(cache);cache=null;Release(client);client=null;}
}

// UIA objects are created, used and released on one windowless MTA thread.
internal sealed class LayoutWorker : IDisposable {
    readonly AutoResetEvent wake=new AutoResetEvent(false);readonly Thread thread;
    readonly Action<TaskbarAutomation> action;volatile bool stopped;
    public LayoutWorker(Action<TaskbarAutomation> callback){action=callback;thread=new Thread(Run){IsBackground=true,Name="NetSpeck taskbar layout"};thread.SetApartmentState(ApartmentState.MTA);thread.Start();}
    void Run(){
        try{
            while(!stopped){
                try{using(var automation=new TaskbarAutomation())while(!stopped){action(automation);if(!stopped)wake.WaitOne(1000);}}
                catch(Exception e){Log.Write("Layout worker: "+e.Message);if(!stopped)wake.WaitOne(1000);}
            }
        }finally{wake.Dispose();}
    }
    public void Request(){if(stopped)return;try{wake.Set();}catch(ObjectDisposedException){}}
    public void Dispose(){stopped=true;try{wake.Set();}catch(ObjectDisposedException){}}
    internal bool IsAlive {get{return thread.IsAlive;}}
}

// Vtable order and signatures follow the Windows SDK UIAutomationClient.h.
// Unused slots are declared explicitly; they must never be called.
[ComImport,Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUiAutomation {
    void Unused0();void Unused1();void Unused2();
    void ElementFromHandle(IntPtr window,out IUiElement element);
    void Unused4();void Unused5();void Unused6();void Unused7();void Unused8();void Unused9();void Unused10();void Unused11();void Unused12();void Unused13();void Unused14();void Unused15();void Unused16();
    void CreateCacheRequest(out IUiCacheRequest cache);void CreateTrueCondition(out IUiCondition condition);
}
[ComImport,Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUiElement {
    void Unused0();void Unused1();void Unused2();void Unused3();void Unused4();
    void FindAllBuildCache(int scope,IUiCondition condition,IUiCacheRequest cache,out IUiElementArray elements);
    void Unused6();void Unused7();void Unused8();
    void GetCachedPropertyValue(int property,[MarshalAs(UnmanagedType.Struct)]out object value);
}
[ComImport,Guid("14314595-b4bc-4055-95f2-58f2e42c9855"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUiElementArray {void GetLength(out int length);void GetElement(int index,out IUiElement element);}
[ComImport,Guid("b32a92b5-bc25-4078-9c08-d7ee95c48e03"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUiCacheRequest {
    void AddProperty(int property);void Unused1();void Unused2();void Unused3();void SetTreeScope(int scope);void Unused5();void Unused6();void Unused7();void SetElementMode(int mode);
}
[ComImport,Guid("352ffba8-0973-437c-a61f-f64cafd81df9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUiCondition {}
}
