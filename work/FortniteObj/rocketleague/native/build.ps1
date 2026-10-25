param([string]$BuildDirectory=(Join-Path $env:TEMP ('porter-reader-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$upstream=Join-Path $BuildDirectory 'UEViewer'
if(Test-Path -LiteralPath $upstream){throw 'Choose an empty build directory.'}
git -c core.longpaths=true clone --no-checkout https://github.com/ShinyEmii/UEViewer.git $upstream
if($LASTEXITCODE){throw 'Source clone failed'}
git -C $upstream checkout 1b6977ec1c8414acec4cbc183b50d88823adc212
if($LASTEXITCODE){throw 'Pinned source checkout failed'}
$tool=Join-Path $upstream 'Tools/PorterDump'
New-Item -ItemType Directory -Path $tool | Out-Null
foreach($name in @('Main.cpp','Build.h','CMakeLists.txt')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $tool}
$core=Join-Path $upstream 'Core/CoreWin32.cpp'
$text=[IO.File]::ReadAllText($core)
[IO.File]::WriteAllText($core,"#if defined(_WIN32) && !defined(_MSC_VER)`n#include <windows.h>`n#endif`n"+$text)
if(Test-Path -LiteralPath 'C:\winlibs\mingw64\bin\g++.exe'){$env:PATH='C:\winlibs\mingw64\bin;'+$env:PATH}
cmake -S $tool -B (Join-Path $BuildDirectory 'obj') -G 'MinGW Makefiles'
if($LASTEXITCODE){throw 'CMake configuration failed'}
cmake --build (Join-Path $BuildDirectory 'obj') -j 4
if($LASTEXITCODE){throw 'Native build failed'}
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'obj/porter-rl-dump.exe') -Destination (Join-Path $PSScriptRoot '../porter-rl-dump.exe')
