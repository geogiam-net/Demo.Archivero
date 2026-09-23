using Demo.Archivero.Converter.Worker.Startup;
using Demo.Archivero.Converter.Worker.StartUp;
using Demo.Archivero.Infrastructure.ServiceBus;

var builder = Host.CreateApplicationBuilder(args);
SettingsTester.TestSettingsExist(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();
builder.Services.AddArchiveroFileBus(builder.Configuration);
builder.Services.AddFileWorkers();
await builder.Build().RunAsync();
