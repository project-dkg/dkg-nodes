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

using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System.Text;

using Google.Protobuf;
using Grpc.Net.Client;
using Grpc.Core;

using dkg;
using dkg.share;
using dkg.group;

using dkgNode.Models;
using dkgCommon;
using dkgCommon.Constants;
using dkgCommon.Models;

using static dkgCommon.Constants.NStatus;
using static dkgCommon.DkgNode;
using dkg.vss;

namespace dkgNode.Services
{
    // Узел
    // Создаёт instance gRPC сервера (class DkgNodeServer)
    // и gRPC клиента (это просто отдельный поток TheThread)
    // В TheThread реализована незатейливая логика этого примера
    class DkgNodeService
    {
        internal JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
        internal Server GRpcServer { get; }
        internal DkgNodeServer DkgNodeSrv { get; }

        // Публичныке ключи других участников
        internal IPoint[] PublicKeys { get; set; } = [];

        internal Thread RunnerThread { get; set; }
        internal bool IsRunning { get; set; } = true;

        internal bool ContinueDkg
        {
            get { return Status == Running && IsRunning; }
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

        internal int? Round
        {
            get { return DkgNodeSrv.GetRound(); }
        }
        internal IGroup G { get; }

        internal ILogger Logger { get; }
        internal string ServiceNodeUrl { get; }
        DkgNodeConfig Config { get; }
        DkgNodeConfig[] Configs
        {
            get { return DkgNodeSrv.Configs; }
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
                response = await httpClient.PostAsync($"{ServiceNodeUrl}/api/nodes/register", httpContent);
            }
            catch (Exception e)
            {
                Logger.LogError("'{Name}': failed to register with '{ServiceNodeUrl}', Exception: {Message}",
                                 Config.Name, ServiceNodeUrl, e.Message);
            }
            if (response == null)
            {
                Logger.LogError("Node '{Name}' failed to register with '{ServiceNodeUrl}', no response received",
                                 Config.Name, ServiceNodeUrl);
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
                            Logger.LogError("'{Name}': failed to parse service node response '{responseContent}' from '{ServiceNodeUrl}'",
                                             Config.Name, responseContent, ServiceNodeUrl);
                        }
                        else
                        {
                            if (reference.Id == 0)
                            {
                                roundId = null;
                                Logger.LogDebug("'{Name}': attempted to register with '{ServiceNodeUrl}' [No round]",
                                                       Config.Name, ServiceNodeUrl);
                            }
                            else
                            {
                                roundId = reference.Id;
                                Logger.LogInformation("'{Name}': registered with '{ServiceNodeUrl}' [Round {roundId}]",
                                                        Config.Name, ServiceNodeUrl, roundId);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Logger.LogError("'{Name}': failed to parse service node response '{responseContent}' from '{ServiceNodeUrl}'",
                                         Config.Name, responseContent, ServiceNodeUrl);
                        Logger.LogError(ex.Message);
                    }
                }
                else
                {
                    Logger.LogError("'{Name}': failed to register with '{ServiceNodeUrl}': {StatusCode}",
                                    Config.Name, ServiceNodeUrl, response.StatusCode);
                    Logger.LogError(responseContent);
                }
            }
            return roundId;
        }

