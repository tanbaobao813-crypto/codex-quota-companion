using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace CodexQuotaWidget
{
    internal sealed class QuotaReadException : IOException
    {
        internal readonly string Code;
        internal readonly bool Retry;
        internal QuotaReadException(string code,bool retry) : base(code) { Code=code;Retry=retry; }
    }
    internal static class LiveQuotaReader
    {
        internal static Quota Parse(object raw)
        {
            var result=raw as IDictionary<string,object>;
            if(result==null)throw new InvalidDataException("额度接口没有返回数据");
            var buckets=Data.Get(result,"rateLimitsByLimitId") as IDictionary<string,object>;
            var limits=(buckets==null?null:Data.Get(buckets,"codex")) as IDictionary<string,object>;
            if(limits==null)limits=Data.Get(result,"rateLimits") as IDictionary<string,object>;
            if(limits==null)throw new InvalidDataException("额度接口暂不可用");
            var credits=Data.Get(limits,"credits") as IDictionary<string,object>;
            return new Quota{Id=Data.Text(Data.Get(limits,"limitId"))??"codex",Name=Data.Text(Data.Get(limits,"limitName")),Plan=Data.Text(Data.Get(limits,"planType")),UpdatedUtc=DateTime.UtcNow,Primary=Data.ReadWindow(Data.Get(limits,"primary")),Secondary=Data.ReadWindow(Data.Get(limits,"secondary")),CreditBalance=Data.Text(Data.Get(credits,"balance")),UnlimitedCredits=Object.Equals(Data.Get(credits,"unlimited"),true)};
        }
        internal static Quota Read(string binary){return Read(binary,CancellationToken.None);}
        internal static Quota Read(string binary,CancellationToken cancel){return ReadWithRetryAsync(binary,cancel).GetAwaiter().GetResult();}
        private static async Task<Quota> ReadWithRetryAsync(string binary,CancellationToken cancel)
        {
            for(int attempt=1;;attempt++)
            {
                try { return await ReadAsync(binary,cancel).ConfigureAwait(false); }
                catch(OperationCanceledException){throw;}
                catch(Exception error)
                {
                    if(error is OutOfMemoryException)throw;
                    var known=error as QuotaReadException;
                    string code=known!=null?known.Code:error is Win32Exception?"startup":error is InvalidDataException?"protocol":error is IOException?"connection":"internal";
                    bool retry=known!=null?known.Retry:error is IOException&&!(error is InvalidDataException);
                    QuotaReadDiagnostics.Failure(code,attempt);
                    if(attempt>=2||!retry)throw new QuotaReadException(code,retry);
                }
                await Task.Delay(1000,cancel).ConfigureAwait(false);
            }
        }
        private static async Task<Quota> ReadAsync(string binary,CancellationToken cancel)
        {
            var json=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};
            using(var process=new Process{StartInfo=new ProcessStartInfo(binary,"app-server --listen stdio:// -c analytics.enabled=false"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}})
            {
                cancel.ThrowIfCancellationRequested();
                process.Start();var stderr=process.StandardError.ReadToEndAsync();
                Func<int,string,object,Task<IDictionary<string,object>>> request=async delegate(int id,string method,object parameters)
                {
                    var message=new Dictionary<string,object>{{"id",id},{"method",method}};if(parameters!=null)message["params"]=parameters;
                    await process.StandardInput.WriteLineAsync(json.Serialize(message)).ConfigureAwait(false);await process.StandardInput.FlushAsync().ConfigureAwait(false);
                    DateTime deadline=DateTime.UtcNow.AddSeconds(method=="initialize"?10:25);
                    while(true)
                    {
                        cancel.ThrowIfCancellationRequested();
                        if(DateTime.UtcNow>=deadline)throw new QuotaReadException("timeout",true);
                        int remaining=(int)Math.Max(1,(deadline-DateTime.UtcNow).TotalMilliseconds);
                        var read=process.StandardOutput.ReadLineAsync();
                        if(await Task.WhenAny(read,Task.Delay(remaining,cancel)).ConfigureAwait(false)!=read){cancel.ThrowIfCancellationRequested();throw new QuotaReadException("timeout",true);}
                        string line=await read.ConfigureAwait(false);if(line==null)throw new QuotaReadException("connection",true);
                        var item=json.DeserializeObject(line) as IDictionary<string,object>;
                        if(item!=null&&item.ContainsKey("id")&&Convert.ToInt32(item["id"])==id)
                        {
                            if(item.ContainsKey("error"))
                            {
                                var rpcError=item["error"] as IDictionary<string,object>;
                                string rpcMessage=(Data.Text(Data.Get(rpcError,"message"))??"").ToLowerInvariant();
                                bool auth=rpcMessage.Contains("unauthorized")||rpcMessage.Contains("authentication")||rpcMessage.Contains("token")||rpcMessage.Contains("login")||rpcMessage.Contains("401");
                                bool protocol=rpcMessage.Contains("not initialized")||rpcMessage.Contains("method not found")||rpcMessage.Contains("invalid params");
                                throw new QuotaReadException(auth?"auth":protocol?"protocol":"service",!auth&&!protocol);
                            }
                            return item;
                        }
                    }
                };
                try
                {
                    await request(1,"initialize",new{clientInfo=new{name="codex_quota_widget",title="Codex Quota Widget",version="0.3.1"}}).ConfigureAwait(false);
                    await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}").ConfigureAwait(false);await process.StandardInput.FlushAsync().ConfigureAwait(false);
                    var reply=await request(2,"account/rateLimits/read",null).ConfigureAwait(false);
                    return Parse(Data.Get(reply,"result"));
                }
                finally
                {
                    try{process.StandardInput.Close();if(cancel.IsCancellationRequested||!process.WaitForExit(2000))process.Kill();process.WaitForExit(2000);if(stderr.IsCompleted)stderr.GetAwaiter().GetResult();}catch(InvalidOperationException){}catch(IOException){}
                }
            }
        }
    }
}
