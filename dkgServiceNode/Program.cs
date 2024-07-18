using Microsoft.EntityFrameworkCore;

using dkgServiceNode.Data;
using dkgServiceNode.Services.Authorization;
using dkgServiceNode.Services.RoundRunner;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddCors();

// configure DI for application services
builder.Services.AddScoped<IJwtUtils, JwtUtils>();
builder.Services.AddHttpContextAccessor();

var configuration = builder.Configuration;

// Configure Jwt secret
builder.Services.Configure<AppSecret>(configuration.GetSection("AppSecret"));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var connectionString = configuration.GetConnectionString("DefaultConnection");
DbEnsure.Ensure(connectionString ?? "");

builder.Services.AddDbContext<VersionContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<UserContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<DkgContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddSingleton<Runner>();

var app = builder.Build();

app.UseCors(x => x
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
);

// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//    app.UseSwagger();
//    app.UseSwaggerUI();
// }

app.UseMiddleware<JwtMiddleware>();

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
