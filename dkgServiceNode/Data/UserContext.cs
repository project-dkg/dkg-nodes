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
using Microsoft.EntityFrameworkCore;

using dkgServiceNode.Models;

namespace dkgServiceNode.Data
{
    public class UserContext : DbContext
    {
        public UserContext(DbContextOptions<UserContext> options) : base(options) { }
        public DbSet<User> Users { get; set; }
        public async Task<bool> Exists(int id)
        {
            return await Users.AnyAsync(e => e.Id == id);
        }
        public async Task<bool> Exists(string email)
        {
            return await Users.AnyAsync(e => e.Email == email);
        }
        public async Task<List<UserViewItem>> UserViewItems()
        {
            return await Users.AsNoTracking().Select(x => new UserViewItem(x)).ToListAsync();
        }
        public async Task<UserViewItem?> UserViewItem(int id)
        {
            var user = await Users.AsNoTracking().Where(x => x.Id == id).Select(x => new UserViewItem(x)).FirstOrDefaultAsync();
            return user ?? null;
        }
        public async Task<ActionResult<bool>> CheckAdmin(int cuid)
        {
            var curUser = await UserViewItem(cuid);
            return curUser != null && curUser.IsAdmin;
        }
        public async Task<ActionResult<bool>> CheckAdminOrSameUser(int id, int cuid)
        {
            if (cuid == 0) return false;
            if (cuid == id) return true;
            return await CheckAdmin(cuid);
        }
        public bool CheckSameUser(int id, int cuid)
        {
            if (cuid == 0) return false;
            if (cuid == id) return true;
            return false;
        }
    }
}
