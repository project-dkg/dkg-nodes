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

using System.Text;
using System.Text.Json; 
using Microsoft.Extensions.Logging;

using dkg.share;
using dkg.group;
using dkg.poly;

using dkgNode.Models;
using dkgCommon.Constants;
using dkgCommon.Models;

using static dkgCommon.Constants.NStatus;


namespace dkgNode.Services
{
    public class DkgNodeService
    {
        // Node Status and Round
        internal NStatus Status { get; set; } = NotRegistered;
        internal int? Round { get; set; } = null;
        public NStatus GetStatus() => Status;
        public int? GetRound() => Round;
        public void SetStatus(NStatus status)
        {
            Status = status;
        }
        public void SetStatusAndRound(NStatus status, int round)
        {
            Status = status;
            Round = round;
        }
        public void SetStatusClearRound(NStatus status)
        {
            Status = status;
            Round = null;
        }

        // ...
        internal Secp256k1Group G { get; }
        internal IScalar PrivateKey { get; }  // Приватный ключ этого узла  
        internal IPoint PublicKey { get; }    // Публичный ключ этого узла

        // Пороговое значение для верификации ключа, то есть сколько нужно валидных commitment'ов
        // Алгоритм Шамира допускает минимальное значение = N/2+1, где N - количество участников
        internal int Threshold { get; set; } = 0;

        internal DistKeyGenerator? Dkg { get; set; } = null;

        // Distributeg key Generation output
        internal IPoint? DistributedPublicKey { get; set; } = null;
        internal PriShare? SecretShare { get; set; } = null;


        internal JsonSerializerOptions JsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

        // Публичныке ключи других участников
        internal IPoint[] PublicKeys { get; set; } = [];

        internal ILogger Logger { get; }
        internal DkgNodeConfig Config { get; }
        internal string Name => Config.Name;
        internal string ServiceNodeUrl => Config.ServiceNodeUrl;
        internal int PollingInterval => Config.PollingInterval;
        public DkgNodeService(DkgNodeConfig config, ILogger<DkgNodeService> logger)
        {
            Config = config;
            Logger = logger;

            logger.LogInformation("'{Name}': creating", Name);

            G = new Secp256k1Group();
            PrivateKey = G.Scalar();
            PublicKey = G.Base().Mul(PrivateKey);

            Config.EncodePublicKey(PublicKey.GetBytes());
        }

