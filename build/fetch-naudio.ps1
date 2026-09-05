# Downloads NAudio 1.10.0 and extracts the .NET 3.5 build used by MicMonitor.
# The DLL is embedded into the executable as a resource at compile time.

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$zip = Join-Path $here 'naudio.zip'

Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/NAudio/1.10.0' -OutFile $zip -UseBasicParsing
Expand-Archive -Path $zip -DestinationPath (Join-Path $here 'naudio') -Force
Remove-Item $zip -Force

$dll = Join-Path $here 'naudio\lib\net35\NAudio.dll'
if (-not (Test-Path $dll)) { throw 'NAudio.dll non trovata dopo l''estrazione' }
Write-Output "OK: $dll"
