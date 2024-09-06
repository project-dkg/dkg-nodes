using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using dkgServiceNode.Models;
using System.Xml.Linq;
using dkgServiceNode.Data;
using dkgServiceNode.Controllers;
using Microsoft.Extensions.Logging.Configuration;

namespace dkgServiceNode.Services.RequestProcessor
{
    public class RequestProcessor(ILogger<RequestProcessor> lgger)
    {
        private readonly ConcurrentQueue<Request> requestQueue = new();
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private Task? backgroundTask = null;
        private bool isRunning = false;
        private DkgDbContext? dbContext;
        private readonly ILogger logger = lgger;

        public enum Command
        {
            Add,
            Update,
            Delete
        }

        public class Request(Command commandType, object data)
        {
            public Command CommandType { get; } = commandType;
            public object Data { get; } = data;
        }

        public void Start(DkgDbContext dC)
        {
            if (isRunning)
            {
                logger.LogWarning("Request Processor is already running. 'Start' ignored.");
            }
            else
            {

                isRunning = true;
                dbContext = dC;
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
                backgroundTask = null;
                logger.LogInformation("Request Processor has been stopped.");
            }
        }

        public void EnqueueRequest(Request request)
        {
            requestQueue.Enqueue(request);
        }

        public void EnqueueRequest(Command commandType, object data)
        {
            requestQueue.Enqueue(new Request(commandType, data));
        }

        private async Task ProcessRequests()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested && dbContext != null)
            {
                if (requestQueue.TryDequeue(out var request))
                {
                    try
                    {
                        await HandleRequestAsync(request);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("An error occurred while processing a request: {msg}.", ex.Message );
                    }
                }
                await Task.Delay(100);
            }
        }

        private async Task HandleRequestAsync(Request request)
        {
            switch (request.CommandType)
            {
                case Command.Add:
                    await HandleAddAsync(request.Data);
                    break;
                case Command.Update:
                    await HandleUpdateAsync(request.Data);
                    break;
                case Command.Delete:
                    await HandleDeleteAsync(request.Data);
                    break;
                default:
                    logger.LogError("Unknown command type '{command}'.", request.CommandType);
                    break;
            }
        }

        private async Task HandleAddAsync(object data)
        {
            if (data is Node node)
            {
                dbContext!.Nodes.Add(node);
                await dbContext.SaveChangesAsync(cancellationTokenSource.Token.IsCancellationRequested);
            }
            else if (data is Round round)
            {
                dbContext!.Rounds.Add(round);
                await dbContext.SaveChangesAsync(cancellationTokenSource.Token.IsCancellationRequested);
            }
        }

        private async Task HandleUpdateAsync(object data)
        {
            if (data is Node node)
            {
                dbContext!.Nodes.Update(node);
                await dbContext.SaveChangesAsync(cancellationTokenSource.Token.IsCancellationRequested);
            }
            else if (data is Round round)
            {
                dbContext!.Rounds.Update(round);
                await dbContext.SaveChangesAsync(cancellationTokenSource.Token.IsCancellationRequested);
            }
        }

        private async Task HandleDeleteAsync(object data)
        {
            if (data is Node node)
            {
                dbContext!.Nodes.Remove(node);
                await dbContext.SaveChangesAsync(cancellationTokenSource.Token.IsCancellationRequested);
            }
            else if (data is Round round)
            {
                dbContext!.Rounds.Remove(round);
                await dbContext.SaveChangesAsync(cancellationTokenSource.Token.IsCancellationRequested);
            }
        }
    }
}
