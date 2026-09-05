# Inlines the base64 font faces into ui/app.html and writes build/app.bundle.html,
# the single self-contained page that gets embedded in the executable.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$page = [IO.File]::ReadAllText((Join-Path $root 'ui\app.html'), [Text.Encoding]::UTF8)
$fonts = [IO.File]::ReadAllText((Join-Path $root 'ui\fonts\fonts.css'), [Text.Encoding]::UTF8)

if ($page -notmatch '<!--FONTS-->') { throw 'Segnaposto <!--FONTS--> non trovato in ui/app.html' }

$page = $page.Replace('<!--FONTS-->', "<style>`n$fonts</style>")

$out = Join-Path $root 'build\app.bundle.html'
[IO.File]::WriteAllText($out, $page, (New-Object Text.UTF8Encoding $false))
Write-Output ("OK: $out  ({0:N0} byte)" -f (Get-Item $out).Length)
