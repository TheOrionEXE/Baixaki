using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Visual language of Baixaki 1.2: flat dark surfaces, one accent color, owner-drawn controls (no layout panels that reflow on resize).
public static class Theme
{
    public static readonly Color Bg = Color.FromArgb(20, 21, 26), Bar = Color.FromArgb(15, 16, 19), Panel = Color.FromArgb(26, 27, 33), Raised = Color.FromArgb(31, 33, 41),
        Line = Color.FromArgb(38, 41, 50), Line2 = Color.FromArgb(56, 60, 74), Ink = Color.FromArgb(230, 231, 234), Soft = Color.FromArgb(196, 199, 207),
        Muted = Color.FromArgb(166, 170, 183), Accent = Color.FromArgb(162, 89, 255), AccentHover = Color.FromArgb(180, 118, 255),
        Warn = Color.FromArgb(244, 150, 100), Ok = Color.FromArgb(111, 211, 168);
    // One scale factor for the whole interface: every font size and every pixel measure goes through it.
    public static float Zoom = 1.3f;
    public static int S(int pixels) { return (int)Math.Round(pixels * Zoom); }
    static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
    public static Font Font(float size, bool semibold = false)
    {
        string key = size.ToString() + (semibold ? "s" : "r");
        lock (fonts)
        {
            Font font;
            if (!fonts.TryGetValue(key, out font)) { font = new Font(semibold ? "Segoe UI Semibold" : "Segoe UI", size * Zoom, FontStyle.Regular, GraphicsUnit.Point); fonts[key] = font; }
            return font;
        }
    }
    public static Font IconFont(float size)
    {
        string key = "i" + size;
        lock (fonts)
        {
            Font font;
            if (!fonts.TryGetValue(key, out font)) { font = new Font("Segoe MDL2 Assets", size * Zoom, FontStyle.Regular, GraphicsUnit.Point); fonts[key] = font; }
            return font;
        }
    }
    // Stable muted color for videos without a thumbnail yet.
    public static Color Placeholder(string seed)
    {
        int hash = 17; foreach (char c in seed ?? "") hash = hash * 31 + c;
        double hue = Math.Abs(hash % 360) / 360.0;
        return FromHsl(hue, 0.38, 0.30);
    }
    static Color FromHsl(double h, double s, double l)
    {
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        Func<double, double> channel = delegate(double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t; if (t < 0.5) return q; if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6; return p;
        };
        return Color.FromArgb((int)(channel(h + 1.0 / 3) * 255), (int)(channel(h) * 255), (int)(channel(h - 1.0 / 3) * 255));
    }
    public static string Duration(int seconds)
    {
        if (seconds <= 0) return "";
        int h = seconds / 3600, m = seconds % 3600 / 60, s = seconds % 60;
        return h > 0 ? h + ":" + m.ToString("00") + ":" + s.ToString("00") : m + ":" + s.ToString("00");
    }
    public static string Size(long bytes)
    {
        if (bytes >= 1073741824) return (bytes / 1073741824.0).ToString("0.0") + " GB";
        if (bytes >= 1048576) return (bytes / 1048576.0).ToString("0") + " MB";
        return Math.Max(1, bytes / 1024) + " KB";
    }
}
// The icon is embedded in the program, so the window and header show it wherever Baixaki runs.
public static class AppIcon
{
    static Icon window; static Bitmap logo; static bool loaded;
    static Stream Open() { return typeof(AppIcon).Assembly.GetManifestResourceStream("Baixaki.Icon.ico"); }
    static void Load()
    {
        if (loaded) return; loaded = true;
        try { using (var stream = Open()) if (stream != null) window = new Icon(stream, new Size(32, 32)); } catch { }
        // Icon.ToBitmap garbles PNG-compressed frames, so the logo is decoded straight from the .ico directory (96 px frame).
        try
        {
            byte[] data; using (var stream = Open()) using (var memory = new MemoryStream()) { if (stream != null) { stream.CopyTo(memory); data = memory.ToArray(); } else data = new byte[0]; }
            int count = data.Length > 6 ? BitConverter.ToUInt16(data, 4) : 0, best = -1, bestSize = 0;
            for (int i = 0; i < count; i++)
            {
                int size = data[6 + i * 16]; if (size == 0) size = 256;
                if (size >= 64 && (best < 0 || size < bestSize)) { best = i; bestSize = size; }
            }
            if (best >= 0)
            {
                int length = BitConverter.ToInt32(data, 6 + best * 16 + 8), offset = BitConverter.ToInt32(data, 6 + best * 16 + 12);
                using (var frame = new MemoryStream(data, offset, length)) using (var image = new Bitmap(frame)) logo = new Bitmap(image);
            }
        }
        catch { }
        if (window == null) try { window = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
    }
    public static Icon Window { get { Load(); return window; } }
    public static Bitmap Logo { get { Load(); return logo; } }
}
public static class Glyph
{
    public const string Close = "", Play = "", Folder = "", Retry = "", Delete = "", Down = "", Up = "", Right = "",
        Settings = "", Link = "", Pause = "", Add = "", Search = "", Download = "", Check = "", Video = "";
}
public static class ModernPaint
{
    public static Color Background(Control parent) { while (parent != null) { if (parent.BackColor.A == 255) return parent.BackColor; parent = parent.Parent; } return Theme.Bg; }
    public static GraphicsPath Round(RectangleF rect, float radius)
    {
        float d = Math.Max(1, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height))); var p = new GraphicsPath();
        p.AddArc(rect.X, rect.Y, d, d, 180, 90); p.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        p.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); p.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    public static void Icon(Graphics g, string glyph, Rectangle bounds, Color color, float size)
    {
        if (glyph == Glyph.Folder)
        {
            // The system icon font renders this glyph inconsistently, so the folder is drawn as a vector.
            float k = Theme.Zoom; int w = (int)(15 * k), h = (int)(12 * k), x = bounds.X + (bounds.Width - w) / 2, y = bounds.Y + (bounds.Height - h) / 2;
            var mode = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.4f * k) { LineJoin = LineJoin.Round })
                g.DrawPolygon(pen, new[] { new PointF(x, y + k), new PointF(x + 5 * k, y + k), new PointF(x + 7 * k, y + 3 * k), new PointF(x + w, y + 3 * k), new PointF(x + w, y + h), new PointF(x, y + h) });
            g.SmoothingMode = mode; return;
        }
        TextRenderer.DrawText(g, glyph, Theme.IconFont(size), bounds, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }
    public static void Text(Graphics g, string text, Font font, Rectangle bounds, Color color, bool center = false)
    {
        TextRenderer.DrawText(g, text ?? "", font, bounds, color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | (center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
    }
    public static void Check(Graphics g, Rectangle box, bool on, bool hot)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Round(new RectangleF(box.X + 0.5f, box.Y + 0.5f, box.Width - 1, box.Height - 1), Theme.S(4)))
        {
            if (on) using (var brush = new SolidBrush(Theme.Accent)) g.FillPath(brush, path);
            else using (var pen = new Pen(hot ? Theme.Muted : Theme.Line2)) g.DrawPath(pen, path);
        }
        if (on) using (var pen = new Pen(Color.White, 2f * Theme.Zoom) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            g.DrawLines(pen, new[] { new PointF(box.X + box.Width * 0.27f, box.Y + box.Height * 0.52f), new PointF(box.X + box.Width * 0.44f, box.Y + box.Height * 0.69f), new PointF(box.X + box.Width * 0.74f, box.Y + box.Height * 0.32f) });
    }
}

