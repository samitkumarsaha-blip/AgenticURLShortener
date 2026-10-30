namespace UrlShortener.Orchestration;

public sealed record ScenarioDefinition(
    string Name,
    string Classification,
    string Requirement,
    IReadOnlyList<string> Decomposition,
    IReadOnlyList<string> ValidationSteps);

public static class ScenarioCatalog
{
    public static IReadOnlyList<ScenarioDefinition> All { get; } =
    [
        new(
            "greenfield",
            "new system",
            "Build a secure URL shortener with redirects, TTL, analytics, and operational controls.",
            ["normalize requirements", "approve architecture", "implement API and persistence", "run QA and release checks"],
            ["URL security tests", "concurrent creation tests", "HTTP integration tests", "release approval"]),
        new(
            "brownfield",
            "enhancement",
            "Add vanity aliases, TTL, and rate limiting to an existing URL service without breaking redirects.",
            ["analyze blast radius", "checkpoint current behavior", "implement isolated enhancement", "rollback on regression"],
            ["regression suite", "alias collision test", "rate-limit test", "rollback verification"]),
        new(
            "ambiguous",
            "clarification required",
            "The business needs shorter links and better reporting; TTL, access policy, and analytics detail are unspecified.",
            ["capture ambiguity", "pause for human clarification", "replan affected tasks", "validate clarified contract"],
            ["clarification approval", "plan version increment", "selective invalidation", "contract validation"])
    ];

    public static ScenarioDefinition? Find(string name) =>
        All.SingleOrDefault(scenario => scenario.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}