namespace Slg.DeadKm.Api;
public static class Routes
{
    // AuthEndpoints
    public const string Health = "/api/health";
    public const string Login = "/api/auth/login";
    public const string Me = "/api/auth/me";
    public const string VehicleShiftsInDateRange = "/api/vehicle-shifts/{startDate}/{endDate}";
    public const string VehicleShiftById = "/api/vehicle-shifts/{id:int}";
}