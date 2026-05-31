# Aspire Demo — Implementation Plan

> A presentation demo that starts **without** Aspire and has Aspire **added live on stage**.
> The application is intentionally **over-engineered in its architecture** (many services + infrastructure)
> but **simple in what it does** (a todo list and a fake weather forecast). The over-engineering exists
> to create real "distributed app" pain in Phase 0 that Aspire then relieves in Phase 1.

---

## 1. Goals & narrative arc

The talk demonstrates **all four** of Aspire's headline capabilities:

1. **Orchestration & one-button run** — replace the manual "start 5 things in the right order" ritual with a single F5.
2. **Service discovery & configuration** — replace hardcoded connection strings, ports, and URLs with references injected by Aspire.
3. **Dashboard & telemetry** — get OpenTelemetry traces, structured logs, and metrics for free; show one browser click become a single distributed trace.
4. **Integrations (SQL Server / Redis)** — Aspire spins up SQL Server and Redis as containers, wires health checks, and adds client resilience.

**Arc:**

- **Phase 0 (Before Aspire):** Build the whole topology the painful, manual way. End the phase by enumerating the pain.
- **Phase 1 (After Aspire):** Add two projects (`ServiceDefaults`, `AppHost`), make small edits to each service, press F5. Everything comes up wired together and observable.

---

## 2. Tech stack

| Concern          | Choice                                           |
|------------------|--------------------------------------------------|
| Backend          | .NET 10 (LTS), minimal APIs                      |
| ORM              | EF Core 10 (SQL Server provider) — Todo.Api only |
| Gateway          | YARP (`Yarp.ReverseProxy`)                       |
| Frontend         | Angular (standalone components, latest)          |
| API explorer     | Scalar at `/scalar` (Development) — Todo & Weather |
| Datastore        | Microsoft SQL Server (Todo)                      |
| Cache            | Redis (Weather)                                  |
| Orchestration    | Aspire — **added in Phase 1**                    |
| AppHost language | C# (idiomatic for a .NET audience)               |

> **Version note:** Aspire ships GA updates frequently. Pin to the latest stable Aspire workload/templates
> and matching integration package versions **at scaffold time**. Install via the Aspire CLI / templates.
> The API surface used here (`AddSqlServer`, `AddRedis`, `AddProject`, `AddNpmApp`, `WithReference`,
> `AddServiceDefaults`) has been stable across recent releases.

---

## 3. Repository / solution structure

```txt
/ (repo root)
├─ Demo.sln
├─ src/
│  ├─ Todo.Api/              # .NET 10 minimal API + EF Core → SQL Server
│  ├─ Weather.Api/           # .NET 10 minimal API + Redis cache-aside
│  ├─ Gateway.Api/           # .NET 10 YARP reverse proxy (single browser ingress)
│  └─ Demo.Web/              # Angular SPA (Todos + Weather pages)
│
│  # ── Added in Phase 1 only ──
│  ├─ Demo.ServiceDefaults/  # Shared OTel + health + resilience + service discovery
│  └─ Demo.AppHost/          # Orchestrator (the "one F5")
└─ demo-plan.md
```

`Demo.ServiceDefaults` and `Demo.AppHost` **do not exist in Phase 0** — adding them is the on-stage transformation.

