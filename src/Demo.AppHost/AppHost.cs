IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Aspire manages the Redis container and injects its connection into services that reference it.
IResourceBuilder<RedisResource> cache = builder.AddRedis("cache");

builder.AddProject<Projects.Weather_Api>("weather-api")
	.WithReference(cache)
	.WaitFor(cache);

builder.Build().Run();
