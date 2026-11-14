param([switch]$Integration,[switch]$Menus,[switch]$Memory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$sourceRoot = if (Test-Path -LiteralPath (Join-Path $root 'src')) { Join-Path $root 'src' } else { $root }
$buildDir = Join-Path $root 'build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $fx 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The x64 .NET Framework compiler is required.' }
$references = @('/r:System.Drawing.dll', '/r:System.Windows.Forms.dll')
$sources = @('AssemblyInfo.cs','Native.cs','Localization.cs','Core.cs','PreferencesMigration.cs','Settings.cs','Fonts.cs','Icons.cs','TaskbarAutomation.cs','Taskbar.cs','Renderer.cs','SettingsForm.cs','Program.cs') | ForEach-Object { Join-Path $sourceRoot $_ }
$resources = @('flows','speedometer') | ForEach-Object { $resourceFile=Join-Path $root ('icons\'+$_+'.ico'); if (!(Test-Path -LiteralPath $resourceFile)) { throw ('Missing built-in icon: '+$_) }; '/resource:'+ $resourceFile + ',NetSpeck.Icons.'+$_+'.ico' }
$icon = @()
if (Test-Path -LiteralPath (Join-Path $root 'NetSpeck.ico')) { $icon = @('/win32icon:' + (Join-Path $root 'NetSpeck.ico')) }
& $compiler /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ /utf8output ('/win32manifest:' + (Join-Path $root 'app.manifest')) ('/out:' + (Join-Path $root 'NetSpeck.exe')) @icon @resources @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
& $compiler /nologo /codepage:65001 /utf8output /target:exe /platform:x64 /optimize+ ('/out:' + (Join-Path $buildDir 'NetSpeck.Tests.exe')) (Join-Path $sourceRoot 'Native.cs') (Join-Path $sourceRoot 'Localization.cs') (Join-Path $sourceRoot 'Core.cs') (Join-Path $sourceRoot 'PreferencesMigration.cs') (Join-Path $sourceRoot 'Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& (Join-Path $buildDir 'NetSpeck.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
if ($Integration) {
    & $compiler /nologo /codepage:65001 /utf8output /target:exe /platform:x64 /optimize+ /main:NetSpeck.IntegrationTests ('/win32manifest:' + (Join-Path $root 'app.manifest')) ('/out:' + (Join-Path $buildDir 'IntegrationTests.exe')) @resources @references @sources (Join-Path $sourceRoot 'IntegrationTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Integration-test build failed.' }
    & (Join-Path $buildDir 'IntegrationTests.exe') (Join-Path $buildDir 'verification')
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
}
if ($Menus) {
    if (Get-Process -Name NetSpeck,TaskbarNet -ErrorAction SilentlyContinue) { throw 'Exit the app before the menu behavior tests.' }
    & $compiler /nologo /codepage:65001 /utf8output /target:exe /platform:x64 /optimize+ /main:NetSpeck.BehaviorTests ('/win32manifest:' + (Join-Path $root 'app.manifest')) ('/out:' + (Join-Path $buildDir 'BehaviorTests.exe')) @resources @references @sources (Join-Path $sourceRoot 'BehaviorTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Menu-test build failed.' }
    & (Join-Path $buildDir 'BehaviorTests.exe') (Join-Path $buildDir 'verification')
    if ($LASTEXITCODE -ne 0) { throw 'Menu behavior tests failed.' }
}
if ($Memory) {
    & $compiler /nologo /codepage:65001 /utf8output /target:exe /platform:x64 /optimize+ /main:NetSpeck.MemoryTests ('/out:' + (Join-Path $buildDir 'MemoryTests.exe')) @resources @references @sources (Join-Path $sourceRoot 'MemoryTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Memory-test build failed.' }
    & (Join-Path $buildDir 'MemoryTests.exe') layout 2000 (Join-Path $buildDir 'verification\memory-layout.csv')
    if ($LASTEXITCODE -ne 0) { throw 'Layout memory regression failed.' }
    & (Join-Path $buildDir 'MemoryTests.exe') render 10000 (Join-Path $buildDir 'verification\memory-render.csv')
    if ($LASTEXITCODE -ne 0) { throw 'Render memory regression failed.' }
}
Write-Output (Join-Path $root 'NetSpeck.exe')
