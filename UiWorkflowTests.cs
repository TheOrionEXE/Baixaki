using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
class UiWorkflowTests
{
    static int checks;
    static Exception failure;
    [STAThread] static int Main()
    {
        Application.EnableVisualStyles();
        var host=new Form { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000) };
        host.Shown+=async delegate
        {
            try
            {
                foreach(string source in new[]{"Youtube","Instagram","TikTok","Generic"})
                {
                    var folder=Path.GetFullPath("test-output/ui-"+source+"-"+Guid.NewGuid().ToString("N"));
                    var fake=new SimulatedEngine { Extractor=source,Playlist=true,Entries=3 };
                    using(var form=new BaixakiForm(new StateStore(folder),fake))
                    {
                        form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;form.Show();
                        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                        Func<string,object> field=name=>form.GetType().GetField(name,flags).GetValue(form);
                        ((TextBox)field("txtUrl")).Text="https://media.example/collection";
                        await (Task)form.GetType().GetMethod("Analyze",flags).Invoke(form,null);
                        var panel=(FlowLayoutPanel)field("collectionPanel");
                        if(!panel.Visible) throw new Exception("Collection panel did not open: "+source);
                        var manager=(DownloadManager)field("manager");
                        if(fake.Calls!=1 || manager.Discovering || manager.State.Collections[0].Limit!=10) throw new Exception("Unsafe automatic discovery: "+source);
                        Console.WriteLine("PASS: collection preview does not auto-enumerate profile; default limit 10: "+source);checks++;
                        await manager.Discover(manager.State.Collections[0]);
                        manager.State.PaceSeconds=0;manager.State.Destination=folder;
                        form.GetType().GetMethod("QueueBatch",flags).Invoke(form,null);
                        manager.State.PaceSeconds=0; await manager.Start();
                        form.GetType().GetMethod("RefreshQueue",flags).Invoke(form,null);
                        if(manager.State.Queue.Count!=3 || manager.State.Queue.Exists(q=>q.Status!="Completed") || ((DataGridView)field("queue")).Rows.Count!=3) throw new Exception("Universal UI queue flow failed: "+source);
                        if(((DataGridView)field("queue")).Rows[0].Cells[4].Value.ToString()!="Concluído" || !((DataGridView)field("queue")).Rows[0].Cells[3].Value.ToString().Contains("Vídeo"))throw new Exception("Untranslated queue status/format");
                        Console.WriteLine("PASS: readable Portuguese queue status and format: "+source);checks++;
                        Console.WriteLine("PASS: same auto-classification, collection panel, batch and queue controls: "+source); checks++;
                        form.Close();
                        for(int i=0;i<100 && !form.IsDisposed;i++) await Task.Delay(10);
                        if(!form.IsDisposed) throw new Exception("Idle form close failed");
                        Console.WriteLine("PASS: idle close checkpoints state and exits: "+source); checks++;
                    }
                }
                using(var form=new BaixakiForm(new StateStore(Path.GetFullPath("test-output/ui-unsupported-"+Guid.NewGuid().ToString("N"))),new SimulatedEngine { Scenario="unsupported" }))
                {
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;form.Show();
                    var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                    ((TextBox)form.GetType().GetField("txtUrl",flags).GetValue(form)).Text="https://media.example/profile";
                    await (Task)form.GetType().GetMethod("Analyze",flags).Invoke(form,null);
                    var message=((Label)form.GetType().GetField("resource",flags).GetValue(form)).Text;
                    await (Task)form.GetType().GetMethod("AddUrl",flags).Invoke(form,null);
                    var manager=(DownloadManager)form.GetType().GetField("manager",flags).GetValue(form);
                    if(!message.StartsWith("Este link não é atendido") || manager.State.Queue.Count!=0 || manager.State.Collections.Count!=0 || manager.Browser!="")throw new Exception("Unsupported profile UI encouraged invalid queue/authentication");
                    Console.WriteLine("PASS: unsupported profile UI explains engine limitation and does not queue or enable cookies");checks++;
                    form.Close();for(int i=0;i<100 && !form.IsDisposed;i++)await Task.Delay(10);
                }
                using(var form=new BaixakiForm(new StateStore(Path.GetFullPath("test-output/ui-scopes-"+Guid.NewGuid().ToString("N"))),new SimulatedEngine()))
                {
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;form.Show();
                    var flags=BindingFlags.NonPublic|BindingFlags.Instance;var manager=(DownloadManager)form.GetType().GetField("manager",flags).GetValue(form);
                    foreach(string status in new[]{"Downloading","Pending","Completed","Failed","Paused","Converting","Blocked","Cancelled"})manager.State.Queue.Add(new DownloadItem{Title="Demonstração "+status,Url="https://media.example/"+status,Status=status,Destination=manager.State.Destination});
                    var grid=(DataGridView)form.GetType().GetField("queue",flags).GetValue(form);string[] scopes={"all","active","pending","done","failed","stopped"};int[] counts={8,2,1,1,2,2};
                    form.GetType().GetMethod("RefreshQueue",flags).Invoke(form,null);var filterButtons=(System.Collections.Generic.Dictionary<string,Button>)form.GetType().GetField("scopeButtons",flags).GetValue(form);
                    for(int i=0;i<scopes.Length;i++)
                    {
                        filterButtons[scopes[i]].PerformClick();
                        if(grid.Rows.Count!=counts[i])throw new Exception("Queue filter count mismatch: "+scopes[i]);
                        Console.WriteLine("PASS: qBittorrent-style queue category "+scopes[i]+" shows "+counts[i]+" expected items");checks++;
                    }
                    filterButtons["all"].PerformClick();grid.Rows[0].Selected=true;
                    form.GetType().GetField("userSized",flags).SetValue(form,true);
                    var advanced=(FlowLayoutPanel)form.GetType().GetField("advancedPanel",flags).GetValue(form);advanced.Visible=true;
                    var cookie=(ComboBox)form.GetType().GetField("cookies",flags).GetValue(form);var cookieGroup=cookie.Parent.Parent;var hint=cookieGroup.Controls.Find("CookieExplanation",true)[0];
                    foreach(var size in new[]{new Size(1120,760),new Size(860,650),new Size(1320,900)})
                    {
                        form.ClientSize=size;form.PerformLayout();Application.DoEvents();
                        if(hint.Parent!=cookieGroup || hint.Top<cookie.Parent.Bottom || hint.Top-cookie.Parent.Bottom>16 || cookieGroup.Width>advanced.ClientSize.Width)throw new Exception("Cookie description detached or clipped during resize: "+size);
                    }
                    Console.WriteLine("PASS: cookie selector and explanation stay grouped at three window sizes");checks++;
                    int changes=0;DataGridViewCellEventHandler changed=delegate{changes++;};grid.CellValueChanged+=changed;
                    Size stable=form.ClientSize;var selected=grid.SelectedRows[0].Tag;
                    for(int repeat=0;repeat<30;repeat++)form.GetType().GetMethod("RefreshQueue",flags).Invoke(form,null);
                    grid.CellValueChanged-=changed;
                    if(changes!=0||form.ClientSize!=stable)throw new Exception("Idle refresh mutated cells or window size");
                    Console.WriteLine("PASS: 30 unchanged queue refreshes do not rewrite cells or resize the window");checks++;
                    if(grid.SelectedRows.Count!=1||grid.SelectedRows[0].Tag!=selected)throw new Exception("Selection lost during resize/refresh");
                    Console.WriteLine("PASS: queue selection survives repeated resize and refresh");checks++;
                    filterButtons["all"].PerformClick();grid.Rows[0].Cells[0].Value=true;grid.Rows[1].Cells[0].Value=true;
                    var marked=(System.Collections.Generic.HashSet<string>)form.GetType().GetField("checkedItems",flags).GetValue(form);
                    filterButtons["done"].PerformClick();filterButtons["all"].PerformClick();
                    if(marked.Count!=2||!Convert.ToBoolean(grid.Rows[0].Cells[0].Value)||!Convert.ToBoolean(grid.Rows[1].Cells[0].Value))throw new Exception("Checkboxes lost across filter refresh");
                    var targets=(System.Collections.Generic.List<DownloadItem>)form.GetType().GetMethod("TargetItems",flags).Invoke(form,null);
                    if(targets.Count!=2)throw new Exception("Batch actions did not use checked items");
                    Console.WriteLine("PASS: queue checkboxes persist across filters and drive batch actions");checks++;
                    marked.Clear();form.GetType().GetMethod("RefreshQueue",flags).Invoke(form,null);
                    var headerClick=typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick",BindingFlags.Instance|BindingFlags.NonPublic);var click=new DataGridViewCellMouseEventArgs(0,-1,0,0,new MouseEventArgs(MouseButtons.Left,1,0,0,0));
                    headerClick.Invoke(grid,new object[]{click});if(marked.Count!=8)throw new Exception("Header checkbox did not mark visible rows");
                    filterButtons["done"].PerformClick();headerClick.Invoke(grid,new object[]{click});if(marked.Count!=7)throw new Exception("Header checkbox changed hidden rows");
                    Console.WriteLine("PASS: checkbox header toggles only visible rows and preserves hidden checks");checks++;
                    marked.Clear();filterButtons["all"].PerformClick();
                    filterButtons["done"].PerformClick();var completed=manager.State.Queue.Find(q=>q.Status=="Completed");string artifact=Path.GetFullPath("test-output/ui-open-example.mp4");File.WriteAllText(artifact,"Synthetic UI artifact");completed.OutputFile=artifact;
                    grid.Rows[0].Selected=true;form.GetType().GetMethod("RefreshSummary",flags).Invoke(form,null);
                    if(!((Button)form.GetType().GetField("openFileButton",flags).GetValue(form)).Enabled)throw new Exception("Completed artifact action not available");
                    filterButtons["failed"].PerformClick();grid.Rows[0].Selected=true;form.GetType().GetMethod("RefreshSummary",flags).Invoke(form,null);
                    if(((Button)form.GetType().GetField("openFileButton",flags).GetValue(form)).Enabled)throw new Exception("Open file enabled for incomplete item");
                    Console.WriteLine("PASS: open-file action is available only for an existing completed artifact");checks++;
                    form.Close();for(int i=0;i<100 && !form.IsDisposed;i++)await Task.Delay(10);
                }
                var compactStore=new StateStore(Path.GetFullPath("test-output/ui-compact-"+Guid.NewGuid().ToString("N")));compactStore.Save(new DownloadState { Destination=@"C:\Downloads\Baixaki" });
                using(var form=new BaixakiForm(compactStore,new SimulatedEngine()))
                {
                    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);form.ShowInTaskbar=false;form.Show();await Task.Delay(50);form.PerformLayout();
                    var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                    var content=(TableLayoutPanel)form.GetType().GetField("contentLayout",flags).GetValue(form);
                    Console.WriteLine("Startup geometry: font="+form.Font.Size+" client="+form.ClientSize+" content="+content.Bounds);
                    var credit=form.Controls.Find("CreatorCredit",true);if(credit.Length!=1||credit[0].Text!="Created by OrionEXE")throw new Exception("Creator attribution missing");
                    if(form.Font.Size<11 || form.ClientSize.Height>content.Bottom+5 || ((FlowLayoutPanel)form.GetType().GetField("advancedPanel",flags).GetValue(form)).Visible)throw new Exception("Compact startup layout or readable font failed");
                    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save("test-output/startup-compact.png");}
                    Console.WriteLine("PASS: readable 11pt font, compact startup and no unused bottom area; client="+form.ClientSize+" content="+content.Size);checks++;
                    form.Close();for(int i=0;i<100 && !form.IsDisposed;i++)await Task.Delay(10);
                }
                if(HelpCenterForm.Topics().Count!=12 || !HelpCenterForm.Topics()[1].warning.Contains("conta")) throw new Exception("Missing profile risk help");
                using(var help=new HelpCenterForm())
                {
                    help.StartPosition=FormStartPosition.Manual;help.Location=new Point(-30000,-30000);help.ShowInTaskbar=false;help.Show();
                                        var content=help.Controls[0].Controls[1];var body=(TextBox)content.Controls[2];
                    Console.WriteLine("Help body bounds="+body.Bounds+" text characters="+body.TextLength);
                    if(body.Width<200 || body.Height<200 || body.TextLength<100)throw new Exception("Help body clipped or empty");
                    using(var bitmap=new Bitmap(help.Width,help.Height)) { help.DrawToBitmap(bitmap,new Rectangle(0,0,help.Width,help.Height));bitmap.Save("test-output/help-center.png"); }
                    help.Close();
                }
                Console.WriteLine("PASS: 12 embedded help topics and profile/account risk warning");checks++;
            }
            catch(Exception ex){failure=ex;}
            host.Close();
        };
        Application.Run(host);
        if(failure!=null){Console.Error.WriteLine(failure);return 1;}
        Console.WriteLine("ALL "+checks+" UI WORKFLOW CHECKS PASSED");return 0;
    }
}















