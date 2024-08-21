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

using dkgCommon.Constants;
using dkgCommon.Models;
using dkgServiceNode.Data;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.RoundRunner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using static dkgCommon.Constants.NodeStatusConstants;
using static dkgCommon.Constants.RoundStatusConstants;

using Solnet.Wallet;
using System.Xml.Linq;
using static NpgsqlTypes.NpgsqlTsQuery;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.AspNetCore.Routing;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace dkgServiceNode.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]

    public class NodesController : DControllerBase
    {
        protected readonly DkgContext dkgContext;
        protected readonly Runner runner;
        protected readonly ILogger logger;

        private static UInt64 c2Fetch = 0;
        private static UInt64 c2Get = 0;
        private static UInt64 c2GetAll = 0;
        private static UInt64 c2Register = 0;
        private static UInt64 c2Status = 0;
        private static UInt64 c2Reset = 0;
        private static UInt64 c2Delete = 0;
        private static UInt64 c2Statistics = 0;

        private static TimeSpan e2Fetch = new();
        private static readonly object e2FetchLock = new();

        private static TimeSpan e2Get = new();
        private static readonly object e2GetLock = new();

        private static TimeSpan e2GetAll = new();
        private static readonly object e2GetAllLock = new();

        private static TimeSpan e2Register = new();
        private static readonly object e2RegisterLock = new();

        private static TimeSpan e2Status = new();
        private static readonly object e2StatusLock = new();

        private static TimeSpan e2Reset = new();
        private static readonly object e2ResetLock = new();

        private static TimeSpan e2Delete = new();
        private static readonly object e2DeleteLock = new();

        private static TimeSpan e2Statistics = new();
        private static readonly object e2StatisticsLock = new();

        public NodesController(IHttpContextAccessor httpContextAccessor,
                               UserContext uContext, DkgContext dContext,
                               Runner rnner, ILogger<NodesController> lgger) :
               base(httpContextAccessor, uContext)
        {
            dkgContext = dContext;
            runner = rnner;
            logger = lgger;
        }

        // GET: api/nodes/fetch
        [HttpPost("fetch")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NodesFrameResult))]
        public async Task<ActionResult<NodesFrameResult>> FetchNodes(NodesFrame nodesFrame)
        {
            Interlocked.Increment(ref c2Fetch);

            Stopwatch stopwatch = new();
            stopwatch.Start();

            var nf = await dkgContext.Nodes
                .ToListAsync();

            var sf = nf.Count;

            nf = nf.Where(n =>
                n.Name.Contains(nodesFrame.Search) ||
                n.Id.ToString().Contains(nodesFrame.Search) ||
                n.Address.Contains(nodesFrame.Search) ||
                (n.RoundId.ToString() != null && n.RoundId.ToString()!.Contains(nodesFrame.Search)) ||
                (n.RoundId == null && ("null".Contains(nodesFrame.Search) ||
                                       "--".Contains(nodesFrame.Search))) ||
                NodeStatusConstants.GetNodeStatusById(n.StatusValue).ToString().Contains(nodesFrame.Search))
                .ToList();

            if (nodesFrame.SortBy != null && nodesFrame.SortBy.Length > 0)
            {
                // Sort nf based on nodesFrame.SortOrder[0].key and nodesFrame.SortOrder[0].order
                var sortKey = nodesFrame.SortBy[0].Key.ToLower();
                var sortOrder = nodesFrame.SortBy[0].Order.ToLower();

                switch (sortKey)
                {
                    case "name":
                        nf = sortOrder == "asc" ? [.. nf.OrderBy(n => n.Name)] : [.. nf.OrderByDescending(n => n.Name)];
                        break;
                    case "id":
                        nf = sortOrder == "asc" ? [.. nf.OrderBy(n => n.Id)] : [.. nf.OrderByDescending(n => n.Id)];
                        break;
                    case "address":
                        nf = sortOrder == "asc" ? [.. nf.OrderBy(n => n.Address)] : [.. nf.OrderByDescending(n => n.Address)];
                        break;
                    case "roundid":
                        nf = sortOrder == "asc" ? [.. nf.OrderBy(n => n.RoundId)] : [.. nf.OrderByDescending(n => n.RoundId)];
                        break;
                    case "status":
                        nf = sortOrder == "asc" ? [.. nf.OrderBy(n => n.StatusValue)] : [.. nf.OrderByDescending(n => n.StatusValue)];
                        break;
                    default:
                        // Invalid sort key, do not perform sorting
                        break;
                }
            }
            nf = nf.Skip(nodesFrame.Page * nodesFrame.ItemsPerPage)
                .Take(nodesFrame.ItemsPerPage)
                .ToList();

            NodesFrameResult res = new()
            {
                TotalNodes = sf,
                NodesFrame = nf
            };

            stopwatch.Stop();
            lock (e2FetchLock)
            {
                e2Fetch += stopwatch.Elapsed;
            } 
            return res;
        }

        // GET: api/nodes/statistics
        [HttpGet("statistics")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<TimingResult>))]
        public async Task<ActionResult<IEnumerable<TimingResult>>> GetStatistics()
        {
            Interlocked.Increment(ref c2Statistics);
            Stopwatch stopwatch = new();
            stopwatch.Start();
            
            ActionResult<IEnumerable<TimingResult>> res;
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value)
            {
                res = _403();
            }
            else
            {
                List<TimingResult> timingResults = [];

                double v;
                lock (e2FetchLock)
                {
                    v = c2Fetch == 0 ? 0 : e2Fetch.TotalMilliseconds / (double)c2Fetch;
                }

                timingResults.Add(new TimingResult()
                {
                    Name = "fetch",
                    Count = Interlocked.Read(ref c2Fetch),
                    TimePerCall = v
                });

                lock (e2GetLock)
                {
                    v = c2Get == 0 ? 0 : e2Get.TotalMilliseconds / (double)c2Get;
                }
                timingResults.Add(new TimingResult()
                {
                    Name = "get",
                    Count = Interlocked.Read(ref c2Get),
                    TimePerCall = v
                });

                lock (e2GetAllLock)
                {
                    v = c2GetAll == 0 ? 0 : e2GetAll.TotalMilliseconds / (double)c2GetAll;
                }
                timingResults.Add(new TimingResult()
                {
                    Name = "get all",
                    Count = Interlocked.Read(ref c2GetAll),
                    TimePerCall = v
                });

                lock (e2RegisterLock)
                {
                    v = c2Register == 0 ? 0 : e2Register.TotalMilliseconds / (double)c2Register;
                }
                timingResults.Add(new TimingResult()
                {
                    Name = "register",
                    Count = Interlocked.Read(ref c2Register),
                    TimePerCall = v
                });

                lock (e2StatusLock)
                {
                    v = c2Status == 0 ? 0 : e2Status.TotalMilliseconds / (double)c2Status;
                }
                timingResults.Add(new TimingResult()
                {
                    Name = "status",
                    Count = Interlocked.Read(ref c2Status),
                    TimePerCall = v
                });

                lock (e2ResetLock)
                {
                    v = c2Reset == 0 ? 0 : e2Reset.TotalMilliseconds / (double)c2Reset;
                }
                timingResults.Add(new TimingResult()
                {
                    Name = "reset",
                    Count = Interlocked.Read(ref c2Reset),
                    TimePerCall = v
                });

                lock (e2DeleteLock)
                {
                    v = c2Delete == 0 ? 0 : e2Delete.TotalMilliseconds / (double)c2Delete;
                }
                timingResults.Add(new TimingResult()
                {
                    Name = "delete",
                    Count = Interlocked.Read(ref c2Delete),
                    TimePerCall = v
                });

                lock (e2StatisticsLock)
                {
                    v = c2Statistics == 0 ? 0 : e2Statistics.TotalMilliseconds / (double)c2Statistics;
                }
                timingResults.Add(new TimingResult()
                {
                    Name = "statistics",
                    Count = Interlocked.Read(ref c2Statistics),
                    TimePerCall = v
                });
                res = Ok(timingResults);
            }

            stopwatch.Stop();
            lock (e2StatisticsLock)
            {
                e2Statistics += stopwatch.Elapsed;
            }
            return res;
        }

        // GET: api/nodes
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Node>))]
        public async Task<ActionResult<IEnumerable<Node>>> GetNodes()
        {
            Interlocked.Increment(ref c2GetAll);
            Stopwatch stopwatch = new();
            stopwatch.Start();

            var res = await dkgContext.Nodes.OrderBy(n => n.Id).ToListAsync();

            stopwatch.Stop();
            lock (e2GetAllLock)
            {
                e2GetAll += stopwatch.Elapsed;
            }
            return res;
        }

        // GET: api/nodes/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Node))]
        public async Task<ActionResult<Node>> GetNode(int id)
        {
            c2Get++;
            Stopwatch stopwatch = new();
            stopwatch.Start();
            
            var node = await dkgContext.Nodes.FindAsync(id);
            if (node == null) return _404Node(id);
            
            stopwatch.Stop();
            lock (e2GetLock)
            {
                e2Get += stopwatch.Elapsed;
            }
            return node;

        }

        // POST: api/Nodes/register
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        public async Task<ActionResult<StatusResponse>> RegisterNode(Node node)
        {

            Interlocked.Increment(ref c2Register);
            Stopwatch stopwatch = new();
            stopwatch.Start();

            ActionResult<StatusResponse> res;
            bool verified = false;

            try
            {
                verified = new PublicKey(node.Address)
                    .Verify(
                        Encoding.UTF8.GetBytes($"{node.Address}{node.PublicKey}{node.Name}"),
                        Convert.FromBase64String(node.Signature)
                    );
                logger.LogInformation("Verified node [{name}] signature", node.Name);
            }
            catch (Exception ex)
            {
                logger.LogInformation("Failed to verify node [{name}] signature: {ex.Message}", node.Name, ex.Message);
            }

            if (!verified)
            {
                res = _403InvalidSignature();
            }
            else
            {

                int? roundId = null;
                Round? round = null;
                List<Round> rounds = await dkgContext.Rounds.Where(r => r.StatusValue == (short)RStatus.Registration).ToListAsync();
                if (rounds.Count != 0)
                {
                    round = rounds[new Random().Next(rounds.Count)];
                    roundId = round.Id;
                }

                var xNode = await dkgContext.FindNodeByAddressAsync(node.Address);
                if (xNode == null)
                {
                    node.RoundId = roundId;
                    node.CalculateRandom();
                    if (roundId == null) node.StatusValue = (short)NStatus.NotRegistered;
                    dkgContext.Nodes.Add(node);
                    await dkgContext.SaveChangesAsync();
                    xNode = await dkgContext.FindNodeByAddressAsync(node.Address);
                }
                else
                {
                    bool modified = false;
                    if (xNode.Name != node.Name)
                    {
                        xNode.Name = node.Name;
                        modified = true;
                    }

                    if (xNode.RoundId != roundId)
                    {
                        xNode.RoundId = roundId;
                        modified = true;
                    }

                    if (xNode.PublicKey != node.PublicKey)
                    {
                        xNode.PublicKey = node.PublicKey;
                        xNode.CalculateRandom();
                        modified = true;
                    }

                    if (roundId == null)
                    {
                        if (xNode.StatusValue != (short)NStatus.NotRegistered)
                        {
                            xNode.StatusValue = (short)NStatus.NotRegistered;
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        dkgContext.Entry(xNode).State = EntityState.Modified;
                        await dkgContext.SaveChangesAsync();
                    }
                }

                NodesRoundHistory? lastRoundHistory = null;
                if (xNode is not null)
                {
                    lastRoundHistory = await dkgContext.GetLastNodeRoundHistory(xNode.Id, roundId ?? 0);
                }

                await CreateStatusResponse(round, lastRoundHistory, xNode?.Status ?? NStatus.Unknown, node.Random);
                logger.LogDebug("Node registration round [{id}] node [{name}] -> status [{ status }]",
                                    (round != null ? round.Id.ToString() : "null"), node.Name, xNode?.Status ?? NStatus.Unknown);

                res = Ok(await CreateStatusResponse(round, lastRoundHistory, xNode?.Status ?? NStatus.Unknown, node.Random));
            }

            stopwatch.Stop();
            lock (e2RegisterLock)
            {
                e2Register += stopwatch.Elapsed;
            }

            return res;
        }

        // Accept action
        // Acknowledges that the status report has been received
        internal async Task<ObjectResult> Accept(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            await UpdateNodeState(dkgContext, node, (short)stReport.Status, round?.Id);
            if (round != null)
            {
                await UpdateRoundState(round);
            }

            return Accepted(await CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }

        internal async Task<ObjectResult> TrToNotRegistered(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round != null)
            {
                runner.SetNoResult(round, node);
            }

            await ResetNodeState(dkgContext, node);
            var response = await CreateStatusResponse(round, lastRoundHistory, NStatus.NotRegistered, node.Random);
            if (stReport.Status != NStatus.NotRegistered || stReport.RoundId != 0)
            {
                return Ok(response);
            }
            return Accepted(response);
        }

        internal async Task<ObjectResult> TrToRunningStepOne(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (!runner.CheckNode(round, node))
            {
                await ResetNodeState(dkgContext, node);
                var response = await CreateStatusResponse(round, lastRoundHistory, NStatus.NotRegistered, node.Random);
                if (stReport.Status != NStatus.NotRegistered || stReport.RoundId != 0)
                {
                    return Ok(response);
                }
            }

            await Task.Delay(0);
            string[] data = runner.GetStepOneData(round!);
            if (data.Length == 0)
            {
                return _500MisssingStepOneData(round.Id, GetRoundStatusById(round.StatusValue).ToString());
            }
            return Ok(await CreateStatusResponseWithData(round, lastRoundHistory, NStatus.RunningStepOne, node.Random, data));
        }
        internal async Task<ObjectResult> TrToRunningStepTwoConditional(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (runner.CheckTimedOutNode(round, node))
            {
                await UpdateNodeState(dkgContext, node, (short)NStatus.TimedOut, round.Id);
                var response = await CreateStatusResponse(round, lastRoundHistory, NStatus.TimedOut, node.Random);
                if (stReport.Status != NStatus.TimedOut)
                {
                    return Ok(response);
                }
            }

            runner.SetStepTwoWaitingTime(round);
            await UpdateNodeState(dkgContext, node, (short)stReport.Status, round?.Id);

            if (stReport.Data.Length != 0)
            {
                runner.SetStepTwoData(round!, node, stReport.Data);
            }
            if (runner.IsStepTwoDataReady(round!))
            {
                await UpdateRoundState(round!);
                return Ok(await CreateStatusResponseWithData(round, lastRoundHistory, 
                                                             NStatus.RunningStepTwo, node.Random, 
                                                             runner.GetStepTwoData(round!, node)));
            }
            return Accepted(await CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }
        internal async Task<ObjectResult> TrToRunningStepThreeConditional(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (runner.CheckTimedOutNode(round, node))
            {
                await UpdateNodeState(dkgContext, node, (short)NStatus.TimedOut, round.Id);
                var response = await CreateStatusResponse(round, lastRoundHistory, NStatus.TimedOut, node.Random);
                if (stReport.Status != NStatus.TimedOut)
                {
                    return Ok(response);
                }
            }

            runner.SetStepThreeWaitingTime(round);
            await UpdateNodeState(dkgContext, node, (short)stReport.Status, round?.Id);

            if (stReport.Data.Length != 0)
            {
                runner.SetStepThreeData(round!, node, stReport.Data);
            }
            if (runner.IsStepThreeDataReady(round!))
            {
                await UpdateRoundState(round!);
                return Ok(await CreateStatusResponseWithData(round, lastRoundHistory, 
                                                             NStatus.RunningStepThree, node.Random, 
                                                             runner.GetStepThreeData(round!, node)));
            }
            return Accepted(await CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }
        internal async Task<ObjectResult> WrongStatus(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round != null)
            {
                runner.SetNoResult(round, node);
            }

            await ResetNodeState(dkgContext, node);
            string rStatus = round == null ? "null" : GetRoundStatusById(round.StatusValue).ToString();
            return _409Status(stReport.PublicKey, stReport.Name, GetNodeStatusById(stReport.Status).ToString(), rStatus);
        }
        internal async Task<ObjectResult> AcceptFinished(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            runner.SetResultWaitingTime(round);

            if (stReport.Data.Length != 0)
            {
                runner.SetResult(round, node, stReport.Data);
                await UpdateNodeState(dkgContext, node, (short)stReport.Status, round.Id);
                await UpdateRoundState(round);
                return Accepted(await CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
            }
            else
            {
                runner.SetNoResult(round, node);
                await UpdateNodeState(dkgContext, node, (short)stReport.Status, round.Id);
                await UpdateRoundState(round);
                return _400NoResult(round.Id, node.Name, node.PublicKey);
            }
        }
        internal async Task<ObjectResult> AcceptFailed(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            runner.SetResultWaitingTime(round);

            runner.SetNoResult(round, node);
            await UpdateNodeState(dkgContext, node, (short)stReport.Status, round.Id);
            await UpdateRoundState(round);
            
            return Accepted(await CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }

        internal async Task<StatusResponse> CreateStatusResponse(Round? round,
                                                            NodesRoundHistory? lastRoundHistory, 
                                                            NStatus status, int? random)
        {
            int roundId = round != null ? round.Id : 0;
            RStatus roundStatus = round != null ? (RStatus)round.StatusValue : RStatus.Unknown;
            int lastRoundId = lastRoundHistory?.RoundId ?? 0;
            Round? lastRound = lastRoundId == 0 ? null : await dkgContext.Rounds.FirstOrDefaultAsync(r => r.Id == lastRoundId);
            RStatus lastRoundStatus = lastRound != null ? (RStatus)lastRound.StatusValue : RStatus.Unknown;
            NStatus lastNodeStatus = lastRoundHistory != null ? (NStatus)lastRoundHistory.NodeFinalStatus : NStatus.Unknown;
            int? lastRoundResult = lastRound?.Result;
            int? lastNodeRandom = lastRoundHistory?.NodeRandom;
            return new StatusResponse(roundId, roundStatus, 
                                      lastRoundId, lastRoundStatus, lastRoundResult, 
                                      status, random, lastNodeStatus, lastNodeRandom);
        }

        internal async Task<StatusResponse> CreateStatusResponseWithData(Round? round, 
                                                                    NodesRoundHistory? lastRoundHistory, 
                                                                    NStatus status, int? random, string[] data)
        {
            StatusResponse result = await CreateStatusResponse(round, lastRoundHistory, status, random);
            result.Data = data;
            return result;
        }

        // POST: api/Nodes/status
        [HttpPost("status")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
        public async Task<ActionResult<StatusResponse>> Status(StatusReport statusReport)
        {
            Interlocked.Increment(ref c2Status);
            Stopwatch stopwatch = new();
            stopwatch.Start();

            ActionResult<StatusResponse> res;

            var actionMap = new Dictionary<(RStatus?, NStatus), Func<Round?, Node, NodesRoundHistory?, StatusReport, Task<ObjectResult>>>()
            {
                { (null, NStatus.NotRegistered), Accept },
                { (RStatus.NotStarted, NStatus.NotRegistered), WrongStatus },
                { (RStatus.Registration, NStatus.NotRegistered), Accept },
                { (RStatus.CreatingDeals, NStatus.NotRegistered), WrongStatus },
                { (RStatus.ProcessingDeals, NStatus.NotRegistered), WrongStatus },
                { (RStatus.ProcessingResponses, NStatus.NotRegistered), WrongStatus },
                { (RStatus.Finished, NStatus.NotRegistered), WrongStatus },
                { (RStatus.Cancelled, NStatus.NotRegistered), WrongStatus },
                { (RStatus.Failed, NStatus.NotRegistered), WrongStatus },
                { (RStatus.Unknown, NStatus.NotRegistered), WrongStatus },

                { (null, NStatus.WaitingRoundStart), WrongStatus },
                { (RStatus.NotStarted, NStatus.WaitingRoundStart), WrongStatus },
                { (RStatus.Registration, NStatus.WaitingRoundStart), Accept },
                { (RStatus.CreatingDeals, NStatus.WaitingRoundStart), TrToRunningStepOne },
                { (RStatus.ProcessingDeals, NStatus.WaitingRoundStart), TrToNotRegistered },
                { (RStatus.ProcessingResponses, NStatus.WaitingRoundStart), TrToNotRegistered },
                { (RStatus.Finished, NStatus.WaitingRoundStart), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.WaitingRoundStart), TrToNotRegistered },
                { (RStatus.Failed, NStatus.WaitingRoundStart), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.WaitingRoundStart), TrToNotRegistered },

                { (null, NStatus.RunningStepOne), WrongStatus },
                { (RStatus.NotStarted, NStatus.RunningStepOne), WrongStatus },
                { (RStatus.Registration, NStatus.RunningStepOne), WrongStatus },
                { (RStatus.CreatingDeals, NStatus.RunningStepOne), Accept },
                { (RStatus.ProcessingDeals, NStatus.RunningStepOne), Accept },
                { (RStatus.ProcessingResponses, NStatus.RunningStepOne), TrToNotRegistered },
                { (RStatus.Finished, NStatus.RunningStepOne), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.RunningStepOne), TrToNotRegistered },
                { (RStatus.Failed, NStatus.RunningStepOne), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.RunningStepOne), TrToNotRegistered },

                { (null, NStatus.WaitingStepTwo), WrongStatus },
                { (RStatus.NotStarted, NStatus.WaitingStepTwo), WrongStatus },
                { (RStatus.Registration, NStatus.WaitingStepTwo), WrongStatus },
                { (RStatus.CreatingDeals, NStatus.WaitingStepTwo), TrToRunningStepTwoConditional },
                { (RStatus.ProcessingDeals, NStatus.WaitingStepTwo), TrToRunningStepTwoConditional },
                { (RStatus.ProcessingResponses, NStatus.WaitingStepTwo), TrToNotRegistered },
                { (RStatus.Finished, NStatus.WaitingStepTwo), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.WaitingStepTwo), TrToNotRegistered },
                { (RStatus.Failed, NStatus.WaitingStepTwo), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.WaitingStepTwo), TrToNotRegistered },

                { (null, NStatus.RunningStepTwo), WrongStatus },
                { (RStatus.NotStarted, NStatus.RunningStepTwo), WrongStatus },
                { (RStatus.Registration, NStatus.RunningStepTwo), WrongStatus },
                { (RStatus.CreatingDeals, NStatus.RunningStepTwo), WrongStatus },
                { (RStatus.ProcessingDeals, NStatus.RunningStepTwo), Accept },
                { (RStatus.ProcessingResponses, NStatus.RunningStepTwo), Accept },
                { (RStatus.Finished, NStatus.RunningStepTwo), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.RunningStepTwo), TrToNotRegistered },
                { (RStatus.Failed, NStatus.RunningStepTwo), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.RunningStepTwo), TrToNotRegistered },

                { (null, NStatus.WaitingStepThree), WrongStatus },
                { (RStatus.NotStarted, NStatus.WaitingStepThree), WrongStatus },
                { (RStatus.Registration, NStatus.WaitingStepThree), WrongStatus },
                { (RStatus.CreatingDeals, NStatus.WaitingStepThree), WrongStatus },
                { (RStatus.ProcessingDeals, NStatus.WaitingStepThree), TrToRunningStepThreeConditional },
                { (RStatus.ProcessingResponses, NStatus.WaitingStepThree), TrToRunningStepThreeConditional },
                { (RStatus.Finished, NStatus.WaitingStepThree), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.WaitingStepThree), TrToNotRegistered },
                { (RStatus.Failed, NStatus.WaitingStepThree), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.WaitingStepThree), TrToNotRegistered },

                { (null, NStatus.RunningStepThree), WrongStatus },
                { (RStatus.NotStarted, NStatus.RunningStepThree), WrongStatus },
                { (RStatus.Registration, NStatus.RunningStepThree), WrongStatus },
                { (RStatus.CreatingDeals, NStatus.RunningStepThree), WrongStatus },
                { (RStatus.ProcessingDeals, NStatus.RunningStepThree), WrongStatus },
                { (RStatus.ProcessingResponses, NStatus.RunningStepThree), Accept },
                { (RStatus.Finished, NStatus.RunningStepThree), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.RunningStepThree), TrToNotRegistered },
                { (RStatus.Failed, NStatus.RunningStepThree), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.RunningStepThree), TrToNotRegistered },

                { (null, NStatus.Finished), WrongStatus },
                { (RStatus.NotStarted, NStatus.Finished), WrongStatus },
                { (RStatus.Registration, NStatus.Finished), WrongStatus },
                { (RStatus.CreatingDeals, NStatus.Finished), WrongStatus },
                { (RStatus.ProcessingDeals, NStatus.Finished), WrongStatus },
                { (RStatus.ProcessingResponses, NStatus.Finished), AcceptFinished },
                { (RStatus.Finished, NStatus.Finished), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.Finished), TrToNotRegistered },
                { (RStatus.Failed, NStatus.Finished), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.Finished), TrToNotRegistered },

                { (null, NStatus.Failed), WrongStatus },
                { (RStatus.NotStarted, NStatus.Failed), WrongStatus },
                { (RStatus.Registration, NStatus.Failed), TrToNotRegistered },
                { (RStatus.CreatingDeals, NStatus.Failed), TrToNotRegistered },
                { (RStatus.ProcessingDeals, NStatus.Failed), TrToNotRegistered },
                { (RStatus.ProcessingResponses, NStatus.Failed), AcceptFailed },
                { (RStatus.Finished, NStatus.Failed), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.Failed), TrToNotRegistered },
                { (RStatus.Failed, NStatus.Failed), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.Failed), TrToNotRegistered },

                { (null, NStatus.TimedOut), WrongStatus },
                { (RStatus.NotStarted, NStatus.TimedOut), WrongStatus },
                { (RStatus.Registration, NStatus.TimedOut), TrToNotRegistered },
                { (RStatus.CreatingDeals, NStatus.TimedOut), TrToNotRegistered },
                { (RStatus.ProcessingDeals, NStatus.TimedOut), TrToNotRegistered },
                { (RStatus.ProcessingResponses, NStatus.TimedOut), TrToNotRegistered },
                { (RStatus.Finished, NStatus.TimedOut), TrToNotRegistered },
                { (RStatus.Cancelled, NStatus.TimedOut), TrToNotRegistered },
                { (RStatus.Failed, NStatus.TimedOut), TrToNotRegistered },
                { (RStatus.Unknown, NStatus.TimedOut), TrToNotRegistered },

            };

            var node = await dkgContext.FindNodeByPublicKeyAsync(statusReport.PublicKey);
            if (node == null)
            {
                res = _404Node(statusReport.PublicKey, statusReport.Name);
            }
            else
            {
                var round = statusReport.RoundId == 0 ? null : await dkgContext.Rounds.FirstOrDefaultAsync(r => r.Id == statusReport.RoundId);
                var lastRoundHistory = await dkgContext.GetLastNodeRoundHistory(node.Id, statusReport.RoundId);

                RStatus? rStatus = null;
                if (round != null) rStatus = round.Status;

                if (actionMap.TryGetValue((rStatus, statusReport.Status), out var function))
                {
                    logger.LogDebug("State transition round [{id}] node [{name}] : ({rStatus}, {nStatus}) -> {f}",
                                        (round != null ? round.Id.ToString() : "null"),
                                        node.Name, rStatus, statusReport.Status, function.Method.Name);
                    res = await function(round, node, lastRoundHistory, statusReport);

                }
                else
                {
                    res =  _500UnknownStateTransition(rStatus == null ? "null" : GetRoundStatusById((short)rStatus).ToString(), 
                                                      GetNodeStatusById((short)node.Status).ToString());
                }
            }
            stopwatch.Stop();
            lock (e2StatusLock)
            {
                e2Status += stopwatch.Elapsed;
            }
            return res;
        }

        // RESET: api/nodes/reset/5
        [HttpPost("reset/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ResetNode(int id)
        {
            Interlocked.Increment(ref c2Reset);
            Stopwatch stopwatch = new();
            stopwatch.Start();
            IActionResult res;
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value)
            {
                res = _403();
            }
            else
            {
                var node = await dkgContext.Nodes.FindAsync(id);
                if (node == null)
                {
                    res = _404Node(id);
                }
                else
                {

                    node.StatusValue = (short)NStatus.NotRegistered;
                    node.RoundId = null;
                    dkgContext.Entry(node).State = EntityState.Modified;
                    await dkgContext.SaveChangesAsync();

                    res = NoContent();
                }
            }
            stopwatch.Stop();
            lock (e2ResetLock)
            {
                e2Reset += stopwatch.Elapsed;
            }
            return res;
        }

        // DELETE: api/nodes/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteNode(int id)
        {
            Interlocked.Increment(ref c2Reset);
            Stopwatch stopwatch = new();
            stopwatch.Start();

            IActionResult res;

            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value)
            {
                res = _403();
            }
            else
            {


                var node = await dkgContext.Nodes.FindAsync(id);
                if (node == null)
                {
                    res = _404Node(id);
                }
                else
                {
                    dkgContext.Nodes.Remove(node);
                    await dkgContext.SaveChangesAsync();

                    res = NoContent();
                }
            }
            stopwatch.Stop();
            lock (e2DeleteLock)
            {
                e2Delete += stopwatch.Elapsed;
            }
            return res;
        }

        internal async Task UpdateRoundState(Round round)
        {
            RStatus status = (RStatus)round.StatusValue;
            if (runner.IsResultReady(round))
            {
                round.Result = runner.FinishRound(round);
                if (round.Result == null)
                {
                    status = RStatus.Failed;
                }
                else
                {
                    status = RStatus.Finished;
                }
            }
            else
            {
                if (runner.IsStepTwoDataReady(round))
                {
                    status = RStatus.ProcessingDeals;
                }
                if (runner.IsStepThreeDataReady(round))
                {
                    status = RStatus.ProcessingResponses;
                }
            }
            if (status != (RStatus)round.StatusValue)
            {
                round.StatusValue = (short)status;
                round.CreatedOn = round.CreatedOn.ToUniversalTime();
                round.ModifiedOn = DateTime.Now.ToUniversalTime();
                dkgContext.Entry(round).State = EntityState.Modified;
                try
                {
                    await dkgContext.SaveChangesAsync();
                }
                catch
                {
                }
            }
        }

    }
}