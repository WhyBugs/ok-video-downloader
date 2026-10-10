using System;using System.IO;using System.Threading;using System.Threading.Tasks;using OkVideo;
class Integration {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static EngineResult Download(string url,DownloadOptions o){using(var s=new EngineSession()){var r=s.RunAsync(url,o,Console.WriteLine).GetAwaiter().GetResult();Assert(r.ExitCode==0,"yt-dlp failed: "+r.Error);return r;}}
 static int Main(string[] args){try{string folder=Path.GetFullPath(args[0]),baseUrl="http://127.0.0.1:39721";
  var p=Download(baseUrl+"/playlist",new DownloadOptions{Folder=Path.Combine(folder,"playlist"),Playlist=true,PlaylistItems="1,2"});Assert(p.Files.Count==2,"Playlist must save two files");
  var audio=Download(baseUrl+"/single",new DownloadOptions{Folder=Path.Combine(folder,"audio"),Mode="mp3",AudioQuality="192K"});Assert(audio.Files.Count==1&&audio.Files[0].EndsWith(".mp3"),"Audio conversion failed");
  var sub=Download(baseUrl+"/single",new DownloadOptions{Folder=Path.Combine(folder,"subtitles"),Subtitles=true,EmbedSubtitles=true,Metadata=true,Thumbnail=true,InfoJson=true});Assert(sub.Files.Count==1,"Subtitle video failed");Assert(Directory.GetFiles(Path.Combine(folder,"subtitles"),"*.vtt").Length==1,"Subtitle file missing");Assert(Directory.GetFiles(Path.Combine(folder,"subtitles"),"*.info.json").Length==1,"Metadata JSON missing");Assert(Directory.GetFiles(Path.Combine(folder,"subtitles"),"*.jpg").Length==1,"Thumbnail missing");
  var section=Download(baseUrl+"/single",new DownloadOptions{Folder=Path.Combine(folder,"section"),ExtraArguments="--download-sections \"*0-1\" --force-keyframes-at-cuts"});Assert(section.Files.Count==1,"Advanced section download failed");
  var saved=new DownloadOptions{Folder=Path.Combine(folder,"archive"),Archive=true};Download(baseUrl+"/single",saved);var skipped=Download(baseUrl+"/single",saved);Assert(skipped.Files.Count==0,"Download archive did not skip duplicate");
  using(var s=new EngineSession()){var task=s.RunAsync(baseUrl+"/slow.mp4",new DownloadOptions{Folder=Path.Combine(folder,"cancel")},Console.WriteLine);Thread.Sleep(900);s.Cancel();Assert(task.Wait(7000),"Session cancellation hung");Assert(task.Result.Cancelled,"Session must be cancelled");}
  Console.WriteLine("PASS: real playlist, MP3, subtitles, metadata, thumbnail, custom yt-dlp argument, archive and cancellation");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
