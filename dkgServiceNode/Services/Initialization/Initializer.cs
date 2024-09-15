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

using dkgCommon.Constants;
using dkgServiceNode.Data;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;
using Npgsql;

namespace dkgServiceNode.Services.Initialization
{
    public class Initializer(
        NodesCache nodesCache,
        RoundsCache roundsCache,
        NodesRoundHistoryCache nodesRoundHistoryCache,
        ILogger logger)
    {
        private readonly NodesCache _nodesCache = nodesCache;
        private readonly RoundsCache _roundsCache = roundsCache;
        private readonly NodesRoundHistoryCache _nodesRoundHistoryCache = nodesRoundHistoryCache;
        private readonly ILogger _logger = logger;

        public void Initialize(string connectionString) 
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
                    _logger.LogError("Failed to create database connection: {msg}", ex.Message);
                    Thread.Sleep(3000);
                }
            }

            DbEnsure.Ensure(connection, _logger);
            InitializeNodesCache(connection);
            ClearStaledRounds(connection);
            InitializeRoundsCache(connection);
            InitializeNodesRoundHistoryCache(connection);
            InitializeHistoryCounters(connection);
        }

        private void InitializeNodesCache(NpgsqlConnection connection)
        {
            try
            {
                string selectQuery = "SELECT * FROM nodes";

                using var command = new NpgsqlCommand(selectQuery, connection);
                using var reader = command.ExecuteReader();

                int counter = 0;

                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string address = reader.GetString(1);
                    string name = reader.GetString(2);

                    Node node = new()
                    {
                        Id = id,
                        Address = address,
                        Name = name
                    };

                    _nodesCache.SaveNodeToCacheNoLock(node);
                    counter++;
                }
                _logger.LogInformation("Populated cache with {counter} nodes from database", counter);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to populate nodes cache from database: {msg}", ex.Message);
            }
        }

        private void ClearStaledRounds(NpgsqlConnection connection)
        {
            try
            {
                string updateQuery = @"
                    UPDATE rounds
                    SET status = @FailedStatus
                    WHERE status NOT IN (@FinishedStatus, @CancelledStatus, @FailedStatus, @NotStartedStatus)";

                using var command = new NpgsqlCommand(updateQuery, connection);
                command.Parameters.AddWithValue("@CancelledStatus", (short)RStatus.Cancelled);
                command.Parameters.AddWithValue("@FinishedStatus", (short)RStatus.Finished);
                command.Parameters.AddWithValue("@FailedStatus", (short)RStatus.Failed);
                command.Parameters.AddWithValue("@NotStartedStatus", (short)RStatus.NotStarted);

                int affectedRows = command.ExecuteNonQuery();
                _logger.LogInformation("Cleared staled rounds, updated {count} rows", affectedRows);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to clear staled rounds: {msg}", ex.Message);
            }
        }

        private void InitializeRoundsCache(NpgsqlConnection connection)
        {
            try
            {
                string selectQuery = "SELECT * FROM rounds";

                using var command = new NpgsqlCommand(selectQuery, connection);
                using var reader = command.ExecuteReader();

                int counter = 0;

                while (reader.Read())
                {
                    Round round = new()
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        StatusValue = reader.GetInt16(reader.GetOrdinal("status")),
                        MaxNodes = reader.GetInt32(reader.GetOrdinal("max_nodes")),
                        Timeout2 = reader.GetInt32(reader.GetOrdinal("timeout2")),
                        Timeout3 = reader.GetInt32(reader.GetOrdinal("timeout3")),
                        TimeoutR = reader.GetInt32(reader.GetOrdinal("timeoutr")),
                        Result = reader.IsDBNull(reader.GetOrdinal("result")) ? null : reader.GetInt32(reader.GetOrdinal("result")),
                        CreatedOn = reader.GetDateTime(reader.GetOrdinal("created")),
                        ModifiedOn = reader.GetDateTime(reader.GetOrdinal("modified"))
                    };

                    _roundsCache.SaveRoundToCacheNoLock(round);
                    counter++;
                }
                _logger.LogInformation("Populated cache with {counter} rounds from database", counter);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to populate rounds cache from database: {msg}", ex.Message);
            }
        }
        private void InitializeNodesRoundHistoryCache(NpgsqlConnection connection)
        {
            try
            {
                string selectQuery = @"
                    SELECT DISTINCT ON (node_id)
                        id,
                        round_id,
                        node_id,
                        node_final_status,
                        node_random
                    FROM
                        nodes_round_history
                    ORDER BY
                        node_id,
                        round_id DESC,
                        id ASC;";

                using var command = new NpgsqlCommand(selectQuery, connection);
                using var reader = command.ExecuteReader();

                int counter = 0;
                
                while (reader.Read())
                {
                    NodesRoundHistory history = new()
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        RoundId = reader.GetInt32(reader.GetOrdinal("round_id")),
                        NodeId = reader.GetInt32(reader.GetOrdinal("node_id")),
                        NodeFinalStatusValue = reader.GetInt16(reader.GetOrdinal("node_final_status")),
                        NodeRandom = reader.IsDBNull(reader.GetOrdinal("node_random")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("node_random"))
                    };

                    _nodesRoundHistoryCache.SaveNodesRoundHistoryToCacheNoLock(history);
                    counter++;
                }

                _logger.LogInformation("Populated cache with {count} nodes/round histories from database", counter);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to populate nodes/round history cache from database: {msg}", ex.Message);
            }
        }

        private void InitializeHistoryCounters(NpgsqlConnection connection)
        {
            try
            {
                string selectQuery = @"
                        SELECT 
                            round_id, 
                            node_final_status, 
                            COUNT(*)
                        FROM 
                            nodes_round_history
                        GROUP BY 
                            round_id, 
                            node_final_status;"; 

                using var command = new NpgsqlCommand(selectQuery,connection);
                
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    int roundId = reader.GetInt32(0);
                    NStatus status = (NStatus)reader.GetInt32(1);
                    int count = reader.GetInt32(2);

                    _nodesRoundHistoryCache.SetHistoryCounterNoLock(roundId, status, count);
                }
                _logger.LogInformation("Populated cache with history counters from database");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to populate history counters from database: {msg}", ex.Message);
            }
        }
    }
}


