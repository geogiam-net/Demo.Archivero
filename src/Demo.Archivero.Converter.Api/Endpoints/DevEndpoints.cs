using Slg.DeadKm.Domain.Extensions;
using Slg.DeadKm.Application.Interfaces.Infrastructure;
using Slg.DeadKm.Domain.Math;

namespace Slg.DeadKm.Api.Endpoints;

public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/dev/TestTomTomClient", TestTomTomClient);
        app.MapPatch("/api/dev/TestIvuDataSourceClient", TestIvuDataSourceClient);

        return app;
    }

    private static async Task<IResult> TestTomTomClient(IRouteFinder routeFinder, CancellationToken ct)
    {
        var sgl = new Vector2(5.920732, 49.5680827);
        var ikea = new Vector2(5.8889419, 49.6368705);
        var route = await routeFinder.FindRoute(sgl, ikea, ct);

        return TypedResults.NotFound();
    }

    private static async Task<IResult> TestIvuDataSourceClient(IIvuDataSourceService ivuDataSourceService, CancellationToken ct)
    {
        var date = DateTime.Parse("06/01/2026", System.Globalization.CultureInfo.InvariantCulture);
        var carPlates = await ivuDataSourceService.GetCarPlatesAsync(DateOnly.FromDateTime(date), DateOnly.FromDateTime(date), ct);
        var found = carPlates.ToList().Any(p => p.CleanCarPlate() == "SL3668");

        var trips = await ivuDataSourceService.GetIvuTripDataAsync(DateOnly.FromDateTime(date), "SL3668", ct);
        
        return TypedResults.NotFound();
    }
}
