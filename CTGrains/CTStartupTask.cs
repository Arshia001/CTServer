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
    public class CTStartupTask : IStartupTask
    {
        IGrainFactory GrainFactory;
        IConfigWriter ConfigWriter;

        public CTStartupTask(IGrainFactory GrainFactory, IConfigWriter ConfigWriter)
        {
            this.GrainFactory = GrainFactory;
            this.ConfigWriter = ConfigWriter;
        }

        public async Task Execute(CancellationToken cancellationToken)
        {
            ConfigWriter.Config = (await GrainFactory.GetGrain<ISystemConfig>(0).GetConfig()).Value;

            await GrainFactory.GetGrain<ILeaderBoard>(LeaderBoardUtil.LifetimeId).Initialize();
            await GrainFactory.GetGrain<ILeaderBoard>(LeaderBoardUtil.GetThisMonthIdString()).Initialize();

            if (!await GrainFactory.GetGrain<ILeaderBoardTop3Entry>(LeaderBoardUtil.GetLastMonthIdString()).IsSet())
                await GrainFactory.GetGrain<ILeaderBoard>(LeaderBoardUtil.GetLastMonthIdString()).CheckEndMonthHighScores();
        }
    }
}