public enum BtnKind { Primary, Secondary, Ghost, Chip }
public class FlatButton : Control
{
    public BtnKind Kind = BtnKind.Secondary;
    public bool Active, AutoFit = true;
    string iconGlyph = "";
    bool dropdown, hover, down;
    public string Icon { get { return iconGlyph; } set { iconGlyph = value ?? ""; if (AutoFit) Fit(); Invalidate(); } }
    public bool Dropdown { get { return dropdown; } set { dropdown = value; if (AutoFit) Fit(); Invalidate(); } }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (AutoFit) Fit(); Invalidate(); }
    public FlatButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        Cursor = Cursors.Hand; Font = Theme.Font(9.5F); Height = Theme.S(34); TabStop = true; ForeColor = Theme.Ink;
    }
    public void Fit()
    {
        int width = Theme.S(24);
        if (Icon.Length > 0) width += Theme.S(22);
        if (Text.Length > 0) width += TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, 200), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        if (Dropdown) width += Theme.S(20);
        if (Text.Length == 0 && Icon.Length > 0) width = Height;
        if (Width != width) Width = width;
    }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); if (AutoFit) Fit(); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys key) { return key == Keys.Enter || key == Keys.Space || base.IsInputKey(key); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(ModernPaint.Background(Parent));
        Color fill, border, ink = Enabled ? ForeColor : Theme.Muted;
        switch (Kind)
        {
            case BtnKind.Primary: fill = !Enabled ? Theme.Line : hover ? Theme.AccentHover : Theme.Accent; border = fill; ink = Enabled ? Color.White : Theme.Muted; break;
            case BtnKind.Ghost: fill = hover && Enabled ? Theme.Raised : Theme.Bg; border = fill; break;
            case BtnKind.Chip: fill = Theme.Raised; border = Active ? Theme.Accent : hover ? Theme.Line2 : Theme.Line; break;
            default: fill = hover && Enabled ? Theme.Line : Theme.Raised; border = Theme.Line2; break;
        }
        if (Kind == BtnKind.Ghost) fill = hover && Enabled ? Theme.Raised : ModernPaint.Background(Parent);
        if (down && Enabled) fill = ControlPaint.Dark(fill, 0.1f);
        using (var path = ModernPaint.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Theme.S(8)))
        using (var brush = new SolidBrush(fill)) using (var pen = new Pen(border))
        { g.FillPath(brush, path); if (Kind != BtnKind.Ghost) g.DrawPath(pen, path); }
        int x = Theme.S(12), right = Width - Theme.S(12);
        if (Dropdown) { ModernPaint.Icon(g, Glyph.Down, new Rectangle(Width - Theme.S(26), 0, Theme.S(18), Height), Theme.Muted, 7F); right -= Theme.S(14); }
        if (Icon.Length > 0)
        {
            Rectangle iconBox = Text.Length == 0 ? new Rectangle(0, 0, Width, Height) : new Rectangle(x - Theme.S(2), 0, Theme.S(20), Height);
            ModernPaint.Icon(g, Icon, iconBox, ink, 11F); x += Theme.S(22);
        }
        if (Text.Length > 0) ModernPaint.Text(g, Text, Font, new Rectangle(x, 0, Math.Max(4, right - x), Height), ink, Icon.Length == 0 && !Dropdown && !AutoFit);
        if (Focused && ShowFocusCues) using (var pen = new Pen(Theme.Accent)) g.DrawRectangle(pen, 2, 2, Width - 5, Height - 5);
    }
}

