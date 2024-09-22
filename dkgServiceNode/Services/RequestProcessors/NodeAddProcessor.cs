using System.Collections.Concurrent;
using dkgServiceNode.Models;
using Npgsql;

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
        private volatile bool isRunning = false;
        private readonly ILogger logger;
        private readonly string connectionString;
        private bool disposed = false;

        public NodeAddProcessor(string connectionStr, ILogger<NodeAddProcessor> lgger)
        {
            connectionString = connectionStr;
            logger = lgger;
        }

        public void Start()
        {
            if (isRunning)
            {
                logger.LogWarning("Node Request Processor is already running. 'Start' ignored.");
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
                        using var writer = dbConnection.BeginBinaryImport("COPY nodes (address, name) FROM STDIN (FORMAT BINARY)");

                        foreach (var request in requests)
                        {
                            writer.StartRow();
                            writer.Write(request.Address, NpgsqlTypes.NpgsqlDbType.Text);
                            writer.Write(request.Name, NpgsqlTypes.NpgsqlDbType.Text);
                        }

                        await writer.CompleteAsync(cancellationTokenSource.Token);

                        // Retrieve the IDs and addresses of the inserted rows
                        var insertedNodes = new List<(string Address, int Id)>();
                        using (var command = new NpgsqlCommand("SELECT address, id FROM nodes WHERE address = ANY(@addresses)", dbConnection))
                        {
                            command.Parameters.AddWithValue("addresses", requests.Select(r => r.Address).ToArray());
                            using var reader = await command.ExecuteReaderAsync(cancellationTokenSource.Token);
                            while (await reader.ReadAsync(cancellationTokenSource.Token))
                            {
                                var address = reader.GetString(0);
                                var id = reader.GetInt32(1);
                                insertedNodes.Add((address, id));
                            }
                        }

                        foreach (var (Address, Id) in insertedNodes)
                        {
                            logger.LogInformation("Inserted node with Address: {address}, ID: {id}", Address, Id);
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
                disposed = true;
            }
        }

        ~NodeAddProcessor()
        {
            Dispose(false);
        }
    }
}
