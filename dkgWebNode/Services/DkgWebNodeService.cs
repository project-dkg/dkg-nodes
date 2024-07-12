using Microsoft.JSInterop;

namespace dkgWebNode.Services
{
    public class DkgWebNodeService
    {
        private readonly IJSRuntime _jsRuntime;
        private string? _publicKey = null;
        private string? _privateKey = null;
        private string? _address() => _publicKey;

        public DkgWebNodeService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        internal async Task LogToConsoleAsync(string message)
        {
            await _jsRuntime.InvokeVoidAsync("console.log", message);
        }

        public void SetKeys(string publicKey, string privateKey)
        {
            _privateKey = privateKey;
            _publicKey = publicKey;
            NotifyStateChanged();
        }
        public void ClearKeys()
        {
            _privateKey = null;
            _publicKey = null;
            NotifyStateChanged();
        }
        public bool HasKeys()
        {
            return _publicKey is not null && _privateKey is not null;
        }

        public bool IsAuthorized()
        {
            return false;
        }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
