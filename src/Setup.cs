using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Threading.Tasks;

static class Setup {
 static string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","OkVideo");
 static string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","DLP Видео");
 static string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"DLP Видео.lnk");
 static string oldMenu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","ОК Видео");
 static string oldDesktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"ОК Видео.lnk");
 const string key=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OkVideo";
 static void Extract(string path){Directory.CreateDirectory(path);using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var archive=new ZipArchive(input,ZipArchiveMode.Read)){foreach(var entry in archive.Entries){string full=Path.GetFullPath(Path.Combine(path,entry.FullName));if(!full.StartsWith(Path.GetFullPath(path)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Некорректный путь в установщике.");Directory.CreateDirectory(Path.GetDirectoryName(full));if(entry.Name.Length>0)entry.ExtractToFile(full,true);}}}
 static void Shortcut(string path,string executable){Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic shortcut=shell.CreateShortcut(path);shortcut.TargetPath=executable;shortcut.WorkingDirectory=target;shortcut.Description="yt-dlp: видео, аудио, плейлисты и Одноклассники";shortcut.Save();System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
 static void RemoveLegacyShortcuts(){if(File.Exists(oldDesktop))File.Delete(oldDesktop);string link=Path.Combine(oldMenu,"ОК Видео.lnk");if(File.Exists(link))File.Delete(link);if(Directory.Exists(oldMenu)&&Directory.GetFileSystemEntries(oldMenu).Length==0)Directory.Delete(oldMenu);}
 static void Install(bool onDesktop){
  string app=Path.Combine(target,"OkVideo.exe");if(File.Exists(app))using(var check=new FileStream(app,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
  Extract(target);Directory.CreateDirectory(menu);Shortcut(Path.Combine(menu,"DLP Видео.lnk"),app);if(onDesktop)Shortcut(desktop,app);RemoveLegacyShortcuts();
  using(var r=Registry.CurrentUser.CreateSubKey(key)){r.SetValue("DisplayName","DLP Видео (yt-dlp)");r.SetValue("DisplayVersion","2.0.0");r.SetValue("Publisher","WhyBugs");r.SetValue("InstallLocation",target);r.SetValue("DisplayIcon",app);r.SetValue("UninstallString","\""+Path.Combine(target,"Uninstall.exe")+"\"");r.SetValue("NoModify",1);r.SetValue("NoRepair",1);}
 }
 static void Uninstall(){
  if(MessageBox.Show("Удалить DLP Видео? Скачанные файлы сохранятся.","DLP Видео",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  try{
   foreach(string file in new[]{"OkVideo.exe","README.txt","THIRD-PARTY.txt","LICENSE.txt","DENO-LICENSE.txt","YTDLP-LICENSES.txt","tools\\yt-dlp.exe","tools\\ffmpeg.exe","tools\\ffprobe.exe","tools\\deno.exe","tools\\yt-dlp.exe.old"}){string full=Path.Combine(target,file);if(File.Exists(full))File.Delete(full);}
   string toolDir=Path.Combine(target,"tools");if(Directory.Exists(toolDir)&&Directory.GetFileSystemEntries(toolDir).Length==0)Directory.Delete(toolDir);
   if(File.Exists(desktop))File.Delete(desktop);string shortcut=Path.Combine(menu,"DLP Видео.lnk");if(File.Exists(shortcut))File.Delete(shortcut);if(Directory.Exists(menu)&&Directory.GetFileSystemEntries(menu).Length==0)Directory.Delete(menu);RemoveLegacyShortcuts();Registry.CurrentUser.DeleteSubKeyTree(key,false);
   // Schedule only this uninstaller for removal, without recursively deleting a folder.
   string self=Assembly.GetExecutingAssembly().Location;
   string command="$p='"+self.Replace("'","''")+"'; while (Get-Process -Id "+Process.GetCurrentProcess().Id+" -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 500 }; Remove-Item -LiteralPath $p -Force; $d=[IO.Path]::GetDirectoryName($p); if ((Get-ChildItem -LiteralPath $d -Force | Measure-Object).Count -eq 0) { Remove-Item -LiteralPath $d }";
   string encoded=Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
   Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -WindowStyle Hidden -EncodedCommand "+encoded){UseShellExecute=false,CreateNoWindow=true});
   MessageBox.Show("Приложение удалено. Скачанные файлы сохранены.","DLP Видео");
  }catch(Exception e){MessageBox.Show("Не удалось удалить приложение. Закройте DLP Видео и повторите.\n\n"+e.Message,"DLP Видео");}
 }
 [STAThread]static int Main(string[] args){
  Application.EnableVisualStyles();
#if UNINSTALLER
  Uninstall();return 0;
#else
  if(args.Length==2&&args[0]=="--extract-only"){try{Extract(Path.GetFullPath(args[1]));return 0;}catch{return 1;}}
  using(var f=new Form{Text="Установка DLP Видео 2.0",ClientSize=new Size(520,260),StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,Font=new Font("Segoe UI",10)}){
   var title=new Label{Text="DLP Видео",Location=new Point(24,20),AutoSize=true,Font=new Font("Segoe UI",22,FontStyle.Bold),ForeColor=Color.FromArgb(238,119,22)};
   var description=new Label{Text="yt-dlp: видео, аудио, плейлисты и Одноклассники.\nВсе компоненты внутри. Установка без прав администратора.",Location=new Point(24,76),Size=new Size(474,50)};
   var location=new Label{Text="Папка: "+target,Location=new Point(24,134),Size=new Size(474,42),Font=new Font("Segoe UI",8)};
   var check=new CheckBox{Text="Создать ярлык на рабочем столе",Location=new Point(24,176),Size=new Size(400,26),Checked=true};
   var button=new Button{Text="Установить",Location=new Point(310,214),Size=new Size(184,34)};
   bool installing=false;f.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(installing)e.Cancel=true;};
   button.Click+=async delegate{button.Enabled=false;check.Enabled=false;button.Text="Установка…";installing=true;bool onDesktop=check.Checked;try{await Task.Run(delegate{Install(onDesktop);});installing=false;MessageBox.Show(f,"Готово! Приложение установлено.","DLP Видео");Process.Start(Path.Combine(target,"OkVideo.exe"));f.Close();}catch(Exception e){installing=false;MessageBox.Show(f,"Не удалось установить. Если DLP Видео открыто, закройте его и повторите.\n\n"+e.Message,"Ошибка");button.Enabled=check.Enabled=true;button.Text="Установить";}};
   f.Controls.AddRange(new Control[]{title,description,location,check,button});Application.Run(f);
  }return 0;
#endif
 }
}
