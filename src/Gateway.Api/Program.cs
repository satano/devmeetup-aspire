WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Aspire service defaults — importantly this turns on service discovery, which lets YARP resolve
// logical destination names like http://weather-api (see appsettings.json) to real endpoints.
builder.AddServiceDefaults();

// Routes/clusters are still loaded from appsettings.json. AddServiceDiscoveryDestinationResolver()
// makes YARP resolve a cluster's destination through service discovery, so the Weather destination
// can be the name "http://weather-api" instead of a hardcoded port.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

WebApplication app = builder.Build();

// Aspire default health endpoints (/health, /alive).
app.MapDefaultEndpoints();

app.MapReverseProxy();

app.Run();