public class ToggleSwitch : Control
{
    bool on;
    public event EventHandler CheckedChanged;
    public ToggleSwitch() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.StandardClick, true); Size = new Size(Theme.S(38), Theme.S(22)); Cursor = Cursors.Hand; TabStop = true; }
    public bool Checked { get { return on; } set { if (on == value) return; on = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
    protected override bool IsInputKey(Keys key) { return key == Keys.Space || base.IsInputKey(key); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; } base.OnKeyDown(e); }
    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(ModernPaint.Background(Parent));
        using (var path = ModernPaint.Round(new RectangleF(0, 1, Width - 1, Height - 2), Height / 2f - 1)) using (var brush = new SolidBrush(on ? Theme.Accent : Theme.Line2)) g.FillPath(brush, path);
        int knob = Height - Theme.S(8), margin = Theme.S(4);
        using (var brush = new SolidBrush(Color.White)) g.FillEllipse(brush, on ? Width - knob - margin : margin, (Height - knob) / 2, knob, knob);
        if (Focused && ShowFocusCues) using (var pen = new Pen(Theme.Accent)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

public class TabStrip : Control
{
    string[] tabs = new string[0];
    int selected, hover = -1;
    public event EventHandler SelectedChanged;
    public TabStrip() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Height = Theme.S(36); Font = Theme.Font(10F); Cursor = Cursors.Hand; }
    internal int SelectedIndexForTest { get { return selected; } }
    public int Selected { get { return selected; } set { if (selected == value) return; selected = value; Invalidate(); if (SelectedChanged != null) SelectedChanged(this, EventArgs.Empty); } }
    public void SetTabs(params string[] labels)
    {
        if (labels.SequenceEqual(tabs)) return;
        tabs = labels; Width = TabRect(tabs.Length - 1).Right + 2; Invalidate();
    }
    int TabWidth(int index) { return TextRenderer.MeasureText(tabs[index], Font, new Size(int.MaxValue, 200), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width + Theme.S(28); }
    Rectangle TabRect(int index)
    {
        int x = 0; for (int i = 0; i < index; i++) x += TabWidth(i) + Theme.S(4);
        return new Rectangle(x, 0, tabs.Length == 0 ? 0 : TabWidth(index), Height);
    }
    int At(Point p) { for (int i = 0; i < tabs.Length; i++) if (TabRect(i).Contains(p)) return i; return -1; }
    protected override void OnMouseMove(MouseEventArgs e) { int at = At(e.Location); if (at != hover) { hover = at; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { int at = At(e.Location); if (at >= 0) Selected = at; base.OnMouseDown(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(ModernPaint.Background(Parent));
        for (int i = 0; i < tabs.Length; i++)
        {
            Rectangle r = TabRect(i);
            if (i == selected || i == hover)
                using (var path = ModernPaint.Round(new RectangleF(r.X + 0.5f, r.Y + Theme.S(2) + 0.5f, r.Width - 1, r.Height - Theme.S(4) - 1), Theme.S(8))) using (var brush = new SolidBrush(i == selected ? Theme.Line : Theme.Raised)) g.FillPath(brush, path);
            ModernPaint.Text(g, tabs[i], Font, r, i == selected ? Theme.Ink : Theme.Muted, true);
        }
    }
}

// Rounded input with the primary action inside it, so the main task is one line: paste, press Enter.
public class PillInput : Control
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, string lParam);
    public readonly TextBox Box;
    public readonly FlatButton Button;
    string placeholder = "", icon = Glyph.Link;
    bool showButton = true;
    public event EventHandler Submit;
    public string IconGlyph { get { return icon; } set { icon = value; Invalidate(); } }
    public bool ShowButton { get { return showButton; } set { showButton = value; Button.Visible = value; LayoutInner(); Invalidate(); } }
    public PillInput()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.ContainerControl, true);
        Height = Theme.S(50); BackColor = Theme.Bg;
        Box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Raised, ForeColor = Theme.Ink, Font = Theme.Font(11F), MaxLength = 2048 };
        Button = new FlatButton { Kind = BtnKind.Primary, Text = "Adicionar", Height = Theme.S(36), Font = Theme.Font(10F, true) };
        Controls.Add(Box); Controls.Add(Button);
        Box.GotFocus += delegate { Invalidate(); }; Box.LostFocus += delegate { Invalidate(); };
        Box.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; e.Handled = true; if (Submit != null) Submit(this, EventArgs.Empty); } };
        Box.HandleCreated += delegate { ApplyPlaceholder(); };
        LayoutInner();
    }
    public string Placeholder { get { return placeholder; } set { placeholder = value; ApplyPlaceholder(); } }
    void ApplyPlaceholder() { if (Box.IsHandleCreated) SendMessage(Box.Handle, 0x1501, IntPtr.Zero, placeholder); }
    void LayoutInner()
    {
        if (Box == null || Button == null) return;
        Button.Fit(); Button.Width += Theme.S(14); Button.Location = new Point(Width - Button.Width - Theme.S(7), (Height - Button.Height) / 2);
        Box.Location = new Point(icon.Length > 0 ? Theme.S(48) : Theme.S(18), (Height - Box.PreferredHeight) / 2);
        Box.Width = Math.Max(40, (showButton ? Button.Left - Theme.S(10) : Width - Theme.S(16)) - Box.Left);
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Theme.Bg);
        using (var path = ModernPaint.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Height / 2f)) using (var brush = new SolidBrush(Theme.Raised)) using (var pen = new Pen(Box.Focused ? Theme.Accent : Theme.Line2))
        { g.FillPath(brush, path); g.DrawPath(pen, path); }
        if (icon.Length > 0) ModernPaint.Icon(g, icon, new Rectangle(Theme.S(14), 0, Theme.S(28), Height), Theme.Muted, 12F);
    }
    protected override void OnMouseDown(MouseEventArgs e) { Box.Focus(); base.OnMouseDown(e); }
}

