using System;using System.Linq;using System.Collections.Generic;using System.Diagnostics;using System.IO;using OkVideo;
class Checks {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Equal(string actual,string expected){Assert(actual==expected,actual+" != "+expected);}
 static int Main(){try{
  Equal(Downloader.Normalize("https://m.ok.ru/video/123?fromTime=4"),"https://ok.ru/video/123");
  Equal(Downloader.Normalize("ok.ru/videoembed/456"),"https://ok.ru/video/456");
  Equal(Downloader.Normalize("https://odnoklassniki.ru/dk?st.cmd=movie&st.mvId=789"),"https://ok.ru/video/789");
  Equal(Downloader.Normalize("https://www.youtube.com/watch?v=abc&list=xyz"),"https://www.youtube.com/watch?v=abc&list=xyz");
  Equal(Downloader.Normalize("https://vimeo.com/123"),"https://vimeo.com/123");
  Equal(Downloader.Normalize("https://yandex.ru/video/preview/5111608093787682592"),"https://yandex.ru/video/preview/5111608093787682592");
  Equal(Downloader.ExtractYandexSource("<a class='Link VideoViewer-SourcePathLink' href='http://ok.ru/video/6170427722461'>ОК</a>"),"https://ok.ru/video/6170427722461");
  Equal(Downloader.ExtractYandexSource("<a class='Link VideoViewer-SourceLink' href='https://www.youtube.com/watch?v=abc&amp;list=xyz'>YouTube</a>"),"https://www.youtube.com/watch?v=abc&list=xyz");
  foreach(string url in new[]{"file:///ok.ru/video/1","javascript:alert(1)","","https://example.com/\n--exec"}){bool rejected=false;try{Downloader.Normalize(url);}catch(ArgumentException){rejected=true;}Assert(rejected,"Accepted invalid URL: "+url);}
  bool wrong=false;try{Downloader.ExtractYandexSource("<a href='https://ok.ru/video/999'>Рекомендация</a>");}catch(ArgumentException){wrong=true;}Assert(wrong,"Used a recommendation as source");
  var values=new[]{"C:\\","a\"b","C:\\Папка с пробелами\\","","one\\\"two"};
  var parsed=Downloader.ParseArguments(Downloader.CommandLine(values));Assert(values.SequenceEqual(parsed),"Windows argument quoting did not round-trip");
  var options=new DownloadOptions{Mode="mp3",Playlist=true,PlaylistItems="1-3",ExtraArguments="--download-sections \"*0-5\" --limit-rate 2M"};
  var clone=options.Clone();options.Mode="mkv";Equal(clone.Mode,"mp3");
  var saved=Json.Serializer().Deserialize<DownloadOptions>(Json.Serializer().Serialize(clone));Equal(saved.Mode,"mp3");Equal(saved.PlaylistItems,"1-3");
  var args=Downloader.BuildArguments(saved);Assert(args.Contains("-x")&&args.Contains("--yes-playlist")&&args.Contains("*0-5"),"Audio / playlist / advanced options lost");
  var raw=Downloader.BuildArguments(new DownloadOptions{RawOptions=true,ExtraArguments="-f best -P C:\\Temp --simulate"});Assert(!raw.Contains("--recode-video")&&!raw.Contains("--no-playlist")&&raw.Contains("--simulate"),"Raw options were overridden");
  using(var own=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -Command Start-Sleep -Seconds 60"){UseShellExecute=false,CreateNoWindow=true})){Downloader.Cancel(own);Assert(own.WaitForExit(5000),"Cancellation failed");}
  Console.WriteLine("PASS: URLs, Yandex source, Windows argument round-trip, option snapshots, CLI parameters, cancellation");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
