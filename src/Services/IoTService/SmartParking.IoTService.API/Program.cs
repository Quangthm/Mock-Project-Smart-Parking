using SmartParking.IoTService.API;

// Demo stays explicit; normal hosting exposes only S04 and authenticated management.
var environment = WebApplication.CreateBuilder(args).Environment.EnvironmentName;
var app = environment == "Demo" ? AssistantDemoHost.Build(args) : AssistantGuestHost.Build(args);
await app.RunAsync();
