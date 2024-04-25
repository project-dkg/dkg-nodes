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


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<DkgNodeService>>();
    string? ps = Environment.GetEnvironmentVariable("DKG_NODE_POLLING_INTERVAL");
    int pollingInterval = 3000;
    if (ps != null)
    {
        try
        {
            pollingInterval = int.Parse(ps);
        }
        catch
        {
            logger.LogWarning("DKG_NODE_POLLING_INTERVAL must be an integer, got {ps}", ps);
        }
    }

    Guid guid = Guid.NewGuid(); 
    string? gd = Environment.GetEnvironmentVariable("DKG_NODE_GUID");
    if (gd != null)
    {
        try
        {
            guid = new(gd);
        }
        catch
        {
            logger.LogWarning("DKG_NODE_GUID must be a balid GUID, got {gd}", gd);
        }
    }

    var config = new DkgNodeConfig()
    {
        NiceName = Environment.GetEnvironmentVariable("DKG_NODE_NAME"),
        PollingInterval = pollingInterval,
        ServiceNodeUrl = Environment.GetEnvironmentVariable("DKG_SERVICE_NODE_URL") ?? "https://localhost:8081",
        Gd = guid
    };

    return new dkgNode.DkgNodeWorker(config, logger);
});

var host = builder.Build();
host.Run();
