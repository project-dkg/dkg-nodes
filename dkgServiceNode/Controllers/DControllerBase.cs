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
using Microsoft.EntityFrameworkCore;
using dkgServiceNode.Services.Cache;

namespace dkgServiceNode.Controllers
{
    public class DControllerBase : ControllerBase
    {
        protected readonly UserContext userContext;
        protected readonly int curUserId;

        private static UInt64 c2Fetch = 0;
        private static UInt64 c2Get = 0;
        private static UInt64 c2GetAll = 0;
        private static UInt64 c2Register = 0;
        private static UInt64 c2Status = 0;
        private static UInt64 c2Reset = 0;
        private static UInt64 c2Delete = 0;
        private static UInt64 c2Statistics = 0;

        private static TimeSpan e2Fetch = new();
        private static readonly object e2FetchLock = new();
        private static TimeSpan e2Get = new();
        private static readonly object e2GetLock = new();
        private static TimeSpan e2GetAll = new();
        private static readonly object e2GetAllLock = new();
        private static TimeSpan e2Register = new();
        private static readonly object e2RegisterLock = new();
        private static TimeSpan e2Status = new();
        private static readonly object e2StatusLock = new();
        private static TimeSpan e2Reset = new();
        private static readonly object e2ResetLock = new();
        private static TimeSpan e2Delete = new();
        private static readonly object e2DeleteLock = new();
        private static TimeSpan e2Statistics = new();
        private static readonly object e2StatisticsLock = new();

        protected static void UpdateE2Timing(ref TimeSpan e2Timing, ref UInt64 c2Timing, TimeSpan t, object lockObject)
        {
            lock (lockObject)
            {
                e2Timing += t;
            }
            Interlocked.Increment(ref c2Timing);
        }

        protected static TimingResult GetTimingResult(string name, ref UInt64 c2Timing, ref TimeSpan e2Timing, object lockObject)
        {
            double v;
            UInt64 c = Interlocked.Read(ref c2Timing);
            lock (lockObject)
            {
                v = c == 0 ? 0 : e2Timing.TotalMilliseconds / (double)c;
            }
            return new TimingResult()
            {
                Name = name,
                Count = c,
                TimePerCall = v
            };
        }

        protected static void UpdateE2Fetch(TimeSpan t) => UpdateE2Timing(ref e2Fetch, ref c2Fetch, t, e2FetchLock);
        protected static void UpdateE2Get(TimeSpan t) => UpdateE2Timing(ref e2Get, ref c2Get, t, e2GetLock);
        protected static void UpdateE2GetAll(TimeSpan t) => UpdateE2Timing(ref e2GetAll, ref c2GetAll, t, e2GetAllLock);
        protected static void UpdateE2Register(TimeSpan t) => UpdateE2Timing(ref e2Register, ref c2Register, t, e2RegisterLock);
        protected static void UpdateE2Status(TimeSpan t) => UpdateE2Timing(ref e2Status, ref c2Status, t, e2StatusLock);
        protected static void UpdateE2Reset(TimeSpan t) => UpdateE2Timing(ref e2Reset, ref c2Reset, t, e2ResetLock);
        protected static void UpdateE2Delete(TimeSpan t) => UpdateE2Timing(ref e2Delete, ref c2Delete, t, e2DeleteLock);
        protected static void UpdateE2Statistics(TimeSpan t) => UpdateE2Timing(ref e2Statistics, ref c2Statistics, t, e2StatisticsLock);
        protected static TimingResult GetE2Fetch() => GetTimingResult("fetch", ref c2Fetch, ref e2Fetch, e2FetchLock);
        protected static TimingResult GetE2Get() => GetTimingResult("get", ref c2Get, ref e2Get, e2GetLock);
        protected static TimingResult GetE2GetAll() => GetTimingResult("getAll", ref c2GetAll, ref e2GetAll, e2GetAllLock);
        protected static TimingResult GetE2Status() => GetTimingResult("status", ref c2Status, ref e2Status, e2StatusLock);
        protected static TimingResult GetE2Reset() => GetTimingResult("reset", ref c2Reset, ref e2Reset, e2ResetLock);
        protected static TimingResult GetE2Delete() => GetTimingResult("delete", ref c2Delete, ref e2Delete, e2DeleteLock);
        protected static TimingResult GetE2Register() => GetTimingResult("register", ref c2Register, ref e2Register, e2RegisterLock);
        protected static TimingResult GetE2Statistics() => GetTimingResult("statistics", ref c2Statistics, ref e2Statistics, e2StatisticsLock);

        protected ObjectResult _400()
        {
            return StatusCode(StatusCodes.Status400BadRequest,
                              new ErrMessage { Msg = "Inconsistent request." });
        }
        protected ObjectResult _400NoResult(int roundId, string name, string address)
        {
            return StatusCode(StatusCodes.Status400BadRequest,
                              new { message = $"Round: {roundId} node [{name}:@{address}] finished with no result data" });
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
        protected ObjectResult _404Node(string address, string name)
        {
            return StatusCode(StatusCodes.Status404NotFound,
                              new { message = $"Failed to find node [{name}:@{address}]." });
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

        protected ObjectResult _409Status(string address, string name, string nStatus, string rStatus)
        {
            return StatusCode(StatusCodes.Status409Conflict,
                              new { message = $"Node [{name}:@{address}] reports status '{nStatus}' that does not fit round status {rStatus}" });
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
                await dkgContext.UpdateNodeAsync(node);
            }
        }

        protected async Task ResetNodeStates(DkgContext dkgContext, List<Node> nodes)
        {
            List<Task> tasks = [];
            foreach (var node in nodes)
            {
                tasks.Add(ResetNodeState(dkgContext, node));
            }
            await Task.WhenAll(tasks);
        }

        protected async Task UpdateNodeState(DkgContext dkgContext, Node node, short nStatus, int? roundId)
        {
            if (node.StatusValue != nStatus || node.RoundId != roundId)
            {
                node.StatusValue = nStatus;
                node.RoundId = roundId;
                await dkgContext.UpdateNodeAsync(node);
            }
        }
    }
}