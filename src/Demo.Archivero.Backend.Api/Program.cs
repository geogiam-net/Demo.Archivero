using Demo.Archivero.Api;
using Demo.Archivero.Api.Endpoints;
using Demo.Archivero.Api.Startup;
using Demo.Archivero.Api.StartUp;
using Microsoft.OpenApi;
using Demo.Archivero.Infrastructure.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

SettingsTester.TestSettingsExist(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Enables the "Authorize" button so secured (RequireAuthorization) endpoints
    // can be tested by pasting the JWT returned from /api/auth/login.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT from /api/auth/login. No 'Bearer ' prefix needed."
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("web",
        policy => policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin());
});

builder.Services.AddAuthorization(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();
builder.Services.AddArchiveroFileBus(builder.Configuration);

#if DEBUG
builder.Services.AddProblemDetails();
#endif

// ------------------------------------------------------------------------------------

var app = builder.Build();

await app.RunDatabaseMigrations();

app.UseCors("web");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    //options.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// UseStatusCodePages enables middleware that provides default responses for HTTP status codes
// (like 404, 400, 500) when your API does not return a body
app.UseStatusCodePages();
app.UseExceptionHandler();

app.MapAuthEndpoints();
app.MapFileEndpoints();

app.MapGet(Routes.Health, () => Results.Ok(new
{
    status = "ok",
    service = "Archivero.Backend API is up"
}));

app.Run();
