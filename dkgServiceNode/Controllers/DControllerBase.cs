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

using Microsoft.AspNetCore.Mvc;
using dkgServiceNode.Data;
using dkgServiceNode.Models;
using dkgCommon.Constants;
using dkgServiceNode.Services.RoundRunner;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Controllers
{
    public class DControllerBase : ControllerBase
    {
        protected readonly UserContext userContext;
        protected readonly int curUserId;

        protected ObjectResult _400()
        {
            return StatusCode(StatusCodes.Status400BadRequest,
                              new ErrMessage { Msg = "Inconsistent request." });
        }
        protected ObjectResult _400NoResult(int roundId, string name, string publicKey)
        {
            return StatusCode(StatusCodes.Status400BadRequest,
                              new { message = $"Round: {roundId} node [{name}:{publicKey}] finished with no result data" });
        }
        protected ObjectResult _403()
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                              new { message = "Insufficient privileges for the operation." });
        }
        protected ObjectResult _403Protect()
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                              new { message = "This user cannot be deleted or modified." });
        }
        protected ObjectResult _403InvalidSignature()
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                              new { message = "Failed to verify node signature" });
        }
        protected ObjectResult _404(int id, string item)
        {
            return StatusCode(StatusCodes.Status404NotFound,
                              new { message = $"Failed to find {item} [id={id}]." });
        }
        protected ObjectResult _404CurrentVersion()
        {
            return StatusCode(StatusCodes.Status404NotFound,
                              new { message = "Failed to found current database version." });
        }
        protected ObjectResult _404Node(int id)
        {
            return _404(id, "Node");
        }
        protected ObjectResult _404Node(string publicKey, string name)
        {
            return StatusCode(StatusCodes.Status404NotFound,
                              new { message = $"Failed to find node [{name}:{publicKey}]." });
        }
        protected ObjectResult _404Round(int id)
        {
            return _404(id, "Round");
        }
        protected ObjectResult _404User(int id)
        {
            return _404(id, "User");
        }
        protected ObjectResult _409Email(string email)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                              new { message = $"A user with this email is already registered [email = {email}]." });
        }
        protected ObjectResult _409Round()
        {
            return StatusCode(StatusCodes.Status409Conflict,
                              new { message = $"Could not find a round that is collecting node applications." });
        }

        protected ObjectResult _409Status(string publicKey, string name, string nStatus, string rStatus)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                              new { message = $"Node [{name}:{publicKey}] reports status '{nStatus}' that does not fit round status {rStatus}" });
        }
        protected ObjectResult _500(string msg)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                              new ErrMessage { Msg = msg });
        }
        protected ObjectResult _500UndefinedRound()
        {
            return _500("Round is not defined (null)");
        }
        protected ObjectResult _500MisssingStepOneData(int id, string status)
        {
            return _500($"Round [{id}] status is [{status}] but step one data is missing");
        }

        protected ObjectResult _500UnknownStateTransition(string rState, string nState)
        {
            return _500($"Unknown state transition [rState = {rState}, nState = {nState}]");
        }
        protected DControllerBase(IHttpContextAccessor httpContextAccessor, UserContext uContext)
        {
            userContext = uContext;
            curUserId = 0;
            var htc = httpContextAccessor.HttpContext;
            if (htc != null)
            {
                var uid = htc.Items["UserId"];
                if (uid != null) curUserId = (int)uid;
            }
        }

        protected async Task ResetNodeState(DkgContext dkgContext, Node node)
        {
            bool needsUpdate = false;
            if (node.StatusValue != (short)NStatus.NotRegistered)
            {
                node.StatusValue = (short)NStatus.NotRegistered;
                needsUpdate = true;
            }

            if (node.RoundId != null)
            {
                node.RoundId = null;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                dkgContext.Entry(node).State = EntityState.Modified;
                await dkgContext.SaveChangesAsync();
            }
        }

        protected async Task ResetNodeStates(DkgContext dkgContext, List<Node> nodes)
        {
            bool needsUpdate = false;

            foreach (var node in nodes)
            {
                if (node.StatusValue != (short)NStatus.NotRegistered)
                {
                    node.StatusValue = (short)NStatus.NotRegistered;
                    needsUpdate = true;
                }

                if (node.RoundId != null)
                {
                    node.RoundId = null;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    dkgContext.Entry(node).State = EntityState.Modified;
                }
            }

            if (needsUpdate)
            {
                await dkgContext.SaveChangesAsync();
            }
        }

        protected async Task UpdateNodeState(DkgContext dkgContext, Node node, short nStatus, int? roundId)
        {
            if (node.StatusValue != nStatus || node.RoundId != roundId)
            {
                node.StatusValue = nStatus;
                node.RoundId = roundId;
                dkgContext.Entry(node).State = EntityState.Modified;
                await dkgContext.SaveChangesAsync();
            }
        }
    }
}