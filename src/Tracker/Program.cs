using Tracker;
using Tracker.Models;
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
builder.Services.AddScoped<DataBackupService>();

// Feedback form: EmailJS IDs come from wwwroot/appsettings.json (public by design; empty = form shows "not connected").
builder.Services.AddSingleton(new EmailJsOptions(
    builder.Configuration["Feedback:EmailJs:ServiceId"],
    builder.Configuration["Feedback:EmailJs:TemplateId"],
    builder.Configuration["Feedback:EmailJs:PublicKey"]));
builder.Services.AddScoped(_ => new HttpClient());
builder.Services.AddScoped<FeedbackService>();

await builder.Build().RunAsync();
