using Tracker;
using Tracker.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Frontend-only app: all state lives in this browser's localStorage.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<PreferencesService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<WaterTrackingService>();
builder.Services.AddScoped<ReminderService>();

await builder.Build().RunAsync();
