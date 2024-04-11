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

using dkg.group;
using dkgNode.Models;
using Grpc.Core;
using static dkgNode.DkgNode;
using Channel = Grpc.Core.Channel;

namespace dkgNode.Services
{
    // Узел
    // Создаёт instance gRPC сервера (class DkgNodeServer)
    // и gRPC клиента (это просто отдельный поток TheThread)
    // В TheThread реализована незатейливая логика этого примера
    class DkgNodeServer
    {
        internal int Index { get; }
        internal int Port { get; }
        internal string Host { get; }
        internal int SendTo { get; }
        internal bool IsMisbehaving { get; }
        internal Server GRpcServer { get; }
        internal DkgNodeService DkgNodeSrv { get; }
        //internal DkgNodeConfig[] Configs { get; } = [];

        // gRPC клиенты "в сторону" других участников
        // включая самого себя, чтобы было меньше if'ов
        internal Channel[] Channels { get; set; } = [];
        internal DkgNodeClient[] Clients { get; set; } = [];

        // Публичныке ключи других участников
        internal IPoint[] PublicKeys { get; set; } = [];

        //internal Thread TheThread { get; set; }
        internal bool IsRunning { get; set; } = true;
        internal IGroup G { get; }

        public DkgNodeServer(DkgNodeConfig config, ILogger logger)
        {
            G = new Secp256k1Group();

            Port = config.Port;
            Host = config.Host;

            logger.LogInformation($"Starting DkgNode Host: {Host}, Port: {Port}");

            DkgNodeSrv = new DkgNodeService(logger, config.Name, G);

            GRpcServer = new Server
            {
                Services = { BindService(DkgNodeSrv) },
                Ports = { new ServerPort(Host, Port, ServerCredentials.Insecure) }
            };
        }
    }
}