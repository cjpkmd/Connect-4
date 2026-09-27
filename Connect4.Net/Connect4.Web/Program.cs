using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Connect4.App.Services;
using Connect4.App.ViewModels;
using Connect4.Web;
using Connect4.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(services => (IJSInProcessRuntime)services.GetRequiredService<IJSRuntime>());
builder.Services.AddSingleton<BrowserDialogService>();
builder.Services.AddSingleton<IDialogService>(services => services.GetRequiredService<BrowserDialogService>());
builder.Services.AddSingleton<IGameFileService, BrowserGameFileService>();
builder.Services.AddSingleton<ISettingsStore, LocalStorageSettingsStore>();
builder.Services.AddSingleton<ISoundService, BrowserSoundService>();
builder.Services.AddSingleton<BrainDocs>();
builder.Services.AddSingleton<AppearanceStore>();
builder.Services.AddSingleton<IEngineHost, WebEngineHost>();
builder.Services.AddSingleton(services => new MainViewModel(
    services.GetRequiredService<IEngineHost>(),
    services.GetRequiredService<IDialogService>(),
    services.GetRequiredService<IGameFileService>(),
    services.GetRequiredService<ISettingsStore>(),
    services.GetRequiredService<ISoundService>()));

await builder.Build().RunAsync();
