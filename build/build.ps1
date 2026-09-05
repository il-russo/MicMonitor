# Compiles MicMonitor.exe with the C# compiler that ships with Windows.
# No Visual Studio and no .NET SDK required.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) { throw "csc.exe non trovato: $csc" }

# --- dependencies -----------------------------------------------------------
$naudio = Join-Path $root 'build\naudio\lib\net35\NAudio.dll'
if (-not (Test-Path $naudio)) { & (Join-Path $root 'build\fetch-naudio.ps1') }

$wv = Join-Path $root 'build\webview2'
if (-not (Test-Path (Join-Path $wv 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) {
  & (Join-Path $root 'build\fetch-webview2.ps1')
}
$wvCore    = Join-Path $wv 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$wvForms   = Join-Path $wv 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$wvLoader  = Join-Path $wv 'runtimes\win-x64\native\WebView2Loader.dll'

# --- bundle the interface ---------------------------------------------------
& (Join-Path $root 'build\bundle-ui.ps1')
$html = Join-Path $root 'build\app.bundle.html'

# --- compile ----------------------------------------------------------------
$icon = Join-Path $root 'build\MicMonitor.ico'
$output = Join-Path $root 'MicMonitor.exe'

& $csc /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /codepage:65001 `
  "/out:$output" `
  "/win32icon:$icon" `
  "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
  /r:System.Web.Extensions.dll `
  "/r:$naudio" "/r:$wvCore" "/r:$wvForms" `
  "/resource:$naudio,NAudio.dll" `
  "/resource:$wvCore,Microsoft.Web.WebView2.Core.dll" `
  "/resource:$wvForms,Microsoft.Web.WebView2.WinForms.dll" `
  "/resource:$wvLoader,WebView2Loader.dll" `
  "/resource:$html,app.html" `
  "/resource:$icon,app.ico" `
  (Join-Path $root 'src\MicMonitor.cs') (Join-Path $root 'src\AssemblyInfo.cs')

if ($LASTEXITCODE -ne 0) { throw "Compilazione fallita (exit $LASTEXITCODE)" }
Write-Output ("OK: $output  ({0:N0} byte)" -f (Get-Item $output).Length)
