using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public interface IMatchmakerGrain : IGrainWithIntegerKey
    {
        Task<bool> EnterQueue(IUserProfile User, Immutable<UserProfileState> ProfileInfo);
        // Task LeaveQueue(IUserProfile User, Immutable<UserProfileState> ProfileInfo);
    }
}
