$ErrorActionPreference='Stop'
$root=(Get-Location).Path
$installer=Join-Path $root 'work\installer-beta0.7.1\oozys-Fortnite-Porter-Installer (beta 0.7.1).exe'
$payload=Join-Path $root 'work\publish-beta0.7.1'
$qa=Join-Path $root ('recovery\package-check-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $qa | Out-Null
$checks=[System.Collections.Generic.List[string]]::new()
function Check($ok,$name){if(-not $ok){throw $name};$checks.Add($name);Write-Output "PASS $name"}
function Install($path){$p=Start-Process -FilePath $installer -ArgumentList @('--extract-to',('"'+$path+'"')) -WindowStyle Hidden -Wait -PassThru;Check ($p.ExitCode -eq 0) 'Installer completed'}
function Hashes($path){foreach($f in Get-ChildItem -LiteralPath $payload -Recurse -File){$rel=[IO.Path]::GetRelativePath($payload,$f.FullName);if($rel -eq 'app\installation.json'){continue};if((Get-FileHash -LiteralPath $f.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $path $rel)).Hash){throw "Payload mismatch: $rel"}};Check $true 'All payload hashes match'}
$fresh=Join-Path $qa 'fresh install ü'
$env:DOTNET_ROOT='C:\no-external-runtime';$env:DOTNET_MULTILEVEL_LOOKUP='0'
Install $fresh
Hashes $fresh
$launcher=Join-Path $fresh 'oozys-Fortnite-Porter (beta 0.7.1).exe'
$p=Start-Process -FilePath $launcher -ArgumentList @('--ui-check',('"'+(Join-Path $qa 'app-ui')+'"')) -WindowStyle Hidden -Wait -PassThru
Check ($p.ExitCode -eq 0 -and (Test-Path (Join-Path $qa 'app-ui\result.json'))) 'Packaged app UI and saved format controls pass without external runtime'
Check ((Get-Item -LiteralPath $launcher).VersionInfo.FileVersion -eq 'beta 0.7.1') 'Launcher version metadata matches'
Set-Content -LiteralPath (Join-Path $fresh 'my-export.blend') -Value 'preserve'
Install $fresh
Hashes $fresh
Check ((Get-Content -LiteralPath (Join-Path $fresh 'my-export.blend')).Trim() -eq 'preserve') 'Repair preserves user files'
$old=Join-Path $qa 'previous version'
New-Item -ItemType Directory -Path $old | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'outputs\oozys-Fortnite-Porter (beta 0.4.4)\app') -Destination $old -Recurse
Copy-Item -LiteralPath (Join-Path $root 'outputs\oozys-Fortnite-Porter (beta 0.4.4)\oozys-Fortnite-Porter (beta 0.4.4).exe') -Destination $old
Install $old
Hashes $old
Check (-not (Test-Path -LiteralPath (Join-Path $old 'oozys-Fortnite-Porter (beta 0.4.4).exe'))) 'Update removes previous managed launcher'
$p=Start-Process -FilePath $installer -ArgumentList @('--ui-check',('"'+(Join-Path $qa 'installer-ui')+'"')) -WindowStyle Hidden -Wait -PassThru
Check ($p.ExitCode -eq 0) 'Installer UI location detection and invalid-path checks pass'
@{Root=$qa;Passed=$checks;Count=$checks.Count}|ConvertTo-Json -Depth 4|Set-Content recovery/package-results.json -Encoding utf8
