# Testing and Quality Gates

## Test projects

### Unit tests

`tests/UrlShortener.UnitTests` covers deterministic domain and orchestration behavior without a web server:

- HTTP/HTTPS allowlist
- loopback, private, and link-local destination rejection
- alias character policy
- Architecture and ReleaseReadiness approval gates
- dependency graph progression
- parallel security/performance tasks
- three-attempt retry limit
- SafeStopped transition
- checkpoint creation and rollback
- plan version increment and selective replan
- agent-produced artifacts and validations

### Integration tests

`tests/UrlShortener.IntegrationTests` uses `WebApplicationFactory<Program>` with an isolated temporary database:

- URL creation and `201 Created`
- correlation ID propagation
- `302 Found` redirect and Location header
- persisted click analytics
- orchestration run creation through HTTP
- architecture approval through HTTP
- workflow task output in the response contract

## Quality commands

```sh
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

dotnet restore
dotnet build UrlShortener.sln --no-restore -v:q
dotnet test UrlShortener.sln --no-build -v:q
```

Expected current result: zero build warnings, zero build errors, 10 unit tests passed, and 2 integration tests passed.

## Security validation

The URL validation tests cover:

- `javascript:`, `file:`, and other unsupported schemes
- `localhost` and loopback addresses
- RFC1918 private IPv4 ranges
- IPv4 link-local metadata-style addresses
- malformed and non-absolute URLs
- aliases containing slash, control, or non-ASCII homograph characters

The implementation rejects these inputs before persistence. Production deployment must add DNS resolution controls and outbound egress policy because URL string validation alone cannot prevent DNS rebinding.

## Failure validation

The failure-path tests inject a failing workflow agent and verify that:

- the agent is attempted no more than three times
- retry count is recorded
- the task becomes failed
- the run becomes SafeStopped
- dependent tasks do not execute

Rollback and replan tests verify that the recovery action is visible in audit lineage and that unaffected work is retained where the impacted stage permits it.

## Quality gate interpretation

A run is not release-ready merely because code compiles. The release gate requires:

1. Requirements normalized and classified.
2. Architecture artifacts produced and approved by a human.
3. Implementation build validation completed.
4. Security, concurrency, and recovery validations recorded.
5. Correlation and operational health validated.
6. Release artifacts produced and final human approval recorded.

## Known test gaps

- Multi-process/distributed rate-limit coordination is not tested because the first deployment is single-node.
- Real Redis outage fallback is not tested because cache is not required by the current correctness path.
- SQL Server provider compatibility should be validated with a provider-specific integration environment before production deployment.
