using BackgammonLogic;
using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public enum GameOverReason
    {
        GameEndedNormally = 1,
        Inactivity = 2,
        Concede = 3,
        FailedToJoinInTime = 4
    }

    public class BackgammonGameState
    {
        public BackgammonGameLogic Game { get; set; }
        public byte PlayerColorIdx { get; set; }
        public TimeSpan CurrentTurnRemainingTime { get; set; }
        public uint OpponentLevel { get; set; }
        public string OpponentName { get; set; }
        public List<int> OpponentItems { get; set; }
        public int GameID { get; set; }
    }

    public interface IBackgammonGame : IGame, IGrainWithGuidKey
    {
        Task<byte> MakeMove(Guid ClientID, sbyte[] From, sbyte[] To, byte InitialStamp);
        Task<bool> RollDice(Guid ClientID, byte InitialStamp);
        Task<bool> UndoLastMove(Guid ClientID, byte InitialStamp);
        Task<bool> EndTurn(Guid ClientID, byte InitialStamp);
        Task<Immutable<BackgammonGameState>> GetGameState(Guid ClientID);
        Task Emote(Guid ClientID, int EmoteID);
        Task Concede(Guid ClientID);
    }
}
