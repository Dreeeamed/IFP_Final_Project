using System.Globalization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WordDuel.Core.Words;
using WordDuel.Web.Client.Services;

// The UI text is English, so numbers and dates use English formatting too ("65.5s", "15 Nov").
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// In WebAssembly a singleton lives for the lifetime of the browser tab.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<BrowserInterop>();
builder.Services.AddSingleton<UserPreferences>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<DuelConnection>();
builder.Services.AddSingleton(_ => EmbeddedWordLists.Load()); // only resolved by the practice page

await builder.Build().RunAsync();
