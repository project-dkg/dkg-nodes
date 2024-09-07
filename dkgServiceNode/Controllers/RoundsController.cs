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
using dkgServiceNode.Services.NodeComparer;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.RoundRunner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using dkgServiceNode.Services.Cache;

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
        protected readonly ILogger logger;
        private readonly NodesRoundHistoryCache nodesRoundHistoryCache;

        public RoundsController(IHttpContextAccessor httpContextAccessor, 
                                UserContext uContext, 
                                DkgContext dContext, 
                                Runner rnner,
                                NodesRoundHistoryCache nrhc,
                                ILogger<NodesController> lgger) :
                                base(httpContextAccessor, uContext)
        {
            dkgContext = dContext;
            runner = rnner;
            logger = lgger;
            nodesRoundHistoryCache = nrhc;
        }

        // GET: api/rounds
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Round>))]
        public ActionResult<IEnumerable<Round>> GetRounds()
        {
            var rounds = dkgContext.GetAllRoundsSortedByIdDescending();

            foreach (var round in rounds)
            {
                int nodeCountWaitingRoundStart = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.WaitingRoundStart);
                round.NodeCountStepOne = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.RunningStepOne);
                round.NodeCountWStepTwo = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.WaitingStepTwo);
                round.NodeCountStepTwo = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.RunningStepTwo);
                round.NodeCountWStepThree = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.WaitingStepThree);
                round.NodeCountStepThree = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.RunningStepThree);
                round.NodeCountStepFour = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.RunningStepFour);
                round.NodeCountFailed = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.Failed);
                round.NodeCountFinished = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.Finished);
                round.NodeCountTimedOut = nodesRoundHistoryCache.GetNodeCountForRound(round.Id, NStatus.TimedOut);

                int eaCount = round.NodeCountStepOne +
                              round.NodeCountWStepTwo +
                              round.NodeCountStepTwo +
                              round.NodeCountWStepThree +
                              round.NodeCountStepThree +
                              round.NodeCountStepFour +
                              round.NodeCountFailed +
                              round.NodeCountFinished +
                              round.NodeCountTimedOut +
                              nodeCountWaitingRoundStart;

                round.NodeCount = eaCount;
            }

            return rounds;
        }

        // GET: api/rounds/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Round))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public ActionResult<Round> GetRound(int id)
        {
            var round = dkgContext.GetRoundById(id);
            if (round == null) return _404Round(id);
            round.NodeCount = dkgContext.GetAllNodes().Count(n => n.RoundId == round.Id);

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

            await dkgContext.AddRoundAsync(round);

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

            Round? round = dkgContext.GetRoundById(id);
            if (round == null) return _404Round(id);

            round.ModifiedOn = DateTime.Now.ToUniversalTime();
            round.CreatedOn = round.CreatedOn.ToUniversalTime();
            round.Status = round.NextStatus;

            switch (round.StatusValue)
            {
                case (short)RStatus.Registration:
                    runner.StartRound(round);
                    break;
                case (short)RStatus.CreatingDeals:
                    await TryRunRound(round);
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

            Round? round = dkgContext.GetRoundById(id);
            if (round == null) return _404Round(id);

            round.ModifiedOn = DateTime.Now.ToUniversalTime();
            round.CreatedOn = round.CreatedOn.ToUniversalTime();
            round.Status = RoundStatusConstants.Cancelled;

            runner.CancelRound(round);
            return await UpdateRoundState(dkgContext, round);
        }

        internal async Task TryRunRound(Round round)
        {
            List<Node> rNodes = dkgContext.GetAllNodes()
                    .Where(n => n.RoundId == round.Id && n.Status == NStatus.WaitingRoundStart)
                    .ToList();


            List<Node> fiNodes = rNodes
                    .Where(node => dkgContext.CheckNodeQualification(node.Id, round.Id - 1))
                    .ToList();

            if (fiNodes.Count < 3)
            {
                logger.LogWarning("Not enough nodes has been qualified to start a round. Count = {count}, minimum = 3", fiNodes.Count);

                await ResetNodeStates(dkgContext, rNodes);
                round.Result = null;
                round.StatusValue = (short)RStatus.Failed;
            }
            else
            {
                List<Node> reNodes = rNodes.Except(fiNodes).ToList();
                await ResetNodeStates(dkgContext, reNodes);

                List<Node> fi2Nodes = fiNodes;
                reNodes = [];
                    
                if (round.MaxNodes < fiNodes.Count)
                {
                    int lastRR = dkgContext.LastRoundResult() ?? new Random().Next();
                    fiNodes.Sort(new NodeComparer(lastRR, round.Id - 1, nodesRoundHistoryCache));
                    fi2Nodes = fiNodes.Take(round.MaxNodes).ToList();
                    reNodes = fiNodes.Skip(round.MaxNodes).ToList();
                }
                runner.RunRound(round, fi2Nodes);
                await ResetNodeStates(dkgContext, reNodes);
            }
        }
        internal async Task<ActionResult<Round>> UpdateRoundState(DkgContext dkgContext, Round round)
        {
            try
            {
                await dkgContext.UpdateRoundAsync(round);
            }
            catch 
            {
                return _404Round(round.Id);
            }
            return NoContent();
        }
    }
}
