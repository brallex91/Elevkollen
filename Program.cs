using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Elevkollen;
using Elevkollen.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

// Skolverket's open API is called directly from the browser \u2014 no server of our own.
// All student data lives locally in IndexedDB and never leaves the device.
var syllabusBaseUrl = builder.Configuration["Syllabus:BaseUrl"]
    ?? "https://api.skolverket.se/syllabus/v1/";
builder.Services.AddHttpClient<SyllabusClient>(http => http.BaseAddress = new Uri(syllabusBaseUrl));

builder.Services.AddScoped<StudentStore>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<TourState>();

await builder.Build().RunAsync();
