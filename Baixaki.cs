// Native Windows Forms; build with the Windows .NET Framework compiler.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Single line under the input: shows what was recognized (thumbnail, title, details) or the current hint/error.
class PreviewStrip : Control
{
    public string Thumb = "", Title = "", Sub = "";
    public Color Tone = Theme.Muted;
    public PreviewStrip() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Height = Theme.S(66); BackColor = Theme.Bg; }
    public void ShowMedia(string thumb, string title, string sub, Color tone)
    {
        if (Thumb == thumb && Title == title && Sub == sub && Tone == tone) return;
        Thumb = thumb ?? ""; Title = title ?? ""; Sub = sub ?? ""; Tone = tone; Invalidate();
    }
    public void Message(string text, bool error) { ShowMedia("", "", text, error ? Theme.Warn : Theme.Muted); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Bg);
        if (Title.Length == 0) { ModernPaint.Text(g, Sub, Theme.Font(10F), new Rectangle(Theme.S(4), 0, Width - Theme.S(8), Height), Tone); return; }
        int th = Theme.S(48), tw = th * 16 / 9;
        var r = new Rectangle(Theme.S(4), (Height - th) / 2, tw, th);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Bitmap bmp = ThumbCache.Get(Thumb);
        using (var path = ModernPaint.Round(r, Theme.S(6)))
        {
            if (bmp != null) { var state = g.Save(); g.SetClip(path, System.Drawing.Drawing2D.CombineMode.Intersect); g.DrawImage(bmp, r); g.Restore(state); }
            else { using (var brush = new SolidBrush(Theme.Placeholder(Title))) g.FillPath(brush, path); ModernPaint.Icon(g, Glyph.Play, r, Color.FromArgb(150, 255, 255, 255), 12F); }
        }
        int lh = Theme.S(22);
        ModernPaint.Text(g, Title, Theme.Font(11F), new Rectangle(r.Right + Theme.S(14), r.Y - Theme.S(1), Width - r.Right - Theme.S(18), lh), Theme.Ink);
        ModernPaint.Text(g, Sub, Theme.Font(9.5F), new Rectangle(r.Right + Theme.S(14), r.Y + lh, Width - r.Right - Theme.S(18), lh), Tone);
    }
}

