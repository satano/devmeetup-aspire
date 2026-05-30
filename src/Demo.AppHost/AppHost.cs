IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Aspire manages the Redis container and injects its connection into services that reference it.
IResourceBuilder<RedisResource> cache = builder.AddRedis("cache");

// Phase 1 — incremental: the Weather API and the Gateway are orchestrated by Aspire. The Gateway
// reaches the Weather API by its service-discovery name (http://weather-api) instead of a hardcoded
// port — WithReference(weatherApi) injects the discovery info. Todo.Api and the Angular frontend
// stay on the manual Phase 0 path for now.
IResourceBuilder<ProjectResource> weatherApi = builder.AddProject<Projects.Weather_Api>("weather-api")
    .WithReference(cache)
    .WaitFor(cache);

builder.AddProject<Projects.Gateway_Api>("gateway")
    .WithReference(weatherApi)
    .WaitFor(weatherApi);

builder.Build().Run();
