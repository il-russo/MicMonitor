# Compiles MicMonitor.exe with the C# compiler that ships with Windows.
# No Visual Studio and no .NET SDK required.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) { throw "csc.exe non trovato: $csc" }

$naudio = Join-Path $root 'build\naudio\lib\net35\NAudio.dll'
if (-not (Test-Path $naudio)) { & (Join-Path $root 'build\fetch-naudio.ps1') }

$icon = Join-Path $root 'build\MicMonitor.ico'
$output = Join-Path $root 'MicMonitor.exe'

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /warn:4 /codepage:65001 `
  "/out:$output" `
  "/win32icon:$icon" `
  "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
  /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  "/r:$naudio" `
  "/resource:$naudio,NAudio.dll" `
  (Join-Path $root 'src\MicMonitor.cs') (Join-Path $root 'src\AssemblyInfo.cs')

if ($LASTEXITCODE -ne 0) { throw "Compilazione fallita (exit $LASTEXITCODE)" }
Write-Output "OK: $output"
