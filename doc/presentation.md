# .NET Aspire — Live Demo Run Sheet

A presenter's outline for the talk. The live demo is driven by Git tags: you start on
`aspire-0-no-aspire` (the manual, painful "before") and walk forward one tag at a time, adding
Aspire **service by service**. Each tag is a checkpoint you can `git checkout` or, better, `git diff`
against the previous one so the audience sees *exactly* what changed.

> **Core message to keep front-of-mind the whole time:** the app is *over-engineered in
> architecture* (gateway + 2 APIs + SQL + Redis + SPA) but *trivial in function* (a todo list and a
> fake forecast). The heavy topology exists only to manufacture real distributed-app pain — and
> Aspire is what relieves it.

---

## Tag map (the demo spine)

| Tag                      | Commit                                        | What it adds                                                 | Aspire capability spotlighted                  |
|--------------------------|-----------------------------------------------|--------------------------------------------------------------|------------------------------------------------|
| `aspire-0-no-aspire`     | Demo WEB                                      | Full Phase 0: 4 projects wired the manual way                | — (this is the pain)                           |
| `aspire-1-weather`       | Use Aspire for Weather API                    | `ServiceDefaults` + `AppHost`; Weather + Redis under Aspire  | Orchestration, integrations (Redis), telemetry |
| `aspire-2-gateway`       | Add Gateway to Aspire                         | Gateway joins; YARP destination becomes `http://weather-api` | Service discovery                              |
| `aspire-3-gateway-local` | Allow run Weather with Gateway without Aspire | Standalone fallback config for the gateway                   | Service discovery is just config (no lock-in)  |
| `aspire-4-todo`          | Add Todo API to Aspire                        | Todo + SQL Server container + migrate-on-start               | Integrations (SQL), `WaitFor` ordering         |
| `aspire-5-frontend`      | Add Demo WEB to Aspire                        | Angular SPA orchestrated; `PORT` + `GATEWAY_URL` injected    | One F5 for the whole system (incl. the SPA)    |

**Recurring sub-theme across tags 1–5:** every service keeps a *standalone fallback*
(`appsettings.Development.json`, `proxy.conf.js`, `aspire-serve.js`). Aspire-injected env vars
override at runtime, but each project still `dotnet run`s / `npm start`s on its own. Aspire is
**additive, not a cage** — call this out; it disarms the "lock-in" objection.

---

## 0. Intro (slides, ~3–4 min)

- **Hook:** "Five processes, three hardcoded config files, one README full of setup steps — to run a
  todo app. Let's fix that without rewriting a single feature."
- One slide on the topology diagram: Browser → Gateway → {Todo.Api → SQL, Weather.Api → Redis}.
- Name the four headline capabilities you'll prove, in order:
  1. **Orchestration / one F5** — stop starting things by hand.
  2. **Service discovery & config injection** — stop hardcoding ports and connection strings.
  3. **Dashboard & telemetry** — OpenTelemetry traces/logs/metrics for free.
  4. **Integrations** — SQL Server + Redis as managed containers.
- Set the format expectation: "This is a before/after, performed live, from Git tags."

---

## 1. Phase 0 — the "before" (`git checkout aspire-0-no-aspire`, ~6–8 min)

**Goal: make the pain visceral before you offer the cure. Do not rush this.**

Beats:

1. **Show it working.** Browse the SPA: add a todo, query a city, point at the **"cached" badge**
   flipping on the Weather page (~1.5 s on miss, instant on hit, expires after ~15 s). It works —
   that's important. The pain isn't bugs; it's *operations*.
2. **Reveal the cost.** Pull back the curtain on what it took to get here:
   - SQL Server + Redis containers started by hand (Podman Desktop on this machine).
   - `dotnet ef database update` had to run **before** Todo.Api could serve.
   - Several terminals, started in the right order: SQL → migrate → Todo.Api; Redis → Weather.Api;
     then Gateway; then `ng serve`.
3. **Show the brittleness — open the three config files:**
   - `src/Todo.Api/appsettings.Development.json` — hardcoded SQL connection string.
   - `src/Weather.Api/appsettings.Development.json` — hardcoded `localhost:6379` Redis.
   - `src/Gateway.Api/appsettings.json` — YARP destinations hardcoded to `https://localhost:7001/7002`.
   - `src/Demo.Web/proxy.conf.json` — gateway URL hardcoded to `https://localhost:7000`.
   - Line to land: *"Change any backend port and you go edit the gateway. There's no shared place
     that knows the topology."*
4. **The pain slide** (say it out loud, then move on):
   - 5+ processes, manual start order · containers are your problem · connection strings/URLs copied
     across files · no unified observability (logs scattered across terminals, no cross-service
     trace) · nothing waits for SQL/Redis to be ready · onboarding = a page of README.

