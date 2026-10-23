BAIXAKI - video downloader (yt-dlp + ffmpeg)

QUICK START
1. Unzip this folder anywhere.
2. Double-click build-installer.bat (needs internet the first time).
   It gets yt-dlp and ffmpeg, builds Baixaki.exe and creates the installer:
   Output\BaixakiSetup.exe
3. Give BaixakiSetup.exe to your team. They just click Next - nothing else to install.

If the build stops with an error, copy the error text and send it to Claude.

WITHOUT AN INSTALLER (portable)
- Double-click build.bat to create Baixaki.exe only.
- Put Baixaki.exe, yt-dlp.exe and ffmpeg.exe in the same folder (zip it to share).

AUTO-UPDATE SETUP (one time)
1. Create a free GitHub account and a new PUBLIC repository (e.g. "baixaki").
2. Open Baixaki.cs and change this line near the top:
       const string UpdateRepo = "OWNER/REPO";
   to your own, e.g. "agenciaduvidiu/baixaki".
3. Run build-installer.bat and give the team the first BaixakiSetup.exe.
   (Updates only work for versions built with the repo name filled in.)

PUBLISHING AN UPDATE
1. Change the program and raise the number in Baixaki.cs:
       const string AppVersion = "1.0.1";
2. Run build-installer.bat -> Output\BaixakiSetup.exe
3. On GitHub: your repo > Releases > Draft a new release
   - Tag: v1.0.1 (same number as AppVersion, with a "v" in front)
   - Upload Output\BaixakiSetup.exe (the file name must stay BaixakiSetup.exe)
   - Publish release
4. Next time anyone opens Baixaki, it asks "Update now?" and updates itself.

The "Update engine" button inside the app updates yt-dlp separately (no release needed).

FILES
- Baixaki.cs / Baixaki.ico ..... the app source and icon
- build.bat ................... builds Baixaki.exe only
- build-installer.bat/.ps1 .... builds Baixaki.exe + BaixakiSetup.exe (needs Inno Setup, installed automatically via winget if missing)
- baixaki.iss ................. installer settings (Inno Setup)
- extras-powershell-version ... older PowerShell version of the app (fallback)
