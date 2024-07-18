using System.Text.RegularExpressions;

namespace dkgWebNode.Models
{
    public class DkgStatus
    {
        public string NodeStatus { get; set; } = "Unknown";
        public string NodeRandom { get; set; } = "Unknown";
        public string RoundId { get; set; } = "Unknown";
        public string RoundStatus { get; set; } = "Unknown";
        public string LastRoundId { get; set; } = "Unknown";
        public string LastRoundStatus { get; set; } = "Unknown";
        public string LastRoundResult { get; set; } = "Unknown";
        public string LastNodeStatus { get; set; } = "Unknown";
        public string LastNodeRandom { get; set; } = "Unknown";

    }
}
