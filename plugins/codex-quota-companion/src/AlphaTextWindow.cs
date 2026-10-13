using System;
using System.Drawing;
using System.Runtime.InteropServices;
namespace CodexQuotaWidget
{
    internal static class AlphaTextWindow
    {
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint key, byte opacity, uint flags);
        [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr window, IntPtr rect, bool erase);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern bool SetProp(IntPtr window,string name,IntPtr value);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr GetProp(IntPtr window,string name);
        internal static void SetValue(IntPtr window,string name,IntPtr value){SetProp(window,name,value);}
        internal static bool Apply(IntPtr window,ClientWindows.PointNative destination,Size size,Quota quota,string error)
        {
            Color c=QuotaFace.KeyColor;
            bool applied=SetLayeredWindowAttributes(window,(uint)(c.R|(c.G<<8)|(c.B<<16)),255,1);
            if(applied)
            {
                ClientWindows.SetWindowPos(window,IntPtr.Zero,destination.X,destination.Y,size.Width,size.Height,0x0004|0x0010);
                InvalidateRect(window,IntPtr.Zero,true);
            }
            SetProp(window,"CodexQuotaAlphaText",applied?new IntPtr(1):IntPtr.Zero);
            SetProp(window,"CodexQuotaNativeText",applied?new IntPtr(1):IntPtr.Zero);
            return applied;
        }
    }
}
