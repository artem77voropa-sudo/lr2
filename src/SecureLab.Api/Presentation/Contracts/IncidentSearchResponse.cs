namespace SecureLab.Api.Presentation.Contracts;

public sealed record IncidentSearchResponse(
    Guid Id, string Title, string Description, string Severity, string Status, DateTimeOffset CreatedAtUtc);
