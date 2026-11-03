using System.Security.Cryptography;
using StudyGuardian.Core;

namespace StudyGuardian.Monitor;

public sealed class BrowserPairingStore
{
    private readonly ProtectedSecretStore _store;
    private readonly int _bytes;
    private string? _token;
    public BrowserPairingStore(string dataDirectory,BrowserOptions options)
    { _store=new(Path.Combine(dataDirectory,"browser-token.dpapi")); _bytes=options.TokenBytes; }
    public string ReadOrCreate()
    {
        lock(_store)
        {
            if(_token!=null) return _token;
            _token=_store.Read();
            if(string.IsNullOrEmpty(_token)) return Reset();
            return _token;
        }
    }
    public string Reset()
    {
        lock(_store)
        {
            var next=Convert.ToHexString(RandomNumberGenerator.GetBytes(_bytes));
            _store.Save(next); _token=next; return next;
        }
    }
}
