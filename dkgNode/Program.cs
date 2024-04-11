using System.Text;
using System.Text.Json;
using dkgNode.Services;
using dkgNode.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddLogging();

var app = builder.Build();

var config = new DkgNodeConfig()
{  
    Port = int.Parse(Environment.GetEnvironmentVariable("DKG_NODE_SERVER_RPCS_PORT") ?? "5000"),
    Host = Environment.GetEnvironmentVariable("DKG_NODE_SERVER_RPCS_HOST") ?? "localhost",
    NiceName = Environment.GetEnvironmentVariable("DKG_NODE_SERVER_NAME")
};

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("DkgNode");
/* var server = */ new DkgNodeServer(config, logger);

// Create HttpClient instance
var httpClient = new HttpClient();
var jsonPayload = JsonSerializer.Serialize(config);
var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

var serviceNodeUrl = Environment.GetEnvironmentVariable("DKG_SERVICE_NODE_URL") ?? "http://localhost:8080";

HttpResponseMessage? response = null;
try
{
    response = await httpClient.PostAsync(serviceNodeUrl + "/api/nodes/register", httpContent);
}
catch (Exception e)
{
    logger.LogError($"Failed to register with {serviceNodeUrl}, Exception: {e.Message}");
}

if (response == null)
{
    logger.LogError($"Failed to register with {serviceNodeUrl}, no response received");
}
else
{
    /*var responseContent = */ await response.Content.ReadAsStringAsync();

    if (response.IsSuccessStatusCode)
    {
        logger.LogInformation($"Succesfully registered with {serviceNodeUrl}");
        app.Run();
    }
    else
    {
        logger.LogError($"Failed to register with {serviceNodeUrl}, Status: {response.StatusCode}");
    }
}