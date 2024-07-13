using dkgWebNode.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using BlazorDownloadFile;

namespace dkgWebNode
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddMudServices();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddBlazorDownloadFile(ServiceLifetime.Scoped);
            builder.Services.AddSingleton<DkgWebNodeService>();
            builder.Services.AddSingleton<KeystoreService>();

            builder.Logging.SetMinimumLevel(LogLevel.Debug);
            builder.Logging.AddFilter("Microsoft", LogLevel.Information); 
            builder.Logging.AddFilter("System", LogLevel.Information); 
            
            var host = builder.Build();
            await host.RunAsync();
        }
    }
}

