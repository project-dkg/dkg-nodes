using dkgWebNode.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;




namespace dkgWebNode
{
    public class Program
    {
        private static DkgWebNodeService? DkgNodeService;
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddMudServices();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddScoped<DkgWebNodeService>();

            var host = builder.Build();
            DkgNodeService = host.Services.GetRequiredService<DkgWebNodeService>();

            await host.RunAsync();
        }

        [JSInvokable]
        public static async Task SaveDataOnShutdown()
        {
            if (DkgNodeService is not null)
            {
                await DkgNodeService.SaveDataOnShutdown();
            }
        }
    }
}