public class RowItem
{
    public string Key = "", Kind = "item", Title = "", Sub = "", Right = "", ThumbUrl = "", Badge = "", PrimaryText = "";
    public double? Progress;
    public int State; // 0 neutral, 1 active, 2 problem, 3 done
    public bool HasCheck, Checked, Expanded;
    public string[] Actions = new string[0];
    public object Tag;
    internal int Top, Height;
    public string Signature()
    {
        return String.Join("\u001f", new[] { Key, Kind, Title, Sub, Right, ThumbUrl ?? "", Badge, PrimaryText, Progress.HasValue ? Progress.Value.ToString("0.0") : "-", State.ToString(), HasCheck ? "1" : "0", Checked ? "1" : "0", Expanded ? "1" : "0", String.Join(",", Actions) });
    }
}

// Thumbnails load off the UI thread, are cached on disk, and never block painting.
public static class ThumbCache
{
    static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
    static readonly HashSet<string> pending = new HashSet<string>(), failed = new HashSet<string>();
    public static event Action Loaded;
    public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baixaki", "thumbs");
    public static Func<string, byte[]> Fetch = DownloadBytes;
    public static string Normalize(string url)
    {
        if (url == null) return "";
        // GDI+ cannot decode WebP; YouTube serves the same image as JPEG under /vi/.
        if (url.IndexOf("/vi_webp/", StringComparison.OrdinalIgnoreCase) >= 0) url = url.Replace("/vi_webp/", "/vi/").Replace(".webp", ".jpg");
        return url;
    }
    public static Bitmap Get(string url)
    {
        url = Normalize(url);
        if (url.Length == 0 || !(url.StartsWith("https://") || url.StartsWith("http://"))) return null;
        lock (cache)
        {
            Bitmap bmp;
            if (cache.TryGetValue(url, out bmp)) return bmp;
            if (pending.Contains(url) || failed.Contains(url)) return null;
            pending.Add(url);
        }
        ThreadPool.QueueUserWorkItem(delegate { Load(url); });
        return null;
    }
    public static void CleanOld(int days)
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            foreach (string file in Directory.GetFiles(Folder)) if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-days)) File.Delete(file);
        }
        catch { }
    }
    static byte[] DownloadBytes(string url)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Timeout = 8000; request.ReadWriteTimeout = 8000; request.UserAgent = "Baixaki";
        using (var response = (HttpWebResponse)request.GetResponse())
        {
            if (response.ContentLength > 4 * 1024 * 1024) throw new IOException("Thumbnail too large");
            using (var stream = response.GetResponseStream()) using (var memory = new MemoryStream())
            {
                var buffer = new byte[16384]; int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) { memory.Write(buffer, 0, read); if (memory.Length > 4 * 1024 * 1024) throw new IOException("Thumbnail too large"); }
                return memory.ToArray();
            }
        }
    }
    static void Load(string url)
    {
        Bitmap result = null;
        try
        {
            string name;
            using (var sha = SHA1.Create()) name = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", "");
            string path = Path.Combine(Folder, name + ".img");
            byte[] data = null;
            try { if (File.Exists(path)) data = File.ReadAllBytes(path); } catch { }
            if (data == null)
            {
                data = Fetch(url);
                try { Directory.CreateDirectory(Folder); File.WriteAllBytes(path, data); } catch { }
            }
            using (var memory = new MemoryStream(data)) using (var image = Image.FromStream(memory))
            {
                // Keep a small 16:9 copy: rows never need more than ~160 px.
                result = new Bitmap(256, 144);
                using (var g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    double scale = Math.Max(256.0 / image.Width, 144.0 / image.Height);
                    int w = (int)(image.Width * scale), h = (int)(image.Height * scale);
                    g.DrawImage(image, (256 - w) / 2, (144 - h) / 2, w, h);
                }
            }
        }
        catch { result = null; }
        lock (cache)
        {
            pending.Remove(url);
            if (result == null) failed.Add(url);
            else { if (cache.Count > 600) { foreach (var old in cache.Values) old.Dispose(); cache.Clear(); } cache[url] = result; }
        }
        Action handler = Loaded; if (handler != null && result != null) handler();
    }
}

