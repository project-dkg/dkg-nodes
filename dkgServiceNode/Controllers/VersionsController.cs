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
using Version = dkgServiceNode.Models.Version;

namespace dkgServiceNode.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ErrMessage))]

    public class VersionsController : DControllerBase
    {
        protected readonly VersionContext versionContext;

        public VersionsController(IHttpContextAccessor httpContextAccessor, UserContext uContext, VersionContext vContext) :
               base(httpContextAccessor, uContext)
        {
            versionContext = vContext;
        }

        // GET: api/versions/current
        [HttpGet("current")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Version))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrMessage))]
        public async Task<ActionResult<Version>> GetCurrentVersion()
        {
            var version = await versionContext.Versions.OrderByDescending(v => v.Id).FirstOrDefaultAsync();
            if (version == null) return _404CurrentVersion();
            return version;
        }
    }
}
