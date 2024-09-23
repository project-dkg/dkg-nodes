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
int bulkInsertLimit = configuration.GetValue<int?>("BulkInsertLimit") ?? 10000;
int queueReparseDelay = configuration.GetValue<int?>("QueueReparseDelay") ?? 1000;

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
builder.Services.AddSingleton<NodeCompositeContext>();

var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

builder.Services.AddDbContext<VersionContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<UserContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<NodeContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<RoundContext>(options => {
    options.UseNpgsql(connectionString);
    options.EnableSensitiveDataLogging();
});

builder.Services.AddSingleton<Runner>();

builder.Services.AddSingleton<NrhAddProcessor>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<NrhAddProcessor>>();
    return new NrhAddProcessor(connectionString, bulkInsertLimit, queueReparseDelay, logger);
});

builder.Services.AddSingleton<NodeAddProcessor>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<NodeAddProcessor>>();
    return new NodeAddProcessor(connectionString, bulkInsertLimit, queueReparseDelay, logger);
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

app.Services
    .GetRequiredService<NrhAddProcessor>()
    .Start(app.Services.GetRequiredService<NodeCompositeContext>());

app.Services
    .GetRequiredService<NodeAddProcessor>()
    .Start(app.Services.GetRequiredService<NodeCompositeContext>());

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
