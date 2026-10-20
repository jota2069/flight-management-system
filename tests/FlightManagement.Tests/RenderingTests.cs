using Microsoft.EntityFrameworkCore;

namespace FlightManagement.Tests;

public sealed class RenderingTests(FlightHost host) : IClassFixture<FlightHost>
{
    [Fact]
    public async Task Razor_FlightDataIsEncodedData_NotExecutableHandlersOrInnerHtml()
    {
        using var admin = await host.RegisterAndLogin();
        await host.MakeAdmin(admin);
        var flight = await host.Flight();
        await using (var db = host.Db())
        {
            var row = (await db.Flights.FindAsync(flight.Id))!;
            row.FlightNumber = "X');window.__flight_xss=true;//";
            row.Origin = "<img src=x onerror=window.__flight_xss=true>";
            await db.SaveChangesAsync();
        }
        var index = await admin.Browser.GetStringAsync("/");
        var administration = await admin.Browser.GetStringAsync("/Admin");
        Assert.Contains("data-book-flight=", index);
        Assert.Contains("data-edit-flight=", administration);
        Assert.Contains("data-origin=\"&lt;img", index);
        Assert.Contains("data-origin=\"&lt;img", administration);
        Assert.DoesNotContain("onclick=\"openBooking(", index);
        Assert.DoesNotContain("onclick=\"openEdit(", administration);
        Assert.DoesNotContain(".innerHTML", index);
        Assert.Contains(".textContent", index);
        Assert.DoesNotContain("<img src=x", index);
        Assert.DoesNotContain("<img src=x", administration);
    }
}
