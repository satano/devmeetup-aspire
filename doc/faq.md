# FAQ

## Running only a subset of services with Aspire (per-domain configurations)

**Q:** If I have many (micro)services all wired up with Aspire, it's often unnecessary to run all of
them — only a subset. For example, some services relate to invoicing, others to HR, and some core
services are always used. In addition to running everything, I'd like multiple Aspire configurations
so I can run only the invoicing-related services, or only the HR-related ones. How would I do this?

**A:**

The key thing to know is that **the AppHost is just a regular C# program**, so "which services run"
is something you compose in code. There's no magic, which means you have several clean options. Here
are the main patterns, best-first.

### 1. One AppHost, group the wiring, select via launch profiles (recommended)

Factor each domain's resources into its own method, then conditionally add them based on a config value:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

AddCoreServices(builder);                 // always on

var run = builder.Configuration["Profiles"]?.Split(',') ?? ["all"];
bool all = run.Contains("all");

if (all || run.Contains("invoicing")) AddInvoicingServices(builder);
if (all || run.Contains("hr"))        AddHrServices(builder);

builder.Build().Run();
```

Then give the **AppHost** multiple launch profiles that just set that value:

```jsonc
// Demo.AppHost/Properties/launchSettings.json
"profiles": {
  "All":       { "commandName": "Project", "environmentVariables": { "Profiles": "all" } },
  "Invoicing": { "commandName": "Project", "environmentVariables": { "Profiles": "core,invoicing" } },
  "HR":        { "commandName": "Project", "environmentVariables": { "Profiles": "core,hr" } }
}
```

Now in **Visual Studio** you pick `All` / `Invoicing` / `HR` from the run-profile dropdown and hit
F5 — one click runs just that subset. From the CLI it's `dotnet run --launch-profile Invoicing` (or
pass `--Profiles core,hr` as args). This is the most popular approach: **one source of truth for all
wiring**, subsets chosen by configuration. The `AddXxxServices` methods can live in separate files
(or a shared class library) so each domain stays cohesive as the count grows.

The trade-off: resources not in the active profile aren't in the model at all, so changing the subset
means restarting the AppHost (fine for most workflows).

### 2. `WithExplicitStart()` — everything defined, started on demand

If you'd rather have one running AppHost and spin groups up/down **without restarting**, mark the
optional resources so Aspire adds them but doesn't auto-start them:

```csharp
AddCoreServices(builder);   // auto-start
foreach (var r in invoicingResources) r.WithExplicitStart();   // idle until you start them
```

Then start an idle resource (and its dependencies) from the **dashboard's Start button**. Good for
"core always up, occasionally bring up invoicing for an hour." Downside: the dashboard lists
everything (more visual clutter), and it's manual clicking rather than a named config.

You can **combine 1 and 2**: launch profiles choose what's *added*, `WithExplicitStart` controls what
*auto-runs* within that.

### 3. Multiple AppHost projects sharing a composition library

If you prefer explicit, separate entry points over a config switch: put all the `AddXxxServices`
extension methods in a shared class library, then create thin AppHost projects — `AppHost.All`,
`AppHost.Invoicing`, `AppHost.Hr` — each calling the relevant subset. You pick which to run via the
startup-project dropdown. More projects, but each is a self-documenting, dependency-free entry point.
The shared library keeps wiring DRY.

### The one real gotcha: cross-group dependencies + service discovery

Aspire wires references (`WithReference`, `WaitFor`) and service discovery between resources. If an
invoicing service calls a core service via `http://core-api`, **that core service has to be in the
running subset** or the call fails / the resource never becomes ready. So:

- Keep truly-shared things in `AddCoreServices` (always on).
- If invoicing legitimately depends on something in HR, your `invoicing` profile needs to pull HR in
  too (your `if` logic can express "invoicing implies core+hr").
- A resource referenced but not started (e.g., a `WithExplicitStart` dependency) will block dependents
  until you start it — which is sometimes what you want, sometimes a trap.

### Minor nicety

For dashboard readability with many services, `WithParentRelationship(...)` lets you nest related
resources visually under a parent — orthogonal to *running* subsets, but helps the dashboard stay
legible once you have dozens.

**Recommendation:** start with **#1** (config-driven, one AppHost, launch profiles `All`/`Invoicing`/`HR`
with `core` always included). It's the least machinery, works identically in VS and CLI, and scales
cleanly as you add domains. Reach for `WithExplicitStart` only if you find yourself wanting to toggle
groups mid-session without a restart.

## Aspire and a non-standard health-check URL (e.g. `/check-health`)

**Q:** I already have a service with its health check, but it isn't at the standard `/health` URL —
it's at something different (say `/check-health`). Do I need to add another `/health` health check for
Aspire, or will it use the existing one?

