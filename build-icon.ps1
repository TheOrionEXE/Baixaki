$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $csc)){$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
New-Item -ItemType Directory -Path test-output -Force | Out-Null
& $csc /nologo /target:exe /r:System.Drawing.dll /out:test-output\BuildIcon.exe tools\BuildIcon.cs
if($LASTEXITCODE -ne 0){throw 'Icon tool compilation failed.'}
& .\test-output\BuildIcon.exe $PSScriptRoot
if($LASTEXITCODE -ne 0){throw 'Icon generation failed.'}
