using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
[assembly:AssemblyVersion("2.0.0.0")]
[assembly:AssemblyFileVersion("2.0.0.0")]
namespace OkVideo {
 static class Program {
  [STAThread]static int Main(string[] args) {
   if(args.Length>=3&&args[0]=="--download") {
    var options=new DownloadOptions {Folder=args[2],Height=480};if(args.Length>3)options.Mode=args[3];if(args.Length>4)options.ExtraArguments=args[4];
    using(var session=new EngineSession()){EngineResult result=session.RunAsync(args[1],options,Console.WriteLine).GetAwaiter().GetResult();return result.ExitCode==0&&result.Files.Count>0?0:1;}
   }
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var f=new MainForm(args.Length>0)) {
    if(args.Length==2&&args[0]=="--ui-test"){f.Show();Application.DoEvents();bool ok=f.TestUI(args[1]);f.Close();return ok?0:1;}
    if(args.Length>=4&&args[0]=="--ui-download-test") {
     int result=1;f.Shown+=async delegate {try{result=await f.TestDownload(args[1],args[2],args[3],args.Length>4?args[4]:"mp4")?0:1;}finally{f.Close();}};
     Application.Run(f);return result;
    }
    Application.Run(f);return 0;
   }
  }
 }
}
