using System;
using System.IO;
using System.Linq;
namespace CodexQuotaWidget
{
    internal static class CompanionPaths
    {
        internal static string DataDirectory
        {
            get
            {
                string configured=Environment.GetEnvironmentVariable("CODEX_QUOTA_DATA_DIR");
                string path=String.IsNullOrWhiteSpace(configured)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexQuotaCompanion"):Path.GetFullPath(configured);
                Directory.CreateDirectory(path);return path;
            }
        }
        internal static string FindCli()
        {
            string configured=Environment.GetEnvironmentVariable("CODEX_QUOTA_CLI");
            if(!String.IsNullOrWhiteSpace(configured)&&File.Exists(configured))return Path.GetFullPath(configured);
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] roots={Path.Combine(local,"OpenAI","Codex","bin"),Path.Combine(roaming,"npm","node_modules","@openai","codex"),Path.Combine(local,"Programs","codex")};
            foreach(string root in roots)
            {
                try{if(Directory.Exists(root)){var match=Directory.EnumerateFiles(root,"codex.exe",SearchOption.AllDirectories).FirstOrDefault();if(match!=null)return match;}}
                catch(IOException){}catch(UnauthorizedAccessException){}
            }
            foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))
            {
                try{string candidate=Path.Combine(folder.Trim('"'),"codex.exe");if(File.Exists(candidate))return candidate;}
                catch(ArgumentException){}catch(IOException){}catch(UnauthorizedAccessException){}
            }
            throw new FileNotFoundException("未找到 Codex 程序。请安装客户端，或设置 CODEX_QUOTA_CLI。 ");
        }
    }
}
