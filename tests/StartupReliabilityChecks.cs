using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace CodexQuotaWidget
{
    internal static class StartupReliabilityChecks
    {
        private static readonly List<string> checks=new List<string>();
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);checks.Add(label);}
        private static async Task Until(Func<bool> ready,int timeout=3500)
        {var watch=Stopwatch.StartNew();while(!ready()){if(watch.ElapsedMilliseconds>timeout)throw new TimeoutException("Fixture condition timed out.");await Task.Delay(20);}}
        private static Process Holder(string name,string ready,int duration,bool abandon)
        {
            var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--hold "+name+" \""+ready+"\" "+duration+" "+abandon){UseShellExecute=false,CreateNoWindow=true};
            var child=Process.Start(start);var wait=Stopwatch.StartNew();
            while(!File.Exists(ready)){if(wait.ElapsedMilliseconds>3000)throw new TimeoutException("Mutex holder startup failed.");Thread.Sleep(10);}
            return child;
        }
        private static void MutexChecks(string root)
        {
            foreach(bool abandon in new[]{false,true})
            {
                string name="Local\\CodexQuotaFixture_"+Guid.NewGuid().ToString("N");
                using(var child=Holder(name,Path.Combine(root,Guid.NewGuid()+".ready"),700,abandon))
                using(var mutex=new Mutex(false,name))
                {
                    Check(!mutex.WaitOne(0),"old zero-wait startup would reject a closing instance (abandon="+abandon+")");
                    var elapsed=Stopwatch.StartNew();bool acquired=StartupGate.Acquire(mutex,2500);
                    Check(acquired&&elapsed.ElapsedMilliseconds>=250,"new startup waits for prior process and takes over (abandon="+abandon+")");
                    if(acquired)mutex.ReleaseMutex();Check(child.WaitForExit(2000),"prior instance exits cleanly from fixture (abandon="+abandon+")");
                }
            }
            string heldName="Local\\CodexQuotaFixture_"+Guid.NewGuid().ToString("N");
            using(var child=Holder(heldName,Path.Combine(root,Guid.NewGuid()+".ready"),650,false))
            using(var mutex=new Mutex(false,heldName))
            {
                var elapsed=Stopwatch.StartNew();Check(!StartupGate.Acquire(mutex,100),"healthy existing instance prevents a duplicate");
                Check(elapsed.ElapsedMilliseconds<500,"duplicate launch wait is bounded");Check(child.WaitForExit(2000),"healthy fixture releases its mutex");
            }
        }
        [STAThread] private static int Main(string[] args)
        {
            if(args.Length>0&&args[0]=="--hold")
            {
                using(var mutex=new Mutex(false,args[1]))
                {
                    mutex.WaitOne();File.WriteAllText(args[2],"ready");Thread.Sleep(Int32.Parse(args[3]));
                    if(Boolean.Parse(args[4]))Environment.Exit(0);mutex.ReleaseMutex();
                }
                return 0;
            }
            string report=args[0],root=Path.GetDirectoryName(report);Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("CODEX_HOME",Path.Combine(root,"empty-codex-profile"));
            Environment.SetEnvironmentVariable("CODEX_QUOTA_DATA_DIR",Path.Combine(root,"private-fixture-data"));
            Exception failed=null;
            try{MutexChecks(root);}catch(Exception error){failed=error;}
            if(failed==null)
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                var host=new Form{Text="额度组件启动可靠性检查（示例窗口）",ClientSize=new Size(800,520),BackColor=Color.FromArgb(15,22,31)};
                host.Controls.Add(new Label{Text="仅测试本程序窗口；不关闭客户端、不查询账号。",ForeColor=Color.White,AutoSize=true,Location=new Point(24,24)});
                host.Shown+=async delegate
                {
                    DockContext context=null;
                    try
                    {
                        var made=new List<DockStrip>();int attempts=0;
                        context=new DockContext(false,new IntPtr[0],p=>false,h=>h==host.Handle,1000,
                            h=>{var strip=new DockStrip(h,false);made.Add(strip);return strip;},
                            s=>{attempts++;if(attempts==1)throw new InvalidOperationException("Simulated host owner not ready.");s.Attach();});
                        context.Add(host.Handle);
                        Check(context.Active&&context.AttachedCount==0&&context.PendingAttachCount==1,"transient native owner failure stays alive and queues retry");
                        Check(made[0].IsDisposed,"failed attachment disposes its controller and resources");
                        await Until(()=>context.AttachedCount==1);
                        Check(attempts==2&&!made[1].IsDisposed,"native attachment recovers on next bounded attempt");
                        Check(!context.AttachRetryActive&&context.PendingAttachCount==0,"retry timer stops after success");
                        Check(ClientWindows.GetWindow(made[1].Handle,4)==host.Handle,"recovered controller is actually owned by fixture window");
                        context.Dispose();Check(made[1].IsDisposed&&!context.Active,"normal shutdown releases recovered surfaces");context=null;

                        made.Clear();attempts=0;
                        context=new DockContext(false,new IntPtr[0],p=>false,h=>h==host.Handle,1000,
                            h=>{var strip=new DockStrip(h,false);made.Add(strip);return strip;},
                            s=>{attempts++;throw new System.ComponentModel.Win32Exception(5);});
                        context.Add(host.Handle);await Until(()=>attempts==10);
                        Check(context.PendingAttachCount==0&&!context.AttachRetryActive,"permanent native failure stops at ten attempts");
                        context.Add(host.Handle);await Task.Delay(300);Check(attempts==10,"later window events do not restart an exhausted retry loop");
                        Check(made.TrueForAll(s=>s.IsDisposed),"every failed surface is disposed");context.Dispose();context=null;

                        int created=0;
                        context=new DockContext(false,new IntPtr[0],p=>false,h=>h==host.Handle,1000,
                            h=>{created++;return new DockStrip(h,false);},
                            s=>{context.Add(host.Handle);s.Attach();});
                        context.Add(host.Handle);Check(created==1&&context.AttachedCount==1,"reentrant window events cannot create duplicate controllers");
                        context.Dispose();context=null;

                        using(var transient=new Form{Text="关闭中的示例窗口",ClientSize=new Size(650,460)})
                        {
                            transient.Show();IntPtr handle=transient.Handle;DockStrip discarded=null;
                            context=new DockContext(false,new IntPtr[0],p=>false,h=>h==handle,1000,
                                h=>discarded=new DockStrip(h,false),s=>{transient.Close();throw new InvalidOperationException("Host disappeared.");});
                            context.Add(handle);
                            Check(discarded.IsDisposed&&context.PendingAttachCount==0&&!context.AttachRetryActive,"window destroyed during attach does not crash or keep retrying");
                            context.Dispose();context=null;
                        }
                    }
                    catch(Exception error){failed=error;}
                    finally{if(context!=null)context.Dispose();host.Close();}
                };
                Application.Run(host);host.Dispose();
            }
            File.WriteAllText(report,Data.Json.Serialize(new{ok=failed==null,checks=checks,error=failed==null?null:failed.ToString(),fixtureOnly=true,accountQueries=0}));
            return failed==null?0:1;
        }
    }
}
