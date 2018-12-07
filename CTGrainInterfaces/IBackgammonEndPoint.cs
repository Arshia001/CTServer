using LightMessage.OrleansUtils.GrainInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public interface IBackgammonEndPoint : IEndPointGrain
    {
        Task SendStartGame(Guid ClientID, byte MyColor, uint OpponentLevel, string OpponentName, IEnumerable<int> OpponentItems, int GameID); // corresponding to Logic.Color: white 1, black 0
        Task SendInitDiceRolled(Guid ClientID, byte My, byte Their, byte Stamp);
        Task SendStartTurn(Guid ClientID, bool My, byte Stamp, TimeSpan TurnTime);
        Task SendOpponentUndo(Guid ClientID, byte Stamp);
        Task SendDiceRolled(Guid ClientID, bool My, byte Roll1, byte Roll2, byte Stamp);
        Task SendOpponentMoved(Guid ClientID, sbyte From, sbyte To, byte Stamp);
        Task SendForcedOwnMove(Guid ClientID, sbyte From, sbyte To, byte Stamp);
        Task SendMatchResult(Guid ClientID, bool MyWin, EndMatchResults Rewards, GameOverReason reason);
        Task SendEmote(Guid ClientID, int EmoteID);
    }
}
