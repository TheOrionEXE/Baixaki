using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Renders the real window with demonstration data and checks that the layout stays stable.
class LayoutTests
{
    static int failures;
    static void Check(bool value, string name) { Console.WriteLine((value ? "PASS: " : "FAIL: ") + name); if (!value) failures++; }
    static byte[] FakeImage(string url)
    {
        int hash = 7; foreach (char c in url) hash = hash * 31 + c;
        using (var bmp = new Bitmap(320, 180)) using (var g = Graphics.FromImage(bmp))
        {
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 320, 180), Theme.Placeholder(url), Color.FromArgb(Math.Abs(hash) % 200, 90, 160), 45f)) g.FillRectangle(brush, 0, 0, 320, 180);
            using (var stream = new MemoryStream()) { bmp.Save(stream, ImageFormat.Jpeg); return stream.ToArray(); }
        }
    }
    static void Shot(BaixakiForm form, string name)
    {
        // First pass requests the thumbnails; the second one (after they load in the background) is the picture.
        using (var warm = new Bitmap(form.Width, form.Height)) form.DrawToBitmap(warm, new Rectangle(0, 0, form.Width, form.Height));
        for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(60); }
        using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save("test-output/" + name + ".png"); }
    }
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory("test-output");
        ThumbCache.Folder = Path.GetFullPath("test-output/thumbs-layout"); ThumbCache.Fetch = FakeImage;
        var folder = Path.GetFullPath("test-output/layout-state"); if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(folder);
        var state = new DownloadState { Destination = @"C:\Users\Demo\Videos\Baixaki" };
        string completed = Path.GetFullPath("test-output/demo-completed.mp4"); File.WriteAllText(completed, "fixture");
        state.Queue.Add(new DownloadItem { Title = "Como montar um PC em 2026 — guia completo", Channel = "Canal Tech", Url = "https://example.com/a", Extractor = "Generic", Status = "Downloading", Percent = 62, Speed = "4404019", Thumb = "https://img.example/a.jpg", Duration = 760, Destination = state.Destination });
        state.Queue.Add(new DownloadItem { Title = "Podcast #214 com convidado especial", Url = "https://example.com/b", Extractor = "Generic", Status = "Pending", Mode = MediaMode.Mp3, Thumb = "https://img.example/b.jpg", Duration = 2890, Destination = state.Destination });
        state.Queue.Add(new DownloadItem { Title = "Teaser do lançamento", Url = "https://example.com/c", Extractor = "Generic", Status = "Pending", Mode = MediaMode.Gif, Thumb = "https://img.example/c.jpg", Duration = 45, Destination = state.Destination });
        state.Queue.Add(new DownloadItem { Title = "Vídeo indisponível", Url = "https://example.com/d", Extractor = "Generic", Status = "Failed", Error = "Engine failed: video is private", Destination = state.Destination });
        state.Queue.Add(new DownloadItem { Title = "Aula 03 · Cores e contraste", Channel = "Aulas de edição", Url = "https://example.com/e", Extractor = "Generic", Status = "Completed", Percent = 100, OutputFile = completed, Thumb = "https://img.example/e.jpg", Duration = 872, Destination = state.Destination });
        state.Queue.Add(new DownloadItem { Title = "Entrevista completa", Url = "https://example.com/f", Extractor = "Generic", Status = "Completed", Percent = 100, Mode = MediaMode.Mp3, OutputFile = completed, Thumb = "https://img.example/f.jpg", Duration = 838, Destination = state.Destination, Updated = DateTime.UtcNow.AddDays(-1) });
        var job = new CollectionJob { Url = "https://www.youtube.com/playlist?list=demo", Title = "Aulas de edição de vídeo", Extractor = "YoutubeTab", Status = "Discovered (extent unknown)", Destination = state.Destination, Thumb = "https://img.example/p.jpg" };
        for (int i = 1; i <= 24; i++) job.Entries.Add(new CollectionEntry { Id = "v" + i, Extractor = "Youtube", Url = "https://www.youtube.com/watch?v=v" + i, Title = "Aula " + i.ToString("00") + " · " + (i % 2 == 0 ? "Cortes e transições" : "Introdução ao fluxo de trabalho"), Kind = "Video", Thumb = "https://img.example/p" + i + ".jpg", Duration = 300 + i * 41, Skip = i == 3 });
        state.Collections.Add(job);
        var store = new StateStore(folder); store.Save(state);
        using (var form = new BaixakiForm(store, new EngineRunner()))
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            var size0 = form.ClientSize;
            form.Manager.State.Queue[0].Status = "Downloading"; // loading a saved queue pauses interrupted downloads, so mark it active here
            form.RefreshNow(); Check(form.ListView.RowCount >= 6, "queue tab lists the playlist, active, waiting and problem sections: " + form.ListView.RowCount + " rows");
            Check(form.ListView.Rows.Any(r => r.Kind == "playlist" && r.PrimaryText.StartsWith("Adicionar 23")), "playlist offers to add the 23 selected videos");
            Check(form.ListView.Rows.Any(r => r.Kind == "item" && r.Progress.HasValue && r.State == 1), "active download shows a progress bar");
            Shot(form, "layout-fila");
            // expand the playlist
            var playlist = form.ListView.Rows.First(r => r.Kind == "playlist"); form.ActOnRow(playlist, "expand"); form.RefreshNow();
            Check(form.ListView.Rows.Count(r => r.Kind == "entry") == 24, "expanded playlist lists all 24 videos with checkboxes");
            Check(form.ListView.Rows.Any(r => r.Kind == "selectall" && !r.Checked), "select-all is unchecked while one video is deselected");
            Shot(form, "layout-playlist");
            form.Tabs.Selected = 1; form.RefreshNow(); Application.DoEvents();
            Check(form.ListView.Rows.Count(r => r.Kind == "item") == 2 && form.ListView.Rows.Count(r => r.Kind == "header") == 2, "completed tab groups by day");
            Check(form.ClientSize == size0, "switching tabs does not resize the window");
            Shot(form, "layout-concluidos");
            form.Tabs.Selected = 0; form.ToggleSettingsForTest(); Application.DoEvents();
            Check(form.SettingsPanelControl.Visible && form.SettingsPanelControl.Right == form.ClientSize.Width, "settings drawer opens at the right edge");
            Check(form.ClientSize == size0, "opening settings does not resize the window");
            Shot(form, "layout-config");
            using (var panel = new Bitmap(form.SettingsPanelControl.Width, form.SettingsPanelControl.Height)) { form.SettingsPanelControl.DrawToBitmap(panel, new Rectangle(0, 0, panel.Width, panel.Height)); panel.Save("test-output/layout-config-painel.png"); }
            form.ToggleSettingsForTest();
            foreach (var size in new[] { new Size(820, 640), new Size(1400, 900) })
            {
                form.ClientSize = size; form.PerformLayout(); Application.DoEvents();
                Check(form.Tabs.Right <= form.ClientSize.Width && form.GearButton.Right <= form.ClientSize.Width, "controls stay inside the window at " + size.Width + "x" + size.Height);
                Shot(form, "layout-" + size.Width);
            }
            form.SetFormatForTest(MediaMode.Gif, false); form.ClientSize = new Size(1000, 700); form.PerformLayout(); Shot(form, "layout-gif");
        }
        Console.WriteLine(failures == 0 ? "ALL LAYOUT CHECKS PASSED" : failures + " LAYOUT CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