Transition: **"Let's add Aspire — live."**

---

## 2. `aspire-1-weather` — the first F5 (~8–10 min)

> `git diff aspire-0-no-aspire aspire-1-weather` — this is the biggest single step; budget time.

**What changed (walk the diff):**

- **Two new projects appear** — this *is* the transformation, so dwell on them:
  - `src/Demo.ServiceDefaults/Extensions.cs` — one `AddServiceDefaults()` extension that every
    service will call. It wires **OpenTelemetry (traces/metrics/logs) with OTLP export**, default
    **health endpoints** (`/health`, `/alive`), and **HttpClient resilience + service discovery**.
    Show it once here; you won't re-explain it on later tags.
  - `src/Demo.AppHost/AppHost.cs` — the orchestrator, ~7 lines:

    ```csharp
    var cache = builder.AddRedis("cache");
    builder.AddProject<Projects.Weather_Api>("weather-api")
        .WithReference(cache)
        .WaitFor(cache);
    ```

    Point out: **Redis is now infrastructure-as-code.** No `docker run`, no remembered port.
- **Weather.Api edits** (`src/Weather.Api/Program.cs`): the manual `ConnectionMultiplexer.Connect(...)`
  registration is **deleted** and replaced by `builder.AddRedisClient("cache")`; add
  `AddServiceDefaults()` and `MapDefaultEndpoints()`. Emphasize: net change is mostly *deletion*.

**Do it live:** set AppHost as startup, **F5.**

- The **Aspire dashboard** opens. This is the money moment — tour it:
  - The `cache` (Redis) container and `weather-api` show up with status and endpoints.
  - **Logs** tab: structured logs from the service, in one place.
  - **Traces** tab: hit `/api/weather/Bratislava` (via the weather-api endpoint from the dashboard),
    query the same city again → show the trace and the **cached badge / `cached:true`** on the
    second call. Telemetry you wrote **zero** code for.

Capability proven: **orchestration (managed Redis container) + telemetry for free.**

---

## 3. `aspire-2-gateway` — service discovery (~4–5 min)

> `git diff aspire-1-weather aspire-2-gateway`

**What changed:**

- `AppHost.cs`: the Gateway is now a project Aspire runs, and it `WithReference(weatherApi)`.
- `src/Gateway.Api/Program.cs`: `AddServiceDefaults()` + on the YARP pipeline,
  `.AddServiceDiscoveryDestinationResolver()`.
- `src/Gateway.Api/appsettings.json` — **the headline line of this tag:**

  ```diff
  - "Address": "https://localhost:7002"
  + "Address": "http://weather-api"
  ```

**The point to make:** the gateway no longer knows a port. It names the service it wants
(`http://weather-api`) and Aspire's service discovery resolves the real scheme/host/port at runtime.
*"This is the hardcoded-port pain from Phase 0, gone."*

**Do it live:** F5, browse through the gateway to the Weather page — still works, now with **no port
in the gateway config.**

Capability proven: **service discovery & config injection.**

---

## 4. `aspire-3-gateway-local` — no lock-in (~2–3 min, optional/fast)

> `git diff aspire-2-gateway aspire-3-gateway-local` — a one-file change; keep it short.

**What changed:** `src/Gateway.Api/appsettings.Development.json` gains a `Services:weather-api`
section mapping the logical name to `localhost:5002/7002`.

**Why it earns a tag:** it answers the obvious objection — *"so now I can only run this under
Aspire?"* No. When the AppHost runs, Aspire injects `services__weather-api__*` env vars that win.
When you run the gateway **standalone**, this config makes `http://weather-api` resolve to a
manually-started Weather.Api. **Same discovery mechanism, two sources of truth — Aspire is additive.**

This is the moment to generalize the **standalone-fallback theme** for the rest of the demo: every
service keeps working on its own; Aspire just supplies better values when it's in charge.

Capability proven: **service discovery is plain config — adopt incrementally, no cage.**

---

## 5. `aspire-4-todo` — the SQL integration & ordering (~5–6 min)

> `git diff aspire-3-gateway-local aspire-4-todo`

**What changed:**

- `AppHost.cs`: add SQL as managed infrastructure and wire Todo + the gateway:

  ```csharp
  var sql    = builder.AddSqlServer("sql");
  var todoDb = sql.AddDatabase("tododb");
  var todoApi = builder.AddProject<Projects.Todo_Api>("todo-api")
      .WithReference(todoDb)
      .WaitFor(todoDb);
  // gateway now references todoApi too
  ```

