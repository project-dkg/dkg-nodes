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

using dkgCommon.Models;
using dkgServiceNode.Data;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.RoundRunner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace dkgServiceNode.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]

    public class RoundsController : DControllerBase
    {
        protected readonly RoundContext roundContext;
        protected readonly NodeContext nodeContext;

        public RoundsController(IHttpContextAccessor httpContextAccessor, UserContext uContext, RoundContext rContext, NodeContext nContext) :
               base(httpContextAccessor, uContext)
        {
            roundContext = rContext;
            nodeContext = nContext;
        }

        // GET: api/rounds
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Round>))]
        public async Task<ActionResult<IEnumerable<Round>>> GetRounds()
        {
            var res = await roundContext.Rounds.OrderByDescending(r => r.Id).ToListAsync();
            foreach (var round in res)
            {
                if (round.IsVersatile)
                {
                    round.NodeCount = await nodeContext.Nodes.CountAsync(n => n.RoundId == round.Id);
                }
            }
            return res;
        }

        // GET: api/rounds/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Round))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Round>> GetRound(int id)
        {
            var round = await roundContext.Rounds.FindAsync(id);
            if (round == null) return _404Round(id);

            if (round.IsVersatile)
            { 
                round.NodeCount = await nodeContext.Nodes.CountAsync(n => n.RoundId == round.Id);
            }

            return round;
        }

        // POST: api/rounds/add
        [HttpPost("add")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Reference))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Reference>> AddRound()
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            Round round = new();
            roundContext.Rounds.Add(round);
            await roundContext.SaveChangesAsync();

            var reference = new Reference(round.Id) { Id = round.Id };
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

            Round? round = await roundContext.Rounds.FindAsync(id);
            if (round == null) return _404Round(id);

            round.ModifiedOn = DateTime.Now.ToUniversalTime();
            round.CreatedOn = round.CreatedOn.ToUniversalTime();
            round.Status = round.NextStatus;

            if (round.IsVersatile)
            {
                round.NodeCount = await nodeContext.Nodes.CountAsync(n => n.RoundId == round.Id);
            }

            switch (round.StatusValue)
            {
                case (short)RStatus.Started:
                    Runner.StartRound(round);
                    break;
                case (short)RStatus.Running:
                    Runner.RunRound(round, await nodeContext.Nodes.ToListAsync());
                    break;
                case (short)RStatus.Finished:
                    round.Result = Runner.FinishRound(round, await nodeContext.Nodes.ToListAsync());
                    if (round.Result == null)
                    {
                        round.StatusValue = (short)RStatus.Failed;
                    }
                    break;
                case (short)RStatus.Cancelled:
                    Runner.CancelRound(round, await nodeContext.Nodes.ToListAsync());
                    break;
                default:
                    break;
            }

            roundContext.Entry(round).State = EntityState.Modified;
            try
            {
                await roundContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await roundContext.ExistsAsync(id))
                {
                    return _404Round(id);
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
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

            Round? round = await roundContext.Rounds.FindAsync(id);
            if (round == null) return _404Round(id);

            round.ModifiedOn = DateTime.Now.ToUniversalTime();
            round.CreatedOn = round.CreatedOn.ToUniversalTime();
            round.Status = RoundStatusConstants.Cancelled;

            roundContext.Entry(round).State = EntityState.Modified;
            try
            {
                await roundContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (! await roundContext.ExistsAsync(id))
                {
                    return _404Round(id);
                }
                else
                {
                    throw;
                }
            }
            Runner.CancelRound(round, await nodeContext.Nodes.ToListAsync());
            return NoContent();
        }
    }
}
