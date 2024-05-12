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
using System.Security.Cryptography.X509Certificates;
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

            var xNode = await dkgContext.FindNodeByGuidAsync(node.Gd);
            if (xNode == null)
            {
                node.RoundId = roundId;
                if (roundId == null) node.StatusValue = (short)NStatus.NotRegistered;
                dkgContext.Nodes.Add(node);
                await dkgContext.SaveChangesAsync();
            }
            else
            {
                xNode.Name = node.Name;
                xNode.RoundId = roundId;
                xNode.PublicKey = node.PublicKey;
                if (roundId == null) xNode.StatusValue = (short)NStatus.NotRegistered;
                dkgContext.Entry(xNode).State = EntityState.Modified;
                await dkgContext.SaveChangesAsync();
            }

            roundId ??= 0;
            var reference = new Reference((int)roundId);
            
            return Ok(reference);
        }

        // POST: api/Nodes/status
        [HttpPost("status")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(StatusResponse))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Reference>> Status(StatusReport statusReport)
        {
            var node = await dkgContext.FindNodeByPublicKeyAsync(statusReport.PublicKey);
            if (node == null)
            {
                return _404Node(statusReport.PublicKey, statusReport.Name);
            }

            if (node.RoundId == null && statusReport.Status != NStatus.NotRegistered)
            {
                node.StatusValue = (short)NStatus.NotRegistered;
                dkgContext.Entry(node).State = EntityState.Modified;
                await dkgContext.SaveChangesAsync();

                return Ok(new StatusResponse(0, NStatus.NotRegistered, []));
            }

            var round = await dkgContext.Rounds.FirstOrDefaultAsync(r => r.Id == statusReport.RoundId);

            if (round == null)
            {
                if (statusReport.Status != NStatus.NotRegistered)
                {
                    return _404Round(statusReport.RoundId);
                }
            }
            else
            {
                if (node.StatusValue != (short)statusReport.Status)
                {
                    node.StatusValue = (short)statusReport.Status;
                    dkgContext.Entry(node).State = EntityState.Modified;
                    await dkgContext.SaveChangesAsync();
                }

                switch (statusReport.Status)
                {
                    case NStatus.WaitingRoundStart:
                        switch ((RStatus)round.Status)
                        {
                            case RStatus.Registration:
                                break;
                            case RStatus.CreatingDeals:
                            case RStatus.ProcessingDeals:
                            case RStatus.ProcessingResponses:
                                string[] data = runner.GetStepOneData(round);
                                if (data.Length == 0)
                                {
                                    return StatusCode(StatusCodes.Status409Conflict,
                                      new
                                      {
                                          message = $"Round [{round.Id}] status is {round.Status}] but step one data is missing"
                                      });
                                }

                                return Ok(new StatusResponse(round.Id, NStatus.RunningStepOne, data));
                            case RStatus.Finished:
                            case RStatus.Cancelled:
                            case RStatus.Failed:
                                return Ok(new StatusResponse(round.Id, NStatus.NotRegistered, []));
                            case RStatus.NotStarted:
                            case RStatus.Unknown:
                                return _409Status(statusReport.PublicKey, statusReport.Name, 
                                                  GetNodeStatusById(statusReport.Status).ToString(), 
                                                  GetRoundStatusById(round.StatusValue).ToString());
                            default:
                                break;
                        }
                        break;
                    case NStatus.WaitingStepTwo:
                        switch ((RStatus)round.Status)
                        {
                            case RStatus.CreatingDeals:
                            case RStatus.ProcessingDeals:
                            case RStatus.ProcessingResponses:
                                if (statusReport.Data.Length != 0)
                                {
                                    runner.SetStepTwoData(round, node, statusReport.Data);
                                }
                                if (runner.IsStepTwoDataReady(round))
                                {
                                    await UpdateRoundState(round);
                                    return Ok(new StatusResponse(round.Id, NStatus.RunningStepTwo, runner.GetStepTwoData(round, node)));
                                }
                                break;
                            case RStatus.Cancelled:
                            case RStatus.Failed:
                            case RStatus.Finished:
                                return Ok(new StatusResponse(round.Id, NStatus.NotRegistered, []));
                            case RStatus.NotStarted:
                            case RStatus.Registration:
                            case RStatus.Unknown:
                                return _409Status(statusReport.PublicKey, statusReport.Name,
                                                  GetNodeStatusById(statusReport.Status).ToString(),
                                                  GetRoundStatusById(round.StatusValue).ToString());
                            default:
                                break;
                        }
                        break;
                    case NStatus.WaitingStepThree:
                        switch ((RStatus)round.Status)
                        {
                            case RStatus.CreatingDeals:
                            case RStatus.ProcessingDeals:
                            case RStatus.ProcessingResponses:
                                if (statusReport.Data.Length != 0)
                                {
                                    runner.SetStepThreeData(round, node, statusReport.Data);
                                }
                                if (runner.IsStepThreeDataReady(round))
                                {
                                    await UpdateRoundState(round);
                                    return Ok(new StatusResponse(round.Id, NStatus.RunningStepThree, runner.GetStepThreeData(round, node)));
                                }
                                break;
                            case RStatus.Cancelled:
                            case RStatus.Failed:
                            case RStatus.Finished:
                            case RStatus.NotStarted:
                                return Ok(new StatusResponse(0, NStatus.NotRegistered, []));
                            case RStatus.Registration:
                            case RStatus.Unknown:
                                return _409Status(statusReport.PublicKey, statusReport.Name,
                                                  GetNodeStatusById(statusReport.Status).ToString(),
                                                  GetRoundStatusById(round.StatusValue).ToString());
                            default:
                                break;
                        }
                        break;
                    case NStatus.Finished:
                        switch ((RStatus)round.Status)
                        {
                            case RStatus.CreatingDeals:
                            case RStatus.ProcessingDeals:
                            case RStatus.ProcessingResponses:
                                if (statusReport.Data.Length != 0)
                                {
                                    runner.SetResult(round, node, statusReport.Data);
                                    await UpdateRoundState(round);
                                }
                                break;
                            case RStatus.Cancelled:
                            case RStatus.Failed:
                            case RStatus.Finished:
                                return Ok(new StatusResponse(0, NStatus.NotRegistered, []));
                            case RStatus.NotStarted:
                            case RStatus.Registration:
                            case RStatus.Unknown:
                                return _409Status(statusReport.PublicKey, statusReport.Name,
                                                  GetNodeStatusById(statusReport.Status).ToString(),
                                                  GetRoundStatusById(round.StatusValue).ToString());
                            default:
                                break;
                        }
                        break;
                    case NStatus.Failed:
                        switch ((RStatus)round.Status)
                        {
                            case RStatus.CreatingDeals:
                            case RStatus.ProcessingDeals:
                            case RStatus.ProcessingResponses:
                                if (statusReport.Data.Length != 0)
                                {
                                    runner.SetNoResult(round, node);
                                }
                                break;
                            case RStatus.Cancelled:
                            case RStatus.Failed:
                            case RStatus.Finished:
                                return Ok(new StatusResponse(0, NStatus.NotRegistered, []));
                            case RStatus.NotStarted:
                            case RStatus.Registration:
                            case RStatus.Unknown:
                                return _409Status(statusReport.PublicKey, statusReport.Name,
                                                  GetNodeStatusById(statusReport.Status).ToString(),
                                                  GetRoundStatusById(round.StatusValue).ToString());
                            default:
                                break;
                        }
                        break;
                    default:
                        break;
                }
            }

            var response = new StatusResponse(round != null ? round.Id : 0, statusReport.Status);
            return Accepted(response);
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