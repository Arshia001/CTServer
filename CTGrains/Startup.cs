using CTGrainInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Providers;
using Orleans.Runtime;
using OrleansBondUtils.CassandraInterop;
using OrleansCassandraUtils.Reminders;

namespace CTGrains
{
    public class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IGrainReferenceConversionProvider, BondGrainReferenceConversionProvider>();

            var ConfigProvider = new ConfigProvider();
            services.AddSingleton<IConfigWriter>(ConfigProvider);
            services.AddSingleton<IConfigReader>(ConfigProvider);

            services.AddSingletonNamedService<IControllable, ConfigUpdateControllable>(ConfigUpdateControllable.ServiceName);
        }
    }
}
