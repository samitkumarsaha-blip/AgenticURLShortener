# Engineering Summary

## Requirements and assumptions

The system converts URL-shortener requirements into a reviewable implementation and a governed SDLC run. It supports greenfield construction, brownfield enhancement, and ambiguous requirements requiring human clarification. The first deployment is an internal, unauthenticated, single-node service using SQLite; SQL Server is the intended durable replacement provider.

## Implemented artifacts

- ASP.NET Core 8 controller API
- URL validation for HTTP/HTTPS, local/private/link-local destinations, and safe ASCII aliases
- TTL-aware redirects and persisted click analytics
- EF Core SQLite schema and `InitialCreate` migration
- Provider-neutral Domain/Application contracts
- Orchestration agents for requirements, architecture, implementation, QA, observability, and release readiness, each producing typed artifacts, decisions, risks, and validations
- Explicit dependency graph with parallel security/performance branches
- Human approval gates for architecture and release
- Bounded retry, safe-stop, checkpoint, rollback, selective replan, and audit lineage
- Correlation IDs, structured logs, health, p50/p95/p99 metrics, and creation rate limiting
- Unit and HTTP integration tests: 10 unit tests and 2 integration tests currently pass

## Risks and trade-offs

In-process rate limiting and metrics do not coordinate across nodes. Workflow payload snapshots are durable but not optimized for cross-run analytics. Analytics is best effort after redirect resolution. DNS rebinding and outbound egress policy require deployment-level controls beyond string and IP input validation. Authentication, authorization, API keys, Redis, background queues, and distributed tracing are intentionally separate production follow-ups.

The complete coverage-gap register is documented in [limitations.md](limitations.md), including restart recovery, collision stress testing, cache fallback, distributed rate limiting, identity, SSRF deployment controls, MTTR, worker queues, LLM-backed agents, SQL Server validation, and broader security/load testing.

## Validation evidence

`dotnet build UrlShortener.sln --no-restore -v:q` completes with zero warnings and zero errors. `dotnet test UrlShortener.sln --no-build -v:q` covers URL security, workflow state transitions, retry exhaustion, safe-stop, rollback, replanning, URL creation, correlation propagation, redirects, analytics persistence, and HTTP orchestration approval flow.

## Evaluation coverage

- Requirement understanding: normalized contracts, scenario catalog, ambiguity and assumptions
- Architecture: layered projects, repository boundaries, EF provider isolation, documented data flow
- Agentic execution: stage agents, dependency DAG, parallel paths, synchronization, approvals, retries, rollback, safe-stop, replanning, persisted run snapshots
- Engineering quality: domain/application/infrastructure separation and explicit error contracts
- Validation: unit, integration, security, failure-path, and HTTP tests
- Security: protocol allowlist, SSRF-sensitive address rejection, alias restrictions, rate limiting, correlation IDs, Problem Details
- Observability: JSON logs, audit trail, health, latency percentiles, success rate, retry and rollback counters
- Ownership: setup instructions, scenario walkthroughs, trade-offs, limitations, and SQL Server migration path
