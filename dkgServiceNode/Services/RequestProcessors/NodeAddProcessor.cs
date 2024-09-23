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

using System.Collections.Concurrent;
using dkgServiceNode.Models;
using Npgsql;

namespace dkgServiceNode.Services.RequestProcessors
{
    public class NodeAddProcessor : RequestProcessorBase
    {
        public NodeAddProcessor(
            string connectionStr,
            int bInsertLimit,
            int qReparseDelay,
            ILogger<NodeAddProcessor> lgger
        ) : base(connectionStr, bInsertLimit, qReparseDelay, lgger)
        {
        }

        public void EnqueueRequest(Node request)
        {
            requestQueue.Enqueue(request);
        }

        protected override async Task ProcessRequests()
        {
            using var dbConnection = new NpgsqlConnection(connectionString);
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await dbConnection.OpenAsync(cancellationTokenSource.Token);
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to create database connection: {msg}", ex.Message);
                    await Task.Delay(databaseReconnectDelay, cancellationTokenSource.Token);
                }
            }

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var requests = new List<Node>();
                int counter = 0;
                while (requestQueue.TryDequeue(out var request) && requests.Count < bulkInsertLimit)
                {
                    requests.Add(request);
                    if (counter++ > bulkRestLimit)
                    {
                        counter = 0;
                        await Task.Delay(0);
                    }
                }

                if (requests.Count > 0)
                {
                    try
                    {
                        // Convert the list of requests to a JSON array
                        var jsonItems = System.Text.Json.JsonSerializer.Serialize(requests.Select(r => new
                        {
                            node_address = r.Address,
                            node_name = r.Name,
                            round_id = r.RoundId,
                            node_final_status = r.StatusValue,
                            node_random = r.Random
                        }));

                        // Call the bulk_insert_node_with_round_history procedure
                        using (var command = new NpgsqlCommand("CALL bulk_insert_node_with_round_history(@p_items)", dbConnection))
                        {
                            command.Parameters.AddWithValue("p_items", NpgsqlTypes.NpgsqlDbType.Json, jsonItems);
                            await command.ExecuteNonQueryAsync();
                        }

                        // Finalize registration for each request
                        foreach (var request in requests)
                        {
                            ncContext!.FinalizeRegistration(request);
                            if (counter++ > bulkRestLimit)
                            {
                                counter = 0;
                                await Task.Delay(0);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("An error occurred while processing requests: {msg}.", ex.Message);
                    }
                }
                else
                {
                    await Task.Delay(queueReparseDelay, cancellationTokenSource.Token);
                }
            }
        }
    }
}
