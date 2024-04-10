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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<NodeViewItem>))]
        public async Task<ActionResult<IEnumerable<NodeViewItem>>> GetNodes()
        {
            return await nodeContext.NodeViewItems();
        }

        // GET: api/Nodes/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Round))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<NodeViewItem>> GetNode(int id)
        {
            var node = await nodeContext.NodeViewItem(id);
            if (node == null) return _404Node(id);
            return node;
        }

        // POST: api/Nodes/add
        [HttpPost("add")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Reference))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Node>> AddNode(Node node)
        {
            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            nodeContext.Nodes.Add(node);
            await nodeContext.SaveChangesAsync();

            var reference = new Reference(node.Id) { Id = node.Id };
            return CreatedAtAction(nameof(GetNode), new { id = node.Id }, reference);
        }


        // PUT: api/Nodes/5
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrMessage))]
        [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ErrMessage))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Node>> UpdateNode(int id, Node node)
        {
            if (id != node.Id) return _400();

            var ch = await userContext.CheckAdmin(curUserId);
            if (ch == null || !ch.Value) return _403();

            nodeContext.Entry(node).State = EntityState.Modified;
            try
            {
                await nodeContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!nodeContext.Exists(id))
                {
                    return _404Node(id);
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