// Owner-drawn list: only visible rows are painted, so large playlists stay smooth and resizing never reflows controls.
public class RowList : Control
{
    static readonly int ItemH = Theme.S(78), PlaylistH = Theme.S(84), EntryH = Theme.S(56), HeaderH = Theme.S(38), SelectH = Theme.S(44), Pad = Theme.S(14), Bar = Theme.S(12);
    List<RowItem> rows = new List<RowItem>();
    string[] signatures = new string[0];
    int scroll, contentHeight, hoverRow = -1, dragOffset;
    string hoverAction = "", tipText = "";
    bool dragging;
    readonly ToolTip tip = new ToolTip { InitialDelay = 500, ReshowDelay = 100 };
    public string EmptyTitle = "", EmptySub = "";
    public event Action<RowItem, string> ActionInvoked;
    struct Hit { public Rectangle R; public string Action; }
    static readonly Dictionary<string, string> labels = new Dictionary<string, string> {
        { "cancel", "Cancelar download" }, { "retry", "Tentar de novo" }, { "play", "Abrir arquivo" }, { "folder", "Mostrar na pasta" },
        { "remove", "Remover da lista" }, { "expand", "Mostrar ou ocultar os vídeos" }, { "check", "Marcar ou desmarcar" } };
    readonly Action loadedHandler;
    public RowList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Theme.Bg; DoubleBuffered = true;
        loadedHandler = delegate { try { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(Invalidate)); } catch { } };
        ThumbCache.Loaded += loadedHandler;
    }
    protected override void Dispose(bool disposing) { ThumbCache.Loaded -= loadedHandler; tip.Dispose(); base.Dispose(disposing); }
    public int RowCount { get { return rows.Count; } }
    public RowItem RowAt(int index) { return rows[index]; }
    public IList<RowItem> Rows { get { return rows; } }
    static int HeightOf(RowItem row)
    {
        switch (row.Kind) { case "playlist": return PlaylistH; case "entry": return EntryH; case "header": return HeaderH; case "selectall": return SelectH; default: return ItemH; }
    }
    int ViewHeight { get { return Height; } }
    int ViewWidth { get { return Width - (contentHeight > ViewHeight ? Bar : 0); } }
    public void SetRows(List<RowItem> next)
    {
        var nextSigs = next.Select(r => r.Signature()).ToArray();
        bool sameShape = nextSigs.Length == signatures.Length;
        int top = 0;
        for (int i = 0; i < next.Count; i++)
        {
            next[i].Height = HeightOf(next[i]); next[i].Top = top; top += next[i].Height;
            if (sameShape && rows[i].Height != next[i].Height) sameShape = false;
        }
        var old = rows; rows = next; signatures = nextSigs; contentHeight = top;
        int max = Math.Max(0, contentHeight - ViewHeight); if (scroll > max) scroll = max;
        if (hoverRow >= rows.Count) hoverRow = -1;
        if (!sameShape) { Invalidate(); return; }
        for (int i = 0; i < rows.Count; i++) if (old[i].Signature() != rows[i].Signature()) Invalidate(RowRect(i));
    }
    Rectangle RowRect(int index) { return new Rectangle(0, rows[index].Top - scroll, Width, rows[index].Height); }
    int IndexAt(int y)
    {
        y += scroll;
        for (int i = 0; i < rows.Count; i++) if (y >= rows[i].Top && y < rows[i].Top + rows[i].Height) return i;
        return -1;
    }
    List<Hit> LayoutRow(RowItem row, int y, int width, out Rectangle thumb, out Rectangle check, out Rectangle text, out Rectangle status)
    {
        var hits = new List<Hit>(); int h = row.Height, right = width - Pad, x = Pad, a = Theme.S(30);
        for (int i = row.Actions.Length - 1; i >= 0; i--)
        {
            var rect = new Rectangle(right - a, y + (h - a) / 2, a, a);
            hits.Add(new Hit { R = rect, Action = row.Actions[i] }); right -= a + Theme.S(4);
        }
        if (row.PrimaryText.Length > 0)
        {
            int w = TextRenderer.MeasureText(row.PrimaryText, Theme.Font(9F, true), new Size(int.MaxValue, 200), TextFormatFlags.NoPadding).Width + Theme.S(28), bh = Theme.S(32);
            hits.Add(new Hit { R = new Rectangle(right - w, y + (h - bh) / 2, w, bh), Action = "primary" }); right -= w + Theme.S(10);
        }
        status = Rectangle.Empty;
        if (row.Progress.HasValue) { int sw = Theme.S(150); status = new Rectangle(right - sw, y, sw, h); right -= sw + Theme.S(12); }
        else if (row.Right.Length > 0)
        {
            int w = Math.Min(Theme.S(170), TextRenderer.MeasureText(row.Right, Theme.Font(9F), new Size(int.MaxValue, 200), TextFormatFlags.NoPadding).Width + Theme.S(6));
            status = new Rectangle(right - w, y, w, h); right -= w + Theme.S(12);
        }
        check = Rectangle.Empty; thumb = Rectangle.Empty;
        if (row.Kind == "entry" || row.Kind == "selectall") x += Theme.S(24);
        if (row.HasCheck) { int c = Theme.S(20); check = new Rectangle(x, y + (h - c) / 2, c, c); x += c + Theme.S(12); }
        if (row.Kind == "item" || row.Kind == "playlist") { int th = Theme.S(54), tw = th * 16 / 9; thumb = new Rectangle(x, y + (h - th) / 2, tw, th); x += tw + Theme.S(16); }
        else if (row.Kind == "entry") { int th = Theme.S(38), tw = th * 16 / 9; thumb = new Rectangle(x, y + (h - th) / 2, tw, th); x += tw + Theme.S(14); }
        text = new Rectangle(x, y, Math.Max(40, right - x), h);
        return hits;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Theme.Bg); g.SmoothingMode = SmoothingMode.AntiAlias;
        var card = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using (var path = ModernPaint.Round(card, 10))
        {
            using (var brush = new SolidBrush(Theme.Panel)) g.FillPath(brush, path);
            var state = g.Save(); g.SetClip(path, CombineMode.Intersect);
            if (rows.Count == 0) PaintEmpty(g);
            else
            {
                int width = ViewWidth;
                for (int i = 0; i < rows.Count; i++)
                {
                    int y = rows[i].Top - scroll;
                    if (y + rows[i].Height < e.ClipRectangle.Top) continue;
                    if (y > e.ClipRectangle.Bottom || y > Height) break;
                    PaintRow(g, rows[i], i, y, width);
                }
                PaintScrollbar(g);
            }
            g.Restore(state);
            using (var pen = new Pen(Theme.Line)) g.DrawPath(pen, path);
        }
    }
    void PaintEmpty(Graphics g)
    {
        ModernPaint.Icon(g, Glyph.Download, new Rectangle(0, Height / 2 - Theme.S(70), Width, Theme.S(48)), Theme.Line2, 26F);
        ModernPaint.Text(g, EmptyTitle, Theme.Font(11.5F, true), new Rectangle(20, Height / 2 - Theme.S(20), Width - 40, Theme.S(30)), Theme.Soft, true);
        ModernPaint.Text(g, EmptySub, Theme.Font(10F), new Rectangle(20, Height / 2 + Theme.S(10), Width - 40, Theme.S(28)), Theme.Muted, true);
    }
    void PaintScrollbar(Graphics g)
    {
        if (contentHeight <= ViewHeight) return;
        int m = Theme.S(8), track = ViewHeight - 2 * m, thumb = Math.Max(Theme.S(32), (int)((long)track * ViewHeight / contentHeight));
        int y = m + (int)((long)(track - thumb) * scroll / Math.Max(1, contentHeight - ViewHeight));
        using (var path = ModernPaint.Round(new RectangleF(Width - Theme.S(10), y, Theme.S(6), thumb), Theme.S(3))) using (var brush = new SolidBrush(dragging ? Theme.Muted : Theme.Line2)) g.FillPath(brush, path);
    }
    void PaintRow(Graphics g, RowItem row, int index, int y, int width)
    {
        Rectangle thumb, check, text, status;
        var hits = LayoutRow(row, y, width, out thumb, out check, out text, out status);
        bool hot = index == hoverRow && row.Kind != "header";
        if (row.Kind == "header")
        {
            ModernPaint.Text(g, row.Title, Theme.Font(8.5F, true), new Rectangle(Theme.S(16), y + Theme.S(8), width - Theme.S(32), HeaderH - Theme.S(8)), Theme.Muted);
            return;
        }
        if (hot) using (var brush = new SolidBrush(Theme.Raised)) g.FillRectangle(brush, 0, y, width, row.Height);
        using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, Pad, y + row.Height - 1, width - Pad, y + row.Height - 1);
        if (row.HasCheck) ModernPaint.Check(g, check, row.Checked, hot && hoverAction == "check");
        if (!thumb.IsEmpty) PaintThumb(g, row, thumb);
        bool two = row.Sub.Length > 0 && row.Kind != "selectall";
        Font titleFont = Theme.Font(row.Kind == "entry" ? 10F : 10.5F);
        if (two)
        {
            int lh = Theme.S(21);
            ModernPaint.Text(g, row.Title, titleFont, new Rectangle(text.X, y + row.Height / 2 - lh, text.Width, lh), Theme.Ink);
            ModernPaint.Text(g, row.Sub, Theme.Font(9F), new Rectangle(text.X, y + row.Height / 2 + Theme.S(1), text.Width, lh), row.State == 2 ? Theme.Warn : Theme.Muted);
        }
        else ModernPaint.Text(g, row.Title, titleFont, text, row.Kind == "selectall" ? Theme.Soft : Theme.Ink);
        if (row.Progress.HasValue)
        {
            var bar = new Rectangle(status.X, y + row.Height / 2 - Theme.S(3), Theme.S(100), Theme.S(6));
            using (var path = ModernPaint.Round(bar, bar.Height / 2f)) using (var brush = new SolidBrush(Theme.Line)) g.FillPath(brush, path);
            int fill = (int)(bar.Width * Math.Max(0, Math.Min(100, row.Progress.Value)) / 100);
            if (fill > 0) using (var path = ModernPaint.Round(new Rectangle(bar.X, bar.Y, Math.Max(bar.Height, fill), bar.Height), bar.Height / 2f)) using (var brush = new SolidBrush(Theme.Accent)) g.FillPath(brush, path);
            ModernPaint.Text(g, Math.Round(row.Progress.Value).ToString("0") + "%", Theme.Font(9F), new Rectangle(status.X + Theme.S(106), y, Theme.S(44), row.Height), Theme.Muted);
        }
        else if (!status.IsEmpty)
            ModernPaint.Text(g, row.Right, Theme.Font(9.5F), status, row.State == 1 ? Theme.Accent : row.State == 2 ? Theme.Warn : row.State == 3 ? Theme.Ok : Theme.Muted);
        foreach (Hit hit in hits)
        {
            bool over = index == hoverRow && hoverAction == hit.Action;
            if (hit.Action == "primary")
            {
                using (var path = ModernPaint.Round(new RectangleF(hit.R.X + 0.5f, hit.R.Y + 0.5f, hit.R.Width - 1, hit.R.Height - 1), Theme.S(8))) using (var brush = new SolidBrush(over ? Theme.AccentHover : Theme.Accent)) g.FillPath(brush, path);
                ModernPaint.Text(g, row.PrimaryText, Theme.Font(9F, true), hit.R, Color.White, true);
                continue;
            }
            if (over) using (var path = ModernPaint.Round(new RectangleF(hit.R.X, hit.R.Y, hit.R.Width, hit.R.Height), Theme.S(7))) using (var brush = new SolidBrush(Theme.Line2)) g.FillPath(brush, path);
            string glyph = hit.Action == "cancel" || hit.Action == "remove" ? Glyph.Close : hit.Action == "retry" ? Glyph.Retry : hit.Action == "play" ? Glyph.Play : hit.Action == "folder" ? Glyph.Folder : row.Expanded ? Glyph.Up : Glyph.Down;
            ModernPaint.Icon(g, glyph, hit.R, over ? Theme.Ink : Theme.Muted, hit.Action == "expand" ? 9F : 11F);
        }
    }
    void PaintThumb(Graphics g, RowItem row, Rectangle r)
    {
        if (row.Kind == "playlist") using (var pen = new Pen(Theme.Line2, 2f * Theme.Zoom)) { g.DrawLine(pen, r.X + Theme.S(6), r.Y - Theme.S(4), r.Right - Theme.S(6), r.Y - Theme.S(4)); }
        Bitmap bmp = ThumbCache.Get(row.ThumbUrl);
        using (var path = ModernPaint.Round(r, Theme.S(5)))
        {
            if (bmp != null)
            {
                var state = g.Save(); g.SetClip(path, CombineMode.Intersect); g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(bmp, r); g.Restore(state);
            }
            else
            {
                using (var brush = new SolidBrush(Theme.Placeholder(row.Title))) g.FillPath(brush, path);
                ModernPaint.Icon(g, row.Kind == "playlist" ? Glyph.Video : Glyph.Play, r, Color.FromArgb(150, 255, 255, 255), r.Height > 30 ? 12F : 9F);
            }
        }
        if (row.Badge.Length > 0 && r.Width >= 60)
        {
            Size size = TextRenderer.MeasureText(row.Badge, Theme.Font(8F), new Size(int.MaxValue, 200), TextFormatFlags.NoPadding);
            var badge = new Rectangle(r.Right - size.Width - Theme.S(11), r.Bottom - size.Height - Theme.S(5), size.Width + Theme.S(8), size.Height + Theme.S(2));
            using (var path = ModernPaint.Round(badge, Theme.S(3))) using (var brush = new SolidBrush(Color.FromArgb(200, 0, 0, 0))) g.FillPath(brush, path);
            ModernPaint.Text(g, row.Badge, Theme.Font(8F), badge, Color.White, true);
        }
    }
    string ActionAt(RowItem row, int y, Point p, out bool body)
    {
        Rectangle thumb, check, text, status; body = false;
        foreach (Hit hit in LayoutRow(row, y, ViewWidth, out thumb, out check, out text, out status)) if (hit.R.Contains(p)) return hit.Action;
        if (row.HasCheck && check.Inflate2(Theme.S(6)).Contains(p)) return "check";
        if (row.Kind == "playlist") { body = true; return "expand"; }
        if (row.Kind == "entry" || row.Kind == "selectall") { body = true; return "check"; }
        return "";
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragging)
        {
            int m = Theme.S(8), track = ViewHeight - 2 * m, thumb = Math.Max(Theme.S(32), (int)((long)track * ViewHeight / contentHeight));
            int y = Math.Max(0, Math.Min(track - thumb, e.Y - m - dragOffset));
            scroll = (int)((long)y * (contentHeight - ViewHeight) / Math.Max(1, track - thumb)); Invalidate(); return;
        }
        int index = IndexAt(e.Y); string action = ""; bool body = false;
        if (index >= 0) action = ActionAt(rows[index], rows[index].Top - scroll, e.Location, out body);
        if (index != hoverRow || action != hoverAction)
        {
            int old = hoverRow; hoverRow = index; hoverAction = action;
            if (old >= 0 && old < rows.Count) Invalidate(RowRect(old));
            if (index >= 0) Invalidate(RowRect(index));
            Cursor = action.Length > 0 ? Cursors.Hand : Cursors.Default;
            string text = labels.ContainsKey(action) && !body ? labels[action] : "";
            if (text != tipText) { tipText = text; tip.SetToolTip(this, text); }
        }
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        int old = hoverRow; hoverRow = -1; hoverAction = ""; if (old >= 0 && old < rows.Count) Invalidate(RowRect(old));
        base.OnMouseLeave(e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (contentHeight > ViewHeight && e.X >= Width - Theme.S(16))
        {
            int m = Theme.S(8), track = ViewHeight - 2 * m, thumb = Math.Max(Theme.S(32), (int)((long)track * ViewHeight / contentHeight));
            int top = m + (int)((long)(track - thumb) * scroll / Math.Max(1, contentHeight - ViewHeight));
            dragging = true; dragOffset = e.Y >= top && e.Y <= top + thumb ? e.Y - top : thumb / 2; Capture = true; OnMouseMove(e); return;
        }
        int index = IndexAt(e.Y); if (index < 0) return;
        bool body; string action = ActionAt(rows[index], rows[index].Top - scroll, e.Location, out body);
        if (action.Length > 0 && ActionInvoked != null) ActionInvoked(rows[index], action);
    }
    protected override void OnMouseUp(MouseEventArgs e) { if (dragging) { dragging = false; Capture = false; Invalidate(); } base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int max = Math.Max(0, contentHeight - ViewHeight), next = Math.Max(0, Math.Min(max, scroll - e.Delta / 120 * Theme.S(56)));
        if (next != scroll) { scroll = next; Invalidate(); }
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e); int max = Math.Max(0, contentHeight - ViewHeight); if (scroll > max) scroll = max; Invalidate();
    }
}
// Lets the mouse wheel scroll the list under the cursor even while the URL box has the keyboard focus.
public class WheelRedirect : IMessageFilter
{
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != 0x020A) return false;
        long l = m.LParam.ToInt64();
        var point = new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF));
        IntPtr target = WindowFromPoint(point);
        if (target == IntPtr.Zero || target == m.HWnd || !(Control.FromHandle(target) is RowList)) return false;
        SendMessage(target, m.Msg, m.WParam, m.LParam); return true;
    }
}
public static class RectExtensions
{
    public static Rectangle Inflate2(this Rectangle rect, int amount) { rect.Inflate(amount, amount); return rect; }
}

