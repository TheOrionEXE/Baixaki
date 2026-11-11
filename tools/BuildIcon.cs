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
            // Baixaki 1.2: violet tile (the interface accent), bold white arrow, solid base.
            using(var path=Round(12,12,232,232,64))using(var brush=new LinearGradientBrush(new PointF(12,12),new PointF(244,244),Color.FromArgb(190,146,255),Color.FromArgb(104,44,226)))g.FillPath(brush,path);
            using(var path=Round(13,13,230,230,63))using(var pen=new Pen(Color.FromArgb(56,255,255,255),2))g.DrawPath(pen,path);
            using(var pen=new Pen(Color.White,27))
            {pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;g.DrawLine(pen,128,56,128,142);g.DrawLines(pen,new[]{new Point(88,106),new Point(128,146),new Point(168,106)});}
            using(var path=Round(68,178,120,26,13))using(var brush=new SolidBrush(Color.FromArgb(235,255,255,255)))g.FillPath(brush,path);
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

