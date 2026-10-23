param([switch]$Integration)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $csc)) { throw 'Windows .NET Framework C# compiler not found.' }
New-Item -ItemType Directory -Path 'test-output' -Force | Out-Null
$references = @('/nologo','/target:exe','/r:System.Web.Extensions.dll','/r:System.Core.dll')
$core = @('DownloadState.cs','DownloadEngine.cs','Version.cs')
$ui = @('Baixaki.cs','AppUpdater.cs','HelpCenter.cs','ModernControls.cs','UiText.cs','/resource:HelpTopics.json,Baixaki.HelpTopics.json','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/win32icon:assets\baixaki.ico')
function Run-Check([string]$Name, [string[]]$Sources, [string[]]$Options, [string[]]$Arguments) {
    $binary = Join-Path $PSScriptRoot ('test-output\' + $Name + '.exe')
    & $csc @references @Options "/out:$binary" @Sources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $Name" }
    & $binary @Arguments | Tee-Object -FilePath (Join-Path $PSScriptRoot ('test-output\' + $Name + '-results.txt'))
    if ($LASTEXITCODE -ne 0) { throw "Checks failed: $Name" }
}
Run-Check -Name 'Tests' -Sources (@('Tests.cs') + $core) -Arguments @('test-output\simulation-state')
Run-Check -Name 'UiWorkflowTests' -Sources (@('UiWorkflowTests.cs','Tests.cs') + $core + $ui) -Options @('/main:UiWorkflowTests')
Run-Check -Name 'LayoutTests' -Sources (@('LayoutTests.cs') + $core + $ui) -Options @('/main:LayoutTests')
if ($Integration) { Run-Check -Name 'IntegrationTests' -Sources (@('IntegrationTests.cs') + $core) }
Write-Host 'All requested checks completed successfully.' -ForegroundColor Green



