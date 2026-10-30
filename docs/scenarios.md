# Scenario Demonstrations

## Running from the report UI

Open `/report.html`, choose a scenario, and generate the run. Enable **Manual step mode** to execute one dependency-ready task at a time with **Run next step**. Architecture and Release Readiness still pause for a reviewer decision. The run ID can be used to load or refresh the report.

## Greenfield

Request: build a secure URL shortener with redirects, TTL, analytics, and operational controls.

The Requirements Agent produces normalized requirements and classification. The Architecture Agent produces an ADR, API contract, persistence model, and architecture validation, then pauses for approval. Implementation produces the migration and implementation artifacts. Security and performance reviews are independently eligible in parallel during automatic execution; manual mode advances one ready review at a time. QA waits for both, and Observability precedes the release approval. Evidence is available through artifacts, validations, task dependencies, approvals, and audit trail in the persisted run snapshot.

Start with `POST /api/orchestration/scenarios/greenfield/runs`.

## Brownfield

Request: add vanity aliases, TTL, and rate limiting to an existing URL service without breaking redirects.

The scenario uses the same graph but records the enhancement as a brownfield change. The Implementation Agent produces a checkpoint before change execution, the QA Agent records regression and collision evidence, and rollback or rate-limit failures safe-stop the run rather than silently changing behavior.

Start with `POST /api/orchestration/scenarios/brownfield/runs`.

## Ambiguous

Request: the business wants shorter links and better reporting, but TTL, access policy, and analytics detail are unspecified.

The Requirements Agent records the ambiguity as a risk and the run pauses at the architecture approval gate. A human supplies clarification through `POST /api/orchestration/runs/{id}/replan`. The plan version increments, tasks at or after the impacted stage are invalidated, unaffected tasks remain when the replan is downstream, and the revised contract is validated before implementation continues.

Start with `POST /api/orchestration/scenarios/ambiguous/runs`.

## Failure demonstrations

- Inject a failing workflow agent in the unit tests to observe three attempts followed by SafeStopped.
- Call rollback after implementation to restore the pre-implementation checkpoint.
- Replan with a QA or contract rationale to selectively invalidate downstream work.
- Inspect `GET /api/orchestration/runs/{id}` for status, tasks, approvals, checkpoints, retries, plan version, and audit lineage.
