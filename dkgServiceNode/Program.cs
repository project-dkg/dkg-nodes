using Microsoft.EntityFrameworkCore;

using dkgServiceNode.Data;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.Cache;
using dkgServiceNode.Services.Initialization;
using dkgServiceNode.Services.RequestProcessors;
using dkgServiceNode.Services.RequestLimitingMiddleware;
using dkgServiceNode.Services.RoundRunner;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

int controllers = configuration.GetValue<int?>("Controllers") ?? 5;

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCors();

// configure DI for application services
builder.Services.AddScoped<IJwtUtils, JwtUtils>();
builder.Services.AddHttpContextAccessor();

// Configure Jwt secret
builder.Services.Configure<AppSecret>(configuration.GetSection("AppSecret"));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<NodesCache>();
builder.Services.AddSingleton<RoundsCache>();
builder.Services.AddSingleton<NodesRoundHistoryCache>();

var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

builder.Services.AddDbContext<VersionContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<UserContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<DkgContext>(options => {
    options.UseNpgsql(connectionString);
    options.EnableSensitiveDataLogging();
});

builder.Services.AddSingleton<Runner>();

builder.Services.AddSingleton<NrhAddProcessor>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<NrhAddProcessor>>();
    return new NrhAddProcessor(connectionString, logger);
});

var app = builder.Build();

// -------------- Initialize database and caches ---------------
// runs syncronously with no locks against parallel execution 
var initializer = new Initializer(
        app.Services.GetRequiredService<NodesCache>(),
        app.Services.GetRequiredService<RoundsCache>(),
        app.Services.GetRequiredService<NodesRoundHistoryCache>(),
        app.Services.GetRequiredService<ILogger<Initializer>>()
        );
initializer.Initialize(connectionString);

app.Services.GetRequiredService<NrhAddProcessor>().Start();

// -------------------------------------------------------------

app.UseMiddleware<RequestLimitingMiddleware>(controllers);

app.UseCors(x => x
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<JwtMiddleware>();

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
