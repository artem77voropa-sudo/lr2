using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class SearchRegressionTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("USB", "20000000-0000-0000-0000-000000000005")]
    [InlineData("O'Brien", "20000000-0000-0000-0000-000000000004")]
    public async Task T04_Search_ReturnsExpectedSeed(string q, string expectedId)
    {
        var rows = await _client.GetFromJsonAsync<List<IncidentSearchResponse>>(
            "/api/incidents/search?q=" + Uri.EscapeDataString(q));
        Assert.NotNull(rows);
        Assert.Equal(Guid.Parse(expectedId), Assert.Single(rows).Id);
    }

    [Fact]
    public async Task T05_UnknownSort_ReturnsValidationProblem()
    {
        using var response = await _client.GetAsync("/api/incidents/search?sortBy=price");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public async Task Search_TreatsWildcardCharactersLiterally_InTitleAndDescription(string character)
    {
        var marker = "Literal" + Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        Incident Make(string title, string description) => new()
        {
            Id = Guid.NewGuid(), OwnerUserId = DbSeeder.AliceId, Title = title, Description = description,
            Severity = IncidentSeverity.Low, Status = IncidentStatus.New,
            OccurredAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var inTitle = Make(marker + character, "Навчальні дані");
        var inDescription = Make(marker + "description", marker + character);
        var decoy = Make(marker + "X", "Навчальні дані");
        var ids = new[] { inTitle.Id, inDescription.Id, decoy.Id };
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        try
        {
            db.Incidents.AddRange(inTitle, inDescription, decoy);
            await db.SaveChangesAsync();
            var rows = await _client.GetFromJsonAsync<List<IncidentSearchResponse>>(
                "/api/incidents/search?q=" + Uri.EscapeDataString(marker.ToLowerInvariant() + character));
            Assert.NotNull(rows);
            Assert.Equal(new[] { inTitle.Id, inDescription.Id }.Order(), rows.Select(row => row.Id).Order());
        }
        finally { await db.Incidents.Where(row => ids.Contains(row.Id)).ExecuteDeleteAsync(); }
    }

    [Theory]
    [InlineData("createdAtUtc")]
    [InlineData("severity")]
    [InlineData("status")]
    [InlineData("")]
    public async Task Search_SortsByContractWithStableId_ThenLimitsTo50(string sortBy)
    {
        var marker = "Sort" + Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var items = Enumerable.Range(0, 55).Select(i => new Incident
        {
            Id = Guid.Parse($"{marker[4..12]}-0000-0000-0000-{i:000000000000}"),
            OwnerUserId = DbSeeder.AliceId, Title = marker + i, Description = "Навчальні дані",
            Severity = (IncidentSeverity)(i % 4), Status = (IncidentStatus)(i % 5),
            OccurredAtUtc = now, CreatedAtUtc = now.AddMinutes(i % 3), UpdatedAtUtc = now
        }).ToList();
        var ids = items.Select(row => row.Id).ToArray();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        try
        {
            db.Incidents.AddRange(items);
            await db.SaveChangesAsync();
            var rows = await _client.GetFromJsonAsync<List<IncidentSearchResponse>>(
                $"/api/incidents/search?q={marker}&sortBy={sortBy}");
            Assert.NotNull(rows);
            var expected = sortBy switch
            {
                "severity" => items.OrderByDescending(row => row.Severity),
                "status" => items.OrderBy(row => row.Status),
                _ => items.OrderByDescending(row => row.CreatedAtUtc)
            };
            Assert.Equal(expected.ThenBy(row => row.Id).Take(50).Select(row => row.Id), rows.Select(row => row.Id));
        }
        finally { await db.Incidents.Where(row => ids.Contains(row.Id)).ExecuteDeleteAsync(); }
    }
}
