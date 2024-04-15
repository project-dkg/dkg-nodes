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

using Npgsql;

namespace dkgServiceNode.Data
{
    public static class DbEnsure
    {

        // Modify the table creation script to set the default value of "created" column to current date and time
        readonly static string sqlScript_0_1_0 = @"
            START TRANSACTION;

            DROP TABLE IF EXISTS ""users"";

            CREATE TABLE ""users"" (
              ""id""              SERIAL PRIMARY KEY,
              ""name""            VARCHAR(64) NOT NULL,
              ""email""           VARCHAR(64) NOT NULL,
              ""password""        VARCHAR(64) NOT NULL,
              ""is_enabled""      BOOLEAN NOT NULL DEFAULT TRUE,
              ""is_admin""        BOOLEAN NOT NULL DEFAULT FALSE
            );

            CREATE UNIQUE INDEX ""idx_users_email"" ON ""users"" (""email"");

            INSERT INTO ""users"" (""name"", ""email"", ""password"", ""is_enabled"", ""is_admin"") VALUES
            ('maxirmx', 'maxirmx@sw.consulting', '$2a$11$s27FRc4jeV9F44dUCsA4hOx6JTtrdSVq1rYLmesa3anbaa937lrfW', TRUE, TRUE);

            DROP INDEX IF EXISTS ""idx_nodes_host_port"";
            DROP TABLE IF EXISTS ""nodes"";

            DROP TABLE IF EXISTS ""rounds"";

            CREATE TABLE ""rounds"" (
              ""id""              SERIAL PRIMARY KEY,
              ""status""          SMALLINT NOT NULL DEFAULT 0,
              ""node_count""      INT NOT NULL DEFAULT 0,
              ""result""          INT,
              ""created""         TIMESTAMP NOT NULL DEFAULT now(),
              ""modified""        TIMESTAMP NOT NULL DEFAULT now()
            );

            CREATE TABLE ""nodes"" (
              ""id""              SERIAL PRIMARY KEY,
              ""host""            VARCHAR(64) NOT NULL DEFAULT 'localhost',
              ""port""            INT NOT NULL DEFAULT 0,
              ""name""            VARCHAR(64) NOT NULL DEFAULT '--',
              ""public_key""      VARCHAR(128) NOT NULL DEFAULT '',
              ""round_id""        INTEGER REFERENCES ""rounds"" (""id"") ON DELETE RESTRICT
            );

            CREATE INDEX ""idx_nodes_host_port"" ON ""nodes"" (""host"", ""port"");

            DROP TABLE IF EXISTS ""versions"";

            CREATE TABLE ""versions"" (
              ""id""      SERIAL PRIMARY KEY,
              ""version"" VARCHAR(16) NOT NULL,
              ""date""    DATE NOT NULL DEFAULT now()
            );

            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('0.1.0', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

            COMMIT;
            ";

        readonly static string sqlScript_0_2_0 = @"
            START TRANSACTION;
            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('0.2.0', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

            COMMIT;
            ";


        public static void Ensure_0_1_0(NpgsqlConnection connection)
        {
            // Check if table 'versions' exists
            var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'versions';";
            var command = new NpgsqlCommand(sql, connection);
            var rows = command.ExecuteScalar();

            if (rows != null && (long)rows != 0)
            {
                sql = "SELECT COUNT(*) FROM versions WHERE version = '0.1.0';";
                command = new NpgsqlCommand(sql, connection);
                rows = command.ExecuteScalar();
            }

            if (rows == null || (long)rows == 0)
            {
                using var scriptCommand = new NpgsqlCommand(sqlScript_0_1_0, connection);
                int r = scriptCommand.ExecuteNonQuery();
            }
        }

        public static void Ensure_0_2_0(NpgsqlConnection connection)
        {
            // Check if table 'versions' exists
            var sql = "SELECT COUNT(*) FROM versions WHERE version = '0.2.0';";
            using var command = new NpgsqlCommand(sql, connection);
            var rows = command.ExecuteScalar();

            if (rows == null || (long)rows == 0)
            {
                using var scriptCommand = new NpgsqlCommand(sqlScript_0_2_0, connection);
                int r = scriptCommand.ExecuteNonQuery();
            }
        }
        public static void Ensure(string connectionString)
        {

            using (var connection = new NpgsqlConnection("Host=dkgservice_db;Port=5432;Database=dkgservice;Username=postgres;Password=postgres"))
            {
                connection.Open();
                Ensure_0_1_0(connection);
                Ensure_0_2_0(connection);
            }
        }
    }


}
