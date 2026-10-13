using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;
namespace CodexQuotaWidget
{
    internal static class WhaleArt
    {
        internal const int BubbleWidth=202,BubbleHeight=132;
        internal static GraphicsPath SpeechShape()
        {
            var p=new GraphicsPath();
            p.AddArc(1,1,36,36,180,90);p.AddLine(19,1,163,1);p.AddArc(145,1,36,36,270,90);
            p.AddLine(181,19,181,94);p.AddBezier(181,94,188,96,195,103,201,107);
            p.AddBezier(201,107,194,109,187,111,181,113);p.AddArc(145,95,36,36,0,90);
            p.AddLine(163,131,19,131);p.AddArc(1,95,36,36,90,90);p.AddLine(1,113,1,19);p.CloseFigure();
            return p;
        }
        internal static bool BubbleHit(Point point,Size size)
        {using(var shape=SpeechShape())return shape.IsVisible(point.X*BubbleWidth/(float)size.Width,point.Y*BubbleHeight/(float)size.Height);}
        private static readonly Bitmap Character=LoadCharacter();
        private static Bitmap CharacterFrame;
        private static Size CharacterFrameSize;
        private static Bitmap LoadCharacter()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("QuotaWidget.Character.png"))
            {
                if(stream==null)return null;
                using(var image=Image.FromStream(stream))return new Bitmap(image);
            }
        }
        internal static bool HasCharacter {get{return Character!=null;}}
        internal static double CharacterAspect {get{return Character==null?1:Character.Height/(double)Character.Width;}}
        internal static bool AvatarHit(Point point,Size size)
        {
            if(Character==null)return new Rectangle(Point.Empty,size).Contains(point);
            float scale=Math.Min((size.Width-6F)/Character.Width,(size.Height-6F)/Character.Height);
            if(scale<=0)return false;
            int x=(int)((point.X-(size.Width-Character.Width*scale)/2)/scale),y=(int)((point.Y-(size.Height-Character.Height*scale)/2)/scale);
            return x>=0&&y>=0&&x<Character.Width&&y<Character.Height&&Character.GetPixel(x,y).A>16;
        }
        internal static readonly Color Ink=Color.FromArgb(18,29,43);
        internal static GraphicsPath Round(float x,float y,float w,float h,float r)
        {
            var p=new GraphicsPath();float d=r*2;
            p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);
            p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;
        }
        internal static void Whale(Graphics g,Size size,float phase)
        {
            if(Character!=null)
            {
                var imageState=g.Save();g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                float padding=3F,portraitBob=(float)Math.Sin(phase)*1.3F;
                float scale=Math.Min((size.Width-2*padding)/Character.Width,(size.Height-2*padding)/Character.Height);
                float w=Character.Width*scale,h=Character.Height*scale;
                var frameSize=new Size(Math.Max(1,(int)Math.Round(w)),Math.Max(1,(int)Math.Round(h)));
                if(CharacterFrame==null||CharacterFrameSize!=frameSize)
                {
                    if(CharacterFrame!=null)CharacterFrame.Dispose();
                    CharacterFrameSize=frameSize;CharacterFrame=new Bitmap(frameSize.Width,frameSize.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using(var frame=Graphics.FromImage(CharacterFrame))
                    {frame.Clear(Color.Transparent);frame.InterpolationMode=InterpolationMode.HighQualityBicubic;frame.PixelOffsetMode=PixelOffsetMode.HighQuality;frame.DrawImage(Character,new Rectangle(Point.Empty,frameSize));}
                }
                g.DrawImageUnscaled(CharacterFrame,(int)Math.Round((size.Width-w)/2),(int)Math.Round((size.Height-h)/2+portraitBob));
                g.Restore(imageState);return;
            }
            var saved=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size.Width/64F,size.Height/64F);
            float bob=(float)Math.Sin(phase)*1.6F;g.TranslateTransform(0,bob);
            using(var glow=new SolidBrush(Color.FromArgb(35,127,214)))g.FillEllipse(glow,13,55,41,4);
            using(var tail=new GraphicsPath())
            {
                tail.AddBezier(46,42,57,44,59,32,56,28);tail.AddBezier(56,28,54,24,55,20,61,18);
                tail.AddBezier(61,18,65,28,64,35,60,40);tail.AddBezier(60,40,58,50,48,51,46,42);tail.CloseFigure();
                using(var b=new LinearGradientBrush(new Rectangle(45,18,19,34),Color.FromArgb(81,216,224),Color.FromArgb(18,135,199),90))g.FillPath(b,tail);
            }
            using(var body=new GraphicsPath())
            {
                body.AddBezier(9,34,12,22,30,17,44,26);body.AddBezier(44,26,56,33,54,48,41,53);
                body.AddBezier(41,53,26,59,8,52,9,34);body.CloseFigure();
                using(var b=new LinearGradientBrush(new Rectangle(9,21,46,35),Color.FromArgb(107,241,236),Color.FromArgb(17,149,212),75))g.FillPath(b,body);
                var clip=g.Save();g.SetClip(body);
                using(var belly=new SolidBrush(Color.FromArgb(230,252,250)))g.FillEllipse(belly,12,43,37,19);
                g.Restore(clip);
                using(var edge=new Pen(Color.FromArgb(158,253,245),1F))g.DrawPath(edge,body);
            }
            using(var fin=new SolidBrush(Color.FromArgb(18,128,189)))g.FillEllipse(fin,36,41,12,6);
            using(var eye=new SolidBrush(Ink)){g.FillEllipse(eye,18,34,5,6);g.FillEllipse(eye,31,34,5,6);}
            using(var shine=new SolidBrush(Color.White)){g.FillEllipse(shine,19,34.4F,1.5F,1.5F);g.FillEllipse(shine,32,34.4F,1.5F,1.5F);}
            using(var blush=new SolidBrush(Color.FromArgb(255,163,179))){g.FillEllipse(blush,13,41,6,3);g.FillEllipse(blush,34,41,6,3);}
            using(var smile=new Pen(Ink,1.2F)){g.DrawArc(smile,23,39,7,6,10,160);}
            using(var water=new Pen(Color.FromArgb(127,246,246),2.5F))
            {g.DrawBezier(water,28,20,28,12,22,11,21,13);g.DrawBezier(water,28,20,29,10,35,9,36,13);}
            using(var drops=new SolidBrush(Color.FromArgb(175,252,248))){g.FillEllipse(drops,16,10,3,4);g.FillEllipse(drops,36,6,3,4);}
            g.Restore(saved);
        }
        internal static string Name(WindowQuota w,string fallback)
        {if(w==null||!w.Minutes.HasValue)return fallback;int m=w.Minutes.Value;return m==10080?"本周":m%60==0?(m/60)+" 小时":m+" 分钟";}
        internal static string Percent(WindowQuota w)
        {DateTime? reset=Data.ResetUtc(w);return w!=null&&w.Remaining.HasValue&&(!reset.HasValue||reset>DateTime.UtcNow)?Math.Floor(w.Remaining.Value+.5).ToString("0",CultureInfo.InvariantCulture)+"%":"—";}
        internal static string ResetText(WindowQuota w)
        {
            DateTime? reset=Data.ResetUtc(w);if(!reset.HasValue)return "重置时间未提供";
            return "重置 "+reset.Value.ToLocalTime().ToString("MM/dd HH:mm")+(reset.Value<=DateTime.UtcNow?" · 已到":"");
        }
        private static void Text(Graphics g,string text,float x,float y,float w,float h,float size,Color color,bool bold)
        {
            using(var transform=g.Transform)
            {
                var points=new[]{new PointF(x,y),new PointF(x+w,y+h)};transform.TransformPoints(points);
                float scale=transform.Elements[0];g.ResetTransform();
                using(var font=new Font("Microsoft YaHei UI",size*scale,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g,text,font,Rectangle.FromLTRB((int)points[0].X,(int)points[0].Y,(int)points[1].X,(int)points[1].Y),color,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter);
                g.Transform=transform;
            }
        }
        internal static void Bubble(Graphics g,Size size,Quota q,string error)
        {
            var saved=g.Save();g.ScaleTransform(size.Width/(float)BubbleWidth,size.Height/(float)BubbleHeight);g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var p=SpeechShape())
            {
                using(var bg=new SolidBrush(Color.FromArgb(19,31,45)))g.FillPath(bg,p);
                using(var edge=new Pen(Color.FromArgb(59,105,119),1))g.DrawPath(edge,p);
            }
            using(var dot=new SolidBrush(error==null?Color.FromArgb(105,236,201):Color.FromArgb(251,190,108)))g.FillEllipse(dot,12,14,4,4);
            Text(g,"CODEX  ·  剩余额度",22,7,136,19,10.5F,Color.FromArgb(180,217,223),true);
            Text(g,"×",163,6,13,20,16,Color.FromArgb(125,155,170),false);
            WindowQuota a=q==null?null:q.Primary,bq=q==null?null:q.Secondary;
            Row(g,a,"5 小时",27,error);Row(g,bq,"本周",67,error);
            string status=q==null?"正在读取…":error!=null?"暂未更新":"更新 "+q.UpdatedUtc.ToLocalTime().ToString("HH:mm:ss");
            Text(g,status,12,110,128,18,10,error==null?Color.FromArgb(128,159,174):Color.FromArgb(251,190,108),false);
            Text(g,"刷新",147,110,28,18,10,Color.FromArgb(129,243,231),true);
            g.Restore(saved);
        }
        private static void Row(Graphics g,WindowQuota w,string fallback,int y,string error)
        {
            Color color=error==null?Color.FromArgb(128,243,231):Color.FromArgb(251,190,108);
            Text(g,Name(w,fallback),12,y,111,19,12,Color.FromArgb(225,236,243),false);
            Text(g,Percent(w),132,y-1,44,21,16,color,true);
            Text(g,ResetText(w),12,y+19,163,16,10.5F,Color.FromArgb(159,187,201),false);
            using(var track=new SolidBrush(Color.FromArgb(33,57,71)))g.FillRectangle(track,12,y+35,158,2);
            if(w!=null&&w.Remaining.HasValue&&Percent(w)!="—")using(var fill=new SolidBrush(color))g.FillRectangle(fill,12,y+35,(float)(158*w.Remaining.Value/100),2);
        }
        internal static void Preview(string path)
        {
            var epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
            var q=new Quota{UpdatedUtc=DateTime.UtcNow,Primary=new WindowQuota{Remaining=89,Minutes=300,ResetsAt=(long)(DateTime.UtcNow.AddHours(2)-epoch).TotalSeconds},Secondary=new WindowQuota{Remaining=77,Minutes=10080,ResetsAt=(long)(DateTime.UtcNow.AddDays(5)-epoch).TotalSeconds}};
            using(var bmp=new Bitmap(640,480))using(var g=Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(15,22,31));
                Text(g,"Codex 额度伙伴 · 外观预览",24,18,580,34,18,Color.FromArgb(215,240,245),true);
                Text(g,"点击人物展开气泡，拖动人物调整位置",24,57,580,25,12,Color.FromArgb(127,159,174),false);
                var avatar=new Rectangle(490,288,100,Math.Min(148,(int)(100*CharacterAspect)));
                var popup=WhaleLayout.Popup(new Rectangle(0,0,640,480),avatar,96);
                var s=g.Save();g.TranslateTransform(popup.X,popup.Y);Bubble(g,popup.Size,q,null);g.Restore(s);
                s=g.Save();g.TranslateTransform(avatar.X,avatar.Y);Whale(g,avatar.Size,0);g.Restore(s);
                Text(g,"示例数据；运行时显示实际剩余额度",24,435,440,25,12,Color.FromArgb(127,159,174),false);
                bmp.Save(path,System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
    internal static class WhaleLayout
    {
        internal static Rectangle Icon(Rectangle host,int dpi)
        {
            double s=Math.Max(1,dpi)/96.0;
            double responsive=Math.Max(.65,Math.Min(1.1,Math.Min(host.Width/s/1400.0,host.Height/s/850.0)));
            int side=(int)Math.Round((WhaleArt.HasCharacter?88:56)*s*responsive);
            int height=Math.Min((int)Math.Round(side*WhaleArt.CharacterAspect),(int)Math.Round(112*s));
            return Place(host,new Size(side,height),dpi,null);
        }
        internal static Rectangle AnchorRange(Rectangle host,Size size,int dpi)
        {
            double s=Math.Max(1,dpi)/96.0;var icon=new Rectangle(Point.Empty,size);
            var group=Rectangle.Union(icon,Popup(host,icon,dpi));
            int left=host.Left+(int)Math.Round(6*s)-group.Left,right=host.Right-(int)Math.Round(6*s)-group.Right;
            int top=host.Top+(int)Math.Round(54*s)-group.Top,bottom=host.Bottom-(int)Math.Round(6*s)-group.Bottom;
            return Rectangle.FromLTRB(left,top,Math.Max(left,right),Math.Max(top,bottom));
        }
        internal static Rectangle Place(Rectangle host,Size size,int dpi,AvatarPlacement placement)
        {
            var range=AnchorRange(host,size,dpi);double x=placement==null?1:placement.X,y=placement==null?1:placement.Y;
            return new Rectangle(range.Left+(int)Math.Round(range.Width*x),range.Top+(int)Math.Round(range.Height*y),size.Width,size.Height);
        }
        internal static Rectangle Popup(Rectangle host,Rectangle icon,int dpi)
        {
            double s=Math.Max(1,dpi)/96.0;int w=(int)Math.Round(WhaleArt.BubbleWidth*s),h=(int)Math.Round(WhaleArt.BubbleHeight*s);
            // Leave a four-pixel gap before the round body. The side tail reaches
            // just into the portrait silhouette and stays level with the face.
            int left=icon.Left-(int)Math.Round(185*s);
            int top=icon.Top+(int)Math.Round(icon.Height*.55)-(int)Math.Round(107*s);
            return new Rectangle(left,top,w,h);
        }
        internal static Rectangle ClampGroup(Rectangle icon,int dpi,Rectangle area)
        {
            var range=AnchorRange(area,icon.Size,dpi);
            icon.Location=new Point(Math.Max(range.Left,Math.Min(range.Right,icon.X)),Math.Max(range.Top,Math.Min(range.Bottom,icon.Y)));return icon;
        }
    }
    internal abstract class OwnedQuotaSurface : Form
    {
        internal readonly IntPtr Host;
        protected OwnedQuotaSurface(IntPtr host,string title)
        {
            Host=host;ClientWindows.MatchDpi(host);Text=title;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;
            AutoScaleMode=AutoScaleMode.None;BackColor=QuotaFace.KeyColor;TransparencyKey=QuotaFace.KeyColor;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ClientWindows.Style(Handle,-8,Host.ToInt64());}
        protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
        internal void Position(Rectangle r)
        {
            ClientWindows.SetWindowPos(Handle,IntPtr.Zero,r.X,r.Y,r.Width,r.Height,0x0004|0x0010);
            if(!Visible)Show();
            ClientWindows.Style(Handle,-8,Host.ToInt64());
        }
    }
    internal sealed class WhaleBubble : OwnedQuotaSurface
    {
        internal Quota Value;internal string Error;internal Action RefreshAction;
        internal WhaleBubble(IntPtr host):base(host,"Codex 额度气泡"){AccessibleName="Codex 剩余额度气泡";Opacity=.80;}
        protected override void OnPaint(PaintEventArgs e){WhaleArt.Bubble(e.Graphics,ClientSize,Value,Error);QuotaLiveState.Publish(Handle,Value,Error);AlphaTextWindow.SetValue(Handle,"CodexQuotaBubbleOpacity",new IntPtr(80));}
        protected override void OnMouseClick(MouseEventArgs e)
        {
            double x=e.X*WhaleArt.BubbleWidth/(double)ClientSize.Width,y=e.Y*WhaleArt.BubbleHeight/(double)ClientSize.Height;
            if(x>157&&x<183&&y<28)Hide();else if(x>140&&x<181&&y>106&&y<132&&RefreshAction!=null)RefreshAction();
            base.OnMouseClick(e);
        }
    }
    internal sealed class WhaleCompanion : OwnedQuotaSurface
    {
        private readonly WhaleBubble bubble;
        private readonly OutsideBubbleDismiss outside;
        private readonly System.Windows.Forms.Timer motion=new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer dismiss=new System.Windows.Forms.Timer();
        private Quota value;private string error;private float phase;private bool hover,openBubble;
        private readonly bool rememberPosition;
        private AvatarPlacement placement;
        private bool dragging,dragMoved,skipClick;
        private Point dragStart;
        private Rectangle dragStartBounds;
        internal double BubbleOpacity {get{return bubble.Opacity;}}
        private Rectangle HostArea()
        {
            ClientWindows.Rect r;ClientWindows.GetClientRect(Host,out r);var origin=new ClientWindows.PointNative();ClientWindows.ClientToScreen(Host,ref origin);
            return new Rectangle(origin.X,origin.Y,r.Right-r.Left,r.Bottom-r.Top);
        }
        internal bool PopupVisible {get{return bubble.Visible;}}
        internal IntPtr BubbleHandle {get{return bubble.Handle;}}
        internal Rectangle BubbleBounds {get{return bubble.Bounds;}}
        internal bool OutsideDismissActive {get{return outside.Active;}}
        internal bool HandleOutsideClick(Point point){return outside.DismissAt(point);}
        internal WhaleCompanion(IntPtr host,Action refresh,bool remember=true):base(host,"Codex 小鲸鱼")
        {
            rememberPosition=remember;if(remember)placement=AvatarPlacement.Load();
            AccessibleName="小鲸鱼，点击查看剩余额度";AccessibleRole=AccessibleRole.PushButton;Cursor=Cursors.Hand;
            bubble=new WhaleBubble(host){RefreshAction=refresh};
            outside=new OutsideBubbleDismiss(bubble,this);bubble.VisibleChanged+=BubbleVisibilityChanged;
            motion.Interval=120;motion.Tick+=delegate{phase+=.32F;Invalidate();};
            dismiss.Interval=20000;dismiss.Tick+=delegate{bubble.Hide();dismiss.Stop();if(!hover)motion.Stop();};
        }
        private void BubbleVisibilityChanged(object sender,EventArgs e)
        {
            if(bubble.Visible)outside.Start();
            else {outside.Stop();dismiss.Stop();if(!hover)motion.Stop();}
        }
        internal void UpdateQuota(Quota q,string failure)
        {value=q;error=failure;bubble.Value=q;bubble.Error=failure;Invalidate();bubble.Invalidate();}
        internal void Place()
        {
            if(!ClientWindows.IsWindow(Host)||!ClientWindows.IsWindowVisible(Host)||ClientWindows.IsIconic(Host))
            {if(dragging)EndDrag();Hide();bubble.Hide();motion.Stop();dismiss.Stop();return;}
            ClientWindows.MatchDpi(Host);ClientWindows.Rect r;ClientWindows.GetClientRect(Host,out r);
            var p=new ClientWindows.PointNative();ClientWindows.ClientToScreen(Host,ref p);
            var host=new Rectangle(p.X,p.Y,r.Right-r.Left,r.Bottom-r.Top);int dpi=ClientWindows.Dpi(Host);
            var icon=WhaleLayout.Icon(host,dpi);
            if(placement!=null)
            {
                icon=WhaleLayout.Place(host,icon.Size,dpi,placement);
            }
            Position(icon);
            if(bubble.Visible||openBubble)bubble.Position(WhaleLayout.Popup(host,icon,dpi));
        }
        internal void ToggleBubble()
        {
            if(bubble.Visible){bubble.Hide();dismiss.Stop();if(!hover)motion.Stop();}
            else{openBubble=true;Place();openBubble=false;bubble.Invalidate();dismiss.Stop();dismiss.Start();motion.Start();}
        }
        internal void BeginDrag(Point screenPoint)
        {dragging=true;dragMoved=false;skipClick=false;dragStart=screenPoint;dragStartBounds=Bounds;dismiss.Stop();outside.Stop();Capture=true;}
        internal void DragTo(Point screenPoint)
        {
            if(!dragging)return;int dx=screenPoint.X-dragStart.X,dy=screenPoint.Y-dragStart.Y;
            if(!dragMoved&&Math.Max(Math.Abs(dx),Math.Abs(dy))<Math.Max(4,SystemInformation.DragSize.Width/2))return;
            dragMoved=true;skipClick=true;Cursor=Cursors.SizeAll;
            int dpi=ClientWindows.Dpi(Host);var target=dragStartBounds;target.Offset(dx,dy);
            var host=HostArea();target=WhaleLayout.ClampGroup(target,dpi,host);var range=WhaleLayout.AnchorRange(host,target.Size,dpi);
            placement=new AvatarPlacement{X=range.Width==0?1:(target.X-range.Left)/(double)range.Width,Y=range.Height==0?1:(target.Y-range.Top)/(double)range.Height};
            Position(target);
            if(bubble.Visible)bubble.Position(WhaleLayout.Popup(Rectangle.Empty,target,dpi));
        }
        internal void EndDrag()
        {
            if(!dragging)return;dragging=false;skipClick=dragMoved;Capture=false;Cursor=Cursors.Hand;
            if(dragMoved&&placement!=null&&rememberPosition)placement.Save();
            if(bubble.Visible){outside.Start();dismiss.Stop();dismiss.Start();}
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {if(e.Button==MouseButtons.Left&&WhaleArt.AvatarHit(e.Location,ClientSize))BeginDrag(System.Windows.Forms.Cursor.Position);base.OnMouseDown(e);}
        protected override void OnMouseMove(MouseEventArgs e){if(dragging)DragTo(System.Windows.Forms.Cursor.Position);base.OnMouseMove(e);}
        protected override void OnMouseUp(MouseEventArgs e){if(e.Button==MouseButtons.Left)EndDrag();base.OnMouseUp(e);}
        protected override void OnMouseCaptureChanged(EventArgs e){if(dragging&&!Capture)EndDrag();base.OnMouseCaptureChanged(e);}
        internal void CharacterClick(Point point)
        {if(!dragMoved&&!skipClick){if(WhaleArt.AvatarHit(point,ClientSize))ToggleBubble();else bubble.Hide();}}
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if(e.Button==MouseButtons.Left)CharacterClick(e.Location);
            base.OnMouseClick(e);
        }
        protected override void OnMouseEnter(EventArgs e){hover=true;motion.Start();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;if(!bubble.Visible)motion.Stop();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {WhaleArt.Whale(e.Graphics,ClientSize,phase);QuotaLiveState.Publish(Handle,value,error);AlphaTextWindow.SetValue(Handle,"CodexQuotaWhale",new IntPtr(1));}
        protected override void Dispose(bool disposing)
        {if(disposing){dragging=false;Capture=false;bubble.VisibleChanged-=BubbleVisibilityChanged;outside.Dispose();motion.Dispose();dismiss.Dispose();bubble.Dispose();}base.Dispose(disposing);}
    }
    internal static class WhaleDemo
    {
        internal static void Run()
        {
            var host=new Form{Text="鲸鱼额度组件交互预览",ClientSize=new Size(800,520),StartPosition=FormStartPosition.CenterScreen,BackColor=Color.FromArgb(15,22,31),MinimumSize=new Size(520,440)};
            var instructions=new Label{Text="小鲸鱼额度组件 · 示例数据\n\n点击右下角鲸鱼：展开 / 收起额度气泡\n缩小或移动本窗口：检查跟随效果\n此预览不连接账号、不查询网络。",AutoSize=true,Location=new Point(28,28),ForeColor=Color.FromArgb(206,228,238),Font=new Font("Microsoft YaHei UI",12)};
            host.Controls.Add(instructions);WhaleCompanion whale=null;
            var expiry=new System.Windows.Forms.Timer{Interval=180000};expiry.Tick+=delegate{host.Close();};
            host.Shown+=delegate
            {
                whale=new WhaleCompanion(host.Handle,delegate{},false);
                whale.UpdateQuota(new Quota{UpdatedUtc=DateTime.UtcNow,Primary=new WindowQuota{Remaining=89,Minutes=300},Secondary=new WindowQuota{Remaining=77,Minutes=10080}},null);whale.Place();expiry.Start();
            };
            host.Move+=delegate{if(whale!=null)whale.Place();};host.Resize+=delegate{if(whale!=null)whale.Place();};
            host.FormClosed+=delegate{expiry.Dispose();if(whale!=null)whale.Dispose();};
            Application.Run(host);host.Dispose();
        }
    }
}
