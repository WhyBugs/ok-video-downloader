using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

static class Setup {
 static string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","OkVideo");
 static string menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),"Programs","ОК Видео");
 static string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"ОК Видео.lnk");
 const string key=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OkVideo";
 static void Extract(string path){Directory.CreateDirectory(path);using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))using(var archive=new ZipArchive(input,ZipArchiveMode.Read)){foreach(var entry in archive.Entries){string full=Path.GetFullPath(Path.Combine(path,entry.FullName));if(!full.StartsWith(Path.GetFullPath(path)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Некорректный путь в установщике.");Directory.CreateDirectory(Path.GetDirectoryName(full));if(entry.Name.Length>0)entry.ExtractToFile(full,true);}}}
 static void Shortcut(string path,string executable){Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic shortcut=shell.CreateShortcut(path);shortcut.TargetPath=executable;shortcut.WorkingDirectory=target;shortcut.Description="Скачать видео из Одноклассников в MP4";shortcut.Save();System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
 static void Install(bool onDesktop){
  Extract(target);Directory.CreateDirectory(menu);Shortcut(Path.Combine(menu,"ОК Видео.lnk"),Path.Combine(target,"OkVideo.exe"));if(onDesktop)Shortcut(desktop,Path.Combine(target,"OkVideo.exe"));
  using(var r=Registry.CurrentUser.CreateSubKey(key)){r.SetValue("DisplayName","ОК Видео");r.SetValue("DisplayVersion","1.0.0");r.SetValue("Publisher","ОК Видео");r.SetValue("InstallLocation",target);r.SetValue("DisplayIcon",Path.Combine(target,"OkVideo.exe"));r.SetValue("UninstallString","\""+Path.Combine(target,"Uninstall.exe")+"\"");r.SetValue("NoModify",1);r.SetValue("NoRepair",1);}
 }
 static void Uninstall(){
  if(MessageBox.Show("Удалить ОК Видео? Скачанные ролики сохранятся.","ОК Видео",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  try{
   foreach(string file in new[]{"OkVideo.exe","README.txt","THIRD-PARTY.txt","LICENSE.txt","tools\\yt-dlp.exe","tools\\ffmpeg.exe","tools\\ffprobe.exe"}){string full=Path.Combine(target,file);if(File.Exists(full))File.Delete(full);}
   string toolDir=Path.Combine(target,"tools");if(Directory.Exists(toolDir)&&Directory.GetFileSystemEntries(toolDir).Length==0)Directory.Delete(toolDir);
   if(File.Exists(desktop))File.Delete(desktop);string shortcut=Path.Combine(menu,"ОК Видео.lnk");if(File.Exists(shortcut))File.Delete(shortcut);if(Directory.Exists(menu)&&Directory.GetFileSystemEntries(menu).Length==0)Directory.Delete(menu);Registry.CurrentUser.DeleteSubKeyTree(key,false);
   // Schedule only this uninstaller for removal, without recursively deleting a folder.
   string self=Assembly.GetExecutingAssembly().Location;
   string command="$p='"+self.Replace("'","''")+"'; while (Get-Process -Id "+Process.GetCurrentProcess().Id+" -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 500 }; Remove-Item -LiteralPath $p -Force; $d=[IO.Path]::GetDirectoryName($p); if ((Get-ChildItem -LiteralPath $d -Force | Measure-Object).Count -eq 0) { Remove-Item -LiteralPath $d }";
   string encoded=Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
   Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -WindowStyle Hidden -EncodedCommand "+encoded){UseShellExecute=false,CreateNoWindow=true});
   MessageBox.Show("Приложение удалено. Скачанные видео сохранены.","ОК Видео");
  }catch(Exception e){MessageBox.Show("Не удалось удалить приложение. Закройте ОК Видео и повторите.\n\n"+e.Message,"ОК Видео");}
 }
 [STAThread]static int Main(string[] args){
  Application.EnableVisualStyles();
#if UNINSTALLER
  Uninstall();return 0;
#else
  if(args.Length==2&&args[0]=="--extract-only"){try{Extract(Path.GetFullPath(args[1]));return 0;}catch{return 1;}}
  using(var f=new Form{Text="Установка ОК Видео",ClientSize=new Size(520,260),StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,Font=new Font("Segoe UI",10)}){
   var title=new Label{Text="ОК Видео",Location=new Point(24,20),AutoSize=true,Font=new Font("Segoe UI",22,FontStyle.Bold),ForeColor=Color.FromArgb(238,119,22)};
   var description=new Label{Text="Скачивайте видео из Одноклассников в MP4.\nУстановка для текущего пользователя, без прав администратора.",Location=new Point(24,76),Size=new Size(474,50)};
   var location=new Label{Text="Папка: "+target,Location=new Point(24,134),Size=new Size(474,42),Font=new Font("Segoe UI",8)};
   var check=new CheckBox{Text="Создать ярлык на рабочем столе",Location=new Point(24,176),Size=new Size(400,26),Checked=true};
   var button=new Button{Text="Установить",Location=new Point(310,214),Size=new Size(184,34)};
   button.Click+=delegate{button.Enabled=false;button.Text="Установка…";f.Refresh();try{Install(check.Checked);MessageBox.Show(f,"Готово! Приложение установлено.","ОК Видео");Process.Start(Path.Combine(target,"OkVideo.exe"));f.Close();}catch(Exception e){MessageBox.Show(f,"Не удалось установить. Если ОК Видео открыто, закройте его и повторите.\n\n"+e.Message,"Ошибка");button.Enabled=true;button.Text="Установить";}};
   f.Controls.AddRange(new Control[]{title,description,location,check,button});Application.Run(f);
  }return 0;
#endif
 }
}
