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

// The Gateway reaches the backends by service-discovery name (http://weather-api, http://todo-api).
IResourceBuilder<ProjectResource> gateway = builder.AddProject<Projects.Gateway_Api>("gateway")
    .WithReference(weatherApi)
    .WithReference(todoApi)
    .WaitFor(weatherApi)
    .WaitFor(todoApi);

// The Angular SPA is orchestrated too. Aspire runs `npm run start`, injects the
// gateway URL (GATEWAY_URL, consumed by proxy.conf.js) and the port to bind to (PORT, consumed by
// aspire-serve.js), and exposes the dev server as an external endpoint you can open in the browser.
builder.AddJavaScriptApp("web", "../Demo.Web", "start")
    .WithReference(gateway)
    .WithEnvironment("GATEWAY_URL", gateway.GetEndpoint("https"))
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WaitFor(gateway);

builder.Build().Run();