        internal async Task<bool> ReportStatus(HttpClient httpClient)
        {
            bool res = false;
            string r = $"[status '{NodeStatusConstants.GetNodeStatusById(Status).Name}', round '{(Round == null ? 0: Round)}']";

            StatusReport report = new StatusReport(Config.Port, Config.Host, Round ?? 0, Status);
            HttpResponseMessage? response = null;
            var jsonPayload = JsonSerializer.Serialize(report);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            try
            {
                response = await httpClient.PostAsync($"{ServiceNodeUrl}/api/nodes/status", httpContent);
            }
            catch (Exception e)
            {
                Logger.LogError("'{Name}': failed to report {r} to '{ServiceNodeUrl}', Exception: {Message}",
                                 Config.Name, r, ServiceNodeUrl, e.Message);
            }
            if (response == null)
            {
                Logger.LogError("Node '{Name}' failed to report {r} to '{ServiceNodeUrl}', no response received",
                                 Config.Name, r, ServiceNodeUrl);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    Logger.LogDebug("'{Name}': reported {r} to '{ServiceNodeUrl}'  [Ok, {Status}]", Config.Name, r, ServiceNodeUrl, response.StatusCode);
                    res = true;
                }
                else
                {
                    Logger.LogDebug("'{Name}': reported {r} to '{ServiceNodeUrl}' [Needs reset, {Status}]\n{Message}", Config.Name, r, ServiceNodeUrl, response.StatusCode, responseContent);
                }
            }
            return res;
        }

        internal async void Runner()
        {
            var httpClient = new HttpClient();
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
                bool conf = await ReportStatus(httpClient);
                if (!conf)  // Hard reset
                {
                    DkgNodeSrv.SetStatusClearRound(NotRegistered);
                }
                else
                {
                    if (Status == Running)
                    {
                        RunDkg();
                    }
                    else
                    {
                        Thread.Sleep(3000);
                    }
                }


            }
        }
        public DkgNodeService(DkgNodeConfig config, string serviceNodeUrl, ILogger logger)
        {
            Config = config;
            Logger = logger;
            ServiceNodeUrl = serviceNodeUrl;

            logger.LogInformation("'{Name}': starting at {Host}:{Port}",
                Config.Name, Config.Host, Config.Port);
            G = new Secp256k1Group();

            DkgNodeSrv = new DkgNodeServer(logger, Config.Name, G);
            Config.PublicKey = Convert.ToBase64String(PublicKey);

            // Obtain the machine's IP address
            var addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            var address = addressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            string? ipAddress = address?.ToString();

            logger.LogInformation("'{Name}': binding to {ipAddress}:{Port}",
                Config.Name, ipAddress, Config.Port);

            GRpcServer = new Server
            {
                Services = { BindService(DkgNodeSrv) },
                Ports = { new ServerPort(ipAddress, Config.Port, ServerCredentials.Insecure) }
            };

            RunnerThread = new Thread(Runner);
        }

        public void Start()
        {

            Logger.LogInformation("'{Name}': Start", Config.Name);
            GRpcServer.Start();
            RunnerThread.Start();
        }

        public void Shutdown()
        {
            GRpcServer.ShutdownAsync().Wait();
            IsRunning = false;
            RunnerThread.Join();
            Logger.LogInformation("'{Name}': Shutdown", Config.Name);
        }

        // gRPC клиент и драйвер всего процесса
        public void RunDkg()
        {
            Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, step 1]",
                Config.Name, Configs.Length, Round);
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
            int threshold = PublicKeys.Length / 2 + 1;

            // 1. Декодируем публичные ключи со для вчех участников
            //    Тут, конечно, упрощение. Предполагается, что все ответят без ошибoк
            //    В промышленном варианте список участников, который у нас есть - это список желательных участников
            //    В этом цикле нужно сформировать список реальных участников, то есть тех, где gRPC end point хотя бы
            //    откликается
            for (int j = 0; j < Configs.Length; j++)
            {
                byte[] pkb = [];
                var pk = Configs[j].PublicKey;
                if (pk != null)
                {
                    pkb = Convert.FromBase64String(pk);
                }
                if (pkb.Length != 0)
                {
                    PublicKeys[j] = G.Point().SetBytes(pkb);
                }
                else
                {
                    // См. комментарий выше
                    // PubliсKeys[j] = null  не позволит инициализировать узел
                    // Можно перестроить список участников, можно использовать "левый"
                    // Пока считаем это фатальной ошибкой
                    Logger.LogError("'{Name}': NODE FATAL ERROR, failed to get public key of node '{OtherName}'",
                         Config.Name, Configs[j].Name);
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
                Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, step 2]",
                    Config.Name, Configs.Length, Round);
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
                    Logger.LogError("'{Name}': NODE FATAL ERROR\n{Message}", Config.Name, ex.Message);
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
                    try
                    {
                        int progress = 0;
                        byte[] rspb = [];
                        var rb = Clients[i].ProcessDeal(new ProcessDealRequest
                        {
                            RoundId = (int)(Round == null ? 0 : Round),
                            Data = ByteString.CopyFrom(deal.GetBytes())
                        });
                        progress = 1;
                        if (rb != null)
                        {
                            rspb = rb.Data.ToByteArray();
                            progress = 2;
                        }
                        if (rspb.Length != 0)
                        {
                            DistResponse response = new();
                            response.SetBytes(rspb);
                            responses.Add(response);
                            progress = 3;
                        }
                        else
                        {
                            // На этом этапе ошибка не является фатальной
                            // Просто у нас или получится или не получится достаточное количество commitment'ов
                            // См. комментариё выше про Threshold
                            Logger.LogDebug("'{Name}': failed to get response from node '{OtherName}, progress [{p}]'",
                                Config.Name, Configs[i].Name, progress);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug("'{Name}': exception on call of ProcessDeal to node '{OtherName}'\n{Message}",
                            Config.Name, Configs[i].Name, ex.Message);
                    }
                    if (!ContinueDkg) break;
                }

                if (ContinueDkg)
                {
                    // Тут опять точка синхронизации
                    // Участник должен сперва получить deal, а только потом response'ы для этого deal
                    // В противном случае response будет проигнорирован
                    // Можно передать ошибку через gRPC, анализировать в цикле выше и вызывать ProcessResponse повторно.
                    // Однако, опять вопрос с теми, кто не ответит никогда.
                    Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, step 3]",
                        Config.Name, Configs.Length, Round);
                    Thread.Sleep(syncTimeout);

                    foreach (var response in responses)
                    {

                        for (int i = 0; i < PublicKeys.Length && ContinueDkg; i++)
                        {
                            try
                            {
                                // Самому себе тоже пошлём, хотя можно вызвать локально
                                // if (Index == i) try { DkgNode.Dkg!.ProcessResponse(response) } catch { }
                                Clients[i].ProcessResponse(new ProcessResponseRequest
                                {
                                    RoundId = (int)(Round == null ? 0 : Round),
                                    Data = ByteString.CopyFrom(response.GetBytes())
                                });
                            }
                            catch (Exception ex)
                            {
                                Logger.LogDebug("'{Name}': exception on call of ProcessResponse to node '{OtherName}'\n{Message}",
                                    Config.Name, Configs[i].Name, ex.Message);
                            }
                        }
                        if (!ContinueDkg) break;
                    }
                }
            }

            if (ContinueDkg)
            {
                // И ещё одна точка синхронизации
                // Теперь мы ждём, пока все обменяются responsе'ами
                Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, step 4]",
                    Config.Name, Configs.Length, Round);
                Thread.Sleep(syncTimeout);
            }

            DistributedPublicKey = null;
            Status = Failed;

            if (DkgNodeSrv.Dkg != null)
            { 
                DkgNodeSrv.Dkg.SetTimeout();

                // Обрадуемся тому, что нас признали достойными :)
                bool crt = DkgNodeSrv.Dkg.ThresholdCertified();
                string certified = crt ? "" : "not ";
                Logger.LogInformation("'{Name}': {certified}certified", Config.Name, certified);

                if (crt)
                {
                    // Методы ниже безопасно вызывать, только если ThresholdCertified() вернул true
                    distrKey = DkgNodeSrv.Dkg.DistKeyShare();
                    DkgNodeSrv.SecretShare = distrKey.PriShare();
                    distrPublicKey = distrKey.Public();
                    DistributedPublicKey = distrPublicKey;
                    Status = Finished;
                }
            }
            else
            {
                Logger.LogInformation("'{Name}': Dkg algorith has been cancelled prematurely", Config.Name);
            }
        }
    }
}
