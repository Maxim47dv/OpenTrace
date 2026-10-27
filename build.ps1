param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Install .NET Framework 4.8 on Windows.' }
$buildFolder = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $buildFolder | Out-Null
$coreSource = Join-Path $PSScriptRoot 'src\Core.cs'
$appSource = Join-Path $PSScriptRoot 'src\App.cs'
& $compiler /nologo /target:winexe /codepage:65001 "/out:$buildFolder\OpenTrace.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll $coreSource $appSource
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
if ($Test) {
    $testSource = Join-Path $PSScriptRoot 'tests\Tests.cs'
    & $compiler /nologo /target:exe /codepage:65001 "/out:$buildFolder\Tests.exe" /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll $coreSource $testSource
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & "$buildFolder\Tests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $uiTestSource = Join-Path $PSScriptRoot 'tests\UiSmoke.cs'
    & $compiler /nologo /target:exe /main:UiSmoke /codepage:65001 "/out:$buildFolder\UiTests.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll $coreSource $appSource $uiTestSource
    if ($LASTEXITCODE -ne 0) { throw 'UI test build failed.' }
    & "$buildFolder\UiTests.exe" "$buildFolder\test-preview.png"
    if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
}
Write-Output "Built: $buildFolder\OpenTrace.exe"