        public async Task Register(HttpClient httpClient)
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
                                 Name, ServiceNodeUrl, e.Message);
            }
            if (response == null)
            {
                Logger.LogError("Node '{Name}' failed to register with '{ServiceNodeUrl}', no response received",
                                 Name, ServiceNodeUrl);
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
                                             Name, responseContent, ServiceNodeUrl);
                        }
                        else
                        {
                            if (reference.Id == 0)
                            {
                                roundId = null;
                                Logger.LogDebug("'{Name}': attempted to register with '{ServiceNodeUrl}' [No round]",
                                                       Name, ServiceNodeUrl);
                            }
                            else
                            {
                                roundId = reference.Id;
                                Logger.LogInformation("'{Name}': registered with '{ServiceNodeUrl}' [Round {roundId}]",
                                                        Name, ServiceNodeUrl, roundId);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Logger.LogError("'{Name}': failed to parse service node response '{responseContent}' from '{ServiceNodeUrl}'\n{message}",
                                         Name, responseContent, ServiceNodeUrl, ex.Message);
                    }
                }
                else
                {
                    Logger.LogError("'{Name}': failed to register with '{ServiceNodeUrl}': {StatusCode}\n{content}",
                                    Name, ServiceNodeUrl, response.StatusCode, responseContent);
                }
            }

            if (roundId != null)
            {
                SetStatusAndRound(WaitingRoundStart, (int)roundId);
            }
        }

        public async Task RunDkg(HttpClient httpClient, string[] encodedPublicKeys, CancellationToken stoppingToken)
        {
            StatusResponse statusResponse;
            if (!await ReportStatusAndCheck(httpClient, [RunningStepOne], stoppingToken)) return;

            try
            {
                PublicKeys = new Secp256k1Point[encodedPublicKeys.Length];

                for (int i = 0; i < encodedPublicKeys.Length; i++)
                {
                    if (encodedPublicKeys[i] != null)
                    {
                        byte[] decodedBytes = Convert.FromBase64String(encodedPublicKeys[i]);
                        if (decodedBytes.Length != 0)
                        {
                            PublicKeys[i] = new Secp256k1Point();
                            PublicKeys[i].SetBytes(decodedBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("'{Name}': failed to parse service node response '{responseContent}' from '{ServiceNodeUrl}'\n{Message}",
                                                                 Name, encodedPublicKeys, ServiceNodeUrl, ex.Message);
                SetStatus(Failed);
                return;
            }

            if (!await ReportStatusAndCheck(httpClient, [RunningStepOne], stoppingToken)) return;

            string[] encodedDeals = RunDkgStepOne();
            if (Status == Failed) return;

            Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, waiting step 2]",
                            Name, PublicKeys.Length, Round);
            Status = WaitingStepTwo;

            statusResponse = await ReportStatus(httpClient, encodedDeals);
            if (!ShallContinue([WaitingStepTwo, RunningStepTwo], stoppingToken)) return;

            while (Status == WaitingStepTwo)
            {
                Thread.Sleep(PollingInterval);
                statusResponse = await ReportStatus(httpClient, null);
                if (!ShallContinue([WaitingStepTwo, RunningStepTwo], stoppingToken)) return;
            }

            if (!await ReportStatusAndCheck(httpClient, [RunningStepTwo], stoppingToken)) return;
            string[] encodedResponses = RunDkgStepTwo(statusResponse.Data);


            Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, waiting step 3]",
                             Name, PublicKeys.Length, Round);
            Status = WaitingStepThree;

            statusResponse = await ReportStatus(httpClient, encodedResponses);
            if (!ShallContinue([WaitingStepThree, RunningStepThree], stoppingToken)) return;

            while (Status == WaitingStepThree)
            {
                Thread.Sleep(PollingInterval);
                statusResponse = await ReportStatus(httpClient, null);
                if (!ShallContinue([WaitingStepThree, RunningStepThree], stoppingToken)) return;
            }

            if (!await ReportStatusAndCheck(httpClient, [RunningStepThree], stoppingToken)) return;
            RunDkgStepThree(statusResponse.Data);


            DistributedPublicKey = null;
            string[] encodedResult = [];

            if (Dkg != null)
            {
                Dkg.SetTimeout();

                // Обрадуемся тому, что нас признали достойными :)
                bool crt = Dkg.ThresholdCertified();
                string certified = crt ? "" : "not ";
                Logger.LogInformation("'{Name}': {certified}certified", Name, certified);

                if (crt)
                {
                    // Методы ниже безопасно вызывать, только если ThresholdCertified() вернул true
                    var distrKey = Dkg.DistKeyShare();
                    SecretShare = distrKey.PriShare();
                    DistributedPublicKey = distrKey.Public();
                    Status = Finished;
                    encodedResult = new string[2];
                    encodedResult[0] = Convert.ToBase64String(DistributedPublicKey.GetBytes());
                    encodedResult[1] = Convert.ToBase64String(SecretShare.GetBytes());
                }
                else
                {
                    Status = Failed;
                }
            }
            else
            {
                Status = Failed;
                Logger.LogInformation("'{Name}': Dkg algorith has been cancelled prematurely", Name);
            }
            await ReportStatus(httpClient, encodedResult);
        }

        internal StatusResponse? ParseStatusResponse(string responseContent)
        {
            StatusResponse? statusResponse = null;
            try
            {
                statusResponse = JsonSerializer.Deserialize<StatusResponse>(responseContent, JsonSerializerOptions);
                if (statusResponse == null)
                {
                    Logger.LogError("'{Name}': failed to parse service node response '{responseContent}' from '{ServiceNodeUrl}'",
                                                            Name, responseContent, ServiceNodeUrl);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("'{Name}': failed to parse service node response '{responseContent}' from '{ServiceNodeUrl}'\n{Message}",
                                                   Name, responseContent, ServiceNodeUrl, ex.Message);
            }
            return statusResponse;
        }

        public async Task<StatusResponse> ReportStatus(HttpClient httpClient, string[]? data)
        {
            var statusResponse = new StatusResponse((int)(Round == null ? 0 : Round), Failed);
            string r = $"[status '{NodeStatusConstants.GetNodeStatusById(Status).Name}', round '{(Round == null ? 0: Round)}']";

            var report = new StatusReport(Config.GetPublicKey()!, Name, Round ?? 0, Status);
            if (data != null)
            {
                report.Data = data;
            }
            HttpResponseMessage? httpResponse = null;
            var jsonPayload = JsonSerializer.Serialize(report);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            try
            {
                httpResponse = await httpClient.PostAsync($"{ServiceNodeUrl}/api/nodes/status", httpContent);
                if (httpResponse == null)
                {
                    Logger.LogError("Node '{Name}' failed to report {r} to '{ServiceNodeUrl}', no response received, resetting node",
                                     Name, r, ServiceNodeUrl);
                    SetStatusClearRound(NotRegistered);
                }
            }
            catch (Exception e)
            {
                Logger.LogError("'{Name}': failed to report {r} to '{ServiceNodeUrl}', Exception: {Message}, resetting node",
                                 Name, r, ServiceNodeUrl, e.Message);
                SetStatusClearRound(NotRegistered);
            }

            if (httpResponse != null)
            {
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                if (httpResponse.IsSuccessStatusCode)
                {
                    Logger.LogDebug("'{Name}': reported {r} to '{ServiceNodeUrl}'  Response status = {Status}\nResponse Data: {Data}", 
                                    Name, r, ServiceNodeUrl, 
                                    httpResponse.StatusCode, responseContent);
                    var sr = ParseStatusResponse(responseContent);
                    if (sr != null)
                    {
                        statusResponse = sr;
                        if (Status != statusResponse.Status)
                        {
                            Logger.LogDebug("'{Name}': Changing node state to {Status}", Name, statusResponse.Status);
                            if (statusResponse.Status == NotRegistered) SetStatusClearRound(statusResponse.Status);
                            else SetStatus(statusResponse.Status);
                        }
                    }
                }
                else
                {
                    Logger.LogDebug("'{Name}': reported {r} to '{ServiceNodeUrl}', Response status = {Status}, resetting node]\n{Message}", 
                        Name, r, ServiceNodeUrl, httpResponse.StatusCode, responseContent);
                    SetStatusClearRound(NotRegistered);
                }
            }

            return statusResponse;
        }

        internal async Task<bool> ReportStatusAndCheck(HttpClient httpClient, NodeStatus[] allowedStatuses, CancellationToken stoppingToken)
        {
            await ReportStatus(httpClient, null);
            return ShallContinue(allowedStatuses, stoppingToken);
        }

        private string[] RunDkgStepOne()
        {
            // Здесь будут distributed deals (не знаю, как перевести), предложенные этим узлом другим узлам
            // <индекс другого узла> --> наш deal для другого узла
            Dictionary<int, DistDeal> deals = [];
            string[] encodedDeals = [];

            Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, step 1]",
                             Name, PublicKeys.Length, Round);

            Threshold = PublicKeys.Length / 2 + 1;

            // Создаём генератор/обработчик распределённого ключа для этого узла
            // Это будет DkgNode.Dkg.  Он создаётся уровнем ниже, чтобы быть доступным как из gRPC клиента (этот объект),
            // так и из сервера (DkgNode)

            try
            {
                Dkg = DistKeyGenerator.CreateDistKeyGenerator(G, PrivateKey, PublicKeys, Threshold) ??
                      throw new Exception($"Could not create distributed key generator/handler");
                deals = Dkg.GetDistDeals() ??
                        throw new Exception($"Could not get a list of deals");
            }
            // Исключение может быть явно созданное выше, а может "выпасть" из DistKeyGenerator
            // Ошибки здесь все фатальны
            catch (Exception ex)
            {
                Logger.LogError("'{Name}': NODE FATAL ERROR\n{Message}", Name, ex.Message);
                Status = Failed;
            }

            if (Status != Failed)
            {
                encodedDeals = new string[PublicKeys.Length];
                for (int i = 0; i < PublicKeys.Length; i++)
                {
                    deals.TryGetValue(i, out DistDeal? deal);
                    encodedDeals[i] = deal != null ? Convert.ToBase64String(deal.GetBytes()) :
                                                     Convert.ToBase64String(Array.Empty<byte>());
                }
            }
            return encodedDeals;
        }

        private string[] RunDkgStepTwo(string[] encodedDeals)
        {
            Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, step 2]",
                                            Name, PublicKeys.Length, Round);

            string[] encodedResponses = new string[PublicKeys.Length];
            for (int i = 0; i < encodedDeals.Length; i++)
            {
                byte[]? bkr = null;
                try
                {
                    byte[] pkd = Convert.FromBase64String(encodedDeals[i]);

                    if (pkd.Length != 0)
                    {
                        DistDeal distDeal = new();
                        distDeal.SetBytes(pkd);
                        bkr = Dkg!.ProcessDeal(distDeal).GetBytes();
                    }
                }
                catch (Exception ex)
                {
                    // Ошибки на данном этапе не являются фатальными
                    // Если response'а нет, это просто значит, что в дальнейшую обработку ничего не уйдёт. 
                    Logger.LogDebug("'{Name}': ProcessDeal request failed [Round: {RoundId}],\n {Message}", Name, Round, ex.Message);
                }
                encodedResponses[i] = (bkr != null) ? Convert.ToBase64String(bkr) :
                                                      Convert.ToBase64String(Array.Empty<byte>());
            }

            return encodedResponses;
        }

        private void RunDkgStepThree(string[] encodedResponses)
        {
            Logger.LogDebug("'{Name}': Running Dkg algorithm for {Length} nodes [Round {round}, step 3]", Name, PublicKeys.Length, Round);
            for (int i = 0; i < encodedResponses.Length; i++)
            {
                try
                {
                    byte[] pkd = Convert.FromBase64String(encodedResponses[i]);

                    if (pkd.Length != 0)
                    {
                        DistResponse distResponse = new();
                        distResponse.SetBytes(pkd);
                        DistJustification? bkr = Dkg!.ProcessResponse(distResponse);
                        if (bkr != null)
                        {
                            Logger.LogWarning("'{Name}': ProcessResponse request needed justification [Round: {RoundId}]", Name, Round);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Ошибки на данном этапе не являются фатальными
                    // Если response не удалось обработать, это значит, что он не учитывается. Как будто и не было.
                    Logger.LogDebug("'{Name}': ProcessResponse request failed [Round: {RoundId}],\n{Message}", Name, Round, ex.Message);
                }
            }
        }
        internal bool ShallContinue(NodeStatus[] allowedStatuses, CancellationToken stoppingToken)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                Logger.LogWarning("'{Name}': Cancelling Dkg algorithm [Round '{round}', Stopping token is set]", Name, Round);
                Status = Failed;
                return false;
            }

            if (Status == NotRegistered)
            {
                Round = null;
                return false;
            }

            foreach (var status in allowedStatuses)
            {
                if (Status == status)
                {
                    return true;
                }
            }

            Logger.LogWarning("'{Name}': Cancelling Dkg algorithm [Round '{round}', Expected status 'RunningStepOne', Got status '{status}']", Name, Round, (NodeStatus)Status);
            Status = Failed;

            return false;
        }


    }
}
