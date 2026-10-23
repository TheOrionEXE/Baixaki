# Builds BaixakiSetup.exe: gets yt-dlp + ffmpeg, compiles Baixaki.exe, then runs Inno Setup.
# Run it through build-installer.bat. Keep all project files in this folder.
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Set-Location $PSScriptRoot

function Download([string]$url, [string]$out) {
    Invoke-WebRequest $url -OutFile $out -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
}

# Looks for a real ffmpeg.exe: winget package folders first (Gyan preferred), then PATH
function Find-Ffmpeg {
    $roots = @("$env:LOCALAPPDATA\Microsoft\WinGet\Packages", "$env:ProgramFiles\WinGet\Packages")
    $all = @()
    foreach ($r in $roots) {
        if (Test-Path $r) {
            $all += Get-ChildItem $r -Recurse -Filter "ffmpeg.exe" -ErrorAction SilentlyContinue
        }
    }
    $gyan = $all | Where-Object { $_.FullName -match "Gyan" } | Select-Object -First 1
    if ($gyan) { return $gyan.FullName }
    if ($all.Count -gt 0) { return $all[0].FullName }

    $cmd = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($cmd) {
        $item = Get-Item $cmd.Source
        if ($item.LinkType -and $item.Target) {
            $t = @($item.Target)[0]
            if (Test-Path $t) { return $t }
        }
        return $cmd.Source
    }
    return $null
}

try {
    foreach ($f in "Baixaki.cs", "Baixaki.ico", "baixaki.iss") {
        if (-not (Test-Path $f)) { throw "Missing file: $f (keep it in the same folder as this script)." }
    }

    Write-Host "[1/4] yt-dlp..."
    if (-not (Test-Path "yt-dlp.exe")) {
        Download "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe" "yt-dlp.exe"
    } else { Write-Host "      already here, skipping download." }

    Write-Host "[2/4] ffmpeg..."
    if (-not (Test-Path "ffmpeg.exe")) {
        $ffPath = Find-Ffmpeg

        if (-not $ffPath) {
            Write-Host "      installing with winget (Gyan.FFmpeg)..."
            try {
                winget install --id Gyan.FFmpeg -e --accept-package-agreements --accept-source-agreements
            } catch {
                Write-Host "      winget failed: $($_.Exception.Message)" -ForegroundColor Yellow
            }
            $ffPath = Find-Ffmpeg
        }

        if (-not $ffPath) {
            Write-Host "      winget did not provide ffmpeg, trying GitHub backup..." -ForegroundColor Yellow
            $tmp = Join-Path $env:TEMP "ffmpeg-extract"
            if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
            New-Item -ItemType Directory -Path $tmp -Force | Out-Null
            $zip = Join-Path $env:TEMP "ffmpeg-backup.zip"
            Download "https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip" $zip
            Expand-Archive $zip -DestinationPath $tmp -Force
            Remove-Item $zip -Force -ErrorAction SilentlyContinue
            $f = Get-ChildItem $tmp -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
            if ($f) { $ffPath = $f.FullName }
        }

        if (-not $ffPath) { throw "Could not get ffmpeg. Run 'winget install --id Gyan.FFmpeg -e' manually, then run this again." }

        Write-Host "      using $ffPath"
        Copy-Item $ffPath ".\ffmpeg.exe" -Force

        # keep the ffmpeg license files for the installer
        $dir = Split-Path $ffPath
        $root = if ((Split-Path $dir -Leaf) -eq "bin") { Split-Path $dir } else { $dir }
        New-Item -ItemType Directory -Path "licenses" -Force | Out-Null
        Get-ChildItem $root -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^(LICENSE|README)' } |
            ForEach-Object { Copy-Item $_.FullName ("licenses\ffmpeg-" + $_.Name) -Force }
    } else { Write-Host "      already here, skipping." }

    # Version: remembered in version.txt, suggested as "last + 1", and written into Version.cs for the build
    $cur = "1.0.0"
    if (Test-Path "version.txt") {
        $t = (Get-Content "version.txt" -Raw).Trim()
        if ($t) { $cur = $t }
    }
    $suggest = $cur
    try {
        $parts = $cur.Split('.')
        $parts[$parts.Length - 1] = [string]([int]$parts[$parts.Length - 1] + 1)
        $suggest = $parts -join '.'
    } catch { }
    $new = Read-Host "Last build was $cur. Version for this build (press Enter for $suggest)"
    if ($new) { $new = $new.Trim().TrimStart('v', 'V') } else { $new = $suggest }
    Set-Content "version.txt" -Value $new -Encoding ASCII
    Set-Content "Version.cs" -Value ('static class AppInfo { public const string Version = "' + $new + '"; }') -Encoding UTF8
    Write-Host "      building version $new   (use the tag v$new on GitHub)"

    Write-Host "[3/4] Compiling Baixaki.exe..."
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    if (-not (Test-Path $csc)) { throw "C# compiler not found." }
    & $csc /nologo /target:winexe /optimize+ /win32icon:Baixaki.ico /out:Baixaki.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Baixaki.cs Version.cs
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed. Copy the error lines above and send them to Claude." }

    Write-Host "[4/4] Building the installer..."
    function Find-Iscc {
        $c = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        )
        foreach ($p in $c) { if (Test-Path $p) { return $p } }
        return $null
    }
    $iscc = Find-Iscc
    if (-not $iscc) {
        Write-Host "      Inno Setup not found. Trying to install it with winget..."
        try {
            winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
        } catch { }
        $iscc = Find-Iscc
    }
    if (-not $iscc) { throw "Inno Setup 6 is required. Install it free from https://jrsoftware.org/isdl.php and run this again." }

    $ver = $new
    Write-Host "      version $ver"
    & $iscc "/DMyAppVersion=$ver" "baixaki.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed. Copy the error lines above and send them to Claude." }

    Write-Host ""
    Write-Host "Done! Your installer is here: $PSScriptRoot\Output\BaixakiSetup.exe" -ForegroundColor Green
    Start-Process explorer.exe (Join-Path $PSScriptRoot "Output")
}
catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
