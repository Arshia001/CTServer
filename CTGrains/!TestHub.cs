using CTGrainInterfaces;
using LightMessage.Common.Messages;
using LightMessage.Common.ProtocolMessages;
using LightMessage.OrleansUtils.GrainInterfaces;
using LightMessage.OrleansUtils.Grains;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    [StatelessWorker(128), EndPointName("test")]
    public class TestHub : EndPointGrain, ITestHub
    {
    }
}
