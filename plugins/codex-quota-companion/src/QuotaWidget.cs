using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodexQuotaWidget
{
    internal sealed class WindowQuota
    {
        public double? Remaining;
        public int? Minutes;
        public long? ResetsAt;
    }

    internal sealed class Quota
    {
        public string Id, Name, Plan, CreditBalance;
        public DateTime UpdatedUtc;
        public WindowQuota Primary, Secondary;
        public bool UnlimitedCredits;
    }

    internal static class Data
    {
        internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
        internal static object Get(IDictionary<string, object> d, params string[] keys)
        {
            if (d == null) return null;
            foreach (string key in keys) { object value; if (d.TryGetValue(key, out value)) return value; }
            return null;
        }
        internal static string Text(object value) { return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture); }
        internal static double? Number(object value)
        {
            double v;
            return value != null && Double.TryParse(Text(value), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && !Double.IsInfinity(v) && !Double.IsNaN(v) ? (double?)v : null;
        }
        internal static WindowQuota ReadWindow(object value)
        {
            var d = value as IDictionary<string, object>;
            if (d == null) return null;
            double? remaining = Number(Get(d, "remaining_percent", "remainingPercent"));
            if (!remaining.HasValue) { double? used = Number(Get(d, "used_percent", "usedPercent")); if (used.HasValue) remaining = 100 - used.Value; }
            double? minutes = Number(Get(d, "window_minutes", "windowDurationMins"));
            double? reset = Number(Get(d, "resets_at", "resetsAt"));
            return new WindowQuota { Remaining = remaining.HasValue ? (double?)Math.Max(0, Math.Min(100, remaining.Value)) : null, Minutes = minutes.HasValue && minutes > 0 && minutes <= Int32.MaxValue ? (int?)minutes.Value : null, ResetsAt = reset.HasValue && reset > 0 && reset < 253402300800L ? (long?)reset.Value : null };
        }
        internal static Quota ParseEvent(string line)
        {
            // Only actual token-count records qualify. Mentions in prompts or tool output cannot supply a quota.
            if (line.IndexOf("\"token_count\"", StringComparison.Ordinal) < 0 || line.IndexOf("\"rate_limits\"", StringComparison.Ordinal) < 0) return null;
            var d = Json.DeserializeObject(line) as IDictionary<string, object>;
            if (Text(Get(d, "type")) != "event_msg") return null;
            var payload = Get(d, "payload") as IDictionary<string, object>;
            if (Text(Get(payload, "type")) != "token_count") return null;
            var limits = Get(payload, "rate_limits") as IDictionary<string, object>;
            if (limits == null) return null;
            DateTime updated;
            if (!DateTime.TryParse(Text(Get(d, "timestamp")), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out updated)) return null;
            var credits = Get(limits, "credits") as IDictionary<string, object>;
            return new Quota {
                Id = Text(Get(limits, "limit_id", "limitId")) ?? "codex",
                Name = Text(Get(limits, "limit_name", "limitName")),
                Plan = Text(Get(limits, "plan_type", "planType")),
                UpdatedUtc = updated,
                Primary = ReadWindow(Get(limits, "primary")),
                Secondary = ReadWindow(Get(limits, "secondary")),
                CreditBalance = Text(Get(credits, "balance")),
                UnlimitedCredits = Object.Equals(Get(credits, "unlimited"), true)
            };
        }
        internal static string SessionRoot()
        {
            string home = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (String.IsNullOrWhiteSpace(home)) home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            return Path.Combine(home, "sessions");
        }
        internal static IEnumerable<string> RecentFiles(string root)
        {
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            // Scan recent date folders, rather than walking years of chats on every update.
            var files = new List<string>();
            for (int days = 0; days < 14; days++)
            {
                DateTime date = DateTime.Now.Date.AddDays(-days);
                string dir = Path.Combine(root, date.ToString("yyyy"), date.ToString("MM"), date.ToString("dd"));
                if (Directory.Exists(dir)) files.AddRange(Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly));
            }
            return files.OrderByDescending(File.GetLastWriteTimeUtc).Take(40).ToArray();
        }
        internal static List<Quota> Load(string root)
        {
            var latest = new Dictionary<string, Quota>(StringComparer.Ordinal);
            foreach (string path in RecentFiles(root))
            {
                try
                {
                    using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        long start = Math.Max(0, f.Length - 1024 * 1024);
                        f.Seek(start, SeekOrigin.Begin);
                        using (var reader = new StreamReader(f, Encoding.UTF8, true))
                        {
                            if (start > 0) reader.ReadLine();
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                Quota quota;
                                try { quota = ParseEvent(line); } catch (ArgumentException) { continue; } catch (InvalidOperationException) { continue; }
                                if (quota == null) continue;
                                Quota old;
                                if (!latest.TryGetValue(quota.Id, out old) || quota.UpdatedUtc > old.UpdatedUtc) latest[quota.Id] = quota;
                            }
                        }
                    }
                }
                catch (IOException) { } // A log can rotate while it is being read.
                catch (UnauthorizedAccessException) { }
            }
            return latest.Values.OrderBy(q => q.Id == "codex" ? "" : q.Id).ToList();
        }
        internal static string WindowName(WindowQuota q, string fallback)
        {
            if (q == null || !q.Minutes.HasValue) return fallback;
            int minutes = q.Minutes.Value;
            if (minutes == 10080) return "每周额度";
            if (minutes % 1440 == 0) return (minutes / 1440) + " 天额度";
            if (minutes % 60 == 0) return (minutes / 60) + " 小时额度";
            return minutes + " 分钟额度";
        }
        internal static DateTime? ResetUtc(WindowQuota q)
        {
            if (q == null || !q.ResetsAt.HasValue) return null;
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(q.ResetsAt.Value);
        }
        internal static string ResetLabel(WindowQuota q)
        {
            DateTime? reset = ResetUtc(q);
            if (!reset.HasValue) return "重置时间未提供";
            if (reset.Value <= DateTime.UtcNow) return "已到重置时间，等待客户端更新";
            DateTime local = reset.Value.ToLocalTime();
            return "重置 " + local.ToString(local.Date == DateTime.Now.Date ? "今天 HH:mm" : "MM月dd日 HH:mm");
        }
        internal static object Snapshot(Quota q)
        {
            return new { limitId = q.Id, plan = q.Plan, updatedUtc = q.UpdatedUtc.ToString("o"), primary = q.Primary, secondary = q.Secondary };
        }
    }

    internal static class Program
    {
        internal static bool WhaleEnabled=true;
        [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
        [STAThread] private static int Main(string[] args)
        {
            WhaleEnabled=!args.Contains("--text-mode");
            if(args.Contains("--whale-preview")){int i=Array.IndexOf(args,"--whale-preview");WhaleArt.Preview(args[i+1]);return 0;}
            if(args.Contains("--whale-demo")){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);WhaleDemo.Run();return 0;}
            if(args.Contains("--whale-check")){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);WhaleNativeChecks.Run(args[Array.IndexOf(args,"--whale-check")+1]);return 0;}
            if (args.Contains("--preview"))
            {
                int position = Array.IndexOf(args, "--preview");
                string path = args.Length > position+1 ? args[position+1] : "科技款预览.png";
                var list = Data.Load(Data.SessionRoot());
                QuotaFace.Preview(path, list.FirstOrDefault(q => q.Id == "codex") ?? list.FirstOrDefault(), 4);
                return 0;
            }
            if (args.Contains("--snapshot") || args.Contains("--self-test") || args.Contains("--host-snapshot") || args.Contains("--host-details") || args.Contains("--dock-snapshot") || args.Contains("--stop") || args.Contains("--dpi-report"))
            {
                AttachConsole(-1);
                if (args.Contains("--self-test")) return SelfTest();
                if (args.Contains("--dpi-report")) { Console.WriteLine(Data.Json.Serialize(ClientWindows.DpiReport())); return 0; }
                if (args.Contains("--host-details")) { Console.WriteLine(Data.Json.Serialize(ClientWindows.Diagnostics())); return 0; }
                if (args.Contains("--dock-snapshot") || args.Contains("--stop")) { Console.WriteLine(Data.Json.Serialize(ClientWindows.Docked(args.Contains("--stop")))); return 0; }
                if (args.Contains("--host-snapshot"))
                {
                    Console.WriteLine(Data.Json.Serialize(ClientWindows.Find().Select(h => new { window = h.ToInt64(), dpi = ClientWindows.Dpi(h) }).ToArray()));
                    return 0;
                }
                var snapshot = Data.Load(Data.SessionRoot()).Select(Data.Snapshot).ToArray();
                Console.WriteLine(Data.Json.Serialize(snapshot));
                return snapshot.Length == 0 ? 2 : 0;
            }
            if (args.Contains("--launch-client")) ClientWindows.Launch();
            bool acquired;
            using (var mutex = new Mutex(false, "Local\\CodexQuotaWidget_" + Environment.UserName))
            {
                try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) return 0;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    using (var context = new DockContext(false))
                    { if (context.Active) Application.Run(context); }
                }
                finally { mutex.ReleaseMutex(); }
                return 0;
            }
        }
        private static int SelfTest()
        {
            try
            {
                Quota a = Data.ParseEvent("{\"type\":\"event_msg\",\"timestamp\":\"2026-10-09T07:00:00Z\",\"payload\":{\"type\":\"token_count\",\"rate_limits\":{\"limit_id\":\"codex\",\"primary\":{\"used_percent\":3,\"window_minutes\":300,\"resets_at\":1791549721},\"secondary\":null}}}");
                Assert(a.Primary.Remaining == 97 && a.Primary.Minutes == 300 && a.Secondary == null, "quota conversion and missing secondary");
                Assert(Data.ParseEvent("{\"type\":\"response_item\",\"payload\":{\"type\":\"token_count\",\"rate_limits\":{}}}") == null, "reject user/tool mentions");
                Assert(Data.ReadWindow(Data.Json.DeserializeObject("{\"usedPercent\":120}" )).Remaining == 0, "over-limit clamp");
                Assert(Data.ReadWindow(Data.Json.DeserializeObject("{\"remainingPercent\":66}" )).Remaining == 66, "legacy remaining percentage");
                Assert(Data.ReadWindow(Data.Json.DeserializeObject("{}" )).Remaining == null, "unknown is not zero");
                Assert(Data.ResetLabel(new WindowQuota { ResetsAt = 1 }).Contains("等待客户端更新"), "never invent replenished quota");
                Console.WriteLine("PASS: 6 quota correctness checks"); return 0;
            }
            catch (Exception e) { Console.WriteLine("FAIL: " + e.Message); return 1; }
        }
        private static void Assert(bool ok, string name) { if (!ok) throw new Exception(name); }
    }
}
