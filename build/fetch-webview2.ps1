# Downloads the Microsoft.Web.WebView2 SDK. Only the managed assemblies and the
# x64 native loader are used; they are embedded into the executable at build time.

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$zip = Join-Path $here 'webview2.zip'

Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/1.0.4191.47' -OutFile $zip -UseBasicParsing
Expand-Archive -Path $zip -DestinationPath (Join-Path $here 'webview2') -Force
Remove-Item $zip -Force

foreach ($relative in @(
    'lib\net462\Microsoft.Web.WebView2.Core.dll',
    'lib\net462\Microsoft.Web.WebView2.WinForms.dll',
    'runtimes\win-x64\native\WebView2Loader.dll')) {
  $path = Join-Path $here "webview2\$relative"
  if (-not (Test-Path $path)) { throw "File mancante dopo l'estrazione: $relative" }
}
Write-Output 'OK: SDK WebView2 pronto'
