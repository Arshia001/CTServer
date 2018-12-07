using CTGrainInterfaces;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConfigUpdateUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            IClusterClient Client;

            while (true)
                try
                {
                    Client = new ClientBuilder()
                        .UseLocalhostClustering(40000, "CTServer", "CTServer")
                        .ConfigureLogging(l => l.AddFilter("Orleans", LogLevel.Information).AddConsole())
                        .Build();
                    Client.Connect().Wait();
                    break;
                }
                catch (Exception Ex)
                {
                    Console.WriteLine("Unable to connect due to exception: " + Ex.ToString());
                    Thread.Sleep(1000);
                    continue;
                }

            Console.WriteLine("Connected, sending update request...");

            Client.GetGrain<ISystemConfig>(0).UpdateConfig().Wait();

            Console.WriteLine("Done, press enter to exit...");
            Console.ReadLine();
        }
    }
}
