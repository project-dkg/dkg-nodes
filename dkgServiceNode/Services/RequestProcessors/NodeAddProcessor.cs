using System.Collections.Concurrent;
using dkgCommon.Constants;
using dkgServiceNode.Data;
using dkgServiceNode.Models;
using dkgServiceNode.Services.Cache;
using Npgsql;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace dkgServiceNode.Services.RequestProcessors
{
    public class NodeAddProcessor : IDisposable
    {
        private readonly int _database_reconnect_delay = 3000;
        private readonly int _queue_reparse_delay = 1000;
        private readonly int _bulk_insert_limit = 10000;

        private readonly ConcurrentQueue<Node> requestQueue = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();

        private Task? backgroundTask = null;
        private NodeCompositeContext? ncContext = null;

        private volatile bool isRunning = false;
        private readonly ILogger logger;
        private readonly string connectionString;
        private bool disposed = false;

        public NodeAddProcessor(
            string connectionStr,
            ILogger<NodeAddProcessor> lgger
        )
        {
            connectionString = connectionStr;
            logger = lgger;
        }

        public void Start(NodeCompositeContext nContext)
        {
            if (isRunning)
            {
                logger.LogWarning("Node Request Processor is already running. 'Start' ignored.");
            }
            else
            {
                isRunning = true;
                ncContext = nContext;
                backgroundTask = Task.Run(ProcessRequests, cancellationTokenSource.Token);
                logger.LogInformation("Request Processor has been started.");
            }
        }

        public void Stop()
        {
            if (!isRunning)
            {
                logger.LogWarning("Request Processor is not running. 'Stop' ignored.");
            }
            else
            {
                cancellationTokenSource.Cancel();
                backgroundTask?.Wait();
                isRunning = false;
                logger.LogInformation("Request Processor has been stopped.");
            }
        }

        public void EnqueueRequest(Node request)
        {
            requestQueue.Enqueue(request);
        }

        private async Task ProcessRequests()
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
                    await Task.Delay(_database_reconnect_delay, cancellationTokenSource.Token);
                }
            }

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                var requests = new List<Node>();

                while (requestQueue.TryDequeue(out var request) && requests.Count < _bulk_insert_limit)
                {
                    requests.Add(request);
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
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("An error occurred while processing requests: {msg}.", ex.Message);
                    }
                }
                else
                {
                    await Task.Delay(_queue_reparse_delay, cancellationTokenSource.Token);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Stop();
                    cancellationTokenSource.Dispose();
                    backgroundTask?.Dispose();
                }
                ncContext = null;
                disposed = true;
            }
        }

        ~NodeAddProcessor()
        {
            Dispose(false);
        }
    }
}
