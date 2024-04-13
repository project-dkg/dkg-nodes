using Microsoft.EntityFrameworkCore;

using dkgServiceNode.Data;
using dkgServiceNode.Services.Authorization;

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
builder.Services.AddDbContext<RoundContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDbContext<NodeContext>(options => options.UseNpgsql(connectionString));

builder.ConfigureWebHostDefaults(webBuilder =>
{
    webBuilder.UseStartup<Startup>()
    .UseKestrel(options =>
    {
        options.Configure(builder.Configuration.GetSection("Kestrel"));
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Cors policy for development
    app.UseCors(x => x
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()
    );

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<JwtMiddleware>();

// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
