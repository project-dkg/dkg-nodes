using dkgNode.Services;
using dkgNode.Models;
using System.Runtime.Loader;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddLogging();

var app = builder.Build();

var config = new DkgNodeConfig()
{  
    Port = int.Parse(Environment.GetEnvironmentVariable("DKG_NODE_SERVER_GRPC_PORT") ?? "5000"),
    Host = Environment.GetEnvironmentVariable("DKG_NODE_SERVER_GRPC_HOST") ?? "localhost",
    NiceName = Environment.GetEnvironmentVariable("DKG_NODE_SERVER_NAME")
};
var serviceNodeUrl = Environment.GetEnvironmentVariable("DKG_SERVICE_NODE_URL") ?? "http://localhost:8080";

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("DkgNode");
var server = new DkgNodeServer(config, serviceNodeUrl, logger);

var cts = new CancellationTokenSource();
AssemblyLoadContext.Default.Unloading += ctx =>
{
    server.Shutdown();
    cts.Cancel();
};
server.Start();
app.Run();
