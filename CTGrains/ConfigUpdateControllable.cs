using CTGrainInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Providers;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CTGrains
{
    public class ConfigUpdateControllable : IControllable
    {
        public static string ServiceName = "ConfigUpdater";

        IGrainFactory GrainFactory;
        IConfigWriter ConfigWriter;

        public ConfigUpdateControllable(IGrainFactory GrainFactory, IConfigWriter ConfigWriter)
        {
            this.GrainFactory = GrainFactory;
            this.ConfigWriter = ConfigWriter;
        }

        public async Task<object> ExecuteCommand(int command, object arg)
        {
            if (command > ConfigWriter.Version)
                ConfigWriter.Config = (await GrainFactory.GetGrain<ISystemConfig>(0).GetConfig()).Value;
            return null;
        }
    }
}
