using Scalar.AspNetCore;
using StackExchange.Redis;
using System.Text.Json;
using Weather.Api.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Generates the OpenAPI document served at /openapi/v1.json (consumed by Scalar below).
builder.Services.AddOpenApi();

// Phase 0: connect to a Redis instance the developer started manually (see
// appsettings.Development.json for the local connection). Keeping the connection fully external
// means an Azure Cache for Redis connection string can be supplied per environment without any
// code change. In Phase 1, Aspire replaces this registration with builder.AddRedisClient("cache").
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
	ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("cache")!));

WebApplication app = builder.Build();

// Interactive API explorer at /scalar (Development only).
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference(options => options.WithTitle("Weather API"));
}

// Short TTL so cache expiry is observable live on stage.
TimeSpan cacheTtl = TimeSpan.FromSeconds(15);
string[] summaries = ["Sunny", "Partly cloudy", "Cloudy", "Rainy", "Stormy", "Snowy", "Foggy"];

app.MapGet("/api/weather/{city}", async (string city, IConnectionMultiplexer redis) =>
{
	IDatabase db = redis.GetDatabase();
	string key = $"weather:{city.ToLowerInvariant()}";

	// Cache-aside: try Redis first.
	RedisValue cachedValue = await db.StringGetAsync(key);
	if (cachedValue.HasValue)
	{
		string json = cachedValue!;
		WeatherForecast hit = JsonSerializer.Deserialize<WeatherForecast>(json)!;
		return Results.Ok(hit with { Cached = true });
	}

	// Miss: simulate slow upstream work so the cache benefit is obvious on stage.
	await Task.Delay(TimeSpan.FromSeconds(2));

	WeatherForecast forecast = new(
		City: city,
		TempC: Random.Shared.Next(-5, 31),
		Summary: summaries[Random.Shared.Next(summaries.Length)],
		Cached: false);

	await db.StringSetAsync(key, JsonSerializer.Serialize(forecast), cacheTtl);
	return Results.Ok(forecast);
});

app.Run();
