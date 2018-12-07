using LightMessage.OrleansUtils.GrainInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public enum ReadyResponse
    {
        OK = 1,
        AlreadyInProgress = 2,
        NotInGame = 3
    }

    public interface IMatchmakingEndPoint : IEndPointGrain
    {
        Task SendJoinGame(Guid ClientID, uint OpponentLevel, string OpponentName, IEnumerable<int> OpponentCustomizations, ulong TotalGold);
        Task SendRemovedFromQueue(Guid ClientID);
    }
}
