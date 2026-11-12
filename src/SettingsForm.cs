using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NetSpeck {
internal sealed class SettingsForm : Form {
    readonly Settings config;
    readonly ComboBox adapters=new ComboBox(),units=new ComboBox(),interval=new ComboBox(),smoothing=new ComboBox(),fontFamily=new ComboBox(),fontStyle=new ComboBox();
    internal const int FieldWidth=176;
    readonly ComboBox graphSeconds=new ComboBox(),graphScale=new ComboBox(),graphFps=new ComboBox(),language=new ComboBox(),iconChoice=new ComboBox();
    readonly CheckBox startup=new CheckBox(),graphEnabled=new CheckBox(),graphFill=new CheckBox(),fontForUi=new CheckBox();
    readonly NumericUpDown graphOpacity=new NumericUpDown(),graphLineWidth=new NumericUpDown(),graphMaximum=new NumericUpDown(),graphSmoothing=new NumericUpDown(),font=new NumericUpDown();
    readonly ToolTip tips=new ToolTip{ShowAlways=true,InitialDelay=500,ReshowDelay=100,AutoPopDelay=15000};
    readonly Dictionary<Control,string> localized=new Dictionary<Control,string>(),controlHints=new Dictionary<Control,string>();
    readonly Dictionary<Control,Label> captions=new Dictionary<Control,Label>();string appliedLanguage;
    readonly Label graphSmoothingHint=new Label();
    readonly Button downColor=new Button(),upColor=new Button();
    readonly Label status=new Label();readonly TabControl tabs=new TabControl();
    readonly Label header=new Label(),hint=new Label();readonly Button applyButton=new Button(),closeButton=new Button();
    readonly Button importFonts=new Button(),resetFont=new Button();readonly Label fontPreview=new Label(),fontHint=new Label();Font previewFont;
    readonly Button importIcons=new Button();readonly PictureBox iconPreview=new PictureBox{BorderStyle=BorderStyle.FixedSingle,SizeMode=PictureBoxSizeMode.CenterImage};readonly Label iconHint=new Label();Icon windowIcon;
    readonly Dictionary<Control,int> rowControls=new Dictionary<Control,int>(),rowLabels=new Dictionary<Control,int>();bool arranging;
    readonly TabPage mainPage=new TabPage("Основное"),graphPage=new TabPage("График"),fontsPage=new TabPage("Шрифты"),iconsPage=new TabPage("Иконки");bool syncing;
    int chosenDown,chosenUp;
    public event Action ApplyRequested;
    public SettingsForm(Settings s,List<Adapter> list){
        SuspendLayout();config=s;Ui.SetLanguage(s.Language);Text="NetSpeck — настройки";FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(452,560);Font=AppFonts.Ui(s);BackColor=Color.White;
        header.Text="Скорость сети в панели задач";header.Font=AppFonts.Heading(s);header.AutoSize=true;header.Location=new Point(18,16);Controls.Add(header);
        tabs.SetBounds(18,54,504,414);tabs.TabPages.Add(mainPage);tabs.TabPages.Add(graphPage);tabs.TabPages.Add(fontsPage);tabs.TabPages.Add(iconsPage);Controls.Add(tabs);
        mainPage.BackColor=Color.White;graphPage.BackColor=Color.White;fontsPage.BackColor=Color.White;iconsPage.BackColor=Color.White;
        AddRow(mainPage,"Сетевой адаптер",adapters,20);adapters.Items.Add(new Adapter{Id="",Name="Автоматически",Up=true});foreach(var a in list)adapters.Items.Add(a);
        AddRow(mainPage,"Единицы",units,62);units.Items.AddRange(new object[]{"Байты/с (kB/s, MB/s)","Биты/с (kbit/s, Mbit/s)"});
        AddRow(mainPage,"Размер шрифта",font,104);font.Minimum=6;font.Maximum=24;
        AddRow(mainPage,"Обновление",interval,146);interval.Items.AddRange(new object[]{"0,5 секунды","1 секунда","2 секунды"});
        AddRow(mainPage,"Усреднение скорости",smoothing,188);smoothing.Items.AddRange(new object[]{"Выключено","2 секунды","3 секунды","5 секунд","10 секунд"});
        hint.Text="Сглаживает короткие скачки; реакция становится плавнее.";hint.Location=new Point(14,226);hint.AutoSize=true;hint.ForeColor=Color.DimGray;mainPage.Controls.Add(hint);
        AddRow(mainPage,"Язык интерфейса",language,268);
        startup.Text="Запускать при входе в Windows";startup.AutoSize=true;mainPage.Controls.Add(startup);
        graphEnabled.Text="Показывать график на фоне индикатора";graphEnabled.AutoSize=true;graphEnabled.Location=new Point(14,14);graphPage.Controls.Add(graphEnabled);
        AddRow(graphPage,"Частота графика, кадр/с",graphFps,48);graphFps.Items.AddRange(new object[]{5,10,20,30});
        AddRow(graphPage,"Сглаживание графика, с",graphSmoothing,80);graphSmoothing.Minimum=0;graphSmoothing.Maximum=5;graphSmoothing.DecimalPlaces=1;graphSmoothing.Increment=0.1m;
        graphSmoothingHint.Text="0 — выкл.; больше — плавнее.";graphSmoothingHint.AutoSize=true;graphSmoothingHint.ForeColor=Color.DimGray;graphPage.Controls.Add(graphSmoothingHint);
        AddRow(graphPage,"История графика",graphSeconds,134);graphSeconds.Items.AddRange(new object[]{15,30,60,120});
        AddRow(graphPage,"Непрозрачность, %",graphOpacity,166);graphOpacity.Minimum=5;graphOpacity.Maximum=60;
        AddRow(graphPage,"Толщина линий, px",graphLineWidth,198);graphLineWidth.Minimum=0.5m;graphLineWidth.Maximum=3;graphLineWidth.DecimalPlaces=1;graphLineWidth.Increment=0.5m;
        AddRow(graphPage,"Общая шкала",graphScale,230);graphScale.Items.AddRange(new object[]{"Автоматическая","Фиксированная"});
        AddRow(graphPage,"Предел шкалы, MB/s",graphMaximum,262);graphMaximum.Minimum=0.001m;graphMaximum.Maximum=1000000;graphMaximum.DecimalPlaces=3;graphMaximum.Increment=1;
        AddRow(graphPage,"Цвет загрузки ↓",downColor,294);AddRow(graphPage,"Цвет отдачи ↑",upColor,326);
        downColor.Click+=(o,e)=>PickColor(true);upColor.Click+=(o,e)=>PickColor(false);
        graphFill.Text="Лёгкая заливка под линиями";graphFill.AutoSize=true;graphFill.Location=new Point(14,322);graphPage.Controls.Add(graphFill);
        graphEnabled.CheckedChanged+=(o,e)=>EnableGraphControls();graphScale.SelectedIndexChanged+=(o,e)=>EnableGraphControls();
        AddRow(fontsPage,"Шрифт индикатора",fontFamily,20);RefreshFonts(s.FontName);
        AddRow(fontsPage,"Начертание",fontStyle,62);fontStyle.Items.AddRange(new object[]{"Обычный","Полужирный","Курсив","Полужирный курсив"});
        fontForUi.Text="Применять этот шрифт также к меню и настройкам";fontForUi.AutoSize=true;fontsPage.Controls.Add(fontForUi);
        importFonts.Text="Добавить шрифты…";importFonts.Click+=(o,e)=>ImportFonts();fontsPage.Controls.Add(importFonts);
        resetFont.Text="Вернуть Segoe UI";resetFont.Click+=(o,e)=>{SelectFont("Segoe UI");fontStyle.SelectedIndex=0;fontForUi.Checked=false;UpdateFontPreview();};fontsPage.Controls.Add(resetFont);
        fontPreview.Text="↓ 12.3 MB/s\n↑ 456 kB/s";fontPreview.BorderStyle=BorderStyle.FixedSingle;fontsPage.Controls.Add(fontPreview);
        fontHint.Text="Свои файлы: TTF, OTF или TTC.\nРазмер — на вкладке «Основное».\nШрифты добавляются только в приложение.";fontHint.AutoSize=true;fontHint.ForeColor=Color.DimGray;fontsPage.Controls.Add(fontHint);
        fontFamily.SelectedIndexChanged+=(o,e)=>UpdateFontPreview();fontStyle.SelectedIndexChanged+=(o,e)=>UpdateFontPreview();font.ValueChanged+=(o,e)=>UpdateFontPreview();
        AddRow(iconsPage,"Иконка приложения",iconChoice,20);RefreshIcons(s.IconId);
        importIcons.Text="Добавить иконки…";importIcons.Click+=(o,e)=>ImportIcons();iconsPage.Controls.Add(importIcons);
        iconsPage.Controls.Add(iconPreview);iconChoice.SelectedIndexChanged+=(o,e)=>UpdateIconPreview();iconPreview.SizeChanged+=(o,e)=>UpdateIconPreview();
        iconHint.Text="Иконка в трее и окнах приложения.\nСвои файлы: ICO или PNG, до 10 МБ.\nДля сохранения нажмите «Применить».";iconHint.AutoSize=true;iconHint.ForeColor=Color.DimGray;iconsPage.Controls.Add(iconHint);
        status.SetBounds(18,480,504,34);status.ForeColor=Color.DimGray;Controls.Add(status);
        applyButton.Text="Применить";applyButton.SetBounds(314,522,100,28);applyButton.Click+=(o,e)=>Apply();Controls.Add(applyButton);
        closeButton.Text="Закрыть";closeButton.SetBounds(424,522,98,28);closeButton.Click+=(o,e)=>Close();Controls.Add(closeButton);AcceptButton=applyButton;CancelButton=closeButton;
        tabs.SelectedIndexChanged+=(o,e)=>PerformLayout();
        CaptureText(this);BindHints();SyncFromConfig();ResumeLayout(true);
    }
    void CaptureText(Control root){if((root is Form||root is Label||root is Button||root is CheckBox||root is TabPage)&&root.Text.Length>0)localized[root]=root.Text;foreach(Control c in root.Controls)CaptureText(c);}
    void Options(ComboBox combo,params string[] values){int selected=combo.SelectedIndex;combo.Items.Clear();foreach(string value in values)combo.Items.Add(Ui.T(value));if(selected>=0&&selected<combo.Items.Count)combo.SelectedIndex=selected;}
    void ApplyLanguage(){
        Ui.SetLanguage(config.Language);if(appliedLanguage==Ui.Language)return;appliedLanguage=Ui.Language;
        foreach(var text in localized)text.Key.Text=Ui.T(text.Value);foreach(var pair in captions)pair.Key.AccessibleName=pair.Value.Text;
        Options(units,"Байты/с (kB/s, MB/s)","Биты/с (kbit/s, Mbit/s)");Options(interval,"0,5 секунды","1 секунда","2 секунды");
        Options(smoothing,"Выключено","2 секунды","3 секунды","5 секунд","10 секунд");Options(graphScale,"Автоматическая","Фиксированная");
        Options(fontStyle,"Обычный","Полужирный","Курсив","Полужирный курсив");Options(language,"Автоматически (Windows)","Русский","English");
        int adapterIndex=adapters.SelectedIndex;((Adapter)adapters.Items[0]).Name=Ui.T("Автоматически");for(int i=0;i<adapters.Items.Count;i++)adapters.Items[i]=adapters.Items[i];if(adapterIndex>=0)adapters.SelectedIndex=adapterIndex;
        RefreshFonts(config.FontName);RefreshIcons(config.IconId);tabs.ShowToolTips=true;mainPage.ToolTipText=Ui.Help("tabMain");graphPage.ToolTipText=Ui.Help("tabGraph");fontsPage.ToolTipText=Ui.Help("tabFonts");iconsPage.ToolTipText=Ui.Help("tabIcons");
    }
    void BindHints(){
        Bind(adapters,"adapter");Bind(units,"units");Bind(font,"fontSize");Bind(interval,"interval");Bind(smoothing,"smoothing");Bind(hint,"smoothing");Bind(language,"language");Bind(startup,"startup");
        Bind(graphEnabled,"graphEnabled");Bind(graphFps,"graphFps");Bind(graphSmoothing,"graphSmoothing");Bind(graphSmoothingHint,"graphSmoothing");Bind(graphSeconds,"graphSeconds");Bind(graphOpacity,"graphOpacity");Bind(graphLineWidth,"graphLineWidth");Bind(graphScale,"graphScale");Bind(graphMaximum,"graphMaximum");Bind(downColor,"downColor");Bind(upColor,"upColor");Bind(graphFill,"graphFill");
        Bind(fontFamily,"fontFamily");Bind(fontStyle,"fontStyle");Bind(fontForUi,"fontForUi");Bind(importFonts,"fontImport");Bind(resetFont,"fontReset");Bind(fontPreview,"fontPreview");Bind(fontHint,"fontImport");Bind(applyButton,"apply");Bind(closeButton,"close");Bind(status,"status");Bind(header,"version");
        Bind(iconChoice,"iconChoice");Bind(importIcons,"iconImport");Bind(iconPreview,"iconPreview");Bind(iconHint,"iconChoice");
    }
    void Bind(Control control,string id){
        controlHints[control]=id;Label caption;if(captions.TryGetValue(control,out caption))controlHints[caption]=id;
        var combo=control as ComboBox;if(combo!=null)combo.SelectedIndexChanged+=(s,e)=>RefreshHints();
        var numeric=control as NumericUpDown;if(numeric!=null)numeric.ValueChanged+=(s,e)=>RefreshHints();
    }
    void SetHint(Control control,string text){tips.SetToolTip(control,text);control.AccessibleDescription=text;foreach(Control child in control.Controls)SetHint(child,text);}
    void RefreshHints(){if(syncing)return;foreach(var pair in controlHints){string text=Ui.Help(pair.Value);if(pair.Key is ComboBox||pair.Key is NumericUpDown)text+="\n"+Ui.Wrap(Ui.T("Текущее значение: ")+pair.Key.Text);SetHint(pair.Key,text);}}
    void RefreshFonts(string selected){fontFamily.Items.Clear();foreach(var choice in AppFonts.Choices())fontFamily.Items.Add(choice);SelectFont(selected);}
    void RefreshIcons(string selected){iconChoice.Items.Clear();foreach(var choice in AppIcons.Choices(selected))iconChoice.Items.Add(choice);iconChoice.SelectedIndex=0;for(int i=0;i<iconChoice.Items.Count;i++)if(((IconChoice)iconChoice.Items[i]).Id==selected){iconChoice.SelectedIndex=i;break;}}
    void UpdateIconPreview(){if(syncing||iconChoice.SelectedItem==null||iconPreview.Width<1||iconPreview.Height<1)return;float scale=(float)(CurrentAutoScaleDimensions.Width/96.0);var next=AppIcons.Preview(((IconChoice)iconChoice.SelectedItem).Id,iconPreview.Width,iconPreview.Height,scale);var previous=iconPreview.Image;iconPreview.Image=next;if(previous!=null)previous.Dispose();}
    void ImportIcons(){
        using(var dialog=new OpenFileDialog{Title=Ui.T("Добавить свои иконки"),Filter=Ui.T("Иконки (*.ico;*.png)|*.ico;*.png"),Multiselect=true}){
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;string selected=((IconChoice)iconChoice.SelectedItem).Id;var errors=new List<string>();
            foreach(string file in dialog.FileNames)try{selected=AppIcons.Import(file);}catch(Exception e){errors.Add(System.IO.Path.GetFileName(file)+": "+e.Message);}
            RefreshIcons(selected);UpdateIconPreview();RefreshHints();if(errors.Count>0)MessageBox.Show(this,String.Join("\n",errors.ToArray()),Ui.T("Добавление иконок"));
        }
    }
    void SelectFont(string name){for(int i=0;i<fontFamily.Items.Count;i++)if(String.Equals(((FontChoice)fontFamily.Items[i]).Name,name,StringComparison.OrdinalIgnoreCase)){fontFamily.SelectedIndex=i;return;}fontFamily.Items.Add(new FontChoice{Name=name});fontFamily.SelectedIndex=fontFamily.Items.Count-1;}
    void ImportFonts(){
        using(var dialog=new OpenFileDialog{Title=Ui.T("Добавить свои шрифты"),Filter=Ui.T("Шрифты (*.ttf;*.otf;*.ttc)|*.ttf;*.otf;*.ttc"),Multiselect=true}){
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;string selected=null;var errors=new List<string>();
            foreach(string file in dialog.FileNames){try{var names=AppFonts.Import(file);if(names.Length>0)selected=names[0];}catch(Exception e){errors.Add(System.IO.Path.GetFileName(file)+": "+e.Message);}}
            RefreshFonts(selected??((FontChoice)fontFamily.SelectedItem).Name);UpdateFontPreview();
            if(errors.Count>0)MessageBox.Show(this,String.Join("\n",errors.ToArray()),Ui.T("Добавление шрифтов"));
        }
    }
    void UpdateFontPreview(){
        if(syncing||fontFamily.SelectedItem==null||fontStyle.SelectedIndex<0)return;
        var choice=(FontChoice)fontFamily.SelectedItem;var next=AppFonts.Create(choice.Name,(float)font.Value,(FontStyle)fontStyle.SelectedIndex,GraphicsUnit.Point);
        var previous=previewFont;previewFont=next;fontPreview.Font=next;if(previous!=null)previous.Dispose();
    }
    void AddRow(Control parent,string name,Control control,int top){
        var label=new Label{Text=name,Location=new Point(14,top+4),AutoSize=true};parent.Controls.Add(label);rowLabels[label]=top+4;
        captions[control]=label;control.AccessibleName=name;control.SetBounds(218,top,FieldWidth,27);parent.Controls.Add(control);
        rowControls[control]=top;
        var combo=control as ComboBox;if(combo!=null){combo.DropDownStyle=ComboBoxStyle.DropDownList;combo.DropDownWidth=430;}
    }
    void PickColor(bool download){
        using(var dialog=new ColorDialog{Color=MeterRenderer.ColorOf(download?chosenDown:chosenUp,255),FullOpen=true}){
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;
            int rgb=dialog.Color.ToArgb()&0xFFFFFF;if(download)chosenDown=rgb;else chosenUp=rgb;PaintColors();
        }
    }
    void PaintColors(){SetColor(downColor,chosenDown);SetColor(upColor,chosenUp);}
    static void SetColor(Button button,int rgb){button.Text="#"+rgb.ToString("X6");button.BackColor=MeterRenderer.ColorOf(rgb,255);button.ForeColor=button.BackColor.GetBrightness()<0.5?Color.White:Color.Black;button.UseVisualStyleBackColor=false;}
    void EnableGraphControls(){foreach(Control c in graphPage.Controls)if(c!=graphEnabled){if(c is Label)c.ForeColor=graphEnabled.Checked?Color.Black:Color.DimGray;else c.Enabled=graphEnabled.Checked;}graphSmoothingHint.ForeColor=Color.DimGray;graphMaximum.Enabled=graphEnabled.Checked&&graphScale.SelectedIndex==1;}
    void Apply(){
        try{
            config.AdapterId=((Adapter)adapters.SelectedItem).Id;config.Bits=units.SelectedIndex==1;config.FontSize=(int)font.Value;
            config.FontName=((FontChoice)fontFamily.SelectedItem).Name;config.FontStyle=fontStyle.SelectedIndex;config.FontForUi=fontForUi.Checked;config.Language=new[]{"auto","ru","en"}[language.SelectedIndex];
            config.IconId=((IconChoice)iconChoice.SelectedItem).Id;
            config.Interval=interval.SelectedIndex==0?500:interval.SelectedIndex==2?2000:1000;
            config.SmoothingSeconds=new[]{0,2,3,5,10}[smoothing.SelectedIndex];
            config.GraphEnabled=graphEnabled.Checked;config.GraphSeconds=(int)graphSeconds.SelectedItem;config.GraphOpacity=(int)graphOpacity.Value;config.GraphFps=(int)graphFps.SelectedItem;
            config.GraphLineWidth=(double)graphLineWidth.Value;config.GraphAutoScale=graphScale.SelectedIndex==0;config.GraphMaximum=(double)graphMaximum.Value*1000000;
            config.GraphFill=graphFill.Checked;config.GraphSmoothingSeconds=(double)graphSmoothing.Value;config.DownloadColor=chosenDown;config.UploadColor=chosenUp;
            if(startup.Checked!=Settings.AutoStart)Settings.AutoStart=startup.Checked;
            var h=ApplyRequested;if(h!=null)h();
        }catch(Exception e){MessageBox.Show(this,Ui.T("Не удалось применить настройки: ")+e.Message,"NetSpeck");}
    }
    public void SyncFromConfig(){
        syncing=true;
        ApplyLanguage();
        Font=AppFonts.Ui(config);header.Font=AppFonts.Heading(config);
        var nextIcon=AppIcons.Create(config.IconId,48);var previousIcon=windowIcon;windowIcon=nextIcon;if(IsHandleCreated)Icon=nextIcon;if(previousIcon!=null)previousIcon.Dispose();
        adapters.SelectedIndex=0;for(int i=0;i<adapters.Items.Count;i++)if(((Adapter)adapters.Items[i]).Id==config.AdapterId){adapters.SelectedIndex=i;break;}
        units.SelectedIndex=config.Bits?1:0;font.Value=config.FontSize;interval.SelectedIndex=config.Interval==500?0:config.Interval==2000?2:1;
        SelectFont(config.FontName);fontStyle.SelectedIndex=config.FontStyle;fontForUi.Checked=config.FontForUi;
        language.SelectedIndex=config.Language=="ru"?1:config.Language=="en"?2:0;
        for(int i=0;i<iconChoice.Items.Count;i++)if(((IconChoice)iconChoice.Items[i]).Id==config.IconId){iconChoice.SelectedIndex=i;break;}
        smoothing.SelectedIndex=Array.IndexOf(new[]{0,2,3,5,10},config.SmoothingSeconds);startup.Checked=Settings.AutoStart;
        graphEnabled.Checked=config.GraphEnabled;graphSeconds.SelectedItem=config.GraphSeconds;graphOpacity.Value=config.GraphOpacity;graphFps.SelectedItem=config.GraphFps;
        graphLineWidth.Value=(decimal)config.GraphLineWidth;graphScale.SelectedIndex=config.GraphAutoScale?0:1;graphMaximum.Value=(decimal)(config.GraphMaximum/1000000);
        graphFill.Checked=config.GraphFill;graphSmoothing.Value=(decimal)config.GraphSmoothingSeconds;chosenDown=config.DownloadColor;chosenUp=config.UploadColor;PaintColors();EnableGraphControls();
        syncing=false;UpdateFontPreview();UpdateIconPreview();RefreshHints();
    }
    protected override void OnLayout(LayoutEventArgs e){
        base.OnLayout(e);if(arranging||tabs==null||tabs.Parent!=this)return;arranging=true;
        try{
            double scale=CurrentAutoScaleDimensions.Width/96.0;
            Func<int,int> px=n=>(int)Math.Round(n*scale);
            header.Location=new Point(px(18),px(16));
            header.MaximumSize=new Size(ClientSize.Width-px(36),0);
            tabs.SetBounds(px(18),px(54),ClientSize.Width-px(36),ClientSize.Height-px(146));
            status.SetBounds(px(18),ClientSize.Height-px(80),ClientSize.Width-px(36),px(34));
            applyButton.SetBounds(ClientSize.Width-px(226),ClientSize.Height-px(38),px(100),px(28));
            closeButton.SetBounds(ClientSize.Width-px(116),ClientSize.Height-px(38),px(98),px(28));
            foreach(var row in rowLabels)row.Key.Location=new Point(px(14),px(row.Value));
            // TabPage.ClientSize can still be its initial 200px during first
            // display. Derive widths from the already arranged tab control.
            int pageWidth=Math.Max(px(50),tabs.ClientSize.Width-8);
            foreach(var row in rowControls){var c=row.Key;c.SetBounds(px(218),px(row.Value),px(FieldWidth),Math.Max(c.PreferredSize.Height,px(27)));}
            hint.Location=new Point(px(14),px(226));hint.MaximumSize=new Size(Math.Max(1,pageWidth-px(28)),0);
            startup.Location=new Point(px(14),px(316));graphEnabled.Location=new Point(px(14),px(14));graphFill.Location=new Point(px(14),px(364));graphSmoothingHint.Location=new Point(px(218),px(112));graphSmoothingHint.MaximumSize=new Size(px(FieldWidth),0);
            fontForUi.Location=new Point(px(14),px(108));importFonts.SetBounds(px(14),px(150),px(FieldWidth),px(30));resetFont.SetBounds(px(218),px(150),px(FieldWidth),px(30));
            fontPreview.SetBounds(px(14),px(198),Math.Max(1,pageWidth-px(28)),px(92));fontHint.Location=new Point(px(14),px(310));fontHint.MaximumSize=new Size(Math.Max(1,pageWidth-px(28)),0);
            importIcons.SetBounds(px(218),px(64),px(FieldWidth),px(30));iconPreview.SetBounds(px(14),px(124),Math.Max(1,pageWidth-px(28)),px(126));iconHint.Location=new Point(px(14),px(280));iconHint.MaximumSize=new Size(Math.Max(1,pageWidth-px(28)),0);
        }finally{arranging=false;}
    }
    public void UpdateStatus(Reading r,Placement p){status.Text=(r.Connected?r.AdapterName:Ui.T(r.AdapterName))+"\n"+(p==null?Ui.T("Подготовка…"):Ui.T(p.Status));}
    protected override void OnShown(EventArgs e){base.OnShown(e);if(windowIcon!=null)Icon=windowIcon;PerformLayout();UpdateIconPreview();}
    protected override void Dispose(bool disposing){if(disposing){tips.Dispose();if(previewFont!=null){previewFont.Dispose();previewFont=null;}var image=iconPreview.Image;iconPreview.Image=null;if(image!=null)image.Dispose();if(windowIcon!=null){Icon=null;windowIcon.Dispose();windowIcon=null;}}base.Dispose(disposing);}
}
}
