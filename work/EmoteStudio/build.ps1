param([string]$OutputRoot=(Join-Path $PSScriptRoot '../../outputs/porter-studio-build-beta0.10.0'),[string]$Toolchain='C:/winlibs/mingw64/bin')
& (Join-Path $PSScriptRoot '../build-porter-studio.ps1') -OutputRoot $OutputRoot -Toolchain $Toolchain