**Default ports used in Phase 0** (hardcoded everywhere — that's the point):

| Component           | URL                      |
|---------------------|--------------------------|
| Gateway.Api         | `https://localhost:7000` |
| Todo.Api            | `https://localhost:7001` |
| Weather.Api         | `https://localhost:7002` |
| Demo.Web (ng serve) | `http://localhost:4200`  |
| SQL Server          | `localhost,1433`         |
| Redis               | `localhost:6379`         |

---

## 4. Phase 0 — No Aspire (the "before")

Build all four services + infrastructure manually. Each service is functionally tiny but independently configured.

### 4.1 Todo.Api (SQL Server)

**Purpose:** CRUD for a simple todo list, backed by SQL Server via EF Core.

**Entity:**

```csharp
public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Endpoints (minimal API):**

| Method | Route             | Behavior                                  |
|--------|-------------------|-------------------------------------------|
| GET    | `/api/todos`      | List all todos                            |
| GET    | `/api/todos/{id}` | Get one (404 if missing)                  |
| POST   | `/api/todos`      | Create `{ title }` → returns created item |
| PUT    | `/api/todos/{id}` | Update title / IsDone                     |
| DELETE | `/api/todos/{id}` | Delete (404 if missing)                   |

**Data access:** `TodoDbContext` (EF Core, SQL Server provider). Connection string (named `TodoDb`) hardcoded in `appsettings.Development.json`; local dev uses Windows auth to `PETKONB\SQLSERVER2022`.

**Manual steps required in Phase 0:**

1. Start a SQL Server instance (local install or `docker run` an MSSQL container) and remember its port/SA password.
2. Put the connection string into `appsettings.Development.json` by hand.
3. Run `dotnet ef database update` to create the schema **before** the API can serve requests.
4. Start the API (`dotnet run`) and confirm it can reach SQL.

> Pain seeded: container lifecycle is the developer's problem, the connection string is copy-pasted, and there's an ordering dependency (DB must exist first).

### 4.2 Weather.Api (Redis cache-aside)

**Purpose:** Return a (fake) forecast for a city, demonstrating caching. **No database** — deliberately different from Todo.Api so the two services show two distinct Aspire integrations.

**Endpoint:**

| Method | Route                 | Behavior                     |
|--------|-----------------------|------------------------------|
| GET    | `/api/weather/{city}` | Return forecast for the city |

**Response shape:**

```json
{ "city": "Bratislava", "tempC": 17, "summary": "Partly cloudy", "cached": true }
```

**Cache-aside logic:**

1. Look up key `weather:{city}` in Redis.
2. **Hit** → deserialize, set `cached: true`, return immediately.
3. **Miss** → simulate slow work with an artificial delay (~1.5 s), generate a forecast, store in Redis with a **short TTL (~15 s)** so expiry is visible during the talk, set `cached: false`, return.

> The visible `cached` flag + artificial delay make Redis hits/misses obvious on stage. The short TTL lets you re-query the same city live and watch it flip cached → not cached → cached.

**Manual steps required in Phase 0:**

1. Start Redis manually (local or `docker run redis`).
2. Hardcode the Redis connection (`localhost:6379`) in config.
3. Start the API and hope Redis is already up.

### 4.3 Gateway.Api (YARP)

**Purpose:** Single ingress. The browser talks **only** to the gateway; the gateway forwards to the backend APIs. Almost no code — the wiring lives in config, which makes the wiring the star of the service-discovery beat.

**Setup (`Program.cs`):**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
var app = builder.Build();
app.MapReverseProxy();
app.Run();
```

**Routing (`appsettings.json`) — Phase 0 (hardcoded destinations):**

```jsonc
{
  "ReverseProxy": {
    "Routes": {
      "todo":    { "ClusterId": "todo-cluster",    "Match": { "Path": "/api/todos/{**catch-all}" } },
      "weather": { "ClusterId": "weather-cluster", "Match": { "Path": "/api/weather/{**catch-all}" } }
    },
    "Clusters": {
      "todo-cluster":    { "Destinations": { "todo-api":    { "Address": "https://localhost:7001" } } },
      "weather-cluster": { "Destinations": { "weather-api": { "Address": "https://localhost:7002" } } }
    }
  }
}
```

*(No path transform needed — the backends own the `/api/...` paths, so the gateway forwards the path unchanged.)*

> Pain seeded: every backend URL/port is hardcoded here. If a backend port changes, you edit the gateway. This is exactly what service discovery removes in Phase 1.

### 4.4 Demo.Web (Angular)

**Purpose:** Frontend for both backends. Talks **only** to the gateway under `/api/...`.

**Pages / routes:**

- **/todos** — list todos, add (text box + button), toggle done (checkbox), delete. Calls `/api/todos`.
- **/weather** — text box for a city → shows forecast + a **"cached" badge** (highlights Redis behavior). Calls `/api/weather/{city}`.

**Backend access:** Angular dev-server proxy forwards `/api` → gateway, so the SPA is effectively same-origin → **no CORS configuration needed** anywhere, and the gateway URL is not baked into client code.

**Phase 0 `proxy.conf.json` (hardcoded gateway URL):**

```json
{ "/api": { "target": "https://localhost:7000", "secure": false, "changeOrigin": true } }
```

**Manual steps required in Phase 0:**

1. `npm install`.
2. Start the gateway + both APIs + SQL + Redis first.
3. `ng serve` and browse to `http://localhost:4200`.

### 4.5 Phase 0 pain summary (the slide before you add Aspire)

- **5+ processes** to start by hand, in the right order (SQL → migrate → Todo.Api; Redis → Weather.Api; then Gateway; then `ng serve`).
- **Containers are manual** — `docker run` for SQL and Redis, remembering ports and passwords.
- **Connection strings & URLs hardcoded** in three different config files.
- **No unified observability** — logs scattered across terminals; no cross-service trace.
- **No health/readiness story** — nothing waits for SQL/Redis to be ready.
- "Works on my machine" onboarding: a new dev needs a page of README steps.

---

## 5. Phase 1 — Add Aspire (the "after")

Done live. Two new projects + small edits per service. Then **F5**.

### 5.1 Demo.ServiceDefaults

Shared project referenced by all three APIs (and the gateway). One extension method wires the cross-cutting concerns:

```csharp
builder.AddServiceDefaults();
// gives every service:
//  - OpenTelemetry (traces, metrics, logs) with OTLP export to the dashboard
//  - default health endpoints (/health, /alive)
//  - HttpClient defaults: resilience handler + service discovery
app.MapDefaultEndpoints();
```

> This single call is the "telemetry for free" beat.

### 5.2 Demo.AppHost (orchestrator)

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure as code — Aspire manages the containers
var sql   = builder.AddSqlServer("sql").AddDatabase("tododb");
var cache = builder.AddRedis("cache");

// Backend services reference the infra; connection strings injected automatically
var todoApi = builder.AddProject<Projects.Todo_Api>("todo-api")
    .WithReference(sql)
    .WaitFor(sql);

var weatherApi = builder.AddProject<Projects.Weather_Api>("weather-api")
    .WithReference(cache)
    .WaitFor(cache);

// Gateway references the APIs → service discovery resolves YARP destinations
var gateway = builder.AddProject<Projects.Gateway_Api>("gateway")
    .WithReference(todoApi)
    .WithReference(weatherApi);

// Frontend orchestrated too: ng serve launched + gateway URL injected
builder.AddNpmApp("web", "../Demo.Web", "start")
    .WithReference(gateway)
    .WithEnvironment("GATEWAY_URL", gateway.GetEndpoint("https"))
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WaitFor(gateway);

builder.Build().Run();
```

### 5.3 Per-service edits in Phase 1

| Service                | Change                                                                                                                                                                                                                                                              |
|------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **All APIs + Gateway** | Reference `Demo.ServiceDefaults`; add `builder.AddServiceDefaults();` + `app.MapDefaultEndpoints();`                                                                                                                                                                |
| **Todo.Api**           | Replace hardcoded connection string with the **named** connection `"tododb"` injected by Aspire (`builder.AddSqlServerDbContext<TodoDbContext>("tododb")`). Run `db.Database.MigrateAsync()` on startup (safe because AppHost `WaitFor(sql)` guarantees SQL is up). |
| **Weather.Api**        | Replace hardcoded Redis config with the **named** `"cache"` connection (`builder.AddRedisClient("cache")`).                                                                                                                                                         |
| **Gateway.Api**        | Change YARP destinations from `https://localhost:700x` to `http://todo-api` / `http://weather-api`. Aspire service discovery resolves the scheme/host/port — **no ports anywhere**.                                                                                 |
| **Demo.Web**           | Swap `proxy.conf.json` for `proxy.conf.js` that reads `process.env.GATEWAY_URL`. Update npm `start` script to honor Aspire's injected `PORT` (see gotcha below).                                                                                                    |

### 5.4 The payoff (what you show after pressing F5)

1. **One F5** → Aspire starts the SQL and Redis containers, waits for them, runs migrations, starts all three APIs, then launches `ng serve`.
2. **Aspire dashboard** opens: every resource listed with status, endpoints, and live logs in one place.
3. Browse the SPA, add a todo → open the dashboard's **traces**: a single distributed trace **Web → Gateway → Todo.Api → SQL**.
4. Query a city twice on the Weather page → **cached badge flips**, and the trace shows the Redis call (fast) vs the slow miss.
5. **Metrics** tab shows request rates/latency per service — none of which you wrote code for.

---

## 6. On-stage flow (beat sequence)

1. Show the running app in Phase 0 — it works, but reveal the **5 terminals** behind it.
2. Show the three hardcoded config files (connection strings, ports, YARP destinations) — the brittleness.
3. Kill everything. "Let's add Aspire."
4. Add `Demo.ServiceDefaults` + `Demo.AppHost`; wire infra and projects (sections 5.1–5.2).
5. Make the per-service edits (section 5.3) — emphasize **deleting** hardcoded values.
6. **F5.** Containers + services + SPA come up together.
7. Tour the dashboard; trigger a todo + a weather query; show the distributed trace and the cache flip.
8. Close on the contrast slide: Phase 0 pain list (4.5) vs "one F5 + free observability."

---

## 7. Open items to pin at scaffold time

- **Aspire version:** install the latest stable Aspire CLI/templates and matching integration packages; confirm `AddNpmApp` / `AddSqlServer` / `AddRedis` signatures against the installed version.
- **Angular + injected PORT:** Aspire injects `PORT`; Angular's dev server must bind to it. Make the npm `start` script cross-platform (e.g. a tiny Node wrapper or a cross-platform env tool) so `ng serve --port <PORT>` works on Windows. (Presentation machine is Windows 11.)
- **HTTPS dev certs:** decide whether backends run HTTP or HTTPS under Aspire (HTTP internal + service discovery is simplest; avoids cert juggling between services).
- **Migrations strategy:** `MigrateAsync()` on startup is simplest for the demo. A dedicated migration/worker resource is the "more correct" pattern but adds stage complexity — out of scope per the agreed topology.
- **SQL image startup time:** the SQL Server container is the slowest to start; mention `WaitFor` handles ordering, and consider a data volume if you want todos to persist across runs.
