param([string]$OutputRoot=(Join-Path $PSScriptRoot '../../outputs/emote-studio'),[string]$Toolchain='C:/winlibs/mingw64/bin')
$ErrorActionPreference='Stop'
$version='beta 0.0.1'
$appSource=$PSScriptRoot
$work=Split-Path $PSScriptRoot -Parent
$publish=Join-Path $OutputRoot 'package'
New-Item -ItemType Directory -Force $publish | Out-Null
dotnet publish (Join-Path $appSource 'EmoteStudio.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $publish 'app')
if($LASTEXITCODE){throw 'App publish failed.'}
Copy-Item (Join-Path $appSource 'README.md'),(Join-Path $appSource 'CHANGELOG.md') (Join-Path $publish 'app')
Push-Location (Join-Path $work 'EmoteStudioLauncher')
try {
 & (Join-Path $Toolchain 'windres.exe') app.rc -O coff -o app.res
 if($LASTEXITCODE){throw 'Launcher resource build failed.'}
 & (Join-Path $Toolchain 'g++.exe') main.cpp app.res -municode -mwindows -static -static-libgcc -static-libstdc++ -O2 -o (Join-Path ([IO.Path]::GetFullPath($publish)) "oozys-Emote-Studio ($version).exe")
 if($LASTEXITCODE){throw 'Launcher build failed.'}
} finally {Pop-Location}
$payload=Join-Path $work 'EmoteStudioInstaller/FortniteInstaller.payload.zip'
if(Test-Path -LiteralPath $payload){Remove-Item -LiteralPath $payload}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory([IO.Path]::GetFullPath($publish),$payload)
dotnet publish (Join-Path $work 'EmoteStudioInstaller/Installer.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $OutputRoot 'installer')
if($LASTEXITCODE){throw 'Installer publish failed.'}
Write-Host "Built Emote Studio $version in $OutputRoot"