**A:**

This trips people up because there are **two separate layers** that both get called "health checks."

### 1. The *check* (logic) is separate from the *endpoint* (URL)

In ASP.NET Core you register checks once with `AddHealthChecks().AddCheck(...)`, and you expose them
with `MapHealthChecks("/whatever")`. One registration can be mapped to any number of URLs. So
`/check-health` is just *where you routed* your checks — there's nothing special about the name
`/health`.

### 2. Aspire does not require, auto-discover, or silently poll `/health`

The AppHost doesn't guess a health path. Locally, a *project* resource shows **Running/Healthy** based
on its process starting and its endpoints responding — the AppHost is **not** scraping your app's
health endpoint unless you explicitly tell it to. (When the dashboard shows a project as healthy, that
is not Aspire calling your `/health`; and conversely, the `/health` endpoint that ServiceDefaults maps
is something *you* call, not something the AppHost polls by default.)

### 3. So: no, you do not need a second `/health` endpoint for Aspire

Keep your existing `/check-health`. The check logic stays registered once; the URL is just routing.

### 4. If you want Aspire to actually *use* your endpoint

To drive the dashboard's "Healthy" badge and gate dependents that `WaitFor(thisService)`, point the
AppHost at your existing URL — no new endpoint, no duplicate check logic:

```csharp
builder.AddProject<Projects.MyService>("my-service")
       .WithHttpHealthCheck("/check-health");   // reuses YOUR path; expects HTTP 200
```

### 5. `MapDefaultEndpoints()` is optional and just a convention

ServiceDefaults' `MapDefaultEndpoints()` would *add* `/health` (readiness — all checks) and `/alive`
(liveness — `live`-tagged checks), Development-only. If you don't want those extra URLs, simply don't
call it (or adapt it to map *your* path). The `/health` + `/alive` convention mainly earns its keep
**after deployment**, where Kubernetes / Azure Container Apps probes expect well-known liveness/
readiness URLs — not for Aspire to function locally.

**Bottom line:** one set of check logic, exposed at whatever URL you like; tell the AppHost which URL
to watch via `WithHttpHealthCheck("/check-health")` if you want it gating readiness. No `/health`
required.

## The `"cache"` name: AddRedis ↔ AddRedisClient ↔ ConnectionStrings

**Q:** In the AppHost there's `builder.AddRedis("cache")` and in the Weather API
`builder.AddRedisClient("cache")`. Must the key `"cache"` be the same? And is it also the key into the
`ConnectionStrings` section in configuration?

**A:** Yes to both — and the way they connect is the nice part of the design.

- **Must the two `"cache"` strings match?** Yes. It's a single **connection/resource name** that links
  the producer and the consumer. Rename one and you must rename the other.
- **Is it also the key into `ConnectionStrings`?** Yes. Aspire injects it as `ConnectionStrings__cache`,
  and `AddRedisClient("cache")` reads `ConnectionStrings:cache`.

### How the wiring flows

1. **AppHost — produce:** `builder.AddRedis("cache")` creates a Redis resource named `cache`. When the
   consuming project does `.WithReference(cache)`, Aspire injects an **environment variable** into that
   process: `ConnectionStrings__cache=<host:port,password=…>`. (The `__` is .NET's convention for nested
   config keys, so it maps to the configuration path `ConnectionStrings:cache`.)
2. **Service — consume:** `builder.AddRedisClient("cache")` does essentially
   `configuration.GetConnectionString("cache")` — i.e. it reads `ConnectionStrings:cache` — and registers
   the `IConnectionMultiplexer` from it.

So the literal `"cache"` is the contract on both ends: the resource name in the AppHost **and** the
`ConnectionStrings` key the client looks up.

### Why standalone still works (same key)

Run the service *without* the AppHost and there's no injected env var, so `AddRedisClient("cache")` falls
back to `ConnectionStrings:cache` from `appsettings.Development.json` — exactly the
`"cache": "localhost:6379"` entry. One named connection; Aspire supplies it at runtime, or appsettings
supplies it standalone. Under the AppHost the injected **environment variable wins** (env vars outrank
appsettings in the default config order), which is why it transparently uses Aspire's container.

### Two things worth knowing

- **The name does double duty** — it's also the resource label in the dashboard. Same pattern for other
  integrations: `AddSqlServer("tododb")` ↔ `AddSqlServerDbContext("tododb")`.
- **This `ConnectionStrings` mapping is specific to connection-string-type resources** (caches, databases,
  etc.). `WithReference` to *another project* instead injects **service-discovery** variables
  (`services__<name>__…`), not `ConnectionStrings` — different mechanism, same idea that "the name is the
  link."

