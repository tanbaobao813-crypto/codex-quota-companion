using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CodexQuotaWidget
{
    internal static class ClientWindows
    {
        internal const string AppId = "OpenAI.Codex_2p2nqsd0c76g0!App";
        internal delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        internal delegate void EventProc(IntPtr hook, uint type, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
        [StructLayout(LayoutKind.Sequential)] internal struct Rect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] internal struct PointNative { internal int X, Y; }
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool ClientToScreen(IntPtr window, ref PointNative point);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] internal static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd, uint command);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int length);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetApplicationUserModelId(IntPtr process, ref uint size, StringBuilder appId);
        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, EventProc callback, uint pid, uint thread, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLong64(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLong64(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] private static extern IntPtr GetThreadDpiAwarenessContext();
        [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
        internal static object DpiReport()
        {
            var reports = new List<object>();
            var original = GetThreadDpiAwarenessContext();
            foreach (IntPtr host in Find())
            {
                Rect logical; GetWindowRect(host, out logical);
                SetThreadDpiAwarenessContext(new IntPtr(-4));
                Rect physical; GetWindowRect(host, out physical);
                SetThreadDpiAwarenessContext(original);
                reports.Add(new { host = host.ToInt64(), hostDpi = GetDpiForWindow(host), hostAwareness = GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(host)), threadAwareness = GetAwarenessFromDpiAwarenessContext(original), systemDpi = GetDpiForSystem(), logicalWidth = logical.Right-logical.Left, physicalWidth = physical.Right-physical.Left });
            }
            return reports;
        }

        internal static long Style(IntPtr hwnd, int index) { return IntPtr.Size == 8 ? GetWindowLong64(hwnd, index).ToInt64() : GetWindowLong32(hwnd, index); }
        internal static void Style(IntPtr hwnd, int index, long value)
        {
            if (IntPtr.Size == 8) SetWindowLong64(hwnd, index, new IntPtr(value)); else SetWindowLong32(hwnd, index, unchecked((int)value));
        }
        internal static int Dpi(IntPtr host)
        {
            try { uint dpi = GetDpiForWindow(host); return dpi > 0 ? (int)dpi : 96; } catch (EntryPointNotFoundException) { return 96; }
        }
        internal static void MatchDpi(IntPtr host)
        {
            try { SetThreadDpiAwarenessContext(GetWindowDpiAwarenessContext(host)); } catch (EntryPointNotFoundException) { }
        }
        internal static bool IsClient(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !IsWindowVisible(hwnd) || GetParent(hwnd) != IntPtr.Zero || GetWindow(hwnd, 4) != IntPtr.Zero) return false;
            var className = new StringBuilder(100);
            GetClassName(hwnd, className, className.Capacity);
            if (!className.ToString().StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal)) return false;
            Rect r; if (!GetClientRect(hwnd, out r) || !IsIconic(hwnd)&&(r.Right - r.Left < 400 || r.Bottom - r.Top < 200)) return false;
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            IntPtr process = OpenProcess(0x1000, false, pid);
            if (process == IntPtr.Zero)
            {
                // Restricted hosts may allow window inspection but deny querying another process.
                // Match only the observed desktop-client title, never arbitrary browser tab titles.
                var title = new StringBuilder(512); GetWindowText(hwnd, title, title.Capacity);
                return title.ToString() == "ChatGPT" || title.ToString() == "Codex";
            }
            try
            {
                var id = new StringBuilder(512); uint size = (uint)id.Capacity;
                if (GetApplicationUserModelId(process, ref size, id) == 0 && id.ToString() == AppId) return true;
                var path = new StringBuilder(2048); int length = path.Capacity;
                if (!QueryFullProcessImageName(process, 0, path, ref length)) return false;
                string exe = path.ToString();
                return exe.IndexOf("\\WindowsApps\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) >= 0 && Path.GetFileName(exe).Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase);
            }
            finally { CloseHandle(process); }
        }
        internal static string CliFor(IntPtr host)
        {
            uint pid;GetWindowThreadProcessId(host,out pid);IntPtr process=OpenProcess(0x1000,false,pid);
            if(process!=IntPtr.Zero)
            {
                try
                {
                    var path=new StringBuilder(2048);int size=path.Capacity;
                    if(QueryFullProcessImageName(process,0,path,ref size))
                    {
                        string binary=Path.Combine(Path.GetDirectoryName(path.ToString()),"resources","codex.exe");
                        if(File.Exists(binary))return binary;
                    }
                }
                finally{CloseHandle(process);}
            }
            return CompanionPaths.FindCli();
        }
        internal static List<IntPtr> Find()
        {
            var found = new List<IntPtr>();
            EnumWindows(delegate(IntPtr h, IntPtr p) { if (IsClient(h)) found.Add(h); return true; }, IntPtr.Zero);
            return found;
        }
        internal static List<object> Diagnostics()
        {
            var found = new List<object>();
            EnumWindows(delegate(IntPtr hwnd, IntPtr param)
            {
                var cls = new StringBuilder(100); GetClassName(hwnd, cls, cls.Capacity);
                if (!cls.ToString().StartsWith("Chrome_WidgetWin_")) return true;
                uint pid; GetWindowThreadProcessId(hwnd, out pid);
                IntPtr process = OpenProcess(0x1000, false, pid);
                string path = null, appId = null;
                if (process != IntPtr.Zero)
                {
                    try
                    {
                        var exe = new StringBuilder(2048); int size = exe.Capacity;
                        if (QueryFullProcessImageName(process, 0, exe, ref size)) path = exe.ToString();
                        var id = new StringBuilder(512); uint length = (uint)id.Capacity;
                        if (GetApplicationUserModelId(process, ref length, id) == 0) appId = id.ToString();
                    }
                    finally { CloseHandle(process); }
                }
                Rect r; GetClientRect(hwnd, out r);
                found.Add(new { window = hwnd.ToInt64(), pid = pid, parent = GetParent(hwnd).ToInt64(), owner = GetWindow(hwnd, 4).ToInt64(), path = path, appId = appId, width = r.Right, height = r.Bottom, accepted = IsClient(hwnd) });
                return true;
            }, IntPtr.Zero);
            return found;
        }
        internal static List<object> Docked(bool stop)
        {
            var found = new List<object>();
            var hosts = Find();
            ClientWindows.EnumProc collect = delegate(IntPtr child, IntPtr param)
            {
                    var title = new StringBuilder(100); GetWindowText(child, title, title.Capacity);
                    string caption=title.ToString();
                    if (caption != "Codex 全局额度条"&&caption!="Codex 小鲸鱼"&&caption!="Codex 额度气泡") return true;
                    IntPtr host = GetWindow(child, 4);
                    if (host == IntPtr.Zero) host = GetParent(child);
                    if (!hosts.Contains(host)) return true;
                    Rect r, hr; GetWindowRect(child, out r); GetWindowRect(host, out hr);
                    found.Add(new { kind=caption=="Codex 小鲸鱼"?"whale":caption=="Codex 额度气泡"?"bubble":"text", host = host.ToInt64(), child = child.ToInt64(), parent = GetParent(child).ToInt64(), owner = GetWindow(child, 4).ToInt64(), x = r.Left, y = r.Top, hostX = hr.Left, hostY = hr.Top, hostRight = hr.Right, hostBottom = hr.Bottom, width = r.Right - r.Left, height = r.Bottom - r.Top, childStyle = (Style(child, -16) & 0x40000000L) != 0, alphaText = AlphaTextWindow.GetProp(child, "CodexQuotaAlphaText").ToInt64() == 1, nativeText = AlphaTextWindow.GetProp(child, "CodexQuotaNativeText").ToInt64() == 1, clickThrough = (Style(child, -20) & 0x20) != 0, livePrimary = QuotaLiveState.Percent(child,"CodexQuotaPrimary"), liveSecondary = QuotaLiveState.Percent(child,"CodexQuotaSecondary"), updatedAt = AlphaTextWindow.GetProp(child,"CodexQuotaUpdatedAt").ToInt64(), syncError = AlphaTextWindow.GetProp(child,"CodexQuotaSyncError").ToInt64() != 0, primaryResetAt = AlphaTextWindow.GetProp(child,"CodexQuotaPrimaryReset").ToInt64(), secondaryResetAt = AlphaTextWindow.GetProp(child,"CodexQuotaSecondaryReset").ToInt64(), outsideDismissActive = AlphaTextWindow.GetProp(child,"CodexQuotaOutsideDismiss").ToInt64() != 0, visible = IsWindowVisible(child) });
                    if (stop&&caption=="Codex 全局额度条") PostMessage(child, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    return true;
            };
            EnumWindows(collect, IntPtr.Zero);
            foreach (IntPtr host in hosts) EnumChildWindows(host, collect, IntPtr.Zero);
            return found;
        }
        internal static void Launch()
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + AppId) { UseShellExecute = true });
        }
    }

    internal static class DockLayout
    {
        internal static Size SizeFor(int clientWidth, int clientHeight, int dpi)
        {
            double scale = Math.Max(1, dpi) / 96.0;
            double sidebar = Math.Min(285 * scale, clientWidth * .45);
            return new Size(Math.Max(1, (int)Math.Round(Math.Min(192*scale, clientWidth-sidebar-16*scale))), Math.Max(1, (int)Math.Round(14*scale)));
        }
        internal static ClientWindows.PointNative Position(int clientWidth, int clientHeight, int dpi, Size size)
        {
            double scale = Math.Max(1, dpi)/96.0;
            double sidebar = Math.Min(285*scale, clientWidth*.45);
            return new ClientWindows.PointNative { X = Math.Max(0, (int)Math.Round((clientWidth+sidebar-size.Width)/2)), Y = Math.Max(0, clientHeight-size.Height-(int)Math.Round(scale)) };
        }
    }

    // An owned tool window is visually docked inside the client; Chromium's compositor can
    // cover foreign child HWNDs. Ownership follows minimize/restore and avoids global topmost.
    internal sealed class DockStrip : Form
    {
        internal readonly IntPtr Host;
        private readonly System.Windows.Forms.Timer ageTimer = new System.Windows.Forms.Timer();
        private readonly System.Threading.Timer debounce;
        private readonly object gate = new object();
        private readonly string root = Data.SessionRoot();
        private readonly string liveBinary;
        private readonly QuotaRefreshState refresh=new QuotaRefreshState();
        private readonly CancellationTokenSource readCancel=new CancellationTokenSource();
        private readonly WhaleCompanion whale;
        private FileSystemWatcher watcher;
        private Quota quota;
        private bool loading, pending, closing;
        private bool refreshScheduled;
        private DateTime lastAttemptUtc=DateTime.MinValue;
        private int dpi;
        private string failure;
        private bool frameDirty = true;

        internal DockStrip(IntPtr host)
        {
            Host = host;
            liveBinary=ClientWindows.CliFor(host);
            if(Program.WhaleEnabled)whale=new WhaleCompanion(host,delegate{Schedule(0);});
            ClientWindows.MatchDpi(host);
            Text = "Codex 全局额度条";
            AccessibleName = Text;
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            debounce = new System.Threading.Timer(delegate { LoadQuota(); }, null, Timeout.Infinite, Timeout.Infinite);
            ageTimer.Interval = 30000; ageTimer.Tick += delegate { if(ClientWindows.IsWindowVisible(Host)&&!ClientWindows.IsIconic(Host))Schedule(0); }; ageTimer.Start();
            StartWatcher();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams p = base.CreateParams; p.ExStyle |= 0x080800A0; return p; }
        }
        internal void Attach()
        {
            IntPtr child = Handle;
            ClientWindows.Style(child, -8, Host.ToInt64());
            if (ClientWindows.GetWindow(child, 4) != Host) throw new InvalidOperationException("无法绑定客户端窗口，错误 " + Marshal.GetLastWin32Error());
            Place(); Schedule(0);
        }
        internal void Place()
        {
            if (closing || !ClientWindows.IsWindow(Host)) return;
            if (ClientWindows.IsIconic(Host) || !ClientWindows.IsWindowVisible(Host)) { if (Visible) Hide();if(whale!=null)whale.Place(); return; }
            if(whale!=null){if(Visible)Hide();bool shown=whale.Visible;whale.Place();if(!shown&&whale.Visible)Schedule(0);return;}
            ClientWindows.MatchDpi(Host);
            ClientWindows.Rect rect;
            if (!ClientWindows.GetClientRect(Host, out rect)) return;
            if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return;
            int currentDpi = ClientWindows.Dpi(Host);
            dpi = currentDpi;
            Size size = DockLayout.SizeFor(rect.Right-rect.Left, rect.Bottom-rect.Top, dpi);
            var point = DockLayout.Position(rect.Right-rect.Left, rect.Bottom-rect.Top, dpi, size);
            ClientWindows.ClientToScreen(Host, ref point);
            ClientWindows.Rect actual;
            bool positioned = ClientWindows.GetWindowRect(Handle, out actual) && actual.Left == point.X && actual.Top == point.Y && actual.Right-actual.Left == size.Width && actual.Bottom-actual.Top == size.Height;
            if (frameDirty || !positioned || AlphaTextWindow.GetProp(Handle, "CodexQuotaAlphaText") == IntPtr.Zero)
            {
                if (!AlphaTextWindow.Apply(Handle, point, size, quota, failure)) { Hide(); return; }
                frameDirty = false;
            }
            if (!Visible) { Show();Schedule(0); }
        }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(QuotaFace.KeyColor); }
        protected override void OnPaint(PaintEventArgs e) { QuotaFace.PaintText(e.Graphics,ClientSize,quota,failure);QuotaLiveState.Publish(Handle,quota,failure); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0084) { message.Result = new IntPtr(-1); return; }
            base.WndProc(ref message);
        }
        private void StartWatcher()
        {
            if (watcher != null || !Directory.Exists(root)) return;
            try
            {
                watcher = new FileSystemWatcher(root, "*.jsonl") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size, InternalBufferSize = 32768 };
                watcher.Changed += delegate { Schedule(350); }; watcher.Created += delegate { Schedule(350); };
                watcher.Renamed += delegate { Schedule(350); }; watcher.Deleted += delegate { Schedule(350); };
                watcher.Error += delegate { Schedule(0); };
                watcher.EnableRaisingEvents = true;
            }
            catch (IOException) { failure = "客户端记录监听失败，可右键刷新"; }
            catch (UnauthorizedAccessException) { failure = "没有客户端记录读取权限"; }
        }
        private void Schedule(int delay)
        {
            lock(gate)
            {
                if(closing||refreshScheduled)return;
                if(!ClientWindows.IsWindowVisible(Host)||ClientWindows.IsIconic(Host))return;
                if(loading){pending=true;return;}
                int cooldown=(int)Math.Max(0,(lastAttemptUtc.AddSeconds(refresh.IntervalSeconds)-DateTime.UtcNow).TotalMilliseconds);
                refreshScheduled=true;debounce.Change(Math.Max(delay,cooldown),Timeout.Infinite);
            }
        }
        private void LoadQuota()
        {
            lock (gate) { refreshScheduled=false;if (closing) return; if(!ClientWindows.IsWindowVisible(Host)||ClientWindows.IsIconic(Host)){pending=false;return;} if (loading) { pending = true; return; } loading = true;lastAttemptUtc=DateTime.UtcNow; }
            Quota value = null; string error = null;
            try { value=LiveQuotaReader.Read(liveBinary,readCancel.Token); }
            catch (Exception e) { if(e is OutOfMemoryException)throw;var detail=e as QuotaReadException;error=detail==null?"internal":detail.Code; }
            finally
            {
                lock (gate)
                {
                    loading = false;
                    if (!closing)
                    {
                        if(value!=null&&refresh.Failures>0)QuotaReadDiagnostics.Failure("recovered",0);
                        refresh.Accept(value,error);
                        Quota shown=refresh.Value;string warning=refresh.Error;
                        try { BeginInvoke((Action)delegate { if(closing)return;quota = shown; failure = warning; Render(); }); } catch (InvalidOperationException) { }
                        if (pending||error!=null) { pending = false; int wait=(int)Math.Max(1000,(lastAttemptUtc.AddSeconds(refresh.IntervalSeconds)-DateTime.UtcNow).TotalMilliseconds);refreshScheduled=true;debounce.Change(wait,Timeout.Infinite); }
                    }
                }
            }
        }
        private void Render()
        {
            if (closing) return;
            if(whale!=null)whale.UpdateQuota(quota,failure);
            frameDirty = true; Place();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            lock (gate) { closing = true; debounce.Dispose();readCancel.Cancel(); }
            ageTimer.Stop(); ageTimer.Dispose();
            if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); }
            if(whale!=null)whale.Dispose();
            base.OnFormClosing(e);
        }
    }

    internal sealed class DockContext : ApplicationContext
    {
        private readonly Control dispatcher = new Control();
        private readonly Dictionary<IntPtr, DockStrip> strips = new Dictionary<IntPtr, DockStrip>();
        private readonly Dictionary<IntPtr, Process> processes = new Dictionary<IntPtr, Process>();
        private readonly Dictionary<IntPtr, uint> processIds = new Dictionary<IntPtr, uint>();
        private readonly ClientWindows.EventProc callback;
        private readonly List<IntPtr> hooks = new List<IntPtr>();
        private readonly System.Windows.Forms.Timer deadline = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer placement = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer updateRestartDeadline = new System.Windows.Forms.Timer();
        private readonly HashSet<IntPtr> pendingPlacement = new HashSet<IntPtr>();
        private readonly bool discoverClients;
        private readonly Func<uint, bool> updateRequested;
        private readonly Func<IntPtr, bool> isClient;
        private bool waitingForUpdateRestart;
        private bool attached, closing;
        internal bool Active { get { return !closing; } }

        internal DockContext(bool launch) : this(launch, null) { }
        internal DockContext(bool launch, IEnumerable<IntPtr> explicitHosts) : this(launch, explicitHosts, null, null, 300000) { }
        internal DockContext(bool launch, IEnumerable<IntPtr> explicitHosts, Func<uint, bool> updateCheck, Func<IntPtr, bool> clientCheck, int restartTimeoutMs)
        {
            updateRequested = updateCheck ?? DesktopUpdateSignal.WasUpdateRequested;
            isClient = clientCheck ?? ClientWindows.IsClient;
            updateRestartDeadline.Interval = Math.Max(1, restartTimeoutMs);
            updateRestartDeadline.Tick += delegate { updateRestartDeadline.Stop(); if (strips.Count == 0) ExitThread(); };
            var unused = dispatcher.Handle;
            callback = OnWindowEvent;
            hooks.Add(ClientWindows.SetWinEventHook(0x8000, 0x800B, IntPtr.Zero, callback, 0, 0, 2));
            hooks.Add(ClientWindows.SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 2));
            hooks.Add(ClientWindows.SetWinEventHook(0xA, 0xB, IntPtr.Zero, callback, 0, 0, 2));
            hooks.Add(ClientWindows.SetWinEventHook(0x16, 0x17, IntPtr.Zero, callback, 0, 0, 2));
            if (hooks.Any(h => h == IntPtr.Zero)) throw new InvalidOperationException("客户端窗口监听无法启动");
            if (launch) ClientWindows.Launch();
            discoverClients = explicitHosts == null;
            placement.Interval = 16;
            placement.Tick += delegate
            {
                placement.Stop(); var handles = pendingPlacement.ToArray(); pendingPlacement.Clear();
                foreach (IntPtr h in handles) { DockStrip strip; if (strips.TryGetValue(h, out strip)) strip.Place(); }
            };
            foreach (IntPtr host in explicitHosts ?? ClientWindows.Find()) Add(host);
            deadline.Interval = 60000;
            deadline.Tick += delegate
            {
                // One bounded startup deadline; no process polling after a client has been found.
                deadline.Stop();
                if (!attached) ExitThread();
            };
            if (!attached) deadline.Start();
        }
        private void OnWindowEvent(IntPtr hook, uint type, IntPtr hwnd, int objectId, int childId, uint thread, uint time)
        {
            if (closing || hwnd == IntPtr.Zero) return;
            if (type >= 0x8000 && objectId != 0 && objectId != -4) return;
            IntPtr host = strips.ContainsKey(hwnd) ? hwnd : ClientWindows.GetAncestor(hwnd, 2);
            DockStrip strip;
            if (strips.TryGetValue(host, out strip))
            {
                if (type == 0x8001 && hwnd == host && objectId == 0 && childId == 0) Remove(host);
                else if (type == 3 || type == 0xB || type == 0x16 || type == 0x17 || type == 0x8002 || (type == 0x8003 && hwnd == host)) strip.Place();
                else if (type == 0x800B || type == 0xA) { pendingPlacement.Add(host); placement.Start(); }
            }
            else if ((discoverClients || waitingForUpdateRestart) && (type == 3 || type == 0x8000 || type == 0x8002 || type == 0x800B) && isClient(host)) Add(host);
        }
        private void Add(IntPtr host)
        {
            if (closing || strips.ContainsKey(host)) return;
            if (!ClientWindows.IsWindow(host)) return;
            uint hostProcess; ClientWindows.GetWindowThreadProcessId(host, out hostProcess);
            var strip = new DockStrip(host);
            try { strip.Attach(); }
            catch { strip.Dispose(); throw; }
            strips.Add(host, strip); attached = true; deadline.Stop();
            processIds[host] = hostProcess;
            waitingForUpdateRestart = false; updateRestartDeadline.Stop();
            strip.FormClosed += delegate { Remove(host); };
            try
            {
                uint pid; ClientWindows.GetWindowThreadProcessId(host, out pid);
                var process = Process.GetProcessById((int)pid); processes[host] = process;
                process.EnableRaisingEvents = true;
                process.Exited += delegate { if (!closing) { try { dispatcher.BeginInvoke((Action)delegate { Remove(host); }); } catch (InvalidOperationException) { } } };
            }
            catch (ArgumentException) { if (!ClientWindows.IsWindow(host)) Remove(host); }
            catch (System.ComponentModel.Win32Exception) { }
        }
        private void Remove(IntPtr host)
        {
            DockStrip strip;
            if (!strips.TryGetValue(host, out strip)) return;
            uint processId; processIds.TryGetValue(host, out processId); processIds.Remove(host);
            strips.Remove(host);
            if (!strip.IsDisposed) strip.Close();
            Process process; if (processes.TryGetValue(host, out process)) { processes.Remove(host); process.Dispose(); }
            if (attached && strips.Count == 0 && !closing)
            {
                if (updateRequested(processId))
                {
                    // Only an explicit, recent update intent keeps this existing companion alive.
                    // Window events attach the replacement; this deadline never polls the machine.
                    waitingForUpdateRestart = true; updateRestartDeadline.Start();
                }
                else ExitThread();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !closing) ExitThreadCore();
            base.Dispose(disposing);
        }
        protected override void ExitThreadCore()
        {
            if (!closing)
            {
                closing = true; deadline.Stop(); deadline.Dispose();
                placement.Stop(); placement.Dispose(); pendingPlacement.Clear();
                updateRestartDeadline.Stop(); updateRestartDeadline.Dispose(); processIds.Clear();
                foreach (IntPtr hook in hooks) if (hook != IntPtr.Zero) ClientWindows.UnhookWinEvent(hook);
                foreach (var strip in strips.Values.ToArray()) if (!strip.IsDisposed) strip.Close();
                foreach (var process in processes.Values) process.Dispose();
                dispatcher.Dispose();
            }
            base.ExitThreadCore();
        }
    }
}
