using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace NetSpeck {
internal static class MeterRenderer {
    public static Bitmap Draw(Placement p,Reading r,Settings s,bool dark,SpeedPoint[] points,double now){
        var bitmap=new Bitmap(p.Width,p.Height,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(bitmap)){
            // Alpha 1 makes the whole indicator clickable without an opaque panel.
            g.Clear(Color.FromArgb(1,0,0,0));
            if(s.GraphEnabled)using(var layer=new Bitmap(p.Width,p.Height,PixelFormat.Format32bppPArgb)){
                using(var graph=Graphics.FromImage(layer))Graph(graph,p,s,points,now);
                double fadeScale=p.Dpi/96.0,left=2*fadeScale,right=p.Width-2*fadeScale;
                if(points!=null&&points.Length>1){double from=now-s.GraphSeconds,span=right-left;double first=Math.Max(from,points[0].Time),last=Math.Min(now,points[points.Length-1].Time);left+=((first-from)/s.GraphSeconds)*span;right=2*fadeScale+((last-from)/s.GraphSeconds)*span;}
                FadeEdges(layer,left,right);g.DrawImageUnscaled(layer,0,0);
            }
            g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
            float scale=p.Dpi/96f;
            float pixels=Math.Min(s.FontSize*96f/72f*scale,(p.Height-4*scale)/2.35f);
            using(var font=AppFonts.Indicator(s,pixels))
            using(var arrowFont=AppFonts.Arrow(s,pixels))
            using(var brush=new SolidBrush(dark?Color.FromArgb(245,245,245):Color.FromArgb(24,24,24)))
            using(var format=new StringFormat(StringFormat.GenericTypographic)){
                format.FormatFlags=StringFormatFlags.NoWrap;format.Alignment=StringAlignment.Far;format.LineAlignment=StringAlignment.Center;
                float row=p.Height/2f;
                string down=r.Connected?Rates.Format(r.Down,s.Bits):"—";
                string up=r.Connected?Rates.Format(r.Up,s.Bits):"—";
                float arrowWidth=Math.Max(16*scale,g.MeasureString("↓",arrowFont,10000,format).Width+2*scale);
                using(var arrow=new StringFormat(format)){arrow.Alignment=StringAlignment.Near;
                    DrawArrow(g,"↓",arrowFont,brush,new RectangleF(3*scale,0,arrowWidth,row),arrow,scale);
                    DrawArrow(g,"↑",arrowFont,brush,new RectangleF(3*scale,row,arrowWidth,row),arrow,scale);
                }
                float valueLeft=arrowWidth+6*scale;
                g.DrawString(down,font,brush,new RectangleF(valueLeft,0,Math.Max(1,p.Width-valueLeft-5*scale),row),format);
                g.DrawString(up,font,brush,new RectangleF(valueLeft,row,Math.Max(1,p.Width-valueLeft-5*scale),row),format);
            }
            if(s.ResizeEnabled)using(var grip=new Pen(Color.FromArgb(110,dark?Color.LightGray:Color.DimGray),Math.Max(1,scale)))g.DrawLine(grip,scale,12*scale,scale,p.Height-12*scale);
        }
        return bitmap;
    }
    internal static void DrawArrow(Graphics g,string text,Font font,Brush brush,RectangleF rect,StringFormat format,float scale){
        using(var path=new GraphicsPath())using(var stroke=new Pen(brush,0.25f*scale)){
            path.AddString(text,font.FontFamily,(int)font.Style,font.Size,rect,format);
            var state=g.Save();try{g.SmoothingMode=SmoothingMode.AntiAlias;stroke.LineJoin=LineJoin.Round;g.FillPath(brush,path);g.DrawPath(stroke,path);}finally{g.Restore(state);}
        }
    }
    internal static void FadeEdges(Bitmap layer,double left,double right){
        var rect=new Rectangle(0,0,layer.Width,layer.Height);
        var data=layer.LockBits(rect,ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
        try{
            int stride=data.Stride;byte[] pixels=new byte[stride*layer.Height];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
            for(int x=0;x<layer.Width;x++){
                double opacity=GraphMath.EdgeOpacity((x-left)/Math.Max(1,right-left));if(opacity>=1)continue;
                for(int y=0;y<layer.Height;y++){int at=y*stride+x*4;for(int c=0;c<4;c++)pixels[at+c]=(byte)Math.Round(pixels[at+c]*opacity);}
            }
            // PArgb: RGB must be attenuated together with alpha, otherwise DWM
            // would show colored fringes instead of a true transparency fade.
            Marshal.Copy(pixels,0,data.Scan0,pixels.Length);
        }finally{layer.UnlockBits(data);}
    }
    static void Graph(Graphics g,Placement p,Settings s,SpeedPoint[] points,double now){
        if(points==null||points.Length<2)return;
        double from=now-s.GraphSeconds;
        int first=0;while(first<points.Length&&points[first].Time<from)first++;
        if(first>0)first--;if(points.Length-first<2)return;
        double maximum=GraphMath.Maximum(points,now,s.GraphSeconds,s.GraphRaw,s.GraphAutoScale,s.GraphMaximum);
        float scale=p.Dpi/96f,left=2*scale,right=p.Width-2*scale,top=2*scale,bottom=p.Height-2*scale;
        var state=g.Save();g.SetClip(new RectangleF(left,top,right-left,bottom-top));g.SmoothingMode=SmoothingMode.AntiAlias;
        try{
            for(int direction=0;direction<2;direction++){
                var line=new List<PointF>();
                for(int i=first;i<points.Length;i++){
                    var point=points[i];if(point.Time>now)break;
                    double value=direction==0?(s.GraphRaw?point.RawDown:point.Down):(s.GraphRaw?point.RawUp:point.Up);
                    line.Add(new PointF(left+(float)((point.Time-from)/s.GraphSeconds)*(right-left),bottom-(float)GraphMath.Fraction(value,maximum)*(bottom-top)));
                }
                if(line.Count<2)continue;
                if(!s.GraphRaw)line=SmoothLine(line);
                int rgb=direction==0?s.DownloadColor:s.UploadColor;
                int alpha=(int)Math.Round(s.GraphOpacity*255/100.0);
                if(s.GraphFill){
                    using(var path=new GraphicsPath()){
                        path.AddLines(line.ToArray());path.AddLine(line[line.Count-1],new PointF(line[line.Count-1].X,bottom));
                        path.AddLine(new PointF(line[line.Count-1].X,bottom),new PointF(line[0].X,bottom));path.CloseFigure();
                        using(var fill=new SolidBrush(ColorOf(rgb,Math.Max(1,alpha/3))))g.FillPath(fill,path);
                    }
                }
                using(var pen=new Pen(ColorOf(rgb,alpha),(float)s.GraphLineWidth*scale)){
                    pen.LineJoin=LineJoin.Round;g.DrawLines(pen,line.ToArray());
                }
            }
        }finally{g.Restore(state);}
    }
    internal static List<PointF> SmoothLine(List<PointF> source){
        int n=source.Count;var x=new double[n];var y=new double[n];
        for(int i=0;i<n;i++){x[i]=source[i].X;y[i]=source[i].Y;}
        var slopes=GraphMath.CurveSlopes(x,y);var line=new List<PointF>();line.Add(source[0]);
        for(int i=0;i<n-1;i++){
            int steps=Math.Max(1,Math.Min(512,(int)Math.Ceiling((x[i+1]-x[i])*2)));
            for(int j=1;j<=steps;j++){double at=x[i]+(x[i+1]-x[i])*j/steps;line.Add(new PointF((float)at,(float)GraphMath.CurveValue(x[i],x[i+1],y[i],y[i+1],slopes[i],slopes[i+1],at)));}
        }
        return line;
    }
    public static Color ColorOf(int rgb,int alpha){return Color.FromArgb(alpha,(rgb>>16)&255,(rgb>>8)&255,rgb&255);}
}
}
