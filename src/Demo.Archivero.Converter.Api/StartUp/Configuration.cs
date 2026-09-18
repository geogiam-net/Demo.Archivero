using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Slg.DeadKm.Api.Security;
using Slg.DeadKm.Api.Startup;
using Slg.DeadKm.Application.Security;
using Slg.DeadKm.Infrastructure.SqlRepository.Data;
using System.Text;

namespace Slg.DeadKm.Api.Startup;

public static class Configuration
{
    public static async Task RunDatabaseMigrations(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DeadKmDbContext>();
            var migrateOnStartup = app.Configuration.GetValue("Database:EnsureCreatedOnStartup", true);

            if (migrateOnStartup)
            {
                app.Logger.LogInformation("Database migration starting.");
                await db.Database.MigrateAsync();
                app.Logger.LogInformation("Database migration completed.");
            }

            app.Logger.LogInformation("Authentication seed starting.");
            await AppUserSeeder.SeedAsync(db, app.Configuration);
            app.Logger.LogInformation("Authentication seed completed.");
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "WPS startup failed during database migration or authentication seed.");
            throw;
        }
    }

    public static void AddAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSigningKey = configuration["DeadKmJwt:SigningKey"]
    ?? throw new InvalidOperationException("DeadKmJwt:SigningKey is not configured.");

        var jwtIssuer = configuration["DeadKmJwt:Issuer"]
            ?? throw new InvalidOperationException("DeadKmJwt:Issuer is not configured.");

        var jwtAudience = configuration["DeadKmJwt:Audience"]
            ?? throw new InvalidOperationException("DeadKmJwt:Audience is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        services.AddAuthorization(options =>
        {

            options.AddPolicy("AnyModuleView", policy =>
                policy.RequireAnyPermission(
                    AppPermissions.DeadKmView));
        });
    }
}