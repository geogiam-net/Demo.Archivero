namespace Demo.Archivero.Application.Dtos.User;

public sealed record UpdateUserDto(
    string Role,
    bool IsActive,
    IReadOnlyList<string>? MaintenanceSkills,
    int? DepotId,
    bool? IsAvailableForMaintenance,
    int? DailyCapacityMinutes);