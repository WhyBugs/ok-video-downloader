$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force 'dist\portable\tools' | Out-Null
& $compilerPath /nologo /target:winexe /out:dist\portable\OkVideo.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll src\Program.cs src\Engine.cs src\MainForm.cs
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
& $compilerPath /nologo /target:winexe /define:UNINSTALLER /out:dist\portable\Uninstall.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll src\Setup.cs
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed' }
Copy-Item 'tools\yt-dlp.exe','tools\ffmpeg.exe','tools\ffprobe.exe','tools\deno.exe' 'dist\portable\tools'
Copy-Item 'README.txt','THIRD-PARTY.txt','LICENSE.txt','DENO-LICENSE.txt','YTDLP-LICENSES.txt' 'dist\portable'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payloadPath = Join-Path $PSScriptRoot 'dist\payload.zip'
if (Test-Path $payloadPath) { Remove-Item -LiteralPath $payloadPath }
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $PSScriptRoot 'dist\portable'),$payloadPath,[IO.Compression.CompressionLevel]::Optimal,$false)
& $compilerPath /nologo /target:winexe /out:dist\OkVideo-Setup.exe /resource:dist\payload.zip,payload.zip /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll src\Setup.cs
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
Get-Item 'dist\OkVideo-Setup.exe' | Select-Object FullName,Length
