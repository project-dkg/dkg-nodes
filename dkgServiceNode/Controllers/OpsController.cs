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
using System.Text;
using static dkgCommon.Constants.NodeStatusConstants;
using static dkgCommon.Constants.RoundStatusConstants;

using Solnet.Wallet;
using System.Diagnostics;

namespace dkgServiceNode.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]

    public class OpsController : DControllerBase
    {
        protected readonly RoundContext roundContext;
        protected readonly NodeCompositeContext ncContext;
        protected readonly Runner runner;
        protected readonly ILogger logger;

        public OpsController(
            IHttpContextAccessor httpContextAccessor,
            UserContext uContext, 
            RoundContext dContext,
            NodeCompositeContext nContext,
            Runner rnner, 
            ILogger<NodesController> lgger) : base(httpContextAccessor, uContext)
        {
            roundContext = dContext;
            ncContext = nContext;
            runner = rnner;
            logger = lgger;
        }


        // POST: api/ops/register
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        public ActionResult<StatusResponse> RegisterNode(Node node)
        {
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
                List<Round> rounds = roundContext.GetAllRounds().Where(r => r.StatusValue == (short)RStatus.Registration).ToList();
                var xNode = ncContext.GetNodeByAddress(node.Address);
                NodesRoundHistory? lastRoundHistory = null;

                if (rounds.Count != 0)
                {
                    logger.LogDebug("{count} round{s} open for registration", rounds.Count, rounds.Count != 1 ? "s" : "");
                    round = rounds[new Random().Next(rounds.Count)];
                    roundId = round.Id;

                    if (xNode == null)
                    {
                        logger.LogDebug("Registering new node for round [{roundId}]", roundId);
                        node.RoundId = roundId;
                        if (roundId == null)
                        {
                            node.StatusValue = (short)NStatus.NotRegistered;
                        }
                        else
                        {
                            node.Name = node.Name;
                            node.RoundId = roundId;
                            node.Status = NStatus.WaitingRegistration;
                            node.PublicKey = node.PublicKey;
                            node.CalculateRandom();
                            ncContext.RegisterNode(node);
                        }
                        xNode = ncContext.GetNodeByAddress(node.Address);
                    }
                    else
                    {
                        logger.LogDebug("Registering known node for round [{roundId}", roundId);

                        xNode.Name = node.Name;
                        xNode.RoundId = roundId;
                        xNode.Status = NStatus.WaitingRoundStart;
                        xNode.PublicKey = node.PublicKey;
                        xNode.CalculateRandom();
                        ncContext.UpdateNode(xNode, true);
                    }

                    if (xNode is not null)
                    {
                        lastRoundHistory = ncContext.GetLastNodeRoundHistory(xNode.Address, roundId ?? 0);
                    }

                    logger.LogDebug("Node registration round [{id}] node [{name}] -> status [{ status }]",
                                        (round != null ? round.Id.ToString() : "null"), node.Name, xNode?.Status ?? NStatus.Unknown);

                    res = Ok(CreateStatusResponse(round, lastRoundHistory, xNode?.Status ?? NStatus.Unknown, node.Random));
                }
                else
                {
                    if (xNode is not null)
                    {
                        lastRoundHistory = ncContext.GetLastNodeRoundHistory(xNode.Address, roundId ?? 0);
                    }
                    logger.LogDebug("No rounds open for registration");
                    res = Ok(CreateStatusResponse(null, lastRoundHistory, NStatus.NotRegistered, node.Random));
                }
            }
            stopwatch.Stop();
            UpdateE2Register(stopwatch.Elapsed);

            return res;
        }

        // Accept action
        // Acknowledges that the status report has been received
        internal async Task<ObjectResult> Accept(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            UpdateNodeState(ncContext, node, stReport.Status, round?.Id);
            if (round != null)
            {
                await UpdateRoundState(round);
            }

            return Accepted(CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }

        internal async Task<ObjectResult> TrToWaitingRoundStartConditional(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            await Task.Delay(0);
            return Ok(CreateStatusResponse(round, lastRoundHistory, node.Status, node.Random));
        }


        internal async Task<ObjectResult> TrToNotRegistered(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round != null)
            {
                runner.SetNoResult(round, node);
            }

            ResetNodeState(ncContext, node);
            await Task.Delay(0);
            var response = CreateStatusResponse(round, lastRoundHistory, NStatus.NotRegistered, node.Random);
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
                ResetNodeState(ncContext, node);
                var response = CreateStatusResponse(round, lastRoundHistory, NStatus.NotRegistered, node.Random);
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
            return Ok(CreateStatusResponseWithData(round, lastRoundHistory, NStatus.RunningStepOne, node.Random, data));
        }
        internal async Task<ObjectResult> TrToRunningStepTwoConditional(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (runner.CheckTimedOutNode(round, node))
            {
                UpdateNodeState(ncContext, node, NStatus.TimedOut, round.Id);
                var response = CreateStatusResponse(round, lastRoundHistory, NStatus.TimedOut, node.Random);
                if (stReport.Status != NStatus.TimedOut)
                {
                    return Ok(response);
                }
            }

            runner.SetStepTwoWaitingTime(round);
            UpdateNodeState(ncContext, node, stReport.Status, round?.Id);

            if (stReport.Data.Length != 0)
            {
                runner.SetStepTwoData(round!, node, stReport.Data);
            }
            if (runner.IsStepTwoDataReady(round!))
            {
                await UpdateRoundState(round!);
                return Ok(CreateStatusResponseWithData(round, lastRoundHistory, 
                                                       NStatus.RunningStepTwo, node.Random, 
                                                       runner.GetStepTwoData(round!, node)));
            }
            return Accepted(CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }
        internal async Task<ObjectResult> TrToRunningStepThreeConditional(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (runner.CheckTimedOutNode(round, node))
            {
                UpdateNodeState(ncContext, node, NStatus.TimedOut, round.Id);
                var response = CreateStatusResponse(round, lastRoundHistory, NStatus.TimedOut, node.Random);
                if (stReport.Status != NStatus.TimedOut)
                {
                    return Ok(response);
                }
            }

            runner.SetStepThreeWaitingTime(round);
            UpdateNodeState(ncContext, node, stReport.Status, round?.Id);

            if (stReport.Data.Length != 0)
            {
                runner.SetStepThreeData(round!, node, stReport.Data);
            }
            if (runner.IsStepThreeDataReady(round!))
            {
                await UpdateRoundState(round!);
                return Ok(CreateStatusResponseWithData(round, lastRoundHistory, 
                                                       NStatus.RunningStepThree, node.Random, 
                                                       runner.GetStepThreeData(round!, node)));
            }
            return Accepted(CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }
        internal async Task<ObjectResult> WrongStatus(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round != null)
            {
                runner.SetNoResult(round, node);
            }

            ResetNodeState(ncContext, node);
            await Task.Delay(0);
            string rStatus = round == null ? "null" : GetRoundStatusById(round.StatusValue).ToString();
            return _409Status(stReport.Address, stReport.Name, GetNodeStatusById(stReport.Status).ToString(), rStatus);
        }
        internal async Task<ObjectResult> AcceptFinished(Round? round, Node node, NodesRoundHistory? lastRoundHistory, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (node.Status == NStatus.Finished)
            {
                return Ok(CreateStatusResponse(round, lastRoundHistory, NStatus.Finished, node.Random));
            }
            else
            {
                runner.SetResultWaitingTime(round);

                if (stReport.Data.Length != 0)
                {
                    runner.SetResult(round, node, stReport.Data);
                    UpdateNodeState(ncContext, node, stReport.Status, round.Id);
                    await UpdateRoundState(round);
                    return Accepted(CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
                }
                else
                {
                    runner.SetNoResult(round, node);
                    UpdateNodeState(ncContext, node, stReport.Status, round.Id);
                    await UpdateRoundState(round);
                    return _400NoResult(round.Id, node.Name, node.PublicKey);
                }
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
            UpdateNodeState(ncContext, node, stReport.Status, round.Id);
            await UpdateRoundState(round);
            
            return Accepted(CreateStatusResponse(round, lastRoundHistory, stReport.Status, node.Random));
        }

        internal StatusResponse CreateStatusResponse(Round? round,
                                                     NodesRoundHistory? lastRoundHistory, 
                                                     NStatus status, int? random)
        {
            int roundId = round != null ? round.Id : 0;
            RStatus roundStatus = round != null ? (RStatus)round.StatusValue : RStatus.Unknown;
            int lastRoundId = lastRoundHistory?.RoundId ?? 0;
            Round? lastRound = lastRoundId == 0 ? null : roundContext.GetRoundById(lastRoundId);
            RStatus lastRoundStatus = lastRound != null ? (RStatus)lastRound.StatusValue : RStatus.Unknown;
            NStatus lastNodeStatus = lastRoundHistory != null ? (NStatus)lastRoundHistory.NodeFinalStatus : NStatus.Unknown;
            int? lastRoundResult = lastRound?.Result;
            int? lastNodeRandom = lastRoundHistory?.NodeRandom;
            return new StatusResponse(roundId, roundStatus, 
                                      lastRoundId, lastRoundStatus, lastRoundResult, 
                                      status, random, lastNodeStatus, lastNodeRandom);
        }

        internal StatusResponse CreateStatusResponseWithData(Round? round, 
                                                             NodesRoundHistory? lastRoundHistory, 
                                                             NStatus status, int? random, string[] data)
        {
            StatusResponse result = CreateStatusResponse(round, lastRoundHistory, status, random);
            result.Data = data;
            return result;
        }

        // POST: api/ops/status
        [HttpPost("status")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
        public async Task<ActionResult<StatusResponse>> Status(StatusReport statusReport)
        {
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

                { (null, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.NotStarted, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.Registration, NStatus.WaitingRegistration), TrToWaitingRoundStartConditional },
                { (RStatus.CreatingDeals, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.ProcessingDeals, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.ProcessingResponses, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.Finished, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.Cancelled, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.Failed, NStatus.WaitingRegistration), WrongStatus },
                { (RStatus.Unknown, NStatus.WaitingRegistration), WrongStatus },

                { (null, NStatus.WaitingRoundStart), WrongStatus },
                { (RStatus.NotStarted, NStatus.WaitingRoundStart), WrongStatus },
                { (RStatus.Registration, NStatus.WaitingRoundStart), TrToWaitingRoundStartConditional },
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

            var node = ncContext.GetNodeByAddress(statusReport.Address);
            if (node == null)
            {
                res = _404Node(statusReport.Address, statusReport.Name);
            }
            else
            {
                var round = statusReport.RoundId == 0 ? null : roundContext.GetRoundById(statusReport.RoundId);
                var lastRoundHistory = ncContext.GetLastNodeRoundHistory(node.Address, statusReport.RoundId);

                RStatus? rStatus = null;
                if (round != null)
                {
                    // await UpdateRoundState(round);
                    rStatus = round.Status;
                }

                if (actionMap.TryGetValue((rStatus, statusReport.Status), out var function))
                {
                    logger.LogDebug("State transition round [{id}] node [{name}] : ({rStatus}, {nStatus}) -> {f}(Data: {data})",
                                        (round != null ? round.Id.ToString() : "null"),
                                        node.Name, rStatus, statusReport.Status, function.Method.Name,
                                       statusReport.Data.Length == 0 ? "empty" : statusReport.Data);
                    res = await function(round, node, lastRoundHistory, statusReport);

                }
                else
                {
                    res =  _500UnknownStateTransition(rStatus == null ? "null" : GetRoundStatusById((short)rStatus).ToString(), 
                                                      GetNodeStatusById((short)node.Status).ToString());
                }
            }
            stopwatch.Stop();
            UpdateE2Status(stopwatch.Elapsed);
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
                try
                {
                    await roundContext.UpdateRoundAsync(round);
                }
                catch
                {
                }
            }
        }
    }
}