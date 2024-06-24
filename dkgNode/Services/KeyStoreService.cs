using Solnet.KeyStore.Model;
using Solnet.KeyStore;
using Solnet.Wallet.Bip39;
using Solnet.Wallet;
using System.Text;
using System.Text.Json;

namespace dkgNode.Services
{
    public static class KeyStoreService
    {
        public static (string, string, string?) DecodeOrCreate(string? keyStore, string keyStorePwd, ILogger logger)
        {
            string? solanaAddress = null;
            string? solanaPrivateKey = null;

            byte[] keyStoreDataBytes;
            string keyStoreString;

            string? newKeyStore = null;

            var secretKeyStoreService = new SecretKeyStoreService();

            if (keyStore is not null)
            {
                try
                {

                    keyStoreDataBytes = Convert.FromBase64String(keyStore);
                    keyStoreString = Encoding.UTF8.GetString(keyStoreDataBytes);

                    JsonDocument jsonDocument = JsonDocument.Parse(keyStoreString);
                    solanaAddress = jsonDocument.RootElement.GetProperty("address").GetString();

                    if (solanaAddress is null)
                    {
                        logger.LogWarning("Failed to determine solana address from key store, creating a new one.");
                    }
                    else
                    {
                        keyStoreDataBytes = secretKeyStoreService.DecryptKeyStoreFromJson(keyStorePwd, keyStoreString);
                        solanaPrivateKey = Encoding.UTF8.GetString(keyStoreDataBytes);
                        logger.LogInformation("Using Solana Address: {solanaAddress}", solanaAddress);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Failed to decode key store, creating a new one.\n{msg}", ex.Message);
                }
            }

            if (solanaAddress is null || solanaPrivateKey is null)
            {
                var mnemonic = new Mnemonic(WordList.English, WordCount.Twelve);
                var wallet = new Wallet(mnemonic);

                Account account = wallet.Account;

                // The public key of the account is the Solana address
                solanaAddress = account.PublicKey.Key;
                solanaPrivateKey = account.PrivateKey.Key;
                logger.LogWarning("**** Created solana account, please use it for testing only ****\nMnemonic: {mnemonic}\nAddress: {solanaAddress}\nPrivate key: {solanaPrivateKey}",
                    mnemonic,
                    solanaAddress,
                solanaPrivateKey);

                keyStoreDataBytes = Encoding.UTF8.GetBytes(solanaPrivateKey);
                keyStoreString = secretKeyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson("", keyStoreDataBytes, solanaAddress);
                keyStoreDataBytes = Encoding.UTF8.GetBytes(keyStoreString);
                keyStoreString = Convert.ToBase64String(keyStoreDataBytes);
                newKeyStore = keyStoreString;
            }

            return (solanaAddress, solanaPrivateKey, newKeyStore);
        }

        public static void UpdateAppsettingsJson(string newKeyStore, ILogger logger)
        {
            try
            {
                const string appSettingsPath = "appsettings.json";
                const string nodeSectionName = "Node";
                const string keyStorePropertyName = "KeyStore";

                var json = File.ReadAllText(appSettingsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement.Clone();

                var cfg = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetRawText());

                if (cfg is not null)
                {
                    Dictionary<string, object> nodeSection;

                    if (cfg.TryGetValue(nodeSectionName, out object? nodeSectionObj) && nodeSectionObj is JsonElement nodeSectionElement)
                    {
                        nodeSection = JsonSerializer.Deserialize<Dictionary<string, object>>(nodeSectionElement.GetRawText()) ?? new Dictionary<string, object>();
                    }
                    else
                    {
                        nodeSection = new Dictionary<string, object>();
                    }

                    nodeSection[keyStorePropertyName] = newKeyStore;
                    var nodeSectionJson = JsonSerializer.Serialize(nodeSection, new JsonSerializerOptions { WriteIndented = true });
                    cfg[nodeSectionName] = JsonSerializer.Deserialize<JsonElement>(nodeSectionJson);

                    var modifiedJson = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(appSettingsPath, modifiedJson);
                    logger.LogInformation("Saved key store to {appSettingsPath}:\n{keyStoreString}", appSettingsPath, newKeyStore);
                }
                else
                {
                   logger.LogWarning("Failed to save key store: configuration is null.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to save key store:\n{msg}", ex.Message);
            }

        }
    }
}
