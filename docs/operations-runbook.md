# Operations Runbook

## Start the service

```sh
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
dotnet restore
dotnet ef database update --project src/UrlShortener.Infrastructure --startup-project src/UrlShortener.Api
dotnet run --project src/UrlShortener.Api -- --urls http://127.0.0.1:5080
```

The API applies pending EF migrations during startup. For production, migrations should be reviewed and applied by a deployment pipeline rather than by every application instance.

To create a future migration in the dedicated folder:

```sh
dotnet ef migrations add MigrationName \
  --project src/UrlShortener.Infrastructure \
  --startup-project src/UrlShortener.Api \
  --output-dir Database/Migrations
```

## Verify health

```sh
curl -i http://127.0.0.1:5080/api/operations/health
curl -i http://127.0.0.1:5080/api/operations/metrics
```

Confirm the response is healthy, `X-Correlation-ID` is present, and latency metrics are being updated.

## Verify URL lifecycle

```sh
curl -i -X POST http://127.0.0.1:5080/api/urls \
  -H 'Content-Type: application/json' \
  -H 'X-Correlation-ID: runbook-create-001' \
  -d '{"url":"https://example.com","ttlMinutes":30}'

curl -i http://127.0.0.1:5080/api/urls/{code}
curl -i http://127.0.0.1:5080/api/urls/{code}/analytics
```

Use a client that disables automatic redirect following when validating the `302` response.

## Start and inspect an orchestration run

```sh
curl -sS -X POST http://127.0.0.1:5080/api/orchestration/scenarios/greenfield/runs
curl -sS http://127.0.0.1:5080/api/orchestration/runs/{id}
```

Review `status`, `currentStage`, `tasks`, `artifacts`, `validations`, `approvals`, `retryCount`, `rollbackCount`, `planVersion`, and `auditTrail`.

For manual task-by-task execution, start a custom run with `stepMode: true`:

```sh
curl -sS -X POST http://127.0.0.1:5080/api/orchestration/runs \
  -H 'Content-Type: application/json' \
  -d '{"requirements":"Build a secure URL shortener","scenario":"greenfield","stepMode":true}'
```

The start response includes a run ID and an `AwaitingStep` status after the Requirements task. Advance exactly one dependency-ready task at a time:

```sh
curl -sS -X POST http://127.0.0.1:5080/api/orchestration/runs/{id}/steps
```

When a step reaches Architecture or Release Readiness, inspect the snapshot and submit the approval shown below. In manual mode, approval makes the next task available; call `/steps` again to execute it. The same workflow controls are available in the browser at `/report.html`.

Approve architecture:

```sh
curl -X POST http://127.0.0.1:5080/api/orchestration/runs/{id}/approvals \
  -H 'Content-Type: application/json' \
  -d '{"stage":"Architecture","approved":true,"reviewer":"on-call","rationale":"Reviewed contract and persistence boundary"}'
```

Approve release only after the final run snapshot has been reviewed:

```sh
curl -X POST http://127.0.0.1:5080/api/orchestration/runs/{id}/approvals \
  -H 'Content-Type: application/json' \
  -d '{"stage":"ReleaseReadiness","approved":true,"reviewer":"release-owner","rationale":"Validation evidence reviewed"}'
```

## Safe-stop recovery

When a run reaches `SafeStopped`:

1. Read `safeStopReason` and the failed task error.
2. Inspect the audit trail and retry count.
3. Decide whether the requirement or implementation should change.
4. Use `/replan` for a changed contract or `/rollback` for a recoverable implementation failure.
5. Re-run validation before requesting approval again.

Do not manually mark a run completed. The release gate must be approved through the API.

## Rollback

```sh
curl -X POST http://127.0.0.1:5080/api/orchestration/runs/{id}/rollback
```

Rollback is task-state recovery to the latest stored checkpoint. It does not undo already deployed external changes; deployment rollback must be handled by the release system.

## Incident signals

Investigate when:

- `failedRequests` rises or `successRate` falls
- p95/p99 latency grows unexpectedly
- repeated SafeStopped runs occur for the same task
- retry or rollback counts increase
- database migration or lock errors appear in structured logs
- correlation IDs are missing from client reports

Structured logs include method, path, status, duration, and correlation ID. Use the correlation ID to trace an individual API request through the log stream.

## Recovery limitations

The current metrics are process-local. Rate limiting is process-local and uses a fixed window of 20 URL-creation requests. A multi-node deployment requires a shared rate-limit store, centralized telemetry, externalized orchestration workers, and a coordinated migration process.
