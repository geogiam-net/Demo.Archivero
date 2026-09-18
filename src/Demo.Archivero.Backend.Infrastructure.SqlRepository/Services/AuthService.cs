using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Demo.Archivero.Infrastructure.SqlRepository.Data;
using Demo.Archivero.Application.Security;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Dtos.Auth;
using Demo.Archivero.Application.Dtos;
using Demo.Archivero.Domain.Enums;

namespace Demo.Archivero.Infrastructure.SqlRepository.Services;

public sealed class AuthService(
    Data.DbContextArchivero db,
    IConfiguration configuration) : IAuthService
{
    public async Task<ResultDto<LoginResponseDto?>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            username = username.Trim();

            var user = await db.AppUsers
                .FirstOrDefaultAsync(
                    x => x.Username == username && x.IsActive,
                    cancellationToken);

            if (user is null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                var errorMessages = new List<string> { "Invalid username or password." };
                return new ResultDto<LoginResponseDto?>(null, Error.NotAuthorized, errorMessages);
            }

            user.LastLoginAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var settings = configuration.GetSection(JwtSettings.ArchiveroJwtConfiguration).Get<JwtSettings>();

            var expirationMinutes = settings!.ExpirationMinutes ?? 480;

            var expiresAtUtc = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var permissions = AppPermissionCatalog.GetPermissions(user.Role);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: settings.Issuer,
                audience: settings.Audience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: credentials);

            return new ResultDto<LoginResponseDto?>(new LoginResponseDto(
                new JwtSecurityTokenHandler().WriteToken(token),
                user.Username,
                user.Role,
                permissions,
                expiresAtUtc));

        }
        catch (Exception)
        {
            return new ResultDto<LoginResponseDto?>(null, Error.InternalServerError, []);
        }
    }
}