public class DarkColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Theme.Raised; } }
    public override Color MenuBorder { get { return Theme.Line2; } }
    public override Color MenuItemBorder { get { return Color.Transparent; } }
    public override Color MenuItemSelected { get { return Theme.Line; } }
    public override Color MenuItemSelectedGradientBegin { get { return Theme.Line; } }
    public override Color MenuItemSelectedGradientEnd { get { return Theme.Line; } }
    public override Color ImageMarginGradientBegin { get { return Theme.Raised; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.Raised; } }
    public override Color ImageMarginGradientEnd { get { return Theme.Raised; } }
    public override Color SeparatorDark { get { return Theme.Line; } }
    public override Color SeparatorLight { get { return Theme.Line; } }
    public override Color CheckBackground { get { return Theme.Accent; } }
    public override Color CheckSelectedBackground { get { return Theme.Accent; } }
    public override Color CheckPressedBackground { get { return Theme.Accent; } }
}
public class DarkRenderer : ToolStripProfessionalRenderer
{
    public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Theme.Ink : Theme.Muted; base.OnRenderItemText(e); }
}
public static class DarkMenu
{
    public static ContextMenuStrip Create()
    {
        return new ContextMenuStrip { Renderer = new DarkRenderer(), BackColor = Theme.Raised, ForeColor = Theme.Ink, Font = Theme.Font(9.5F), ShowImageMargin = true };
    }
    public static void Header(ContextMenuStrip menu, string text)
    {
        menu.Items.Add(new ToolStripMenuItem(text) { Enabled = false, Font = Theme.Font(8F, true) });
    }
    public static void Item(ContextMenuStrip menu, string text, string detail, bool isChecked, Action action)
    {
        var item = new ToolStripMenuItem(text) { Checked = isChecked, ShortcutKeyDisplayString = detail, ForeColor = Theme.Ink };
        item.Click += delegate { action(); };
        menu.Items.Add(item);
    }
}

