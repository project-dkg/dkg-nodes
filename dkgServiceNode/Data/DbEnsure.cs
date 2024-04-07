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

using Microsoft.EntityFrameworkCore;

namespace dkgServiceNode.Data
{
    public static class DbEnsure
    {

        readonly static string sqlScript_0_1_0 = @"
    START TRANSACTION;

    DROP TABLE IF EXISTS ""users"";

    CREATE TABLE ""users"" (
      ""id""              SERIAL PRIMARY KEY,
      ""name""            VARCHAR(16) NOT NULL,
      ""email""           VARCHAR(64) NOT NULL,
      ""password""        VARCHAR(64) NOT NULL,
      ""api_key""         VARCHAR(64) NOT NULL,
      ""api_secret""      VARCHAR(64) NOT NULL,
      ""is_enabled""      BOOLEAN NOT NULL DEFAULT TRUE,
      ""is_admin""        BOOLEAN NOT NULL DEFAULT FALSE
    );

    CREATE UNIQUE INDEX ""idx_users_email"" ON ""users"" (""email"");

    INSERT INTO ""users"" (""name"", ""email"", ""password"", ""api_key"", ""api_secret"", ""is_enabled"", ""is_admin"") VALUES
    ('maxirmx', 'maxirmx@sw.consulting', '$2a$11$PUWwhEUzqrusmtrDsH4wguSDVx1kmGcksoU1rOKjAcWkGKdGA55ZK', '', '', TRUE, TRUE);

    DROP TABLE IF EXISTS ""versions"";

    CREATE TABLE ""versions"" (
      ""id""      SERIAL PRIMARY KEY,
      ""version"" VARCHAR(16) NOT NULL,
      ""date""    DATE NOT NULL
    );

    INSERT INTO ""versions"" (""version"", ""date"") VALUES
    ('0.1.0', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

    COMMIT;
    ";

        public static int Ensure_0_1_0(DbContext context)
        {
            // Check if table 'versions' exists
            var sql = "SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'versions';";
            var rows = context.Database.ExecuteSqlRaw(sql);

            //if (rows == 0)
            {
                rows = context.Database.ExecuteSqlRaw(sqlScript_0_1_0);
            }
            return rows;
        }
    
        public static void Ensure(DbContext context)
        {
            context.Database.EnsureCreated();
            Ensure_0_1_0(context);
        }
    }


}
