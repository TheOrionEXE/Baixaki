using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public class ProcessResult
{
    public int Code;
    public bool Restricted, Unsupported;
    public string Error = "", Restriction = "";
    public DateTime? RetryAfter;
}
public class UnsupportedResourceException : IOException
{
    public const string Guidance = "The installed yt-dlp engine does not support this URL, or its extractor is marked as broken. Individual media links may still work. Manual Collection selection or browser cookies cannot repair a missing/broken extractor.";
    public UnsupportedResourceException() : base(Guidance) { }
}
public class EngineRunner
{
    public string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    public static bool UnsupportedDiagnostic(string line)
    {
        return Regex.IsMatch(line ?? "", @"(?i)unsupported URL|no suitable extractor|extractor.*(?:marked as broken|currently broken|not working)|functionality.*(?:marked as broken|not working)");
    }
    public static bool IsUnsupported(ProcessResult result) { return result.Unsupported || UnsupportedDiagnostic(result.Error); }
    public static string Restriction(string line)
    {
        if (Regex.IsMatch(line, @"(?i)\b429\b|too many requests|rate.limit|retry.after")) return "Rate limit";
        if (Regex.IsMatch(line, @"(?i)\b401\b|\b403\b|sign.?in|log.?in|authentication|captcha|confirm.*bot|cookies.*required|private.*video")) return "Authentication or access restriction";
        return "";
    }
    public static DateTime? RetryAfter(string line)
    {
        Match m = Regex.Match(line, @"(?i)retry-after\s*[:=]\s*(.+)");
        int seconds; DateTimeOffset date;
        if (!m.Success) return null;
        if (Int32.TryParse(m.Groups[1].Value.Trim(), out seconds)) return DateTime.UtcNow.AddSeconds(Math.Max(0, Math.Min(seconds, 604800)));
        if (DateTimeOffset.TryParse(m.Groups[1].Value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date)) return date.UtcDateTime;
        return null;
    }
    static void Kill(Process process)
    {
        try
        {
            if (process.HasExited) return;
            var info = new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true };
            using (var kill = Process.Start(info)) { if (!kill.WaitForExit(5000)) kill.Kill(); }
        }
        catch { try { process.Kill(); } catch { } }
    }
    public virtual async Task<ProcessResult> Run(string exe, IEnumerable<string> arguments, string dir, Action<string> stdout, Action<string> diagnostic, CancellationToken token, int timeoutSeconds)
    {
        token.ThrowIfCancellationRequested();
        string local = Path.Combine(BaseDirectory, exe + ".exe");
        var info = new ProcessStartInfo(File.Exists(local) ? local : exe, String.Join(" ", arguments.Select(Safety.Quote)))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = dir
        };
        info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        info.EnvironmentVariables["PYTHONUTF8"] = "1";
        info.EnvironmentVariables["PATH"] = BaseDirectory + ";" + Environment.GetEnvironmentVariable("PATH");
        var result = new ProcessResult();
        using (var limit = CancellationTokenSource.CreateLinkedTokenSource(token))
        using (var process = new Process { StartInfo = info })
        {
            if (timeoutSeconds > 0) limit.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            process.Start();
            using (limit.Token.Register(delegate { Kill(process); }))
            {
                Task output = Read(process.StandardOutput, delegate(string line) { stdout(line); });
                Task error = Read(process.StandardError, delegate(string line)
                {
                    if (UnsupportedDiagnostic(line)) { result.Unsupported = true; Kill(process); }
                    string restriction = Restriction(line);
                    if (restriction.Length > 0)
                    {
                        result.Restricted = true; result.Restriction = restriction;
                        DateTime? retry = RetryAfter(line); if (retry.HasValue) result.RetryAfter = retry;
                        Kill(process);
                    }
                    else { DateTime? retry = RetryAfter(line); if (retry.HasValue) result.RetryAfter = retry; }
                    string safe = Safety.Redact(line);
                    result.Error = safe.Length > 700 ? safe.Substring(0, 700) : safe;
                    diagnostic(safe);
                });
                try { await Task.WhenAll(output, error); }
                catch { Kill(process); throw; }
                await Task.Run(delegate { process.WaitForExit(); });
                token.ThrowIfCancellationRequested();
                if (limit.IsCancellationRequested) throw new TimeoutException("Engine operation timed out; partial results were kept.");
                result.Code = process.ExitCode;
            }
        }
        return result;
    }
    static async Task Read(StreamReader reader, Action<string> line)
    {
        string value;
        while ((value = await reader.ReadLineAsync()) != null) line(value);
    }
}
public class ResourceInfo
{
    public string Url, Title, Extractor, Id, Type;
    public bool IsCollection;
    public List<CollectionEntry> Preview = new List<CollectionEntry>();
}
public static class Metadata
{
    public static Dictionary<string, object> Parse(string json) { return StateStore.Serializer().Deserialize<Dictionary<string, object>>(json); }
    public static string Text(Dictionary<string, object> data, string name)
    { object value; return data.TryGetValue(name, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : ""; }
    public static ResourceInfo Resource(Dictionary<string, object> data, string url)
    {
        string type = Text(data, "_type");
        var info = new ResourceInfo { Url = url, Title = Text(data, "title"), Extractor = Text(data, "extractor_key"), Id = Text(data, "id"), Type = type,
            IsCollection = type == "playlist" || type == "multi_video" || data.ContainsKey("entries") };
        object entries;
        if (data.TryGetValue("entries",out entries) && entries is IEnumerable)
        {
            foreach (object value in (IEnumerable)entries)
            {
                var entry = value as Dictionary<string,object>;
                if (entry != null) { info.Preview.Add(Entry(entry)); break; }
            }
        }
        return info;
    }
    public static CollectionEntry Entry(Dictionary<string, object> data)
    {
        string url = Text(data, "webpage_url");
        if (!Safety.ValidUrl(url)) url = Text(data, "url");
        string type = Text(data, "_type"), ext = Text(data, "ext"), v = Text(data, "vcodec"), a = Text(data, "acodec");
        string kind = "Unknown";
        if (v != "" && v != "none") kind = "Video";
        else if (v == "none" && a != "" && a != "none") kind = "Audio";
        else if (new[] { "jpg", "png", "webp", "gif", "avif" }.Contains(ext)) kind = "Other";
        string extractor = Text(data, "extractor_key");
        if (extractor.Length == 0) extractor = Text(data, "ie_key");
        return new CollectionEntry { Id = Text(data, "id"), Title = Safety.Redact(Text(data, "title")), Extractor = extractor,
            Url = Safety.ValidUrl(url) ? url : "", Kind = kind, HasAudio = a != "" && a != "none", Status = (type == "playlist" || type == "multi_video") ? "Nested collection" : "Discovered" };
    }
    public static bool Eligible(CollectionEntry entry, string filter)
    {
        if (filter == "Other media: unsupported") return false;
        if (entry.Status == "Nested collection" || String.IsNullOrEmpty(entry.Url) || entry.Kind == "Other") return false;
        if (filter == "Videos only (known metadata)") return entry.Kind == "Video";
        if (filter == "Audio-compatible only (known metadata)") return entry.HasAudio;
        return true;
    }
}
public class DownloadManager
{
    public DownloadState State;
    public readonly StateStore Store;
    public readonly EngineRunner Engine;
    public Action Changed = delegate { };
    public Action<string> Log = delegate { };
    public bool Running, Discovering, Updating;
    public int Analyzing;
    public bool Busy { get { return processing != null && !processing.IsCompleted; } }
    public string Browser = ""; // Browser name only, session memory; never cookies or cookie values.
    public string ActiveId;
    CancellationTokenSource active, discovery, queueStop;
    string discoveryUrl;
    Task processing;
    public DownloadManager(StateStore store, EngineRunner engine)
    { Store = store; Engine = engine; State = store.Load(); }
    public void Save() { Store.Save(State); Changed(); }
    public bool Held(string url)
    {
        string source = Safety.Source(url);
        return State.Holds.Any(h => h.Source == source && (!h.Until.HasValue || h.Until.Value > DateTime.UtcNow));
    }
    public void ReleaseHolds()
    {
        if (State.Holds.Any(h => h.Until.HasValue && h.Until.Value > DateTime.UtcNow)) throw new InvalidOperationException("Server Retry-After has not elapsed.");
        State.Holds.Clear(); Save();
    }
    void Restrict(string url, ProcessResult result)
    {
        if (!result.Restricted) return;
        string source = Safety.Source(url);
        State.Holds.RemoveAll(h => h.Source == source);
        State.Holds.Add(new SourceHold { Source = source, Reason = result.Restriction, Until = result.RetryAfter });
        Save(); Log(source + ": " + result.Restriction + ". Further requests stopped. Review access before releasing the source hold.");
        if (discovery != null && Safety.Source(discoveryUrl) == source) discovery.Cancel();
        if (active != null && State.Queue.Any(q => q.Id == ActiveId && (Safety.Source(q.Url) == source || Safety.Source(q.OriginalUrl) == source))) active.Cancel();
    }
    List<string> Common()
    {
        var args = new List<string> { "--ignore-config", "--encoding", "utf-8", "--socket-timeout", "20", "--retries", "0", "--fragment-retries", "0", "--extractor-retries", "0", "--sleep-requests", State.PaceSeconds.ToString(CultureInfo.InvariantCulture), "--no-colors" };
        if (Browser != "") { args.Add("--cookies-from-browser"); args.Add(Browser); }
        return args;
    }
    public async Task<ResourceInfo> Analyze(string url, CancellationToken token)
    {
        if (!Safety.ValidUrl(url)) throw new ArgumentException("Use an HTTP(S) page URL without credentials or authentication tokens.");
        if (Held(url)) throw new InvalidOperationException("Source is on hold. Review authentication/rate limit before releasing it.");
        if (Updating) throw new InvalidOperationException("Wait for the engine update.");
        Analyzing++; Changed();
        try {
        var args = Common(); args.AddRange(new[] { "--flat-playlist", "--lazy-playlist", "--playlist-end", "1", "--dump-single-json", "--skip-download", "--", url });
        ResourceInfo info = null;
        var result = await Engine.Run("yt-dlp", args, Engine.BaseDirectory, delegate(string line) { if (line.StartsWith("{")) info = Metadata.Resource(Metadata.Parse(line), url); }, Log, token, 60);
        Restrict(url, result);
        if (EngineRunner.IsUnsupported(result) && !result.Restricted) throw new UnsupportedResourceException();
        if (info == null || result.Code != 0 || result.Restricted) throw new IOException(result.Restricted ? result.Restriction : "Classification unavailable: " + result.Error);
        return info;
        } finally { Analyzing--; Changed(); }
    }
    public CollectionJob CreateCollection(ResourceInfo info)
    {
        var existing = State.Collections.FirstOrDefault(j => j.Url == info.Url);
        if (existing != null) return existing;
        var job = new CollectionJob { Url = info.Url, Title = Safety.Redact(info.Title), Extractor = info.Extractor,
            Destination = State.Destination, Mode = State.Mode, Premiere = State.Premiere };
        job.Status = "Preview only; review discovery settings";
        foreach (var entry in info.Preview)
        {
            if (entry.Extractor == "") entry.Extractor = info.Extractor;
            if (entry.Url == "") entry.Status = "Unsupported entry URL";
            else if (entry.Kind == "Other") entry.Status = "Unsupported media";
            job.Entries.Add(entry);
        }
        State.Collections.Add(job); Save(); return job;
    }
    public async Task Discover(CollectionJob job)
    {
        if (Discovering || Updating) throw new InvalidOperationException("Another discovery or engine update is running.");
        if (Held(job.Url)) throw new InvalidOperationException("Source is on hold.");
        Discovering = true; discoveryUrl = job.Url; discovery = new CancellationTokenSource(); job.Status = "Discovering"; job.Error = ""; Save();
        var seen = new HashSet<string>(job.Entries.Select(e => e.Key)); int sinceSave = 0;
        try
        {
            var args = Common();
            args.AddRange(new[] { "--flat-playlist", "--lazy-playlist", "--dump-json", "--skip-download", "--yes-playlist", "--ignore-errors" });
            if (job.Limit > 0) args.AddRange(new[] { "--playlist-end", job.Limit.ToString() });
            args.AddRange(new[] { "--", job.Url });
            var result = await Engine.Run("yt-dlp", args, Engine.BaseDirectory, delegate(string line)
            {
                if (!line.StartsWith("{")) return;
                CollectionEntry entry = Metadata.Entry(Metadata.Parse(line));
                if (entry.Extractor == "") entry.Extractor = job.Extractor;
                if (entry.Url == "") { entry.Status = "Unsupported entry URL"; }
                else if (entry.Kind == "Other") entry.Status = "Unsupported media";
                if (seen.Add(entry.Key)) { job.Entries.Add(entry); sinceSave++; }
                if (sinceSave >= 25) { Save(); sinceSave = 0; }
            }, Log, discovery.Token, 900);
            Restrict(job.Url, result);
            bool unsupported = EngineRunner.IsUnsupported(result) && !result.Restricted;
            job.Status = unsupported ? "Unsupported" : result.Restricted ? "Blocked" : (result.Code == 0 && result.Error == "" ? "Discovered (extent unknown)" : "Partial");
            job.Error = unsupported ? UnsupportedResourceException.Guidance : result.Restricted ? result.Restriction : result.Error;
            if (job.Entries.Count == 0 && !unsupported) { job.Status = "Empty / unsupported"; job.Error = "No accessible entries returned by the extractor. " + job.Error; }
        }
        catch (OperationCanceledException) { job.Status = Held(job.Url) ? "Blocked" : "Partial"; }
        catch (Exception ex) { job.Status = "Partial"; job.Error = Safety.Redact(ex.Message); }
        finally { Discovering = false; discovery.Dispose(); discovery = null; Save(); }
    }
    public void StopDiscovery() { if (discovery != null) discovery.Cancel(); }
    public bool Add(string url, string title, string extractor, string mediaId, CollectionJob collection)
    {
        if (!Safety.ValidUrl(url)) return false;
        var item = new DownloadItem { Url = url, OriginalUrl = collection == null ? url : collection.Url, Title = title, Extractor = extractor,
            MediaId = mediaId, Mode = collection == null ? State.Mode : collection.Mode, Premiere = collection == null ? State.Premiere : collection.Premiere,
            Destination = collection == null ? State.Destination : collection.Destination, CollectionId = collection == null ? null : collection.Id };
        if (collection != null && collection.OwnFolder) item.Destination = Path.Combine(item.Destination, "Collection-" + collection.Id.Substring(0, 8));
        if (State.Duplicates && (State.Queue.Any(q => (q.Key == item.Key || (q.Url == item.Url && q.Variant == item.Variant)) && q.Status != "Cancelled" && q.Status != "Failed" && (q.Status != "Completed" || File.Exists(q.OutputFile)))
            || State.History.Any(h => (h.Key == item.Key || (h.Url == item.Url && h.Key.EndsWith("|" + item.Variant))) && File.Exists(h.OutputFile)))) return false;
        State.Queue.Add(item); return true;
    }
    public int QueueBatch(CollectionJob job)
    {
        if (job.Status == "Unsupported" && job.Entries.Count == 0) throw new UnsupportedResourceException();
        if (State.Queue.Any(q => q.CollectionId == job.Id && new[] { "Pending", "Downloading", "Converting", "Paused" }.Contains(q.Status))) throw new InvalidOperationException("Finish or cancel the current batch first.");
        int count = 0;
        foreach (CollectionEntry entry in job.Entries.Where(e => e.Status == "Discovered"))
        {
            if (!Metadata.Eligible(entry, job.Filter)) continue;
            if (Add(entry.Url, entry.Title, entry.Extractor, entry.Id, job)) { entry.Status = "Queued"; count++; }
            else entry.Status = "Duplicate";
            if (count >= job.BatchSize) break;
        }
        job.BatchNumber++; job.Status = count > 0 ? "Batch " + job.BatchNumber + " queued; manual continuation" : "No eligible undispatched entries"; Save(); return count;
    }
    public Task Start()
    {
        if (Running || Busy) return processing ?? Task.FromResult(0);
        if (Updating) throw new InvalidOperationException("Wait for the engine update.");
        foreach (var item in State.Queue.Where(q => q.Status == "Paused")) item.Status = "Pending";
        queueStop = new CancellationTokenSource(); Running = true; processing = ProcessQueue(); return processing;
    }
    public void Pause()
    { Running = false; if (queueStop != null) queueStop.Cancel(); if (active != null) active.Cancel(); Save(); }
    public void Cancel(DownloadItem item)
    {
        item.Status = "Cancelled"; item.Updated = DateTime.UtcNow; UpdateEntry(item, "Cancelled");
        if (ActiveId == item.Id && active != null) active.Cancel();
        else Safety.Cleanup(item);
        Save();
    }
    public void Retry(DownloadItem item)
    {
        if (ActiveId == item.Id) return;
        item.Status = "Pending"; item.Error = ""; item.Percent = null; Save();
    }
    public void Remove(DownloadItem item)
    {
        if (ActiveId == item.Id) { item.RemoveWhenStopped = true; Cancel(item); return; }
        Safety.Cleanup(item); UpdateEntry(item, "Discovered"); State.Queue.Remove(item); Save();
    }
    async Task ProcessQueue()
    {
        try
        {
            while (Running)
            {
                var item = State.Queue.FirstOrDefault(q => q.Status == "Pending" && !Held(q.Url) && !Held(q.OriginalUrl));
                if (item == null) break;
                active = new CancellationTokenSource(); ActiveId = item.Id;
                try
                {
                    item.Status = "Downloading"; item.Error = ""; item.Percent = null; item.Speed = ""; Save();
                    await Download(item, active.Token);
                    if (item.Status == "Cancelled") continue;
                    item.Status = "Completed"; item.Percent = 100; item.Speed = "";
                    State.History.RemoveAll(h => h.Key == item.Key);
                    State.History.Add(new HistoryEntry { Key = item.Key, Url = item.Url, OutputFile = item.OutputFile });
                    UpdateEntry(item, "Completed"); Log("DONE: " + Path.GetFileName(item.OutputFile));
                }
                catch (OperationCanceledException)
                {
                    if (item.Status != "Cancelled") item.Status = Held(item.Url) || Held(item.OriginalUrl) ? "Blocked" : "Paused";
                    if (item.Status == "Cancelled") Safety.Cleanup(item);
                }
                catch (Exception ex)
                {
                    item.Status = Held(item.Url) || Held(item.OriginalUrl) ? "Blocked" : "Failed";
                    item.Error = Safety.Redact(ex.Message); UpdateEntry(item, item.Status); Log("FAIL: " + item.Error);
                }
                finally { item.Updated = DateTime.UtcNow; ActiveId = null; active.Dispose(); active = null; if (item.RemoveWhenStopped) { Safety.Cleanup(item); UpdateEntry(item,"Discovered"); State.Queue.Remove(item); } Save(); }
                if (Running) { try { await Task.Delay(TimeSpan.FromSeconds(State.PaceSeconds), queueStop.Token); } catch (OperationCanceledException) { break; } }
            }
        }
        finally { Running = false; queueStop.Dispose(); queueStop = null; Save(); }
    }
    void UpdateEntry(DownloadItem item, string status)
    {
        var job = State.Collections.FirstOrDefault(j => j.Id == item.CollectionId);
        if (job == null) return;
        var entry = job.Entries.FirstOrDefault(e => e.Url == item.Url);
        if (entry != null) entry.Status = status;
    }
    public static List<string> ModeArguments(MediaMode mode)
    {
        if (mode == MediaMode.Mp3) return new List<string> { "-f", "ba/b", "-x", "--audio-format", "mp3", "--audio-quality", "0" };
        if (mode == MediaMode.VideoOnly) return new List<string> { "-f", "bv[vcodec^=avc1][ext=mp4]/bv[ext=mp4]/bv/b", "--merge-output-format", "mp4" };
        return new List<string> { "-f", "bv[vcodec^=avc1][ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv+ba/b", "--merge-output-format", "mp4" };
    }
    public static List<string> ConversionArguments(string source, string output, bool silent)
    {
        var args = new List<string> { "-y", "-nostdin", "-hide_banner", "-loglevel", "error", "-i", source, "-map", "0:v:0" };
        if (!silent) args.AddRange(new[] { "-map", "0:a?" });
        args.AddRange(new[] { "-c:v", "libx264", "-profile:v", "high", "-level", "4.0", "-pix_fmt", "yuv420p", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-r", "29.97" });
        args.AddRange(silent ? new[] { "-an" } : new[] { "-c:a", "aac", "-b:a", "192k" });
        args.AddRange(new[] { "-movflags", "+faststart", output }); return args;
    }
    public void Progress(DownloadItem item, string line)
    {
        string[] fields = line.Substring(2).Split('|'); double done, total;
        item.Percent = fields.Length >= 2 && Double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out done) && Double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out total) && total > 0 ? (double?)Math.Min(100, done * 100 / total) : null;
        item.Speed = fields.Length > 2 && fields[2] != "NA" ? fields[2] + " B/s" : "";
        Changed();
    }
    async Task Download(DownloadItem item, CancellationToken token)
    {
        string work = Safety.WorkDirectory(item); Directory.CreateDirectory(work);
        if (File.Exists(item.OutputFile)) return;
        if (State.Duplicates)
        {
            var found = State.History.FirstOrDefault(h => h.Key == item.Key && File.Exists(h.OutputFile));
            if (found != null) { item.OutputFile = found.OutputFile; return; }
        }
        if (!File.Exists(item.SourceFile))
        {
            string archive = Path.Combine(work, "archive.txt");
            // Rebuild from outputs that still exist, scoped to this artifact variant.
            var records = State.Queue.Where(q => q.Status == "Completed" && q.Variant == item.Variant && File.Exists(q.OutputFile) && !String.IsNullOrEmpty(q.MediaId) && !String.IsNullOrEmpty(q.Extractor));
            File.WriteAllLines(archive, State.Duplicates ? records.Select(q => q.Extractor.ToLowerInvariant() + " " + q.MediaId).ToArray() : new string[0]);
            var args = Common(); args.AddRange(ModeArguments(item.Mode));
            args.AddRange(new[] { "--no-playlist", "--max-downloads", "1", "--no-simulate", "--progress", "--newline", "--continue", "--no-overwrites", "--download-archive", archive,
                "-o", Path.Combine(work, "%(title).100s [%(id).60s].%(ext)s"),
                "--print", "before_dl:M|%(extractor_key)j|%(id)j", "--print", "after_move:F|%(filepath)j",
                "--progress-template", "download:P|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.speed)s", "--", item.Url });
            ProcessResult result = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                item.Attempts++; Save();
                result = await Engine.Run("yt-dlp", args, work, delegate(string line)
                {
                    if (line.StartsWith("F|"))
                    {
                        string file = StateStore.Serializer().Deserialize<string>(line.Substring(2));
                        if (Safety.Inside(file, work) && File.Exists(file)) { item.SourceFile = file; Save(); }
                    }
                    else if (line.StartsWith("M|"))
                    {
                        // Each JSON string can contain pipes. Decode the pair as a JSON array after locating its delimiter.
                        Match m = Regex.Match(line.Substring(2), "^(\"(?:[^\"\\\\]|\\\\.)*\")\\|(.*)$");
                        if (m.Success) { item.Extractor = StateStore.Serializer().Deserialize<string>(m.Groups[1].Value); item.MediaId = StateStore.Serializer().Deserialize<string>(m.Groups[2].Value); }
                    }
                    else if (line.StartsWith("P|")) Progress(item, line);
                }, Log, token, 0);
                Restrict(item.OriginalUrl, result);
                if (result.Restricted) { Restrict(item.Url, result); throw new IOException(result.Restriction); }
                if (result.Code == 0 || result.Code == 101) break;
                if (attempt == 2 || !Regex.IsMatch(result.Error, @"(?i)timed? out|temporary|connection reset|\b50[0234]\b")) break;
                Log("Transient failure; retrying conservatively."); await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, State.PaceSeconds) * (attempt + 1)), token);
            }
            if (result.Code != 0 && result.Code != 101) throw new IOException("Engine failed: " + result.Error);
            if (!File.Exists(item.SourceFile)) throw new IOException("No output artifact returned. The item may be unsupported, unavailable, or archived.");
        }
        token.ThrowIfCancellationRequested();
        if (!Safety.Inside(item.SourceFile, work)) throw new IOException("Source is outside this job's workspace.");
        string output = item.SourceFile;
        if (item.Premiere && item.Mode != MediaMode.Mp3)
        {
            item.Status = "Converting"; item.Percent = null; Save();
            output = Path.Combine(work, "Premiere.mp4");
            var result = await Engine.Run("ffmpeg", ConversionArguments(item.SourceFile, output, item.Mode == MediaMode.VideoOnly), work, delegate { }, Log, token, 0);
            if (result.Code != 0) throw new IOException("Premiere conversion failed. Source kept; Retry will reuse it.");
        }
        token.ThrowIfCancellationRequested();
        if (item.Mode == MediaMode.VideoOnly && !item.Premiere)
        {
            item.Status = "Converting"; item.Percent = null; Save();
            output = Path.Combine(work, "VideoOnly" + Path.GetExtension(item.SourceFile));
            var stripped = await Engine.Run("ffmpeg", new[] { "-y", "-nostdin", "-hide_banner", "-loglevel", "error", "-i", item.SourceFile, "-map", "0:v:0", "-c:v", "copy", "-an", output }, work, delegate { }, Log, token, 0);
            if (stripped.Code != 0) throw new IOException("Unable to produce a video-only artifact. Source kept for retry.");
        }
        token.ThrowIfCancellationRequested();
        string name = Path.GetFileNameWithoutExtension(item.SourceFile) + (item.Premiere && item.Mode != MediaMode.Mp3 ? "_Premiere" : "") + "_" + item.Variant;
        string target = Path.Combine(item.Destination, name + "_" + item.Id.Substring(0, 8) + Path.GetExtension(output));
        if (File.Exists(target)) target = Path.Combine(item.Destination, name + "_" + item.Id + "_" + DateTime.UtcNow.Ticks + Path.GetExtension(output));
        File.Move(output, target); item.OutputFile = target; Save();
        if (!item.Premiere) item.SourceFile = null;
    }
    public async Task UpdateEngine()
    {
        if (Running || Busy || Discovering || Updating || Analyzing > 0) throw new InvalidOperationException("Pause the queue and stop discovery before updating the engine.");
        Updating = true; Changed();
        try
        {
            var result = await Engine.Run("yt-dlp", new[] { "--ignore-config", "-U" }, Engine.BaseDirectory, Log, Log, CancellationToken.None, 180);
            if (result.Code != 0) throw new IOException("Engine update failed: " + result.Error);
            Log("DONE: engine update check finished.");
        }
        finally { Updating = false; Changed(); }
    }
}










