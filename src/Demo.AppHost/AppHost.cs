IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

// Aspire manages the Redis container and injects its connection into services that reference it.
IResourceBuilder<RedisResource> cache = builder.AddRedis("cache");

// Aspire manages the SQL Server container and the "tododb" database, injecting the connection.
IResourceBuilder<SqlServerServerResource> sql = builder.AddSqlServer("sql");
IResourceBuilder<SqlServerDatabaseResource> todoDb = sql.AddDatabase("tododb");

IResourceBuilder<ProjectResource> weatherApi = builder.AddProject<Projects.Weather_Api>("weather-api")
    .WithReference(cache)
    .WaitFor(cache);

IResourceBuilder<ProjectResource> todoApi = builder.AddProject<Projects.Todo_Api>("todo-api")
    .WithReference(todoDb)
    .WaitFor(todoDb);

builder.AddProject<Projects.Gateway_Api>("gateway")
    .WithReference(weatherApi)
    .WithReference(todoApi)
    .WaitFor(weatherApi)
    .WaitFor(todoApi);

builder.Build().Run();
