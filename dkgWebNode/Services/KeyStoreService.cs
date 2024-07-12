using Microsoft.JSInterop;
using Solnet.KeyStore;
using System.Text;
using MudBlazor;
using Solnet.Wallet;
using Org.BouncyCastle.Security.Certificates;
using Microsoft.Extensions.Logging;
using Solnet.KeyStore.Model;
using Solnet.KeyStore.Services;
using System.Text.Json;
using Solnet.Wallet.Utilities;

namespace dkgWebNode.Services
{
    public class KeystoreService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly string _keyStoreKey = "KeyStore";
        private SecretKeyStoreService _secretKeyStoreService = new SecretKeyStoreService();
        public KeystoreService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task Save(string password, byte[] privateKeyBytes, string publicKey)
        {
            // EncryptAndGenerateDefaultKeyStoreAsJson в wasm непередаваемо медленно работает
            // Поэтому вместо этого нужно что-то другое придумать
            // string keystoreString = _secretKeyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(password, privateKeyBytes, publicKey);
            // byte[] keystoreDataBytes = Encoding.UTF8.GetBytes(keystoreString);
            // keystoreString = Convert.ToBase64String(keystoreDataBytes);

            // Пока что просто сохраняем secret key

            string keystoreString = new Base58Encoder().EncodeData(privateKeyBytes);
            await _jsRuntime.InvokeVoidAsync("saveToLocalStorage", _keyStoreKey, keystoreString);
        }

        public async Task<(string?, string?)> Load(string password)
        {
            string? keystoreString = await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", _keyStoreKey);
            string? solanaAddress = null;
            string? solanaPrivateKey = null;
            if (keystoreString is not null)
            {
                try
                {
                        //        byte[] keystoreDataBytes = Convert.FromBase64String(keystoreString);
                        //        keystoreString = Encoding.UTF8.GetString(keystoreDataBytes);

                        //        JsonDocument jsonDocument = JsonDocument.Parse(keystoreString);
                        //        solanaAddress = jsonDocument.RootElement.GetProperty("address").GetString();

                        //       if (solanaAddress is null)
                        //       {
                        //           return (null, null);                       
                        //       }
                        //       else
                        //       {
                        //           keystoreDataBytes = _secretKeyStoreService.DecryptKeyStoreFromJson(password, keystoreString);
                        //           solanaPrivateKey = Solnet.Wallet.Utilities.Encoders.Base58.EncodeData(keystoreDataBytes);
                        //       }
                        Account account = Account.FromSecretKey(keystoreString);
                        solanaPrivateKey = account.PrivateKey.Key;
                        solanaAddress = account.PublicKey.Key;
                    }
                    catch
                {
                }
            }
            return (solanaPrivateKey, solanaAddress);
        }

        public async Task<string?> LoadAddress()
        {
            string? keystoreString = await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", _keyStoreKey);
            string? solanaAddress = null;
            if (keystoreString is not null)
            {
                try
                {
                    //        byte[] keystoreDataBytes = Convert.FromBase64String(keystoreString);
                    //        keystoreString = Encoding.UTF8.GetString(keystoreDataBytes);

                    //        JsonDocument jsonDocument = JsonDocument.Parse(keystoreString);
                    //        solanaAddress = jsonDocument.RootElement.GetProperty("address").GetString();

                    string solanaSecretKey = keystoreString;
                    Account account = Account.FromSecretKey(solanaSecretKey);
                    solanaAddress = account.PublicKey.Key;
                }
                catch
                {
                }
            }
            return solanaAddress;
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
