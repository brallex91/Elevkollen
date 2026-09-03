using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Elevkollen;
using Elevkollen.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

// Skolverkets öppna API anropas direkt från webbläsaren — ingen egen server är inblandad.
// All elevdata ligger lokalt i IndexedDB och lämnar aldrig enheten.
var syllabusBaseUrl = builder.Configuration["Syllabus:BaseUrl"]
    ?? "https://api.skolverket.se/syllabus/v1/";
builder.Services.AddHttpClient<SyllabusClient>(http => http.BaseAddress = new Uri(syllabusBaseUrl));

builder.Services.AddScoped<StudentStore>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<TourState>();

await builder.Build().RunAsync();
