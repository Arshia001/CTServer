using LightMessage.OrleansUtils.GrainInterfaces;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public interface ISystemEndPoint : IEndPointGrain
    {
        //Task<EndPointFunctionResult> GetProfileInfo(EndPointFunctionParams Params);
        //Task<EndPointFunctionResult> SetActiveCustomizations(EndPointFunctionParams Params);
    }
}