- `src/Todo.Api/Program.cs`: delete the manual `AddDbContext(... UseSqlServer(connstring))`; replace
  with `builder.AddSqlServerDbContext<TodoDbContext>("tododb")`. Add `AddServiceDefaults()`,
  `MapDefaultEndpoints()`, and **migrate-on-startup**:

  ```csharp
  await db.Database.MigrateAsync();
  ```

**The teaching beats:**

- **SQL Server container, fully managed** — the slowest dependency to start, and you never touch it.
- **`WaitFor(todoDb)`** is why migrate-on-startup is safe: Aspire gates Todo.Api until SQL is
  actually ready. This is the *"strict start-up ordering"* pain from Phase 0, now declarative.
- Recall the fallback theme: standalone, Todo.Api still uses the `tododb` entry in
  `appsettings.Development.json`.

**Do it live:** F5. In the dashboard, watch `sql` come up, Todo.Api **wait**, then migrate, then
serve. Add a todo in the SPA. Open **Traces** → show the full distributed trace
**Web → Gateway → Todo.Api → SQL** as a single correlated request.

Capability proven: **integrations (SQL) + `WaitFor` ordering + end-to-end distributed trace.**

---

## 6. `aspire-5-frontend` — one F5 for the *whole* system (~4–5 min)

> `git diff aspire-4-todo aspire-5-frontend`

**What changed:**

- `AppHost.cs`: the Angular SPA is orchestrated too:

  ```csharp
  builder.AddJavaScriptApp("web", "../Demo.Web", "start")
      .WithReference(gateway)
      .WithEnvironment("GATEWAY_URL", gateway.GetEndpoint("https"))
      .WithHttpEndpoint(env: "PORT")
      .WithExternalHttpEndpoints()
      .WaitFor(gateway);
  ```

- `src/Demo.Web/proxy.conf.json` → **`proxy.conf.js`**: reads `process.env.GATEWAY_URL`, falls back
  to `https://localhost:7000` standalone. (The hardcoded gateway URL is gone.)
- `src/Demo.Web/aspire-serve.js` (new) + `package.json` `start` → `node aspire-serve.js`: binds
  `ng serve` to Aspire's injected **`PORT`**, falls back to 4200 standalone.

**Windows gotcha to name out loud:** Aspire injects `PORT`; the npm `start` script must honor it
cross-platform. The tiny `aspire-serve.js` wrapper (`spawn('ng', [...], { shell: true })`) is what
makes `ng serve --port <PORT>` work on Windows — this is the one frontend papercut worth flagging.

**Do it live — the finale F5:** SQL + Redis containers, all three APIs, **and** the Angular dev
server come up together from a single F5. Open the SPA from the dashboard's external endpoint.

Capability proven: **the complete one-button run — backend, infra, and frontend, orchestrated and
observable.**

---

## 7. The payoff / close (~3 min)

Put the Phase 0 pain slide back up next to what they just watched:

| Phase 0 (manual)                        | With Aspire                                          |
|-----------------------------------------|------------------------------------------------------|
| 5+ processes, manual start order        | one F5                                               |
| `docker/podman run` SQL + Redis by hand | containers managed as code                           |
| connection strings & ports in 4 files   | named references, injected; **no ports in code**     |
| migrate manually, before startup        | `WaitFor` + migrate-on-start, ordered automatically  |
| logs scattered across terminals         | one dashboard: logs, **distributed traces**, metrics |
| onboarding = README page                | clone, F5                                            |

Closing lines:

- *"We didn't change one feature. We deleted configuration and added two projects."*
- *"And every service still runs standalone — Aspire is additive."*
- Mention versioning honestly: Aspire ships GA updates often; APIs here
  (`AddServiceDefaults`, `AddRedis`/`AddRedisClient`, `AddSqlServer`/`AddSqlServerDbContext`,
  `AddJavaScriptApp`, `WithReference`/`WaitFor`) are pinned to the installed workload.

---

## Presenter checklist (before you walk on stage)

- [ ] **Podman Desktop running** (container runtime for SQL + Redis). Pre-pull the SQL Server and
      Redis images — first pull is slow and the SQL container is the slowest to start.
- [ ] `git status` clean; you're on `aspire-0-no-aspire`. Rehearse `git checkout <tag>` / `git diff`.
- [ ] Aspire workload/templates installed; solution builds on each tag (`dotnet build` per tag).
- [ ] `npm install` already done in `src/Demo.Web` (don't restore packages on stage).
- [ ] Dashboard opens and you know where **Resources / Logs / Traces / Metrics** are.
- [ ] Weather cache demo rehearsed: miss (~1.5 s) → hit (instant) → expiry (~15 s).
- [ ] Decide your diff tool (VS Code `git diff` view reads well on a projector).
- [ ] Fallback plan if a container is slow: keep talking through the dashboard while SQL warms up;
      `WaitFor` makes the wait *part of the story*, not a failure.
