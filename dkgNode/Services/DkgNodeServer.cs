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
using System.Text.Json;
using System.Text;
using dkgNode.Constants;
using static dkgNode.Constants.NStatus;

using static dkgCommon.DkgNode;

using dkgCommon.Models;
using dkg.share;
using dkg;
using dkgCommon;
using Google.Protobuf;
using Grpc.Net.Client;

namespace dkgNode.Services
{
    // Узел
    // Создаёт instance gRPC сервера (class DkgNodeServer)
    // и gRPC клиента (это просто отдельный поток TheThread)
    // В TheThread реализована незатейливая логика этого примера
    class DkgNodeServer
    {
        internal JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
        internal Server GRpcServer { get; }
        internal DkgNodeService DkgNodeSrv { get; }

        // Публичныке ключи других участников
        internal IPoint[] PublicKeys { get; set; } = [];

        internal Thread RunnerThread { get; set; }
        internal bool IsRunning { get; set; } = true;

        internal bool ContinueDkg
        {
            get { return Status == Running && IsRunning;  }
        }
        internal NStatus Status
        {
            get { return DkgNodeSrv.GetStatus(); }
            set { DkgNodeSrv.SetStatus(value); }
        }
        internal IPoint? DistributedPublicKey
        {
            get { return DkgNodeSrv.GetDistributedPublicKey(); }
            set { DkgNodeSrv.SetDistributedPublicKey(value); }
        }
        internal IGroup G { get; }

        internal ILogger Logger { get; }
        internal string ServiceNodeUrl { get; }
        DkgNodeConfig Config { get; }
        DkgNodeConfig[] Configs
        {
            get { return DkgNodeSrv.Configs;  }
        }
        public byte[] PublicKey
        {
            get { return DkgNodeSrv.PublicKey.GetBytes(); }
        }

        public string Name
        {
            get { return DkgNodeSrv.Name; }
        }

        internal async Task<int?> Register(HttpClient httpClient)
        {
            int? roundId = null;
            HttpResponseMessage? response = null;
            var jsonPayload = JsonSerializer.Serialize(Config);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                response = await httpClient.PostAsync(ServiceNodeUrl + "/api/nodes/register", httpContent);
            }
            catch (Exception e)
            {
                Logger.LogError($"Node '{Config.Name}' failed to register with {ServiceNodeUrl}, Exception: {e.Message}");
            }
            if (response == null)
            {
                Logger.LogError($"Node '{Config.Name}' failed to register with {ServiceNodeUrl}, no response received");
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        Reference? reference = JsonSerializer.Deserialize<Reference>(responseContent, JsonSerializerOptions);
                        if (reference == null)
                        {
                            Logger.LogError($"Node '{Config.Name}' failed to parse service node response '{responseContent}' from {ServiceNodeUrl}");
                        }
                        else
                        {
                            if (reference.Id == 0)
                            {
                                roundId = null;
                                Logger.LogInformation($"Node '{Config.Name}' not registered with {ServiceNodeUrl} - no rounds");
                            }
                            else
                            {
                                roundId = reference.Id;
                                Logger.LogInformation($"Node '{Config.Name}' succesfully registered with {ServiceNodeUrl}");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Logger.LogError($"Node '{Config.Name}' failed to parse service node response '{responseContent}' from {ServiceNodeUrl}");
                        Logger.LogError(ex.Message);
                    }
                }
                else
                {
                    Logger.LogError($"Node '{Config.Name}' failed to register with {ServiceNodeUrl}: {response.StatusCode}");
                    Logger.LogError(responseContent);
                }
            }
            return roundId;
        }

        internal async void Runner()
        {
            var httpClient = new HttpClient();
            int j = 0;
            while (IsRunning)
            {
                if (Status == NotRegistered)
                {
                    int? roundId = await Register(httpClient);
                    if (roundId != null)
                    {
                        DkgNodeSrv.SetStatusAndRound(WaitingRoundStart, (int)roundId);
                    }
                }

                if (Status == Running)
                {
                    RunDkg();
                }
                else
                {
                    if (j++ % 30 == 0)
                    {
                        Logger.LogDebug($" '{Config.Name}': '{NodeStatusConstants.GetRoundStatusById(Status).Name}'");
                    }
                    Thread.Sleep(1000);
                }
            }
        }
        public DkgNodeServer(DkgNodeConfig config, string serviceNodeUrl, ILogger logger)
        {
            Config = config;
            Logger = logger;
            ServiceNodeUrl = serviceNodeUrl;

            logger.LogInformation($"Starting '{Config.Name}' host: {Config.Host}, port: {Config.Port}");

            G = new Secp256k1Group();

            DkgNodeSrv = new DkgNodeService(logger, Config.Name, G);
            Config.PublicKey = Convert.ToBase64String(PublicKey);

            GRpcServer = new Server
            {
                Services = { BindService(DkgNodeSrv) },
                Ports = { new ServerPort("0.0.0.0", Config.Port, ServerCredentials.Insecure) }
            };

            RunnerThread = new Thread(Runner);
        }

