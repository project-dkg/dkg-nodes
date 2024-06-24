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

using dkgNode.Models;
using dkgNode.Services;
using Solnet.Wallet;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

// Set up configuration sources
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

builder.Services.AddSingleton<IConfiguration>(configuration);

builder.Services.AddHostedService(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<DkgNodeService>>();

    var configSection = configuration.GetSection("Node");
    int pollingInterval = configSection.GetValue("PollingInterval", configuration.GetValue<int?>("DKG_NODE_POLLING_INTERVAL") ?? 5000);

    string serviceNodeUrlDefault = configuration.GetValue<string?>("DKG_SERVICE_NODE_URL") ?? "https://localhost:8081";
    string serviceNodeUrl = configSection.GetValue<string?>("ServiceNodeUrl") ?? serviceNodeUrlDefault;

    string? niceName = configSection.GetValue("Name", configuration.GetValue<string?>("DKG_NODE_NAME"));

    string? keyStoreDefault = configuration.GetValue<string?>("DKG_SOLANA_KEYSTORE");
    string? keyStore = configSection.GetValue<string?>("KeyStore") ?? keyStoreDefault;

    string keyStorePwdDefault = configuration.GetValue<string?>("DKG_SOLANA_KEYSTORE_PWD") ?? "";
    string keyStorePwd = configSection.GetValue<string?>("KeyStorePwd") ?? keyStorePwdDefault;

    string? solanaAddress = null;
    string? solanaPrivateKey = null;

    string? newKeyStore;

    (solanaAddress, solanaPrivateKey, newKeyStore) = KeyStoreService.DecodeOrCreate(keyStore, keyStorePwd, logger);

    if (newKeyStore is not null)
    {
        KeyStoreService.UpdateAppsettingsJson(newKeyStore, logger);
    }
    var config = new DkgNodeConfig()
    {
        NiceName = niceName,
        PollingInterval = pollingInterval,
        ServiceNodeUrl = serviceNodeUrl,
        Address = solanaAddress,
        SolanaAccount = new Account(solanaPrivateKey, solanaAddress)
    };

    // These are for testing purposes only
    // Use environment variables, appsettings.json won't work
    string ? dieOnStep2 = configuration.GetValue<string?>("DKG_NODE_DIE_ON_STEP_TWO");
    string? dieOnStep3 = configuration.GetValue<string?>("DKG_NODE_DIE_ON_STEP_THREE");

    return new dkgNode.DkgNodeWorker(config, logger, dieOnStep2 != null, dieOnStep3 != null);
});

var host = builder.Build();
host.Run();
