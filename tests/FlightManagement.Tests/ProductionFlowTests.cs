using System.Net;
using System.Net.Http.Json;
using FlightManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Tests;

public sealed class ProductionFlowTests
{
    [Fact]
    public async Task ProductionCookieAndCsrf_WorkAcrossWebAndApi_WithoutDisablingSecurePolicies()
    {
        var host = new FlightHost { EnvironmentName = "Production" };
        try
        {
            await host.InitializeAsync();
            using var account = await host.RegisterAndLogin();
            var flight = await host.Flight();
            var response = await FlightHost.PostForm(account.Browser, "/?handler=Book", new()
            {
                ["BookForm.FlightId"] = flight.Id.ToString(), ["BookForm.FirstName"] = "Synthetic",
                ["BookForm.LastName"] = "Test", ["BookForm.PassportNumber"] = "TEST-ONLY",
                ["BookForm.Email"] = "synthetic@example.invalid", ["BookForm.Phone"] = "TEST",
                ["BookForm.DateOfBirth"] = "2000-01-01", ["BookForm.SeatNumber"] = "2B", ["BookForm.Price"] = "1000"
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await using var db = host.Db();
            var ticket = await db.Tickets.SingleAsync(t => t.FlightId == flight.Id);
            Assert.Equal(account.Id, ticket.OwnerUserId);
            Assert.Equal(HttpStatusCode.OK, (await account.Api.GetAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
            account.Api.DefaultRequestHeaders.Remove(FlightManagement.Security.FlightSecurity.CsrfHeader);
            Assert.Equal(HttpStatusCode.BadRequest, (await account.Api.DeleteAsync($"/api/Tickets/{ticket.Id}")).StatusCode);
            Assert.True(await db.Tickets.AnyAsync(t => t.Id == ticket.Id));
        }
        finally { await host.DisposeAsync(); }
    }
}
