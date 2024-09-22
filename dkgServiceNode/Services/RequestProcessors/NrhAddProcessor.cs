using System.Collections.Concurrent;
using System.Text.Json;
using dkgServiceNode.Data;
using dkgServiceNode.Models;
using Npgsql;

namespace dkgServiceNode.Services.RequestProcessors
{
    public class NrhAddProcessor : IDisposable
    {
        private readonly int _database_reconnect_delay = 3000;
        private readonly int _queue_reparse_delay = 1000;
        private readonly int _bulk_upsert_limit = 10000;

        private readonly ConcurrentQueue<Node> requestQueue = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private Task? backgroundTask = null;
        private volatile bool isRunning = false;
        private readonly ILogger logger;
        private readonly string connectionString;
        private bool disposed = false;

        public NrhAddProcessor(string connectionStr, ILogger<NrhAddProcessor> lgger)
        {
            connectionString = connectionStr;
            logger = lgger;
        }

        public void Start()
        {
            if (isRunning)
            {
                logger.LogWarning("Nodes Round History Request Processor is already running. 'Start' ignored.");
            }
            else
            {
                isRunning = true;
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
            if (request.RoundId != null)
            {
                requestQueue.Enqueue(request);
            }
            else
            {
                logger.LogWarning("Ignoring history record with null RoundId.");
            }
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

                while (requestQueue.TryDequeue(out var request) && requests.Count < _bulk_upsert_limit)
                {
                    requests.Add(request);
                }

                if (requests.Count > 0)
                {
                    try
                    {
                        var items = requests.Select(r => new
                        {
                            node_address = r.Address,
                            round_id = r.RoundId,
                            node_final_status = r.StatusValue,
                            node_random = r.Random
                        }).ToArray();

                        var jsonItems = JsonSerializer.Serialize(items);

                        using var command = new NpgsqlCommand("CALL bulk_upsert_node_round_history(@p_items)", dbConnection);
                        var parameter = new NpgsqlParameter("p_items", NpgsqlTypes.NpgsqlDbType.Json)
                        {
                            Value = jsonItems
                        };
                        command.Parameters.Add(parameter);

                        await command.ExecuteNonQueryAsync(cancellationTokenSource.Token);
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
                disposed = true;
            }
        }

        ~NrhAddProcessor()
        {
            Dispose(false);
        }
    }
}

