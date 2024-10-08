﻿// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
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
        readonly static string sqlScript_0_14_0 = @"
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
            INSERT INTO ""users"" (""name"", ""email"", ""password"", ""is_enabled"", ""is_admin"") VALUES
            ('Admin', 'admin@example.com', '$2a$11$YygO9mUKjDioWY0CPj35LeCGY4SRnVHNdT2cFdVAGTSRwSpYHhytu', TRUE, TRUE);

            DROP TABLE IF EXISTS ""rounds"";

            CREATE TABLE ""rounds"" (
              ""id""              SERIAL PRIMARY KEY,
              ""status""          SMALLINT NOT NULL DEFAULT 0,
              ""result""          INT,
              ""max_nodes""       INT NOT NULL DEFAULT 256,
              ""timeout2""        INT NOT NULL DEFAULT 30,
              ""timeout3""        INT NOT NULL DEFAULT 30,
              ""timeoutr""        INT NOT NULL DEFAULT 120,
              ""created""         TIMESTAMP NOT NULL DEFAULT now(),
              ""modified""        TIMESTAMP NOT NULL DEFAULT now()
            );

            DROP TABLE IF EXISTS ""nodes"";

            CREATE TABLE ""nodes"" (
              ""address""         VARCHAR(128) NOT NULL PRIMARY KEY, 
              ""name""            VARCHAR(64) NOT NULL DEFAULT '--'
            );

            DROP TABLE IF EXISTS ""nodes_round_history"";

            CREATE TABLE ""nodes_round_history"" (
              ""id""                 SERIAL PRIMARY KEY,
              ""round_id""           INTEGER NOT NULL REFERENCES ""rounds"" (""id"") ON DELETE CASCADE,
              ""node_address""       VARCHAR(128) NOT NULL REFERENCES ""nodes"" (""address"") ON DELETE CASCADE,
              ""node_final_status""  SMALLINT NOT NULL DEFAULT 0,
              ""node_random""        INTEGER
            );

            CREATE INDEX ""idx_nodes_round_history_round_id"" ON ""nodes_round_history"" (""round_id"");
            CREATE INDEX ""idx_nodes_round_history_node_address"" ON ""nodes_round_history"" (""node_address"");

            CREATE OR REPLACE PROCEDURE upsert_node_round_history(
                p_node_address VARCHAR(128),
                p_round_id INT,
                p_node_final_status SMALLINT,
                p_node_random INT
            )
            LANGUAGE plpgsql
            AS $$
            BEGIN
            -- Check if the record exists
                IF EXISTS(
                    SELECT 1
                    FROM nodes_round_history
                    WHERE node_address = p_node_address AND round_id = p_round_id
                ) THEN
            -- Update the existing record and return it
                    UPDATE nodes_round_history
                    SET node_final_status = p_node_final_status,
                        node_random = p_node_random
                    WHERE node_address = p_node_address AND round_id = p_round_id;
                ELSE
            -- Insert a new record and return it
                    INSERT INTO nodes_round_history(node_address, round_id, node_final_status, node_random)
                    VALUES(p_node_address, p_round_id, p_node_final_status, p_node_random);
                END IF;
            END;
            $$;

            CREATE OR REPLACE PROCEDURE bulk_upsert_node_round_history(
                p_items JSON
            )
            LANGUAGE plpgsql
            AS $$
            DECLARE
                item JSON;
                node_address VARCHAR(128);
                round_id INT;
                node_final_status SMALLINT;
                node_random INT;
            BEGIN
                FOR item IN SELECT * FROM json_array_elements(p_items)
                LOOP
                    node_address := (item->>'node_address')::VARCHAR(128);
                    round_id := (item->>'round_id')::INT;
                    node_final_status := (item->>'node_final_status')::SMALLINT;
                    node_random := (item->>'node_random')::INT;

                    -- Call the existing upsert_node_round_history procedure
                    CALL upsert_node_round_history(node_address, round_id, node_final_status, node_random);
                END LOOP;
            END;
            $$;

            CREATE OR REPLACE PROCEDURE insert_node_with_round_history(
                p_node_address VARCHAR(128),
                p_node_name VARCHAR(64),
                p_round_id INT,
                p_node_final_status SMALLINT,
                p_node_random INT
            )
            LANGUAGE plpgsql
            AS $$
            BEGIN
                -- 
                INSERT INTO nodes(address, name)
                VALUES(p_node_address, p_node_name)
                ON CONFLICT (address) DO NOTHING;

                -- 
                INSERT INTO nodes_round_history(node_address, round_id, node_final_status, node_random)
                VALUES(p_node_address, p_round_id, p_node_final_status, p_node_random);

            END;
            $$;

           CREATE OR REPLACE PROCEDURE bulk_insert_node_with_round_history(
                p_items JSON
            )
            LANGUAGE plpgsql
            AS $$
            DECLARE
                item JSON;
                node_address VARCHAR(128);
                node_name VARCHAR(64);
                round_id INT;
                node_final_status SMALLINT;
                node_random INT;
            BEGIN
                FOR item IN SELECT * FROM json_array_elements(p_items)
                LOOP
                    node_address := (item->>'node_address')::VARCHAR(128);
                    node_name := (item->>'node_name')::VARCHAR(64);
                    round_id := (item->>'round_id')::INT;
                    node_final_status := (item->>'node_final_status')::SMALLINT;
                    node_random := (item->>'node_random')::INT;

                    -- Call the existing insert_node_with_round_history procedure
                    CALL insert_node_with_round_history(node_address, node_name, round_id, node_final_status, node_random);
                END LOOP;
            END;
            $$;

            DROP TABLE IF EXISTS ""versions"";

            CREATE TABLE ""versions"" (
              ""id""      SERIAL PRIMARY KEY,
              ""version"" VARCHAR(16) NOT NULL,
              ""date""    DATE NOT NULL DEFAULT now()
            );

            INSERT INTO ""versions"" (""version"", ""date"") VALUES
            ('0.14.0', '" + DateTime.Now.ToString("yyyy-MM-dd") + @"');

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

        public static int Ensure_0_14_0(NpgsqlConnection connection)
        {
            // Check if table 'versions' exists
            var sql = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'versions';";
            var command = new NpgsqlCommand(sql, connection);
            var rows = command.ExecuteScalar();

            int r = 0;

            if (rows != null && (long)rows != 0)
            {
                sql = "SELECT COUNT(*) FROM versions WHERE version = '0.14.0';";
                command = new NpgsqlCommand(sql, connection);
                rows = command.ExecuteScalar();
            }

            if (rows == null || (long)rows == 0)
            {
                var scriptCommand = new NpgsqlCommand(sqlScript_0_14_0, connection);
                r = scriptCommand.ExecuteNonQuery();
            }

            return r;
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
        public static void Ensure(NpgsqlConnection connection, ILogger logger)
        {
            try
            {
                logger.LogInformation("Initializing database at 0.14.0");
                Ensure_0_14_0(connection);
                logger.LogInformation("Tagging 0.14.1");
                PuVersionUpdate("0.14.1", connection);
            }
            catch (Exception ex)
            {
                logger.LogError("Error initializing database: {msg}", ex.Message);
            }
        }
    }


}
