using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Drives the real window with a simulated engine: paste a link, analyze, add, playlists, tabs, formats.
class UiWorkflowTests
{
    static int passed, failed;
    static void Check(bool value, string name) { Console.WriteLine((value ? "PASS: " : "FAIL: ") + name); if (value) passed++; else failed++; }
    static void Pump(Task task, int timeoutMs = 20000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!task.IsCompleted) { Application.DoEvents(); Thread.Sleep(5); if (DateTime.UtcNow > end) throw new TimeoutException("UI task did not finish"); }
        task.GetAwaiter().GetResult();
    }
    static void PumpUntil(Func<bool> condition, int timeoutMs = 20000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition()) { Application.DoEvents(); Thread.Sleep(5); if (DateTime.UtcNow > end) throw new TimeoutException("Condition not reached"); }
    }
    static BaixakiForm Open(SimulatedEngine engine, string name)
    {
        string folder = Path.GetFullPath("test-output/ui-state/" + name); if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(folder);
        var form = new BaixakiForm(new StateStore(folder), engine);
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
        form.Manager.State.Destination = folder; form.Manager.State.PaceAuto = false; form.Manager.State.PaceSeconds = 0;
        return form;
    }
    static void Paste(BaixakiForm form, string url) { form.UrlInput.Box.Text = url; Application.DoEvents(); }
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        ThumbCache.Folder = Path.GetFullPath("test-output/thumbs-ui"); ThumbCache.Fetch = delegate(string url) { throw new IOException("offline test"); };
        try { Run(); } catch (Exception ex) { Console.WriteLine("FAIL: " + ex); failed++; }
        Console.WriteLine(failed == 0 ? "ALL " + passed + " UI WORKFLOW CHECKS PASSED" : failed + " UI WORKFLOW CHECKS FAILED");
        return failed == 0 ? 0 : 1;
    }
    static void Run()
    {
        // 1. single video: paste, analyze, add, download
        {
            var engine = new SimulatedEngine { HoldDownload = true };
            using (var form = Open(engine, "single"))
            {
                Size size = form.ClientSize;
                Paste(form, "https://media.example/video"); Pump(form.AnalyzeNow());
                Check(form.Preview.Title == "Resource" && form.Preview.Sub.Contains("Vídeo"), "pasting a link shows the recognized title and kind");
                Check(!engine.LastArguments.Contains("--sleep-requests"), "analysis runs without the wait between requests");
                Pump(form.AddNow());
                Check(form.Manager.State.Queue.Count == 1 && form.UrlInput.Box.Text.Length == 0, "adding queues the video and clears the input");
                PumpUntil(delegate { return form.Manager.Running && form.Manager.State.Queue[0].Status == "Downloading"; });
                form.RefreshNow();
                Check(form.ListView.Rows.Any(r => r.Kind == "header" && r.Title == "BAIXANDO") && form.ListView.Rows.Any(r => r.Kind == "item" && r.State == 1), "download starts automatically and appears under BAIXANDO");
                Check(form.StartButton.Text == "Pausar", "the main button becomes Pausar while downloading");
                Check(form.ClientSize == size, "adding and downloading never resize the window");
                form.Manager.Pause(); PumpUntil(delegate { return !form.Manager.Running && !form.Manager.Busy; });
            }
        }
        // 1b. a link that is certainly one video joins the queue at once; title and details arrive afterwards
        {
            var engine = new SimulatedEngine { HoldDownload = true, AnalysisDelay = 400 };
            using (var form = Open(engine, "instant"))
            {
                Paste(form, "https://www.youtube.com/watch?v=abc123");
                var adding = form.AddNow();
                Check(form.Manager.State.Queue.Count == 1 && String.IsNullOrEmpty(form.Manager.State.Queue[0].Title), "a clear single-video link is queued immediately, before any analysis");
                form.RefreshNow();
                Check(form.ListView.Rows.Any(r => r.Kind == "item" && r.Title == "Carregando informações…"), "the row says the details are loading");
                Pump(adding);
                Check(form.Manager.State.Queue[0].Title == "Resource" && form.Manager.State.Queue[0].Extractor == "Example", "title and details fill in when the analysis arrives");
                form.Manager.Pause(); PumpUntil(delegate { return !form.Manager.Running && !form.Manager.Busy; });
            }
        }
        // 2. unsupported Instagram profile is explained and never reaches the engine
        {
            var engine = new SimulatedEngine();
            using (var form = Open(engine, "instagram"))
            {
                int before = engine.Calls;
                Paste(form, "https://www.instagram.com/someuser/"); Pump(form.AnalyzeNow());
                Check(form.Preview.Sub.Contains("Instagram") && engine.Calls == before, "Instagram profile shows a clear message without any request");
                Pump(form.AddNow());
                Check(form.Manager.State.Queue.Count == 0 && form.Manager.State.Collections.Count == 0, "nothing is queued for an unsupported profile");
            }
        }
        // 3. playlist: auto-list, choose videos, add, completed tab
        {
            var engine = new SimulatedEngine { Playlist = true, Entries = 5, Extractor = "Youtube" };
            using (var form = Open(engine, "playlist"))
            {
                Paste(form, "https://www.youtube.com/playlist?list=abc"); Pump(form.AnalyzeNow());
                Check(form.Preview.Sub.Contains("Playlist"), "a playlist link is recognized");
                Pump(form.AddNow()); form.RefreshNow();
                var job = form.Manager.State.Collections.Single();
                Check(job.Limit == 0 && job.Entries.Count == 5, "playlist lists every video by default (limit: all)");
                var playlist = form.ListView.Rows.Single(r => r.Kind == "playlist");
                Check(playlist.PrimaryText == "Adicionar 5 à fila" && playlist.Expanded, "playlist row offers to add all videos and starts expanded");
                var entries = form.ListView.Rows.Where(r => r.Kind == "entry").ToList();
                Check(entries.Count == 5 && entries.All(r => r.Checked), "all five videos start selected");
                form.ActOnRow(entries[1], "check"); form.RefreshNow();
                Check(form.ListView.Rows.Single(r => r.Kind == "playlist").PrimaryText == "Adicionar 4 à fila", "unchecking a video updates the count");
                form.ActOnRow(form.ListView.Rows.Single(r => r.Kind == "selectall"), "check"); form.RefreshNow();
                Check(form.ListView.Rows.Single(r => r.Kind == "playlist").PrimaryText == "Adicionar 5 à fila", "select all re-marks every video");
                form.ActOnRow(form.ListView.Rows.First(r => r.Kind == "entry"), "check"); form.RefreshNow();
                form.ActOnRow(form.ListView.Rows.Single(r => r.Kind == "playlist"), "primary");
                PumpUntil(delegate { return form.Manager.State.Queue.Count == 4 && form.Manager.State.Queue.All(q => q.Status == "Completed"); });
                form.RefreshNow();
                Check(form.Manager.State.Queue.Count == 4 && !form.ListView.Rows.Any(r => r.Kind == "playlist") && form.Manager.State.Collections.Count == 0, "the four chosen videos were downloaded and the playlist leaves the list");
                Check(form.Tabs.SelectedIndexForTest == 0 && form.ListView.RowCount == 0, "queue tab is empty after everything finished");
                form.Tabs.Selected = 1; form.RefreshNow();
                Check(form.ListView.Rows.Count(r => r.Kind == "item") == 4, "finished downloads moved to the Concluídos tab");
            }
        }
        // 4. formats: GIF carries the chosen clip, Premiere is a format, quality applies to video only
        {
            var engine = new SimulatedEngine();
            using (var form = Open(engine, "formats"))
            {
                form.Manager.State.AutoStart = false; form.Manager.State.GifStart = 12; form.Manager.State.GifDuration = 5;
                form.SetFormatForTest(MediaMode.Gif, false);
                Paste(form, "https://media.example/clip"); Pump(form.AnalyzeNow()); Pump(form.AddNow());
                var gif = form.Manager.State.Queue.Single();
                Check(gif.Mode == MediaMode.Gif && gif.GifStart == 12 && gif.GifDuration == 5, "GIF format queues the chosen clip");
                form.SetFormatForTest(MediaMode.VideoAudio, true);
                Paste(form, "https://media.example/clip"); Pump(form.AnalyzeNow()); Pump(form.AddNow());
                Check(form.Manager.State.Queue.Count == 2 && form.Manager.State.Queue[1].UsePremiere, "the same link in another format is a separate download");
                form.RefreshNow();
                Check(form.ListView.Rows.Count(r => r.Kind == "item") == 2 && form.ListView.Rows.Any(r => r.Sub.StartsWith("GIF")) && form.ListView.Rows.Any(r => r.Sub.StartsWith("Premiere")), "list shows the format of each item");
                Check(form.Tabs.SelectedIndexForTest == 0, "adding keeps the queue tab selected");
            }
        }
        // 5b. built-in help describes the current screen and keeps the profile/account risk warning
        {
            string json; using (var stream = typeof(BaixakiForm).Assembly.GetManifestResourceStream("Baixaki.HelpTopics.json")) using (var reader = new StreamReader(stream)) json = reader.ReadToEnd();
            var topics = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(json);
            Check(topics.Count == 12 && Convert.ToString(topics[1]["warning"]).Contains("restrições na conta") && Convert.ToString(topics[1]["body"]).Contains("Instagram"), "12 embedded help topics, with the profile/account risk and the Instagram limitation");
            Check(!json.Contains("Opções avançadas") && !json.Contains("Analisar link") && json.Contains("12 dias"), "help no longer mentions the old screen and explains the 12-day history");
        }
        // 5d. a Windows "blocked folder" error (WinError 448) is shown as one clear sentence, other errors stay as reported
        {
            string blocked = UiText.Friendly("ERROR: [WinError 448] The path cannot be traversed because it contains an untrusted mount point");
            Check(blocked.Contains("pasta") && blocked.Contains("Vídeos") && UiText.Friendly("Connection reset") == "Connection reset" && UiText.Friendly("") == "Erro desconhecido", "WinError 448 becomes a clear message; other errors are unchanged");
        }
        // 5c. the program icon is embedded: the window and the header logo never fall back to a generic icon
        {
            using (var form = Open(new SimulatedEngine(), "icon"))
                Check(AppIcon.Window != null && AppIcon.Logo != null && AppIcon.Logo.Width >= 64 && AppIcon.Logo.GetPixel(AppIcon.Logo.Width / 2, AppIcon.Logo.Height / 2).A == 255 && form.Icon != null && form.Icon.Width == 32, "window icon and header logo come from the embedded Baixaki icon");
        }
        // 5. settings drawer
        {
            using (var form = Open(new SimulatedEngine(), "settings"))
            {
                Size size = form.ClientSize;
                form.ToggleSettingsForTest(); Application.DoEvents();
                Check(form.SettingsPanelControl.Visible, "settings drawer opens");
                form.ToggleSettingsForTest(); Application.DoEvents();
                Check(!form.SettingsPanelControl.Visible && form.ClientSize == size, "settings drawer closes without changing the window");
            }
        }
    }
}
