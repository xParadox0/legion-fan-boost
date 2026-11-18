$ErrorActionPreference = 'Stop'

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The x64 .NET Framework C# compiler is required.' }

$dist = Join-Path $PSScriptRoot 'dist'
$null = New-Item -ItemType Directory -Path $dist -Force
$exe = Join-Path $dist 'LenovoFanBoost.exe'
$references = 'System.Drawing.dll', 'System.Management.dll', 'System.Windows.Forms.dll' |
    ForEach-Object { '/reference:{0}' -f (Join-Path $framework $_) }

$buildArgs = '/nologo', '/target:winexe', '/platform:x64', '/optimize+', "/out:$exe",
    "/win32icon:$PSScriptRoot\FanBoost.ico", "/win32manifest:$PSScriptRoot\FanBoost.manifest", "$PSScriptRoot\FanBoost.cs"

& $compiler @buildArgs @references
if ($LASTEXITCODE -ne 0) { throw 'The C# build failed.' }

Get-Item -LiteralPath $exe | Select-Object FullName, Length
