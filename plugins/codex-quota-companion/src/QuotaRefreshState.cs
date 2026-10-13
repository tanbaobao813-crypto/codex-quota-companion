using System;
using System.IO;
namespace CodexQuotaWidget
{
    internal sealed class QuotaRefreshState
    {
        internal Quota Value { get; private set; }
        internal string Error { get; private set; }
        internal int Failures { get; private set; }
        internal int IntervalSeconds { get { return Failures == 0 ? 10 : Failures == 1 ? 10 : Failures == 2 ? 20 : Failures == 3 ? 40 : 60; } }
        internal void Accept(Quota value,string error)
        {
            if(value!=null) { Value=value;Error=null;Failures=0; }
            else { Error=error??"unknown";Failures=Math.Min(100,Failures+1); }
        }
    }
    internal static class QuotaReadDiagnostics
    {
        private static readonly object Gate=new object();
        internal static void Failure(string code,int attempt)
        {
            // Fixed categories only: never save API responses, stderr or login credentials.
            try
            {
                lock(Gate)
                {
                    string path=Path.Combine(CompanionPaths.DataDirectory,"额度读取诊断.log");
                    if(File.Exists(path)&&new FileInfo(path).Length>32768)File.WriteAllText(path,"");
                    File.AppendAllText(path,DateTime.UtcNow.ToString("o")+" category="+code+" attempt="+attempt+Environment.NewLine);
                }
            }
            catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
}
