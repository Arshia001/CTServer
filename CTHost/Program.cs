using CTGrainInterfaces;
using CTGrains;
using LightMessage.Common.ProtocolMessages;
using LightMessage.OrleansUtils.GrainInterfaces;
using LightMessage.OrleansUtils.Grains;
using LightMessage.OrleansUtils.Host;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Logging;
using Orleans.Providers;
using Orleans.Runtime;
using OrleansBondUtils.CassandraInterop;
using OrleansCassandraUtils;
using OrleansCassandraUtils.Clustering;
using OrleansCassandraUtils.Persistence;
using OrleansCassandraUtils.Reminders;
using OrleansIndexingGrains;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace CTHost
{
    class Program
    {
        static IClusterClient client;

        static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip;
            }

            throw new Exception("No network adapters with an IPv4 address in the system");
        }

        static async Task Main(string[] args)
        {
            var silo = new SiloHostBuilder()
                .ConfigureServices(e => Startup.ConfigureServices(e))
                .Configure<ClusterOptions>(o =>
                {
                    o.ClusterId = "CTServer";
                    o.ServiceId = "CTServer";
                })
                .Configure<EndpointOptions>(o =>
                {
                    o.AdvertisedIPAddress = GetLocalIPAddress();
                    o.GatewayPort = 40000;
                    o.SiloPort = 11111;
                })
                .EnableDirectClient()
                .UseCassandraClustering((CassandraClusteringOptions o) => o.ConnectionString = CTSettings.Instance.ConnectionString)
                .UseCassandraReminderService((CassandraReminderTableOptions o) => o.ConnectionString = CTSettings.Instance.ConnectionString)
                .AddCassandraGrainStorageAsDefault((CassandraGrainStorageOptions o) =>
                {
                    o.ConnctionString = CTSettings.Instance.ConnectionString;
                    o.AddSerializationProvider(1, new BondCassandraStorageSerializationProvider());
                })
                .ConfigureLogging(l => l.AddFilter("Orleans", LogLevel.Information).AddConsole().AddFile($"log_{DateTime.Now:yy-MM-dd-H-mm-ss}"))
                .ConfigureApplicationParts(p =>
                {
                    p.AddApplicationPart(typeof(Startup).Assembly).WithReferences();
                    p.AddApplicationPart(typeof(EndPointGrain).Assembly).WithReferences();
                    p.AddApplicationPart(typeof(IndexerGrainUnique<,>).Assembly).WithReferences();
                })
                .Configure<SerializationProviderOptions>(o =>
                {
                    o.SerializationProviders.Add(typeof(LightMessageSerializer).GetTypeInfo());
                })
                .AddStartupTask<CTStartupTask>()
                .Configure<GrainCollectionOptions>(e => e.ClassSpecificCollectionAge[typeof(LeaderBoard).FullName] = TimeSpan.FromHours(12))
                .Build();

            await silo.StartAsync();

            var lightMessageLogger = new LightMessageLogProvider((ILogger)silo.Services.GetService(typeof(ILogger<LightMessageOrleansHost>)),
#if DEBUG
                LightMessage.Common.Util.LogLevel.Verbose, true
#else
                LightMessage.Common.Util.LogLevel.Info
#endif
                );

            client = (IClusterClient)silo.Services.GetService(typeof(IClusterClient));

            var lightMessageHost = new LightMessageOrleansHost();
            await lightMessageHost.Start(client, new IPEndPoint(IPAddress.Any, 1020), OnAuth, lightMessageLogger);

            while (Console.ReadLine() != "exit")
                Console.WriteLine("Type 'exit' to stop silo and exit");

            lightMessageHost.Stop();
            await silo.StopAsync();
        }

        static Task<Guid?> OnAuth(AuthRequestMessage AuthMessage)
        {
            return client.GetGrain<IClientAuthorizer>(0).Authorize(AuthMessage);
        }
    }
}
