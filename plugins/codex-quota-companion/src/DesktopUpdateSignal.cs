using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CodexQuotaWidget
{
    internal static class DesktopUpdateSignal
    {
        internal static bool IsRecentIntent(string line, DateTime nowUtc)
        {
            if (String.IsNullOrEmpty(line) || line.IndexOf(" info update_resume_state_saved ", StringComparison.Ordinal) < 0) return false;
            int space = line.IndexOf(' ');
            DateTime timestamp;
            if (space <= 0 || !DateTime.TryParse(line.Substring(0, space), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp)) return false;
            TimeSpan age = nowUtc-timestamp;
            return age >= TimeSpan.FromSeconds(-5) && age <= TimeSpan.FromSeconds(90);
        }
        internal static bool WasUpdateRequested(uint processId)
        {
            if (processId == 0) return false;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] roots = {
                Path.Combine(local, "Codex", "Logs"),
                Path.Combine(local, "Packages", "OpenAI.Codex_2p2nqsd0c76g0", "LocalCache", "Local", "Codex", "Logs")
            };
            DateTime now = DateTime.UtcNow;
            DateTime[] dates = { DateTime.Now.Date, now.Date, DateTime.Now.Date.AddDays(-1) };
            foreach (string root in roots)
            foreach (DateTime date in dates.Distinct())
            {
                string folder = Path.Combine(root, date.ToString("yyyy"), date.ToString("MM"), date.ToString("dd"));
                try
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (string path in Directory.EnumerateFiles(folder, "*-"+processId+"-t0-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(4))
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            long offset = Math.Max(0, stream.Length-65536); stream.Seek(offset, SeekOrigin.Begin);
                            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                            {
                                if (offset > 0) reader.ReadLine();
                                string line; while ((line = reader.ReadLine()) != null) if (IsRecentIntent(line, now)) return true;
                            }
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return false;
        }
    }
}
