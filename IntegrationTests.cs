using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
class IntegrationTests
{
    static async Task Serve(TcpListener server,byte[] media,CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try { client=await server.AcceptTcpClientAsync(); } catch (ObjectDisposedException) { return; } catch (SocketException) { return; }
            using (client)
            using (var stream=client.GetStream())
            {
                byte[] request=new byte[8192]; int n=await stream.ReadAsync(request,0,request.Length);
                string head=Encoding.ASCII.GetString(request,0,n);
                byte[] header=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: video/mp4\r\nContent-Length: "+media.Length+"\r\nConnection: close\r\n\r\n");
                try { await stream.WriteAsync(header,0,header.Length); if (!head.StartsWith("HEAD")) await stream.WriteAsync(media,0,media.Length); } catch (IOException) { }
            }
        }
    }
    static async Task Run()
    {
        string root=Path.GetFullPath("test-output/integration"); Directory.CreateDirectory(root);
        var engine=new EngineRunner { BaseDirectory=Directory.GetCurrentDirectory() }; string sample=Path.Combine(root,"fixture.mp4");
        var generated=await engine.Run("ffmpeg",new[]{"-y","-nostdin","-hide_banner","-loglevel","error","-f","lavfi","-i","testsrc=size=160x90:rate=10","-f","lavfi","-i","sine=frequency=440","-t","1","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",sample},root,Console.WriteLine,Console.WriteLine,CancellationToken.None,30);
        if (generated.Code!=0) throw new Exception("Fixture generation failed");
        var server=new TcpListener(IPAddress.Loopback,0); server.Start(); var cancel=new CancellationTokenSource();
        Task serving=Serve(server,File.ReadAllBytes(sample),cancel.Token);
        try
        {
            string url="http://127.0.0.1:"+((IPEndPoint)server.LocalEndpoint).Port+"/fixture.mp4";
            var manager=new DownloadManager(new StateStore(Path.Combine(root,Guid.NewGuid().ToString("N"))),engine); manager.State.Destination=root; manager.State.PaceSeconds=0; manager.Log=Console.WriteLine;
            var info=await manager.Analyze(url,CancellationToken.None);
            if (info.IsCollection || String.IsNullOrEmpty(info.Extractor)) throw new Exception("Real generic classification failed");
            Console.WriteLine("PASS: real yt-dlp generic direct-media classification: "+info.Extractor);
            foreach (MediaMode mode in Enum.GetValues(typeof(MediaMode)))
            {
                foreach (bool premiere in (mode==MediaMode.Mp3 ? new[]{false} : new[]{false,true}))
                {
                    manager.State.Mode=mode; manager.State.Premiere=premiere;
                    manager.Add(url,"Fixture",info.Extractor,info.Id,null); await manager.Start();
                    var item=manager.State.Queue.Last();
                    if (item.Status!="Completed" || !File.Exists(item.OutputFile)) throw new Exception(mode+" Premiere="+premiere+": "+item.Status+" "+item.Error);
                    string probe="";
                    var probeResult=await engine.Run("ffmpeg",new[]{"-hide_banner","-i",item.OutputFile},root,delegate{},delegate(string line){probe+=line+"\n";},CancellationToken.None,30);
                    bool audio=probe.Contains("Audio:"),video=probe.Contains("Video:");
                    if (mode==MediaMode.VideoOnly && (audio || !video)) throw new Exception("Video-only output contains audio or no video");
                    if (mode==MediaMode.VideoAudio && (!audio || !video)) throw new Exception("Missing A/V streams");
                    if (mode==MediaMode.Mp3 && (!audio || video || Path.GetExtension(item.OutputFile)!=".mp3")) throw new Exception("Invalid MP3");
                    Console.WriteLine("PASS: actual yt-dlp/FFmpeg "+mode+" Premiere="+premiere+" audio="+audio+" video="+video);
                }
            }
            using (var stop = new CancellationTokenSource(500))
            {
                bool cancelled=false;
                try { await engine.Run("ffmpeg",new[]{"-nostdin","-hide_banner","-loglevel","error","-re","-stream_loop","-1","-i",sample,"-f","null","NUL"},root,delegate{},delegate{},stop.Token,10); }
                catch(OperationCanceledException) { cancelled=true; }
                if(!cancelled) throw new Exception("Real process cancellation failed");
                Console.WriteLine("PASS: real FFmpeg process-tree cancellation");
            }
            var next=await engine.Run("ffmpeg",new[]{"-version"},root,delegate{},delegate{},CancellationToken.None,10);
            if(next.Code!=0) throw new Exception("Job after cancelled process failed");
            Console.WriteLine("PASS: subsequent process unaffected by prior cancellation");
            Console.WriteLine("ALL 8 REAL ENGINE CHECKS PASSED");
        }
        finally { cancel.Cancel(); server.Stop(); }
        await serving;
    }
    static int Main() { try { Run().GetAwaiter().GetResult(); return 0; } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; } }
}


