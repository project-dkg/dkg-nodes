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
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using static dkgCommon.Constants.NodeStatusConstants;
using static dkgServiceNode.Services.RoundRunner.RoundStatusConstants;


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
        protected readonly NodeContext nodeContext;
        protected readonly RoundContext roundContext;

        public NodesController(IHttpContextAccessor httpContextAccessor, UserContext uContext, NodeContext nContext, RoundContext rContext) :
               base(httpContextAccessor, uContext)
        {
            nodeContext = nContext;
            roundContext = rContext;
        }

        // GET: api/Nodes
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Node>))]
        public async Task<ActionResult<IEnumerable<Node>>> GetNodes()
        {
            var res = await nodeContext.Nodes.OrderBy(n => n.Id).ToListAsync();
            return res;
        }

        // GET: api/Nodes/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Round))]
        public async Task<ActionResult<Node>> GetNode(int id)
        {
            var node = await nodeContext.Nodes.FindAsync(id);
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
            List<Round> rounds = await roundContext.Rounds.Where(r => r.StatusValue == (short)RStatus.Registration).ToListAsync();
            if (rounds.Count != 0)
            {
                Round round = rounds[new Random().Next(rounds.Count)];
                roundId = round.Id;
            }

            var xNode = await nodeContext.FindByHostAndPortAsync(node.Host, node.Port);
            if (xNode == null)
            {
                node.RoundId = roundId;
                nodeContext.Nodes.Add(node);
                await nodeContext.SaveChangesAsync();
            }
            else
            {
                xNode.Name = node.Name;
                xNode.PublicKey = node.PublicKey;
                xNode.RoundId = roundId;
                nodeContext.Entry(xNode).State = EntityState.Modified;
                await nodeContext.SaveChangesAsync();
            }

            if (roundId == null) 
            {
                roundId = 0;
            }
            var reference = new Reference((int)roundId);
            
            return Ok(reference);
        }

        // POST: api/Nodes/status
        [HttpPost("status")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Reference>> Status(StatusReport statusReport)
        {
            var node = await nodeContext.FindByHostAndPortAsync(statusReport.Host, statusReport.Port);
            if (node == null)
            {
                return _404Node(statusReport.Host, statusReport.Port);
            }

            var round = await roundContext.Rounds.FirstOrDefaultAsync(r => r.Id == statusReport.RoundId);
            if (round == null)
            {
                if (statusReport.Status != NStatus.NotRegistered)
                {
                    return _404Round(statusReport.RoundId);
                }
            }
            else
            {
                if (!round.IsVersatile && statusReport.Status == NStatus.WaitingRoundStart)
                {
                    return StatusCode(StatusCodes.Status409Conflict,
                      new
                      {
                          message = $"Node [{statusReport.Host}:{statusReport.Port}] reports status '{GetNodeStatusById(statusReport.Status)}' that does not fit round status {GetRoundStatusById(round.StatusValue)}"
                      });
                }
            }

            if (node.StatusValue != (short)statusReport.Status)
            {
                node.StatusValue = (short)statusReport.Status;
                nodeContext.Entry(node).State = EntityState.Modified;
                await nodeContext.SaveChangesAsync();
            }

            return Accepted();
        }


        // RESET: api/nodes/reset/5
        [HttpPost("reset/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ResetNode(int id)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            var node = await nodeContext.Nodes.FindAsync(id);
            if (node == null) return _404Node(id);

            node.StatusValue = (short)NStatus.NotRegistered;
            node.RoundId = null;
            nodeContext.Entry(node).State = EntityState.Modified;
            await nodeContext.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/nodes/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteNode(int id)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            var node = await nodeContext.Nodes.FindAsync(id);
            if (node == null) return _404Node(id);

            nodeContext.Nodes.Remove(node);
            await nodeContext.SaveChangesAsync();

            return NoContent();
        }
    }
}