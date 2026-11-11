using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

public enum MediaMode { VideoAudio, VideoOnly, Mp3, Gif, Wav, Thumbnail }
public class DownloadItem
{
    public string Id = Guid.NewGuid().ToString("N"), Url, OriginalUrl, Title, Extractor, MediaId, CollectionId;
    public MediaMode Mode;
    public bool Premiere, RemoveWhenStopped;
    public string Destination, SourceFile, OutputFile, Status = "Pending", Error = "", Speed = "";
    public string Thumb, Channel, Quality = "best";    public int Duration, GifStart, GifDuration = 8, GifWidth = 480;
    public double? Percent;
    public DateTime Created = DateTime.UtcNow, Updated = DateTime.UtcNow;
    public int Attempts;
    public bool UsePremiere { get { return Premiere && (Mode == MediaMode.VideoAudio || Mode == MediaMode.VideoOnly); } }
    public bool IsVideoMode { get { return Mode == MediaMode.VideoAudio || Mode == MediaMode.VideoOnly; } }
    public string Variant
    {
        get
        {
            return Mode.ToString() + (UsePremiere ? "-Premiere" : "")
                + (IsVideoMode && !String.IsNullOrEmpty(Quality) && Quality != "best" ? "-" + Quality : "")
                + (Mode == MediaMode.Gif ? "-" + GifStart + "-" + GifDuration + "-" + GifWidth : "");
        }
    }
    public string Key { get { return Identity(Extractor, MediaId, Url) + "|" + Variant; } }
    public static string Identity(string extractor, string id, string url)
    {
        return !String.IsNullOrEmpty(extractor) && !String.IsNullOrEmpty(id)
            ? extractor.ToLowerInvariant() + ":" + id : "url:" + url;
    }
}
public class CollectionEntry
{
    public string Id, Extractor, Url, Title, Kind = "Unknown", Status = "Discovered", Thumb, Channel;
    public bool HasAudio, Skip;
    public int Duration;
    public string Key { get { return DownloadItem.Identity(Extractor, Id, Url); } }
}
public class CollectionJob
{
    public string Id = Guid.NewGuid().ToString("N"), Url, Title, Extractor, Destination;
    public string Status = "Ready", Error = "", Filter = "All accessible media";
    public MediaMode Mode;
    public bool Premiere, OwnFolder;
    public int Limit = 0, BatchSize = 100000, BatchNumber;
    public string Thumb;
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
    public string Destination = DefaultDestination();
    // New installs save to the Windows Videos folder (an existing choice in state.json is kept).
    public static string DefaultDestination()
    {
        string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        return String.IsNullOrEmpty(videos) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos") : videos;
    }
    public MediaMode Mode;
    public bool Premiere, Duplicates = true, PaceAuto = true, AutoStart = true, OwnFolder = true;
    public int PaceSeconds = 5, DefaultLimit = 0, KeepCompletedDays = 12;
    public string Quality = "best";
    public int GifStart, GifDuration = 8, GifWidth = 480;
    public DateTime? LastEngineCheck;
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
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    // The real location of a folder: junctions and symbolic links are resolved. Windows can refuse to let the engine walk through
    // a junction (WinError 448, common when Downloads was moved), but it works on the real path, with no copy of the finished file.
    public static string RealPath(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            if (!Directory.Exists(full))
            {
                string parent = Path.GetDirectoryName(full);
                return parent == null || parent == full ? full : Path.Combine(RealPath(parent), Path.GetFileName(full));
            }
            using (SafeFileHandle handle = CreateFile(full, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) return full;
                var text = new StringBuilder(2048); uint length = GetFinalPathNameByHandle(handle, text, 2048, 0);
                if (length == 0 || length >= 2048) return full;
                string real = text.ToString();
                if (real.StartsWith(@"\\?\UNC\")) return @"\\" + real.Substring(8);
                return real.StartsWith(@"\\?\") ? real.Substring(4) : real;
            }
        }
        catch { return path; }
    }
    public static bool HasReparsePoint(string path)
    {
        try
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
                string parent = Path.GetDirectoryName(current);
                if (parent == null || parent == current) break;
                current = parent;
            }
        }
        catch { }
        return false;
    }
    public static string WorkDirectory(DownloadItem item)
    {
        Guid id;
        if (!Guid.TryParseExact(item.Id, "N", out id)) throw new IOException("Invalid job identifier");
        // Work files live on the real path of the destination, so the engine never walks through a junction and the file is written once.
        string destination = RealPath(item.Destination), parent = Path.Combine(destination, ".baixaki"), work = Path.Combine(parent,item.Id);
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







