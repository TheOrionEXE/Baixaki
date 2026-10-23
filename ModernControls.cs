using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public static class ModernPaint
{
    public static Color Background(Control parent){while(parent!=null){if(parent.BackColor.A==255)return parent.BackColor;parent=parent.Parent;}return Color.FromArgb(18,20,26);}
    public static GraphicsPath Round(RectangleF rect, float radius)
    {
        float d=Math.Min(radius*2,Math.Min(rect.Width,rect.Height)); var p=new GraphicsPath();
        p.AddArc(rect.X,rect.Y,d,d,180,90);p.AddArc(rect.Right-d,rect.Y,d,d,270,90);
        p.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);p.AddArc(rect.X,rect.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
}
public class ModernButton : Button
{
    bool hover;
    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
        Padding=new Padding(14,0,14,0);Cursor=Cursors.Hand;UseVisualStyleBackColor=false;
    }
    public override Size GetPreferredSize(Size proposed) { var size=TextRenderer.MeasureText(Text,Font);return new Size(size.Width+32,Math.Max(42,size.Height+18)); }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        e.Graphics.Clear(ModernPaint.Background(Parent));
        Color face=!Enabled?Color.FromArgb(35,39,49):hover?ControlPaint.Light(BackColor,0.12f):BackColor;
        using(var path=ModernPaint.Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),8))
        using(var brush=new SolidBrush(face))using(var pen=new Pen(hover&&Enabled?Color.FromArgb(104,85,115):Color.FromArgb(57,62,78)))
        {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(117,123,142),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Width-10,Height-10),ForeColor,face);
    }
}
public class ModernCombo : ComboBox
{
    public ModernCombo(){DrawMode=DrawMode.OwnerDrawFixed;DropDownStyle=ComboBoxStyle.DropDownList;FlatStyle=FlatStyle.Flat;ItemHeight=30;}
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        Color face=(e.State&DrawItemState.Selected)!=0?Color.FromArgb(67,46,76):BackColor;
        using(var brush=new SolidBrush(face))e.Graphics.FillRectangle(brush,e.Bounds);
        string text=e.Index>=0?GetItemText(Items[e.Index]):Text;
        TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),Enabled?ForeColor:Color.Gray,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}
public class UiCard : TableLayoutPanel
{
    public UiCard()
    {
        DoubleBuffered=true;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Dock=DockStyle.Top;ColumnCount=1;Padding=new Padding(12);Margin=new Padding(0,4,0,6);
        BackColor=Color.FromArgb(31,35,46);ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        int width=proposedSize.Width>0?proposedSize.Width:Width;
        width=Math.Max(200,width);int available=Math.Max(100,width-Padding.Horizontal);int height=Padding.Vertical;
        foreach(Control control in Controls)
        {
            if(!control.Visible)continue;
            var size=control.GetPreferredSize(new Size(Math.Max(80,available-control.Margin.Horizontal),0));
            height+=size.Height+control.Margin.Vertical;
        }
        return new Size(width,height);
    }
    public void Add(Control control){RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(control,0,RowCount++);}
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(ModernPaint.Background(Parent));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=ModernPaint.Round(new RectangleF(0.5f,0.5f,Width-1,Height-1),11))using(var brush=new SolidBrush(BackColor))using(var pen=new Pen(Color.FromArgb(45,51,65)))
        {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path);}
    }
}




public class BufferedPanel : Panel
{
    public BufferedPanel(){DoubleBuffered=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
}
public class BufferedTable : TableLayoutPanel
{
    public BufferedTable(){DoubleBuffered=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
}
public class BufferedFlow : FlowLayoutPanel
{
    public BufferedFlow(){DoubleBuffered=true;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
}
public class BufferedGrid : DataGridView
{
    public BufferedGrid(){DoubleBuffered=true;}
}

public class HeaderLayout : BufferedTable
{
    public override Size GetPreferredSize(Size proposed)
    {
        if(Controls.Count<2)return base.GetPreferredSize(proposed);
        int width=Math.Max(200,proposed.Width>0?proposed.Width:Width);
        Control left=Controls[0],right=Controls[1];Size credit=right.GetPreferredSize(Size.Empty);
        int available=Math.Max(100,width-credit.Width-right.Margin.Horizontal-left.Margin.Horizontal-Padding.Horizontal);
        Size title=left.GetPreferredSize(new Size(available,0));
        return new Size(width,Math.Max(title.Height+left.Margin.Vertical,credit.Height+right.Margin.Vertical)+Padding.Vertical);
    }
}
