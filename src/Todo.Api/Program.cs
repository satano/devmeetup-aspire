using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Todo.Api.Data;
using Todo.Api.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, service discovery, HttpClient resilience.
builder.AddServiceDefaults();

// Generates the OpenAPI document served at /openapi/v1.json (consumed by Scalar below).
builder.Services.AddOpenApi();

// Phase 1: Aspire registers the EF Core DbContext from the named "tododb" connection. The AppHost
// starts the SQL Server container and injects this connection string; running the API standalone
// still works via the "tododb" entry in appsettings.Development.json.
builder.AddSqlServerDbContext<TodoDbContext>("tododb");

WebApplication app = builder.Build();

// Aspire default health endpoints (/health, /alive).
app.MapDefaultEndpoints();

// Apply EF Core migrations on startup. Safe under Aspire because the AppHost gates this service
// with WaitFor(tododb); standalone it runs against the local SQL Server (no-op if already current).
using (IServiceScope scope = app.Services.CreateScope())
{
	TodoDbContext db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
	await db.Database.MigrateAsync();
}

// Interactive API explorer at /scalar (Development only). Open it in the browser to
// try the /todos endpoints without curl or Postman.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference(options => options.WithTitle("Todo API"));
}

RouteGroupBuilder todos = app.MapGroup("/api/todos");

todos.MapGet("/", async (TodoDbContext db) =>
	await db.Todos.OrderBy(t => t.Id).ToListAsync());

todos.MapGet("/{id:int}", async (int id, TodoDbContext db) =>
	await db.Todos.FindAsync(id) is { } item
		? Results.Ok(item)
		: Results.NotFound());

todos.MapPost("/", async (CreateTodoRequest request, TodoDbContext db) =>
{
	if (string.IsNullOrWhiteSpace(request.Title))
	{
		return Results.BadRequest("Title is required.");
	}

	TodoItem item = new()
	{
		Title = request.Title.Trim(),
		IsDone = false,
		CreatedAt = DateTime.UtcNow,
	};

	db.Todos.Add(item);
	await db.SaveChangesAsync();
	return Results.Created($"/api/todos/{item.Id}", item);
});

todos.MapPut("/{id:int}", async (int id, UpdateTodoRequest request, TodoDbContext db) =>
{
	TodoItem? item = await db.Todos.FindAsync(id);
	if (item is null)
	{
		return Results.NotFound();
	}

	if (string.IsNullOrWhiteSpace(request.Title))
	{
		return Results.BadRequest("Title is required.");
	}

	item.Title = request.Title.Trim();
	item.IsDone = request.IsDone;
	await db.SaveChangesAsync();
	return Results.Ok(item);
});

todos.MapDelete("/{id:int}", async (int id, TodoDbContext db) =>
{
	TodoItem? item = await db.Todos.FindAsync(id);
	if (item is null)
	{
		return Results.NotFound();
	}

	db.Todos.Remove(item);
	await db.SaveChangesAsync();
	return Results.NoContent();
});

app.Run();

record CreateTodoRequest(string Title);
record UpdateTodoRequest(string Title, bool IsDone);
