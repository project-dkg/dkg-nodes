using dkg.group;
using dkg.poly;
using dkg.share;
using dkgNode.Constants;
using Google.Protobuf;
using Grpc.Core;
using dkgCommon;
using dkgNode.Models;

using static dkgCommon.DkgNode;
using static dkgNode.Constants.NStatus;


namespace dkgNode.Services
{
    // gRPC сервер
    // Здесь "сложены" параметры узла, которые нужны и клиенту и серверу
    class DkgNodeService : DkgNodeBase
    {
        private IGroup G { get; }
        internal string Name { get; }
        internal IScalar PrivateKey { get; }  // Приватный ключ этого узла  
        internal IPoint PublicKey { get; }    // Публичный ключ этого узла
        internal PriShare? SecretShare { get; set; } = null;
        internal DkgNodeConfig[] Configs { get; set; } = [];

        // Distributed Key Generator 
        public DistKeyGenerator? Dkg { get; set; } = null;
        // Защищает Dkg от параллельной обработки наскольких запросов
        private readonly object dkgLock = new() { };

        // Node status
        private NStatus Status { get; set; } = NotRegistered;
        private int? Round { get; set; } = null;
        private IPoint? DistributedPublicKey = null; // Distributed public key
        private readonly object stsLock = new() { };

        public void SetStatus(NStatus status)
        {
            lock (stsLock)
            {
                Status = status;
            }
        }
        public void SetStatusAndRound(NStatus status, int round)
        {
            lock (stsLock)
            {
                Status = status;
                Round = round;
            }
        }
        public NStatus GetStatus()
        {
            lock (stsLock)
            {
                return Status;
            }
        }

        public void SetDistributedPublicKey(IPoint? dpk)
        {
            lock (stsLock)
            {
                DistributedPublicKey = dpk;
            }
        }

        public IPoint? GetDistributedPublicKey()
        {
            IPoint? dpk = null;
            lock (stsLock)
            {
                dpk = DistributedPublicKey;
            }
            return dpk;
        }

        // Cipher
        internal IPoint C1 { get; set; }
        internal IPoint C2 { get; set; }

        private readonly ILogger _logger;
        public DkgNodeService(ILogger logger, string name, IGroup group)
        {
            _logger = logger;
            G = group;
            C1 = G.Point();
            C2 = G.Point();
            Name = name;
            PrivateKey = G.Scalar();
            PublicKey = G.Base().Mul(PrivateKey);
        }


        // gRPC сервер реализует 4 метода
        //
        // Выдача публичного ключа
        // ProcessDeal
        // ProcessResponse
        // Прием сообщения
        // Частичная расшифровка
        public override Task<PublicKeyReply> GetPublicKey(PublicKeyRequest _, ServerCallContext context)
        {
            PublicKeyReply resp = new() { Data = ByteString.CopyFrom(PublicKey.GetBytes()) };
            return Task.FromResult(resp);
        }

        public override Task<ProcessDealReply> ProcessDeal(ProcessDealRequest deal, ServerCallContext context)
        {
            ProcessDealReply resp;

            DistDeal distDeal = new();
            distDeal.SetBytes(deal.Data.ToByteArray());

            lock (dkgLock)
            {
                ByteString data = ByteString.CopyFrom([]);
                if (Dkg != null)
                {
                    try
                    {
                        data = ByteString.CopyFrom(Dkg.ProcessDeal(distDeal).GetBytes());
                    }
                    catch (Exception ex)
                    {
                        // Ошибки на данном этапе не являются фатальными
                        // Если response'а нет, это просто значит, что в дальнейшую обработку ничего не уйдёт. 
                        Console.WriteLine($"{Name}: {ex.Message}");
                    }
                }

                resp = new ProcessDealReply { Data = data };
            }
            return Task.FromResult(resp);
        }

        public override Task<ProcessResponseReply> ProcessResponse(ProcessResponseRequest response, ServerCallContext context)
        {
            DistResponse distResponse = new();
            distResponse.SetBytes(response.Data.ToByteArray());

            lock (dkgLock)
            {
                ByteString data = ByteString.CopyFrom([]);
                if (Dkg != null)
                {
                    try
                    {
                        DistJustification? distJust = Dkg.ProcessResponse(distResponse);
                        if (distJust != null)
                            Console.WriteLine($"{Name}: justification !!!");
                        //    data = ByteString.CopyFrom(distJust.GetBytes());
                    }
                    catch (Exception ex)
                    {
                        // Ошибки на данном этапе не являются фатальными
                        // Если response не удалось обработать, это значит, что он не учитывается. Как будто и не было.
                        Console.WriteLine($"{Name}: {ex.Message}");
                    }
                }
            }
            return Task.FromResult(new ProcessResponseReply());
        }

        public override Task<RunRoundReply> RunRound(RunRoundRequest request, ServerCallContext context)
        {
            bool res = false;
            DkgNodeConfig[] configs = request.DkgNodeRefs.Select(nodeRef => new DkgNodeConfig
            {
                Port = nodeRef.Port,
                Host = nodeRef.Host,
                PublicKey = nodeRef.PublicKey,
            }).ToArray();

            lock (stsLock)
            {
                if (request.RoundId == Round)
                {
                    Status = Running;
                    Configs = configs;
                    res = true;
                }
            }

            if (!res)
            {
                _logger.LogError($"RunRound request failed for '{Name}' [Node round: {Round}, Request round: {request.RoundId}]");
            }
            else
            {
                _logger.LogDebug($"RunRound request executed for '{Name}' [Round: {request.RoundId}]");
            }

            return Task.FromResult(new RunRoundReply() { Res = res });
        }

        public override Task<EndRoundReply> EndRound(EndRoundRequest request, ServerCallContext context)
        {
            bool res = false;
            lock (stsLock)
            {
                if (request.RoundId == Round)
                {
                    Status = NotRegistered;
                    Round = null;
                    DistributedPublicKey = null;
                    res = true;
                }
            }

            EndRoundReply endRoundReply = new EndRoundReply() { Res = res };

            if (!res)
            {
                _logger.LogError($"EndRound request failed for '{Name}' [Node round: {Round}, Request round: {request.RoundId}]");
            }
            else
            {
                _logger.LogDebug($"EndRound request executed for '{Name}' [Round: {request.RoundId}]");
            }
            return Task.FromResult(endRoundReply);
        }

        public override Task<RoundResultReply> RoundResult(RoundResultRequest request, ServerCallContext context)
        {
            bool res = false;
            IPoint? dpk = null;
            byte[] distributedPublicKey = [];
            lock (stsLock)
            {
                if (request.RoundId == Round)
                {
                    dpk = DistributedPublicKey;
                    res = true;
                }
            }

            if (res && dpk == null)
            {
                _logger.LogError($"RoundResult request succeeded '{Name}' but distributed public key is not set, round: {request.RoundId}]");
                res = false;
            }

            RoundResultReply roundResultReply = new RoundResultReply() { Res = res };

            if (!res)
            {
                _logger.LogError($"RoundResult request failed for '{Name}' [Node round: {Round}, Request round: {request.RoundId}]");
            }
            else
            {
                distributedPublicKey = dpk!.GetBytes();
                roundResultReply.DistributedPublicKey = ByteString.CopyFrom(distributedPublicKey);
                _logger.LogDebug($"RoundResult request executed for '{Name}' [Round: {request.RoundId}]");
            }
            return Task.FromResult(roundResultReply);
        }

    }
}
