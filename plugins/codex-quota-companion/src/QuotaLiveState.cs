using System;
namespace CodexQuotaWidget
{
    internal static class QuotaLiveState
    {
        internal static void Publish(IntPtr window,Quota quota,string error)
        {
            Set(window,"CodexQuotaPrimary",quota==null?null:quota.Primary);
            Set(window,"CodexQuotaSecondary",quota==null?null:quota.Secondary);
            long seconds=quota==null?0:(long)(quota.UpdatedUtc-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
            AlphaTextWindow.SetValue(window,"CodexQuotaUpdatedAt",new IntPtr(seconds));
            AlphaTextWindow.SetValue(window,"CodexQuotaSyncError",error==null?IntPtr.Zero:new IntPtr(1));
            AlphaTextWindow.SetValue(window,"CodexQuotaPrimaryReset",new IntPtr(quota==null||quota.Primary==null?0:quota.Primary.ResetsAt.GetValueOrDefault()));
            AlphaTextWindow.SetValue(window,"CodexQuotaSecondaryReset",new IntPtr(quota==null||quota.Secondary==null?0:quota.Secondary.ResetsAt.GetValueOrDefault()));
        }
        private static void Set(IntPtr window,string key,WindowQuota quota)
        {
            AlphaTextWindow.SetValue(window,key,quota!=null&&quota.Remaining.HasValue?new IntPtr(1+(int)Math.Round(quota.Remaining.Value*100)):IntPtr.Zero);
        }
        internal static double? Percent(IntPtr window,string key)
        {
            long raw=AlphaTextWindow.GetProp(window,key).ToInt64();return raw==0?null:(double?)((raw-1)/100.0);
        }
    }
}
