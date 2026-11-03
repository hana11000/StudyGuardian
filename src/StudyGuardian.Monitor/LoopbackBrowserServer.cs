using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudyGuardian.Core;

namespace StudyGuardian.Monitor;

public sealed record BrowserRequest(string Path, JsonElement Body);
public sealed class LoopbackBrowserServer : IAsyncDisposable
{
    private readonly BrowserOptions _options;
    private readonly Func<string> _token;
    private readonly Func<BrowserRequest,CancellationToken,Task<object>> _handler;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _slots;
    private readonly HashSet<Task> _connections = [];
    private readonly object _gate = new();
    private Task? _accept;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth=16 };
    public LoopbackBrowserServer(BrowserOptions options, Func<string> token, Func<BrowserRequest,CancellationToken,Task<object>> handler)
    {
        options.Validate(); _options=options; _token=token; _handler=handler;
        _listener=new(IPAddress.Loopback,options.Port); _listener.Server.ExclusiveAddressUse=true;
        _slots=new(options.MaximumConnections);
    }
    public void Start()
    {
        if(_accept!=null) return;
        _listener.Start(_options.MaximumConnections);
        _accept=AcceptAsync();
    }
    private async Task AcceptAsync()
    {
        try
        {
            while(!_stop.IsCancellationRequested)
            {
                var client=await _listener.AcceptTcpClientAsync(_stop.Token);
                if(!_slots.Wait(0)) { client.Dispose(); continue; }
                var task=ServeAsync(client);
                lock(_gate) _connections.Add(task);
                _=task.ContinueWith(t=> { lock(_gate) _connections.Remove(t); _slots.Release(); },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            }
        }
        catch(OperationCanceledException) when(_stop.IsCancellationRequested) { }
        catch(SocketException) when(_stop.IsCancellationRequested) { }
    }
    private sealed class Rejected(int status) : Exception { public int Status { get; }=status; }
    private async Task ServeAsync(TcpClient client)
    {
        using(client)
        using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
        {
            timeout.CancelAfter(_options.RequestTimeoutMilliseconds);
            var ct=timeout.Token;
            await using var stream=client.GetStream();
            string? origin=null;
            try
            {
                var header=new List<byte>(); var one=new byte[1];
                while(true)
                {
                    if(header.Count >= _options.MaximumHeaderBytes) throw new Rejected(431);
                    if(await stream.ReadAsync(one,ct)==0) return;
                    if(one[0]>127 || one[0]==0) throw new Rejected(400);
                    header.Add(one[0]);
                    if(header.Count>=4 && header[^4]==13 && header[^3]==10 && header[^2]==13 && header[^1]==10) break;
                }
                var lines=Encoding.ASCII.GetString(header.ToArray()).Split("\r\n",StringSplitOptions.None);
                var first=lines[0].Split(' ');
                if(first.Length!=3 || first[2]!="HTTP/1.1") throw new Rejected(400);
                var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                foreach(var line in lines.Skip(1).Where(l=>l.Length>0))
                {
                    var separator=line.IndexOf(':');
                    if(separator<=0 || char.IsWhiteSpace(line[0]) || !headers.TryAdd(line[..separator],line[(separator+1)..].Trim())) throw new Rejected(400);
                }
                if(!headers.TryGetValue("Host",out var host) || host!=$"127.0.0.1:{_options.Port}") throw new Rejected(403);
                if(headers.TryGetValue("Origin",out var suppliedOrigin))
                {
                    if(!Uri.TryCreate(suppliedOrigin,UriKind.Absolute,out var uri) || uri.Scheme!="chrome-extension" || uri.Host.Length!=32 ||
                        !uri.Host.All(c=>c is >= 'a' and <= 'p') || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath!="/" || uri.Query.Length>0 || uri.Fragment.Length>0 || !uri.IsDefaultPort) throw new Rejected(403);
                    origin=suppliedOrigin;
                }
                var method=first[0]; var path=first[1];
                if(path is not ("/status" or "/activity" or "/command" or "/return")) throw new Rejected(404);
                if(method=="OPTIONS" && origin!=null) { await RespondAsync(stream,204,new{},origin,ct); return; }
                if((path=="/status" && method!="GET") || (path!="/status" && method!="POST")) throw new Rejected(405);
                var expected="Bearer "+_token();
                if(!headers.TryGetValue("Authorization",out var authorization) ||
                    !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(authorization)))) throw new Rejected(401);
                if(headers.ContainsKey("Transfer-Encoding") || headers.ContainsKey("Expect")) throw new Rejected(400);
                var length=0;
                if(headers.TryGetValue("Content-Length",out var textLength) && (!int.TryParse(textLength,NumberStyles.None,CultureInfo.InvariantCulture,out length) || length<0)) throw new Rejected(400);
                if(length>_options.MaximumBodyBytes) throw new Rejected(413);
                if(method=="POST" && (length==0 || !headers.TryGetValue("Content-Type",out var contentType) || !contentType.Split(';')[0].Trim().Equals("application/json",StringComparison.OrdinalIgnoreCase))) throw new Rejected(400);
                if(method=="GET" && length!=0) throw new Rejected(400);
                var body=new byte[length]; await stream.ReadExactlyAsync(body,ct);
                using var document=JsonDocument.Parse(length==0 ? "{}"u8.ToArray() : body,new JsonDocumentOptions {MaxDepth=16});
                if(document.RootElement.ValueKind!=JsonValueKind.Object) throw new Rejected(400);
                var result=await _handler(new(path,document.RootElement.Clone()),ct).WaitAsync(ct);
                await RespondAsync(stream,200,result,origin,ct);
            }
            catch(Rejected e) { await TryErrorAsync(client,stream,e.Status,origin,ct); }
            catch(JsonException) { await TryErrorAsync(client,stream,400,origin,ct); }
            catch(Exception e) when(e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            catch(Exception) { await TryErrorAsync(client,stream,500,origin,ct); }
        }
    }
    private async Task TryErrorAsync(TcpClient client,NetworkStream stream,int status,string? origin,CancellationToken ct)
    {
        try
        {
            await RespondAsync(stream,status,new{error="request_rejected"},origin,ct);
            // Closing with unread request bytes sends RST and can discard the response on Windows.
            client.Client.Shutdown(SocketShutdown.Send);
            var drain=new byte[_options.MaximumBodyBytes];
            var remaining=(long)_options.MaximumBodyBytes+_options.MaximumHeaderBytes;
            while(remaining>0)
            { var read=await stream.ReadAsync(drain.AsMemory(0,(int)Math.Min(remaining,drain.Length)),ct); if(read==0) break; remaining-=read; }
        }
        catch(Exception e) when(e is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
    }
    private static async Task RespondAsync(NetworkStream stream,int status,object value,string? origin,CancellationToken ct)
    {
        var body=status==204 ? [] : JsonSerializer.SerializeToUtf8Bytes(value,Json);
        var cors=origin==null ? "" : $"Access-Control-Allow-Origin: {origin}\r\nAccess-Control-Allow-Headers: Authorization, Content-Type\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nVary: Origin\r\n";
        var header=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Response\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n{cors}\r\n");
        await stream.WriteAsync(header,ct); await stream.WriteAsync(body,ct);
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); _listener.Stop();
        if(_accept!=null) await _accept;
        Task[] pending; lock(_gate) pending=_connections.ToArray();
        await Task.WhenAll(pending);
        // Continuations release the slots; keep the semaphore alive until they finish.
    }
}
