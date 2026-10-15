using System;
using System.IO;
using System.Threading;
namespace CodexQuotaWidget
{
    internal static class StartupGate
    {
        internal static bool Acquire(Mutex mutex,int timeoutMs)
        {
            try
            {
                if(mutex.WaitOne(0))return true;
                StartupDiagnostics.Mark("waiting-for-previous-instance");
                bool acquired=mutex.WaitOne(timeoutMs);
                StartupDiagnostics.Mark(acquired?"previous-instance-released":"another-instance-active");
                return acquired;
            }
            catch(AbandonedMutexException){StartupDiagnostics.Mark("recovered-abandoned-instance");return true;}
        }
    }
    internal static class StartupDiagnostics
    {
        internal static void Mark(string code)
        {
            try
            {
                string root=CompanionPaths.DataDirectory;
                Directory.CreateDirectory(root);
                string path=Path.Combine(root,"启动诊断.log");
                if(File.Exists(path)&&new FileInfo(path).Length>65536)File.WriteAllText(path,"");
                using(var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.ReadWrite))
                using(var writer=new StreamWriter(stream))writer.WriteLine(DateTime.UtcNow.ToString("o")+" "+code);
            }
            catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
        }
    }
}
