using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public enum MediaMode { VideoAudio, VideoOnly, Mp3 }
public class DownloadItem
{
    public string Id = Guid.NewGuid().ToString("N"), Url, OriginalUrl, Title, Extractor, MediaId, CollectionId;
    public MediaMode Mode;
    public bool Premiere, RemoveWhenStopped;
    public string Destination, SourceFile, OutputFile, Status = "Pending", Error = "", Speed = "";
    public double? Percent;
    public DateTime Created = DateTime.UtcNow, Updated = DateTime.UtcNow;
    public int Attempts;
    public string Variant { get { return Mode.ToString() + (Premiere && Mode != MediaMode.Mp3 ? "-Premiere" : ""); } }
    public string Key { get { return Identity(Extractor, MediaId, Url) + "|" + Variant; } }
    public static string Identity(string extractor, string id, string url)
    {
        return !String.IsNullOrEmpty(extractor) && !String.IsNullOrEmpty(id)
            ? extractor.ToLowerInvariant() + ":" + id : "url:" + url;
    }
}
public class CollectionEntry
{
    public string Id, Extractor, Url, Title, Kind = "Unknown", Status = "Discovered";
    public bool HasAudio;
    public string Key { get { return DownloadItem.Identity(Extractor, Id, Url); } }
}
public class CollectionJob
{
    public string Id = Guid.NewGuid().ToString("N"), Url, Title, Extractor, Destination;
    public string Status = "Ready", Error = "", Filter = "All accessible media";
    public MediaMode Mode;
    public bool Premiere, OwnFolder;
    public int Limit = 10, BatchSize = 50, BatchNumber;
    public List<CollectionEntry> Entries = new List<CollectionEntry>();
    public DateTime Created = DateTime.UtcNow;
}
public class HistoryEntry
{
    public string Key, OutputFile, Url;
    public DateTime Completed = DateTime.UtcNow;
}
public class SourceHold
{
    public string Source, Reason;
    public DateTime? Until;
}
public class DownloadState
{
    public int Schema = 1;
    public string Destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public MediaMode Mode;
    public bool Premiere, Duplicates = true;
    public int PaceSeconds = 5;
    public List<DownloadItem> Queue = new List<DownloadItem>();
    public List<CollectionJob> Collections = new List<CollectionJob>();
    public List<HistoryEntry> History = new List<HistoryEntry>();
    public List<SourceHold> Holds = new List<SourceHold>();
}
public class StateStore
{
    public readonly string Root;
    public string Warning;
    public StateStore(string root) { Root = root; }
    public static JavaScriptSerializer Serializer()
    {
        return new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 100 };
    }
    public DownloadState Load()
    {
        foreach (string file in new[] { "state.json", "state.json.bak" })
        {
            string path = Path.Combine(Root, file);
            if (!File.Exists(path)) continue;
            try
            {
                DownloadState state = Serializer().Deserialize<DownloadState>(File.ReadAllText(path));
                if (state == null || state.Schema != 1 || state.Queue == null || state.Collections == null || state.History == null || state.Holds == null) throw new IOException("Unsupported state schema");
                foreach (DownloadItem item in state.Queue)
                    if (item.Status == "Downloading" || item.Status == "Converting") { item.Status = "Paused"; item.Error = "Interrupted; resume to continue."; }
                foreach (CollectionJob job in state.Collections)
                    if (job.Status == "Discovering") job.Status = "Partial";
                return state;
            }
            catch { Warning = "State could not be read. A backup was attempted; original files have been preserved."; }
        }
        var fresh = new DownloadState();
        string legacy = Path.Combine(Root, "folder.txt");
        if (File.Exists(legacy)) { string folder = File.ReadAllText(legacy).Trim(); if (Directory.Exists(folder)) fresh.Destination = folder; }
        return fresh;
    }
    public void Save(DownloadState state)
    {
        Directory.CreateDirectory(Root);
        string file = Path.Combine(Root, "state.json"), temp = file + ".tmp";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(Serializer().Serialize(state));
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        if (File.Exists(file)) File.Replace(temp, file, file + ".bak"); else File.Move(temp, file);
    }
}
public static class Safety
{
    public static bool ValidUrl(string value)
    {
        Uri uri;
        return Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == "https" || uri.Scheme == "http")
            && String.IsNullOrEmpty(uri.UserInfo) && !Regex.IsMatch(value, @"[\r\n\x00]")
            && !Regex.IsMatch(uri.Query, @"(?i)(?:[?&])(?:access_token|token|password|auth|authorization|cookie|signature|sig|key|api_key)=");
    }
    // Application logs never contain engine URLs, cookies, headers or credential diagnostics.
    public static string Redact(string text)
    {
        if (String.IsNullOrEmpty(text)) return "";
        if (Regex.IsMatch(text, "(?i)cookie|authorization|password|bearer|token|header")) return "Authentication diagnostic withheld.";
        return Regex.Replace(text, @"https?://[^\s\""<>]+", "[URL]");
    }
    // CommandLineToArgvW-compatible escaping, including trailing backslashes and quotes.
    public static string Quote(string arg)
    {
        var result = new System.Text.StringBuilder("\"");
        int slashes = 0;
        foreach (char value in arg ?? "")
        {
            if (value == '\\') { slashes++; continue; }
            if (value == '"') { result.Append('\\', slashes * 2 + 1); result.Append('"'); }
            else { result.Append('\\', slashes); result.Append(value); }
            slashes = 0;
        }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }
    public static string Source(string url) { Uri uri; return Uri.TryCreate(url, UriKind.Absolute, out uri) ? uri.Host.ToLowerInvariant() : "unknown"; }
    public static string WorkDirectory(DownloadItem item)
    {
        Guid id;
        if (!Guid.TryParseExact(item.Id, "N", out id)) throw new IOException("Invalid job identifier");
        string destination = Path.GetFullPath(item.Destination), parent = Path.Combine(destination, ".baixaki"), work = Path.Combine(parent,item.Id);
        foreach (string path in new[] { parent, work })
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Job workspace must not be a junction or symbolic link.");
        return work;
    }
    public static bool Inside(string file, string directory)
    {
        return Path.GetFullPath(file).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    public static void Cleanup(DownloadItem item)
    {
        string dir = WorkDirectory(item);
        if (!Directory.Exists(dir)) return;
        if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) return;
        foreach (string file in Directory.GetFiles(dir))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
            if (String.Equals(file, item.SourceFile, StringComparison.OrdinalIgnoreCase)) continue;
            File.Delete(file);
        }
    }
}







