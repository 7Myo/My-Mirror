$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $PSScriptRoot 'MyMirror.cs'
$setup = Join-Path $PSScriptRoot 'ReceiverSetup.cs'
$icon = Join-Path $PSScriptRoot 'MyMirror.ico'
$exe = Join-Path $PSScriptRoot 'MyMirror.exe'
& $csc /nologo /target:winexe /platform:x64 /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll "/win32icon:$icon" "/out:$exe" $source $setup
if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation ($LASTEXITCODE)." }
