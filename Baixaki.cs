// Baixaki - native Windows version (opens instantly, no PowerShell start-up).
// Build it with build.bat (uses the C# compiler that ships with Windows, nothing to install).
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

class BaixakiForm : Form
{
    // ---- auto-update settings ----
    // AppVersion comes from Version.cs, which build-installer.bat writes for you on every build.
    // UpdateRepo: your GitHub "owner/repo" that hosts the releases. While it still says OWNER/REPO, update checks are off.
    static readonly string AppVersion = AppInfo.Version;
    const string UpdateRepo = "TheOrionEXE/Baixaki";

    // ---- palette ----
    static readonly Color Bg = Color.FromArgb(18, 20, 26);
    static readonly Color Field = Color.FromArgb(31, 35, 46);
    static readonly Color Btn2 = Color.FromArgb(42, 47, 62);
    static readonly Color Btn2Hover = Color.FromArgb(55, 61, 80);
    static readonly Color Accent = Color.FromArgb(214, 48, 180);
    static readonly Color AccentHover = Color.FromArgb(230, 82, 199);
    static readonly Color AccentDown = Color.FromArgb(184, 36, 154);
    static readonly Color TextMain = Color.FromArgb(235, 237, 245);
    static readonly Color TextMuted = Color.FromArgb(139, 145, 165);
    static readonly Color LogNormal = Color.FromArgb(170, 176, 196);
    static readonly Color LogInfo = Color.FromArgb(244, 143, 224);
    static readonly Color LogOk = Color.FromArgb(74, 222, 128);
    static readonly Color LogFail = Color.FromArgb(248, 113, 113);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    TextBox txtUrl, txtPasta;
    RichTextBox log;
    CheckBox chkPremiere, chkMp3, chkVideo;
    Button btnDownload, btnTroca, btnUpdate, btnCancel;
    Process current;
    volatile bool cancelled;
    readonly object procLock = new object();
    string destino;
    string cfgPath;
    Regex urlRegex = new Regex(@"^https?://\S+\.\S+", RegexOptions.IgnoreCase);

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new BaixakiForm());
    }

    public BaixakiForm()
    {
        cfgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Baixaki\folder.txt");
        destino = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        try
        {
            if (File.Exists(cfgPath))
            {
                string saved = File.ReadAllText(cfgPath).Trim();
                if (saved.Length > 0 && Directory.Exists(saved)) destino = saved;
            }
        }
        catch { }

        Text = "Baixaki";
        ClientSize = new Size(660, 470);
        MinimumSize = new Size(560, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Bg;
        ForeColor = TextMain;

        System.Drawing.Icon appIcon = null;
        try
        {
            appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Icon = appIcon;
        }
        catch { }

        // ---- header ----
        if (appIcon != null)
        {
            try
            {
                PictureBox pic = new PictureBox();
                pic.Image = new System.Drawing.Icon(appIcon, 48, 48).ToBitmap();
                pic.SizeMode = PictureBoxSizeMode.Zoom;
                pic.Location = new Point(24, 18);
                pic.Size = new Size(48, 48);
                Controls.Add(pic);
            }
            catch { }
        }
        Label title = new Label();
        title.Text = "Baixaki";
        title.Font = new Font("Segoe UI Semibold", 18F);
        title.ForeColor = TextMain;
        title.Location = new Point(82, 14);
        title.AutoSize = true;
        Controls.Add(title);

        Label sub = new Label();
        sub.Text = "Paste a link. Get your video.   |   v" + AppVersion;
        sub.Font = new Font("Segoe UI", 9F);
        sub.ForeColor = TextMuted;
        sub.Location = new Point(85, 46);
        sub.AutoSize = true;
        Controls.Add(sub);

        AnchorStyles tlr = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // ---- link ----
        Controls.Add(MakeCaption("VIDEO LINK", 24, 90));
        txtUrl = MakeTextBox();
        MakeField(txtUrl, 24, 112, 612, 38, tlr);

        // ---- options ----
        chkPremiere = MakeCheck("Convert for Premiere", 24, 162);
        chkPremiere.Checked = false;
        chkMp3 = MakeCheck("Audio only (MP3)", 230, 162);
        chkMp3.CheckedChanged += delegate
        {
            chkPremiere.Enabled = !chkMp3.Checked;
            if (chkMp3.Checked) chkVideo.Checked = false;
        };
        chkVideo = MakeCheck("Video only (no audio)", 390, 162);
        chkVideo.CheckedChanged += delegate { if (chkVideo.Checked) chkMp3.Checked = false; };

        // ---- folder ----
        Controls.Add(MakeCaption("SAVE TO", 24, 200));
        txtPasta = MakeTextBox();
        txtPasta.ReadOnly = true;
        txtPasta.Text = destino;
        MakeField(txtPasta, 24, 222, 484, 38, tlr);

        btnTroca = MakeButton("Change...", 518, 222, 118, 38, Btn2, Btn2Hover, Btn2Hover);
        btnTroca.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnTroca.Click += ChangeFolder;

        // ---- actions ----
        btnDownload = MakeButton("Download", 24, 278, 170, 44, Accent, AccentHover, AccentDown);
        btnDownload.Font = new Font("Segoe UI Semibold", 11F);
        btnDownload.Click += StartDownload;

        btnUpdate = MakeButton("Update engine", 204, 278, 150, 44, Btn2, Btn2Hover, Btn2Hover);
        btnUpdate.Click += UpdateEngine;

        btnCancel = MakeButton("Cancel", 364, 278, 120, 44, Color.FromArgb(70, 40, 52), Color.FromArgb(120, 50, 70), Color.FromArgb(150, 50, 70));
        btnCancel.Visible = false;
        btnCancel.Click += CancelDownload;

        // ---- log ----
        log = new RichTextBox();
        log.ReadOnly = true;
        log.BorderStyle = BorderStyle.None;
        log.BackColor = Field;
        log.ForeColor = LogNormal;
        log.Font = new Font("Consolas", 9F);
        log.DetectUrls = false;
        log.ScrollBars = RichTextBoxScrollBars.Vertical;
        Panel logPanel = new Panel();
        logPanel.BackColor = Field;
        logPanel.Padding = new Padding(12, 10, 12, 10);
        logPanel.Location = new Point(24, 340);
        logPanel.Size = new Size(612, 112);
        logPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        log.Dock = DockStyle.Fill;
        logPanel.Controls.Add(log);
        Controls.Add(logPanel);

        AcceptButton = btnDownload;

        Shown += delegate
        {
            try { SendMessage(txtUrl.Handle, 0x1501, (IntPtr)1, "Paste the link here (YouTube, Instagram, TikTok, X...)"); } catch { }
            FillFromClipboard();
            txtUrl.Focus();
        };
        Activated += delegate { FillFromClipboard(); };
        Shown += delegate { CheckForUpdate(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int on = 1;
            DwmSetWindowAttribute(Handle, 20, ref on, 4); // dark title bar (Windows 10 20H1+ / 11)
            DwmSetWindowAttribute(Handle, 19, ref on, 4); // older builds
        }
        catch { }
    }

    // ---------- UI helpers ----------
    Label MakeCaption(string text, int x, int y)
    {
        Label l = new Label();
        l.Text = text;
        l.Font = new Font("Segoe UI Semibold", 8F);
        l.ForeColor = TextMuted;
        l.Location = new Point(x, y);
        l.AutoSize = true;
        return l;
    }

    TextBox MakeTextBox()
    {
        TextBox t = new TextBox();
        t.BorderStyle = BorderStyle.None;
        t.BackColor = Field;
        t.ForeColor = TextMain;
        t.Font = new Font("Segoe UI", 10.5F);
        return t;
    }

    void MakeField(Control inner, int x, int y, int w, int h, AnchorStyles anchor)
    {
        Panel p = new Panel();
        p.BackColor = Field;
        p.Location = new Point(x, y);
        p.Size = new Size(w, h);
        p.Anchor = anchor;
        p.Padding = new Padding(12, 9, 12, 6);
        inner.Dock = DockStyle.Fill;
        p.Controls.Add(inner);
        Controls.Add(p);
    }

    CheckBox MakeCheck(string text, int x, int y)
    {
        CheckBox c = new CheckBox();
        c.Text = text;
        c.ForeColor = TextMain;
        c.Location = new Point(x, y);
        c.AutoSize = true;
        c.Cursor = Cursors.Hand;
        Controls.Add(c);
        return c;
    }

    Button MakeButton(string text, int x, int y, int w, int h, Color back, Color hover, Color down)
    {
        Button b = new Button();
        b.Text = text;
        b.Location = new Point(x, y);
        b.Size = new Size(w, h);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = hover;
        b.FlatAppearance.MouseDownBackColor = down;
        b.BackColor = back;
        b.ForeColor = Color.White;
        b.UseVisualStyleBackColor = false;
        b.Cursor = Cursors.Hand;
        Controls.Add(b);
        return b;
    }

    // ---------- behaviour ----------
    void FillFromClipboard()
    {
        try
        {
            if (txtUrl.Text.Trim().Length > 0) return;
            if (!Clipboard.ContainsText()) return;
            string c = Clipboard.GetText().Trim();
            if (urlRegex.IsMatch(c)) txtUrl.Text = c;
        }
        catch { }
    }

    void ChangeFolder(object sender, EventArgs e)
    {
        using (FolderBrowserDialog dlg = new FolderBrowserDialog())
        {
            dlg.Description = "Choose where to save the videos";
            dlg.SelectedPath = destino;
            dlg.ShowNewFolderButton = true;
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                destino = dlg.SelectedPath;
                txtPasta.Text = destino;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cfgPath));
                    File.WriteAllText(cfgPath, destino, Encoding.UTF8);
                }
                catch { }
            }
        }
    }

    void Log(string s)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string>(Log), s); } catch { }
            return;
        }
        Color c = LogNormal;
        if (s.StartsWith("DONE")) c = LogOk;
        else if (s.StartsWith("FAIL")) c = LogFail;
        else if (s.StartsWith("---") || s.StartsWith("Downloaded") || s.StartsWith("Converting")) c = LogInfo;
        log.SelectionStart = log.TextLength;
        log.SelectionLength = 0;
        log.SelectionColor = c;
        log.AppendText(s + Environment.NewLine);
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    void StartDownload(object sender, EventArgs e)
    {
        string url = txtUrl.Text.Trim();
        if (!urlRegex.IsMatch(url) || url.IndexOf('"') >= 0)
        {
            MessageBox.Show(this, "Please paste a valid link (http/https).", "Baixaki");
            return;
        }
        try
        {
            if (!Directory.Exists(destino)) Directory.CreateDirectory(destino);
        }
        catch
        {
            MessageBox.Show(this, "The destination folder is not available. Choose another one.", "Baixaki");
            return;
        }

        btnDownload.Enabled = false;
        btnDownload.Text = "Working...";
        btnDownload.BackColor = Btn2;
        cancelled = false;
        btnCancel.Text = "Cancel";
        btnCancel.Enabled = true;
        btnCancel.Visible = true;
        Log("--- " + url);

        bool audio = chkMp3.Checked;
        bool premiere = chkPremiere.Checked;
        bool videoOnly = chkVideo.Checked;
        string dir = destino;
        Thread t = new Thread(delegate() { DoWork(url, audio, premiere, videoOnly, dir); });
        t.IsBackground = true;
        t.Start();
    }

    void Finished()
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action(Finished)); } catch { }
            return;
        }
        btnDownload.Enabled = true;
        btnDownload.Text = "Download";
        btnDownload.BackColor = Accent;
        btnCancel.Visible = false;
        cancelled = false;
        txtUrl.Text = "";
        txtUrl.Focus();
    }

    // ---------- cancel ----------
    void KillTree(Process p)
    {
        try
        {
            ProcessStartInfo k = new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F");
            k.CreateNoWindow = true;
            k.UseShellExecute = false;
            Process.Start(k);
        }
        catch
        {
            try { p.Kill(); } catch { }
        }
    }

    void CancelDownload(object sender, EventArgs e)
    {
        cancelled = true;
        btnCancel.Enabled = false;
        btnCancel.Text = "Cancelling...";
        Process p;
        lock (procLock) { p = current; }
        if (p != null) KillTree(p);
    }

    // Removes half-downloaded leftovers after a cancel
    void CleanupPartial(string dir, DateTime since)
    {
        try
        {
            foreach (string f in Directory.GetFiles(dir))
            {
                FileInfo fi = new FileInfo(f);
                if (fi.LastWriteTime < since) continue;
                string n = fi.Name.ToLowerInvariant();
                if (n.EndsWith(".part") || n.EndsWith(".ytdl") || n.EndsWith(".temp") || Regex.IsMatch(n, @"\.f\d+\.[a-z0-9]+$"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
        catch { }
    }

    // ---------- app self-update (GitHub Releases) ----------
    void CheckForUpdate()
    {
        if (UpdateRepo.StartsWith("OWNER")) return;
        Thread t = new Thread(delegate()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                string json;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Baixaki-Updater";
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString("https://api.github.com/repos/" + UpdateRepo + "/releases/latest");
                }
                Match mt = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                Match mu = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+BaixakiSetup\\.exe)\"");
                if (!mt.Success || !mu.Success) return;
                string tag = mt.Groups[1].Value;
                string dl = mu.Groups[1].Value;
                Version latest = new Version(tag.TrimStart('v', 'V'));
                Version current = new Version(AppVersion);
                if (latest <= current) return;
                BeginInvoke(new Action(delegate { AskUpdate(tag, dl); }));
            }
            catch { }
        });
        t.IsBackground = true;
        t.Start();
    }

    void AskUpdate(string tag, string url)
    {
        DialogResult r = MessageBox.Show(this,
            "Baixaki " + tag + " is available (you have v" + AppVersion + ").\n\nUpdate now? The app will restart.",
            "Baixaki update", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (r != DialogResult.Yes) return;

        btnDownload.Enabled = false;
        btnDownload.Text = "Updating...";
        Log("--- Downloading update " + tag + "...");
        Thread t = new Thread(delegate()
        {
            try
            {
                string setup = Path.Combine(Path.GetTempPath(), "BaixakiSetup-update.exe");
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Baixaki-Updater";
                    wc.DownloadFile(url, setup);
                }
                Log("Installing update, the app will restart...");
                Process.Start(setup, "/SILENT /CLOSEAPPLICATIONS");
                BeginInvoke(new Action(delegate { Application.Exit(); }));
            }
            catch (Exception ex)
            {
                Log("FAIL: update failed: " + ex.Message);
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        btnDownload.Enabled = true;
                        btnDownload.Text = "Download";
                    }));
                }
                catch { }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    void UpdateEngine(object sender, EventArgs e)
    {
        btnUpdate.Enabled = false;
        btnUpdate.Text = "Updating...";
        string dir = destino;
        Thread t = new Thread(delegate()
        {
            try
            {
                Log("--- Updating yt-dlp...");
                Run("yt-dlp", "-U", dir, delegate(string l) { Log(l); });
                Log("DONE: update check finished.");
            }
            catch (Win32Exception) { Log("FAIL: yt-dlp was not found."); }
            catch (Exception ex) { Log("FAIL: " + ex.Message); }
            finally
            {
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        btnUpdate.Enabled = true;
                        btnUpdate.Text = "Update engine";
                    }));
                }
                catch { }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    string Resolve(string line, string dir)
    {
        try
        {
            if (string.IsNullOrEmpty(line) || line.Length > 400) return null;
            string full = Path.IsPathRooted(line) ? line : Path.Combine(dir, line);
            return File.Exists(full) ? full : null;
        }
        catch { return null; }
    }

    // Fallback if the printed path could not be matched (e.g. accented characters in the title)
    string Newest(string dir, DateTime since, string[] exts)
    {
        string best = null;
        DateTime bestTime = DateTime.MinValue;
        foreach (string f in Directory.GetFiles(dir))
        {
            FileInfo fi = new FileInfo(f);
            if (fi.LastWriteTime < since) continue;
            if (fi.Name.IndexOf("_Premiere", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (Array.IndexOf(exts, fi.Extension.ToLowerInvariant()) < 0) continue;
            if (fi.LastWriteTime > bestTime) { bestTime = fi.LastWriteTime; best = f; }
        }
        return best;
    }

    int Run(string exe, string args, string workDir, Action<string> onLine)
    {
        // Prefer the copies installed next to Baixaki.exe; fall back to whatever is on PATH
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string local = Path.Combine(baseDir, exe + ".exe");
        string file = File.Exists(local) ? local : exe;
        ProcessStartInfo psi = new ProcessStartInfo(file, args);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;
        psi.WorkingDirectory = workDir;
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        psi.EnvironmentVariables["PYTHONUTF8"] = "1";
        psi.EnvironmentVariables["PATH"] = baseDir + ";" + Environment.GetEnvironmentVariable("PATH");
        using (Process p = new Process())
        {
            p.StartInfo = psi;
            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs ev) { if (ev.Data != null) onLine(ev.Data); };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs ev) { if (ev.Data != null) onLine(ev.Data); };
            p.Start();
            lock (procLock) { current = p; }
            if (cancelled) KillTree(p);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            lock (procLock) { current = null; }
            return p.ExitCode;
        }
    }

    void DoWork(string url, bool audio, bool premiere, bool videoOnly, string dir)
    {
        try
        {
            DateTime started = DateTime.Now.AddSeconds(-2);
            string found = null;
            Action<string> onLine = delegate(string line)
            {
                Log(line);
                string cand = Resolve(line, dir);
                if (cand != null) found = cand;
            };

            string common = " -o \"%(title).150s.%(ext)s\" --no-simulate --progress --newline --print after_move:filepath \"" + url + "\"";
            string ytArgs;
            if (audio)
                ytArgs = "-x --audio-format mp3 --audio-quality 0" + common;
            else if (videoOnly)
                ytArgs = "-f \"bv*[vcodec^=avc1][ext=mp4]/bv*[ext=mp4]/bv*\" --merge-output-format mp4" + common;
            else
                ytArgs = "-f \"bv*[vcodec^=avc1][ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/b\" --merge-output-format mp4" + common;

            Run("yt-dlp", ytArgs, dir, onLine);
            if (cancelled) { CleanupPartial(dir, started); Log("Cancelled."); return; }

            if (found == null)
                found = Newest(dir, started, audio ? new string[] { ".mp3" } : new string[] { ".mp4", ".mkv", ".webm", ".mov" });
            if (found == null) { Log("FAIL: download failed or file not found."); return; }

            if (audio || !premiere) { Log("DONE: " + found); return; }

            Log("Downloaded: " + found);
            Log("Converting for Premiere...");
            string outFile = Path.Combine(Path.GetDirectoryName(found), Path.GetFileNameWithoutExtension(found) + "_Premiere.mp4");
            string ffArgs = "-y -hide_banner -loglevel error -i \"" + found + "\" " +
                "-c:v libx264 -profile:v high -level 4.0 -pix_fmt yuv420p " +
                "-colorspace bt709 -color_primaries bt709 -color_trc bt709 " +
                "-r 29.97 -c:a aac -b:a 192k -movflags +faststart \"" + outFile + "\"";
            int code = Run("ffmpeg", ffArgs, dir, delegate(string l) { Log(l); });
            if (cancelled) { try { File.Delete(outFile); } catch { } Log("Cancelled. (the downloaded video was kept)"); return; }
            if (code == 0) Log("DONE: " + outFile);
            else Log("FAIL: ffmpeg conversion failed.");
        }
        catch (Win32Exception)
        {
            Log("FAIL: yt-dlp or ffmpeg was not found. Make sure both are installed and on your PATH.");
        }
        catch (Exception ex)
        {
            Log("FAIL: " + ex.Message);
        }
        finally
        {
            Finished();
        }
    }
}
