using LightMessage.Common.Messages;
using LightMessage.Common.ProtocolMessages;
using LightMessage.OrleansUtils.GrainInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public interface ITestHub : IEndPointGrain
    {
        // Task<EndPointFunctionResult> SomeFunc(EndPointFunctionParams Params);
        // Task<EndPointFunctionResult> load(EndPointFunctionParams p);
    }
}
