# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

A demo application for a live conference talk about **.NET Aspire**. The app is fully scaffolded and
committed — the talk is a **before/after performed live by walking Git tags**, not by writing the app
from scratch on stage.

- **HEAD (`main`, tag `aspire-5-frontend`)** is the finished state: the whole topology orchestrated by
  Aspire. This is where day-to-day editing happens.
- **The talk runs the tags in order.** You start the demo on `aspire-0-no-aspire` (everything wired the
  manual way, no Aspire) and `git diff`/`git checkout` forward one tag at a time, adding Aspire **service
  by service**. Each tag is a checkpoint the presenter walks through live.

The solution file is **`Demo.slnx`** (XML solution format), not `Demo.sln`.

> **Planning docs note:** earlier sessions kept a `demo-plan.md` (and a presenter run-sheet
> `presentation.md`, plus `doc/faq.md`). These were never committed and have been removed from the
> working tree. The **source of truth is now the code + the tag history.** If you're asked to "work on
> the demo," it almost always means editing the app at HEAD or adjusting a tag — clarify which.

## The Git-tag demo spine (the most important operational fact)

| Tag | Adds | Aspire capability shown |
|-----|------|-------------------------|
| `aspire-0-no-aspire` | Phase 0: `Todo.Api`, `Weather.Api`, `Gateway.Api`, `Demo.Web` wired manually | — (this is the pain) |
| `aspire-1-weather` | `Demo.ServiceDefaults` + `Demo.AppHost`; Weather + Redis under Aspire | Orchestration, Redis integration, telemetry |
| `aspire-2-gateway` | Gateway joins Aspire; YARP destination → `http://weather-api` | Service discovery |
| `aspire-3-gateway-local` | Standalone-fallback config so the gateway runs without the AppHost | Discovery is just config (no lock-in) |
| `aspire-4-todo` | Todo + SQL Server container + migrate-on-start | SQL integration, `WaitFor` ordering |
| `aspire-5-frontend` | Angular SPA orchestrated; `PORT` + `GATEWAY_URL` injected | One F5 for the whole system |

**Tag integrity is sacred.** `aspire-0-no-aspire` must contain **no** `Demo.AppHost` /
`Demo.ServiceDefaults` and no Aspire references — its existence is the "before." Each later tag must add
only its slice. If you change the app at HEAD in a way that affects the narrative, the tags may need to
be re-cut; flag this rather than silently breaking the progression.

## The demo's core design constraint

The single idea that governs the design: the app is **over-engineered in architecture** (gateway + 2
APIs + SQL + Redis + SPA) but **trivial in function** (a todo list and a fake weather forecast). The
excess topology is deliberate — it manufactures real "distributed app" pain at `aspire-0` that Aspire
then visibly relieves across the later tags. When editing, preserve this tension: don't simplify the
topology (that kills the pain) and don't make the features richer (that distracts from the payoff).

## The standalone-fallback design (do not break it)

Every service keeps working **without** the AppHost — Aspire-injected values override at runtime, but
each project still `dotnet run`s / `npm start`s on its own:

- **Weather.Api / Todo.Api** — `AddRedisClient("cache")` / `AddSqlServerDbContext<TodoDbContext>("tododb")`
  fall back to the `ConnectionStrings` entries in `appsettings.Development.json` when no env var is injected.
- **Gateway.Api** — `appsettings.Development.json` has a `Services:weather-api` block so `http://weather-api`
  resolves to a manually-started Weather.Api when run standalone (this is what `aspire-3-gateway-local` adds).
- **Demo.Web** — `proxy.conf.js` reads `GATEWAY_URL` (falls back to `https://localhost:7000`); `aspire-serve.js`
  reads `PORT` (falls back to 4200).

This "Aspire is additive, not a cage" property is a talking point — preserve the fallbacks when editing.

## Stack

.NET 10 minimal APIs, EF Core 10 (SQL Server provider, **Todo.Api only**), YARP gateway
(`Yarp.ReverseProxy` + `.AddServiceDiscoveryDestinationResolver()`), Angular SPA (standalone components),
SQL Server + Redis. Aspire packages are pinned at **13.3.5** (`Aspire.Hosting.JavaScript`,
`Aspire.Hosting.Redis`, `Aspire.Hosting.SqlServer`; `Aspire.Microsoft.EntityFrameworkCore.SqlServer` in
Todo.Api). AppHost is authored in C# (`src/Demo.AppHost/AppHost.cs`).

## Hard constraints to respect when editing

- **Presentation machine is Windows 11; container runtime is Podman Desktop** (not Docker). Anything that
  runs on stage must work there. This is why `Demo.Web` uses the cross-platform `aspire-serve.js` wrapper
  to honor Aspire's injected `PORT` (the npm `start` script is `node aspire-serve.js`), and why the design
  leans toward HTTP-internal + service discovery to avoid dev-cert juggling.
- **Pin Aspire versions.** Aspire ships GA updates often. Confirm signatures against the installed 13.3.5
  packages: `AddSqlServer` / `AddRedis` / **`AddJavaScriptApp`** (not the older `AddNpmApp`) / `WithReference`
  / `WaitFor` / `AddServiceDefaults`.
- **The browser talks only to the gateway** (`/api/...`), and the Angular dev-server proxy makes the SPA
  same-origin — so there is **no CORS config anywhere**. Keep it that way.
- **Visible cache behavior is intentional:** `Weather.Api` uses an artificial **~2s** delay on cache miss
  (`Task.Delay(TimeSpan.FromSeconds(2))`) and a short **15s** Redis TTL so hits/misses/expiry are observable
  live. Don't "fix" these.
- **Migrate-on-startup** in `Todo.Api` (`db.Database.MigrateAsync()`) is safe because the AppHost gates it
  with `WaitFor(tododb)`. Keep that ordering guarantee if you touch the AppHost.
