using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Demo.Archivero.Api.Security;
using Demo.Archivero.Api.Startup;
using Demo.Archivero.Application.Security;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using System.Text;

namespace Demo.Archivero.Api.Startup;

public static class Configuration
{
    public static async Task RunDatabaseMigrations(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContextArchivero>();
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
            app.Logger.LogCritical(ex, "Archivero startup failed during database migration or authentication seed.");
            throw;
        }
    }

    public static void AddAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(JwtSettings.ArchiveroJwtConfiguration).Get<JwtSettings>();

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
                    ValidIssuer = settings!.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AnyModuleView", policy =>
                policy.RequireAnyPermission(
                    AppPermissions.ArchiveroView));
        });
    }
}