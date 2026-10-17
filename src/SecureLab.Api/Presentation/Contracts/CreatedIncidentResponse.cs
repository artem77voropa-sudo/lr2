namespace SecureLab.Api.Presentation.Contracts;

public sealed record CreatedIncidentResponse(
    Guid Id, string Title, string Description, string Severity, string Status,
    DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
