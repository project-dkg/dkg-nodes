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
        public ActionResult<NodesFrameResult> FetchNodes(NodesFrame nodesFrame)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            var sf = dkgContext.GetNodeCount();
            var nf = dkgContext.GetFilteredNodes(nodesFrame.Search);

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
            UpdateE2Fetch(stopwatch.Elapsed);
            return res;
        }

        // RESET: api/nodes/reset/5
        [HttpPost("reset/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ResetNode(int id)
        {
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
                var node = dkgContext.GetNodeById(id);
                if (node == null)
                {
                    res = _404Node(id);
                }
                else
                {
                    node.StatusValue = (short)NStatus.NotRegistered;
                    node.RoundId = null;
                    await dkgContext.UpdateNodeAsync(node);

                    res = NoContent();
                }
            }
            stopwatch.Stop();
            UpdateE2Reset(stopwatch.Elapsed);
            return res;
        }

        // DELETE: api/nodes/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteNode(int id)
        {
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


                var node = dkgContext.GetNodeById(id);
                if (node == null)
                {
                    res = _404Node(id);
                }
                else
                {
                    await dkgContext.DeleteNodeAsync(node);
                    res = NoContent();
                }
            }
            stopwatch.Stop();
            UpdateE2Delete(stopwatch.Elapsed);
            return res;
        }

        // GET: api/nodes
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<Node>))]
        public ActionResult<IEnumerable<Node>> GetNodes()
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            var res = dkgContext.GetAllNodesSortedById();

            stopwatch.Stop();
            UpdateE2GetAll(stopwatch.Elapsed);
            return Ok(res);
        }

        // GET: api/nodes/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Node))]
        public ActionResult<Node> GetNode(int id)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();

            var node = dkgContext.GetNodeById(id);
            if (node == null) return _404Node(id);

            stopwatch.Stop();
            UpdateE2Get(stopwatch.Elapsed);
            return node;

        }

        // GET: api/nodes/statistics
        [HttpGet("statistics")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<TimingResult>))]
        public async Task<ActionResult<IEnumerable<TimingResult>>> GetStatistics()
        {
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
                List<TimingResult> timingResults = [
                    GetE2Register(),
                    GetE2Status(),
                    GetE2Fetch(),
                    GetE2Get(),
                    GetE2GetAll(),
                    GetE2Reset(),
                    GetE2Delete(),
                    GetE2Statistics()

                ];

                res = Ok(timingResults);
            }

            stopwatch.Stop();
            UpdateE2Statistics(stopwatch.Elapsed);
            return res;
        }

    }
}