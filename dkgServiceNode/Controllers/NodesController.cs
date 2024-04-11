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

using dkgServiceNode.Data;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.RoundRunner;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace dkgServiceNode.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]

    public class NodesController : DControllerBase
    {
        protected readonly NodeContext nodeContext;

        public NodesController(IHttpContextAccessor httpContextAccessor, UserContext uContext, NodeContext nContext) :
               base(httpContextAccessor, uContext)
        {
            nodeContext = nContext;
        }

        // GET: api/Nodes
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Node>))]
        public async Task<ActionResult<IEnumerable<Node>>> GetNodes()
        {
            var res = await nodeContext.Nodes.ToListAsync();
            return res;
        }

        // GET: api/Nodes/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Round))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
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
        public async Task<ActionResult<Node>> RegisterNode(Node node)
        {
            var xNode = await nodeContext.FindByHostAndPortAsync(node.Host, node.Port);
            int id;
            if (xNode == null)
            {
                nodeContext.Nodes.Add(node);
                await nodeContext.SaveChangesAsync();
                id = node.Id;
            }
            else
            {
                id = xNode.Id;
                xNode.Name = node.Name;
                nodeContext.Entry(xNode).State = EntityState.Modified;
                try
                {
                    await nodeContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await nodeContext.ExistsAsync(id))
                    {
                        return _404Node(id);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var reference = new Reference(node.Id) { Id = id };
            return CreatedAtAction(nameof(GetNode), new { id = node.Id }, reference);
        }

        // DELETE: api/nodes/5
        [HttpDelete("{id}")]
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