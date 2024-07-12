using Microsoft.JSInterop;

namespace dkgWebNode.Services
{
    public class DkgWebNodeService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly string _keyStoreKey = "KeyStore";
        private string? _keyStore = null;

        public DkgWebNodeService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task Initialize()
        {
            await LogToConsoleAsync("Initializing DkgWebNodeService ...");
            _keyStore = await GetValueFromLocalStorageAsync(_keyStoreKey);
        }

        internal async Task LogToConsoleAsync(string message)
        {
            await _jsRuntime.InvokeVoidAsync("console.log", message);
        }

        private async Task<string?> GetValueFromLocalStorageAsync(string key)
        {
            return await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", key);
        }

        public async Task SaveDataOnShutdown()
        {
            if (_keyStore is not null)
            {
                await _jsRuntime.InvokeVoidAsync("saveToLocalStorage", _keyStoreKey, _keyStore);
            }
        }

        public async Task<bool> ImportKeystore(string keystore, string password)
        {
            _keyStore = keystore;
            return true;
        }
        public bool HasKeystore()
        {
            return _keyStore is not null;
        }

        public bool IsAuthorized()
        {
            return false;
        }

    }
}