You can verify it: in the dashboard, open **weather-api → Environment variables** and you'll find
`ConnectionStrings__cache` with the value Aspire injected.

## Keeping a persistent local SQL Server instead of Aspire's throwaway container

**Q:** I want to use my local SQL Server like at the beginning (before Aspire), because I want my
data to persist between container restarts — or even a whole machine restart. How do I do that?

**A:**

The thing to understand first: `builder.AddSqlServer("sql")` spins up a **fresh SQL Server
container** each run. By default that container (and its data) is disposable — an F5 or a reboot can
leave you with an empty database. There are two clean ways to get persistence; which one you pick
depends on whether you still want Aspire to manage SQL at all.

### Option 1 — Point Aspire at your existing local SQL Server (recommended here)

You already have a real instance installed (`PETKONB\SQLSERVER2022`) — it's exactly what the Todo
API falls back to standalone. So don't have Aspire start a container at all; reference the existing
connection string instead:

```csharp
// AppHost.cs — replace  builder.AddSqlServer("sql").AddDatabase("tododb")  with:
IResourceBuilder<IResourceWithConnectionString> todoDb = builder.AddConnectionString("tododb");

IResourceBuilder<ProjectResource> todoApi = builder.AddProject<Projects.Todo_Api>("todo-api")
    .WithReference(todoDb);   // note: no WaitFor — see below
```

Put the actual connection string in the **AppHost's** user secrets so it isn't committed:

```text
dotnet user-secrets --project src/Demo.AppHost set "ConnectionStrings:tododb" `
  "Server=PETKONB\SQLSERVER2022;Database=devmeetup-aspire-todo;Trusted_Connection=True;TrustServerCertificate=True"
```

This is the same **"the name is the link"** pattern as the `"cache"` answer above: the AppHost
provides `ConnectionStrings__tododb`, and `builder.AddSqlServerDbContext<TodoDbContext>("tododb")` in
Todo.Api consumes it unchanged. **Nothing in Todo.Api changes** — and because the data lives in your
installed SQL Server, it survives F5, container churn, and reboots automatically.

Two things to know:

- **Drop `WaitFor(todoDb)`.** A connection-string resource isn't a container Aspire starts, so
  there's nothing to wait for (your server is assumed already up). The migrate-on-startup code still
  runs and creates the schema on first launch.
- In the **dashboard**, `tododb` now appears as a connection-string *value*, not a container — that's
  expected.

Trade-off: you lose the "infrastructure managed as code / reproducible on any machine" story for SQL
(a teammate without that instance can't just F5). Great for your own day-to-day dev; for the *talk*
itself the throwaway container is usually the better narrative.

### Option 2 — Keep Aspire's SQL container, but make it persistent

If you'd rather Aspire keeps owning the container (so the demo story stays intact) and *also* keeps
your data, add a data volume and a persistent lifetime:

```csharp
IResourceBuilder<ParameterResource> sqlPassword = builder.AddParameter("sql-password", secret: true);

IResourceBuilder<SqlServerServerResource> sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithDataVolume()                               // data files live in a named volume
    .WithLifetime(ContainerLifetime.Persistent);    // don't tear the container down between F5s
IResourceBuilder<SqlServerDatabaseResource> todoDb = sql.AddDatabase("tododb");
```

What each line buys you:

- **`WithDataVolume()`** — SQL's data files go into a Podman/Docker **named volume** that outlives the
  container. Recreate the container or reboot the machine and the todos are still there. (Prefer
  `WithDataBindMount("./.sql-data")` if you'd rather the files sit in a folder you can see or back up.)
- **`WithLifetime(ContainerLifetime.Persistent)`** — Aspire leaves the container running between
  AppHost restarts instead of disposing it, so the next F5 reattaches to the same warm container (also
  faster: no cold SQL Server boot every run).

**The one real gotcha — pin the password.** A SQL Server data volume is tied to the SA password set
when it was first initialised. Aspire generates a random password and stores it in user secrets, so
on the *same* machine it stays stable. But reset user secrets, move machines, or share the repo and a
new random password won't match the existing volume — SQL then refuses to start. Declaring an explicit
`AddParameter("sql-password", secret: true)` makes the password stable and intentional:

```text
dotnet user-secrets --project src/Demo.AppHost set "Parameters:sql-password" "<your-strong-password>"
```

### Bottom line

- Already have SQL installed and want your real data → **Option 1** (`AddConnectionString`), zero code
  change in the API, drop `WaitFor`.
- Want Aspire to keep managing the container but not lose data → **Option 2**
  (`WithDataVolume` + `WithLifetime(Persistent)`), and pin the password.
- For the live talk, the default disposable container is usually what you want — clean, reproducible,
  nothing to tidy up afterwards. Reach for these only when persistence actually matters to you.
