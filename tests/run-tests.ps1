param([switch]$Integration)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compilerPath /nologo /out:dist\portable\Checks.exe /reference:dist\portable\OkVideo.exe /reference:System.Web.Extensions.dll tests\Checks.cs
if ($LASTEXITCODE -ne 0) { throw 'Checks compilation failed' }
& .\dist\portable\Checks.exe
if ($LASTEXITCODE -ne 0) { throw 'Checks failed' }
if (-not $Integration) { exit 0 }
New-Item -ItemType Directory -Force tests\fixtures,tests\output-integration | Out-Null
& .\tools\ffmpeg.exe -hide_banner -loglevel error -y -f lavfi -i 'color=c=blue:s=160x90:r=25' -f lavfi -i 'sine=frequency=440:sample_rate=44100' -t 2 -c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac tests\fixtures\one.mp4
if ($LASTEXITCODE -ne 0) { throw 'Fixture video failed' }
Copy-Item tests\fixtures\one.mp4 tests\fixtures\two.mp4 -Force
& .\tools\ffmpeg.exe -hide_banner -loglevel error -y -f lavfi -i 'color=c=orange:s=160x90' -frames:v 1 -update 1 tests\fixtures\cover.jpg
if ($LASTEXITCODE -ne 0) { throw 'Fixture thumbnail failed' }
[IO.File]::WriteAllText((Join-Path $PWD 'tests\fixtures\ru.vtt'), "WEBVTT`n`n00:00:00.000 --> 00:00:01.500`nFixture subtitle`n", [Text.Encoding]::UTF8)
& $compilerPath /nologo /out:tests\FixtureServer.exe tests\FixtureServer.cs
if ($LASTEXITCODE -ne 0) { throw 'Fixture server compilation failed' }
& $compilerPath /nologo /out:dist\portable\Integration.exe /reference:dist\portable\OkVideo.exe tests\Integration.cs
if ($LASTEXITCODE -ne 0) { throw 'Integration compilation failed' }
$serverProcess = Start-Process tests\FixtureServer.exe -ArgumentList ('"' + (Join-Path $PWD 'tests\fixtures') + '"') -PassThru -WindowStyle Hidden
try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try { $tcpCheck = New-Object Net.Sockets.TcpClient; $tcpCheck.Connect('127.0.0.1',39721); $tcpCheck.Dispose(); $ready = $true; break } catch { Start-Sleep -Milliseconds 100 }
    }
    if (-not $ready) { throw 'Fixture server did not start' }
    & .\dist\portable\Integration.exe (Join-Path $PWD 'tests\output-integration')
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed' }
    & .\tools\ffprobe.exe -v error -show_entries 'stream=codec_type,codec_name' -of json (Get-ChildItem tests\output-integration\subtitles -Filter '*.mp4' | Select-Object -First 1).FullName
    if ($LASTEXITCODE -ne 0) { throw 'Subtitle probe failed' }
} finally {
    if (-not $serverProcess.HasExited) { Stop-Process -Id $serverProcess.Id -Force }
    Remove-Item -LiteralPath 'dist\portable\Checks.exe','dist\portable\Integration.exe' -ErrorAction SilentlyContinue
}
