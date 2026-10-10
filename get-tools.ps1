param([string]$ToolsDirectory = (Join-Path $PSScriptRoot 'tools'))
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$lock = Get-Content (Join-Path $PSScriptRoot 'tools.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $ToolsDirectory | Out-Null
function Test-ToolHash([string]$Name, [string]$Expected) {
    $toolFile = Join-Path $ToolsDirectory $Name
    return (Test-Path -LiteralPath $toolFile) -and ((Get-FileHash -LiteralPath $toolFile -Algorithm SHA256).Hash -eq $Expected)
}
if (-not (Test-ToolHash 'yt-dlp.exe' $lock.ytdlp.sha256)) {
    Invoke-WebRequest -UseBasicParsing -Uri $lock.ytdlp.url -OutFile (Join-Path $ToolsDirectory 'yt-dlp.exe')
    if (-not (Test-ToolHash 'yt-dlp.exe' $lock.ytdlp.sha256)) { throw 'SHA-256 yt-dlp не совпадает' }
}
if (-not ((Test-ToolHash 'ffmpeg.exe' $lock.ffmpeg.sha256) -and (Test-ToolHash 'ffprobe.exe' $lock.ffprobe.sha256))) {
    $archivePath = Join-Path $ToolsDirectory 'ffmpeg-build.zip'
    $unpackPath = Join-Path $ToolsDirectory 'ffmpeg-build'
    Invoke-WebRequest -UseBasicParsing -Uri $lock.ffmpeg.url -OutFile $archivePath
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $lock.ffmpeg.archiveSha256) { throw 'SHA-256 архива FFmpeg не совпадает' }
    Expand-Archive -LiteralPath $archivePath -DestinationPath $unpackPath -Force
    Get-ChildItem -LiteralPath $unpackPath -Recurse -Filter '*.exe' | Where-Object Name -In 'ffmpeg.exe','ffprobe.exe' | Copy-Item -Destination $ToolsDirectory
    if (-not ((Test-ToolHash 'ffmpeg.exe' $lock.ffmpeg.sha256) -and (Test-ToolHash 'ffprobe.exe' $lock.ffprobe.sha256))) { throw 'SHA-256 FFmpeg/FFprobe не совпадает' }
}
Write-Output 'Компоненты проверены по SHA-256.'
