param([switch]$SkipBuild,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'dist' }
$version = (Get-Content -LiteralPath (Join-Path $projectRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$') { throw 'Invalid VERSION.' }
if (!$SkipBuild) { & (Join-Path $projectRoot 'build.ps1') }
$executable = Join-Path $projectRoot 'NetSpeck.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the application before packaging.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$archivePath = Join-Path $OutputDirectory ('NetSpeck-v' + $version + '-win-x64.zip')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Open($archivePath,[IO.FileMode]::Create,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
try {
    $entries = @(
        @{ Source = $executable; Name = 'NetSpeck/NetSpeck.exe' },
        @{ Source = (Join-Path $projectRoot 'LICENSE'); Name = 'NetSpeck/LICENSE' },
        @{ Source = (Join-Path $projectRoot 'PORTABLE_README.txt'); Name = 'NetSpeck/README.txt' },
        @{ Source = (Join-Path $projectRoot 'docs\KNOWN_ISSUES.md'); Name = 'NetSpeck/KNOWN_ISSUES.md' }
    )
    foreach ($entry in $entries) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$entry.Source,$entry.Name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($archivePath + '.sha256') -Value ($hash + '  ' + [IO.Path]::GetFileName($archivePath)) -Encoding ASCII
Write-Output $archivePath
Write-Output ($archivePath + '.sha256')
