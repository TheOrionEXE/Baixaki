using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class SimulatedEngine : EngineRunner
{
    public int Entries = 2005, Calls, Downloads, Conversions;
    public string Extractor = "Example", Scenario = "", LastMode = "";
    public bool Playlist, Empty, Partial, FailConversion, FailDownload, HoldDownload, SlowCancellation;
    public List<string> LastArguments;
    public override async Task<ProcessResult> Run(string exe, IEnumerable<string> arguments, string dir, Action<string> stdout, Action<string> diagnostic, CancellationToken token, int timeout)
    {
        Calls++; LastArguments = arguments.ToList(); var args = LastArguments;
        if (exe == "ffmpeg")
        {
            Conversions++; if (FailConversion) return new ProcessResult { Code=1 };
            File.WriteAllText(args.Last(), "converted"); return new ProcessResult();
        }
        if (args.Contains("-U")) return new ProcessResult { Code = FailDownload ? 1 : 0 };
        if (Scenario == "unsupported") return new ProcessResult { Code=1, Error="ERROR: Unsupported URL: [URL]" };
        if (Scenario == "restricted") return new ProcessResult { Code=1, Restricted=true, Restriction="Rate limit", RetryAfter=DateTime.UtcNow.AddSeconds(60) };
        if (args.Contains("--dump-single-json"))
        {
            stdout("{\"_type\":\"" + (Playlist ? "playlist" : "video") + "\",\"extractor_key\":\"" + Extractor + "\",\"id\":\"1\",\"title\":\"Resource\"}");
            return new ProcessResult();
        }
        if (args.Contains("--dump-json"))
        {
            int count = Empty ? 0 : Entries;
            int index = args.IndexOf("--playlist-end"); if (index >= 0) count = Math.Min(count,Int32.Parse(args[index+1]));
            for (int i=0;i<count;i++)
            {
                token.ThrowIfCancellationRequested();
                stdout("{\"id\":\"" + i + "\",\"title\":\"Media " + i + "\",\"ie_key\":\"" + Extractor + "\",\"url\":\"https://media.example/" + i + "\"}");
                if (Partial && i == 26) throw new IOException("Simulated interrupted discovery");
            }
            return new ProcessResult();
        }
        Downloads++; LastMode = String.Join(" ",args);
                if (HoldDownload)
        {
            if (SlowCancellation)
            {
                var completion = new TaskCompletionSource<int>();
                using (token.Register(delegate { Task.Delay(50).ContinueWith(delegate(Task t) { completion.TrySetCanceled(); }); })) await completion.Task;
            }
            else await Task.Delay(30000,token);
        }
        if (FailDownload) return new ProcessResult { Code=1, Error="Unsupported resource" };
        if (Scenario == "transient" && Downloads < 2) return new ProcessResult { Code=1, Error="Connection reset" };
        File.WriteAllText(Path.Combine(dir,"source.mp4"),"source");
        stdout("M|\"" + Extractor + "\"|\"id" + Downloads + "\"");
        stdout("P|100|NA|NA"); stdout("P|100|200|1000");
        stdout("F|" + StateStore.Serializer().Serialize(Path.Combine(dir,"source.mp4")));
        return new ProcessResult();
    }
}
class Tests
{
    static int passed;
    static string root;
    static void Check(bool value,string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
    static DownloadManager Manager(SimulatedEngine engine)
    {
        string folder=Path.Combine(root,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var manager = new DownloadManager(new StateStore(folder),engine); manager.State.Destination=folder; manager.State.PaceSeconds=0; return manager;
    }
    static async Task Run()
    {
        Check(Safety.Quote("a\"b\\") == "\"a\\\"b\\\\\"", "Windows argument escaping");
        Check(!Safety.ValidUrl("https://user:secret@example.com") && !Safety.ValidUrl("https://example.com/?token=secret") && Safety.ValidUrl("https://example.com/watch?v=abc&list=123"),"Credential URL rejection and ordinary URLs");
        Check(!Safety.Redact("Authorization: secret").Contains("secret") && !Safety.Redact("error https://example.com/?sig=secret").Contains("secret"),"Secret redaction");
        Check(EngineRunner.Restriction("HTTP Error 429: Too Many Requests") == "Rate limit" && EngineRunner.Restriction("Sign in to confirm you are not a bot") != "" && EngineRunner.Restriction("normal") == "", "Rate and authentication classification");
        Check(EngineRunner.RetryAfter("Retry-After: 120") > DateTime.UtcNow.AddSeconds(110),"Retry-After seconds");
        Check(EngineRunner.RetryAfter("Retry-After: Wed, 01 Jan 2031 00:00:00 GMT").Value.Year == 2031,"Retry-After HTTP date");
        foreach (string extractor in new[] { "Youtube", "Instagram", "TikTok", "Generic" })
        {
            var fake=new SimulatedEngine { Extractor=extractor }; var manager=Manager(fake);
            var single=await manager.Analyze("https://media.example/single",CancellationToken.None);
            Check(!single.IsCollection && single.Extractor == extractor,"Single classification: " + extractor);
            fake.Playlist=true; var info=await manager.Analyze("https://media.example/collection",CancellationToken.None);
            Check(info.IsCollection,"Collection classification: " + extractor);
            var job=manager.CreateCollection(info); job.Limit=0; await manager.Discover(job);
            Check(job.Entries.Count==2005,"2005 entries: " + extractor);
            await manager.Discover(job); Check(job.Entries.Count==2005,"Discovery ID reconciliation: " + extractor);
            Check(manager.QueueBatch(job)==50 && manager.State.Queue.Count==50,"Manual 50 item batch: " + extractor);
            bool blocked=false; try { manager.QueueBatch(job); } catch (InvalidOperationException) { blocked=true; }
            Check(blocked,"Batch continuation gate: " + extractor);
            var restored = new DownloadManager(manager.Store,new SimulatedEngine());
            Check(restored.State.Collections[0].Entries.Count==2005 && restored.State.Queue.Count==50,"Large collection persistence: " + extractor);
            Check(manager.State.Queue.All(q => q.CollectionId==job.Id),"Collection associations: " + extractor);
        }
        {
            var fake=new SimulatedEngine { Partial=true }; var manager=Manager(fake);
            var job=manager.CreateCollection(new ResourceInfo { Url="https://media.example/c",Title="Partial",Extractor="Example",IsCollection=true }); job.Limit=0;
            await manager.Discover(job); Check(job.Entries.Count==27 && job.Status=="Partial","Partial discovery retained");
            fake.Partial=false; fake.Entries=40; await manager.Discover(job); Check(job.Entries.Count==40,"Partial discovery re-enumeration");
            fake.Empty=true; var empty=manager.CreateCollection(new ResourceInfo { Url="https://media.example/empty" }); await manager.Discover(empty);
            Check(empty.Status=="Empty / unsupported" && empty.Entries.Count==0,"Empty or unsupported collection");
            var photo=Metadata.Entry(Metadata.Parse("{\"id\":\"photo\",\"ext\":\"jpg\",\"url\":\"https://media.example/photo\"}"));
            Check(photo.Kind=="Other" && !Metadata.Eligible(photo,"All accessible media"),"Unsupported media skipped");
            var video=Metadata.Entry(Metadata.Parse("{\"vcodec\":\"h264\",\"url\":\"https://media.example/video\"}"));
            Check(Metadata.Eligible(video,"Videos only (known metadata)") && !Metadata.Eligible(new CollectionEntry { Url=video.Url },"Videos only (known metadata)"),"Filters require actual metadata");
        }
        foreach (MediaMode mode in Enum.GetValues(typeof(MediaMode)))
        {
            var fake=new SimulatedEngine(); var manager=Manager(fake); manager.State.Mode=mode;
            manager.Add("https://media.example/item","Media","Example","id1",null); manager.Save();
            await manager.Start(); var item=manager.State.Queue[0];
            Check(item.Status=="Completed" && File.Exists(item.OutputFile),"Download mode: " + mode);
            Check(!manager.Add(item.Url,"Duplicate","Example","id1",null),"Existing duplicate: " + mode);
            File.Delete(item.OutputFile); Check(manager.Add(item.Url,"Redownload","Example","id1",null),"Deleted output redownload: " + mode);
            if (mode==MediaMode.VideoOnly) Check(!fake.LastMode.Contains("bv*"),"Video-only format selection avoids bv* selectors");
        }
        {
            var fake=new SimulatedEngine { FailConversion=true }; var manager=Manager(fake); manager.State.Mode=MediaMode.VideoOnly; manager.State.Premiere=true;
            manager.Add("https://media.example/premiere","Premiere",null,null,null); await manager.Start();
            var item=manager.State.Queue[0]; Check(item.Status=="Failed" && File.Exists(item.SourceFile),"Conversion failure preserves source");
            fake.FailConversion=false; manager.Retry(item); await manager.Start();
            Check(fake.Downloads==1 && fake.Conversions==2 && item.Status=="Completed","Conversion retry reuses downloaded source");
            Check(fake.LastArguments.Contains("-an") && !fake.LastArguments.Contains("0:a?"),"Video only Premiere explicitly strips audio");
            Check(DownloadManager.ConversionArguments("in","out",false).Contains("0:a?"),"Premiere video+audio keeps optional audio");
            manager.State.Mode=MediaMode.Mp3; manager.State.Premiere=false;
            Check(manager.Add(item.Url,"Other format",item.Extractor,item.MediaId,null),"Artifact variants do not collide");
        }
        {
            var fake=new SimulatedEngine { HoldDownload=true }; var manager=Manager(fake);
            manager.Add("https://media.example/slow","Slow",null,null,null); Task work=manager.Start(); await Task.Delay(20);
            manager.Add("https://other.example/next","Next",null,null,null); manager.Pause(); await work;
            var first=manager.State.Queue[0]; Check(first.Status=="Paused" && manager.State.Queue.Count==2,"Pause active item and add while processing");
            manager.Save(); var restarted=new DownloadManager(manager.Store,new SimulatedEngine());
            Check(restarted.State.Queue[0].Status=="Paused","Paused queue restart persistence");
            manager.Cancel(manager.State.Queue[1]); Check(manager.State.Queue[1].Status=="Cancelled","Cancel pending");
            manager.Remove(manager.State.Queue[1]); Check(manager.State.Queue.Count==1,"Remove pending");
            fake.HoldDownload=false; await manager.Start(); Check(first.Status=="Completed","Resume paused queue");
            manager.State.Queue[0].Status="Converting"; manager.Save();
            Check(new DownloadManager(manager.Store,new SimulatedEngine()).State.Queue[0].Status=="Paused","Interrupted conversion recovery");
        }
        {
            var fake=new SimulatedEngine { HoldDownload=true }; var manager=Manager(fake); manager.Add("https://media.example/cancel","Cancel",null,null,null);
            var item=manager.State.Queue[0]; string outside=Path.Combine(item.Destination,"unrelated.part"); File.WriteAllText(outside,"keep");
            Task work=manager.Start(); await Task.Delay(20); string own=Path.Combine(Safety.WorkDirectory(item),"own.part"); File.WriteAllText(own,"partial"); manager.Cancel(item); await work;
            Check(item.Status=="Cancelled" && !File.Exists(own) && File.Exists(outside),"Cancellation cleans only job-owned files");
        }
        {
            var fake=new SimulatedEngine { Scenario="restricted" }; var manager=Manager(fake);
            manager.Add("https://media.example/1","1",null,null,null); manager.Add("https://media.example/2","2",null,null,null); await manager.Start();
            Check(manager.State.Queue[0].Status=="Blocked" && manager.State.Queue[1].Status=="Pending" && fake.Calls==1,"Rate limit stops further source requests");
            Check(manager.Held("https://media.example/anything"),"Source hold persisted");
        }
        {
            var fake=new SimulatedEngine { FailDownload=true }; var manager=Manager(fake); manager.Add("https://media.example/fail","Failure",null,null,null); await manager.Start();
            Check(manager.State.Queue[0].Status=="Failed","Permanent failures are not retried aggressively");
            fake.FailDownload=false; manager.Retry(manager.State.Queue[0]); await manager.Start(); Check(manager.State.Queue[0].Status=="Completed","Explicit failure retry");
            manager.Progress(manager.State.Queue[0],"P|10|NA|NA"); Check(!manager.State.Queue[0].Percent.HasValue,"Unknown total has no invented percentage");
            await manager.UpdateEngine(); Check(fake.LastArguments.Contains("-U"),"Engine update dispatch");
        }
        {
            var manager=Manager(new SimulatedEngine()); manager.Save(); manager.State.PaceSeconds=9; manager.Save();
            File.WriteAllText(Path.Combine(manager.Store.Root,"state.json"),"bad");
            Check(manager.Store.Load().PaceSeconds==0 && manager.Store.Warning!=null,"Atomic backup recovery");
        }
        {
            var fake=new SimulatedEngine { HoldDownload=true, SlowCancellation=true }; var manager=Manager(fake);
            manager.Add("https://media.example/race","Race",null,null,null); Task work=manager.Start(); await Task.Delay(10);
            manager.Pause(); Task repeated=manager.Start();
            Check(Object.ReferenceEquals(work,repeated) && fake.Downloads==1,"Rapid pause/resume cannot launch a second worker");
            await work;
            fake.HoldDownload=false; await manager.Start(); Check(manager.State.Queue[0].Status=="Completed","Worker can restart after cancellation settles");
        }
        {
            var fake=new SimulatedEngine { Scenario="transient" }; var manager=Manager(fake);
            manager.Add("https://media.example/transient","Transient",null,null,null); await manager.Start();
            Check(fake.Downloads==2 && manager.State.Queue[0].Status=="Completed","Bounded transient retry succeeds");
        }
        {
            var manager=Manager(new SimulatedEngine()); manager.State.Holds.Add(new SourceHold { Source="media.example",Until=DateTime.UtcNow.AddMinutes(5) });
            bool refused=false; try { manager.ReleaseHolds(); } catch(InvalidOperationException) { refused=true; }
            Check(refused,"Retry-After cannot be cleared prematurely");
            manager.State.Holds[0].Until=DateTime.UtcNow.AddSeconds(-1); Check(!manager.Held("https://media.example/1"),"Timed source hold expires");
            manager.ReleaseHolds(); Check(manager.State.Holds.Count==0,"Expired source hold releases");
        }
        {
            var preview=Metadata.Resource(Metadata.Parse("{\"_type\":\"playlist\",\"extractor_key\":\"Example\",\"entries\":[{\"id\":\"one\",\"url\":\"https://media.example/one\",\"ie_key\":\"Example\"},{\"id\":\"two\",\"url\":\"https://media.example/two\"}]}"),"https://media.example/profile");
            Check(preview.IsCollection && preview.Preview.Count==1,"Bounded collection preview retains at most one entry");
            var fake=new SimulatedEngine();var manager=Manager(fake);var job=manager.CreateCollection(preview);
            Check(job.Entries.Count==1 && job.Limit==10 && fake.Calls==0,"Creating collection does not enumerate source; conservative default 10");
        }
        Check(!Metadata.Eligible(new CollectionEntry { Url="https://media.example/video",Kind="Video" },"Other media: unsupported"),"Unsupported other-media filter never queues video/audio");
        Check(!Metadata.Eligible(new CollectionEntry { Url="https://media.example/video",Kind="Video" },"Audio-compatible only (known metadata)"),"Audio filter does not assume that every video has audio");
        Check(Metadata.Eligible(Metadata.Entry(Metadata.Parse("{\"vcodec\":\"h264\",\"acodec\":\"aac\",\"url\":\"https://media.example/video\"}")),"Audio-compatible only (known metadata)"),"Audio filter accepts confirmed audio metadata");
        Check(EngineRunner.UnsupportedDiagnostic("ERROR: Unsupported URL: [URL]"),"Unsupported URL classified distinctly from authentication");
        Check(EngineRunner.UnsupportedDiagnostic("WARNING: This extractor is marked as broken") && !EngineRunner.UnsupportedDiagnostic("HTTP Error 429"),"Broken extractor diagnostic has its own failure category");
        {
            var fake=new SimulatedEngine { Scenario="unsupported" };var manager=Manager(fake);bool unsupported=false;
            try { await manager.Analyze("https://media.example/profile",CancellationToken.None); } catch(UnsupportedResourceException) { unsupported=true; }
            Check(unsupported && manager.State.Holds.Count==0,"Unsupported profile does not request authentication or create source hold");
            var job=manager.CreateCollection(new ResourceInfo { Url="https://media.example/profile" });await manager.Discover(job);
            Check(job.Status=="Unsupported" && job.Error.Contains("cookies cannot repair") && job.Entries.Count==0,"Unsupported discovery retains actionable engine limitation");
        }
        Check(AppInfo.Version=="1.1.0", "Application version 1.1.0");
        Console.WriteLine("ALL " + passed + " CHECKS PASSED");
    }
    static int Main(string[] args)
    {
        root=Path.GetFullPath(args.Length>0 ? args[0] : "test-output"); Directory.CreateDirectory(root);
        try { Run().GetAwaiter().GetResult(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}






