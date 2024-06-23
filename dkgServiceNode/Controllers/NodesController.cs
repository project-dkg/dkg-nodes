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
using dkgServiceNode.Constants;
using dkgServiceNode.Data;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.RoundRunner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
using static dkgCommon.Constants.NodeStatusConstants;
using static dkgServiceNode.Constants.RoundStatusConstants;


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

        public NodesController(IHttpContextAccessor httpContextAccessor,
                               UserContext uContext, DkgContext dContext,
                               Runner rnner, ILogger<NodesController> lgger) :
               base(httpContextAccessor, uContext)
        {
            dkgContext = dContext;
            runner = rnner;
            logger = lgger;
        }

        // GET: api/Nodes
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Node>))]
        public async Task<ActionResult<IEnumerable<Node>>> GetNodes()
        {
            var res = await dkgContext.Nodes.OrderBy(n => n.Id).ToListAsync();
            return res;
        }

        // GET: api/Nodes/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Round))]
        public async Task<ActionResult<Node>> GetNode(int id)
        {
            var node = await dkgContext.Nodes.FindAsync(id);
            if (node == null) return _404Node(id);
            return node;
        }

        // POST: api/Nodes/register
        [HttpPost("register")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Reference))]
        public async Task<ActionResult<Reference>> RegisterNode(Node node)
        {
            int? roundId = null;
            List<Round> rounds = await dkgContext.Rounds.Where(r => r.StatusValue == (short)RStatus.Registration).ToListAsync();
            if (rounds.Count != 0)
            {
                Round round = rounds[new Random().Next(rounds.Count)];
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
                xNode.Name = node.Name;
                xNode.RoundId = roundId;
                xNode.PublicKey = node.PublicKey;
                xNode.CalculateRandom();
                if (roundId == null) xNode.StatusValue = (short)NStatus.NotRegistered;
                dkgContext.Entry(xNode).State = EntityState.Modified;
                await dkgContext.SaveChangesAsync();
            }

            roundId ??= 0;
            var reference = new Reference((int)roundId);

            return Ok(reference);
        }

        // Accept action
        // Acknowledges that the status report has been received
        internal async Task<ObjectResult> Accept(Round? round, Node node, StatusReport stReport)
        {
            await UpdateNodeState(dkgContext, node, (short)stReport.Status, round?.Id);
            if (round != null)
            {
                await UpdateRoundState(round);
            }

            var response = new StatusResponse(round != null ? round.Id : 0, stReport.Status);
            return Accepted(response);
        }

        internal async Task<ObjectResult> TrToNotRegistered(Round? round, Node node, StatusReport stReport)
        {
            if (round != null)
            {
                runner.SetNoResult(round, node);
            }

            await ResetNodeState(dkgContext, node);
            var response = new StatusResponse(0, NStatus.NotRegistered);
            if (stReport.Status != NStatus.NotRegistered || stReport.RoundId != 0)
            {
                return Ok(response);
            }
            return Accepted(response);
        }

        internal async Task<ObjectResult> TrToRunningStepOne(Round? round, Node node, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (!runner.CheckNode(round, node))
            {
                await ResetNodeState(dkgContext, node);
                var response = new StatusResponse(0, NStatus.NotRegistered);
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
            return Ok(new StatusResponse(round!.Id, NStatus.RunningStepOne, data));
        }
        internal async Task<ObjectResult> TrToRunningStepTwoConditional(Round? round, Node node, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (runner.CheckTimedOutNode(round, node))
            {
                await UpdateNodeState(dkgContext, node, (short)NStatus.TimedOut, round.Id);
                var response = new StatusResponse(round.Id, NStatus.TimedOut);
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
                return Ok(new StatusResponse(round!.Id, NStatus.RunningStepTwo, runner.GetStepTwoData(round, node)));
            }
            return Accepted(new StatusResponse(round!.Id, stReport.Status));
        }
        internal async Task<ObjectResult> TrToRunningStepThreeConditional(Round? round, Node node, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            if (runner.CheckTimedOutNode(round, node))
            {
                await UpdateNodeState(dkgContext, node, (short)NStatus.TimedOut, round.Id);
                var response = new StatusResponse(round.Id, NStatus.TimedOut);
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
                return Ok(new StatusResponse(round!.Id, NStatus.RunningStepThree, runner.GetStepThreeData(round, node)));
            }
            return Accepted(new StatusResponse(round!.Id, stReport.Status));
        }
        internal async Task<ObjectResult> WrongStatus(Round? round, Node node, StatusReport stReport)
        {
            if (round != null)
            {
                runner.SetNoResult(round, node);
            }

            await ResetNodeState(dkgContext, node);
            string rStatus = round == null ? "null" : GetRoundStatusById(round.StatusValue).ToString();
            return _409Status(stReport.PublicKey, stReport.Name, GetNodeStatusById(stReport.Status).ToString(), rStatus);
        }
        internal async Task<ObjectResult> AcceptFinished(Round? round, Node node, StatusReport stReport)
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
                return Accepted(new StatusResponse(round.Id, stReport.Status));
            }
            else
            {
                runner.SetNoResult(round, node);
                await UpdateNodeState(dkgContext, node, (short)stReport.Status, round.Id);
                await UpdateRoundState(round);
                return _400NoResult(round.Id, node.Name, node.PublicKey);
            }
        }
        internal async Task<ObjectResult> AcceptFailed(Round? round, Node node, StatusReport stReport)
        {
            if (round == null)
            {
                return _500UndefinedRound();
            }

            runner.SetResultWaitingTime(round);

            runner.SetNoResult(round, node);
            await UpdateNodeState(dkgContext, node, (short)stReport.Status, round.Id);
            await UpdateRoundState(round);

            return Accepted(new StatusResponse(round.Id, stReport.Status));
        }

        // POST: api/Nodes/status
        [HttpPost("status")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Reference>> Status(StatusReport statusReport)
        {
            // А это мой любимый автомат Мили ... центр любой системы :)
            Dictionary<(RStatus?, NStatus), Func<Round?, Node, StatusReport, Task<ObjectResult>>> actionMap = 
                new Dictionary<(RStatus?, NStatus), Func<Round?, Node, StatusReport, Task<ObjectResult>>>()
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
                return _404Node(statusReport.PublicKey, statusReport.Name);
            }

            var round = statusReport.RoundId == 0 ? null : await dkgContext.Rounds.FirstOrDefaultAsync(r => r.Id == statusReport.RoundId);

            RStatus? rStatus = null;
            if (round != null) rStatus = round.Status;

            if (actionMap.TryGetValue((rStatus, statusReport.Status), out var function))
            {
                logger.LogDebug("State transition round [{id}] node [{name}] : ({rStatus}, {nStatus}) -> {f}",
                                    (round != null ? round.Id.ToString() : "null"),
                                    node.Name, rStatus, statusReport.Status, function.Method.Name);
                return await function(round, node, statusReport);
            }
            else
            {
                return _500UnknownStateTransition(rStatus == null ? "null" : GetRoundStatusById((short)rStatus).ToString(), GetNodeStatusById((short)node.Status).ToString());
            }
        }

        // RESET: api/nodes/reset/5
        [HttpPost("reset/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ResetNode(int id)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            var node = await dkgContext.Nodes.FindAsync(id);
            if (node == null) return _404Node(id);

            node.StatusValue = (short)NStatus.NotRegistered;
            node.RoundId = null;
            dkgContext.Entry(node).State = EntityState.Modified;
            await dkgContext.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/nodes/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteNode(int id)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            var node = await dkgContext.Nodes.FindAsync(id);
            if (node == null) return _404Node(id);

            dkgContext.Nodes.Remove(node);
            await dkgContext.SaveChangesAsync();

            return NoContent();
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