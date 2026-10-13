using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace CodexQuotaWidget
{
    internal static class WhaleNativeChecks
    {
        [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]private static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
        internal static void Run(string path)
        {
            var checks=new List<string>();Exception failure=null;WhaleCompanion whale=null;
            var host=new Form{Text="小鲸鱼验证（示例数据）",ClientSize=new Size(800,520),StartPosition=FormStartPosition.CenterScreen,BackColor=Color.FromArgb(15,22,31)};
            host.Controls.Add(new Label{AutoSize=true,Location=new Point(24,24),ForeColor=Color.White,Text="正在验证小鲸鱼交互与窗口跟随。\n示例数据，不连接账号；测试后自动关闭。"});
            Action<bool,string> check=delegate(bool ok,string name){if(!ok)throw new InvalidOperationException(name);checks.Add(name);};
            var epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
            Func<Quota> sample=()=>new Quota{UpdatedUtc=DateTime.UtcNow,Primary=new WindowQuota{Remaining=89,Minutes=300,ResetsAt=(long)(DateTime.UtcNow.AddHours(2)-epoch).TotalSeconds},Secondary=new WindowQuota{Remaining=77,Minutes=10080,ResetsAt=(long)(DateTime.UtcNow.AddDays(5)-epoch).TotalSeconds}};
            host.Shown+=async delegate
            {
                try
                {
                    whale=new WhaleCompanion(host.Handle,delegate{},false);var q=sample();whale.UpdateQuota(q,null);whale.Place();await Task.Delay(250);
                    check(whale.Visible,"native whale visible");check(ClientWindows.GetWindow(whale.Handle,4)==host.Handle,"owned by fixture host, no global topmost");
                    check(QuotaLiveState.Percent(whale.Handle,"CodexQuotaPrimary")==89,"painted whale carries shared quota");
                    var focus=GetForegroundWindow();whale.ToggleBubble();await Task.Delay(150);
                    check(whale.PopupVisible,"same click path opens bubble");check(GetForegroundWindow()==focus,"bubble opening does not steal focus");
                    uint colorKey,layerFlags;byte alpha;
                    check(GetLayeredWindowAttributes(whale.BubbleHandle,out colorKey,out alpha,out layerFlags)&&(layerFlags&2)!=0&&alpha==204,"native bubble opacity is eighty percent");
                    check(whale.OutsideDismissActive,"outside click hook installs while popup is visible");
                    var inside=OutsideBubbleDismiss.PhysicalBounds(whale.BubbleHandle);
                    check(!whale.HandleOutsideClick(new Point(inside.X+30,inside.Y+45))&&whale.PopupVisible,"inside click preserves popup");
                    check(QuotaLiveState.Percent(whale.BubbleHandle,"CodexQuotaSecondary")==77,"bubble paints same source snapshot");
                    check(AlphaTextWindow.GetProp(whale.BubbleHandle,"CodexQuotaPrimaryReset").ToInt64()==q.Primary.ResetsAt,"bubble retains exact primary reset timestamp");
                    check(WhaleArt.ResetText(q.Primary)=="重置 "+Data.ResetUtc(q.Primary).Value.ToLocalTime().ToString("MM/dd HH:mm"),"primary reset date and local time format");
                    check(WhaleArt.ResetText(q.Secondary)=="重置 "+Data.ResetUtc(q.Secondary).Value.ToLocalTime().ToString("MM/dd HH:mm"),"weekly reset date and local time format");
                    check(WhaleArt.ResetText(null)=="重置时间未提供","missing reset is explicit instead of invented");
                    check(whale.BubbleBounds.Width==WhaleArt.BubbleWidth&&whale.BubbleBounds.Height==WhaleArt.BubbleHeight,"popup uses compact dimensions");
                    double scale=ClientWindows.Dpi(host.Handle)/96.0;
                    check(Math.Abs(whale.BubbleBounds.Right-21*scale-whale.Left+4*scale)<3*scale,"speech body sits four pixels beside portrait");
                    check(Math.Abs(whale.BubbleBounds.Top+107*scale-(whale.Top+whale.Height*.55))<3*scale,"speech tail stays level with portrait face");
                    check(WhaleArt.BubbleHit(new Point(182,105),new Size(202,132))&&!WhaleArt.BubbleHit(new Point(196,20),new Size(202,132)),"speech tail is clickable while surrounding blank space stays transparent");
                    q=sample();q.Primary.Remaining=84;whale.UpdateQuota(q,null);await Task.Delay(150);
                    check(QuotaLiveState.Percent(whale.Handle,"CodexQuotaPrimary")==84&&QuotaLiveState.Percent(whale.BubbleHandle,"CodexQuotaPrimary")==84,"open bubble updates together with whale without another reader");
                    whale.UpdateQuota(q,"timeout");await Task.Delay(100);
                    check(AlphaTextWindow.GetProp(whale.BubbleHandle,"CodexQuotaSyncError").ToInt64()==1&&QuotaLiveState.Percent(whale.BubbleHandle,"CodexQuotaPrimary")==84,"read failure retains number with stale state");
                    whale.UpdateQuota(q,null);whale.ToggleBubble();check(!whale.PopupVisible,"second click path closes bubble");
                    check(!whale.OutsideDismissActive,"closing popup releases outside click hook");
                    whale.ToggleBubble();await Task.Delay(50);var surface=OutsideBubbleDismiss.PhysicalBounds(host.Handle);
                    check(whale.HandleOutsideClick(new Point(surface.X+35,surface.Y+90))&&!whale.PopupVisible,"outside blank click dismisses without needing close button");
                    check(!whale.OutsideDismissActive,"outside dismissal releases listener immediately");
                    host.ClientSize=new Size(540,460);whale.Place();await Task.Delay(150);
                    var r=new Rectangle(host.PointToScreen(Point.Empty),host.ClientSize);
                    check(r.Contains(whale.Bounds)&&whale.Width<=60,"small host uses responsive compact portrait");
                    whale.ToggleBubble();await Task.Delay(100);check(r.Contains(whale.BubbleBounds),"bubble remains within resized host");
                    host.WindowState=FormWindowState.Minimized;whale.Place();check(!whale.Visible&&!whale.PopupVisible,"minimize hides both surfaces");
                    host.WindowState=FormWindowState.Normal;whale.Place();await Task.Delay(100);check(whale.Visible,"restore reattaches whale");
                    host.ClientSize=new Size(800,520);whale.Place();if(!whale.PopupVisible)whale.ToggleBubble();await Task.Delay(100);
                    var beforeIcon=whale.Bounds;var beforeBubble=whale.BubbleBounds;
                    var offset=new Point(beforeBubble.X-beforeIcon.X,beforeBubble.Y-beforeIcon.Y);
                    var grip=new Point(beforeIcon.X+beforeIcon.Width/2,beforeIcon.Y+beforeIcon.Height/2);
                    whale.BeginDrag(grip);check(whale.Capture,"drag captures pointer while left button is held");
                    check(!whale.OutsideDismissActive,"outside dismissal pauses during drag");
                    whale.DragTo(new Point(grip.X-100,grip.Y-45));whale.EndDrag();await Task.Delay(100);
                    check(whale.Left!=beforeIcon.Left&&whale.Top!=beforeIcon.Top,"drag moves character from prior position");
                    check(whale.PopupVisible,"drag release preserves open bubble");
                    check(whale.OutsideDismissActive,"outside dismissal resumes after drag release");
                    whale.CharacterClick(new Point(whale.Width/2,whale.Height/2));check(whale.PopupVisible,"click raised after drag release is suppressed");
                    check(whale.BubbleBounds.Location==new Point(whale.Left+offset.X,whale.Top+offset.Y),"drag keeps exact bubble-to-character offset");
                    check(!whale.Capture,"release ends pointer capture");
                    var draggedIcon=whale.Bounds;whale.Place();check(whale.Bounds==draggedIcon,"routine placement does not reset dragged position");
                    whale.ToggleBubble();whale.ToggleBubble();check(whale.BubbleBounds.Location==new Point(whale.Left+offset.X,whale.Top+offset.Y),"close and reopen preserve speech alignment");
                    grip=new Point(whale.Left+whale.Width/2,whale.Top+whale.Height/2);whale.BeginDrag(grip);whale.DragTo(new Point(grip.X+65,grip.Y+30));whale.EndDrag();
                    check(whale.BubbleBounds.Location==new Point(whale.Left+offset.X,whale.Top+offset.Y),"second drag keeps identical direction and spacing");
                    grip=new Point(whale.Left+whale.Width/2,whale.Top+whale.Height/2);var area=new Rectangle(host.PointToScreen(Point.Empty),host.ClientSize);
                    whale.BeginDrag(grip);whale.DragTo(new Point(area.Left-500,area.Top-500));whale.EndDrag();
                    check(area.Contains(Rectangle.Union(whale.Bounds,whale.BubbleBounds)),"drag beyond client edge clamps complete group inside client");
                    check(whale.BubbleBounds.Location==new Point(whale.Left+offset.X,whale.Top+offset.Y),"edge clamp never flips or shifts bubble independently");
                    var startIcon=whale.Bounds;host.Location=new Point(host.Left+20,host.Top+15);whale.Place();
                    check(whale.Left==startIcon.Left+20&&whale.Top==startIcon.Top+15,"manually placed group follows host movement");
                    var saved=new AvatarPlacement{X=.72,Y=.41};saved.Save();var loaded=AvatarPlacement.Load();
                    check(loaded!=null&&loaded.X==saved.X&&loaded.Y==saved.Y,"saved placement reloads exact normalized client position");
                    host.ClientSize=new Size(1400,850);whale.Place();int expandedWidth=whale.Width;
                    host.ClientSize=new Size(540,360);whale.Place();
                    area=new Rectangle(host.PointToScreen(Point.Empty),host.ClientSize);
                    check(whale.Width<expandedWidth&&area.Contains(Rectangle.Union(whale.Bounds,whale.BubbleBounds)),"expand then shrink adapts portrait and retains complete group inside client");
                    var firstSmall=whale.Bounds;host.ClientSize=new Size(1400,850);whale.Place();host.ClientSize=new Size(540,360);whale.Place();
                    check(whale.Bounds==firstSmall,"repeated resizing returns to same relative position without drift");
                    foreach(int testDpi in new[]{96,144,192})
                    {
                        var frame=new Rectangle(-800,60,900,650);var icon=WhaleLayout.Icon(frame,testDpi);var popup=WhaleLayout.Popup(frame,icon,testDpi);
                        check(frame.Contains(Rectangle.Union(icon,popup)),"default right-bottom group contained at DPI "+testDpi);
                    }
                    whale.Dispose();check(whale.IsDisposed,"companion and timers dispose on host exit");
                }
                catch(Exception e){failure=e;}
                finally
                {
                    if(whale!=null&&!whale.IsDisposed)whale.Dispose();
                    File.WriteAllText(path,Data.Json.Serialize(new{ok=failure==null,checks=checks,error=failure==null?null:failure.Message,networkQueries=0,fixtureOnly=true}));host.Close();
                }
            };
            Application.Run(host);host.Dispose();
        }
    }
}
