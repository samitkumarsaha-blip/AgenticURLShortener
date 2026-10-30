# API Reference

Base URL: `http://127.0.0.1:5080`

All endpoints return JSON except redirects. Every response includes `X-Correlation-ID`. A caller may supply a correlation ID; otherwise the API generates one. Correlation IDs longer than 128 characters or blank values are replaced.

## URL lifecycle

### Create a short URL

`POST /api/urls`

Request:

```json
{
  "url": "https://example.com/docs",
  "alias": "docs-v1",
  "ttlMinutes": 60
}
```

Fields:

| Field | Type | Required | Rules |
|---|---|---:|---|
| `url` | string | yes | Absolute `http` or `https` URL. Local, loopback, private, and link-local destinations are rejected. |
| `alias` | string | no | ASCII letters, digits, `-`, `_`; maximum 64 characters. |
| `ttlMinutes` | integer | no | 1 through 525600 minutes. |

Successful response: `201 Created`

```json
{
  "code": "aB91xK2p",
  "url": "/aB91xK2p",
  "createdAt": "2026-09-16T12:00:00+00:00",
  "expiresAt": "2026-09-16T13:00:00+00:00",
  "clickCount": 0
}
```

The response `url` is relative to the service host. The generated code is cryptographically random and the database key is the final uniqueness authority.

Possible responses:

- `400`: invalid URL, unsupported scheme, invalid alias, or invalid TTL
- `409`: alias already exists
- `429`: creation rate limit exceeded; response includes `Retry-After: 60`
- `500`: unexpected persistence or application failure

### Resolve and redirect

`GET /api/urls/{code}`

A valid, non-expired URL returns `302 Found` with a `Location` header. The service increments the click count and persists a click event containing timestamp, referrer, user-agent, and client IP where available.

Expired and unknown codes return `404 Not Found`. Redirect correctness is decided from the URL record; analytics is an associated write in the same application use case.

### Analytics

`GET /api/urls/{code}/analytics`

Response: `200 OK`

```json
{
  "code": "aB91xK2p",
  "totalClicks": 2,
  "recentClicks": [
    {
      "occurredAt": "2026-09-16T12:02:00+00:00",
      "referrer": "https://search.example/",
      "userAgent": "ExampleBrowser/1.0"
    }
  ]
}
```

The service returns at most the 100 most recent events. Unknown codes return `404 Not Found`.

## Operations

### Health

`GET /api/operations/health`

```json
{
  "status": "healthy",
  "timestamp": "2026-09-16T12:00:00+00:00"
}
```

### Metrics

`GET /api/operations/metrics`

The in-process metrics snapshot includes:

- `totalRequests`
- `successfulRequests`
- `failedRequests`
- `successRate`
- `p50LatencyMs`
- `p95LatencyMs`
- `p99LatencyMs`

Metrics are process-local and reset when the process restarts.

## Orchestration

### List scenarios

`GET /api/orchestration/scenarios`

Returns the greenfield, brownfield, and ambiguous scenario definitions, including decomposition and validation steps.

### Start a custom run

`POST /api/orchestration/runs`

```json
{
  "requirements": "Build a secure URL shortener with analytics",
  "scenario": "greenfield"
}
```

Returns `202 Accepted` with the current run snapshot. A normal run pauses at the Architecture approval gate.

### Start a predefined scenario

`POST /api/orchestration/scenarios/{scenario}/runs`

Supported values: `greenfield`, `brownfield`, `ambiguous`.

### Read a run

`GET /api/orchestration/runs/{id}`

The snapshot includes status, current stage, plan version, task graph, artifacts, decisions, risks, validations, approvals, retry count, rollback count, checkpoints, safe-stop reason, and audit trail.

### Approve or reject a gate

`POST /api/orchestration/runs/{id}/approvals`

```json
{
  "stage": "Architecture",
  "approved": true,
  "reviewer": "reviewer@example.com",
  "rationale": "The provider boundary and API contract are approved."
}
```

Supported gated stages are `Architecture` and `ReleaseReadiness`. Rejection transitions the run to `Rejected`; approval allows the dependency graph to continue.

### Replan

`POST /api/orchestration/runs/{id}/replan`

```json
{
  "requirements": "Add an optional 24-hour TTL and protected analytics",
  "rationale": "The contract changed after architecture review."
}
```

The plan version increments. Tasks at or after the impacted stage become `Invalidated`; unaffected completed tasks remain available when the impact is downstream.

### Roll back

`POST /api/orchestration/runs/{id}/rollback`

Restores the latest checkpoint, records a rollback event, increments the rollback counter, and resumes eligible tasks. The current implementation creates a checkpoint before the Implementation task.

## Problem Details

Validation and application errors use RFC 7807-style responses:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Invalid URL request",
  "status": 400,
  "detail": "Only HTTP and HTTPS URLs are allowed.",
  "traceId": "correlation-id"
}
```
