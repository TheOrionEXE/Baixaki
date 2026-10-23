using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
partial class BaixakiForm {
    void CheckForUpdate(bool manual = false)
    {
        if (UpdateRepo.StartsWith("OWNER")) return;
        if (manual) Feedback("Verificando atualizações do Baixaki…",false);
        Thread t = new Thread(delegate()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                string json;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Baixaki-Updater";
                    wc.Encoding = Encoding.UTF8;
                    json = wc.DownloadString("https://api.github.com/repos/" + UpdateRepo + "/releases/latest");
                }
                Match mt = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                Match mu = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+BaixakiSetup\\.exe)\"");
                if (!mt.Success || !mu.Success) { if(manual)BeginInvoke(new Action(delegate{Feedback("A release mais recente não contém um instalador compatível.",true);}));return; }
                string tag = mt.Groups[1].Value;
                string dl = mu.Groups[1].Value;
                Version latest = new Version(tag.TrimStart('v', 'V'));
                Version current = new Version(AppVersion);
                if (latest <= current) { if(manual)BeginInvoke(new Action(delegate{Feedback("Você já usa a versão mais recente disponível no GitHub.",false);}));return; }
                BeginInvoke(new Action(delegate { AskUpdate(tag, dl); }));
            }
            catch (Exception ex) { if(manual)try{BeginInvoke(new Action(delegate{Feedback("Não foi possível verificar atualizações. Consulte a atividade técnica.",true);Log(Safety.Redact(ex.Message));}));}catch{} }
        });
        t.IsBackground = true;
        t.Start();
    }

    void AskUpdate(string tag, string url)
    {
        if (manager.Running || manager.Busy || manager.Discovering || manager.Updating || manager.Analyzing > 0) { Feedback("Pause a fila e pare a busca antes de instalar uma atualização.",true); return; }
        DialogResult r = MessageBox.Show(this,
            "Baixaki " + tag + " está disponível (você usa v" + AppVersion + ").\n\nAtualizar agora? O instalador da release será baixado e executado, substituindo os arquivos do aplicativo. A fila será salva e reabrirá pausada.",
            "Atualização do Baixaki", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
        if (r != DialogResult.Yes) return;

        manager.Updating = true; RefreshSummary();
        btnDownload.Enabled = false;
        btnDownload.Text = "Atualizando…";
        Log("--- Baixando atualização " + tag + "...");
        Thread t = new Thread(delegate()
        {
            try
            {
                string setup = Path.Combine(Path.GetTempPath(), "BaixakiSetup-update.exe");
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Baixaki-Updater";
                    wc.DownloadFile(url, setup);
                }
                Log("Abrindo o instalador. O aplicativo será reiniciado…");
                Process.Start(setup, "/SILENT /CLOSEAPPLICATIONS");
                BeginInvoke(new Action(delegate { manager.Updating = false; Application.Exit(); }));
            }
            catch (Exception ex)
            {
                Log("FAIL: a atualização falhou: " + Safety.Redact(ex.Message));
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        manager.Updating = false; RefreshSummary();
                        btnDownload.Enabled = true;
                        btnDownload.Text = "Adicionar à fila";
                    }));
                }
                catch { }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

}





