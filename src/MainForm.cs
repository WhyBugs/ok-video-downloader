using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OkVideo {
 public sealed class MainForm : Form {
  readonly bool testing;
  readonly TextBox links=new TextBox(),folder=new TextBox(),format=new TextBox(),items=new TextBox(),languages=new TextBox(),template=new TextBox();
  readonly TextBox cookies=new TextBox(),proxy=new TextBox(),rate=new TextBox(),extras=new TextBox(),commandPreview=new TextBox(),log=new TextBox();
  readonly ComboBox mode=new ComboBox(),quality=new ComboBox(),audioQuality=new ComboBox(),browser=new ComboBox();
  readonly CheckBox playlist=new CheckBox(),subs=new CheckBox(),autoSubs=new CheckBox(),embedSubs=new CheckBox(),thumb=new CheckBox(),embedThumb=new CheckBox(),metadata=new CheckBox(),infoJson=new CheckBox(),archive=new CheckBox(),raw=new CheckBox();
  readonly NumericUpDown fragments=new NumericUpDown();
  readonly ListView queue=new ListView(),formats=new ListView();
  readonly Label status=new Label(),videoInfo=new Label();readonly ProgressBar progress=new ProgressBar();readonly TabControl tabs=new TabControl();
  readonly Button add=new Button(),analyze=new Button(),resume=new Button(),cancel=new Button(),stop=new Button(),retry=new Button(),remove=new Button();
  readonly List<DownloadJob> jobs=new List<DownloadJob>();readonly Dictionary<DownloadJob,ListViewItem> rows=new Dictionary<DownloadJob,ListViewItem>();
  readonly string[] modes={"mp4","mkv","original","mp3","m4a","opus","flac","wav"};readonly int[] heights={0,2160,1440,1080,720,480,360};
  EngineSession session;bool running,auxBusy,stopRequested,closeAfter,initializing;string analyzedUrl,analyzedTitle;

  public MainForm(bool testMode=false) {
   testing=testMode;initializing=true;Text="DLP Видео — yt-dlp + Одноклассники";Font=new Font("Segoe UI",9.5f);BackColor=Color.FromArgb(248,249,251);
   ClientSize=new Size(1040,850);MinimumSize=new Size(960,850);StartPosition=FormStartPosition.CenterScreen;
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18,10,18,12),ColumnCount=1,RowCount=8};
   root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   foreach(float h in new float[]{24,72,40,320,24})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,43));root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));Controls.Add(root);
   var header=new Panel{Dock=DockStyle.Top,Height=70,BackColor=Color.FromArgb(238,119,22)};Controls.Add(header);
   header.Controls.Add(new Label{Text="DLP Видео",ForeColor=Color.White,Font=new Font("Segoe UI",21,FontStyle.Bold),Location=new Point(20,5),AutoSize=true});
   header.Controls.Add(new Label{Text="yt-dlp в удобном окне • Одноклассники, YouTube, VK и другие сайты",ForeColor=Color.White,Location=new Point(23,43),AutoSize=true});
   root.Controls.Add(new Label{Text="Ссылка на видео или плейлист — несколько ссылок можно вставить по одной на строку",AutoSize=true},0,0);
   var input=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty};input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,176));
   links.Multiline=true;links.ScrollBars=ScrollBars.Vertical;links.Dock=DockStyle.Fill;input.Controls.Add(links,0,0);input.SetRowSpan(links,2);
   var paste=Button("Вставить из буфера");paste.Dock=DockStyle.Fill;paste.Click+=delegate{try{if(Clipboard.ContainsText())links.Text=Clipboard.GetText().Trim();}catch(Exception e){SetStatus(e.Message);}};input.Controls.Add(paste,1,0);
   ConfigureButton(analyze,"Узнать форматы");analyze.Dock=DockStyle.Fill;analyze.Click+=async delegate{await Analyze();};input.Controls.Add(analyze,1,1);root.Controls.Add(input,0,1);
   var pathRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,144));
   pathRow.Controls.Add(Label("Сохранить в"),0,0);folder.Dock=DockStyle.Fill;pathRow.Controls.Add(folder,1,0);
   var choose=Button("Выбрать…");choose.Dock=DockStyle.Fill;choose.Click+=delegate{using(var d=new FolderBrowserDialog{SelectedPath=folder.Text,Description="Папка загрузок"})if(d.ShowDialog(this)==DialogResult.OK){folder.Text=d.SelectedPath;SaveSettings();}};pathRow.Controls.Add(choose,2,0);
   var open=Button("Открыть папку");open.Dock=DockStyle.Fill;open.Click+=delegate{OpenFolder(folder.Text);};pathRow.Controls.Add(open,3,0);root.Controls.Add(pathRow,0,2);
   tabs.Dock=DockStyle.Fill;root.Controls.Add(tabs,0,3);BuildDownloadTab();BuildFormatsTab();BuildAccessTab();BuildAdvancedTab();BuildLogTab();
   root.Controls.Add(new Label{Text="Очередь и история загрузок",Font=new Font(Font,FontStyle.Bold),AutoSize=true},0,4);
   ConfigureList(queue);queue.Columns.Add("Видео / ссылка",390);queue.Columns.Add("Формат",75);queue.Columns.Add("Статус",140);queue.Columns.Add("Прогресс / результат",350);queue.Dock=DockStyle.Fill;
   queue.DoubleClick+=delegate{var job=SelectedJobs().FirstOrDefault();if(job!=null&&job.Files!=null&&job.Files.Count>0&&File.Exists(job.Files[0]))Process.Start("explorer.exe","/select,"+Downloader.Quote(job.Files[0]));};root.Controls.Add(queue,0,5);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
   ConfigureButton(add,"Скачать / в очередь");add.Width=182;add.BackColor=Color.FromArgb(238,119,22);add.ForeColor=Color.White;add.Font=new Font(Font,FontStyle.Bold);add.Click+=async delegate{await AddDownloads();};
   ConfigureButton(resume,"Продолжить");resume.Width=113;resume.Click+=async delegate{await RunQueue();};
   ConfigureButton(cancel,"Отменить текущую");cancel.Width=158;cancel.Click+=async delegate{await CancelCurrent(false);};
   ConfigureButton(stop,"Стоп очереди");stop.Width=121;stop.Click+=async delegate{await CancelCurrent(true);};
   ConfigureButton(retry,"Повторить");retry.Width=106;retry.Click+=async delegate{foreach(var job in SelectedJobs().Where(j=>j.State!="Загрузка")){job.State="Ожидание";job.Detail="";job.Files.Clear();UpdateJob(job);}SaveQueue();await RunQueue();};
   ConfigureButton(remove,"Убрать из списка");remove.Width=144;remove.Click+=delegate{foreach(var job in SelectedJobs().Where(j=>j.State!="Загрузка").ToArray()){queue.Items.Remove(rows[job]);rows.Remove(job);jobs.Remove(job);}SaveQueue();RefreshButtons();};
   buttons.Controls.AddRange(new Control[]{add,resume,cancel,stop,retry,remove});root.Controls.Add(buttons,0,6);
   var footer=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty};footer.RowStyles.Add(new RowStyle(SizeType.Absolute,12));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   progress.Dock=DockStyle.Fill;footer.Controls.Add(progress,0,0);status.Dock=DockStyle.Fill;status.Text="Вставьте ссылку, выберите формат и нажмите «Скачать / в очередь».";footer.Controls.Add(status,0,1);root.Controls.Add(footer,0,7);
   LoadSettings();if(!testing)LoadQueue();initializing=false;RefreshButtons();UpdateCommand();
   links.TextChanged+=delegate{analyzedUrl=null;};
   foreach(Control control in new Control[]{mode,quality,audioQuality,browser,format,items,languages,template,cookies,proxy,rate,extras,folder,playlist,subs,autoSubs,embedSubs,thumb,embedThumb,metadata,infoJson,archive,raw,fragments}) {
    var box=control as TextBox;if(box!=null)box.TextChanged+=delegate{UpdateCommand();};
    var combo=control as ComboBox;if(combo!=null)combo.SelectedIndexChanged+=delegate{UpdateCommand();};
    var check=control as CheckBox;if(check!=null)check.CheckedChanged+=delegate{UpdateCommand();};
    var number=control as NumericUpDown;if(number!=null)number.ValueChanged+=delegate{UpdateCommand();};
   }
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(running||auxBusy){e.Cancel=true;closeAfter=true;stopRequested=true;EngineSession own=session;if(own!=null)Task.Run(delegate{own.Cancel();});SetStatus("Завершаю текущую операцию…");}else{SaveSettings();SaveQueue();}};
  }

  Label Label(string text){return new Label{Text=text,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(3,7,3,3)};}
  Button Button(string text){var b=new Button();ConfigureButton(b,text);return b;}
  void ConfigureButton(Button b,string text){b.Text=text;b.Height=32;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(211,215,222);b.Cursor=Cursors.Hand;b.Margin=new Padding(3);}
  void ConfigureList(ListView view){view.View=View.Details;view.FullRowSelect=true;view.GridLines=true;view.HideSelection=false;view.MultiSelect=true;}
  ComboBox Combo(ComboBox box,object[] values){box.DropDownStyle=ComboBoxStyle.DropDownList;box.Items.AddRange(values);box.SelectedIndex=0;box.Anchor=AnchorStyles.Left;box.Width=180;return box;}
  CheckBox Check(CheckBox c,string text){c.Text=text;c.AutoSize=true;c.Margin=new Padding(6,8,6,3);return c;}
  FlowLayoutPanel Row(){return new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=false,Padding=new Padding(3),Margin=Padding.Empty};}
  Panel Tab(string title){var t=new TabPage(title);var p=new Panel{Dock=DockStyle.Fill,Padding=new Padding(8),AutoScroll=true};t.Controls.Add(p);tabs.TabPages.Add(t);return p;}
  void BuildDownloadTab() {
   var p=Tab("Скачать");var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};p.Controls.Add(layout);
   var row=Row();row.Controls.Add(Label("Что скачать"));Combo(mode,new object[]{"Видео MP4","Видео MKV","Исходный формат","Аудио MP3","Аудио M4A","Аудио Opus","Аудио FLAC","Аудио WAV"});row.Controls.Add(mode);
   row.Controls.Add(Label("Качество видео"));Combo(quality,new object[]{"Лучшее доступное","До 2160p (4K)","До 1440p","До 1080p","До 720p","До 480p","До 360p"});row.Controls.Add(quality);
   row.Controls.Add(Label("Качество аудио"));Combo(audioQuality,new object[]{"Лучшее (VBR 0)","320 кбит/с","192 кбит/с","128 кбит/с"});audioQuality.Width=143;row.Controls.Add(audioQuality);layout.Controls.Add(row);
   mode.SelectedIndexChanged+=delegate{quality.Enabled=!Downloader.IsAudio(modes[mode.SelectedIndex]);audioQuality.Enabled=Downloader.IsAudio(modes[mode.SelectedIndex]);};audioQuality.Enabled=false;
   row=Row();row.Controls.Add(Check(playlist,"Скачать плейлист"));row.Controls.Add(Label("Элементы"));items.Width=195;row.Controls.Add(items);row.Controls.Add(Label("Например 1-10,15. Пусто — все элементы."));layout.Controls.Add(row);
   row=Row();row.Controls.Add(Label("Формат yt-dlp"));format.Width=510;row.Controls.Add(format);row.Controls.Add(Label("Пусто — выбор по настройкам выше"));layout.Controls.Add(row);
   row=Row();row.Controls.Add(Check(subs,"Субтитры"));row.Controls.Add(Check(autoSubs,"Автосубтитры"));row.Controls.Add(Check(embedSubs,"Встроить в видео"));row.Controls.Add(Label("Языки"));languages.Width=155;row.Controls.Add(languages);row.Controls.Add(Label("ru,en или all"));layout.Controls.Add(row);
   row=Row();row.Controls.Add(Check(thumb,"Сохранить обложку"));row.Controls.Add(Check(embedThumb,"Встроить обложку"));row.Controls.Add(Check(metadata,"Встроить метаданные"));row.Controls.Add(Check(infoJson,"Сохранить JSON"));layout.Controls.Add(row);
   row=Row();row.Controls.Add(Check(archive,"Пропускать видео из архива загрузок"));row.Controls.Add(Label("Шаблон имени"));template.Width=427;row.Controls.Add(template);layout.Controls.Add(row);
   row=Row();var note=Label("Точный формат можно выбрать на вкладке «Форматы». Он заменяет автоматический выбор качества.\nПо умолчанию готовые файлы не перезаписываются; настройки запоминаются для следующего запуска.");note.Font=new Font(Font.FontFamily,8.5f);row.Controls.Add(note);layout.Controls.Add(row);
  }
  void BuildFormatsTab() {
   var p=Tab("Форматы");var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));p.Controls.Add(layout);
   videoInfo.Dock=DockStyle.Fill;videoInfo.Text="Нажмите «Узнать форматы», чтобы получить название и доступные варианты с сайта.";layout.Controls.Add(videoInfo,0,0);
   ConfigureList(formats);formats.Columns.Add("ID",100);formats.Columns.Add("Контейнер",90);formats.Columns.Add("Размер / качество",150);formats.Columns.Add("Видео",180);formats.Columns.Add("Аудио",130);formats.Columns.Add("Объём / примечание",270);formats.Dock=DockStyle.Fill;layout.Controls.Add(formats,0,1);
   var row=Row();var use=Button("Выбрать выделенный формат");use.Width=252;use.Click+=delegate{UseFormats();};formats.DoubleClick+=delegate{UseFormats();};row.Controls.Add(use);row.Controls.Add(Label("Можно выделить видео и аудио вместе. Без звука → будет добавлен лучший звук."));layout.Controls.Add(row,0,2);
  }
  void BuildAccessTab() {
   var p=Tab("Доступ и сеть");var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};p.Controls.Add(layout);
   var row=Row();row.Controls.Add(Label("Cookies из браузера"));Combo(browser,new object[]{"Не использовать","Chrome","Edge","Firefox","Brave","Opera","Vivaldi"});row.Controls.Add(browser);row.Controls.Add(Label("Используется ваш вход на сайте, если yt-dlp может прочитать cookies."));layout.Controls.Add(row);
   row=Row();row.Controls.Add(Label("Файл cookies.txt"));cookies.Width=537;row.Controls.Add(cookies);var browse=Button("Выбрать…");browse.Width=98;browse.Click+=delegate{using(var d=new OpenFileDialog{Filter="Cookies (*.txt)|*.txt|Все файлы|*.*"})if(d.ShowDialog(this)==DialogResult.OK)cookies.Text=d.FileName;};row.Controls.Add(browse);var clear=Button("Очистить");clear.Width=98;clear.Click+=delegate{cookies.Clear();};row.Controls.Add(clear);layout.Controls.Add(row);
   row=Row();row.Controls.Add(Label("Прокси"));proxy.Width=380;row.Controls.Add(proxy);row.Controls.Add(Label("HTTP или SOCKS, например socks5://127.0.0.1:1080"));layout.Controls.Add(row);
   row=Row();row.Controls.Add(Label("Лимит скорости"));rate.Width=123;row.Controls.Add(rate);row.Controls.Add(Label("Например 2M, 500K"));row.Controls.Add(Label("Параллельные фрагменты"));fragments.Minimum=1;fragments.Maximum=16;fragments.Value=3;fragments.Width=69;row.Controls.Add(fragments);layout.Controls.Add(row);
   row=Row();var note=Label("Файл cookies имеет приоритет над выбранным браузером. Формат файла — Netscape cookies.txt.\nНекоторые браузеры защищают cookies от чтения: это ограничение yt-dlp, а не отдельный вход в приложение.\nПрокси применяется к yt-dlp; поиск источника Яндекс Видео использует системное подключение.");note.Font=new Font(Font.FontFamily,8.5f);row.Controls.Add(note);layout.Controls.Add(row);
  }
  void BuildAdvancedTab() {
   var p=Tab("Параметры yt-dlp");var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,26));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,64));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,27));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));p.Controls.Add(layout);
   layout.Controls.Add(Label("Дополнительные аргументы yt-dlp — например --download-sections \"*00:00-00:20\" --force-keyframes-at-cuts"),0,0);
   extras.Multiline=true;extras.ScrollBars=ScrollBars.Vertical;extras.Dock=DockStyle.Fill;extras.Font=new Font("Consolas",9);layout.Controls.Add(extras,0,1);
   Check(raw,"Только мои параметры: не применять формат, качество, плейлист и папку из формы");layout.Controls.Add(raw,0,2);
   commandPreview.Multiline=true;commandPreview.ReadOnly=true;commandPreview.ScrollBars=ScrollBars.Vertical;commandPreview.Dock=DockStyle.Fill;commandPreview.Font=new Font("Consolas",8);layout.Controls.Add(commandPreview,0,3);
   var row=Row();var run=Button("Выполнить параметры");run.Width=190;run.Click+=async delegate{await RunCommand(extras.Text);};row.Controls.Add(run);
   var help=Button("Справка yt-dlp");help.Width=136;help.Click+=async delegate{await RunCommand("--help");};row.Controls.Add(help);
   var sites=Button("Поддерживаемые сайты");sites.Width=195;sites.Click+=async delegate{await RunCommand("--list-extractors");};row.Controls.Add(sites);
   var update=Button("Обновить yt-dlp");update.Width=150;update.Click+=async delegate{await RunCommand("--update-to nightly");};row.Controls.Add(update);
   var version=Button("Версия");version.Width=82;version.Click+=async delegate{await RunCommand("--version");};row.Controls.Add(version);layout.Controls.Add(row,0,4);
  }
  void BuildLogTab() {
   var p=Tab("Журнал");log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Both;log.WordWrap=false;log.Font=new Font("Consolas",9);log.Dock=DockStyle.Fill;p.Controls.Add(log);
   var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=36};p.Controls.Add(row);var copy=Button("Копировать журнал");copy.Width=180;copy.Click+=delegate{try{if(log.TextLength>0)Clipboard.SetText(log.Text);}catch(Exception e){SetStatus(e.Message);}};row.Controls.Add(copy);var clean=Button("Очистить журнал");clean.Width=165;clean.Click+=delegate{log.Clear();};row.Controls.Add(clean);
  }

  DownloadOptions GetOptions() {
   string[] audio={"0","320K","192K","128K"};string[] names={"","chrome","edge","firefox","brave","opera","vivaldi"};
   return new DownloadOptions{Folder=folder.Text.Trim(),Mode=modes[Math.Max(0,mode.SelectedIndex)],Height=heights[Math.Max(0,quality.SelectedIndex)],AudioQuality=audio[Math.Max(0,audioQuality.SelectedIndex)],
    Format=format.Text.Trim(),Playlist=playlist.Checked,PlaylistItems=items.Text.Trim(),Subtitles=subs.Checked,AutoSubtitles=autoSubs.Checked,EmbedSubtitles=embedSubs.Checked,SubtitleLanguages=languages.Text.Trim(),
    Thumbnail=thumb.Checked,EmbedThumbnail=embedThumb.Checked,Metadata=metadata.Checked,InfoJson=infoJson.Checked,Archive=archive.Checked,OutputTemplate=template.Text.Trim(),
    CookieBrowser=names[Math.Max(0,browser.SelectedIndex)],CookieFile=cookies.Text.Trim(),Proxy=proxy.Text.Trim(),RateLimit=rate.Text.Trim(),Fragments=(int)fragments.Value,ExtraArguments=extras.Text,RawOptions=raw.Checked};
  }
  void ApplyOptions(DownloadOptions o) {
   folder.Text=o.Folder;mode.SelectedIndex=Math.Max(0,Array.IndexOf(modes,o.Mode));quality.SelectedIndex=Math.Max(0,Array.IndexOf(heights,o.Height));
   string[] a={"0","320K","192K","128K"};audioQuality.SelectedIndex=Math.Max(0,Array.IndexOf(a,o.AudioQuality));format.Text=o.Format;playlist.Checked=o.Playlist;items.Text=o.PlaylistItems;
   subs.Checked=o.Subtitles;autoSubs.Checked=o.AutoSubtitles;embedSubs.Checked=o.EmbedSubtitles;languages.Text=o.SubtitleLanguages;thumb.Checked=o.Thumbnail;embedThumb.Checked=o.EmbedThumbnail;metadata.Checked=o.Metadata;infoJson.Checked=o.InfoJson;archive.Checked=o.Archive;template.Text=o.OutputTemplate;
   string[] b={"","chrome","edge","firefox","brave","opera","vivaldi"};browser.SelectedIndex=Math.Max(0,Array.IndexOf(b,o.CookieBrowser));cookies.Text=o.CookieFile;proxy.Text=o.Proxy;rate.Text=o.RateLimit;fragments.Value=Math.Max(1,Math.Min(16,o.Fragments));extras.Text=o.ExtraArguments;raw.Checked=o.RawOptions;
  }
  void LoadSettings() {
   var options=testing?null:Storage.Load<DownloadOptions>("settings-v2.json");
   if(options==null){options=new DownloadOptions();try{string legacy=Path.Combine(Storage.DirectoryPath,"folder.txt");if(!testing&&File.Exists(legacy))options.Folder=File.ReadAllText(legacy, System.Text.Encoding.UTF8);}catch{}}
   ApplyOptions(options);
  }
  void SaveSettings(){if(testing)return;try{Storage.Save("settings-v2.json",GetOptions());}catch(Exception e){AppendLog("Не удалось сохранить настройки: "+e.Message);}}
  void SaveQueue(){if(testing)return;try{Storage.Save("queue-v2.json",jobs);}catch(Exception e){AppendLog("Не удалось сохранить очередь: "+e.Message);}}
  void LoadQueue(){var saved=Storage.Load<List<DownloadJob>>("queue-v2.json");if(saved==null)return;foreach(var job in saved.Where(j=>j.Options!=null&&j.Url!=null)){if(job.State=="Загрузка")job.State="Прервано";if(job.Files==null)job.Files=new List<string>();AddJob(job);}if(jobs.Any(j=>j.State=="Ожидание"))SetStatus("Есть сохранённые задачи. Нажмите «Продолжить» для запуска.");}
  void UpdateCommand(){if(initializing)return;try{commandPreview.Text=Downloader.Quote(Path.Combine(Downloader.Tools,"yt-dlp.exe"))+" "+Downloader.CommandLine(Downloader.BuildArguments(GetOptions()))+" -- <ссылка>";}catch(Exception e){commandPreview.Text=e.Message;}}
  void SetStatus(string text){status.Text=text;}
  void Post(Action action){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)delegate{if(!IsDisposed)action();});}catch(InvalidOperationException){}}
  void AppendLog(string line){if(log.TextLength>100000)log.Text=log.Text.Substring(log.TextLength-65000);log.AppendText(line+Environment.NewLine);}
  void RefreshButtons(){analyze.Enabled=!running&&!auxBusy;add.Enabled=!auxBusy;resume.Enabled=!running&&!auxBusy&&jobs.Any(j=>j.State=="Ожидание");cancel.Enabled=stop.Enabled=running||auxBusy;retry.Enabled=remove.Enabled=!auxBusy;}
  IEnumerable<DownloadJob> SelectedJobs(){return queue.SelectedItems.Cast<ListViewItem>().Select(r=>(DownloadJob)r.Tag).ToArray();}
  void AddJob(DownloadJob job){jobs.Add(job);var row=new ListViewItem(new[]{job.Title??job.Url,job.Options.Mode.ToUpperInvariant(),job.State,job.Detail??""}){Tag=job};rows[job]=row;queue.Items.Add(row);UpdateJob(job);}
  void UpdateJob(DownloadJob job){ListViewItem row;if(!rows.TryGetValue(job,out row))return;row.Text=job.Title??job.Url;row.SubItems[2].Text=job.State;row.SubItems[3].Text=job.Detail??"";row.ForeColor=job.State=="Ошибка"?Color.Firebrick:job.State=="Готово"?Color.DarkGreen:Color.FromArgb(35,40,48);}
  void HandleLine(DownloadJob job,string line){Post(delegate {
   if(line.StartsWith("__DLP_PROGRESS__")) {
    var values=line.Substring("__DLP_PROGRESS__".Length).Split('|');double percentage;
    if(values.Length>=3&&double.TryParse(values[0].Trim().TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out percentage)){progress.Style=ProgressBarStyle.Continuous;progress.Value=Math.Max(0,Math.Min(100,(int)percentage));
     string detail=values[0].Trim()+" • "+values[1].Trim()+" • осталось "+values[2].Trim();if(values.Length>=5&&values[3]!="NA")detail="Элемент "+values[3]+"/"+values[4]+" • "+detail;
     if(job!=null){job.Detail=detail;UpdateJob(job);}SetStatus(detail);
    }
   }else if(!line.StartsWith("__DLP_FILE__")){AppendLog(line);if(line.Contains("[Merger]")||line.Contains("[VideoConvertor]")||line.Contains("[ExtractAudio]")){progress.Style=ProgressBarStyle.Marquee;SetStatus("Обработка файла: объединение, конвертация или извлечение звука…");}}
  });}
  async Task AddDownloads() {
   List<string> urls;DownloadOptions options;
   try{urls=links.Lines.Where(s=>!string.IsNullOrWhiteSpace(s)).Select(Downloader.Normalize).Distinct().ToList();if(urls.Count==0)throw new ArgumentException("Вставьте хотя бы одну ссылку.");options=GetOptions();Downloader.BuildArguments(options);}catch(Exception e){SetStatus(e.Message);return;}
   foreach(string url in urls)AddJob(new DownloadJob{Url=url,Title=url==analyzedUrl?analyzedTitle:null,Options=options.Clone()});SaveSettings();SaveQueue();RefreshButtons();await RunQueue();
  }
  async Task RunQueue() {
   if(running||auxBusy)return;running=true;stopRequested=false;RefreshButtons();
   try {while(!stopRequested) {
    DownloadJob job=jobs.FirstOrDefault(j=>j.State=="Ожидание");if(job==null)break;
    job.State="Загрузка";job.Detail="Получаю данные с сайта…";UpdateJob(job);SaveQueue();progress.Value=0;progress.Style=ProgressBarStyle.Marquee;SetStatus(job.Detail);AppendLog("\n▶ "+job.Url+" ["+job.Options.Mode+"]");
    using(var own=new EngineSession()){session=own;EngineResult result=await own.RunAsync(job.Url,job.Options,delegate(string l){HandleLine(job,l);});session=null;
     job.Files=result.Files;if(job.Title==null&&result.Files.Count>0)job.Title=Path.GetFileNameWithoutExtension(result.Files[0]);
     if(result.Cancelled){job.State="Отменено";job.Detail="Можно повторить и продолжить загрузку";}
     else if(result.ExitCode==0){job.State=result.Files.Count>0?"Готово":"Выполнено";job.Detail=result.Files.Count>0?"Файлов: "+result.Files.Count+" • "+Path.GetFileName(result.Files.Last()):"yt-dlp завершился без нового медиафайла; смотрите журнал";}
     else{job.State="Ошибка";job.Detail=(result.Files.Count>0?"Файлов сохранено: "+result.Files.Count+"; ":"")+(result.Error??("Код yt-dlp: "+result.ExitCode));}
    }
    UpdateJob(job);SaveQueue();progress.Style=ProgressBarStyle.Continuous;progress.Value=job.State=="Готово"?100:0;SetStatus(job.State+": "+job.Detail);
   }}finally{session=null;running=false;progress.Style=ProgressBarStyle.Continuous;RefreshButtons();if(closeAfter)Close();}
  }
  async Task CancelCurrent(bool all){if(all)stopRequested=true;EngineSession own=session;if(own==null)return;cancel.Enabled=false;SetStatus("Отменяю текущую операцию…");await Task.Run(delegate{own.Cancel();});}
  async Task Analyze() {
   if(running||auxBusy)return;string url;DownloadOptions options;
   try{url=Downloader.Normalize(links.Lines.FirstOrDefault(l=>!string.IsNullOrWhiteSpace(l))??"");options=GetOptions();}catch(Exception e){SetStatus(e.Message);return;}
   auxBusy=true;RefreshButtons();SetStatus("Получаю информацию и форматы…");progress.Style=ProgressBarStyle.Marquee;
   try {using(var own=new EngineSession()){session=own;EngineResult result=await own.RunAsync(url,options,delegate(string line){if(!line.StartsWith("{"))Post(delegate{AppendLog(line);});},true);session=null;
    if(result.Cancelled){SetStatus("Получение форматов отменено.");return;}
    if(result.ExitCode!=0)throw new Exception(result.Error??"yt-dlp не смог получить форматы. Смотрите журнал.");
    var data=Json.Serializer().Deserialize<Dictionary<string,object>>(result.Output.ToString());ShowFormats(data);analyzedUrl=url;analyzedTitle=Json.Text(data,"title",url);SetStatus("Форматы получены. Выберите вариант или скачайте с текущими настройками.");tabs.SelectedIndex=1;
   }}catch(Exception e){SetStatus(e.Message);AppendLog("ERROR: "+e.Message);}finally{auxBusy=false;session=null;progress.Style=ProgressBarStyle.Continuous;RefreshButtons();if(closeAfter)Close();}
  }
  void ShowFormats(Dictionary<string,object> data) {
   formats.Items.Clear();string title=Json.Text(data,"title","Видео"),extractor=Json.Text(data,"extractor");string duration=Json.Text(data,"duration");
   object entries;if(data.TryGetValue("entries",out entries)&&entries is IList){int count=((IList)entries).Count;videoInfo.Text=title+" • "+extractor+" • элементов в списке: "+count+". Для форматов вставьте ссылку отдельного ролика.";return;}
   videoInfo.Text=title+" • "+extractor+(duration!=""?" • длительность "+duration+" сек.":"");object available;if(!data.TryGetValue("formats",out available)||!(available is IList))return;
   foreach(var value in (IList)available) {
    var f=value as Dictionary<string,object>;if(f==null)continue;string resolution=Json.Text(f,"resolution");if(resolution=="")resolution=Json.Text(f,"height")+"p";
    string note=Json.Text(f,"format_note"),size=Json.Text(f,"filesize",Json.Text(f,"filesize_approx"));double bytes;if(double.TryParse(size,NumberStyles.Float,CultureInfo.InvariantCulture,out bytes))size=(bytes/1048576).ToString("0.0",CultureInfo.InvariantCulture)+" МБ";
    formats.Items.Add(new ListViewItem(new[]{Json.Text(f,"format_id"),Json.Text(f,"ext"),resolution,Json.Text(f,"vcodec"),Json.Text(f,"acodec"),size+" "+note}){Tag=f});
   }
  }
  void UseFormats(){if(formats.SelectedItems.Count==0){SetStatus("Выделите формат в списке.");return;}var selected=formats.SelectedItems.Cast<ListViewItem>().ToArray();string expression=string.Join("+",selected.Select(r=>r.Text));
   if(selected.Length==1){var data=(Dictionary<string,object>)selected[0].Tag;if(Json.Text(data,"acodec")=="none"&&Json.Text(data,"vcodec")!="none")expression+="+ba/b";}
   format.Text=expression;tabs.SelectedIndex=0;SetStatus("Выбран формат yt-dlp: "+expression);
  }
  async Task RunCommand(string arguments) {
   if(running||auxBusy){SetStatus("Дождитесь текущей операции или отмените её.");return;}if(string.IsNullOrWhiteSpace(arguments)){SetStatus("Введите аргументы yt-dlp.");return;}
   auxBusy=true;RefreshButtons();tabs.SelectedIndex=4;progress.Style=ProgressBarStyle.Marquee;SetStatus("Выполняю yt-dlp…");
   try{using(var own=new EngineSession()){session=own;EngineResult result=await own.CommandAsync(arguments,delegate(string l){HandleLine(null,l);});session=null;SetStatus(result.Cancelled?"Операция отменена.":result.ExitCode==0?"yt-dlp завершился успешно. Результат в журнале.":"Ошибка yt-dlp: "+(result.Error??result.ExitCode.ToString()));}}
   finally{auxBusy=false;session=null;progress.Style=ProgressBarStyle.Continuous;RefreshButtons();if(closeAfter)Close();}
  }
  void OpenFolder(string path){try{Directory.CreateDirectory(path);Process.Start("explorer.exe",Downloader.Quote(path));}catch(Exception e){SetStatus(e.Message);}}
  void Snapshot(string path){using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(path);}}
  public bool TestUI(string image){links.Text="file:///tmp/video.mp4";AddDownloads().GetAwaiter().GetResult();bool valid=jobs.Count==0&&status.Text.Contains("ссылка");links.Clear();ApplyOptions(new DownloadOptions());SetStatus("Вставьте ссылку, выберите формат и нажмите «Скачать / в очередь».");Snapshot(image);return valid&&mode.Items.Count==8&&tabs.TabPages.Count==5;}
  public async Task<bool> TestDownload(string url,string destination,string image,string selectedMode){ApplyOptions(new DownloadOptions{Folder=destination,Mode=selectedMode,Height=480});links.Text=url;await Analyze();bool gotFormats=formats.Items.Count>0;await AddDownloads();Snapshot(image);File.WriteAllText(image+".log",log.Text);return gotFormats&&jobs.Count==1&&jobs[0].State=="Готово"&&jobs[0].Files.Count>0&&add.Enabled;}
 }
}