        public void Start()
        {

            Logger.LogInformation($"Starting '{Config.Name}'");
            GRpcServer.Start();
            RunnerThread.Start();
        }

        public void Shutdown()
        {
            GRpcServer.ShutdownAsync().Wait();
            IsRunning = false;
            RunnerThread.Join();
        }

        // gRPC клиент и драйвер всего процесса
        public void RunDkg()
        {
            Logger.LogDebug($"'{Config.Name}': Running Dkg algorithm for {Configs.Length} nodes: step 1");
            // gRPC клиенты "в сторону" других участников
            // включая самого себя, чтобы было меньше if'ов
            GrpcChannel[] Channels = new GrpcChannel[Configs.Length];
            DkgNodeClient[] Clients = new DkgNodeClient[Configs.Length];

            for (int j = 0; j < Configs.Length; j++)
            {
                Channels[j] = GrpcChannel.ForAddress($"http://{Configs[j].Host}:{Configs[j].Port}");
                Clients[j] = new DkgNodeClient(Channels[j]); // ChannelCredentials.Insecure ???
            }

            // Таймаут, который используется в точках синхронизации вместо синхронизации
            int syncTimeout = Math.Max(10000, Configs.Length * 1000);

            PublicKeys = new IPoint[Configs.Length];

            // Пороговое значение для верификации ключа, то есть сколько нужно валидных commitment'ов
            // Алгоритм Шамира допускает минимальное значение = N/2+1, где N - количество участников, но мы
            // cделаем N-1, так чтобы 1 неадекватная нода позволяла расшифровать сообщение, а две - нет.
            int threshold = PublicKeys.Length/2 + 1;

            // 1. Собираем публичные ключи со всех участников
            //    Тут, конечно, упрощение. Предполагается, что все ответят без ошибкт
            //    В промышленном варианте список участников, который у нас есть - это список желательных участников
            //    В этом уикле нужно сформировать список реальных кчастников, то есть тех, где gRPC end point хотя бы
            //    откликается
            for (int j = 0; j < Configs.Length; j++)
            {
                byte[] pkb = [];
                var pk = Clients[j].GetPublicKey(new PublicKeyRequest());
                if (pk != null)
                {
                    pkb = pk.Data.ToByteArray();
                }
                if (pkb.Length != 0)
                {
                    PublicKeys[j] = G.Point().SetBytes(pkb);
                    // Console.WriteLine($"Got public key of node {j} at node {Index}: {PublicKeys[j]}");
                }
                else
                {
                    // См. комментарий выше
                    // PubliсKeys[j] = null  не позволит инициализировать узел
                    // Можно перестроить список участников, можно использовать "левый"
                    // Для демо считаем это фатальной ошибкой
                    Logger.LogError($"FATAL ERROR FOR NODE '{Config.Name}': failed to get public key of node '{Configs[j].Name}'");
                    Status = Failed;
                }
            }

            // Здесь будут distributed deals (не знаю, как перевести), предложенные этим узлом другим узлам
            // <индекс другого узла> --> наш deal для другого узла
            Dictionary<int, DistDeal> deals = [];

            if (ContinueDkg)
            {
                // Дадим время всем другим узлам обменяться публичными ключами
                // Можно добавить точку синхронизации, то есть отдельным gRPC вызовом опрашивать вскх участников дошли ли они до этой точки,
                // но тогда возникает вопром, что делать с теми кто до неё не доходит "никогда" (в смысле "достаточно быстро")
                Logger.LogDebug($"'{Config.Name}': Running Dkg algorithm for {Configs.Length} nodes: step 2");
                Thread.Sleep(syncTimeout);

            // 2. Создаём генератор/обработчик распределённого ключа для этого узла
            //    Это будет DkgNode.Dkg.  Он создаётся уровнем ниже, чтобы быть доступным как из gRPC клиента (этот объект),
            //    так и из сервера (DkgNode)

                try
                {
                    DkgNodeSrv.Dkg = DistKeyGenerator.CreateDistKeyGenerator(G, DkgNodeSrv.PrivateKey, PublicKeys, threshold) ??
                          throw new Exception($"Could not create distributed key generator/handler");
                    deals = DkgNodeSrv.Dkg.GetDistDeals() ??
                            throw new Exception($"Could not get a list of deals");
                }
                // Исключение может быть явно созданное выше, а может "выпасть" из DistKeyGenerator
                // Ошибки здесь все фатальны
                catch (Exception ex)
                {
                    Logger.LogError($"FATAL ERROR FOR NODE '{Config.Name}': {ex.Message}");
                    Status = Failed;
                }
            }

            DistKeyShare? distrKey = null;
            IPoint? distrPublicKey = null;

            // 3. Разошkём наши "предложения" другим узлам
            //    В ответ мы ожидаем distributed response, который мы для начала сохраним

            if (ContinueDkg)
            {
                List<DistResponse> responses = new(deals.Count);
                foreach (var (i, deal) in deals)
                {
                    // Console.WriteLine($"Querying from {Index} to process for node {i}");

                    byte[] rspb = [];
                    // Самому себе тоже пошлём, хотя можно вызвать локально
                    // if (Index == i) try { response = DkgNode.Dkg!.ProcessDeal(response) } catch { }
                    var rb = Clients[i].ProcessDeal(new ProcessDealRequest { Data = ByteString.CopyFrom(deal.GetBytes()) });
                    if (rb != null)
                    {
                        rspb = rb.Data.ToByteArray();
                    }
                    if (rspb.Length != 0)
                    {
                        DistResponse response = new();
                        response.SetBytes(rspb);
                        responses.Add(response);
                    }
                    else
                    {
                        // На этом этапе ошибка не является фатальной
                        // Просто у нас или получится или не получится достаточное количество commitment'ов
                        // См. комментариё выше про Threshold
                        Logger.LogDebug($"Node '{Config.Name}': failed to get response from node '{Configs[i].Name}'");
                    }
                }

                if (ContinueDkg)
                {
                    // Тут опять точка синхронизации
                    // Участник должен сперва получить deal, а только потом response'ы для этого deal
                    // В противном случае response будет проигнорирован
                    // Можно передать ошибку через gRPC, анализировать в цикле выше и вызывать ProcessResponse повторно.
                    // Однако, опять вопрос с теми, кто не ответит никогда.
                    Logger.LogDebug($"'{Config.Name}': Running Dkg algorithm for {Configs.Length} nodes: step 3");
                    Thread.Sleep(syncTimeout);

                    foreach (var response in responses)
                    {
                        for (int i = 0; i < PublicKeys.Length; i++)
                        {
                            // Самому себе тоже пошлём, хотя можно вызвать локально
                            // if (Index == i) try { DkgNode.Dkg!.ProcessResponse(response) } catch { }
                            Clients[i].ProcessResponse(new ProcessResponseRequest { Data = ByteString.CopyFrom(response.GetBytes()) });
                        }
                    }
                }

                if (ContinueDkg)
                {
                    // И ещё одна точка синхронизации
                    // Теперь мы ждём, пока все обменяются responsе'ами
                    Logger.LogDebug($"'{Config.Name}': Running Dkg algorithm for {Configs.Length} nodes: step 4");
                    Thread.Sleep(syncTimeout);

                    DkgNodeSrv.Dkg!.SetTimeout();

                    // Обрадуемся тому, что нас признали достойными :)
                    bool crt = DkgNodeSrv.Dkg!.ThresholdCertified();
                    string certified = crt ? "" : "not ";
                    Logger.LogDebug($"'{Config.Name}': {certified}certified");

                    if (crt)
                    {
                        // Методы ниже безопасно вызывать, только если ThresholdCertified() вернул true
                        distrKey = DkgNodeSrv.Dkg!.DistKeyShare();
                        DkgNodeSrv.SecretShare = distrKey.PriShare();
                        distrPublicKey = distrKey.Public();
                        DistributedPublicKey = distrPublicKey;
                        Status = Finished;
                    }
                    else
                    {
                        DistributedPublicKey = null;
                        Status = Failed;
                    }
                }
            }
        }

    }
}