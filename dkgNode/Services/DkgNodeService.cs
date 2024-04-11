using dkg.group;
using dkg.poly;
using dkg.share;
using Google.Protobuf;
using Grpc.Core;
using static dkgNode.DkgNode;

namespace dkgNode.Services
{
    // gRPC сервер
    // Здесь "сложены" параметры узла, которые нужны и клиенту и серверу
    class DkgNodeService : DkgNodeBase
    {
        private IGroup G { get; }
        internal string Name { get; }
        internal IScalar PrivateKey { get; }  // Публичный ключ этого узла
        internal IPoint PublicKey { get; }    // Приватный ключ этого узла  
        internal PriShare? SecretShare { get; set; } = null;

        public DistKeyGenerator? Dkg { get; set; } = null;

        // Защищает Dkg от параллельной обработки наскольких запросов
        internal readonly object dkgLock = new() { };

        // Защищает сообщение, с которым мы работаем
        internal readonly object messageLock = new() { };

        // Есть сообщение, с которым мы работаем
        // Нерасшифрованное входящее соообщение может быть только одно
        internal bool HasMessage { get; set; } = false;

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

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
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
        public override Task<SendMessageReply> SendMessage(SendMessageRequest response, ServerCallContext context)
        {
            string? error = null;
            var c1 = G.Point();
            var c2 = G.Point();

            try
            {
                c1.SetBytes(response.C1.ToByteArray());
                c2.SetBytes(response.C2.ToByteArray());
            }
            catch
            {
                error = "Invalid cipher received, discarded";
            }

            if (error == null)
            {
                lock (messageLock)
                {
                    if (!HasMessage)
                    {
                        C1 = c1;
                        C2 = c2;
                        HasMessage = true;
                    }
                    else
                    {
                        error = "Could not process a second message, discarded";
                    }
                }
            }
            if (error != null)
            {
                Console.WriteLine($"{Name}: {error}");
            }

            return Task.FromResult(new SendMessageReply());
        }
        public override Task<PartialDecryptReply> PartialDecrypt(PartialDecryptRequest response, ServerCallContext context)
        {
            var reply = new PartialDecryptReply();
            if (SecretShare == null)
            {
                Console.WriteLine($"{Name}: could not process partial decrypt request since SecretShare is not set");
            }
            else
            {
                try
                {
                    var c1 = G.Point();
                    var c2 = G.Point();

                    c1.SetBytes(response.C1.ToByteArray());
                    c2.SetBytes(response.C2.ToByteArray());

                    var S = c1.Mul(SecretShare!.V);
                    var partial = c2.Sub(S);
                    reply = new PartialDecryptReply
                    {
                        Partial = ByteString.CopyFrom(partial.GetBytes()
                     )
                    };

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{Name}: could not process partial decrypt request: {ex.Message}");
                }
            }
            return Task.FromResult(reply);
        }
    }
}
