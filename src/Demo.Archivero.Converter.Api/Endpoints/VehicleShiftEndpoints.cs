using Slg.DeadKm.Application;
using Slg.DeadKm.Domain.Extensions;
using Slg.DeadKm.Application.Interfaces.Application;

namespace Slg.DeadKm.Api.Endpoints;

public static class VehicleShiftEndpoints
{
    public static IEndpointRouteBuilder MapVehicleShiftEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Routes.VehicleShiftsInDateRange, GetVehicleShiftsInDateRange).RequireAuthorization();

        app.MapGet(Routes.VehicleShiftById, GetDetailedVehicleShift).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> GetVehicleShiftsInDateRange(
        DateOnly startDate,
        DateOnly endDate,
        IVehicleShiftService vehicleShiftService,
        CancellationToken ct
    )
    {
        var result = await vehicleShiftService.GetVehicleShiftsInDateRange(startDate, endDate, ct);

        if(result.Errors.Any(p => p.StartsWith(Errors.ParameterError)))
        {
            return Results.BadRequest(result.Errors.First());
        }
        else if (result.Errors.Any(p => p.StartsWith(Errors.MissingData)))
        {
            // Let GUI get message
        }
        else if (result.Errors.Any())
        {
            return Results.InternalServerError(result.Errors.First());
        }

        return TypedResults.Ok(result);
    }

    private static async Task<IResult> GetDetailedVehicleShift(
        int id,
        IVehicleShiftService vehicleShiftService,
        CancellationToken ct
    )
    {
        var result = await vehicleShiftService.GetDetailedVehicleShift(id, ct);

        if (result is null)
        {
            return Results.NotFound();
        }

        if (result.Errors.Any())
        {
            return Results.InternalServerError(result.Errors.First());
        }

        return TypedResults.Ok(result);
    }
}
