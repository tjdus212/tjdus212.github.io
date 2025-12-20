using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SwiftPage.Web;
using MudBlazor.Services;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var ko = new CultureInfo("ko-KR");
CultureInfo.DefaultThreadCurrentCulture = ko;
CultureInfo.DefaultThreadCurrentUICulture = ko;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();



var app = builder.Build();
await app.RunAsync();
