// Rebuild the multi-resolution Windows icon from the same vector geometry as assets/baixaki-icon.svg.
// Run through build-icon.ps1; no third-party libraries are required.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
class BuildIcon
{
    static GraphicsPath Round(float x,float y,float w,float h,float r)
    {
        var p=new GraphicsPath();float d=2*r;p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
    }
    static Bitmap Render(int size)
    {
        int scale=4;var large=new Bitmap(size*scale,size*scale,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(large))
        {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(size*scale/256f,size*scale/256f);g.Clear(Color.Transparent);
            using(var path=Round(12,12,232,232,58))using(var brush=new LinearGradientBrush(new PointF(12,12),new PointF(244,244),Color.FromArgb(41,46,64),Color.FromArgb(18,20,26)))g.FillPath(brush,path);
            using(var path=Round(13,13,230,230,57))using(var pen=new Pen(Color.FromArgb(102,98,106,131),2))g.DrawPath(pen,path);
            using(var path=Round(42,175,172,26,13))using(var brush=new SolidBrush(Color.FromArgb(51,214,48,180)))g.FillPath(brush,path);
            using(var brush=new LinearGradientBrush(new PointF(94,52),new PointF(162,145),Color.FromArgb(238,88,203),Color.FromArgb(158,36,139)))
            using(var pen=new Pen(brush,23))
            {pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawLine(pen,128,52,128,143);g.DrawLines(pen,new[]{new Point(94,111),new Point(128,145),new Point(162,111)});}
            using(var path=new GraphicsPath())using(var pen=new Pen(Color.FromArgb(235,237,245),15))
            {pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;path.AddLine(64,160,64,178);path.AddBezier(64,178,64,187,68,187,74,187);path.AddLine(74,187,182,187);path.AddBezier(182,187,192,187,192,183,192,178);path.AddLine(192,178,192,160);g.DrawPath(pen,path);}
            using(var pen=new Pen(Color.FromArgb(238,88,203),9)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLine(pen,90,212,166,212);}
        }
        var output=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(output)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(large,new Rectangle(0,0,size,size));}large.Dispose();return output;
    }
    static void Main(string[] args)
    {
        string root=args.Length>0?args[0]:Directory.GetCurrentDirectory();var sizes=new[]{16,20,24,32,40,48,64,96,128,256};var images=new List<byte[]>();
        foreach(int size in sizes)using(var bitmap=Render(size))using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());if(size==256)bitmap.Save(Path.Combine(root,"assets","baixaki-icon.png"),ImageFormat.Png);}
        using(var stream=File.Create(Path.Combine(root,"assets","baixaki.ico")))using(var writer=new BinaryWriter(stream))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}
            foreach(var data in images)writer.Write(data);
        }
        using(var icon=new Icon(Path.Combine(root,"assets","baixaki.ico"))){if(icon.Width<16)throw new Exception("Invalid icon");}
        Console.WriteLine("Created Baixaki.ico with 10 PNG frames (16–256 px) and assets/baixaki-icon.png");
    }
}

