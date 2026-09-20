using Demo.Archivero.Converter.Api;
using Demo.Archivero.Converter.Api.Endpoints;
using Demo.Archivero.Converter.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

Demo.Archivero.Converter.Api.StartUp.SettingsTester.TestSettingsExist(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();

#if DEBUG
builder.Services.AddProblemDetails();
#endif

// ------------------------------------------------------------------------------------

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    //options.RoutePrefix = "swagger";
});

// UseStatusCodePages enables middleware that provides default responses for HTTP status codes
// (like 404, 400, 500) when your API does not return a body
app.UseStatusCodePages();
app.UseExceptionHandler();

app.MapFileEndpoints();

app.MapGet(Routes.Ping, () => Results.Ok(new
{
    status = "ok",
    service = "Archivero.Converter API is up"
}));

app.Run();
