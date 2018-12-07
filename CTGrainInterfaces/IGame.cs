using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public interface IGame : IGrain
    {
        Task Start(List<Guid> Players, int GameID);
        Task<bool> SetReady(Guid ClientID);
        Task<Immutable<GameConfig>> GetGameConfig();
        Task<bool> IsActive();
    }
}
