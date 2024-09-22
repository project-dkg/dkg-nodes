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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UsersController : DControllerBase
    {
        public UsersController(IHttpContextAccessor httpContextAccessor, UserContext uContext) : base(httpContextAccessor, uContext)
        {
        }

        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserViewItem>>> GetUsers()
        {
            var ch = await userContext.CheckAdminAsync(curUserId);
            if (ch == null || !ch.Value) return _403();

            return await userContext.UserViewItemsAsync();
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserViewItem>> GetUser(int id)
        {
            var ch = await userContext.CheckAdminOrSameUserAsync(id, curUserId);
            if (ch == null || !ch.Value) return _403();

            var user = await userContext.UserViewItemAsync(id);
            return (user == null) ? _404User(id) : user;
        }

        // POST: api/users
        [HttpPost("add")]
        public async Task<ActionResult<Reference>> AddUser(User user)
        {
            var ch = await userContext.CheckAdminAsync(curUserId);
            if (ch == null || !ch.Value) return _403();

            if (await userContext.ExistsAsync(user.Email)) return _409Email(user.Email);

            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

            userContext.Users.Add(user);
            await userContext.SaveChangesAsync();

            var reference = new Reference(user.Id);
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, reference);
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserUpdateItem update)
        {
            if (id == 1) return _403Protect();

            var user = await userContext.Users.FindAsync(id);
            if (user == null) return _404User(id);

            bool adminRequired = (user.IsEnabled != update.IsEnabled) || (user.IsAdmin != update.IsAdmin);

            ActionResult<bool> ch;
            ch = adminRequired ? await userContext.CheckAdminAsync(curUserId) :
                                 await userContext.CheckAdminOrSameUserAsync(id, curUserId);
            if (ch == null || !ch.Value) return _403();

            user.Name = update.Name;
            user.Email = update.Email;
            user.IsEnabled = update.IsEnabled;
            user.IsAdmin = update.IsAdmin;

            if (update.Password != null) user.Password = BCrypt.Net.BCrypt.HashPassword(update.Password);

            userContext.Entry(user).State = EntityState.Modified;

            await userContext.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (id==1) return _403Protect();

            var ch = await userContext.CheckAdminAsync(curUserId);
            if (ch == null || !ch.Value) return _403();

            var user = await userContext.Users.FindAsync(id);
            if (user == null) return _404User(id);

            userContext.Users.Remove(user);
            await userContext.SaveChangesAsync();

            return NoContent();
        }

    }
}
