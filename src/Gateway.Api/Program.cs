WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Phase 0: routes and backend destinations are hardcoded in appsettings.json — including every
// backend port. This brittleness (edit the gateway whenever a backend port changes) is exactly
// what Aspire service discovery removes in Phase 1, where destinations become http://todo-api /
// http://weather-api and no ports appear anywhere.
builder.Services.AddReverseProxy()
	.LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

WebApplication app = builder.Build();

app.MapReverseProxy();

app.Run();