public class GifDialog : Form
{
    readonly NumericUpDown start, length, width;
    public GifDialog(int startSeconds, int lengthSeconds, int widthPixels)
    {
        Text = "Trecho do GIF"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Panel; ForeColor = Theme.Ink; Font = Theme.Font(10F); ClientSize = new Size(Theme.S(400), Theme.S(250));
        Controls.Add(new Label { Text = "Escolha o pedaço do vídeo que vira GIF. Trechos curtos geram arquivos menores.", Location = new Point(Theme.S(20), Theme.S(16)), Size = new Size(Theme.S(360), Theme.S(48)), ForeColor = Theme.Muted });
        start = Field("Começa em (segundos)", 70, 0, 86400, startSeconds); length = Field("Duração (segundos)", 112, 1, 60, lengthSeconds); width = Field("Largura (pixels)", 154, 120, 1280, widthPixels);
        var ok = new FlatButton { Kind = BtnKind.Primary, Text = "Salvar", Location = new Point(0, Theme.S(200)) };
        var cancel = new FlatButton { Kind = BtnKind.Secondary, Text = "Cancelar", Location = new Point(0, Theme.S(200)) };
        Controls.Add(ok); Controls.Add(cancel); AcceptButton = null; CancelButton = null;
        ok.Click += delegate { DialogResult = DialogResult.OK; }; cancel.Click += delegate { DialogResult = DialogResult.Cancel; };
        ok.Left = ClientSize.Width - Theme.S(20) - ok.Width; cancel.Left = ok.Left - Theme.S(10) - cancel.Width;
    }
    NumericUpDown Field(string label, int y, int min, int max, int value)
    {
        y = Theme.S(y);
        Controls.Add(new Label { Text = label, Location = new Point(Theme.S(20), y + Theme.S(5)), AutoSize = true });
        var box = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Location = new Point(ClientSize.Width - Theme.S(20) - Theme.S(120), y), Width = Theme.S(120), BackColor = Theme.Raised, ForeColor = Theme.Ink };
        Controls.Add(box); return box;
    }
    public int StartSeconds { get { return (int)start.Value; } }
    public int LengthSeconds { get { return (int)length.Value; } }
    public int WidthPixels { get { return (int)width.Value; } }
}

public class LogForm : Form
{
    public LogForm(string text)
    {
        Text = "Registro técnico"; StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; ClientSize = new Size(Theme.S(760), Theme.S(440)); ShowInTaskbar = false;
        var box = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Theme.Bg, ForeColor = Theme.Soft, Font = new Font("Consolas", 10F * Theme.Zoom), BorderStyle = BorderStyle.None, Text = text };
        Controls.Add(box); Shown += delegate { box.SelectionStart = box.TextLength; box.ScrollToCaret(); };
    }
}

// Dark system scrollbars for panels that scroll (Windows 10 1809 and newer; older versions keep the light bar).
public static class DarkScroll
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr handle, string appName, string idList);
    public static void Apply(Control control) { try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); } catch { } }
}

public class BufferedPanel : Panel
{
    public BufferedPanel() { DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
}
