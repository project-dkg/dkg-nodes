// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of dkg service node
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Solnet.KeyStore;
using Solnet.Wallet.Bip39;
using Solnet.Wallet;
using System.Text;
using System.Text.Json;

namespace dkgNode.Services
{
    public static class KeyStoreService
    {
        private static readonly JsonSerializerOptions _jops = new () { WriteIndented = true };
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
                        nodeSection = JsonSerializer.Deserialize<Dictionary<string, object>>(nodeSectionElement.GetRawText()) ?? [];
                    }
                    else
                    {
                        nodeSection = [];
                    }

                    nodeSection[keyStorePropertyName] = newKeyStore;
                    var nodeSectionJson = JsonSerializer.Serialize(nodeSection, _jops);
                    cfg[nodeSectionName] = JsonSerializer.Deserialize<JsonElement>(nodeSectionJson);

                    var modifiedJson = JsonSerializer.Serialize(cfg, _jops);
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
