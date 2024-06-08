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

        readonly static string sqlScript_0_3_0 = @"
                START TRANSACTION;
                ALTER TABLE ""nodes"" ADD COLUMN ""status"" SMALLINT NOT NULL DEFAULT 0;
                INSERT INTO ""versions"" (""version"", ""date"") VALUES
                ('0.3.0', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');
                COMMIT;
                ";

        readonly static string sqlScript_0_4_3 = @"
            START TRANSACTION;

            CREATE EXTENSION IF NOT EXISTS ""uuid-ossp"";

            DROP INDEX IF EXISTS ""idx_nodes_host_port"";
            DROP INDEX IF EXISTS ""idx_nodes_public_key"";

            ALTER TABLE ""nodes"" DROP COLUMN IF EXISTS ""host"";
            ALTER TABLE ""nodes"" DROP COLUMN IF EXISTS ""port"";

            ALTER TABLE ""nodes"" ADD COLUMN ""guid"" UUID;
            UPDATE ""nodes"" SET ""guid"" = uuid_generate_v4();
            ALTER TABLE ""nodes"" ALTER COLUMN ""guid"" SET NOT NULL;

            CREATE UNIQUE INDEX ""idx_nodes_guid"" ON ""nodes"" (""guid"");           

            CREATE TABLE ""nodes_round_history"" (
              ""id""                 SERIAL PRIMARY KEY,
              ""round_id""           INTEGER NOT NULL REFERENCES ""rounds"" (""id"") ON DELETE CASCADE,
              ""node_id""            INTEGER NOT NULL REFERENCES ""nodes"" (""id"") ON DELETE CASCADE,
              ""node_final_status""  SMALLINT NOT NULL DEFAULT 0
            );

            CREATE INDEX ""idx_nodes_round_history_round_id"" ON ""nodes_round_history"" (""round_id"");

            ALTER TABLE ""rounds"" DROP COLUMN ""node_count"";
            ALTER TABLE ""rounds"" ADD COLUMN ""max_nodes"" INT NOT NULL DEFAULT 256;

            CREATE OR REPLACE FUNCTION update_nodes_round_history() RETURNS TRIGGER AS $$
            BEGIN
                IF OLD.round_id IS NOT NULL AND NEW.round_id IS NULL THEN
                    -- Check if a record already exists in nodes_round_history
                    IF EXISTS (SELECT 1 FROM nodes_round_history WHERE node_id = OLD.id AND round_id = OLD.round_id) THEN
                        -- Update the existing record
                        UPDATE nodes_round_history 
                        SET node_final_status = OLD.status
                        WHERE node_id = OLD.id AND round_id = OLD.round_id;
                    ELSE
                        -- Insert a new record
                        INSERT INTO nodes_round_history (round_id, node_id, node_final_status)
                        VALUES (OLD.round_id, OLD.id, OLD.status);
                    END IF;
                END IF;
                RETURN NEW;
             END;
             $$ LANGUAGE plpgsql;

             CREATE TRIGGER nodes_before_update_trigger
             BEFORE UPDATE ON nodes
             FOR EACH ROW
             EXECUTE PROCEDURE update_nodes_round_history();


            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('0.4.3', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

            COMMIT;
            ";

        readonly static string sqlScript_0_5_0 = @"
            START TRANSACTION;

            ALTER TABLE ""rounds"" ADD COLUMN ""timeout"" INT NOT NULL DEFAULT 120;

            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('0.5.0', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

            COMMIT;
            ";
        readonly static string sqlScript_0_6_2 = @"
            START TRANSACTION;

            ALTER TABLE ""rounds"" RENAME COLUMN ""timeout"" TO ""timeout2"";
            ALTER TABLE ""rounds"" ALTER COLUMN ""timeout2"" SET DEFAULT 30;
            ALTER TABLE ""rounds"" ADD COLUMN ""timeout3"" INT NOT NULL DEFAULT 30;
            ALTER TABLE ""rounds"" ADD COLUMN ""timeoutr"" INT NOT NULL DEFAULT 120;

            INSERT INTO ""users"" (""name"", ""email"", ""password"", ""is_enabled"", ""is_admin"")
                SELECT 'Admin', 'admin@example.com', '$2a$11$YygO9mUKjDioWY0CPj35LeCGY4SRnVHNdT2cFdVAGTSRwSpYHhytu', TRUE, TRUE
                WHERE NOT EXISTS(SELECT 1 FROM ""users"" WHERE ""email"" = 'admin@example.com');

            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('0.6.2', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

            COMMIT;
            ";

        readonly static string sqlScript_0_7_0 = @"
            START TRANSACTION;

            ALTER TABLE ""nodes_round_history"" ADD COLUMN ""node_random"" INTEGER;

            CREATE INDEX ""idx_nodes_round_history_node_id"" ON ""nodes_round_history"" (""node_id"");
            
            CREATE OR REPLACE FUNCTION update_nodes_round_history() RETURNS TRIGGER AS $$
              BEGIN
                IF OLD.round_id IS NOT NULL AND NEW.round_id IS NULL THEN
                    -- Check if a record already exists in nodes_round_history
                    IF EXISTS (SELECT 1 FROM nodes_round_history WHERE node_id = OLD.id AND round_id = OLD.round_id) THEN
                        -- Update the existing record
                        UPDATE nodes_round_history 
                        SET node_final_status = OLD.status
                        WHERE node_id = OLD.id AND round_id = OLD.round_id;
                    ELSE
                        -- Insert a new record
                        INSERT INTO nodes_round_history (round_id, node_id, node_final_status)
                        VALUES (OLD.round_id, OLD.id, OLD.status);
                    END IF;
                END IF;
                RETURN NEW;
              END;
            $$ LANGUAGE plpgsql;

            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('0.7.0', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

            COMMIT;
            ";

        private static string PuVersionUpdateQuery(string v)
        {
            return @"
            START TRANSACTION;
            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('" + v +"', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');
            COMMIT;
            ";
        }
        private static string VQuery(string v)
        {
            return $"SELECT COUNT(*) FROM versions WHERE version = '{v}';";
        }

        private static bool VCheck(string v, NpgsqlConnection connection)
        {
            var command = new NpgsqlCommand(VQuery(v), connection);
            var rows = command.ExecuteScalar();
            return (rows != null && (long)rows != 0);
        }

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
                var scriptCommand = new NpgsqlCommand(sqlScript_0_1_0, connection);
                int r = scriptCommand.ExecuteNonQuery();
            }
        }
        private static void PuVersionUpdate(string v, NpgsqlConnection connection)
        {
            if (!VCheck(v, connection))
            {
                var scriptCommand = new NpgsqlCommand(PuVersionUpdateQuery(v), connection);
                scriptCommand.ExecuteNonQuery();
            }
        }

        public static void EnsureVersion(string v, string s, NpgsqlConnection connection)
        {
            if (!VCheck(v, connection))
            {
                var scriptCommand = new NpgsqlCommand(s, connection);
                scriptCommand.ExecuteNonQuery();
            }
        }
        public static void Ensure(string connectionString)
        {

            using var connection = new NpgsqlConnection(connectionString);
            while (true)
            {
                try
                {
                    connection.Open();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create database connection: {ex.Message}");
                    Thread.Sleep(3000);
                }
            }

            Ensure_0_1_0(connection);
            EnsureVersion("0.3.0", sqlScript_0_3_0, connection);
            EnsureVersion("0.4.3", sqlScript_0_4_3, connection);
            EnsureVersion("0.5.0", sqlScript_0_5_0, connection);
            EnsureVersion("0.6.2", sqlScript_0_6_2, connection);
            EnsureVersion("0.7.0", sqlScript_0_7_0, connection);
        }
    }


}
