using Orleans;
using Orleans.Concurrency;
using OrleansBondUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    [BondSerializationTag("@lbt3")]
    public interface ILeaderBoardTop3Entry : IGrainWithStringKey
    {
        Task<bool> IsSet();
        Task Set(IEnumerable<LeaderBoardEntry> Entries);
        Task<Immutable<LeaderBoardEntry[]>> Get();
    }
}
