using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace CodexQuotaWidget
{
    // Exists only while the popup is open. Every event passes through unchanged.
    // No keyboard handling, saved coordinates, input injection or polling.
    internal sealed class OutsideBubbleDismiss : IDisposable
    {
        private delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int type,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll")]private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        private readonly Form bubble,avatar;private readonly HookProc callback;
        private IntPtr hook;private bool disposed;private int generation;
        internal bool Active {get{return hook!=IntPtr.Zero;}}
        internal OutsideBubbleDismiss(Form popup,Form character){bubble=popup;avatar=character;callback=Observe;}
        internal static Rectangle PhysicalBounds(IntPtr window)
        {
            IntPtr old=IntPtr.Zero;
            try
            {
                old=SetThreadDpiAwarenessContext(new IntPtr(-4));
                ClientWindows.Rect r;return ClientWindows.GetWindowRect(window,out r)?Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom):Rectangle.Empty;
            }
            finally {if(old!=IntPtr.Zero)SetThreadDpiAwarenessContext(old);}
        }
        internal void Start()
        {
            if(disposed||Active||!bubble.Visible)return;
            generation++;
            hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
            AlphaTextWindow.SetValue(bubble.Handle,"CodexQuotaOutsideDismiss",Active?new IntPtr(1):IntPtr.Zero);
        }
        internal void Stop()
        {
            generation++;
            if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}
            if(!bubble.IsDisposed&&bubble.IsHandleCreated)AlphaTextWindow.SetValue(bubble.Handle,"CodexQuotaOutsideDismiss",IntPtr.Zero);
        }
        private IntPtr Observe(int code,IntPtr message,IntPtr data)
        {
            int kind=message.ToInt32();
            if(code>=0&&!disposed&&(kind==0x201||kind==0x204||kind==0x207))
            {
                var point=new Point(Marshal.ReadInt32(data),Marshal.ReadInt32(data,4));
                int epoch=generation;
                try {bubble.BeginInvoke((Action)delegate{if(epoch==generation)DismissAt(point);});}catch(InvalidOperationException){}
            }
            return CallNextHookEx(hook,code,message,data);
        }
        internal bool DismissAt(Point point)
        {
            if(disposed||!Active||bubble.IsDisposed||!bubble.Visible)return false;
            Rectangle speech=PhysicalBounds(bubble.Handle);
            if(speech.Contains(point)&&WhaleArt.BubbleHit(new Point(point.X-speech.X,point.Y-speech.Y),speech.Size))return false;
            Rectangle character=PhysicalBounds(avatar.Handle);
            if(character.Contains(point)&&WhaleArt.AvatarHit(new Point(point.X-character.X,point.Y-character.Y),character.Size))return false;
            bubble.Hide();return true;
        }
        public void Dispose(){if(disposed)return;Stop();disposed=true;}
    }
}
