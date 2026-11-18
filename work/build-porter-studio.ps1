param([string]$OutputRoot=(Join-Path $PSScriptRoot '../outputs/porter-studio-build'),[string]$Toolchain='C:/winlibs/mingw64/bin')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
$package=Join-Path $OutputRoot 'package'
New-Item -ItemType Directory -Force $package | Out-Null
dotnet publish (Join-Path $PSScriptRoot 'FortniteObj/Porter.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $package 'app')
if($LASTEXITCODE){throw 'Application publish failed.'}
New-Item -ItemType Directory -Force (Join-Path $package 'app/licenses') | Out-Null
Copy-Item (Join-Path $root 'recovery/licenses/*') (Join-Path $package 'app/licenses') -Force
Copy-Item (Join-Path $root 'docs/releases/beta-0.9.0.md') (Join-Path $package 'app/RELEASE-NOTES.md')
Push-Location (Join-Path $PSScriptRoot 'PorterStudioLauncher')
try {
 & (Join-Path $Toolchain 'windres.exe') app.rc -O coff -o app.res
 if($LASTEXITCODE){throw 'Launcher resource compilation failed.'}
 & (Join-Path $Toolchain 'g++.exe') main.cpp app.res -municode -mwindows -static -static-libgcc -static-libstdc++ -O2 -o (Join-Path $package 'oozys-Porter-Studio (beta 0.9.0).exe')
 if($LASTEXITCODE){throw 'Launcher compilation failed.'}
}finally{Pop-Location}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$helper=Join-Path $package 'app/PorterEmoteTools.zip'
if(Test-Path -LiteralPath $helper){Remove-Item -LiteralPath $helper}
$helperZip=[IO.Compression.ZipFile]::Open($helper,[IO.Compression.ZipArchiveMode]::Create)
try {
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($helperZip,(Join-Path $PSScriptRoot 'FortniteObj/FortniteObj.blender_emote_tools.py'),'porter_emote_tools/__init__.py') | Out-Null
 [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($helperZip,(Join-Path $PSScriptRoot 'FortniteObj/FortniteObj.blender_animation.py'),'porter_emote_tools/blender_animation.py') | Out-Null
}finally{$helperZip.Dispose()}
$managed=@(Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object {[IO.Path]::GetRelativePath($package,$_.FullName).Replace('\','/')})
$managed=@($managed+'app/installation.json' | Sort-Object -Unique)
@{ProductId='oozycloud.fortnite-porter';Version='beta 0.9.0';Launcher='oozys-Porter-Studio (beta 0.9.0).exe';Files=$managed} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $package 'app/installation.json') -Encoding utf8
$payload=Join-Path $PSScriptRoot 'Installer/FortniteInstaller.payload.zip'
if(Test-Path -LiteralPath $payload){Remove-Item -LiteralPath $payload}
[IO.Compression.ZipFile]::CreateFromDirectory($package,$payload)
dotnet publish (Join-Path $PSScriptRoot 'Installer/Installer.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $OutputRoot 'installer')
if($LASTEXITCODE){throw 'Installer publish failed.'}
Write-Host "Built oozy's porter studio beta 0.9.0 in $OutputRoot"
