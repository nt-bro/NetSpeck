using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
namespace TaskbarNet {
class BehaviorTests {
 static int checks;static StreamWriter report;
 static void Check(bool result,string name){if(!result)throw new Exception("FAIL: "+name);checks++;report.WriteLine("PASS: "+name);report.Flush();}
 static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static void Call(object target,string name){target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
 static ToolStripMenuItem Item(ContextMenuStrip menu,string text){foreach(ToolStripItem item in menu.Items)if(item.Text==text||Object.Equals(item.Tag,text))return (ToolStripMenuItem)item;throw new Exception("Missing menu: "+text);}
 static int CheckMenuHints(ToolStripItemCollection items){int count=0;foreach(ToolStripItem item in items){if(item is ToolStripSeparator)continue;if(String.IsNullOrWhiteSpace(item.ToolTipText))throw new Exception("Missing tooltip: "+item.Text);count++;var child=item as ToolStripMenuItem;if(child!=null&&child.HasDropDownItems){if(!child.DropDown.ShowItemToolTips)throw new Exception("Submenu tips disabled");count+=CheckMenuHints(child.DropDownItems);}}return count;}
 static long IconInk(System.Drawing.Icon icon){using(var bitmap=icon.ToBitmap()){long hash=17;unchecked{for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)hash=hash*31+bitmap.GetPixel(x,y).ToArgb();}return hash;}}
 // Exercise the real HWND and the same pointer handlers without taking global
 // mouse capture from the person using the desktop during automated checks.
 static void MouseMessage(MeterWindow meter,int message,int x){if(message==0x201)meter.BeginResize(x,false);else if(message==0x200)meter.MoveResize(x);else if(message==0x202)meter.EndResize();}
 [STAThread] static int Main(string[] args){
  bool hadSettings=File.Exists(Settings.FilePath);byte[] baseline=hadSettings?File.ReadAllBytes(Settings.FilePath):new byte[0];Directory.CreateDirectory(Settings.Folder);
  object autorun;using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))autorun=key==null?null:key.GetValue("TaskbarNet");
  MeterContext context=null;
  try{
   Directory.CreateDirectory(args[0]);report=new StreamWriter(Path.Combine(args[0],"menu-behavior-tests.txt"));
   File.WriteAllText(Settings.FilePath,"");Check(Settings.Load().Language=="auto","first-run settings select Windows language automatically");
   File.WriteAllText(Settings.FilePath,"Language=en\n");Check(Settings.Load().Language=="en","explicit English language loads");
   File.WriteAllText(Settings.FilePath,"Language=unknown\n");Check(Settings.Load().Language=="auto","invalid language falls back to Windows language");
   File.WriteAllText(Settings.FilePath,"Icon=speedometer\n");Check(Settings.Load().IconId=="speedometer","built-in icon selection loads");
   File.WriteAllText(Settings.FilePath,"Icon=../../other.ico\n");Check(Settings.Load().IconId=="flows","invalid custom icon reference falls back to the default");
   File.WriteAllText(Settings.FilePath,"GraphRaw=1\n");Check(Settings.Load().GraphSmoothingSeconds==0,"legacy raw graph migrates to zero smoothing");
   File.WriteAllText(Settings.FilePath,"GraphRaw=0\n");Check(Settings.Load().GraphSmoothingSeconds==0.6,"legacy smoothed graph retains its 0.6-second response");
   File.WriteAllText(Settings.FilePath,"GraphSmoothing=1.7\nGraphRaw=1\n");Check(Settings.Load().GraphSmoothingSeconds==1.7,"explicit graph smoothing overrides legacy flag regardless of order");
   foreach(string invalid in new[]{"NaN","Infinity","-1","5.1"}){File.WriteAllText(Settings.FilePath,"GraphSmoothing="+invalid+"\n");Check(Settings.Load().GraphSmoothingSeconds==0.6,"invalid graph smoothing is rejected: "+invalid);}
   File.WriteAllBytes(Settings.FilePath,baseline);
   IntPtr bar=Native.FindWindow("Shell_TrayWnd",null);Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(bar));
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   context=new MeterContext(new string[0]);bool exited=false;context.ThreadExit+=(s,e)=>exited=true;
   ((Settings)Field(context,"config")).Language="ru";Call(context,"Save");
   Thread.Sleep(500);Application.DoEvents();
   var menu=(ContextMenuStrip)Field(context,"menu");
   // An initially empty ContextMenuStrip starts Opening with Cancel=true.
   var opening=new CancelEventArgs(true);
   typeof(ToolStripDropDown).GetMethod("OnOpening",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(menu,new object[]{opening});
   Check(!opening.Cancel&&menu.Items.Count>0,"first context-menu opening is not cancelled");
   Check(menu.ShowItemToolTips&&CheckMenuHints(menu.Items)>20,"every menu item and nested option has an explanatory tooltip");
   var adapters=Item(menu,"Сетевой адаптер");int n=0;var live=Network.ReadAdapters();
   foreach(ToolStripMenuItem item in adapters.DropDownItems){
    var expected=live.Find(a=>a.ToString()==item.Text);string id=item.Text=="Автоматически"?"":expected==null?null:expected.Id;
    if(id==null)throw new Exception("No matching interface for "+item.Text);
    item.PerformClick();Application.DoEvents();Check(Settings.Load().AdapterId==id,"adapter menu saves correct interface: "+item.Text);n++;
   }
   Check(n>1,"adapter submenu populated with live interfaces");
   adapters.DropDownItems[0].PerformClick();Check(Settings.Load().AdapterId=="","automatic adapter restored");
   var units=Item(menu,"Единицы");units.DropDownItems[1].PerformClick();Check(Settings.Load().Bits,"bits menu saves immediately");
   units.DropDownItems[0].PerformClick();Check(!Settings.Load().Bits,"bytes menu saves immediately");
   var smoothing=Item(menu,"Усреднение");int[] windows={0,2,3,5,10};for(int i=0;i<windows.Length;i++){smoothing.DropDownItems[i].PerformClick();Check(Settings.Load().SmoothingSeconds==windows[i],"smoothing menu saves "+windows[i]+" seconds");}
   smoothing.DropDownItems[2].PerformClick();
   var resize=Item(menu,"Изменять ширину");bool resizeBefore=Settings.Load().ResizeEnabled;resize.PerformClick();Check(Settings.Load().ResizeEnabled!=resizeBefore,"width resize mode toggles from context menu");
   var widthConfig=(Settings)Field(context,"config");widthConfig.PanelWidth=220;
   // The same Save path as the mouse-up commit persists logical pixels.
   Call(context,"Save");Check(Settings.Load().PanelWidth==220,"manual panel width persists");
   Item(menu,"Сбросить ширину").PerformClick();Check(Settings.Load().PanelWidth==0,"automatic width can be restored");
   var graph=Item(menu,"Фоновый график");bool previous=Settings.Load().GraphEnabled;graph.PerformClick();Check(Settings.Load().GraphEnabled!=previous,"background graph toggles independently");graph.PerformClick();
   Item(menu,"Настройки…").PerformClick();Application.DoEvents();var form=(SettingsForm)Field(context,"dialog");
   Check(form!=null&&form.Visible,"settings menu shows dialog even after hidden launch");
   var iconChoices=(ComboBox)Field(form,"iconChoice");var trayIcon=(NotifyIcon)Field(context,"tray");
   foreach(int index in new[]{0,1,0}){iconChoices.SelectedIndex=index;Call(form,"Apply");string id=index==0?"flows":"speedometer";
    Check(Settings.Load().IconId==id,"app icon selection persists: "+id);
    using(var expected=AppIcons.Create(id,32))Check(IconInk(trayIcon.Icon)==IconInk(expected),"selected icon reaches the real tray: "+id);
    using(var expected=AppIcons.Create(id,48))Check(IconInk(form.Icon)==IconInk(expected),"selected icon reaches the settings window: "+id);
   }
   string iconFixture=Path.Combine(Path.GetTempPath(),"TaskbarNet-icon-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(iconFixture);string iconLibrary=Path.Combine(iconFixture,"library");
   string sourceIco=Path.Combine(iconFixture,"own.ico");using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TaskbarNet.Icons.flows.ico"))using(var output=File.Create(sourceIco))stream.CopyTo(output);
   string importedIco=AppIcons.Import(sourceIco,iconLibrary);Check(AppIcons.ValidId(importedIco)&&File.Exists(Path.Combine(iconLibrary,importedIco.Substring(7)+".ico")),"own ICO imports into a private library");
   string sourcePng=Path.Combine(iconFixture,"wide.png");using(var bitmap=new System.Drawing.Bitmap(64,32)){using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.Clear(System.Drawing.Color.Transparent);g.FillRectangle(System.Drawing.Brushes.DeepSkyBlue,8,4,48,24);}bitmap.Save(sourcePng,System.Drawing.Imaging.ImageFormat.Png);}
   string importedPng=AppIcons.Import(sourcePng,iconLibrary);string converted=Path.Combine(iconLibrary,importedPng.Substring(7)+".ico");using(var icon=AppIcons.FromBytes(File.ReadAllBytes(converted),32))using(var bitmap=icon.ToBitmap())Check(bitmap.GetPixel(16,0).A==0&&bitmap.GetPixel(16,16).A>200,"PNG conversion preserves transparency and non-square aspect ratio");
   byte[] packedIcon=File.ReadAllBytes(converted);Check(BitConverter.ToUInt16(packedIcon,4)==9,"PNG conversion includes nine native icon sizes from 16 to 256");
   Check(AppIcons.Import(sourcePng,iconLibrary)==importedPng&&Directory.GetFiles(iconLibrary,"*.ico").Length==2,"reimporting the same icon does not duplicate it");
   bool rejectedIcon=false;try{AppIcons.Import(Path.Combine(iconFixture,"not-an-icon.txt"),iconLibrary);}catch(ArgumentException){rejectedIcon=true;}Check(rejectedIcon,"unsupported custom icon formats are rejected");
   using(var missing=AppIcons.Create("custom:"+new string('a',64),32))using(var fallback=AppIcons.Create("flows",32))Check(IconInk(missing)==IconInk(fallback),"missing custom icon safely uses Flows");
   Check(typeof(SettingsForm).GetField("panelWidth",BindingFlags.Instance|BindingFlags.NonPublic)==null&&typeof(SettingsForm).GetField("autoWidth",BindingFlags.Instance|BindingFlags.NonPublic)==null,"pixel width and automatic-width controls are removed from settings");
   var fields=(System.Collections.Generic.Dictionary<Control,int>)Field(form,"rowControls");bool uniform=true;int expectedFieldWidth=(int)Math.Round(SettingsForm.FieldWidth*form.ClientSize.Width/452.0);foreach(var pair in fields)uniform&=pair.Key.Width==expectedFieldWidth;
   string widthDetails="";foreach(var pair in fields)widthDetails+=pair.Key.AccessibleName+"="+pair.Key.Width+"; ";
   Check(uniform,"all entry and selection fields share the same compact width; dpi="+Native.GetDpiForWindow(form.Handle)+"; "+widthDetails);
   var tooltips=(ToolTip)Field(form,"tips");var helpControls=(System.Collections.Generic.Dictionary<Control,string>)Field(form,"controlHints");bool allHints=true;foreach(var pair in helpControls)allHints&=!String.IsNullOrWhiteSpace(tooltips.GetToolTip(pair.Key))&&!String.IsNullOrWhiteSpace(pair.Key.AccessibleDescription);
   Check(allHints&&helpControls.Count>40,"all setting controls and labels expose helpful tooltips and accessibility descriptions");
   widthConfig.PanelWidth=213;Call(context,"Save");Call(form,"Apply");Check(Settings.Load().PanelWidth==213,"applying other settings preserves width chosen by dragging");Item(menu,"Сбросить ширину").PerformClick();
   var adapterControl=(ComboBox)Field(form,"adapters");Check(adapterControl.Width>=160*Native.GetDpiForWindow(form.Handle)/96.0,"first displayed settings fields have usable width");
   units.DropDownItems[1].PerformClick();Check(((ComboBox)Field(form,"units")).SelectedIndex==1,"quick menu synchronizes open settings");
   units.DropDownItems[0].PerformClick();
   var fontTabs=(TabControl)Field(form,"tabs");fontTabs.SelectedIndex=2;
   var families=(ComboBox)Field(form,"fontFamily");int familyIndex=-1;for(int i=0;i<families.Items.Count;i++)if(((FontChoice)families.Items[i]).Name=="Consolas")familyIndex=i;
   if(familyIndex<0)for(int i=0;i<families.Items.Count;i++)if(((FontChoice)families.Items[i]).Name=="Segoe UI")familyIndex=i;
   Check(familyIndex>=0,"installed Windows fonts are listed");families.SelectedIndex=familyIndex;
   ((ComboBox)Field(form,"fontStyle")).SelectedIndex=1;((CheckBox)Field(form,"fontForUi")).Checked=true;Call(form,"Apply");
   var selectedFont=Settings.Load();Check(selectedFont.FontName==((FontChoice)families.SelectedItem).Name&&selectedFont.FontStyle==1&&selectedFont.FontForUi,"font family, style and interface scope round trip");
   ((Button)Field(form,"resetFont")).PerformClick();Call(form,"Apply");Check(Settings.Load().FontName=="Segoe UI"&&Settings.Load().FontStyle==0&&!Settings.Load().FontForUi,"old Segoe UI restored by reset button");
   widthConfig.ResizeEnabled=true;Call(context,"Save");Thread.Sleep(150);Application.DoEvents();Call(context,"Tick");
   var placement=(Placement)Field(context,"placement");var meter=(MeterWindow)Field(context,"meter");
   if(placement==null||meter==null||!placement.Safe)throw new Exception("No safe taskbar placement for resize test");
   Native.Rect beforeRect;Native.GetWindowRect(meter.Handle,out beforeRect);int target=Math.Min(placement.X+placement.Width-placement.MinimumX,beforeRect.Width+90);
   try{MouseMessage(meter,0x201,2);Check(meter.IsResizing,"left-edge mouse down enters resizing mode");MouseMessage(meter,0x200,beforeRect.Width-target);MouseMessage(meter,0x202,0);}
   finally{if(meter.IsResizing)Native.ReleaseCapture();}
   Native.Rect afterRect;Native.GetWindowRect(meter.Handle,out afterRect);
   Check(!meter.IsResizing&&afterRect.Right==beforeRect.Right&&Math.Abs(afterRect.Width-target)<=2,"mouse drag resizes only left edge and preserves right anchor; before="+beforeRect+" after="+afterRect+" target="+target+" layout="+placement.X+","+placement.Width);
   Check(Settings.Load().PanelWidth==ResizeMath.ToDip(target,placement.Dpi),"mouse-up persists resized logical width");
   Item(menu,"Сбросить ширину").PerformClick();
   string fixture=Path.Combine(Path.GetTempPath(),"TaskbarNet-font-test-"+Guid.NewGuid().ToString("N"));
   var imported=AppFonts.Import(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"segoeui.ttf"),fixture);
   Check(imported.Length>0&&Directory.GetFiles(fixture).Length==1,"own TTF file imported privately without installation");
   using(var loadedFont=AppFonts.Create(imported[0],12,System.Drawing.FontStyle.Regular,System.Drawing.GraphicsUnit.Pixel))Check(loadedFont.Name==imported[0],"imported font can render");
   var tabs=(TabControl)Field(form,"tabs");tabs.SelectedIndex=1;
   var enabled=(CheckBox)Field(form,"graphEnabled");enabled.Checked=true;
   int numericSmoothing=Settings.Load().SmoothingSeconds;
   foreach(decimal value in new[]{0m,0.1m,0.6m,1m,2.5m,5m}){
    ((NumericUpDown)Field(form,"graphSmoothing")).Value=value;Call(form,"Apply");
    var changed=Settings.Load();Check(changed.GraphSmoothingSeconds==(double)value&&changed.GraphRaw==(value==0)&&changed.SmoothingSeconds==numericSmoothing,"graph smoothing applies independently and persists: "+value);
   }
   foreach(int fps in new[]{5,10,20,30}){
    ((ComboBox)Field(form,"graphFps")).SelectedItem=fps;Call(form,"Apply");
    Check(Settings.Load().GraphFps==fps&&((System.Windows.Forms.Timer)Field(context,"uiTimer")).Interval==GraphMath.FrameInterval(true,fps),"graph frame rate persists and applies independently: "+fps);
   }
   ((ComboBox)Field(form,"graphFps")).SelectedItem=20;((ComboBox)Field(form,"interval")).SelectedIndex=2;Call(form,"Apply");
   Thread.Sleep(650);Application.DoEvents();Call(context,"Tick");
   var unchangedReading=Field(context,"reading");var renderedBefore=(DateTime)Field(context,"lastRender");
   Thread.Sleep(80);Application.DoEvents();
   Check(Object.ReferenceEquals(unchangedReading,Field(context,"reading"))&&(DateTime)Field(context,"lastRender")>renderedBefore,"graph repaints between numeric updates using the real UI timer");
   ((ComboBox)Field(form,"graphSeconds")).SelectedItem=60;
   ((NumericUpDown)Field(form,"graphOpacity")).Value=37;
   ((NumericUpDown)Field(form,"graphLineWidth")).Value=1.5m;
   ((ComboBox)Field(form,"graphScale")).SelectedIndex=1;
   ((NumericUpDown)Field(form,"graphMaximum")).Value=12.5m;
   ((NumericUpDown)Field(form,"graphSmoothing")).Value=0;((CheckBox)Field(form,"graphFill")).Checked=false;
   typeof(SettingsForm).GetField("chosenDown",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,0x123456);
   typeof(SettingsForm).GetField("chosenUp",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,0xABCDEF);
   Call(form,"Apply");var stored=Settings.Load();
   Check(stored.GraphEnabled&&stored.GraphSeconds==60&&stored.GraphOpacity==37,"graph options persist through Apply");
   Check(stored.GraphLineWidth==1.5&&!stored.GraphAutoScale&&stored.GraphMaximum==12500000,"fixed graph scale and thickness round trip");
   Check(stored.GraphRaw&&!stored.GraphFill&&stored.DownloadColor==0x123456&&stored.UploadColor==0xABCDEF,"graph source, fill and colors round trip");
   graph.PerformClick();Check(!enabled.Checked&&!((NumericUpDown)Field(form,"graphOpacity")).Enabled,"graph menu synchronizes enabled state in settings");
   Check(!((NumericUpDown)Field(form,"graphSmoothing")).Enabled,"graph smoothing control disables with the graph");
   Check(((System.Windows.Forms.Timer)Field(context,"uiTimer")).Interval==250,"disabling graph stops high-rate repaint timer");
   Call(context,"BuildMenu");Item(menu,"Язык").DropDownItems[2].PerformClick();
   Check(Settings.Load().Language=="en"&&form.Text=="TaskbarNet — Settings"&&((TabControl)Field(form,"tabs")).TabPages[1].Text=="Graph","English menu choice persists and translates open settings without restart");
   Check(tooltips.GetToolTip((Control)Field(form,"graphSmoothing")).Contains("Graph-only"),"setting tooltips switch to English");
   var labels=(System.Collections.Generic.Dictionary<Control,string>)Field(form,"localized");bool translated=true;foreach(var pair in labels)if(System.Text.RegularExpressions.Regex.IsMatch(pair.Key.Text,"[А-Яа-яЁё]"))translated=false;
   Check(translated,"English window has no untranslated Russian captions");
   Call(context,"BuildMenu");Check(Item(menu,"Настройки…").Text=="Settings…"&&CheckMenuHints(menu.Items)>20,"context menu and nested hints switch to English");
   ((ComboBox)Field(form,"language")).SelectedIndex=1;Call(form,"Apply");Check(Settings.Load().Language=="ru"&&form.Text=="TaskbarNet — настройки","language can switch back through settings without restart");
   ((ComboBox)Field(form,"language")).SelectedIndex=2;Call(form,"Apply");Check(Settings.Load().Language=="en"&&form.Text=="TaskbarNet — Settings","English can also be selected through settings");
   Call(context,"BuildMenu");Item(menu,"Язык").DropDownItems[0].PerformClick();Check(Settings.Load().Language=="auto"&&Ui.Language==Ui.Resolve("auto",System.Globalization.CultureInfo.CurrentUICulture),"automatic language mode follows Windows again");
   Call(context,"BuildMenu");Item(menu,"Язык").DropDownItems[1].PerformClick();Call(context,"BuildMenu");
   context.OpenMeterMenu();Application.DoEvents();Application.DoEvents();
   Check(menu.Visible,"native indicator WM_CONTEXTMENU opens a visible popup");menu.Close();
   Button close=null;foreach(Control c in form.Controls)if(c is Button&&c.Text=="Закрыть")close=(Button)c;
   close.PerformClick();Application.DoEvents();Check(form.IsDisposed&&!exited,"close settings keeps meter running");
   Call(context,"BuildMenu");Item(menu,"Выход").PerformClick();Application.DoEvents();Check(exited,"exit menu stops application context");
   report.WriteLine("All "+checks+" behavior checks passed.");return 0;
  }catch(Exception e){if(report!=null){report.WriteLine(e);report.Flush();}Console.Error.WriteLine(e);return 1;}
  finally{
   if(context!=null)context.Dispose();if(hadSettings)File.WriteAllBytes(Settings.FilePath,baseline);else if(File.Exists(Settings.FilePath))File.Delete(Settings.FilePath);
   using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(autorun==null)key.DeleteValue("TaskbarNet",false);else key.SetValue("TaskbarNet",autorun);}
   if(report!=null)report.Dispose();
  }
 }
}
}
