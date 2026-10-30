# Coverage Gaps and Rationale

This document records what is intentionally not covered by the current implementation. These are known boundaries, not hidden defects. The current system is a complete reviewable engineering submission for a single-node evaluation deployment, but it is not a fully distributed production service.

## 1. Restart-safe orchestration commands

**Current state:** Workflow snapshots are persisted, and a run can be loaded after restart. The current in-memory orchestrator fully owns active-run mutations; approval, replan, and rollback commands for a run that exists only in storage require hydration into the active execution context.

**Why not complete:** The first implementation focused on making the lifecycle and recovery behavior deterministic and observable. Full restart recovery requires reconstructing execution locks, agent registrations, cancellation state, and idempotency behavior, not only deserializing JSON.

**Production follow-up:** Add an active-run loader before every mutation, optimistic concurrency/version checks, distributed execution leases, idempotency keys, and a durable event/audit model.

## 2. High-concurrency collision stress testing

**Current state:** Short codes use random generation and the database key/unique alias index remains the final uniqueness authority. Unit and HTTP integration tests cover normal behavior, but the repository does not yet include a large parallel creation stress test.

**Why not complete:** A deterministic test requires a collision-injecting generator or a configurable code generator and a controlled SQLite concurrency harness. Random production behavior alone is not useful as repeatable collision evidence.

**Production follow-up:** Inject a code generator, run parallel create requests, force collisions, assert bounded retry behavior, and run the same suite against SQL Server.

## 3. Explicit short-code collision retry

**Current state:** Database uniqueness protects stored codes, but the application does not yet retry a failed random code insert with a new code in a bounded loop.

**Why not complete:** The first implementation established the persistence and Unit of Work boundaries before adding a retry-aware creation policy. Treating a uniqueness exception as a generic conflict is safer than returning duplicate data, but it is not the desired final behavior.

**Production follow-up:** Add an injected `ICodeGenerator`, a maximum collision-attempt setting, uniqueness-exception detection, and a `503`/safe-stop policy after repeated collision failures.

## 4. Cache adapter and cache outage fallback

**Current state:** Redirects read directly from the database. No Redis or in-memory cache adapter is enabled, so there is no cache outage path to test.

**Why not complete:** Cache is an optimization, not a correctness dependency. Adding Redis configuration, deployment, invalidation, TTL synchronization, and chaos tests would add operational surface before the durable correctness path was complete.

**Production follow-up:** Add `IShortUrlCache`, use cache-aside reads, preserve database fallback on timeout, invalidate on create/update/expiry, and test forced cache failures.

## 5. Distributed rate limiting

**Current state:** URL creation uses an in-process fixed-window limiter with 20 permits per minute. It is process-wide and does not use API keys or a shared store.

**Why not complete:** The current deployment is single-node and unauthenticated. A distributed policy requires an identity model and a shared Redis or gateway-based limiter.

**Production follow-up:** Add API-key or authenticated principal partitioning, shared counters, `Retry-After` calculation, and cross-node integration tests.

## 6. Authentication and authorization

**Current state:** The API is intentionally unauthenticated for internal evaluation. Anyone who can reach it can create URLs, read analytics, and submit orchestration approvals.

**Why not complete:** Identity, reviewer roles, tenant ownership, and authorization policy were not specified in the assignment scenario. Inventing an identity model would obscure the core orchestration demonstration.

**Production follow-up:** Add OAuth2/OIDC or API-key authentication, owner/tenant claims, reviewer roles, authorization policies, and audit identity fields.

## 7. Complete SSRF and outbound network protection

**Current state:** The application rejects unsafe schemes, localhost, loopback, RFC1918 private IPv4, and link-local addresses. It does not resolve DNS and re-check the destination at request time.

**Why not complete:** DNS rebinding defense is deployment-sensitive and depends on the outbound HTTP/network architecture. URL validation alone cannot establish a complete SSRF boundary.

**Production follow-up:** Resolve and validate addresses with a controlled resolver, block private ranges after resolution, enforce egress network policy, limit redirects, and isolate outbound fetch behavior. The current service does not fetch destination content; it only redirects the client.

## 8. Full orchestration metrics and MTTR

**Current state:** Request metrics expose total requests, success rate, and p50/p95/p99 latency. Run snapshots expose retry and rollback counts. A dedicated end-to-end orchestration latency and MTTR time series is not implemented.

**Why not complete:** The current metrics are process-local and designed for a runnable evaluation. MTTR requires defining incident start, recovery approval, and durable aggregation across runs.

**Production follow-up:** Persist stage timestamps and failure/recovery events, calculate orchestration latency and MTTR, export OpenTelemetry metrics, and retain historical time series.

## 9. External worker queue and multi-node execution

**Current state:** Workflow agents execute in-process and deterministically. There is no external queue, worker lease, or distributed scheduler.

**Why not complete:** The assignment requires controlled autonomy and governance; it does not require operating a distributed agent platform. In-process execution makes gates, retries, and failure injection reproducible.

**Production follow-up:** Add an execution queue, worker leases, durable task claims, cancellation, dead-letter handling, and distributed idempotency.

## 10. LLM-backed autonomous agents

**Current state:** Workflow agents are deterministic application components. They do not call an external LLM.

**Why not complete:** Deterministic agents provide repeatable acceptance evidence and prevent external model availability, cost, prompt drift, or data handling from affecting the core evaluation. The `IWorkflowAgent` contract is the extension point for an LLM adapter.

**Production follow-up:** Add model-backed adapters behind the same contract, structured output validation, prompt/version lineage, data redaction, token/cost budgets, model failure fallback, and mandatory human approval for high-impact outputs.

## 11. SQL Server execution environment

**Current state:** EF Core migrations and integration tests run against SQLite. Domain and Application code do not depend on SQLite types.

**Why not complete:** SQL Server was selected as the future replacement provider, while SQLite keeps the current submission easy to run without external infrastructure.

**Production follow-up:** Add `UseSqlServer`, SQL Server migrations, provider-specific integration tests, transaction/isolation verification, connection resiliency, and deployment secrets configuration.

## 12. Broader test scale and security tooling

**Current state:** The repository contains unit and HTTP integration coverage for core flows, security validation, and orchestration failure paths. It does not include load testing, dependency scanning, dynamic OWASP scanning, chaos infrastructure, or multi-node tests.

**Why not complete:** These require environment, traffic profiles, external services, and CI tooling beyond the local runnable assignment.

**Production follow-up:** Add CI quality gates, SAST/dependency scanning, DAST, load tests, concurrency stress, cache/database fault injection, and signed release artifacts.

## Summary

The uncovered items are concentrated in distributed operation, identity, external service integration, restart coordination, and production-scale validation. The current implementation deliberately completes the domain/API/orchestration flow first and documents these boundaries so future work can be planned without changing the core contracts.
