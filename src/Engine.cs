using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace OkVideo {
 public sealed class DownloadOptions {
  public string Folder { get; set; }
  public string Mode { get; set; }
  public int Height { get; set; }
  public string AudioQuality { get; set; }
  public string Format { get; set; }
  public bool Playlist { get; set; }
  public string PlaylistItems { get; set; }
  public bool Subtitles { get; set; }
  public bool AutoSubtitles { get; set; }
  public bool EmbedSubtitles { get; set; }
  public string SubtitleLanguages { get; set; }
  public bool Thumbnail { get; set; }
  public bool EmbedThumbnail { get; set; }
  public bool Metadata { get; set; }
  public bool InfoJson { get; set; }
  public bool Archive { get; set; }
  public string OutputTemplate { get; set; }
  public string CookieBrowser { get; set; }
  public string CookieFile { get; set; }
  public string Proxy { get; set; }
  public string RateLimit { get; set; }
  public int Fragments { get; set; }
  public string ExtraArguments { get; set; }
  public bool RawOptions { get; set; }
  public DownloadOptions() {
   Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","DLP Видео");
   Mode="mp4";Height=0;AudioQuality="0";Format="";PlaylistItems="";SubtitleLanguages="ru,en";
   OutputTemplate="";CookieBrowser="";CookieFile="";Proxy="";RateLimit="";Fragments=3;ExtraArguments="";
  }
  public DownloadOptions Clone() { return (DownloadOptions)MemberwiseClone(); }
 }
 public sealed class DownloadJob {
  public string Url { get; set; }
  public string Title { get; set; }
  public string State { get; set; }
  public string Detail { get; set; }
  public DownloadOptions Options { get; set; }
  public List<string> Files { get; set; }
  public DownloadJob() { State="Ожидание";Detail="";Files=new List<string>(); }
 }
 public sealed class EngineResult {
  public int ExitCode;
  public bool Cancelled;
  public string Error;
  public readonly List<string> Files=new List<string>();
  public readonly StringBuilder Output=new StringBuilder();
 }
 public static class Json {
  public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer {MaxJsonLength=int.MaxValue,RecursionLimit=200}; }
  public static string Text(IDictionary<string,object> data,string key,string fallback="") {
   object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?Convert.ToString(value,CultureInfo.InvariantCulture):fallback;
  }
 }
 public static class Downloader {
  public const string Version="2.0.0";
  public static string Tools { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools"); } }
  public static string Normalize(string value) {
   if(string.IsNullOrWhiteSpace(value))throw new ArgumentException("Вставьте ссылку на видео или плейлист.");
   value=value.Trim();if(!value.Contains("://"))value="https://"+value;
   Uri u;if(!Uri.TryCreate(value,UriKind.Absolute,out u)||(u.Scheme!="https"&&u.Scheme!="http")||string.IsNullOrEmpty(u.Host)||value.IndexOfAny(new[]{'\r','\n','\0'})>=0)
    throw new ArgumentException("Нужна ссылка, начинающаяся с https:// или http://.");
   string host=u.Host.ToLowerInvariant();
   if(IsYandex(host)) {
    Match m=Regex.Match(u.AbsolutePath,@"^/video/(?:preview|touch/preview)/(\d+)/?$");
    if(m.Success)return "https://"+host+"/video/preview/"+m.Groups[1].Value;
   }
   if(Regex.IsMatch(host,@"^(?:(?:www|m)\.)?(?:ok|odnoklassniki)\.ru$")) {
    Match m=Regex.Match(u.AbsolutePath,@"^/(?:video|videoembed|web-api/video/moviePlayer)/([\d-]+)/?$");
    if(!m.Success&&u.AbsolutePath=="/dk")m=Regex.Match(u.Query,@"(?:[?&])st\.mvId=([\d-]+)(?:&|$)");
    if(m.Success)return "https://ok.ru/video/"+m.Groups[1].Value;
   }
   return value;
  }
  static bool IsYandex(string host) { return Regex.IsMatch(host,@"^(?:www\.)?yandex\.(?:ru|com|by|kz|uz)$"); }
  public static string ExtractYandexSource(string html) {
   Match m=Regex.Match(html,@"<a\b(?=[^>]*\bclass=[""'][^""']*VideoViewer-Source(?:Path)?Link\b[^""']*[""'])[^>]*\bhref=[""'](?<url>[^""']+)[""']",RegexOptions.IgnoreCase);
   if(!m.Success)throw new ArgumentException("Яндекс не показал источник видео. Скопируйте прямую ссылку с сайта, на котором находится ролик.");
   string url=Normalize(WebUtility.HtmlDecode(m.Groups["url"].Value));
   if(IsYandex(new Uri(url).Host))throw new ArgumentException("Вместо страницы поиска нужна ссылка на исходное видео.");
   return url;
  }
  public static string Resolve(string url,CancellationToken token) {
   url=Normalize(url);Uri u=new Uri(url);
   if(!IsYandex(u.Host)||!Regex.IsMatch(u.AbsolutePath,@"^/video/preview/\d+$"))return url;
   token.ThrowIfCancellationRequested();ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(url);request.Timeout=30000;request.ReadWriteTimeout=30000;
   request.UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0 Safari/537.36";
   request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
   try {using(token.Register(request.Abort))using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return ExtractYandexSource(reader.ReadToEnd());}
   catch(WebException){token.ThrowIfCancellationRequested();throw;}
  }
  public static string Quote(string value) {
   var b=new StringBuilder("\"");int slashes=0;
   foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}
   b.Append('\\',slashes*2);b.Append('"');return b.ToString();
  }
  [DllImport("shell32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern IntPtr CommandLineToArgvW(string command,out int count);
  [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr ptr);
  public static List<string> ParseArguments(string text) {
   if(string.IsNullOrWhiteSpace(text))return new List<string>();
   if(text.Contains("\0"))throw new ArgumentException("В параметрах есть недопустимый символ.");
   int count;IntPtr ptr=CommandLineToArgvW("yt-dlp "+text.Replace('\r',' ').Replace('\n',' '),out count);
   if(ptr==IntPtr.Zero)throw new ArgumentException("Не удалось прочитать параметры yt-dlp.");
   try {var args=new List<string>();for(int i=1;i<count;i++)args.Add(Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptr,i*IntPtr.Size)));return args;}finally{LocalFree(ptr);}
  }
  public static string CommandLine(IEnumerable<string> args) { return string.Join(" ",args.Select(Quote)); }
  static List<string> RuntimeArgs() {
   return new List<string>{"--ignore-config","--encoding","utf-8","--ffmpeg-location",Tools,"--js-runtimes","deno:"+Path.Combine(Tools,"deno.exe")};
  }
  static void Add(List<string> args,string option,string value) { if(!string.IsNullOrWhiteSpace(value)){args.Add(option);args.Add(value.Trim());} }
  static void Access(List<string> args,DownloadOptions o) {
   if(!string.IsNullOrWhiteSpace(o.CookieFile))Add(args,"--cookies",o.CookieFile);
   else Add(args,"--cookies-from-browser",o.CookieBrowser);
   Add(args,"--proxy",o.Proxy);Add(args,"--limit-rate",o.RateLimit);
  }
  public static string VideoFormat(int height,string container) {
   string cap=height==0?"":"[height<="+height+"]";
   string native=height==0?"":"/b[ext=mp4][format_id~='^("+(height>=1080?"full|":"")+(height>=720?"hd|":"")+"sd|low|lowest|mobile)$']";
   if(container=="mp4")return "bv*[ext=mp4]"+cap+"+ba[ext=m4a]/b[ext=mp4]"+cap+native+"/bv*"+cap+"+ba/b"+cap;
   return "bv*"+cap+"+ba/b"+cap+native;
  }
  public static List<string> BuildArguments(DownloadOptions o,bool preview=false) {
   if(string.IsNullOrWhiteSpace(o.Folder))throw new ArgumentException("Выберите папку для сохранения.");
   if(o.Fragments<1||o.Fragments>16)throw new ArgumentException("Количество потоков: от 1 до 16.");
   var args=RuntimeArgs();args.AddRange(new[]{"--no-colors","--newline","--socket-timeout","30","--retries","3","--fragment-retries","3"});
   Access(args,o);
   if(preview) {
    args.AddRange(new[]{"--skip-download","--dump-single-json",o.Playlist?"--yes-playlist":"--no-playlist"});
    if(o.Playlist)args.Add("--flat-playlist");
    Add(args,"--playlist-items",o.PlaylistItems);
    args.AddRange(ParseArguments(o.ExtraArguments));args.AddRange(new[]{"--skip-download","--simulate","--dump-single-json"});return args;
   }
   args.AddRange(new[]{"--no-simulate","--no-quiet","--progress","--windows-filenames","--continue","--no-overwrites","--progress-delta","0.3",
    "--progress-template","download:__DLP_PROGRESS__%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s|%(info.playlist_index)s|%(info.n_entries)s",
    "--print","after_move:__DLP_FILE__%(filepath)j"});
   if(!o.RawOptions) {
    args.Add(o.Playlist?"--yes-playlist":"--no-playlist");Add(args,"--playlist-items",o.PlaylistItems);
    args.AddRange(new[]{"--concurrent-fragments",o.Fragments.ToString(CultureInfo.InvariantCulture),"-P",Path.GetFullPath(o.Folder)});
    string template=string.IsNullOrWhiteSpace(o.OutputTemplate)?(o.Playlist?"%(playlist_title,playlist_id|Плейлист)s/%(playlist_index)03d - %(title).150B [%(id)s].%(ext)s":"%(title).150B [%(id)s].%(ext)s"):o.OutputTemplate;
    Add(args,"-o",template);
    bool audio=IsAudio(o.Mode);
    Add(args,"-f",string.IsNullOrWhiteSpace(o.Format)?(audio?"ba/b":VideoFormat(o.Height,o.Mode)):o.Format);
    if(audio){args.Add("-x");Add(args,"--audio-format",o.Mode);Add(args,"--audio-quality",o.AudioQuality);}
    else if(o.Mode=="mp4"){args.AddRange(new[]{"--merge-output-format","mp4","--recode-video","mp4"});}
    else if(o.Mode=="mkv"){args.AddRange(new[]{"--merge-output-format","mkv","--remux-video","mkv"});}
    if(o.Subtitles)args.Add("--write-subs");if(o.AutoSubtitles)args.Add("--write-auto-subs");
    if(o.Subtitles||o.AutoSubtitles||o.EmbedSubtitles)Add(args,"--sub-langs",o.SubtitleLanguages);
    if(o.EmbedSubtitles)args.Add("--embed-subs");
    if(o.Thumbnail)args.Add("--write-thumbnail");if(o.EmbedThumbnail)args.Add("--embed-thumbnail");
    if(o.Metadata)args.Add("--embed-metadata");if(o.InfoJson)args.Add("--write-info-json");
    if(o.Archive)Add(args,"--download-archive",Path.Combine(Path.GetFullPath(o.Folder),".dlp-download-archive.txt"));
   }
   args.AddRange(ParseArguments(o.ExtraArguments));return args;
  }
  public static bool IsAudio(string mode) { return new[]{"mp3","m4a","opus","flac","wav"}.Contains(mode); }
  public static List<string> CommandArguments(string text) { var args=RuntimeArgs();args.AddRange(ParseArguments(text));return args; }
  public static void Cancel(Process p) {
   if(p==null)return;
   try {if(!p.HasExited)using(var killer=Process.Start(new ProcessStartInfo("taskkill.exe","/PID "+p.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true})) {
    if(!killer.WaitForExit(3000)){killer.Kill();if(!p.HasExited)p.Kill();}
   }}catch{try{if(!p.HasExited)p.Kill();}catch{}}
  }
 }
 public sealed class EngineSession : IDisposable {
  readonly CancellationTokenSource cancellation=new CancellationTokenSource();readonly object gate=new object();Process current;
  public void Cancel() { try{cancellation.Cancel();}catch(ObjectDisposedException){return;}Process own;lock(gate)own=current;Downloader.Cancel(own); }
  public Task<EngineResult> RunAsync(string url,DownloadOptions options,Action<string> line,bool preview=false) {
   return Task.Run(delegate {
    var result=new EngineResult();
    try {
     string resolved=Downloader.Resolve(url,cancellation.Token);if(resolved!=url)line("Источник: "+resolved);
     var args=Downloader.BuildArguments(options,preview);args.Add("--");args.Add(resolved);
     if(!preview&&!options.RawOptions)Directory.CreateDirectory(options.Folder);
     Execute(args,line,result,preview);
    }catch(OperationCanceledException){result.Cancelled=true;result.ExitCode=-1;}
    catch(Exception e){result.Error=e.Message;result.ExitCode=-1;line("ERROR: "+e.Message);}
    result.Cancelled=cancellation.IsCancellationRequested;return result;
   });
  }
  public Task<EngineResult> CommandAsync(string text,Action<string> line) {
   return Task.Run(delegate {var result=new EngineResult();try{Execute(Downloader.CommandArguments(text),line,result,true);}catch(Exception e){result.Error=e.Message;result.ExitCode=-1;line("ERROR: "+e.Message);}result.Cancelled=cancellation.IsCancellationRequested;return result;});
  }
  void Execute(List<string> args,Action<string> line,EngineResult result,bool collectOutput) {
   string executable=Path.Combine(Downloader.Tools,"yt-dlp.exe");if(!File.Exists(executable))throw new FileNotFoundException("Не найден yt-dlp.exe. Переустановите приложение.");
   cancellation.Token.ThrowIfCancellationRequested();
   using(var p=new Process()) {
    p.StartInfo=new ProcessStartInfo(executable,Downloader.CommandLine(args)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};
    p.StartInfo.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";
    p.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;lock(result){if(collectOutput)result.Output.AppendLine(e.Data);if(e.Data.StartsWith("__DLP_FILE__")){try{string file=Json.Serializer().Deserialize<string>(e.Data.Substring(12));if(File.Exists(file)&&!result.Files.Contains(file))result.Files.Add(file);}catch{}}}line(e.Data);};
    p.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;lock(result){if(e.Data.StartsWith("ERROR:"))result.Error=e.Data.Substring(6).Trim();}line(e.Data);};
    lock(gate){cancellation.Token.ThrowIfCancellationRequested();current=p;p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();}
    try {p.WaitForExit();result.ExitCode=p.ExitCode;}finally{lock(gate)current=null;}
   }
  }
  public void Dispose() { cancellation.Dispose(); }
 }
 public static class Storage {
  public static string DirectoryPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OkVideo");}}
  public static void Save(string name,object value){Directory.CreateDirectory(DirectoryPath);string path=Path.Combine(DirectoryPath,name),temp=path+".tmp";File.WriteAllText(temp,Json.Serializer().Serialize(value),Encoding.UTF8);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
  public static T Load<T>(string name) where T:class {try{return Json.Serializer().Deserialize<T>(File.ReadAllText(Path.Combine(DirectoryPath,name),Encoding.UTF8));}catch{return null;}}
 }
}
