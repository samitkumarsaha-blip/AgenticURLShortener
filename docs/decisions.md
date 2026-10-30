# Architecture Decision Records

## ADR-001: ASP.NET Core 8 controllers

**Decision:** Use ASP.NET Core 8 controller endpoints.

**Reasoning:** Controllers provide explicit request/response contracts, conventional validation and Problem Details integration, and a clear surface for Swagger and integration tests. The API remains easy to replace or extend without moving domain logic into HTTP classes.

## ADR-002: Layered architecture

**Decision:** Separate Domain, Application, Infrastructure, API, and Orchestration projects.

**Reasoning:** URL rules and workflow governance must remain testable without a database or web host. Infrastructure-specific code is isolated so the current relational provider can be replaced without changing the domain or use cases.

## ADR-003: Repository and Unit of Work

**Decision:** Application code uses repository contracts and an explicit `IUnitOfWork`.

**Reasoning:** Repositories stage aggregates and events; the Unit of Work owns the commit boundary. This makes create and redirect operations transaction-shaped and gives the future SQL Server implementation one place to introduce transaction isolation, retry policy, and connection behavior.

## ADR-004: Current database provider

**Decision:** Use EF Core with SQLite for the first runnable deployment and keep provider-specific configuration in Infrastructure.

**Reasoning:** SQLite is simple to run locally and supports relational constraints, indexes, and migrations. The generic repository, Unit of Work, DbContext model, and migration boundary provide a controlled path to SQL Server.

## ADR-005: Durable workflow snapshot

**Decision:** Persist the orchestration run as a JSON snapshot in a relational table.

**Reasoning:** The evaluator needs restart-safe status, artifact, approval, and audit inspection without requiring a complex event store. The `IWorkflowRunStore` contract allows normalization into task, approval, audit, and metric tables when cross-run reporting becomes a production requirement.

## ADR-006: Deterministic stage agents

**Decision:** Implement stage agents as deterministic in-process components first.

**Reasoning:** Deterministic agents make approval gates, failure injection, retries, and quality evidence reproducible. An LLM-backed executor can later implement the same `IWorkflowAgent` contract while the orchestrator continues to enforce policy and human control.

## ADR-007: Redirect and analytics behavior

**Decision:** Resolve the destination first, increment the aggregate count, and persist a click event as part of the redirect use case.

**Reasoning:** A redirect should not depend on a separate analytics read. The current implementation prioritizes durable local correctness. A production-scale implementation should move click recording to a resilient queue if analytics write latency threatens redirect latency.

## ADR-008: Security boundary

**Decision:** Enforce protocol, host/IP, alias, TTL, and input-size rules in the application/domain path, with in-process rate limiting at the API boundary.

**Reasoning:** These controls block common unsafe inputs before persistence. They are not a complete outbound network policy; production deployment must add DNS rebinding defenses, egress restrictions, authentication, abuse monitoring, and distributed rate limiting.
