using Microsoft.JSInterop;
using Solnet.KeyStore;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Solnet.Wallet.Utilities;
using Solnet.Wallet;

namespace dkgWebNode.Services
{
    public class KeystoreService(IJSRuntime jsRuntime, ILogger<KeystoreService> logger)
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private readonly string _keyStoreKey = "KeyStore";
        private readonly SecretKeyStoreService _secretKeyStoreService = new();
        private readonly ILogger<KeystoreService> _logger = logger;

        public async Task<string?> GetJsonString()
        {
            string? keystoreString = await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", _keyStoreKey);
            if (keystoreString is not null)
            {
                try
                {
                    byte[] keystoreDataBytes = Convert.FromBase64String(keystoreString);
                    keystoreString = Encoding.UTF8.GetString(keystoreDataBytes);
                }
                catch
                {
                }
            }

            return keystoreString;
        }
        public async Task Save(string password, byte[] privateKeyBytes, string publicKey)
        {
             string keystoreString = _secretKeyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(password, privateKeyBytes, publicKey);
             byte[] keystoreDataBytes = Encoding.UTF8.GetBytes(keystoreString);
             keystoreString = Convert.ToBase64String(keystoreDataBytes);
             await _jsRuntime.InvokeVoidAsync("saveToLocalStorage", _keyStoreKey, keystoreString);
        }

        public async Task<(string?, string?)> Load(string password)
        {
            string? keystoreString = await GetJsonString();
            string? solanaAddress = null;
            string? solanaPrivateKey = null;
            if (keystoreString is not null)
            {
                try
                {
                        JsonDocument jsonDocument = JsonDocument.Parse(keystoreString);
                        solanaAddress = jsonDocument.RootElement.GetProperty("address").GetString();

                        if (solanaAddress is not null)
                        {
                           byte[] keystoreDataBytes = _secretKeyStoreService.DecryptKeyStoreFromJson(password, keystoreString);
                           solanaPrivateKey = Encoders.Base58.EncodeData(keystoreDataBytes);
                        }
                    }
                catch
                {
                }
            }
            _logger.LogDebug("Loaded keystore with private key '{key}', address '{address}'", solanaPrivateKey, solanaAddress);
            return (solanaPrivateKey, solanaAddress);
        }

        public async Task<string?> LoadAddress()
        {
            string? keystoreString = await GetJsonString();
            string? solanaAddress = null;
            if (keystoreString is not null)
            {
                try
                {
                    JsonDocument jsonDocument = JsonDocument.Parse(keystoreString);
                    solanaAddress = jsonDocument.RootElement.GetProperty("address").GetString();
                }
                catch
                {
                }
            }
            return solanaAddress;
        }
        public (string?, string?, byte[]?) Import(string keystore, string password)
        {
            string? solanaAddress = null;
            string? solanaPrivateKey = null;
            byte[]? keystoreDataBytes = null;
            try
            {
                JsonDocument jsonDocument = JsonDocument.Parse(keystore);
                solanaAddress = jsonDocument.RootElement.GetProperty("address").GetString();

                if (solanaAddress is not null)
                {
                   keystoreDataBytes = _secretKeyStoreService.DecryptKeyStoreFromJson(password, keystore);
                   solanaPrivateKey = Encoders.Base58.EncodeData(keystoreDataBytes);
                }
            }
            catch
            {
            }
            return (solanaPrivateKey, solanaAddress, keystoreDataBytes);
        }

        public async void Clear()
        {
            await _jsRuntime.InvokeVoidAsync("clearLocalStorage", _keyStoreKey);
            NotifyStateCleared();
        }

        public async Task<bool> Exists()
        {
            return await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", _keyStoreKey) != null;
        }

        public event Action? OnClear;
        private void NotifyStateCleared() => OnClear?.Invoke();
    }
}
