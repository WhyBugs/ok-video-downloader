using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Globalization;
using System.Net;

namespace OkVideo {
 public static class Downloader {
  public static string Normalize(string value) {
   value = value.Trim();
   if (!value.Contains("://")) value = "https://" + value;
   Uri u;
   if (!Uri.TryCreate(value, UriKind.Absolute, out u) || (u.Scheme != "https" && u.Scheme != "http")) throw new ArgumentException("Вставьте ссылку на видео из Одноклассников.");
   string host = u.Host.ToLowerInvariant();
   if (Regex.IsMatch(host, @"^(?:www\.)?yandex\.(?:ru|com|by|kz|uz)$")) {
    Match preview=Regex.Match(u.AbsolutePath,@"^/video/(?:preview|touch/preview)/(\d+)/?$");
    if(!preview.Success)throw new ArgumentException("Нужна ссылка Яндекс Видео вида yandex.ru/video/preview/…");
    return "https://"+host+"/video/preview/"+preview.Groups[1].Value;
   }
   if (!Regex.IsMatch(host, @"^(?:(?:www|m)\.)?(?:ok|odnoklassniki)\.ru$")) throw new ArgumentException("Поддерживаются ссылки Одноклассников и Яндекс Видео.");
   Match m = Regex.Match(u.AbsolutePath, @"^/(?:video|videoembed|web-api/video/moviePlayer)/([\d-]+)/?$");
   if (!m.Success && u.AbsolutePath == "/dk") m = Regex.Match(u.Query, @"(?:[?&])st\.mvId=([\d-]+)(?:&|$)");
   if (!m.Success) throw new ArgumentException("Откройте само видео и скопируйте его ссылку: https://ok.ru/video/…");
   return "https://ok.ru/video/" + m.Groups[1].Value;
  }
  public static string ExtractYandexSource(string html) {
   // Only the source link of the selected player, never a recommendation.
   Match source=Regex.Match(html,@"<a\b(?=[^>]*\bclass=[""'][^""']*VideoViewer-Source(?:Path)?Link\b[^""']*[""'])[^>]*\bhref=[""'](?<url>[^""']+)[""']",RegexOptions.IgnoreCase);
   if(!source.Success)throw new ArgumentException("Яндекс не показал исходную ссылку. Откройте ролик в браузере и скопируйте ссылку с сайта Одноклассников.");
   string url=Normalize(WebUtility.HtmlDecode(source.Groups["url"].Value));
   if(new Uri(url).Host.Contains("yandex."))throw new ArgumentException("Исходный ролик должен быть на сайте Одноклассников.");
   return url;
  }
  public static string Resolve(string url) {
   url=Normalize(url);if(!new Uri(url).Host.Contains("yandex."))return url;
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(url);request.Timeout=30000;request.ReadWriteTimeout=30000;request.UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36";
   request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
   using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return ExtractYandexSource(reader.ReadToEnd());
  }
  public static string Quote(string s) {
   var b=new StringBuilder("\"");int slashes=0;
   foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}
   b.Append('\\',slashes*2);b.Append('"');return b.ToString();
  }
  public static Process Start(string url, string folder, int height, Action<string> line) {
   url = Resolve(url); Directory.CreateDirectory(folder);
   string tools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
   if (!File.Exists(Path.Combine(tools, "yt-dlp.exe")) || !File.Exists(Path.Combine(tools, "ffmpeg.exe"))) throw new FileNotFoundException("Не найдены компоненты загрузки. Переустановите приложение.");
   string cap = height == 0 ? "" : "[height<=" + height + "]";
   string native = height==0 ? "" : "/b[ext=mp4][format_id~='^("+(height>=1080?"full|":"")+(height>=720?"hd|":"")+"sd|low|lowest|mobile)$']";
   string format = "bv*[ext=mp4]" + cap + "+ba[ext=m4a]/b[ext=mp4]" + cap + native + "/bv*" + cap + "+ba/b" + cap;
   string args = "--ignore-config --no-simulate --no-quiet --progress --encoding utf-8 --no-playlist --no-colors --newline --windows-filenames --no-overwrites --socket-timeout 30 --retries 3 --fragment-retries 3 --concurrent-fragments 3 --match-filter !is_live --merge-output-format mp4 --recode-video mp4 --ffmpeg-location " + Quote(tools) + " -f " + Quote(format) + " -P " + Quote(folder) + " -o " + Quote("%(title).150B [%(id)s].%(ext)s") + " --progress-template " + Quote("download:PROGRESS|%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s") + " --print " + Quote("after_move:FILE|%(filepath)s") + " -- " + Quote(url);
   var p = new Process();
   p.StartInfo = new ProcessStartInfo(Path.Combine(tools,"yt-dlp.exe"),args) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true, StandardOutputEncoding=Encoding.UTF8, StandardErrorEncoding=Encoding.UTF8 };
   p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if(e.Data != null) line(e.Data); };
   p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if(e.Data != null) line(e.Data); };
   p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); return p;
  }
  public static void Cancel(Process p) {
   if (p == null) return;
   try { if (!p.HasExited) { using(var killer=Process.Start(new ProcessStartInfo("taskkill.exe", "/PID " + p.Id + " /T /F") {UseShellExecute=false,CreateNoWindow=true})) {if(!killer.WaitForExit(3000)){killer.Kill();if(!p.HasExited)p.Kill();}} } } catch {try{if(!p.HasExited)p.Kill();}catch{}}
  }
 }
 public class MainForm : Form {
  TextBox link = new TextBox(), folder = new TextBox(), log = new TextBox();
  Button download = new Button(), cancel = new Button(), choose = new Button(), paste = new Button(), open = new Button();
  ComboBox quality = new ComboBox(); ProgressBar progress = new ProgressBar(); Label status = new Label();
  Process current; bool cancelled; string lastFile; string lastError; bool testing;
  string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OkVideo", "folder.txt");
  public MainForm(bool testMode=false) {
   testing=testMode;
   Text="ОК Видео — скачать MP4"; ClientSize=new Size(690,540); MinimumSize=MaximumSize=Size; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
   Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(248,249,251);
   var header=new Panel {Location=new Point(0,0),Size=new Size(690,92),BackColor=Color.FromArgb(238,119,22)}; Controls.Add(header);
   header.Controls.Add(new Label {Text="ОК Видео",Font=new Font("Segoe UI",22,FontStyle.Bold),ForeColor=Color.White,Location=new Point(24,12),AutoSize=true});
   header.Controls.Add(new Label {Text="Одноклассники и Яндекс Видео → файл MP4",ForeColor=Color.White,Location=new Point(27,57),AutoSize=true});
   AddLabel("Ссылка на видео",24,108); link.SetBounds(24,134,536,30); Controls.Add(link);
   SetupButton(paste,"Вставить",570,132,96); paste.Click += delegate {try {if(Clipboard.ContainsText()) link.Text=Clipboard.GetText().Trim();} catch {status.Text="Не удалось прочитать буфер обмена.";} };
   AddLabel("Куда сохранить",24,177); folder.SetBounds(24,203,536,30); folder.ReadOnly=true; Controls.Add(folder);
   folder.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","ОК Видео");
   try {if(!testing&&File.Exists(settings)) folder.Text=File.ReadAllText(settings,Encoding.UTF8);} catch {}
   SetupButton(choose,"Выбрать…",570,201,96); choose.Click += delegate {using(var d=new FolderBrowserDialog {Description="Папка для скачанных видео",SelectedPath=folder.Text}) {if(d.ShowDialog(this)==DialogResult.OK) {folder.Text=d.SelectedPath;SaveSettings();}}};
   AddLabel("Качество",24,250); quality.SetBounds(114,246,217,31); quality.DropDownStyle=ComboBoxStyle.DropDownList;
   quality.Items.AddRange(new object[]{"Лучшее доступное","До 1080p","До 720p","До 480p"});quality.SelectedIndex=0;Controls.Add(quality);
   SetupButton(open,"Открыть папку",486,244,180);open.Click += delegate {try{Directory.CreateDirectory(folder.Text);Process.Start("explorer.exe",Downloader.Quote(folder.Text));}catch(Exception e){MessageBox.Show(this,e.Message,"Папка");}};
   SetupButton(download,"Скачать MP4",24,291,466);download.BackColor=Color.FromArgb(238,119,22);download.ForeColor=Color.White;download.Font=new Font(Font,FontStyle.Bold);download.Click += async delegate {await RunDownload();};
   SetupButton(cancel,"Отмена",502,291,164);cancel.Enabled=false;cancel.Click += async delegate {cancelled=true;cancel.Enabled=false;status.Text="Отменяю загрузку…";Process own=current;await Task.Run(delegate{Downloader.Cancel(own);});};
   progress.SetBounds(24,343,642,15);Controls.Add(progress);status.SetBounds(24,367,642,45);status.Text="Вставьте ссылку и нажмите «Скачать MP4».";Controls.Add(status);
   log.SetBounds(24,416,642,89);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Font=new Font("Consolas",8);log.BackColor=Color.White;Controls.Add(log);
   AddLabel("Закрытые, удалённые и платные видео могут быть недоступны.",24,516).Font=new Font("Segoe UI",8);
   AcceptButton=download;FormClosing += delegate(object s, FormClosingEventArgs e){if(current!=null&&!current.HasExited){cancelled=true;Downloader.Cancel(current);} SaveSettings();};
  }
  Label AddLabel(string text,int x,int y){var l=new Label{Text=text,Location=new Point(x,y),AutoSize=true};Controls.Add(l);return l;}
  void SetupButton(Button b,string text,int x,int y,int width){b.Text=text;b.SetBounds(x,y,width,36);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(215,218,224);b.Cursor=Cursors.Hand;Controls.Add(b);}
  void SaveSettings(){if(testing)return;try{Directory.CreateDirectory(Path.GetDirectoryName(settings));File.WriteAllText(settings,folder.Text,Encoding.UTF8);}catch{}}
  void HandleLine(string line){
   if(IsDisposed || !IsHandleCreated) return;
   BeginInvoke((Action)delegate {
    if(line.StartsWith("PROGRESS|")) {var parts=line.Split('|'); double value; if(parts.Length>=4 && double.TryParse(parts[1].Trim().TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out value)){progress.Style=ProgressBarStyle.Continuous;progress.Value=Math.Max(0,Math.Min(100,(int)value));status.Text="Загрузка: " + parts[1].Trim()+"   •   "+parts[2].Trim()+"   •   осталось "+parts[3].Trim();} }
    else if(line.StartsWith("FILE|")) lastFile=line.Substring(5);
    else {log.AppendText(line+Environment.NewLine);if(line.StartsWith("ERROR:"))lastError=line.Substring(6).Trim();if(line.Contains("[Merger]")||line.Contains("[VideoConvertor]")){progress.Style=ProgressBarStyle.Marquee;status.Text="Подготавливаю MP4…";}}
   });
  }
  async Task RunDownload(){
   string url;try{url=Downloader.Normalize(link.Text);}catch(Exception e){status.Text=e.Message;link.Focus();return;}
   int[] heights={0,1080,720,480};cancelled=false;lastFile=null;lastError=null;log.Clear();progress.Value=0;progress.Style=ProgressBarStyle.Marquee;
   download.Enabled=choose.Enabled=paste.Enabled=quality.Enabled=link.Enabled=false;cancel.Enabled=true;status.Text="Получаю информацию о видео…";SaveSettings();
   try {
    int height=heights[quality.SelectedIndex];string destination=folder.Text;
    current=await Task.Run(delegate {return Downloader.Start(url,destination,height,HandleLine);});
    if(cancelled)Downloader.Cancel(current);
    await Task.Run(delegate {current.WaitForExit();});
    // Drain posted stdout callbacks before judging the resulting file.
    await Task.Delay(120);
    if(cancelled) status.Text="Загрузка отменена. При повторе файл продолжит скачиваться.";
    else if(current.ExitCode==0 && lastFile!=null && File.Exists(lastFile) && Path.GetExtension(lastFile).Equals(".mp4",StringComparison.OrdinalIgnoreCase)){progress.Value=100;status.Text="Готово: "+Path.GetFileName(lastFile);log.AppendText("Сохранено: "+lastFile+Environment.NewLine);}
    else {status.Text="Не удалось скачать видео. Подробности ниже.";if(lastError!=null)log.AppendText("Проверьте доступность видео и подключение к интернету."+Environment.NewLine);}
   }catch(Exception e){status.Text="Ошибка: "+e.Message;log.AppendText(e.ToString());}
   finally {if(current!=null){current.Dispose();current=null;}progress.Style=ProgressBarStyle.Continuous;download.Enabled=choose.Enabled=paste.Enabled=quality.Enabled=link.Enabled=true;cancel.Enabled=false;}
  }
  public bool TestUI(string image){link.Text="https://example.com/video/123";RunDownload().GetAwaiter().GetResult();bool good=status.Text.Contains("Поддерживаются")&&download.Enabled;link.Text="";status.Text="Вставьте ссылку и нажмите «Скачать MP4».";using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(image);}return good;}
  public async Task<bool> TestDownload(string url,string destination,string image){link.Text=url;folder.Text=destination;quality.SelectedIndex=3;await RunDownload();using(var bmp=new Bitmap(Width,Height)){DrawToBitmap(bmp,new Rectangle(0,0,Width,Height));bmp.Save(image);}return status.Text.StartsWith("Готово:")&&download.Enabled&&!cancel.Enabled;}
 }
 static class Program {
  [STAThread] static int Main(string[] args){
   if(args.Length>=3 && args[0]=="--download") {try{string file=null;using(var p=Downloader.Start(args[1],args[2],480,delegate(string l){Console.WriteLine(l);if(l.StartsWith("FILE|"))file=l.Substring(5);})){p.WaitForExit();return p.ExitCode==0&&file!=null&&File.Exists(file)&&Path.GetExtension(file)==".mp4"?0:1;}}catch(Exception e){Console.Error.WriteLine(e.Message);return 2;}}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var f=new MainForm(args.Length>0)){if(args.Length==2&&args[0]=="--ui-test"){f.Show();Application.DoEvents();bool ok=f.TestUI(args[1]);f.Close();return ok?0:1;}if(args.Length==4&&args[0]=="--ui-download-test"){int result=1;f.Shown+=async delegate{try{result=await f.TestDownload(args[1],args[2],args[3])?0:1;}finally{f.Close();}};Application.Run(f);return result;}Application.Run(f);}return 0;
  }
 }
}
