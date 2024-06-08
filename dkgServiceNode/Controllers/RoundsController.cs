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
using dkgServiceNode.Services.NodeComparer;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.RoundRunner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]

    public class RoundsController : DControllerBase
    {
        protected readonly DkgContext dkgContext;
        protected readonly Runner runner;

        public RoundsController(IHttpContextAccessor httpContextAccessor, UserContext uContext, DkgContext dContext, Runner rnner) :
               base(httpContextAccessor, uContext)
        {
            dkgContext = dContext;
            runner = rnner;
        }

        // GET: api/rounds
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Round>))]
        public async Task<ActionResult<IEnumerable<Round>>> GetRounds()
        {
            var rounds = await dkgContext.Rounds.OrderByDescending(r => r.Id).ToListAsync();

            var nodeCounts = await dkgContext.Nodes
                .Where(n => dkgContext.Rounds.Any(r => r.Id == n.RoundId))
                .GroupBy(n => new { n.RoundId, n.StatusValue })
                .Select(g => new NodeCountResult
                {
                    RoundId = g.Key.RoundId ?? 0,
                    Status = (NStatus)g.Key.StatusValue,
                    Count = g.Count()
                })
                .ToListAsync();

            var nodeCountsH = await dkgContext.NodesRoundHistory
                .Where(n => dkgContext.Rounds.Any(r => r.Id == n.RoundId))
                .GroupBy(n => new { n.RoundId, n.NodeFinalStatusValue })
                .Select(g => new NodeCountResult
                {
                    RoundId = g.Key.RoundId,
                    Status = (NStatus)g.Key.NodeFinalStatusValue,
                    Count = g.Count()
                })
                .ToListAsync();

            foreach (var round in rounds)
            {
                round.NodeCount = NodeCountResult.GetCount(nodeCounts, round.Id, null) + 
                                  NodeCountResult.GetCount(nodeCountsH, round.Id, null);
                int nodeCountWaitingRoundStart = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.WaitingRoundStart);
                round.NodeCountStepOne = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.RunningStepOne);
                round.NodeCountWStepTwo = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.WaitingStepTwo);
                round.NodeCountStepTwo = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.RunningStepTwo);
                round.NodeCountWStepThree = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.WaitingStepThree);
                round.NodeCountStepThree = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.RunningStepThree);
                round.NodeCountStepFour = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.RunningStepFour);
                round.NodeCountFailed = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.Failed) +
                                        NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.Failed);
                round.NodeCountFinished = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.Finished) +
                                          NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.Finished);
                round.NodeCountTimedOut = NodeCountResult.GetCount(nodeCounts, round.Id, NStatus.TimedOut) +
                                          NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.TimedOut);

                int eaCount =             NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.RunningStepOne) +
                                          NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.WaitingStepTwo) +
                                          NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.RunningStepTwo) +
                                          NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.WaitingStepThree) +
                                          NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.RunningStepThree) +
                                          NodeCountResult.GetCount(nodeCountsH, round.Id, NStatus.RunningStepFour);
                if (round.Status == RStatus.Cancelled) round.NodeCountFailed += eaCount;
                else round.NodeCountTimedOut += eaCount;
            }

            return rounds;
        }

        // GET: api/rounds/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Round))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Round>> GetRound(int id)
        {
            var round = await dkgContext.Rounds.FindAsync(id);
            if (round == null) return _404Round(id);
            round.NodeCount = await dkgContext.Nodes.CountAsync(n => n.RoundId == round.Id);

            return round;
        }

        // POST: api/rounds/add
        [HttpPost("add")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Reference))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Reference>> AddRound(RoundSettings roundSettings)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            Round round = new()
            {
                MaxNodes = roundSettings.MaxNodes,
                Timeout2 = roundSettings.Timeout2,
                Timeout3 = roundSettings.Timeout3,
                TimeoutR = roundSettings.TimeoutR
            };

            dkgContext.Rounds.Add(round);
            await dkgContext.SaveChangesAsync();

            var reference = new Reference(round.Id);
            return CreatedAtAction(nameof(AddRound), new { id = round.Id }, reference);
        }

        // POST: api/rounds/next/5
        [HttpPost("next/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Round>> NextRoundStep(int id)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            Round? round = await dkgContext.Rounds.FindAsync(id);
            if (round == null) return _404Round(id);

            round.ModifiedOn = DateTime.Now.ToUniversalTime();
            round.CreatedOn = round.CreatedOn.ToUniversalTime();
            round.Status = round.NextStatus;

            var rNodes = await dkgContext.Nodes.Where(n => n.RoundId == round.Id).ToListAsync();
            List<Node> fiNodes = rNodes;
            List<Node> reNodes = [];

            switch (round.StatusValue)
            {
                case (short)RStatus.Registration:
                    runner.StartRound(round);
                    break;
                case (short)RStatus.CreatingDeals:
                    if (round.MaxNodes < rNodes.Count)
                    {
                        int lastRR = await dkgContext.LastRoundResult() ?? new Random().Next();
                        rNodes.Sort(new NodeComparer(lastRR));
                        fiNodes = rNodes.Take(round.MaxNodes).ToList();
                        reNodes = rNodes.Skip(round.MaxNodes).ToList();
                    }
                    runner.RunRound(round, fiNodes);
                    foreach (Node node in reNodes)
                    {
                        await ResetNodeState(dkgContext, node);
                    }

                    break;
                case (short)RStatus.ProcessingDeals:
                    runner.ProcessDeals(round);
                    break;
                case (short)RStatus.ProcessingResponses:
                    runner.ProcessResponses(round);
                    break;
                case (short)RStatus.Finished:
                    round.Result = runner.FinishRound(round);
                    if (round.Result == null)
                    {
                        round.StatusValue = (short)RStatus.Failed;
                    }
                    break;
                case (short)RStatus.Cancelled:
                    runner.CancelRound(round);
                    break;
                default:
                    break;
            }

            return await UpdateRoundState(dkgContext, round);
        }

        // POST: api/rounds/cancel/5
        [HttpPost("cancel/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Round>> CancelRound(int id)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            Round? round = await dkgContext.Rounds.FindAsync(id);
            if (round == null) return _404Round(id);

            round.ModifiedOn = DateTime.Now.ToUniversalTime();
            round.CreatedOn = round.CreatedOn.ToUniversalTime();
            round.Status = RoundStatusConstants.Cancelled;

            runner.CancelRound(round);
            return await UpdateRoundState(dkgContext, round);
        }

        internal async Task<ActionResult<Round>> UpdateRoundState(DkgContext dkgContext, Round round)
        {
            dkgContext.Entry(round).State = EntityState.Modified;
            try
            {
                await dkgContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await dkgContext.RoundExistsAsync(round.Id))
                {
                    return _404Round(round.Id);
                }
                else
                {
                    throw;
                }
            }
            return NoContent();
        }
    }
}
