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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dkgServiceNode.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public required string Name { get; set; }

        [Required]
        [Column("email")]
        public required string Email { get; set; }

        [Required]
        [Column("password")]
        public required string Password { get; set; }

        [Required]
        [Column("is_enabled")]
        public required bool IsEnabled { get; set; }

        [Required]
        [Column("is_admin")]
        public required bool IsAdmin { get; set; }
    }

    public class UserViewItem(User user)
    {
        public int Id { get; set; } = user.Id;
        public string Name { get; set; } = user.Name;
        public string Email { get; set; } = user.Email;
        public bool IsEnabled { get; set; } = user.IsEnabled;
        public bool IsAdmin { get; set; } = user.IsAdmin;
    }

    public class UserViewItemWithJWT(User user) : UserViewItem(user)
    {
        public string Token { get; set; } = "";
    }

    public class UserUpdateItem
    {
        public required string Name { get; set; }
        public required string Email { get; set; }
        public string? Password { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsAdmin { get; set; }
    }
}
