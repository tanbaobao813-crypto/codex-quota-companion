using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
namespace CodexQuotaWidget
{
    internal static class QuotaFace
    {
        internal static readonly Color KeyColor=Color.FromArgb(24,24,24);
        private static string Value(WindowQuota window)
        {
            DateTime? reset=Data.ResetUtc(window);
            return window!=null&&window.Remaining.HasValue&&(!reset.HasValue||reset.Value>DateTime.UtcNow)?Math.Floor(window.Remaining.Value+.5).ToString("0",CultureInfo.InvariantCulture)+"%":"—";
        }
        private static string WindowName(WindowQuota window,string fallback)
        {
            if(window==null||!window.Minutes.HasValue)return fallback;
            int m=window.Minutes.Value;
            return m==10080?"本周":m%1440==0?(m/1440)+"天":m%60==0?(m/60)+"h":m+"分钟";
        }
        internal static void PaintText(Graphics g,Size size,Quota value,string error)
        {
            g.Clear(KeyColor);
            g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
            if(error!=null&&value==null)
            {
                using(var warning=new Font("Microsoft YaHei UI",12F*size.Height/14F,FontStyle.Regular,GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g,error=="auth"?"额度需重新登录":error=="protocol"||error=="startup"?"额度连接异常":"额度重试中",warning,new Rectangle(Point.Empty,size),Color.FromArgb(255,165,165),KeyColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
                return;
            }
            float scale=size.Height/14F;
            var flags=TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter;
            using(var font=new Font("Microsoft YaHei UI",12F*scale,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var numbers=new Font("Segoe UI",12F*scale,FontStyle.Bold,GraphicsUnit.Pixel))
            {
                WindowQuota first=value==null?null:value.Primary,second=value==null?null:value.Secondary;
                if(error!=null)
                {
                    string cached=WindowName(first,"短期")+" "+Value(first)+"  "+WindowName(second,"长期")+" "+Value(second)+" · 未更新";
                    TextRenderer.DrawText(g,cached,font,new Rectangle(Point.Empty,size),Color.FromArgb(247,212,145),KeyColor,flags|TextFormatFlags.HorizontalCenter);
                    return;
                }
                string a=WindowName(first,"短期")+"剩余 ",av=Value(first),b=WindowName(second,"长期")+"剩余 ",bv=Value(second);
                var max=new Size(10000,10000);
                int aw=TextRenderer.MeasureText(g,a,font,max,flags).Width,avw=TextRenderer.MeasureText(g,av,numbers,max,flags).Width;
                int bw=TextRenderer.MeasureText(g,b,font,max,flags).Width,bvw=TextRenderer.MeasureText(g,bv,numbers,max,flags).Width;
                int gap=(int)Math.Round(12*scale),x=(size.Width-aw-avw-bw-bvw-gap)/2;
                bool stale=value==null||DateTime.UtcNow-value.UpdatedUtc>TimeSpan.FromMinutes(10);
                Color color=error!=null?Color.FromArgb(255,165,165):stale?Color.FromArgb(247,212,145):Color.FromArgb(210,240,255);
                int y=-(int)Math.Round(scale),height=size.Height+2*(int)Math.Round(scale);
                TextRenderer.DrawText(g,a,font,new Rectangle(x,y,aw,height),Color.FromArgb(230,233,238),KeyColor,flags);x+=aw;
                TextRenderer.DrawText(g,av,numbers,new Rectangle(x,y,avw,height),color,KeyColor,flags);x+=avw+gap;
                TextRenderer.DrawText(g,b,font,new Rectangle(x,y,bw,height),Color.FromArgb(230,233,238),KeyColor,flags);x+=bw;
                TextRenderer.DrawText(g,bv,numbers,new Rectangle(x,y,bvw,height),color,KeyColor,flags);
            }
        }
        internal static void Preview(string path,Quota quota,int scale)
        {
            using(var image=new Bitmap(192*scale,14*scale,PixelFormat.Format32bppArgb))
            {
                using(var g=Graphics.FromImage(image))PaintText(g,image.Size,quota,null);
                image.MakeTransparent(KeyColor);image.Save(path,ImageFormat.Png);
            }
        }
    }
}
