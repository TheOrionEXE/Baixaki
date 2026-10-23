// Native Windows Forms; build with the Windows .NET Framework compiler.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class BaixakiForm : Form
{
    static readonly string AppVersion = AppInfo.Version;
    const string UpdateRepo = "TheOrionEXE/Baixaki";
    static readonly Color Muted = Color.FromArgb(162,169,189);
    static readonly Color Bg = Color.FromArgb(18,20,26), Surface = Color.FromArgb(31,35,46), Secondary = Color.FromArgb(42,47,62), Accent = Color.FromArgb(214,48,180), Ink = Color.FromArgb(235,237,245);
    readonly DownloadManager manager;
    TextBox txtUrl, destination;
    TextBox log;
    Button btnDownload, start, pause, engine, analyze;
    ComboBox mode, resourceMode, cookies, jobs, filter, limit;
    CheckBox premiere, duplicates, ownFolder;
    NumericUpDown pace, batch, customLimit;
    DataGridView queue;
    Label resource, collectionSummary, queueSummary;
    FlowLayoutPanel collectionPanel, advancedPanel;
    Control collectionHost, queueActions;
    FlowLayoutPanel queueFilters;
    string queueScope = "all";
    readonly HashSet<string> checkedItems = new HashSet<string>();
    bool refreshingChecks;
    readonly System.Collections.Generic.Dictionary<string,Button> scopeButtons = new System.Collections.Generic.Dictionary<string,Button>();
    Button openFileButton, openFolderButton;
    Label emptyQueue;
    Button discoverButton, nextBatch, stopDiscovery, retrySelection, cancelSelection, removeSelection, clearCompleted, detailsButton, releaseHolds;
    CancellationTokenSource analysis;
    ResourceInfo currentResource;
    bool refreshing, closing, shuttingDown, userSized, resizing;
    TableLayoutPanel contentLayout;
    string analyzedUrl, unsupportedUrl;
    System.Windows.Forms.Timer debounce, refresh;
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle, int attr, ref int value, int size);
    [STAThread] static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new BaixakiForm());
    }
    public BaixakiForm() : this(new StateStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Baixaki")), new EngineRunner(), true) { }
    public BaixakiForm(StateStore store, EngineRunner runner, bool startup = false)
    {
        manager=new DownloadManager(store,runner);
        Text="Baixaki — v"+AppVersion;Font=new Font("Segoe UI",11F);BackColor=Bg;ForeColor=Ink;
        AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(1120,760);MinimumSize=new Size(820,560);StartPosition=FormStartPosition.CenterScreen;
        try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
        var scrollHost=new BufferedPanel{Dock=DockStyle.Fill,AutoScroll=true};Controls.Add(scrollHost);
        var root=new BufferedTable{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=9,Padding=new Padding(18),BackColor=Bg};
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<9;i++)root.RowStyles.Add(new RowStyle(i==5?SizeType.Absolute:SizeType.AutoSize,i==5?250:0));scrollHost.Controls.Add(root);contentLayout=root;
        var header=Row();
        if(Icon!=null)header.Controls.Add(new PictureBox{Image=Icon.ToBitmap(),Size=new Size(44,44),SizeMode=PictureBoxSizeMode.Zoom,Margin=new Padding(0,0,12,0)});
        header.Controls.Add(new Label{Text="Baixaki",Font=new Font("Segoe UI Semibold",25F),AutoSize=true,Margin=new Padding(0,0,12,0)});
        header.Controls.Add(Label("v"+AppVersion+"  ·  Vídeos, áudios e coleções"));
        header.Controls.Add(Button("Central de ajuda  ·  F1",delegate{OpenHelp();}));
        var headerFrame=new HeaderLayout{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,BackColor=Bg,Margin=new Padding(0)};headerFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));headerFrame.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));headerFrame.Controls.Add(header,0,0);
        var attribution=new Label{Text="Created by OrionEXE",Name="CreatorCredit",AutoSize=true,ForeColor=Muted,Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=new Padding(12,0,0,0)};headerFrame.Controls.Add(attribution,1,0);root.Controls.Add(headerFrame,0,0);

        var inputCard=new UiCard();inputCard.Add(Heading("Link para download"));
        var input=new BufferedTable{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,BackColor=Surface};
        input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));input.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));input.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        txtUrl=TextField();txtUrl.Font=new Font(Font.FontFamily,12F);input.Controls.Add(FieldFrame(txtUrl,300),0,0);
        analyze=AsyncButton("Analisar link",async delegate{await Analyze();});input.Controls.Add(analyze,1,0);
        btnDownload=AsyncButton("Adicionar à fila",async delegate{await AddUrl();});btnDownload.BackColor=Accent;input.Controls.Add(btnDownload,2,0);inputCard.Add(input);
        resource=Label("Cole um link de vídeo ou coleção. A análise é automática; os downloads começam somente quando você iniciar a fila.");resource.ForeColor=Muted;resource.Margin=new Padding(0,8,0,0);
        inputCard.Resize+=delegate{resource.MaximumSize=new Size(Math.Max(200,inputCard.ClientSize.Width-34),0);};inputCard.Add(resource);root.Controls.Add(inputCard,0,1);

        var settingsCard=new UiCard();settingsCard.Add(Heading("Formato e destino"));
        var settings=Row();settings.Controls.Add(Label("Baixar como"));mode=Combo("Vídeo + áudio","Vídeo sem áudio","Áudio MP3");mode.Width=180;mode.SelectedIndex=(int)manager.State.Mode;settings.Controls.Add(mode);
        premiere=new CheckBox{Text="Preparar para Premiere",Checked=manager.State.Premiere,AutoSize=true,Margin=new Padding(15,11,7,5)};settings.Controls.Add(premiere);
        var folder=settings;folder.Controls.Add(Label("Salvar em"));destination=TextField();destination.ReadOnly=true;destination.Text=manager.State.Destination;var destinationFrame=FieldFrame(destination,300);destinationFrame.Dock=DockStyle.None;folder.Controls.Add(destinationFrame);
        folder.Controls.Add(Button("Escolher pasta…",delegate{using(var dialog=new FolderBrowserDialog{Description="Escolha a pasta para os novos downloads",SelectedPath=manager.State.Destination})if(dialog.ShowDialog(this)==DialogResult.OK){manager.State.Destination=dialog.SelectedPath;destination.Text=dialog.SelectedPath;PersistSettings();}}));settingsCard.Add(folder);root.Controls.Add(settingsCard,0,2);

        var advancedCard=new UiCard();var advancedHeader=Row();var advancedToggle=Button("Opções avançadas  ▾",delegate{});advancedHeader.Controls.Add(advancedToggle);advancedHeader.Controls.Add(Label("Intervalo, duplicatas, tipo do link e autenticação opcional"));advancedCard.Add(advancedHeader);
        advancedPanel=Row();advancedPanel.Visible=false;
        advancedPanel.Controls.Add(Label("Tipo do link"));resourceMode=Combo("Detectar automaticamente","Um vídeo / áudio","Coleção de itens");resourceMode.Width=215;advancedPanel.Controls.Add(resourceMode);
        advancedPanel.Controls.Add(Label("Intervalo (segundos)"));pace=Number(1,120,manager.State.PaceSeconds);advancedPanel.Controls.Add(pace);
        duplicates=new CheckBox{Text="Ignorar arquivos já baixados",Checked=manager.State.Duplicates,AutoSize=true,Margin=new Padding(12,11,7,5)};advancedPanel.Controls.Add(duplicates);
        advancedPanel.SetFlowBreak(duplicates,true);
        var cookieGroup=new BufferedTable{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,RowCount=2,Margin=new Padding(4,8,4,4),BackColor=Color.Transparent};
        cookieGroup.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));cookieGroup.RowStyles.Add(new RowStyle(SizeType.AutoSize));cookieGroup.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var cookieHeader=Row();cookieHeader.Controls.Add(Label("Cookies do navegador"));cookies=Combo("Não usar","chrome","edge","firefox","brave","opera","vivaldi","chromium");cookies.Width=170;cookieHeader.Controls.Add(cookies);cookieGroup.Controls.Add(cookieHeader,0,0);
        var authHint=Label("Opcional: associa as consultas à sua conta. Use apenas se precisar de login. Cookies não corrigem um extrator sem suporte.");authHint.Name="CookieExplanation";authHint.ForeColor=Muted;authHint.MaximumSize=new Size(630,0);authHint.Margin=new Padding(5,0,5,5);cookieGroup.Controls.Add(authHint,0,1);
        cookieGroup.Name="CookieSettings";advancedPanel.Controls.Add(cookieGroup);advancedCard.Add(advancedPanel);
        advancedToggle.Click+=delegate{advancedPanel.Visible=!advancedPanel.Visible;advancedToggle.Text=advancedPanel.Visible?"Opções avançadas  ▴":"Opções avançadas  ▾";FitWindowToContent();};root.Controls.Add(advancedCard,0,3);

        var collectionCard=new UiCard();collectionCard.Visible=false;
        collectionPanel=Row();collectionPanel.Margin=new Padding(0);collectionCard.Add(collectionPanel);
        var collectionTitle=Heading("Coleção de mídia");collectionPanel.Controls.Add(collectionTitle);jobs=Combo();jobs.Width=310;collectionPanel.Controls.Add(jobs);collectionPanel.SetFlowBreak(jobs,true);
        collectionPanel.Controls.Add(Label("Buscar até"));limit=Combo("10","50","100","Todos os disponíveis","Quantidade personalizada");limit.SelectedIndex=0;limit.Width=220;collectionPanel.Controls.Add(limit);
        customLimit=Number(1,1000000,2000);customLimit.Visible=false;collectionPanel.Controls.Add(customLimit);
        collectionPanel.Controls.Add(Label("Itens por lote"));batch=Number(1,10000,50);collectionPanel.Controls.Add(batch);
        ownFolder=new CheckBox{Text="Criar subpasta da coleção",AutoSize=true,Margin=new Padding(12,11,7,5)};collectionPanel.Controls.Add(ownFolder);collectionPanel.SetFlowBreak(ownFolder,true);
        collectionPanel.Controls.Add(Label("Incluir"));filter=Combo("Toda mídia acessível","Vídeos com tipo identificado","Com áudio confirmado","Outros tipos: não suportados");filter.Width=265;collectionPanel.Controls.Add(filter);
        discoverButton=AsyncButton("Buscar itens",async delegate{await Discover();});collectionPanel.Controls.Add(discoverButton);
        nextBatch=Button("Adicionar lote à fila",delegate{QueueBatch();});nextBatch.BackColor=Accent;collectionPanel.Controls.Add(nextBatch);
        stopDiscovery=Button("Parar busca",delegate{manager.StopDiscovery();});collectionPanel.Controls.Add(stopDiscovery);collectionPanel.SetFlowBreak(stopDiscovery,true);
        collectionSummary=Label("Uma busca consulta a fonte. Revise o limite e o aviso antes de continuar.");collectionSummary.ForeColor=Muted;collectionPanel.Controls.Add(collectionSummary);
        collectionCard.Resize+=delegate{collectionPanel.MaximumSize=new Size(Math.Max(200,collectionCard.ClientSize.Width-32),0);collectionSummary.MaximumSize=collectionPanel.MaximumSize;};
        collectionHost=collectionCard;root.Controls.Add(collectionCard,0,4);

        var queueCard=new UiCard{Dock=DockStyle.Fill,AutoSize=false};queueCard.RowStyles.Clear();queueCard.RowCount=3;queueCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));queueCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));queueCard.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var queueHeader=Row();queueHeader.Controls.Add(Heading("Sua fila"));start=AsyncButton("Iniciar downloads",async delegate{await Guard(async delegate{await manager.Start();});});start.BackColor=Accent;queueHeader.Controls.Add(start);
        pause=Button("Pausar",delegate{manager.Pause();});queueHeader.Controls.Add(pause);queueSummary=Label("");queueSummary.ForeColor=Muted;queueHeader.Controls.Add(queueSummary);queueCard.Controls.Add(queueHeader,0,0);
        queueFilters=Row();
        foreach(string key in new[]{"all","active","pending","done","failed","stopped"})
        {
            string scope=key;var button=Button("",delegate{queueScope=scope;RefreshQueue();});button.Font=new Font(Font.FontFamily,11F);scopeButtons.Add(scope,button);queueFilters.Controls.Add(button);
        }
        queueCard.Controls.Add(queueFilters,0,1);
        queue=new BufferedGrid{Dock=DockStyle.Fill,ReadOnly=false,EditMode=DataGridViewEditMode.EditOnEnter,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AutoGenerateColumns=false,BackgroundColor=Surface,BorderStyle=BorderStyle.None,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=true,EnableHeadersVisualStyles=false,MinimumSize=new Size(0,145),CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,GridColor=Color.FromArgb(44,50,63)};
        queue.DefaultCellStyle.BackColor=Surface;queue.DefaultCellStyle.ForeColor=Ink;queue.DefaultCellStyle.SelectionBackColor=Color.FromArgb(65,43,74);queue.DefaultCellStyle.SelectionForeColor=Ink;
        queue.ColumnHeadersDefaultCellStyle.BackColor=Secondary;queue.ColumnHeadersDefaultCellStyle.ForeColor=Muted;queue.ColumnHeadersDefaultCellStyle.Font=new Font(Font,FontStyle.Bold);queue.ColumnHeadersHeight=38;queue.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;queue.RowTemplate.Height=38;queue.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        queue.Columns.Add(new DataGridViewCheckBoxColumn{Name="Marked",HeaderText="✓",Width=38,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,ReadOnly=false,SortMode=DataGridViewColumnSortMode.NotSortable});
        AddColumn("Título",32);AddColumn("Fonte",12);AddColumn("Formato",23);AddColumn("Situação",15);AddColumn("Progresso",10);AddColumn("Velocidade",12);AddColumn("Detalhes do erro",20);queue.Columns[7].Visible=false;
        emptyQueue=new Label{Text="Nenhum download na fila.\nCole um link acima e escolha Adicionar à fila.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Muted,BackColor=Surface};queue.Controls.Add(emptyQueue);
        queue.CurrentCellDirtyStateChanged+=delegate{if(queue.IsCurrentCellDirty&&queue.CurrentCell!=null&&queue.CurrentCell.ColumnIndex==0)queue.CommitEdit(DataGridViewDataErrorContexts.Commit);};
        queue.CellValueChanged+=delegate(object sender,DataGridViewCellEventArgs e){if(refreshingChecks||e.RowIndex<0||e.ColumnIndex!=0)return;var item=queue.Rows[e.RowIndex].Tag as DownloadItem;if(item==null)return;if(Convert.ToBoolean(queue.Rows[e.RowIndex].Cells[0].Value))checkedItems.Add(item.Id);else checkedItems.Remove(item.Id);RefreshSummary();};
        queue.ColumnHeaderMouseClick+=delegate(object sender,DataGridViewCellMouseEventArgs e){if(e.ColumnIndex!=0)return;var visible=queue.Rows.Cast<DataGridViewRow>().Select(r=>r.Tag as DownloadItem).Where(q=>q!=null).ToList();bool mark=visible.Any(q=>!checkedItems.Contains(q.Id));foreach(var item in visible){if(mark)checkedItems.Add(item.Id);else checkedItems.Remove(item.Id);}RefreshQueue();};
        queue.SelectionChanged+=delegate{RefreshSummary();};queue.CellPainting+=PaintQueueProgress;queue.CellDoubleClick+=delegate(object sender,DataGridViewCellEventArgs e){if(e.RowIndex>=0)ShowItemDetails();};queueCard.Controls.Add(queue,0,2);root.Controls.Add(queueCard,0,5);
        var controls=Row();queueActions=controls;retrySelection=Button("Tentar novamente",delegate{Selected(delegate(DownloadItem item){if(new[]{"Failed","Cancelled","Paused","Blocked"}.Contains(item.Status)||(item.Status=="Completed"&&!File.Exists(item.OutputFile)))manager.Retry(item);});});controls.Controls.Add(retrySelection);
        cancelSelection=Button("Cancelar itens",delegate{if(ConfirmQueueAction("Cancelar","Interrompe os itens selecionados e limpa seus arquivos parciais. Para continuar depois sem perder esses parciais, prefira Pausar. Uma fonte já concluída é preservada."))Selected(delegate(DownloadItem item){if(new[]{"Pending","Paused","Downloading","Converting"}.Contains(item.Status))manager.Cancel(item);});});controls.Controls.Add(cancelSelection);
        removeSelection=Button("Remover da fila",delegate{if(ConfirmQueueAction("Remover","Remove os itens selecionados e limpa seus temporários. Arquivos finais e histórico são preservados. Tarefas ativas serão canceladas primeiro."))Selected(delegate(DownloadItem item){manager.Remove(item);});});controls.Controls.Add(removeSelection);
        clearCompleted=Button("Limpar concluídos",delegate{manager.State.Queue.RemoveAll(q=>q.Status=="Completed");manager.Save();RefreshQueue();});controls.Controls.Add(clearCompleted);
        detailsButton=Button("Ver detalhes",delegate{ShowItemDetails();});controls.Controls.Add(detailsButton);releaseHolds=Button("Revisar fontes em pausa",delegate{ReleaseHolds();});controls.Controls.Add(releaseHolds);openFileButton=Button("Abrir arquivo",delegate{OpenOutput(false);});controls.Controls.Add(openFileButton);openFolderButton=Button("Abrir pasta",delegate{OpenOutput(true);});controls.Controls.Add(openFolderButton);root.Controls.Add(controls,0,6);

        var activity=new UiCard();var tools=Row();var activityToggle=Button("Atividade técnica  ▾",delegate{});tools.Controls.Add(activityToggle);
        engine=AsyncButton("Atualizar yt-dlp",async delegate{await Guard(manager.UpdateEngine);});tools.Controls.Add(engine);tools.Controls.Add(Button("Atualizações do Baixaki",delegate{CheckForUpdate(true);}));activity.Add(tools);
        log=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Top,Height=125,ReadOnly=true,BackColor=Bg,ForeColor=Muted,Font=new Font("Consolas",10F),BorderStyle=BorderStyle.None,Visible=false};activity.Add(log);
        activityToggle.Click+=delegate{log.Visible=!log.Visible;activityToggle.Text=log.Visible?"Ocultar atividade  ▴":"Atividade técnica  ▾";FitWindowToContent();};root.Controls.Add(activity,0,7);
        HelpRequested+=delegate(object sender,HelpEventArgs e){OpenHelp();e.Handled=true;};manager.Log=Log;manager.Changed=delegate{if(!resizing)RefreshSummary();};
        mode.SelectedIndexChanged+=delegate{premiere.Enabled=mode.SelectedIndex!=2;PersistSettings();};premiere.Enabled=mode.SelectedIndex!=2;
        premiere.CheckedChanged+=delegate{PersistSettings();};pace.ValueChanged+=delegate{PersistSettings();};duplicates.CheckedChanged+=delegate{PersistSettings();};resourceMode.SelectedIndexChanged+=delegate{RefreshSummary();};
        cookies.SelectedIndexChanged+=delegate
        {
            if(cookies.SelectedIndex>0&&MessageBox.Show(this,"O yt-dlp tentará ler cookies do navegador e usá-los nas próximas consultas. Isso pode associar o acesso à sua conta e causar restrições. Cookies não corrigem suporte ausente ou quebrado.\n\nA escolha vale somente nesta sessão; valores não são salvos pelo Baixaki. Usar esse navegador?","Autenticação opcional",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes){cookies.SelectedIndex=0;return;}
            manager.Browser=cookies.SelectedIndex==0?"":cookies.Text;
        };
        jobs.SelectedIndexChanged+=delegate{LoadCollection();};limit.SelectedIndexChanged+=delegate{customLimit.Visible=limit.SelectedIndex==4;};
        filter.SelectedIndexChanged+=delegate{if(filter.SelectedIndex==3)Log("Outros tipos de mídia não são atendidos pela conversão de vídeo/MP3. Escolha um dos filtros de vídeo ou áudio.");};
        EventHandler saveCollection=delegate{if(!refreshing&&CurrentJob()!=null&&!manager.Discovering)SaveCollection(CurrentJob());};filter.SelectedIndexChanged+=saveCollection;limit.SelectedIndexChanged+=saveCollection;customLimit.ValueChanged+=saveCollection;batch.ValueChanged+=saveCollection;ownFolder.CheckedChanged+=saveCollection;
        debounce=new System.Windows.Forms.Timer{Interval=850};debounce.Tick+=async delegate{debounce.Stop();if(Safety.ValidUrl(txtUrl.Text.Trim()))await Analyze();};
        txtUrl.TextChanged+=delegate{debounce.Stop();if(analysis!=null)analysis.Cancel();currentResource=null;unsupportedUrl=null;resource.Text="Aguardando análise do link…";resource.ForeColor=Muted;debounce.Start();RefreshSummary();};
        refresh=new System.Windows.Forms.Timer{Interval=600};refresh.Tick+=delegate{RefreshQueue();};refresh.Start();
        Activated+=delegate{if(startup)FillClipboard();};Shown+=delegate{if(!startup)return;FillClipboard();if(store.Warning!=null)Log(store.Warning);RefreshCollections(null);RefreshQueue();CheckForUpdate();};FormClosing+=HandleClosing;ResizeBegin+=delegate{userSized=true;resizing=true;refresh.Stop();};ResizeEnd+=delegate{resizing=false;refresh.Start();RefreshQueue();};Shown+=delegate{FitWindowToContent();};
        var tips=new ToolTip{AutoPopDelay=12000,InitialDelay=400,ReshowDelay=150};
        tips.SetToolTip(btnDownload,"Adiciona um item à fila ou abre o painel de uma coleção. Não inicia downloads automaticamente.");tips.SetToolTip(start,"Processa a fila em sequência. Itens bloqueados pela fonte aguardam revisão.");tips.SetToolTip(pause,"Interrompe o processo ativo e preserva os arquivos parciais para retomada.");tips.SetToolTip(filter,"Filtros específicos exigem metadados do extrator. Toda mídia acessível inclui entradas com tipo ainda desconhecido.");tips.SetToolTip(queue,"Duplo clique em um item para ver o arquivo, o destino e o erro completo.");FormClosed+=delegate{tips.Dispose();};
        RefreshQueue();
    }
    protected override void OnHandleCreated(EventArgs e)
    { base.OnHandleCreated(e); try { int on = 1; DwmSetWindowAttribute(Handle,20,ref on,4); } catch { } }
    FlowLayoutPanel Row() { return new BufferedFlow { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, WrapContents = true, Margin = new Padding(0,3,0,3), BackColor = Color.Transparent }; }
    Label Label(string text) { return new Label { Text = text, AutoSize = true, Margin = new Padding(5,9,7,5) }; }
    TextBox TextField() { return new TextBox { BackColor = Surface, ForeColor = Ink, BorderStyle = BorderStyle.None, Margin = new Padding(0) }; }
    ComboBox Combo(params string[] values)
    {
        var combo = new ModernCombo { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Secondary, ForeColor = Ink, FlatStyle = FlatStyle.Flat, Width = 195, Margin = new Padding(4,5,4,5) };
        combo.Items.AddRange(values); if (values.Length > 0) combo.SelectedIndex = 0; return combo;
    }
    NumericUpDown Number(int min, int max, int value) { return new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min,Math.Min(max,value)), Width = 75, BackColor = Surface, ForeColor = Ink, Margin = new Padding(4,6,4,5) }; }
    Button Button(string text, Action action)
    {
        var button = new ModernButton { Text = text, AutoSize = true, MinimumSize = new Size(0,32), FlatStyle = FlatStyle.Flat, BackColor = Secondary, ForeColor = Ink, Margin = new Padding(3) };
        button.FlatAppearance.BorderSize = 0; button.Click += delegate { try { action(); } catch (Exception ex) { Feedback(ex is UnsupportedResourceException ? UiText.Unsupported : "Não foi possível concluir: " + Safety.Redact(ex.Message),true); } }; return button;
    }
    Button AsyncButton(string text, Func<Task> action)
    {
        var button = Button(text, delegate { });
        button.Click += async delegate { await Guard(action); }; return button;
    }
    Label Heading(string text){return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI Semibold",12F),Margin=new Padding(0,2,10,9)};}
    Panel FieldFrame(TextBox box,int width)
    {
        var panel=new Panel{Dock=DockStyle.Fill,Width=width,Height=42,BackColor=Secondary,Padding=new Padding(10,11,10,8),Margin=new Padding(0,3,8,3)};box.BackColor=Secondary;box.Dock=DockStyle.Fill;panel.Controls.Add(box);return panel;
    }
    List<DownloadItem> TargetItems()
    {
        if(checkedItems.Count>0)return manager.State.Queue.Where(q=>checkedItems.Contains(q.Id)).ToList();
        return queue.SelectedRows.Cast<DataGridViewRow>().Select(r=>r.Tag as DownloadItem).Where(q=>q!=null).ToList();
    }
    void ShowItemDetails()
    {
        var targets=TargetItems();if(targets.Count!=1)return;var item=targets[0];
        MessageBox.Show(this,(item.Title??"Mídia")+"\n\nSituação: "+UiText.Status(item.Status)+"\nFormato: "+UiText.Mode(item.Mode,item.Premiere)+"\nDestino: "+item.Destination+"\nArquivo: "+(item.OutputFile??"Ainda não concluído")+(String.IsNullOrEmpty(item.Error)?"":"\n\nErro informado pelo motor:\n"+item.Error),"Detalhes do download",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
    void OpenHelp() { using (var help = new HelpCenterForm()) help.ShowDialog(this); }
    void AddColumn(string title, int weight) { queue.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, FillWeight = weight, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable }); }
    void PersistSettings()
    {
        if (refreshing || mode == null || premiere == null || pace == null || duplicates == null) return;
        manager.State.Mode = (MediaMode)mode.SelectedIndex; manager.State.Premiere = premiere.Checked && mode.SelectedIndex != 2; manager.State.PaceSeconds = (int)pace.Value; manager.State.Duplicates = duplicates.Checked;
        try { manager.Save(); } catch (Exception ex) { Log("Não foi possível salvar as configurações: " + Safety.Redact(ex.Message)); }
    }
    void FillClipboard()
    {
        try { if (txtUrl.Text.Trim().Length == 0 && Clipboard.ContainsText() && Safety.ValidUrl(Clipboard.GetText().Trim())) txtUrl.Text = Clipboard.GetText().Trim(); } catch { }
    }
    void Log(string text)
    {
        if (IsDisposed || shuttingDown) return;
        if (InvokeRequired) { try { BeginInvoke(new Action<string>(Log),text); } catch { } return; }
        if (log.TextLength > 100000) log.Clear();
        log.AppendText(Safety.Redact(UiText.Message(text)) + Environment.NewLine); log.SelectionStart = log.TextLength; log.ScrollToCaret();
    }
    async Task Guard(Func<Task> action)
    { try { await action(); } catch (OperationCanceledException) { } catch (Exception ex) { Feedback(ex is UnsupportedResourceException ? UiText.Unsupported : "Não foi possível concluir: " + Safety.Redact(ex.Message),true); } }
    async Task Analyze()
    {
        if (debounce != null) debounce.Stop();
        string url = txtUrl.Text.Trim(); if (!Safety.ValidUrl(url)) { resource.Text = "Use um link HTTP(S) de página, sem senhas ou tokens embutidos."; return; }
        if (analyzedUrl == url && currentResource != null) return;
        if (analysis != null) { analysis.Cancel(); while (analysis != null) await Task.Delay(50); if (txtUrl.Text.Trim() != url) return; }
        var cancellation = new CancellationTokenSource(); analysis = cancellation; analyze.Enabled = false;
        unsupportedUrl = null;
        resource.Text = "Analisando o link… Essa consulta não inicia o download.";resource.ForeColor=Muted;
        try
        {
            ResourceInfo info = await manager.Analyze(url,cancellation.Token);
            if (txtUrl.Text.Trim() != url || cancellation.IsCancellationRequested) return;
            currentResource = info; analyzedUrl = url;
            resource.Text = info.Extractor + " • " + Safety.Redact(info.Title) + (info.IsCollection ? " · Coleção identificada — revise o painel abaixo" : " · Mídia individual — pronta para adicionar à fila");
            if (info.IsCollection && resourceMode.SelectedIndex != 1)
            {
                var job = manager.CreateCollection(info); RefreshCollections(job.Id);
                Log("Prévia de coleção aberta. Revise o limite e use Buscar itens para iniciar uma busca explícita.");
            }
        }
        catch (OperationCanceledException) { }
        catch (UnsupportedResourceException) { unsupportedUrl = url; currentResource = null; Feedback(UiText.Unsupported,true); }
        catch (Exception ex) { Feedback(manager.Held(url)?"A fonte está em pausa por uma restrição de acesso. Consulte a ajuda e resolva a causa antes de tentar novamente.":"Não foi possível identificar este link. Consulte a atividade técnica; se o tipo for conhecido, use Opções avançadas → Tipo do link.",true);Log(Safety.Redact(ex.Message)); }
        finally { if (analysis == cancellation) analysis = null; cancellation.Dispose(); if (!IsDisposed) {analyze.Enabled = true;RefreshSummary();FitWindowToContent();} }
    }
    async Task AddUrl()
    {
        debounce.Stop(); PersistSettings();
        string url = txtUrl.Text.Trim(); if (!Safety.ValidUrl(url)) { Feedback("Cole um link HTTP(S) sem credenciais ou tokens.",true); return; }
        if (unsupportedUrl == url) { Feedback(UiText.Unsupported,true); return; }
        if (resourceMode.SelectedIndex == 0 && currentResource == null) await Analyze();
        if (unsupportedUrl == url) return;
        if (txtUrl.Text.Trim() != url) return;
        bool collection = resourceMode.SelectedIndex == 2 || (resourceMode.SelectedIndex == 0 && currentResource != null && currentResource.IsCollection);
        if (resourceMode.SelectedIndex == 0 && currentResource == null) { Feedback("Escolha o tipo em Opções avançadas → Tipo do link se souber o formato do recurso. Isso não corrige restrições de acesso.",true); return; }
        if (collection)
        {
            var info = currentResource ?? new ResourceInfo { Url = url, Title = "Collection", Extractor = "Unknown", IsCollection = true };
            var job = manager.CreateCollection(info); RefreshCollections(job.Id); SaveCollection(job);
            Feedback("Coleção aberta. Revise as opções e use Buscar itens para consultar a fonte.",false);

        }
        else
        {
            bool added = manager.Add(url,currentResource == null ? "Media" : Safety.Redact(currentResource.Title),currentResource == null ? null : currentResource.Extractor,currentResource == null ? null : currentResource.Id,null);
            manager.Save(); if(added)txtUrl.Clear();Feedback(added?"Item adicionado. Use Iniciar downloads quando quiser começar.":"Este formato já está na fila ou possui um arquivo concluído existente.",!added);
        }
        RefreshQueue();
    }
    CollectionJob CurrentJob() { return jobs.SelectedIndex >= 0 && jobs.SelectedIndex < manager.State.Collections.Count ? manager.State.Collections[jobs.SelectedIndex] : null; }
    void RefreshCollections(string id)
    {
        refreshing = true; jobs.Items.Clear(); foreach (var job in manager.State.Collections) jobs.Items.Add((job.Extractor ?? "Fonte") + " • " + job.Title);
        collectionHost.Visible = jobs.Items.Count > 0;
        int index = manager.State.Collections.FindIndex(j => j.Id == id); if (jobs.Items.Count > 0) jobs.SelectedIndex = index >= 0 ? index : jobs.Items.Count - 1;
        refreshing = false; LoadCollection(); FitWindowToContent();
    }
    void LoadCollection()
    {
        if (refreshing) return; var job = CurrentJob(); if (job == null) return;
        refreshing = true; filter.SelectedIndex = Math.Max(0, Array.IndexOf(UiText.Filters,job.Filter)); ownFolder.Checked = job.OwnFolder; batch.Value = job.BatchSize;
        limit.SelectedIndex = job.Limit == 0 ? 3 : job.Limit == 10 ? 0 : job.Limit == 50 ? 1 : job.Limit == 100 ? 2 : 4;
        if (job.Limit > 0) customLimit.Value = job.Limit; mode.SelectedIndex = (int)job.Mode; premiere.Checked = job.Premiere; premiere.Enabled = mode.SelectedIndex != 2; refreshing = false; RefreshSummary();
    }
    void SaveCollection(CollectionJob job)
    {
        job.Filter = UiText.Filters[Math.Max(0,filter.SelectedIndex)]; job.OwnFolder = ownFolder.Checked; job.BatchSize = (int)batch.Value;
        job.Limit = limit.SelectedIndex == 3 ? 0 : limit.SelectedIndex == 4 ? (int)customLimit.Value : Int32.Parse(limit.Text);
        job.Mode = (MediaMode)mode.SelectedIndex; job.Premiere = premiere.Checked && mode.SelectedIndex != 2; job.Destination = manager.State.Destination; manager.Save();
    }
    async Task Discover()
    {
        var job = CurrentJob(); if (job == null) return; SaveCollection(job);
        string extent = job.Limit == 0 ? "TODAS as entradas acessíveis, sem limite numérico" : "até " + job.Limit + " entradas";
        if (MessageBox.Show(this, "Perfis e coleções podem gerar muitas solicitações. Isso pode causar limites de acesso ou restrições na conta, especialmente se usar cookies.\n\nO yt-dlp tentará descobrir " + extent + ". O limite de entradas não é uma garantia sobre o número de requisições; o comportamento depende do extrator. Os downloads serão enfileirados separadamente em lotes manuais.\n\nRecomendação: comece com 10 entradas, sem cookies quando possível. A espera entre requisições não elimina o risco. Continuar?", "Descoberta de perfis e coleções", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        await Guard(delegate { return manager.Discover(job); });
    }
    void QueueBatch()
    {
        var job = CurrentJob(); if (job == null) return; SaveCollection(job);
        int count = manager.QueueBatch(job); Feedback(count>0?count+" item(ns) adicionado(s). Use Iniciar downloads na seção Sua fila.":"Nenhum novo item elegível. Revise o filtro e os resultados da busca.",count==0); RefreshQueue();
    }
    void ReleaseHolds()
    {
        var timed = manager.State.Holds.Where(h => h.Until.HasValue && h.Until.Value > DateTime.UtcNow).ToList();
        if (timed.Count > 0) { MessageBox.Show(this,"A fonte pediu uma espera até "+timed.Max(h=>h.Until.Value).ToLocalTime()+". Aguarde o prazo antes de tentar novamente.","Fonte em pausa",MessageBoxButtons.OK,MessageBoxIcon.Information); return; }
        if (manager.State.Holds.Count == 0) { Log("No source holds."); return; }
        if (MessageBox.Show(this,"Você já resolveu a restrição de acesso ou aguardou o limite do serviço? Liberar uma fonte não resolve a causa e repetir tentativas pode agravar a restrição.\n\nLiberar as fontes em pausa?", "Revisar fontes em pausa",MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        { manager.ReleaseHolds(); Feedback("Fontes liberadas. Selecione os itens bloqueados, use Tentar novamente e inicie a fila.",false); }
    }
    bool ConfirmQueueAction(string action, string explanation)
    {
        int count=TargetItems().Count(q=>action!="Cancelar"||new[]{"Pending","Paused","Downloading","Converting"}.Contains(q.Status));if(count==0)return false;
        return MessageBox.Show(this, explanation + "\n\n" + action + " " + count + " item(ns)?", action + " tarefas", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }
    void Selected(Action<DownloadItem> action)
    {
        var items = TargetItems();
        foreach (var item in items) action(item); RefreshQueue();
    }
    bool InScope(DownloadItem item,string scope)
    {
        switch(scope){case "active":return item.Status=="Downloading"||item.Status=="Converting";case "pending":return item.Status=="Pending";case "done":return item.Status=="Completed";case "failed":return item.Status=="Failed"||item.Status=="Blocked";case "stopped":return item.Status=="Paused"||item.Status=="Cancelled";default:return true;}
    }
    void OpenOutput(bool folder)
    {
        var targets=TargetItems();if(targets.Count!=1)return;var item=targets[0];
        if(folder){string directory=File.Exists(item.OutputFile)?Path.GetDirectoryName(item.OutputFile):item.Destination;System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory){UseShellExecute=true});return;}
        if(item.Status!="Completed"||!File.Exists(item.OutputFile))return;
        if(!new[]{".mp4",".mkv",".webm",".mov",".mp3",".m4a",".aac",".wav",".flac",".ogg",".opus",".avi",".mpeg",".mpg",".m4v",".ts"}.Contains(Path.GetExtension(item.OutputFile).ToLowerInvariant())){Feedback("Este tipo de arquivo não será aberto pelo aplicativo. Use Abrir pasta para conferir.",true);return;}
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.OutputFile){UseShellExecute=true});
    }
    void PaintQueueProgress(object sender,DataGridViewCellPaintingEventArgs e)
    {
        if(e.RowIndex<0||e.ColumnIndex!=5)return;var item=queue.Rows[e.RowIndex].Tag as DownloadItem;if(item==null||!item.Percent.HasValue)return;
        e.PaintBackground(e.ClipBounds,true);var track=new Rectangle(e.CellBounds.X+7,e.CellBounds.Y+8,Math.Max(1,e.CellBounds.Width-14),e.CellBounds.Height-16);
        using(var back=new SolidBrush(Secondary))e.Graphics.FillRectangle(back,track);
        var fill=track;fill.Width=(int)(track.Width*Math.Max(0,Math.Min(100,item.Percent.Value))/100);
        if(fill.Width>0)using(var brush=new SolidBrush(item.Status=="Completed"?Color.FromArgb(42,111,86):Color.FromArgb(151,39,128)))e.Graphics.FillRectangle(brush,fill);
        TextRenderer.DrawText(e.Graphics,item.Percent.Value.ToString("0.0")+"%",queue.Font,track,Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Handled=true;
    }
    void RefreshQueue()
    {
        if (closing || resizing || queue == null) return;
        var selected = new HashSet<string>(queue.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Tag != null).Select(r => ((DownloadItem)r.Tag).Id));
        int scroll = queue.FirstDisplayedScrollingRowIndex;
        // Update cells in place: do not discard row selection or scrolling during progress updates.
        checkedItems.RemoveWhere(id=>!manager.State.Queue.Any(q=>q.Id==id));refreshingChecks=true;
        var visibleItems=manager.State.Queue.Where(q=>InScope(q,queueScope)).ToList();
        string[] scopes={"all","active","pending","done","failed","stopped"};string[] labels={"Todos","Em andamento","Aguardando","Concluídos","Com erro","Pausados/cancelados"};
        for(int i=0;i<scopes.Length;i++){scopeButtons[scopes[i]].Text=labels[i]+" ("+manager.State.Queue.Count(q=>InScope(q,scopes[i]))+")";scopeButtons[scopes[i]].BackColor=queueScope==scopes[i]?Color.FromArgb(83,43,88):Secondary;}
        bool rebuild = queue.Rows.Count != visibleItems.Count;
        if (!rebuild) for (int i=0;i<queue.Rows.Count;i++) if (queue.Rows[i].Tag != visibleItems[i]) { rebuild=true; break; }
        if (rebuild) { queue.Rows.Clear(); foreach (var item in visibleItems) { int row = queue.Rows.Add(); queue.Rows[row].Tag = item; } }
        foreach (DataGridViewRow row in queue.Rows)
        {
            var item = (DownloadItem)row.Tag;
            object[] values={checkedItems.Contains(item.Id),String.IsNullOrEmpty(item.Title)?String.IsNullOrEmpty(item.MediaId)?"Sem título":item.MediaId:item.Title, item.Extractor ?? Safety.Source(item.Url), UiText.Mode(item.Mode,item.Premiere), (item.Status=="Completed"&&!File.Exists(item.OutputFile)?"Concluído · arquivo ausente":UiText.Status(item.Status)), item.Percent.HasValue ? item.Percent.Value.ToString("0.0") + "%" : item.Status=="Downloading"?"Sem total":item.Status=="Converting"?"Processando":"—", UiText.Speed(item.Speed), item.Error};
            for(int column=0;column<values.Length;column++)if(!Object.Equals(row.Cells[column].Value,values[column]))row.Cells[column].Value=values[column];
            Color background=item.Status=="Downloading"||item.Status=="Converting"?Color.FromArgb(40,32,49):Surface;
            if(row.DefaultCellStyle.BackColor!=background)row.DefaultCellStyle.BackColor=background;
            Color statusColor=item.Status=="Downloading"||item.Status=="Converting"?Color.FromArgb(244,143,224):item.Status=="Completed"?Color.FromArgb(115,217,173):item.Status=="Failed"||item.Status=="Blocked"?Color.FromArgb(249,180,135):Ink;
            if(row.Cells[4].Style.ForeColor!=statusColor)row.Cells[4].Style.ForeColor=statusColor;
            if (rebuild) row.Selected = selected.Contains(item.Id);
        }
        refreshingChecks=false;
        if (rebuild && scroll >= 0 && scroll < queue.Rows.Count) queue.FirstDisplayedScrollingRowIndex = scroll;
        RefreshSummary();
    }
    void RefreshSummary()
    {
        if(resizing||closing||start==null||queue==null||queueSummary==null)return;
        bool canRun=manager.State.Queue.Any(q=>(q.Status=="Pending"||q.Status=="Paused")&&!manager.Held(q.Url)&&!manager.Held(q.OriginalUrl));
        start.Enabled=canRun&&!manager.Running&&!manager.Busy&&!manager.Updating;
        start.Text=manager.Running?"Baixando…":manager.State.Queue.Any(q=>q.Status=="Paused")?"Retomar fila":"Iniciar downloads";
        pause.Enabled=manager.Running;pause.BackColor=manager.Running?Accent:Secondary;engine.Enabled=!manager.Running&&!manager.Busy&&!manager.Discovering&&!manager.Updating&&manager.Analyzing==0;
        queueSummary.Text=manager.State.Queue.Count+" na fila  ·  "+manager.State.Queue.Count(q=>q.Status=="Completed")+" concluídos"+(checkedItems.Count>0?"  ·  "+checkedItems.Count+" marcados":"");
        bool empty=manager.State.Queue.Count==0;emptyQueue.Visible=queue.Rows.Count==0;
        emptyQueue.Text=empty?"Nenhum download na fila.\nCole um link acima e escolha Adicionar à fila.":"Nenhum item nesta categoria. Escolha outro filtro para ver os downloads.";
        start.Visible=!empty;pause.Visible=!empty;queueSummary.Visible=!empty;
        if(queueActions!=null)queueActions.Visible=!empty;
        var queueMinimum=new Size(0,empty?48:145);if(queue.MinimumSize!=queueMinimum)queue.MinimumSize=queueMinimum;queueFilters.Visible=!empty;
        if(contentLayout!=null&&contentLayout.RowStyles[5].Height!=(empty?130:330))contentLayout.RowStyles[5].Height=empty?130:330;
        string url=txtUrl.Text.Trim();bool unsupported=unsupportedUrl==url&&url!="";
        btnDownload.Enabled=Safety.ValidUrl(url)&&!unsupported&&analysis==null&&!manager.Updating;
        btnDownload.Text=unsupported?"Link não suportado":analysis!=null?"Analisando…":resourceMode.SelectedIndex==2||(resourceMode.SelectedIndex==0&&currentResource!=null&&currentResource.IsCollection)?"Abrir coleção":"Adicionar à fila";
        var selected=TargetItems();
        if(retrySelection!=null)
        {
            retrySelection.Enabled=selected.Any(q=>q.Status=="Failed"||q.Status=="Cancelled"||q.Status=="Paused"||q.Status=="Blocked"||(q.Status=="Completed"&&!File.Exists(q.OutputFile)));
            cancelSelection.Enabled=selected.Any(q=>new[]{"Pending","Paused","Downloading","Converting"}.Contains(q.Status));removeSelection.Enabled=selected.Count>0;detailsButton.Enabled=selected.Count==1;
            var single=selected.Count==1?selected[0]:null;
            if(openFileButton!=null){openFileButton.Enabled=single!=null&&single.Status=="Completed"&&File.Exists(single.OutputFile);openFolderButton.Enabled=single!=null&&Directory.Exists(single.Destination);}
            clearCompleted.Enabled=manager.State.Queue.Any(q=>q.Status=="Completed");releaseHolds.Visible=manager.State.Holds.Count>0;releaseHolds.Text="Revisar fontes em pausa ("+manager.State.Holds.Count+")";
        }
        var job=CurrentJob();
        if(discoverButton!=null)
        {
            discoverButton.Enabled=job!=null&&!manager.Discovering&&!manager.Updating&&!manager.Held(job.Url);stopDiscovery.Enabled=manager.Discovering;
            nextBatch.Enabled=job!=null&&job.Entries.Any(e=>e.Status=="Discovered"&&Metadata.Eligible(e,job.Filter))&&!manager.State.Queue.Any(q=>q.CollectionId==job.Id&&new[]{"Pending","Paused","Downloading","Converting"}.Contains(q.Status));
        }
        if(job!=null)
        {
            int available=job.Entries.Count(e=>e.Status=="Discovered"&&Metadata.Eligible(e,job.Filter));
            collectionSummary.Text=UiText.Collection(job)+"\nEncontrados: "+job.Entries.Count+"  ·  Novos elegíveis: "+available+"  ·  Concluídos: "+job.Entries.Count(e=>e.Status=="Completed")+"  ·  Falhas: "+job.Entries.Count(e=>e.Status=="Failed"||e.Status=="Blocked");
        }
    }
    void FitWindowToContent()
    {
        if(userSized||WindowState!=FormWindowState.Normal||contentLayout==null)return;
        PerformLayout();contentLayout.PerformLayout();
        Rectangle area=Screen.FromControl(this).WorkingArea;
        int chrome=Height-ClientSize.Height;
        int desired=Math.Min(Math.Max(560,contentLayout.Height+2),Math.Max(560,area.Height-chrome-42));
        ClientSize=new Size(Math.Min(ClientSize.Width,Math.Max(800,area.Width-70)),desired);
    }
    void Feedback(string text,bool error)
    {resource.Text=text;resource.ForeColor=error?Color.FromArgb(249,180,135):Color.FromArgb(153,219,186);Log(text);}
    async void HandleClosing(object sender, FormClosingEventArgs e)
    {
        if (closing) return;
        e.Cancel = true; if (shuttingDown) return; shuttingDown = true;
        // Exit this event before issuing a second Close, including when the queue is idle.
        await Task.Yield();
        try
        {
            debounce.Stop(); refresh.Stop(); if (analysis != null) analysis.Cancel(); manager.StopDiscovery(); manager.Pause();
            while (manager.Busy || manager.ActiveId != null || manager.Discovering || analysis != null || manager.Updating) await Task.Delay(100);
            manager.Save(); closing = true; Close();
        }
        catch (Exception ex) { shuttingDown = false; refresh.Start(); Log("Não foi possível salvar a fila: " + Safety.Redact(ex.Message)); }
    }
}
















