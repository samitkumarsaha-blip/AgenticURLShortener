# Agentic URL Shortener

A .NET 8 ASP.NET Core URL shortener with SQLite persistence and a governed SDLC orchestration engine. SQLite is the first EF Core provider; the application and domain layers depend on provider-neutral interfaces so SQL Server can be introduced later.

## Prerequisites

- macOS or Linux
- .NET SDK 8.x
- Optional: `dotnet-ef` 8.x for migration work

If the SDK was installed with the user-local installer:

```sh
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
```

## Run

```sh
dotnet restore
dotnet ef database update --project src/UrlShortener.Infrastructure --startup-project src/UrlShortener.Api
dotnet run --project src/UrlShortener.Api -- --urls http://127.0.0.1:5080
```

Swagger is available at `/swagger` in Development.

## URL API

Create a URL:

```sh
curl -X POST http://127.0.0.1:5080/api/urls \
  -H 'Content-Type: application/json' \
  -H 'X-Correlation-ID: demo-001' \
  -d '{"url":"https://example.com/docs","alias":"docs-v1","ttlMinutes":60}'
```

Redirect with `GET /api/urls/{code}`. Click events are persisted after a successful resolution. Analytics are available at `GET /api/urls/{code}/analytics`.

Operational endpoints:

- `GET /api/operations/health`
- `GET /api/operations/metrics`
- `X-Correlation-ID` is propagated on every response
- URL creation is limited to 20 requests per minute by the in-process fixed-window limiter

## Orchestration API

- `GET /api/orchestration/scenarios`
- `POST /api/orchestration/runs`
- `POST /api/orchestration/scenarios/{scenario}/runs`
- `GET /api/orchestration/runs/{id}`
- `POST /api/orchestration/runs/{id}/approvals`
- `POST /api/orchestration/runs/{id}/replan`
- `POST /api/orchestration/runs/{id}/rollback`

The engine executes a dependency graph with parallel review branches, approval gates, bounded retries, safe-stop, checkpoints, rollback, audit lineage, plan versions, durable run snapshots, and stage-specific artifacts. Agents produce normalized requirements, architecture records, implementation evidence, QA evidence, observability evidence, and release artifacts.

## Test and build

```sh
dotnet build UrlShortener.sln --no-restore -v:q
dotnet test UrlShortener.sln --no-build -v:q
```

The tests include URL security validation, orchestration failure/retry/rollback/replan behavior, database migrations, redirect analytics, correlation IDs, and HTTP orchestration approval flow. The current suite contains 10 unit tests and 2 integration tests.

## Project layout

- `src/UrlShortener.Domain`: URL and click domain rules
- `src/UrlShortener.Application`: use cases and provider-neutral contracts
- `src/UrlShortener.Infrastructure`: provider-neutral repositories, Unit of Work, workflow store, and EF Core database integration
- `src/UrlShortener.Infrastructure/Database/Migrations`: versioned EF Core schema migrations
- `src/UrlShortener.Orchestration`: workflow agents, DAG executor, governance, and scenarios
- `src/UrlShortener.Api`: HTTP controllers, middleware, rate limiting, health, and metrics
- `tests`: unit and HTTP integration tests
- `docs/api-reference.md`: endpoint contracts, payloads, status codes, and examples
- `docs/architecture.md`: runtime boundaries and data flow
- `docs/orchestration.md`: agent contract, DAG, gates, recovery, and persistence
- `docs/scenarios.md`: greenfield, brownfield, and ambiguous walkthroughs
- `docs/testing.md`: test strategy, quality gates, security evidence, and gaps
- `docs/operations-runbook.md`: startup, verification, recovery, and incident procedures
- `docs/decisions.md`: architecture decision records
- `docs/limitations.md`: uncovered capabilities, rationale, impact, and production follow-ups
- `docs/engineering-summary.md`: risks, trade-offs, validation, and implementation coverage

## Security notes

Only HTTP and HTTPS destinations are accepted. Localhost, loopback, private RFC1918, and link-local IP destinations are rejected. Vanity aliases use a strict ASCII allowlist. This is input-level SSRF protection; production deployments still need DNS rebinding protection, outbound network policy, authentication, distributed rate limiting, and abuse monitoring.

## SQL Server migration path

Replace the SQLite provider registration and Infrastructure migrations with `UseSqlServer` and SQL Server migrations. Domain and Application contracts do not reference EF Core or SQLite types. The orchestration run payload remains provider-neutral and can move to a normalized SQL schema when operational query requirements justify it.
