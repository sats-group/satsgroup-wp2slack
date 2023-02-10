using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SatsGroup.Devs.WorkplaceToSlackPoster;

public class Program
{
    static async Task Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureServices(s =>
            {
                s.AddHttpClient();
            })
            .Build();
        await host.RunAsync();
        
    }
}