partial class BaixakiForm : Form
{
    static readonly string AppVersion = AppInfo.Version;
    const string UpdateRepo = "TheOrionEXE/Baixaki";
    readonly DownloadManager manager;
    PillInput urlInput, searchInput;
    FlatButton formatChip, qualityChip, gifChip, folderChip, startButton, clearButton, gearButton;
    TabStrip tabs;
    RowList list;
    PreviewStrip preview;
    BufferedPanel header, chipRow, tabRow, settingsPanel;
    Label creditLabel, engineVersionLabel;
    FlatButton holdsButton;
    readonly ToolTip tips = new ToolTip { AutoPopDelay = 12000, InitialDelay = 500 };
    readonly StringBuilder logText = new StringBuilder();
    readonly HashSet<string> expanded = new HashSet<string>();
    readonly List<Action> settingsRefresh = new List<Action>();
    CancellationTokenSource analysis;
    Task analysisTask;
    string analysisFor, analyzedUrl, unsupportedUrl;
    ResourceInfo currentResource;
    readonly Dictionary<string, ResourceInfo> analysisCache = new Dictionary<string, ResourceInfo>();
    bool closing, shuttingDown, resizing, refreshing, keepAnalysis;
    System.Windows.Forms.Timer debounce, refresh;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle, int attr, ref int value, int size);
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new BaixakiForm());
    }
    public BaixakiForm() : this(new StateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baixaki")), new EngineRunner(), true) { }
    public BaixakiForm(StateStore store, EngineRunner runner, bool startup = false)
    {
        manager = new DownloadManager(store, runner);
        Text = "Baixaki — v" + AppVersion; Font = Theme.Font(10F); BackColor = Theme.Bg; ForeColor = Theme.Ink; KeyPreview = true; DoubleBuffered = true;
        // Default window fits the screen: a large readable layout, never larger than the working area.
        Rectangle work = Screen.PrimaryScreen.WorkingArea;
        AutoScaleMode = AutoScaleMode.None; MinimumSize = new Size(Math.Min(Theme.S(820), work.Width - 40), Math.Min(Theme.S(620), work.Height - 80));
        ClientSize = new Size(Math.Min(Theme.S(980), work.Width - 80), Math.Min(Theme.S(700), work.Height - 120)); StartPosition = FormStartPosition.CenterScreen;
        if (AppIcon.Window != null) Icon = AppIcon.Window;
        BuildLayout(); BuildSettings(); Wire();
        manager.Log = Log; manager.Changed = delegate { if (InvokeRequired) { try { BeginInvoke(new Action(RefreshUi)); } catch { } } else RefreshUi(); };
        Activated += delegate { if (startup) FillClipboard(); };
        Shown += delegate
        {
            if (!startup) return;
            ThumbCache.CleanOld(manager.State.KeepCompletedDays);
            FillClipboard(); if (store.Warning != null) Log(store.Warning);
            manager.PruneCompleted(manager.State.KeepCompletedDays); RefreshUi(); CheckForUpdate();
            var later = new System.Windows.Forms.Timer { Interval = 6000 }; later.Tick += async delegate { later.Stop(); later.Dispose(); await AutoUpdateEngine(); };
            later.Start(); RefreshEngineVersion();
        };
        var wheel = new WheelRedirect(); Application.AddMessageFilter(wheel); FormClosed += delegate { Application.RemoveMessageFilter(wheel); };
        FormClosing += HandleClosing;
        ResizeBegin += delegate { resizing = true; }; ResizeEnd += delegate { resizing = false; RefreshUi(); };
        RefreshUi();
    }
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x02000000; return cp; } }
    protected override void OnHandleCreated(EventArgs e)
    { base.OnHandleCreated(e); try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { } }

    // ---------- layout ----------
    void BuildLayout()
    {
        var body = new BufferedPanel { Dock = DockStyle.Fill, Padding = new Padding(Theme.S(26), Theme.S(6), Theme.S(26), Theme.S(22)), BackColor = Theme.Bg };
        Controls.Add(body);
        list = new RowList { Dock = DockStyle.Fill, EmptyTitle = "Nada na fila", EmptySub = "Cole um link acima e pressione Enter." };
        tabRow = new BufferedPanel { Dock = DockStyle.Top, Height = Theme.S(54), BackColor = Theme.Bg };
        preview = new PreviewStrip { Dock = DockStyle.Top };
        chipRow = new BufferedPanel { Dock = DockStyle.Top, Height = Theme.S(48), BackColor = Theme.Bg };
        var inputRow = new BufferedPanel { Dock = DockStyle.Top, Height = Theme.S(64), BackColor = Theme.Bg, Padding = new Padding(0, Theme.S(4), 0, Theme.S(8)) };
        header = new BufferedPanel { Dock = DockStyle.Top, Height = Theme.S(62), BackColor = Theme.Bg };
        body.Controls.Add(list); body.Controls.Add(tabRow); body.Controls.Add(preview); body.Controls.Add(chipRow); body.Controls.Add(inputRow); body.Controls.Add(header);

        // header: logo, name, version; credit and settings on the right
        creditLabel = new Label { Text = "Created by OrionEXE", Name = "CreatorCredit", AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Font(9.5F), BackColor = Theme.Bg };
        gearButton = new FlatButton { Kind = BtnKind.Ghost, Icon = Glyph.Settings, Width = Theme.S(40), Height = Theme.S(38), AutoFit = false };
        tips.SetToolTip(gearButton, "Configurações");
        header.Controls.Add(creditLabel); header.Controls.Add(gearButton);
        header.Paint += delegate(object sender, PaintEventArgs e)
        {
            int logo = Theme.S(36);
            if (AppIcon.Logo != null) { e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; e.Graphics.DrawImage(AppIcon.Logo, new Rectangle(0, (header.Height - logo) / 2, logo, logo)); }
            Font titleFont = Theme.Font(17F, true); Size title = TextRenderer.MeasureText("Baixaki", titleFont, new Size(int.MaxValue, 200), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, "Baixaki", titleFont, new Point(logo + Theme.S(10), (header.Height - title.Height) / 2), Theme.Ink, TextFormatFlags.NoPadding);
            Font versionFont = Theme.Font(9.5F); Size version = TextRenderer.MeasureText("v" + AppVersion, versionFont, new Size(int.MaxValue, 200), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, "v" + AppVersion, versionFont, new Point(logo + Theme.S(10) + title.Width + Theme.S(10), (header.Height - version.Height) / 2 + Theme.S(3)), Theme.Muted, TextFormatFlags.NoPadding);
        };
        header.Resize += delegate { PlaceHeader(); };
        PlaceHeader();
        preview.Message("Cole um link do YouTube, TikTok, Instagram e de outros sites compatíveis com o yt-dlp.", false);

        urlInput = new PillInput { Dock = DockStyle.Fill, Placeholder = "Cole um link de vídeo ou playlist" };
        urlInput.Button.Text = "Adicionar";
        inputRow.Controls.Add(urlInput);

        formatChip = new FlatButton { Kind = BtnKind.Chip, Dropdown = true, Icon = Glyph.Video };
        qualityChip = new FlatButton { Kind = BtnKind.Chip, Dropdown = true };
        gifChip = new FlatButton { Kind = BtnKind.Chip };
        folderChip = new FlatButton { Kind = BtnKind.Chip, Icon = Glyph.Folder };
        chipRow.Controls.Add(formatChip); chipRow.Controls.Add(qualityChip); chipRow.Controls.Add(gifChip); chipRow.Controls.Add(folderChip);
        chipRow.Resize += delegate { PlaceChips(); };

        tabs = new TabStrip { Location = new Point(0, Theme.S(8)) };
        startButton = new FlatButton { Kind = BtnKind.Primary, Text = "Baixar tudo", Icon = Glyph.Download, Height = Theme.S(38) };
        clearButton = new FlatButton { Kind = BtnKind.Secondary, Text = "Limpar lista", Height = Theme.S(38) };
        searchInput = new PillInput { Placeholder = "Buscar", IconGlyph = Glyph.Search, ShowButton = false, Width = Theme.S(210), Height = Theme.S(38), Visible = false };
        tabRow.Controls.Add(tabs); tabRow.Controls.Add(startButton); tabRow.Controls.Add(clearButton); tabRow.Controls.Add(searchInput);
        tabRow.Resize += delegate { PlaceTabRow(); };
        UpdateChips(); PlaceTabRow();
    }
    void PlaceHeader()
    {
        gearButton.Location = new Point(header.Width - gearButton.Width, (header.Height - gearButton.Height) / 2);
        creditLabel.Location = new Point(gearButton.Left - creditLabel.Width - Theme.S(10), (header.Height - creditLabel.Height) / 2);
    }
    bool showQuality, showGif;
    // Visible is false for every control until the window is shown, so the layout tracks which chips apply by itself.
    void PlaceChips()
    {
        int x = 0;
        foreach (var entry in new[] { new KeyValuePair<FlatButton, bool>(formatChip, true), new KeyValuePair<FlatButton, bool>(qualityChip, showQuality),
            new KeyValuePair<FlatButton, bool>(gifChip, showGif), new KeyValuePair<FlatButton, bool>(folderChip, true) })
        { if (!entry.Value) continue; entry.Key.Location = new Point(x, Theme.S(5)); x += entry.Key.Width + Theme.S(8); }
    }
    void PlaceTabRow()
    {
        int right = tabRow.Width, y = Theme.S(7);
        startButton.Location = new Point(right - startButton.Width, y); clearButton.Location = new Point(right - clearButton.Width, y);
        searchInput.Location = new Point(clearButton.Left - Theme.S(10) - searchInput.Width, y);
    }
    void UpdateChips()
    {
        var state = manager.State;
        formatChip.Text = UiText.Format(state.Mode, state.Premiere);
        bool video = state.Mode == MediaMode.VideoAudio || state.Mode == MediaMode.VideoOnly;
        showQuality = video; qualityChip.Visible = video; qualityChip.Text = UiText.Quality(state.Quality);
        showGif = state.Mode == MediaMode.Gif; gifChip.Visible = showGif; gifChip.Text = "Trecho " + Clock(state.GifStart) + " · " + state.GifDuration + " s · " + state.GifWidth + " px";
        string name = Path.GetFileName((state.Destination ?? "").TrimEnd('\\', '/'));
        folderChip.Text = String.IsNullOrEmpty(name) ? state.Destination : name;
        tips.SetToolTip(folderChip, "Pasta de destino: " + state.Destination + "\nClique para escolher outra.");
        PlaceChips();
    }
    static string Clock(int seconds) { return (seconds / 60) + ":" + (seconds % 60).ToString("00"); }

    // ---------- events ----------
    void Wire()
    {
        HelpRequested += delegate(object sender, HelpEventArgs e) { OpenHelp(); e.Handled = true; };
        urlInput.Submit += async delegate { await Guard(AddUrl); };
        urlInput.Button.Click += async delegate { await Guard(AddUrl); };
        debounce = new System.Windows.Forms.Timer { Interval = 350 };
        debounce.Tick += async delegate { debounce.Stop(); if (Safety.ValidUrl(urlInput.Box.Text.Trim())) await Guard(delegate { return StartAnalysis(urlInput.Box.Text.Trim()); }); };
        urlInput.Box.TextChanged += delegate
        {
            debounce.Stop(); if (analysis != null && !keepAnalysis) analysis.Cancel(); currentResource = null; unsupportedUrl = null; analyzedUrl = null; analysisFor = null;
            string text = urlInput.Box.Text.Trim();
            if (text.Length == 0) preview.Message("Cole um link do YouTube, TikTok, Instagram e de outros sites compatíveis com o yt-dlp.", false);
            else if (!Safety.ValidUrl(text)) preview.Message("Use um link http(s) de página, sem senha ou token no endereço.", true);
            else { preview.Message("Analisando o link…", false); debounce.Start(); }
        };
        formatChip.Click += delegate { ShowFormatMenu(); };
        qualityChip.Click += delegate { ShowQualityMenu(); };
        gifChip.Click += delegate { EditGif(); };
        folderChip.Click += delegate { ChooseFolder(); };
        gearButton.Click += delegate { ToggleSettings(); };
        tabs.SelectedChanged += delegate { list.EmptyTitle = tabs.Selected == 0 ? "Nada na fila" : "Nenhum download concluído"; RefreshUi(); };
        startButton.Click += delegate { if (manager.Running) manager.Pause(); else StartQueue(); RefreshUi(); };
        clearButton.Click += delegate
        {
            manager.State.Queue.RemoveAll(q => q.Status == "Completed"); manager.Save(); RefreshUi();
        };
        searchInput.Box.TextChanged += delegate { RefreshUi(); };
        list.ActionInvoked += async delegate(RowItem row, string action) { await Guard(delegate { return HandleRowAction(row, action); }); };
        refresh = new System.Windows.Forms.Timer { Interval = 500 }; refresh.Tick += delegate { if (!resizing) RefreshUi(); }; refresh.Start();
        var hourly = new System.Windows.Forms.Timer { Interval = 3600000 }; hourly.Tick += delegate { manager.PruneCompleted(manager.State.KeepCompletedDays); }; hourly.Start();
        FormClosed += delegate { hourly.Dispose(); tips.Dispose(); };
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && settingsPanel != null && settingsPanel.Visible) { ToggleSettings(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ---------- menus ----------
    void ShowFormatMenu()
    {
        var menu = DarkMenu.Create(); var s = manager.State;
        DarkMenu.Header(menu, "VÍDEO");
        DarkMenu.Item(menu, "MP4", "Padrão, vídeo + áudio", s.Mode == MediaMode.VideoAudio && !s.Premiere, delegate { SetFormat(MediaMode.VideoAudio, false); });
        DarkMenu.Item(menu, "Premiere", "H.264, abre em qualquer editor", s.Mode == MediaMode.VideoAudio && s.Premiere, delegate { SetFormat(MediaMode.VideoAudio, true); });
        DarkMenu.Item(menu, "GIF", "Escolha o trecho", s.Mode == MediaMode.Gif, delegate { SetFormat(MediaMode.Gif, false); });
        DarkMenu.Header(menu, "ÁUDIO");
        DarkMenu.Item(menu, "MP3", "Compatível com tudo", s.Mode == MediaMode.Mp3, delegate { SetFormat(MediaMode.Mp3, false); });
        DarkMenu.Item(menu, "WAV", "Sem perda, para edição", s.Mode == MediaMode.Wav, delegate { SetFormat(MediaMode.Wav, false); });
        DarkMenu.Header(menu, "OUTROS");
        DarkMenu.Item(menu, "Miniatura", "Só a imagem de capa", s.Mode == MediaMode.Thumbnail, delegate { SetFormat(MediaMode.Thumbnail, false); });
        menu.Show(formatChip, new Point(0, formatChip.Height + 4));
    }
    void SetFormat(MediaMode mode, bool premiere)
    {
        manager.State.Mode = mode; manager.State.Premiere = premiere; Persist(); UpdateChips();
        if (mode == MediaMode.Gif) EditGif();
    }
    void ShowQualityMenu()
    {
        var menu = DarkMenu.Create(); string current = manager.State.Quality ?? "best";
        foreach (string q in new[] { "best", "1080", "720", "480" })
        { string value = q; DarkMenu.Item(menu, UiText.Quality(value), "", current == value, delegate { manager.State.Quality = value; Persist(); UpdateChips(); }); }
        menu.Show(qualityChip, new Point(0, qualityChip.Height + 4));
    }
    void EditGif()
    {
        var s = manager.State;
        using (var dialog = new GifDialog(s.GifStart, s.GifDuration, s.GifWidth))
            if (dialog.ShowDialog(this) == DialogResult.OK) { s.GifStart = dialog.StartSeconds; s.GifDuration = dialog.LengthSeconds; s.GifWidth = dialog.WidthPixels; Persist(); UpdateChips(); }
    }
    void ChooseFolder()
    {
        using (var dialog = new FolderBrowserDialog { Description = "Escolha a pasta para os novos downloads", SelectedPath = manager.State.Destination })
            if (dialog.ShowDialog(this) == DialogResult.OK) { manager.State.Destination = dialog.SelectedPath; Persist(); UpdateChips(); foreach (var refresher in settingsRefresh) refresher(); }
    }
    void Persist()
    {
        if (refreshing) return;
        try { manager.Save(); } catch (Exception ex) { Log("Não foi possível salvar as configurações: " + Safety.Redact(ex.Message)); }
    }

    // ---------- link analysis and adding ----------
    static string UnsupportedMessage(string url)
    {
        return DownloadManager.IsInstagramProfile(url) ? "Perfis do Instagram não são suportados no momento. Cole o link de um post ou reel." : "Este link não é atendido pelo yt-dlp instalado. Links de vídeos individuais ainda podem funcionar.";
    }
    Task StartAnalysis(string url)
    {
        if (analysisFor == url && analysisTask != null && !analysisTask.IsCompleted) return analysisTask;
        if (analysis != null) analysis.Cancel();
        analysisFor = url; analysisTask = AnalyzeCore(url); return analysisTask;
    }
    void ShowAnalysis(string url, ResourceInfo info)
    {
        currentResource = info; analyzedUrl = url;
        string kind = info.IsCollection ? "Playlist ou coleção" : "Vídeo";
        var parts = new List<string>();
        if (!String.IsNullOrEmpty(info.Channel)) parts.Add(info.Channel);
        if (!info.IsCollection && info.Duration > 0) parts.Add(Theme.Duration(info.Duration));
        parts.Add(kind);
        preview.ShowMedia(info.Thumb, String.IsNullOrEmpty(info.Title) ? "Mídia" : Safety.Redact(info.Title), String.Join(" · ", parts), Theme.Muted);
    }
    // Links that are certainly one video do not need to wait for the analysis: they join the queue at once.
    static bool ClearlySingle(string url)
    {
        Uri uri; if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out uri)) return false;
        string host = uri.Host.ToLowerInvariant(); if (host.StartsWith("www.")) host = host.Substring(4); if (host.StartsWith("m.")) host = host.Substring(2);
        string path = uri.AbsolutePath, query = uri.Query; bool hasList = Regex.IsMatch(query, @"(^|[?&])list=");
        if (host == "youtu.be") return !hasList && path.Length > 1;
        if (host == "youtube.com") return (path == "/watch" && Regex.IsMatch(query, @"(^|[?&])v=") && !hasList) || path.StartsWith("/shorts/");
        if (host == "instagram.com") return Regex.IsMatch(path, @"^/(p|reel|tv)/[^/]+");
        if (host == "tiktok.com") return Regex.IsMatch(path, @"^/@[^/]+/video/\d+");
        if (host == "x.com" || host == "twitter.com") return Regex.IsMatch(path, @"^/[^/]+/status/\d+");
        if (host == "vimeo.com") return Regex.IsMatch(path, @"^/\d+");
        return false;
    }
    async Task AddSingleNow(string url)
    {
        bool added = manager.Add(url, "", null, null, null);
        if (!added) { Feedback("Este vídeo, neste formato, já está na fila ou foi baixado.", true); return; }
        var item = manager.State.Queue.Last(); manager.Save();
        keepAnalysis = true; ClearInput(); keepAnalysis = false;
        tabs.Selected = 0; Feedback("Adicionado à fila.", false); if (manager.State.AutoStart) StartQueue();
        await FillMetadata(item, url);
    }
    // Title, thumbnail and duration arrive after the item is already in the queue.
    async Task FillMetadata(DownloadItem item, string url)
    {
        try
        {
            ResourceInfo info; analysisCache.TryGetValue(url, out info);
            if (info == null && analysisFor == url && analysisTask != null) { await analysisTask; analysisCache.TryGetValue(url, out info); }
            if (info == null)
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60))) info = await manager.Analyze(url, cts.Token);
                analysisCache[url] = info;
            }
            if (IsDisposed || info == null || !manager.State.Queue.Contains(item)) return;
            if (String.IsNullOrEmpty(item.Title)) item.Title = Safety.Redact(info.Title);
            if (String.IsNullOrEmpty(item.Thumb)) item.Thumb = info.Thumb;
            if (item.Duration == 0) item.Duration = info.Duration;
            if (String.IsNullOrEmpty(item.Channel)) item.Channel = info.Channel;
            if (String.IsNullOrEmpty(item.Extractor)) item.Extractor = info.Extractor;
            if (String.IsNullOrEmpty(item.MediaId)) item.MediaId = info.Id;
            manager.Save();
        }
        catch (Exception ex) { Log("Detalhes do vídeo indisponíveis por enquanto: " + Safety.Redact(ex.Message)); }
    }
    async Task AnalyzeCore(string url)
    {
        if (analyzedUrl == url && currentResource != null) return;
        ResourceInfo cached;
        if (analysisCache.TryGetValue(url, out cached)) { unsupportedUrl = null; ShowAnalysis(url, cached); return; }
        var cancellation = new CancellationTokenSource(); analysis = cancellation; unsupportedUrl = null;
        preview.Message("Analisando o link…", false);
        try
        {
            ResourceInfo info = await manager.Analyze(url, cancellation.Token);
            if (analysisCache.Count > 60) analysisCache.Clear();
            analysisCache[url] = info;
            if (urlInput.Box.Text.Trim() != url || cancellation.IsCancellationRequested) return;
            ShowAnalysis(url, info);
        }
        catch (OperationCanceledException) { }
        catch (UnsupportedResourceException) { if (urlInput.Box.Text.Trim() == url) { unsupportedUrl = url; currentResource = null; preview.Message(UnsupportedMessage(url), true); } }
        catch (InvalidOperationException ex)
        {
            if (manager.Updating) { preview.Message("Atualizando o motor de download… tentando de novo em instantes.", false); if (debounce != null) { debounce.Interval = 2500; debounce.Start(); } }
            else { preview.Message(manager.Held(url) ? UiText.Hold(manager.State.Holds.FirstOrDefault(h => h.Source == Safety.Source(url))) : "Não foi possível analisar este link.", true); Log(Safety.Redact(ex.Message)); }
        }
        catch (Exception ex)
        {
            string friendly = UiText.Friendly(ex.Message);
            if (urlInput.Box.Text.Trim() == url) preview.Message(friendly != ex.Message ? friendly : "Não foi possível identificar este link. Veja o registro técnico nas configurações.", true);
            Log(Safety.Redact(ex.Message));
        }
        finally { if (analysis == cancellation) analysis = null; cancellation.Dispose(); if (debounce != null) debounce.Interval = 350; if (!IsDisposed) RefreshUi(); }
    }
    async Task AddUrl()
    {
        debounce.Stop();
        string url = urlInput.Box.Text.Trim();
        if (!Safety.ValidUrl(url)) { Feedback("Cole um link http(s) sem senha ou token.", true); return; }
        if (unsupportedUrl == url) { Feedback(UnsupportedMessage(url), true); return; }
        if ((currentResource == null || analyzedUrl != url) && ClearlySingle(url) && !manager.Updating) { await AddSingleNow(url); RefreshUi(); return; }
        if (currentResource == null || analyzedUrl != url) await StartAnalysis(url);
        if (urlInput.Box.Text.Trim() != url || currentResource == null) return;
        ResourceInfo info = currentResource;
        if (info.IsCollection) await OpenCollection(info);
        else
        {
            bool added = manager.Add(url, String.IsNullOrEmpty(info.Title) ? "Mídia" : Safety.Redact(info.Title), info.Extractor, info.Id, null, info.Thumb, info.Duration, info.Channel);
            manager.Save();
            if (added) { ClearInput(); tabs.Selected = 0; Feedback("Adicionado à fila.", false); if (manager.State.AutoStart) StartQueue(); }
            else Feedback("Este vídeo, neste formato, já está na fila ou foi baixado.", true);
        }
        RefreshUi();
    }
    void ClearInput()
    {
        urlInput.Box.Clear(); currentResource = null; analyzedUrl = null; unsupportedUrl = null; analysisFor = null;
    }
    static bool TrustedBulkHost(string url)
    {
        string host = Safety.Source(url); return host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com");
    }
    async Task OpenCollection(ResourceInfo info)
    {
        var job = manager.CreateCollection(info); var s = manager.State;
        job.OwnFolder = s.OwnFolder; job.Limit = s.DefaultLimit; job.Mode = s.Mode; job.Premiere = s.Premiere; job.Destination = s.Destination; job.Thumb = info.Thumb;
        manager.Save(); expanded.Add(job.Id); ClearInput(); tabs.Selected = 0;
        if (TrustedBulkHost(job.Url) && job.Entries.Count <= 1) { Feedback("Buscando os vídeos da playlist…", false); await DiscoverJob(job, false); }
        else Feedback("Coleção aberta. Escolha os vídeos e adicione à fila.", false);
        RefreshUi();
    }
    async Task DiscoverJob(CollectionJob job, bool confirm)
    {
        if (confirm && !TrustedBulkHost(job.Url))
        {
            string extent = job.Limit == 0 ? "todos os itens acessíveis" : "até " + job.Limit + " itens";
            if (MessageBox.Show(this, "Perfis e coleções fora do YouTube podem gerar muitas solicitações e causar limites de acesso ou restrições na conta, principalmente com cookies.\n\nO yt-dlp tentará listar " + extent + ". A espera entre requisições reduz, mas não elimina, o risco.\n\nContinuar?", "Listar coleção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        }
        var task = manager.Discover(job);
        RefreshUi();
        await task;
    }
    void StartQueue()
    {
        try { var ignored = manager.Start(); } catch (Exception ex) { Feedback("Não foi possível iniciar: " + Safety.Redact(ex.Message), true); }
    }

    // ---------- list model ----------
    string Meta(DownloadItem q, bool withMode)
    {
        var parts = new List<string>();
        if (withMode) parts.Add(UiText.Mode(q.Mode, q.Premiere) + (q.IsVideoMode && !String.IsNullOrEmpty(q.Quality) && q.Quality != "best" ? " " + q.Quality + "p" : ""));
        if (!String.IsNullOrEmpty(q.Channel)) parts.Add(q.Channel);
        return String.Join(" · ", parts);
    }
    RowItem ItemRow(DownloadItem q)
    {
        var row = new RowItem { Key = "item:" + q.Id, Kind = "item", Tag = q, ThumbUrl = q.Thumb ?? "", Badge = Theme.Duration(q.Duration),
            Title = String.IsNullOrEmpty(q.Title) ? (q.Status == "Completed" || q.Status == "Failed" ? "Sem título" : "Carregando informações…") : q.Title };
        string meta = Meta(q, true);
        switch (q.Status)
        {
            case "Downloading":
                row.State = 1; row.Progress = q.Percent; row.Right = q.Percent.HasValue ? "" : "Baixando…";
                string speed = UiText.Speed(q.Speed); row.Sub = speed == "—" ? meta : meta + " · " + speed; row.Actions = new[] { "cancel" }; break;
            case "Converting": row.State = 1; row.Right = "Convertendo…"; row.Sub = meta; row.Actions = new[] { "cancel" }; break;
            case "Pending":
                if (manager.Held(q.Url) || manager.Held(q.OriginalUrl))
                {
                    // Waiting on a paused source: say so, instead of looking like an ordinary queue.
                    row.State = 2; row.Right = "Fonte em pausa";
                    row.Sub = Trim(UiText.Hold(manager.State.Holds.FirstOrDefault(h => h.Source == Safety.Source(q.Url) || h.Source == Safety.Source(q.OriginalUrl))), 120);
                }
                else { row.Right = "Na fila"; row.Sub = meta; }
                row.Actions = new[] { "remove" }; break;
            case "Paused": row.Right = "Pausado"; row.Sub = meta; row.Actions = new[] { "remove" }; break;
            case "Failed":
                row.State = 2; row.Right = "Falhou"; row.Sub = Trim(UiText.Friendly(Safety.Redact(q.Error)), 110); row.Actions = new[] { "details", "retry", "remove" }; break;
            case "Blocked": row.State = 2; row.Right = "Fonte em pausa"; row.Sub = "Aguarde antes de tentar de novo"; row.Actions = new[] { "retry", "remove" }; break;
            case "Cancelled": row.Right = "Cancelado"; row.Sub = meta; row.Actions = new[] { "retry", "remove" }; break;
            default:
                if (File.Exists(q.OutputFile))
                {
                    long size = 0; try { size = new FileInfo(q.OutputFile).Length; } catch { }
                    row.State = 3; row.Right = q.Updated.ToLocalTime().ToString("HH:mm"); row.Sub = meta + (size > 0 ? " · " + Theme.Size(size) : ""); row.Actions = new[] { "play", "folder", "remove" };
                }
                else { row.State = 2; row.Right = "Arquivo movido"; row.Sub = meta; row.Actions = new[] { "retry", "remove" }; }
                break;
        }
        return row;
    }
    static string Trim(string text, int max) { return text.Length <= max ? text : text.Substring(0, max - 1) + "…"; }
    bool JobOpen(CollectionJob job)
    {
        return job.Status == "Discovering" || job.Status.StartsWith("Preview") || job.Entries.Any(e => e.Status == "Discovered");
    }
    void AddCollectionRows(List<RowItem> rows)
    {
        foreach (CollectionJob job in manager.State.Collections.Where(JobOpen))
        {
            bool preview = job.Status.StartsWith("Preview") && !TrustedBulkHost(job.Url);
            var shown = job.Entries.Where(e => e.Status == "Discovered").ToList();
            var eligible = shown.Where(e => Metadata.Eligible(e, job.Filter)).ToList();
            bool open = expanded.Contains(job.Id) && !preview;
            var row = new RowItem { Key = "job:" + job.Id, Kind = "playlist", Tag = job, ThumbUrl = job.Thumb ?? "", Title = String.IsNullOrEmpty(job.Title) ? "Playlist" : job.Title, Expanded = open };
            if (job.Status == "Discovering") { row.Sub = "Buscando vídeos… " + job.Entries.Count + " encontrados"; row.Actions = new[] { "cancel", "expand" }; }
            else if (preview) { row.Sub = "Perfil ou coleção · liste os itens para escolher o que baixar"; row.PrimaryText = "Listar itens"; row.Actions = new[] { "remove" }; }
            else
            {
                row.Sub = "Playlist · " + shown.Count + " vídeos · " + eligible.Count + " selecionados" + (job.Status == "Partial" ? " · lista incompleta" : "");
                if (eligible.Count > 0) row.PrimaryText = "Adicionar " + eligible.Count + " à fila";
                row.Actions = new[] { "expand", "remove" };
            }
            rows.Add(row);
            if (!open) continue;
            rows.Add(new RowItem { Key = "all:" + job.Id, Kind = "selectall", Tag = job, HasCheck = true, Checked = shown.Count > 0 && eligible.Count == shown.Count, Title = "Selecionar todos (" + eligible.Count + " de " + shown.Count + ")" });
            foreach (CollectionEntry entry in shown.Take(3000))
                rows.Add(new RowItem { Key = "entry:" + job.Id + ":" + entry.Key, Kind = "entry", Tag = new object[] { job, entry }, HasCheck = true, Checked = !entry.Skip, ThumbUrl = entry.Thumb ?? "",
                    Title = String.IsNullOrEmpty(entry.Title) ? entry.Id : entry.Title, Right = Theme.Duration(entry.Duration), Sub = "" });
        }
    }
    void Section(List<RowItem> rows, string title, IEnumerable<DownloadItem> items)
    {
        var all = items.ToList(); if (all.Count == 0) return;
        rows.Add(new RowItem { Key = "h:" + title, Kind = "header", Title = title });
        foreach (DownloadItem q in all) rows.Add(ItemRow(q));
    }
    List<RowItem> BuildRows()
    {
        var rows = new List<RowItem>(); var queue = manager.State.Queue;
        if (tabs.Selected == 0)
        {
            AddCollectionRows(rows);
            Section(rows, "BAIXANDO", queue.Where(q => q.Status == "Downloading" || q.Status == "Converting"));
            Section(rows, "AGUARDANDO", queue.Where(q => q.Status == "Pending" || q.Status == "Paused"));
            Section(rows, "COM PROBLEMA", queue.Where(q => q.Status == "Failed" || q.Status == "Blocked" || q.Status == "Cancelled"));
        }
        else
        {
            string filter = searchInput.Box.Text.Trim();
            var done = queue.Where(q => q.Status == "Completed" && (filter.Length == 0 || (q.Title ?? "").IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0)).OrderByDescending(q => q.Updated).ToList();
            foreach (var group in done.GroupBy(q => q.Updated.ToLocalTime().Date))
            {
                rows.Add(new RowItem { Key = "h:" + group.Key.ToString("yyyyMMdd"), Kind = "header", Title = UiText.Day(group.Key) });
                foreach (DownloadItem q in group) rows.Add(ItemRow(q));
            }
        }
        return rows;
    }
    void RefreshUi()
    {
        if (closing || list == null || resizing || refreshing) return;
        refreshing = true;
        try
        {
            var queue = manager.State.Queue;
            int open = queue.Count(q => q.Status != "Completed"), done = queue.Count(q => q.Status == "Completed");
            tabs.SetTabs("Fila · " + open, "Concluídos · " + done);
            list.EmptySub = tabs.Selected == 0 ? "Cole um link acima e pressione Enter." : "Os downloads concluídos ficam aqui por " + manager.State.KeepCompletedDays + " dias.";
            list.SetRows(BuildRows());
            bool canRun = queue.Any(q => (q.Status == "Pending" || q.Status == "Paused") && !manager.Held(q.Url) && !manager.Held(q.OriginalUrl));
            startButton.Visible = tabs.Selected == 0 && (open > 0 || manager.Running);
            startButton.Text = manager.Running ? "Pausar" : queue.Any(q => q.Status == "Paused") ? "Retomar" : "Baixar tudo";
            startButton.Icon = manager.Running ? Glyph.Pause : Glyph.Download; startButton.Kind = manager.Running ? BtnKind.Secondary : BtnKind.Primary;
            startButton.Enabled = manager.Running || (canRun && !manager.Busy && !manager.Updating);
            bool completedTab = tabs.Selected == 1;
            clearButton.Visible = completedTab && done > 0; searchInput.Visible = completedTab && done > 0;
            PlaceTabRow();
            string url = urlInput.Box.Text.Trim();
            urlInput.Button.Enabled = Safety.ValidUrl(url) && unsupportedUrl != url && !manager.Updating;
            if (holdsButton != null)
            {
                int paused = manager.State.Holds.Count(h => !h.Until.HasValue || h.Until.Value > DateTime.UtcNow);
                holdsButton.Visible = paused > 0; holdsButton.Text = "Fontes em pausa (" + paused + ")";
            }
        }
        finally { refreshing = false; }
    }
    async Task HandleRowAction(RowItem row, string action)
    {
        var item = row.Tag as DownloadItem;
        if (item != null)
        {
            switch (action)
            {
                case "cancel": manager.Cancel(item); break;
                case "remove": manager.Remove(item); break;
                case "retry": manager.Retry(item); if (manager.State.AutoStart) StartQueue(); break;
                case "play": OpenOutput(item, false); break;
                case "folder": OpenOutput(item, true); break;
                case "details": ShowDetails(item); break;
            }
            RefreshUi(); return;
        }
        var job = row.Tag as CollectionJob;
        if (job != null)
        {
            switch (action)
            {
                case "expand": if (!expanded.Add(job.Id)) expanded.Remove(job.Id); break;
                case "remove": expanded.Remove(job.Id); manager.RemoveCollection(job); break;
                case "cancel": manager.StopDiscovery(); break;
                case "primary":
                    if (job.Status.StartsWith("Preview") && !TrustedBulkHost(job.Url)) { expanded.Add(job.Id); await DiscoverJob(job, true); }
                    else QueueCollection(job);
                    break;
                case "check":
                    var open = job.Entries.Where(e => e.Status == "Discovered").ToList();
                    bool all = open.All(e => !e.Skip && Metadata.Eligible(e, job.Filter));
                    foreach (var entry in open) entry.Skip = all; manager.Save(); break;
            }
            RefreshUi(); return;
        }
        var pair = row.Tag as object[];
        if (pair != null && action == "check") { var entry = (CollectionEntry)pair[1]; entry.Skip = !entry.Skip; manager.Save(); RefreshUi(); }
    }
    // Everything needed to understand (or report) a failed download, in one copyable box.
    void ShowDetails(DownloadItem item)
    {
        string work = ""; try { work = Safety.WorkDirectory(item); } catch { }
        string text = (String.IsNullOrEmpty(item.Title) ? "Sem título" : item.Title) + "\n\nSituação: " + UiText.Status(item.Status) + "\nFormato: " + UiText.Mode(item.Mode, item.Premiere)
            + "\nDestino: " + item.Destination + "\nPasta de trabalho: " + work + "\nBaixaki " + AppVersion
            + (String.IsNullOrEmpty(item.Error) ? "" : "\n\nErro informado pelo motor:\n" + item.Error);
        MessageBox.Show(this, text, "Detalhes do download", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    void QueueCollection(CollectionJob job)
    {
        var s = manager.State;
        job.Mode = s.Mode; job.Premiere = s.Premiere; job.Destination = s.Destination; job.BatchSize = 100000;
        int count;
        try { count = manager.QueueBatch(job); }
        catch (InvalidOperationException) { Feedback("Esta playlist ainda tem vídeos em andamento. Aguarde terminarem para adicionar mais.", true); return; }
        if (count > 0)
        {
            Feedback(count + (count == 1 ? " vídeo adicionado à fila." : " vídeos adicionados à fila."), false);
            // Videos left unchecked were a decision: the playlist leaves the list once nothing selected remains.
            if (!job.Entries.Any(e => e.Status == "Discovered" && !e.Skip)) { expanded.Remove(job.Id); manager.RemoveCollection(job); }
            if (s.AutoStart) StartQueue();
        }
        else Feedback("Nenhum vídeo marcado para adicionar.", true);
    }
    void OpenOutput(DownloadItem item, bool folder)
    {
        string file = item.OutputFile;
        if (folder)
        {
            if (!String.IsNullOrEmpty(file) && File.Exists(file)) Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + file + "\"") { UseShellExecute = true });
            else if (Directory.Exists(item.Destination)) Process.Start(new ProcessStartInfo(item.Destination) { UseShellExecute = true });
            return;
        }
        if (String.IsNullOrEmpty(file) || !File.Exists(file)) return;
        string[] safe = { ".mp4", ".mkv", ".webm", ".mov", ".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".opus", ".avi", ".mpeg", ".mpg", ".m4v", ".ts", ".gif", ".jpg" };
        if (!safe.Contains(Path.GetExtension(file).ToLowerInvariant())) { Feedback("Este tipo de arquivo não é aberto pelo aplicativo. Use Mostrar na pasta.", true); return; }
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }

    // ---------- feedback, log, helpers ----------
    void Feedback(string text, bool error) { preview.Message(text, error); Log(text); }
    void Log(string text)
    {
        if (IsDisposed || shuttingDown) return;
        if (InvokeRequired) { try { BeginInvoke(new Action<string>(Log), text); } catch { } return; }
        if (logText.Length > 100000) logText.Clear();
        logText.Append(Safety.Redact(UiText.Message(text))).Append(Environment.NewLine);
    }
    async Task Guard(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Feedback(ex is UnsupportedResourceException ? UiText.Unsupported : "Não foi possível concluir: " + Safety.Redact(ex.Message), true); }
    }
    void FillClipboard()
    {
        try { if (urlInput.Box.Text.Trim().Length == 0 && Clipboard.ContainsText() && Safety.ValidUrl(Clipboard.GetText().Trim())) urlInput.Box.Text = Clipboard.GetText().Trim(); } catch { }
    }
    void OpenHelp() { using (var help = new HelpCenterForm()) help.ShowDialog(this); }
    async void HandleClosing(object sender, FormClosingEventArgs e)
    {
        if (closing) return;
        e.Cancel = true; if (shuttingDown) return; shuttingDown = true;
        await Task.Yield();
        try
        {
            debounce.Stop(); refresh.Stop(); if (analysis != null) analysis.Cancel(); manager.StopDiscovery(); manager.Pause();
            while (manager.Busy || manager.ActiveId != null || manager.Discovering || analysis != null || manager.Updating) await Task.Delay(100);
            manager.Save(); closing = true; Close();
        }
        catch (Exception ex) { shuttingDown = false; refresh.Start(); Log("Não foi possível salvar a fila: " + Safety.Redact(ex.Message)); }
    }

    // ---------- settings drawer ----------
    void ToggleSettings()
    {
        settingsPanel.Visible = !settingsPanel.Visible;
        if (settingsPanel.Visible) { foreach (var refresher in settingsRefresh) refresher(); settingsPanel.BringToFront(); }
        gearButton.Active = settingsPanel.Visible;
    }
    void BuildSettings()
    {
        int panelWidth = Theme.S(410), margin = Theme.S(22);
        settingsPanel = new BufferedPanel { Width = panelWidth, BackColor = Theme.Panel, Visible = false, AutoScroll = true };
        settingsPanel.HandleCreated += delegate { DarkScroll.Apply(settingsPanel); };
        Controls.Add(settingsPanel); settingsPanel.BringToFront();
        Action place = delegate { settingsPanel.SetBounds(ClientSize.Width - settingsPanel.Width, 0, settingsPanel.Width, ClientSize.Height); };
        Resize += delegate { place(); }; place();
        settingsPanel.Paint += delegate(object sender, PaintEventArgs e) { using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, 0, 0, 0, settingsPanel.Height); };
        int y = Theme.S(18);
        var title = new Label { Text = "Configurações", Font = Theme.Font(12.5F, true), ForeColor = Theme.Ink, AutoSize = true, Location = new Point(margin, y), BackColor = Theme.Panel };
        var close = new FlatButton { Kind = BtnKind.Ghost, Icon = Glyph.Close, Width = Theme.S(38), Height = Theme.S(36), AutoFit = false, Location = new Point(panelWidth - margin - Theme.S(38), y - Theme.S(6)) };
        close.Click += delegate { ToggleSettings(); };
        settingsPanel.Controls.Add(title); settingsPanel.Controls.Add(close); y += Theme.S(50);
        Func<string, int> section = delegate(string name)
        {
            settingsPanel.Controls.Add(new Label { Text = name, Font = Theme.Font(8.5F, true), ForeColor = Theme.Muted, AutoSize = true, Location = new Point(margin, y + Theme.S(4)), BackColor = Theme.Panel }); y += Theme.S(32); return y;
        };
        Func<string, string, Control, Label> row = delegate(string name, string tip, Control right)
        {
            var label = new Label { Text = name, ForeColor = Theme.Ink, AutoSize = true, Location = new Point(margin, y + Theme.S(7)), BackColor = Theme.Panel };
            settingsPanel.Controls.Add(label); right.Location = new Point(panelWidth - margin - right.Width - (settingsPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0), y + (Theme.S(34) - right.Height) / 2); settingsPanel.Controls.Add(right);
            if (tip.Length > 0) { tips.SetToolTip(label, tip); tips.SetToolTip(right, tip); }
            y += Theme.S(44); return label;
        };
        var s = manager.State;

        section("DOWNLOADS");
        var folder = new FlatButton { Kind = BtnKind.Secondary, Height = Theme.S(34), Text = "Alterar" }; folder.Click += delegate { ChooseFolder(); };
        row("Pasta padrão", "A pasta atual aparece na etiqueta da tela principal.", folder);
        var autoStart = new ToggleSwitch { Checked = s.AutoStart }; autoStart.CheckedChanged += delegate { s.AutoStart = autoStart.Checked; Persist(); };
        row("Iniciar ao adicionar", "Começa a baixar assim que você adiciona um item.", autoStart);
        var duplicates = new ToggleSwitch { Checked = s.Duplicates }; duplicates.CheckedChanged += delegate { s.Duplicates = duplicates.Checked; Persist(); };
        row("Pular já baixados", "Evita baixar de novo um vídeo que já existe na pasta, no mesmo formato.", duplicates);
        var ownFolder = new ToggleSwitch { Checked = s.OwnFolder }; ownFolder.CheckedChanged += delegate { s.OwnFolder = ownFolder.Checked; Persist(); };
        row("Subpasta por playlist", "Guarda os vídeos de cada playlist em uma pasta separada.", ownFolder);

        section("RITMO E LIMITES");
        var pace = new FlatButton { Kind = BtnKind.Chip, Dropdown = true, Height = Theme.S(34) };
        Action showPace = delegate { pace.Text = s.PaceAuto ? "Automático" : s.PaceSeconds + " s"; };
        pace.Click += delegate
        {
            var menu = DarkMenu.Create();
            DarkMenu.Item(menu, "Automático", "1 s até 10 vídeos, 5 s acima", s.PaceAuto, delegate { s.PaceAuto = true; Persist(); showPace(); });
            foreach (int seconds in new[] { 1, 3, 5, 10, 30 }) { int value = seconds; DarkMenu.Item(menu, value + " s", "", !s.PaceAuto && s.PaceSeconds == value, delegate { s.PaceAuto = false; s.PaceSeconds = value; Persist(); showPace(); }); }
            menu.Show(pace, new Point(0, pace.Height + 4));
        };
        showPace(); row("Espera entre itens", "Espera entre downloads. No automático: 1 s para menos de 10 vídeos e 5 s a partir disso. Reduz o risco de bloqueio.", pace);
        var limit = new FlatButton { Kind = BtnKind.Chip, Dropdown = true, Height = Theme.S(34) };
        Action showLimit = delegate { limit.Text = s.DefaultLimit == 0 ? "Todos" : s.DefaultLimit.ToString(); };
        limit.Click += delegate
        {
            var menu = DarkMenu.Create();
            foreach (int value in new[] { 0, 10, 50 }) { int v = value; DarkMenu.Item(menu, v == 0 ? "Todos" : v.ToString(), "", s.DefaultLimit == v, delegate { s.DefaultLimit = v; Persist(); showLimit(); }); }
            menu.Show(limit, new Point(0, limit.Height + 4));
        };
        showLimit(); row("Itens por playlist", "Quantos vídeos listar ao abrir uma playlist ou coleção.", limit);

        section("LOGIN (OPCIONAL)");
        var cookies = new FlatButton { Kind = BtnKind.Chip, Dropdown = true, Height = Theme.S(34), Text = "Não usar" };
        cookies.Click += delegate
        {
            var menu = DarkMenu.Create();
            DarkMenu.Item(menu, "Não usar", "", manager.Browser == "", delegate { manager.Browser = ""; cookies.Text = "Não usar"; });
            foreach (string browser in new[] { "chrome", "edge", "firefox", "brave", "opera", "vivaldi", "chromium" })
            {
                string name = browser;
                DarkMenu.Item(menu, name, "", manager.Browser == name, delegate
                {
                    if (MessageBox.Show(this, "O yt-dlp tentará ler cookies do navegador e usá-los nas próximas consultas. Isso pode associar o acesso à sua conta e causar restrições. Cookies não corrigem suporte ausente ou quebrado.\n\nA escolha vale somente nesta sessão; valores não são salvos pelo Baixaki. Usar esse navegador?", "Autenticação opcional", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                    manager.Browser = name; cookies.Text = name;
                });
            }
            menu.Show(cookies, new Point(0, cookies.Height + 4));
        };
        row("Cookies do navegador", "Só para conteúdo que exige login. Vale apenas nesta sessão e pode associar as consultas à sua conta.", cookies);

        section("MOTOR E ATUALIZAÇÕES");
        var engineButton = new FlatButton { Kind = BtnKind.Secondary, Height = Theme.S(34), Text = "Verificar" };
        engineButton.Click += async delegate { await Guard(async delegate { Feedback("Verificando o yt-dlp…", false); await manager.UpdateEngine(); manager.State.LastEngineCheck = DateTime.UtcNow; Persist(); RefreshEngineVersion(); Feedback("yt-dlp verificado.", false); }); };
        engineVersionLabel = row("yt-dlp", "Atualiza sozinho uma vez por dia.", engineButton);
        var appButton = new FlatButton { Kind = BtnKind.Secondary, Height = Theme.S(34), Text = "Verificar" }; appButton.Click += delegate { CheckForUpdate(true); };
        row("Baixaki " + AppVersion, "Procura uma versão nova no GitHub.", appButton);

        section("DIAGNÓSTICO");
        int bx = margin;
        foreach (var entry in new[] { new KeyValuePair<string, Action>("Registro", delegate { using (var form = new LogForm(logText.ToString())) form.ShowDialog(this); }),
            new KeyValuePair<string, Action>("Dados do app", delegate { try { Process.Start(new ProcessStartInfo(manager.Store.Root) { UseShellExecute = true }); } catch { } }),
            new KeyValuePair<string, Action>("Ajuda", OpenHelp) })
        {
            var button = new FlatButton { Kind = BtnKind.Secondary, Height = Theme.S(34), Text = entry.Key, Location = new Point(bx, y) }; var action = entry.Value;
            button.Click += delegate { action(); }; settingsPanel.Controls.Add(button); bx += button.Width + Theme.S(8);
        }
        y += Theme.S(46);
        holdsButton = new FlatButton { Kind = BtnKind.Secondary, Height = Theme.S(34), Text = "Fontes em pausa", Location = new Point(margin, y), Visible = false };
        holdsButton.Click += delegate { ReleaseHolds(); };
        settingsPanel.Controls.Add(holdsButton);
        settingsRefresh.Add(delegate { RefreshEngineVersion(); });
    }
    async void RefreshEngineVersion()
    {
        if (engineVersionLabel == null) return;
        string version = "";
        try { await manager.Engine.Run("yt-dlp", new List<string> { "--version" }, manager.Engine.BaseDirectory, delegate(string line) { if (line.Trim().Length > 0) version = line.Trim(); }, delegate { }, CancellationToken.None, 20); }
        catch { }
        if (!IsDisposed) engineVersionLabel.Text = version.Length > 0 ? "yt-dlp " + version : "yt-dlp (não encontrado)";
    }
    // Quiet once-a-day engine update, only when nothing is running.
    async Task AutoUpdateEngine()
    {
        var last = manager.State.LastEngineCheck;
        if (last.HasValue && last.Value > DateTime.UtcNow.AddHours(-24)) return;
        if (manager.Running || manager.Busy || manager.Discovering || manager.Updating || manager.Analyzing > 0 || shuttingDown) return;
        try { await manager.UpdateEngine(); manager.State.LastEngineCheck = DateTime.UtcNow; manager.Save(); RefreshEngineVersion(); } catch (Exception ex) { Log("Atualização automática do yt-dlp indisponível: " + Safety.Redact(ex.Message)); }
    }
    void ReleaseHolds()
    {
        var timed = manager.State.Holds.Where(h => h.FromServer && h.Until.HasValue && h.Until.Value > DateTime.UtcNow).ToList();
        if (timed.Count > 0) { MessageBox.Show(this, "O site pediu uma espera até " + timed.Max(h => h.Until.Value).ToLocalTime().ToString("HH:mm") + ". Aguarde o prazo antes de tentar novamente.", "Fonte em pausa", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (manager.State.Holds.Count == 0) return;
        string list = String.Join("\n", manager.State.Holds.Select(h => "• " + h.Source + ": " + UiText.HoldReason(h.Reason)).ToArray());
        if (MessageBox.Show(this, "Fontes em pausa:\n" + list + "\n\nVocê já resolveu a restrição de acesso ou aguardou o limite do serviço? Liberar uma fonte não resolve a causa e repetir tentativas pode agravar a restrição.\n\nLiberar as fontes em pausa?", "Fontes em pausa", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        { manager.ReleaseHolds(); Feedback("Fontes liberadas. Use Tentar de novo nos itens bloqueados.", false); RefreshUi(); }
    }

    // ---------- hooks used by the automated UI checks ----------
    internal DownloadManager Manager { get { return manager; } }
    internal RowList ListView { get { return list; } }
    internal PillInput UrlInput { get { return urlInput; } }
    internal TabStrip Tabs { get { return tabs; } }
    internal PreviewStrip Preview { get { return preview; } }
    internal Control SettingsPanelControl { get { return settingsPanel; } }
    internal FlatButton StartButton { get { return startButton; } }
    internal FlatButton FormatChip { get { return formatChip; } }
    internal FlatButton GearButton { get { return gearButton; } }
    internal Task AnalyzeNow() { return StartAnalysis(urlInput.Box.Text.Trim()); }
    internal Task AddNow() { return AddUrl(); }
    internal void SetFormatForTest(MediaMode mode, bool premiere) { manager.State.Mode = mode; manager.State.Premiere = premiere; UpdateChips(); }
    internal void ActOnRow(RowItem row, string action) { HandleRowAction(row, action).Wait(); }
    internal void RefreshNow() { RefreshUi(); }
    internal void ToggleSettingsForTest() { ToggleSettings(); }
    internal string LogText { get { return logText.ToString(); } }
}
