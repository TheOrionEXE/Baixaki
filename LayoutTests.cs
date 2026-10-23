using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class LayoutTests
{
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles();
        var folder = Path.GetFullPath("test-output/layout-state"); Directory.CreateDirectory(folder);
        var state = new DownloadState(); state.Destination = @"C:\Downloads\Baixaki"; state.Collections.Add(new CollectionJob { Url="https://example.com/collection", Title="Coleção demonstrativa • metadados da fonte", Extractor="Generic", Status="Partial" });
        state.Queue.Add(new DownloadItem { Title="Vídeo em download — demonstração", Url="https://example.com/item", Extractor="Generic", Status="Downloading", Percent=42.5, Mode=MediaMode.VideoOnly, Premiere=true });
        state.Queue.Add(new DownloadItem { Title="Próximo vídeo da fila",Url="https://example.com/waiting",Extractor="Generic",Status="Pending",Mode=MediaMode.VideoAudio,Destination=state.Destination });
        state.Queue.Add(new DownloadItem { Title="Download concluído — demonstração",Url="https://example.com/done",Extractor="Generic",Status="Completed",Percent=100,Mode=MediaMode.Mp3,Destination=state.Destination });
        state.Queue.Add(new DownloadItem { Title="Item com falha — consulte detalhes",Url="https://example.com/failed",Extractor="Generic",Status="Failed",Error="Erro demonstrativo de conexão",Destination=state.Destination });
        string completed=Path.GetFullPath("test-output/demo-completed.mp4");File.WriteAllText(completed,"Synthetic artifact for UI fixture only");state.Queue[2].OutputFile=completed;
        var store = new StateStore(folder); store.Save(state);
        using (var form = new BaixakiForm(store,new EngineRunner()))
        {
            // Invoke layout initialization without showing a window or running network update checks.
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000,-30000); form.ShowInTaskbar = false; form.Show(); Application.DoEvents();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            form.GetType().GetMethod("RefreshCollections",flags).Invoke(form,new object[]{null});
            var demo=(DownloadManager)form.GetType().GetField("manager",flags).GetValue(form);demo.State.Queue[0].Status="Downloading";demo.Running=true;
            form.GetType().GetMethod("RefreshQueue",flags).Invoke(form,null);
            form.GetType().GetMethod("Log",flags).Invoke(form,new object[]{"Pronto. Consulte a Central de ajuda (F1) antes de consultar perfis."});
            foreach (var size in new[] { new Size(1120,840),new Size(800,650),new Size(1500,1050) })
            {
                form.ClientSize=size; form.PerformLayout();
                using (var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height)); bitmap.Save("test-output/layout-"+size.Width+".png"); }
                Console.WriteLine("Rendered " + size);
            }
            form.Scale(new SizeF(1.5f,1.5f)); form.PerformLayout();
            using (var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height)); bitmap.Save("test-output/layout-scaled.png"); }
        }
        Console.WriteLine("Layout render checks completed"); return 0;
    }
}







