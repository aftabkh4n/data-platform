using DataPlatform.Api;
using DataPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.Console());

// Database
builder.Services.AddDbContext<DataPlatformDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// Redis cache
builder.Services.AddStackExchangeRedisCache(opt =>
    opt.Configuration = builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataPlatformDbContext>();
    db.Database.Migrate();
    await SeedData.SeedAsync(db);  
}

app.MapOpenApi();
app.MapScalarApiReference();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();