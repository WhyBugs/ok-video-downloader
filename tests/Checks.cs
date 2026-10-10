using System;
using System.Diagnostics;
using OkVideo;
class Checks {
 static void Equal(string actual,string expected){if(actual!=expected)throw new Exception(actual+" != "+expected);}
 static int Main(){try{
  Equal(Downloader.Normalize("https://m.ok.ru/video/123?fromTime=4"),"https://ok.ru/video/123");
  Equal(Downloader.Normalize("ok.ru/videoembed/456"),"https://ok.ru/video/456");
  Equal(Downloader.Normalize("https://odnoklassniki.ru/dk?st.cmd=movie&st.mvId=789"),"https://ok.ru/video/789");
  Equal(Downloader.Normalize("https://yandex.ru/video/preview/5111608093787682592"),"https://yandex.ru/video/preview/5111608093787682592");
  Equal(Downloader.ExtractYandexSource("<a class='Link VideoViewer-SourceLink' href='http://ok.ru/video/6170427722461'>ОК</a>"),"https://ok.ru/video/6170427722461");
  Equal(Downloader.ExtractYandexSource("<a class='Link VideoViewer-SourcePathLink' href='http://ok.ru/video/6170427722461'>ОК</a>"),"https://ok.ru/video/6170427722461");
  bool wrong=false;try{Downloader.ExtractYandexSource("<a href='https://ok.ru/video/999'>Рекомендация</a>");}catch(ArgumentException){wrong=true;}if(!wrong)throw new Exception("Used a recommendation as source");
  foreach(string url in new[]{"https://ok.ru.evil.example/video/1","https://ok.ru@evil.example/video/1","https://ok.ru/group/123","file:///ok.ru/video/1","","https://ok.ru/video/123/other"}){bool rejected=false;try{Downloader.Normalize(url);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Accepted invalid URL: "+url);}
  Equal(Downloader.Quote("C:\\"),"\"C:\\\\\"");
  Equal(Downloader.Quote("a\"b"),"\"a\\\"b\"");
  using(var p=Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -Command Start-Sleep -Seconds 60"){UseShellExecute=false,CreateNoWindow=true})){Downloader.Cancel(p);if(!p.WaitForExit(5000))throw new Exception("Cancellation failed");}
  Console.WriteLine("PASS: links, invalid inputs, argument quoting, process cancellation");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
