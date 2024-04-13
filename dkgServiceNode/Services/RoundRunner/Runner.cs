using dkgCommon;
using dkgServiceNode.Models;
using Grpc.Net.Client;
using static dkgCommon.DkgNode;


namespace dkgServiceNode.Services.RoundRunner
{
    public class ActiveRound
    {
        public int Id
        {
            get { return Round.Id; }
        }
        public GrpcChannel[]? Channels { get; set; } = null;
        public DkgNodeClient[]? DkgNodes { get; set; } = null;
        internal Round Round { get; set; }
        public ActiveRound(Round round)
        {
            Round = round;
        }

        public void Init(List<Node> nodes)
        {
            Channels = new GrpcChannel[nodes.Count];
            DkgNodes = new DkgNodeClient[nodes.Count];
            int j = 0;
            foreach (Node node in nodes)
            {
                Channels[j] = GrpcChannel.ForAddress($"http://{node.Host}:{node.Port}");
                DkgNodes[j] = new DkgNodeClient(Channels[j]);  // ChannelCredentials.Insecure ???
                j++;
            }
        }

        public void Run(List<Node> nodes)
        {
            Init(nodes);
            var runRoundRequest = new RunRoundRequest
            {
                RoundId = Id
            };

            foreach (var node in nodes)
            {
                runRoundRequest.DkgNodeRefs.Add(new DkgNodeRef
                {
                    Port = node.Port,
                    Host = node.Host,
                    PublicKey = node.PublicKey, 
                });
            }

            List<Task> startRounsTasks = [];
            for (int j = 0; j < DkgNodes?.Length; j++)
            {
                startRounsTasks.Add(DkgNodes[j].RunRoundAsync(runRoundRequest).ResponseAsync);
            }

            Task.WaitAll([.. startRounsTasks]);

        }

        public int? GetResult()
        {
            if (DkgNodes == null)
            {
                return null;
            }

            foreach (var dkgNode in DkgNodes)
            {
                var roundResultRequest = new RoundResultRequest
                {
                    RoundId = Round.Id
                };

                RoundResultReply roundResultReply = dkgNode.RoundResult(roundResultRequest); 

                if (roundResultReply.Res)
                {
                    int value = BitConverter.ToInt32(roundResultReply.DistributedPublicKey.ToByteArray(), 0);
                    return value;
                }
            }

            return null;
        }
        public void Clear()
        {
            List<Task> shutdownTasks = [];
            for (int j = 0; j < DkgNodes?.Length; j++)
            {
                shutdownTasks.Add(DkgNodes[j].EndRoundAsync(new EndRoundRequest { RoundId = Id }).ResponseAsync);
            }
            Task.WaitAll([.. shutdownTasks]);

            shutdownTasks.Clear();
            for (int i = 0; i < Channels?.Length; i++)
            {
                shutdownTasks.Add(Channels[i].ShutdownAsync());
            }
            Task.WaitAll([.. shutdownTasks]);
        }
    }


    public static class Runner
    {
        private static List<ActiveRound> ActiveRounds { get; set; } = [];
        private static readonly object lockObject = new();

        public static void StartRound(Round round)
        {
            lock (lockObject)
            {
                ActiveRounds.Add(new ActiveRound(round));
            }
        }

        public static void RunRound(Round round, List<Node>? nodes)
        {
            ActiveRound? roundToRun = null;
            lock (lockObject)
            {
                roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
            }
            if (roundToRun != null && nodes != null)
            {
                roundToRun.Run(nodes);
            }
        }

        public static int? GetRoundResult(Round round)
        {
            ActiveRound? roundToRun = null;
            lock (lockObject)
            {
                roundToRun = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
            }
            if (roundToRun != null)
            {
                return roundToRun.GetResult();
            }
            else
            {
                return null;
            }
        }

        public static int? FinishRound(Round round, List<Node>? nodes)
        {
            int? result = GetRoundResult(round);
            RemoveRound(round, nodes);
            return result;
        }

        public static void CancelRound(Round round, List<Node>? nodes)
        {
            RemoveRound(round, nodes);
        }
        internal static void RemoveRound(Round round, List<Node>? nodes)
        {
            ActiveRound? roundToRemove = null;
            lock (lockObject)
            {
                roundToRemove = ActiveRounds.FirstOrDefault(r => r.Id == round.Id);
                if (roundToRemove != null)
                {
                    ActiveRounds.Remove(roundToRemove);
                }
            }
            if (roundToRemove != null)
            {
                if (roundToRemove.DkgNodes == null && nodes != null)
                {
                    roundToRemove.Init(nodes);
                }
                roundToRemove.Clear();
            }
        }
    }
}
