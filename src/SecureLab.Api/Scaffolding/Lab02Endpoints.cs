using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, SecureLabDbContext db, CancellationToken ct) =>
        {
            var order = sortBy switch
            {
                null or "" or "createdAtUtc" => "created_at_utc DESC",
                "severity" => "severity", "status" => "status", _ => sortBy
            };
            var sql = "SELECT * FROM incidents WHERE title ILIKE '%" + (q ?? "")
                + "%' OR description ILIKE '%" + (q ?? "") + "%' ORDER BY " + order + " LIMIT 50";
            var rows = await db.Incidents.FromSqlRaw(sql).AsNoTracking().ToListAsync(ct);
            return Results.Ok(rows.Select(row => new
            {
                row.Id, row.Title, row.Description,
                Severity = row.Severity.ToString(), Status = row.Status.ToString(), row.CreatedAtUtc
            }));
        });
        app.MapPost("/api/incidents", async (CreateIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();
            var title = request.Title?.Trim() ?? "";
            var description = request.Description?.Trim() ?? "";

            if (title.Length == 0 || request.Title!.Length > 160)
                errors["title"] = ["Заголовок обов'язковий і не може перевищувати 160 символів до обрізання пробілів."];
            if (description.Length == 0 || request.Description!.Length > 4000)
                errors["description"] = ["Опис обов'язковий і не може перевищувати 4000 символів до обрізання пробілів."];

            var validSeverity = Enum.TryParse<IncidentSeverity>(request.Severity, true, out var severity)
                && Enum.IsDefined(severity);
            if (!validSeverity)
                errors["severity"] = ["Допустимі значення: Low, Medium, High, Critical."];
            if (request.OccurredAtUtc is null || request.OccurredAtUtc > now.AddMinutes(5))
                errors["occurredAtUtc"] = ["Час події обов'язковий і не може бути більш ніж на 5 хвилин у майбутньому."];
            if (validSeverity && severity is IncidentSeverity.High or IncidentSeverity.Critical
                && description.Length < 40)
                errors["description"] = ["Для High або Critical опис після обрізання пробілів має містити щонайменше 40 символів."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // Навчальна перевірка конфлікту; не гарантує унікальність при одночасних POST.
            if (await db.Incidents.AnyAsync(row => row.Title == title
                && (row.Status == IncidentStatus.New || row.Status == IncidentStatus.Triaged
                    || row.Status == IncidentStatus.InProgress || row.Status == IncidentStatus.Resolved), ct))
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "Інцидент уже існує",
                    detail: "Незакритий інцидент із таким заголовком уже зареєстровано.");

            var incident = new Incident
            {
                Id = Guid.NewGuid(), OwnerUserId = DbSeeder.AliceId,
                Title = title, Description = description, Severity = severity,
                Status = IncidentStatus.New, OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/incidents/{incident.Id}", new CreatedIncidentResponse(
                incident.Id, incident.Title, incident.Description, incident.Severity.ToString(),
                incident.Status.ToString(), incident.OccurredAtUtc, incident.CreatedAtUtc, incident.UpdatedAtUtc));
        });
    }
}

