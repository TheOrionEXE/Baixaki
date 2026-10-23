using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
public class HelpTopic
{
    public string title, warning, body;
    public override string ToString() { return title; }
}
public class HelpCenterForm : Form
{
    public static List<HelpTopic> Topics()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Baixaki.HelpTopics.json"))
        {
            if (stream == null) throw new IOException("Help content missing from this build.");
            using (var reader = new StreamReader(stream)) return StateStore.Serializer().Deserialize<List<HelpTopic>>(reader.ReadToEnd());
        }
    }
    public HelpCenterForm()
    {
        Text = "Baixaki — Central de ajuda"; StartPosition = FormStartPosition.CenterParent; AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI",11); BackColor = Color.FromArgb(18,20,26); ForeColor = Color.FromArgb(235,237,245);
        ClientSize = new Size(980,700); MinimumSize = new Size(760,550);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill,ColumnCount = 2,RowCount = 1,Padding = new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,255)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); Controls.Add(layout);
        var list = new ListBox { Dock = DockStyle.Fill,BackColor = Color.FromArgb(31,35,46),ForeColor = ForeColor,BorderStyle = BorderStyle.None,IntegralHeight = false,HorizontalScrollbar = true };
        foreach(var topic in Topics()) list.Items.Add(topic); layout.Controls.Add(list,0,0);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill,ColumnCount = 1,RowCount = 3,Padding = new Padding(12,0,0,0) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));content.RowStyles.Add(new RowStyle(SizeType.AutoSize));content.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        layout.Controls.Add(content,1,0);
        var title = new Label { AutoSize = true,Font = new Font(Font,FontStyle.Bold),Margin = new Padding(0,0,0,12) };
        var warning = new Label { AutoSize = true,BackColor = Color.FromArgb(72,49,31),ForeColor = Color.FromArgb(255,216,153),Padding = new Padding(12),Margin = new Padding(0,0,0,12) };
        var body = new TextBox { Multiline = true, WordWrap = true, Dock = DockStyle.Fill,ReadOnly = true,BorderStyle = BorderStyle.None,BackColor = Color.FromArgb(31,35,46),ForeColor = ForeColor,Font = Font,ScrollBars = ScrollBars.Vertical };
        content.Controls.Add(title,0,0);content.Controls.Add(warning,0,1);content.Controls.Add(body,0,2);
        Action resize = delegate { title.MaximumSize = new Size(Math.Max(200,content.ClientSize.Width-20),0);warning.MaximumSize = title.MaximumSize; };
        content.Resize += delegate { resize(); };
        list.SelectedIndexChanged += delegate
        {
            var topic = list.SelectedItem as HelpTopic; if(topic==null)return;
            title.Text = topic.title;warning.Text = "ATENÇÃO — " + topic.warning;body.Text = topic.body.Replace("\r\n","\n").Replace("\n",Environment.NewLine);
            body.SelectionStart = 0;body.ScrollToCaret();resize();
        };
        list.SelectedIndex = 1;
    }
}



