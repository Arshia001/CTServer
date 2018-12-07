using CTGrainInterfaces;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BackgammonLogic;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Concurrency;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;

namespace CTGrains
{
    class BackgammonGame : Game, IBackgammonGame //?? save state in case of silo shutdown
    {
        Guid WhiteId, BlackId;
        int[] InactiveCount = new int[] { 0, 0 };

        BackgammonGameLogic Game;

        Color? AIColor;
        Queue<Tuple<sbyte, sbyte>> AIMoves = new Queue<Tuple<sbyte, sbyte>>();
        IDisposable AIMoveTimer;

        IDisposable TurnTimer;
        DateTime CurrentTurnEndTime;
        ILogger Logger;

        ConfigValues Config;


        public BackgammonGame(IConfigReader ConfigReader, ILogger<BackgammonGame> Logger)
        {
            Config = ConfigReader.Config.ConfigValues;
            this.Logger = Logger;
        }

        public override async Task Start(List<Guid> Players, int GameID)
        {
            if (Players.Count != 1 && Players.Count != 2)
            {
                DeactivateOnIdle();
                throw new InvalidOperationException("Backgammon needs one or two players");
            }

            if (Players.Count == 1)
                Players = new List<Guid> { Players[0], Guid.Empty };

            await base.Start(Players, GameID);

            foreach (var KV in PlayerIdx)
            {
                if (KV.Value == Color.White.AsIndex())
                    WhiteId = KV.Key;
                else
                    BlackId = KV.Key;

                if (KV.Key == Guid.Empty)
                {
                    AIColor = KV.Value == Color.White.AsIndex() ? Color.White : Color.Black;
                    await SetReady(Guid.Empty);
                }
            }

            //?? I really don't like the unnecessary coupling between Game and MatchMaking here...
            var EndPoint = GrainFactory.GetGrain<IMatchmakingEndPoint>(0);

            if (WhiteId != Guid.Empty)
            {
                var Info = await GetInfo(BlackId);
                var UserState = await GrainFactory.GetGrain<IUserProfile>(WhiteId).GetInfo();
                await SendMessage(WhiteId, ID => EndPoint.SendJoinGame(ID, Info.Item1, Info.Item2, Info.Item3, UserState.Value.Funds[CurrencyType.Gold.AsIndex()]));
            }

            if (BlackId != Guid.Empty)
            {
                var Info = await GetInfo(WhiteId);
                var UserState = await GrainFactory.GetGrain<IUserProfile>(BlackId).GetInfo();
                await SendMessage(BlackId, ID => EndPoint.SendJoinGame(ID, Info.Item1, Info.Item2, Info.Item3, UserState.Value.Funds[CurrencyType.Gold.AsIndex()]));
            }
        }

        protected override async Task OnPlayersFailedToJoin()
        {
            if (Ready.All(b => b))
                return; // shouldn't happen, but just in case

            if (Ready[Color.White.AsIndex()])
                await HandleGameOver(Color.White, GameOverReason.FailedToJoinInTime);
            else if (Ready[Color.Black.AsIndex()])
                await HandleGameOver(Color.Black, GameOverReason.FailedToJoinInTime);
            else
                await HandleGameOver(null, GameOverReason.FailedToJoinInTime);

            throw new NotImplementedException();
        }

        async Task<Tuple<uint, string, List<int>>> GetInfo(Guid PlayerID)
        {
            if (PlayerID == Guid.Empty)
                return new Tuple<uint, string, List<int>>(1, "AI", new List<int>()); //?? handle AI profile details (maybe load at random from among current players?)

            var Prof = (await GrainFactory.GetGrain<IUserProfile>(PlayerID).GetInfo()).Value;

            return new Tuple<uint, string, List<int>>(Prof.Level, Prof.Name, Prof.ActiveItems.Select(kv => kv.Value).ToList());
        }

        Task SendMessage(Guid PlayerId, Func<Guid, Task> SendMessageFunc)
        {
            if (PlayerId != Guid.Empty)
                return SendMessageFunc(PlayerId);

            return Task.CompletedTask;
        }

        Guid ID(Color Color)
        {
            return Color == Color.White ? WhiteId : BlackId;
        }

        protected override async Task StartGame()
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            foreach (var KV in PlayerIdx)
            {
                var Idx = PlayerIdx[KV.Key];
                var OtherID = Idx == Color.White.AsIndex() ? BlackId : WhiteId;
                var Info = await GetInfo(OtherID);

                await SendMessage(KV.Key, ID => EndPoint.SendStartGame(ID, KV.Value, Info.Item1, Info.Item2, Info.Item3, GameConfig.ID));
            }

            Game = new BackgammonGameLogic();

            var Stamp = Game.Stamp;

            Game.RollInitDice(out var Turn, out var White, out var Black);
            await SendMessage(WhiteId, ID => EndPoint.SendInitDiceRolled(ID, White, Black, Stamp));
            await SendMessage(BlackId, ID => EndPoint.SendInitDiceRolled(ID, Black, White, Stamp));

            var TurnId = ID(Turn.Value);
            var OtherId = ID(Turn.Value.Flip());

            Stamp = Game.Stamp;
            await SendMessage(TurnId, ID => EndPoint.SendStartTurn(ID, true, Stamp, Config.BackgammonTurnTime));
            await SendMessage(OtherId, ID => EndPoint.SendStartTurn(ID, false, Stamp, Config.BackgammonTurnTime));

            if (AIColor.HasValue && AIColor == Turn.Value)
                HandleAITurn();

            CurrentTurnEndTime = DateTime.Now.Add(Config.BackgammonTurnTime.Add(Config.BackgammonExtraWaitTimePerTurn));
            TurnTimer = RegisterTimer(OnTurnTimerExpired, null, Config.BackgammonTurnTime.Add(Config.BackgammonExtraWaitTimePerTurn), TimeSpan.MaxValue);
        }

        void HandleAITurn()
        {
            AIMoveTimer?.Dispose();
            AIMoveTimer = RegisterTimer(MakeNextAIMove, null,
                TimeSpan.FromSeconds(Config.BackgammonAITimeBetweenPlaysSeconds * (Config.BackgammonAIPlayTimeVariation + Random.NextDouble() * (1 - Config.BackgammonAIPlayTimeVariation))),
                TimeSpan.MaxValue);
        }

        async Task MakeNextAIMove(object State)
        {
            AIMoveTimer?.Dispose();

            if (Game.State == GameState.WaitDice)
            {
                if (await RollDice_Impl())
                {
#if DEBUG
                    if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"AI has no moves to make {Game}");
#endif
                    await EndTurn_Impl();
                    return;
                }

#if DEBUG
                if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"Starting AI turn {AIMoves.Aggregate("", (s, t) => s + $"({t.Item1},{t.Item2})")} {Game}");
#endif

                AIMoveTimer = RegisterTimer(MakeNextAIMove, null,
                    TimeSpan.FromSeconds(Config.BackgammonAIInitPlayTimeSeconds * (Config.BackgammonAIPlayTimeVariation + Random.NextDouble() * (1 - Config.BackgammonAIPlayTimeVariation))),
                    TimeSpan.MaxValue);

                return;
            }

            if (AIMoves.Count == 0)
                Game.AIMakeAllMoves().ForEach(m => AIMoves.Enqueue(m));

            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            var Turn = Game.Turn;

            var Stamp = Game.Stamp;
            var Move = AIMoves.Dequeue();
            var MoveResult = Game.MakeMove(Move.Item1, Move.Item2, out _);
            await SendMessage(ID(Turn.Flip()), ID => EndPoint.SendOpponentMoved(ID, Move.Item1, Move.Item2, Stamp));

#if DEBUG
            if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"Post AI move {MoveResult} ({Move.Item1},{Move.Item2}) {Game}");
#endif

            if (AIMoves.Count > 0)
                AIMoveTimer = RegisterTimer(MakeNextAIMove, null,
                    TimeSpan.FromSeconds(Config.BackgammonAITimeBetweenPlaysSeconds * (Config.BackgammonAIPlayTimeVariation + Random.NextDouble() * (1 - Config.BackgammonAIPlayTimeVariation))),
                    TimeSpan.MaxValue);
            else if (MoveResult == MakeMoveResult.Win)
                await HandleGameOver(Game.Turn, GameOverReason.GameEndedNormally);
            else
                await EndTurn_Impl();
        }

        async Task OnTurnTimerExpired(object State)
        {
            TurnTimer?.Dispose();
            TurnTimer = null;

            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            var Turn = Game.Turn;
            var TurnIdx = Turn.AsIndex();

            ++InactiveCount[TurnIdx];
            if (InactiveCount[TurnIdx] >= Config.BackgammonNumInactiveTurnsToLose)
            {
                await HandleGameOver(Game.Turn.Flip(), GameOverReason.Inactivity);
                return;
            }

            if (Game.State == GameState.WaitDice)
                if (await RollDice_Impl())
                {
                    await EndTurn_Impl();
                    return;
                }

            while (true)
            {
                var Stamp = Game.Stamp;
                var MoveResult = Game.MakeRandomMove(out sbyte From, out sbyte To);

#if DEBUG
                if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"Post random move {Turn} {MoveResult} ({From},{To}) {Game}");
#endif

                if (!MoveResult.IsSuccess())
                    throw new Exception($"Game failed to make random move on its own resulting in {MoveResult}, something is very wrong");

                if (MoveResult != MakeMoveResult.NoMoveToMake)
                {
                    await SendMessage(ID(Turn), ID => EndPoint.SendForcedOwnMove(ID, From, To, Stamp));
                    await SendMessage(ID(Turn.Flip()), ID => EndPoint.SendOpponentMoved(ID, From, To, Stamp));
                }

                if (MoveResult == MakeMoveResult.Win)
                    await HandleGameOver(Game.Turn, GameOverReason.GameEndedNormally);
                else if (Game.CanEndTurn())
                {
                    await EndTurn_Impl();
                    break;
                }
            }
        }

        async Task HandleGameOver(Color? Winner, GameOverReason reason)
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            ulong Reward = GameConfig.Reward;
            var XP = GameConfig.XP;

            if (WhiteId != Guid.Empty)
            {
                var WhiteEndMatchResult = await GrainFactory.GetGrain<IUserProfile>(WhiteId).OnGameResult(this.AsReference<IBackgammonGame>(), Winner == Color.White, Reward, XP);
                await SendMessage(WhiteId, ID => EndPoint.SendMatchResult(ID, Winner == Color.White, WhiteEndMatchResult, reason));
            }

            if (BlackId != Guid.Empty)
            {
                var BlackEndMatchResult = await GrainFactory.GetGrain<IUserProfile>(BlackId).OnGameResult(this.AsReference<IBackgammonGame>(), Winner == Color.Black, Reward, XP);
                await SendMessage(BlackId, ID => EndPoint.SendMatchResult(ID, Winner == Color.Black, BlackEndMatchResult, reason));
            }

            //?? clear state, once there's any
            DeactivateOnIdle();
        }

        // returns if the turn ended due to no moves
        async Task<bool> RollDice_Impl()
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            var Turn = Game.Turn;
            var MyStamp = Game.Stamp;

            var DiceResult = Game.RollDice(out var FirstDie, out var SecondDie);

#if DEBUG
            if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"Dice rolled {Game.Turn} {DiceResult} ({FirstDie} {SecondDie}) {Game}");
#endif

            if (!DiceResult.IsSuccess())
                throw new Exception("Cannot roll dice, something is very wrong");

            if (FirstDie == 6 && SecondDie == 6)
                GrainFactory.GetGrain<IUserProfile>(ID(Turn)).IncreaseStat(UserStatistics.DoubleSixes).Ignore();

            await SendMessage(ID(Turn), ID => EndPoint.SendDiceRolled(ID, true, FirstDie, SecondDie, MyStamp));
            await SendMessage(ID(Turn.Flip()), ID => EndPoint.SendDiceRolled(ID, false, FirstDie, SecondDie, MyStamp));

            return DiceResult == RollDiceResult.HaveNoMovesToMake;
        }

        public async Task<bool> RollDice(Guid ClientID, byte InitialStamp)
        {
            var Idx = PlayerIdx[ClientID];
            var Turn = Game.Turn;
            var MyStamp = Game.Stamp;

            if (Idx != Turn.AsIndex() || MyStamp != InitialStamp || Game.State != GameState.WaitDice)
                return false;

            await RollDice_Impl();
            return true;
        }

        public async Task<bool> UndoLastMove(Guid ClientID, byte InitialStamp)
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            var Idx = PlayerIdx[ClientID];
            var Turn = Game.Turn;
            var MyStamp = Game.Stamp;

            if (Idx != Turn.AsIndex() || MyStamp != InitialStamp)
                return false;

            var result = Game.UndoLastMove().IsSuccess();

#if DEBUG
            if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"Post undo {Turn} {result} {Game}");
#endif

            if (result)
                await SendMessage(ID(Turn.Flip()), ID => EndPoint.SendOpponentUndo(ID, MyStamp)); //also undo callback on endpoint

            return result;
        }

        public Task<bool> EndTurn(Guid ClientID, byte InitialStamp)
        {
            var Idx = PlayerIdx[ClientID];
            var Turn = Game.Turn;
            var MyStamp = Game.Stamp;

            if (Idx != Turn.AsIndex() || MyStamp != InitialStamp)
                return Task.FromResult(false);

            return EndTurn_Impl();
        }

        async Task<bool> EndTurn_Impl()
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);
            var MyStamp = Game.Stamp;

#if DEBUG
            if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"Ending turn {Game}");
#endif

            if (!Game.EndTurn().IsSuccess())
                return false;

            /* //?? In cases when the player simply can't move, we should really not let
             * them roll. So, implement said logic in BackgammonGameLogic and take it into account
             * here. Meaning, we could start the next turn for the same player whose turn
             * just ended. will need careful consideration of how to handle that and what the
             * interface of GameLogic/network messages will be like.
             */

            var Turn = Game.Turn;

#if DEBUG
            if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"New turn {Turn} {Game}");
#endif

            var NextTurnTime = TimeSpan.FromSeconds(InactiveCount[Turn.AsIndex()] > 0 ? Config.BackgammonPenalizedTurnTime.TotalSeconds : Config.BackgammonTurnTime.TotalSeconds);

            await SendMessage(ID(Turn), ID => EndPoint.SendStartTurn(ID, true, MyStamp, NextTurnTime));
            await SendMessage(ID(Turn.Flip()), ID => EndPoint.SendStartTurn(ID, false, MyStamp, NextTurnTime));

            TurnTimer?.Dispose();

            CurrentTurnEndTime = DateTime.Now.Add(NextTurnTime.Add(Config.BackgammonExtraWaitTimePerTurn));
            TurnTimer = RegisterTimer(OnTurnTimerExpired, null, NextTurnTime.Add(Config.BackgammonExtraWaitTimePerTurn), TimeSpan.MaxValue);

            if (AIColor.HasValue && Turn == AIColor)
                HandleAITurn();

            return true;
        }

        public async Task<byte> MakeMove(Guid ClientID, sbyte[] From, sbyte[] To, byte InitStamp)
        {
            Tuple<bool, byte> Res = new Tuple<bool, byte>(false, 0);

            for (int i = 0; i < From.Length; ++i)
            {
                Res = await MakeMove(ClientID, From[i], To[i], i == 0 ? InitStamp : default(byte?));
                if (!Res.Item1)
                    return Res.Item2;
            }

            return Res.Item2;
        }

        async Task<Tuple<bool, byte>> MakeMove(Guid ClientID, sbyte From, sbyte To, byte? Stamp)
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            var Idx = PlayerIdx[ClientID];
            var Turn = Game.Turn;
            if (Idx != Turn.AsIndex())
                return new Tuple<bool, byte>(false, Game.Stamp);

            InactiveCount[Idx] = 0;

            if (Stamp.HasValue && Stamp != Game.Stamp)
                return new Tuple<bool, byte>(false, Game.Stamp);

            var MyStamp = Game.Stamp;
            var MoveResult = Game.MakeMove(From, To, out var CheckerTaken);
            var Result = new Tuple<bool, byte>(MoveResult.IsSuccess(), Game.Stamp);

            if (CheckerTaken)
                GrainFactory.GetGrain<IUserProfile>(ID(Turn)).IncreaseStat(UserStatistics.CheckersHit).Ignore();

#if DEBUG
            if (Logger.IsEnabled(LogLevel.Information)) Logger.Info($"Post move {Turn} {Result} ({From},{To}) {Game}");
#endif

            if (MoveResult.IsSuccess())
                await SendMessage(ID(Turn.Flip()), ID => EndPoint.SendOpponentMoved(ID, From, To, MyStamp));

            if (MoveResult == MakeMoveResult.Win)
            {
                if (Game.IsGammon())
                    GrainFactory.GetGrain<IUserProfile>(ID(Turn)).IncreaseStat(UserStatistics.Gammons).Ignore();
                await HandleGameOver(Game.Turn, GameOverReason.GameEndedNormally);
            }

            return Result;
        }

        public async Task<Immutable<BackgammonGameState>> GetGameState(Guid ClientID)
        {
            if (Game == null)
                throw new Exception("Game not in progress");

            //if (Game == null)
            //{
            //    if (Ready == null || PlayerIdx == null || PlayerIdx.Count == 0)
            //        throw new Exception("Game not in progress");

            //    await SetReady(ClientID);
            //}

            var Idx = PlayerIdx[ClientID];
            var OtherID = Idx == Color.White.AsIndex() ? BlackId : WhiteId;
            var Info = await GetInfo(OtherID);

            if (Game == null)
            {
                return new BackgammonGameState
                {
                    Game = null,
                    PlayerColorIdx = Idx,
                    CurrentTurnRemainingTime = TimeSpan.Zero,
                    OpponentLevel = Info.Item1,
                    OpponentName = Info.Item2,
                    OpponentItems = Info.Item3,
                    GameID = GameConfig.ID
                }.AsImmutable();
            }

            return new BackgammonGameState
            {
                Game = Game,
                PlayerColorIdx = Idx,
                CurrentTurnRemainingTime = CurrentTurnEndTime - DateTime.Now - Config.BackgammonExtraWaitTimePerTurn,
                OpponentLevel = Info.Item1,
                OpponentName = Info.Item2,
                OpponentItems = Info.Item3,
                GameID = GameConfig.ID
            }.AsImmutable();
        }

        public Task Emote(Guid ClientID, int EmoteID)
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            foreach (var ID in PlayerIdx.Keys)
                if (ID != ClientID)
                    SendMessage(ID, _ID => EndPoint.SendEmote(_ID, EmoteID)).Ignore();

            return Task.CompletedTask;
        }

        public Task Concede(Guid ClientID)
        {
            var EndPoint = GrainFactory.GetGrain<IBackgammonEndPoint>(0);

            var Idx = PlayerIdx[ClientID];
            var MyColor = Idx == Color.White.AsIndex() ? Color.White : Color.Black;
            var EnemyColor = MyColor.Flip();
            if (Game.BorneOff[Idx] == 0)
                GrainFactory.GetGrain<IUserProfile>(ID(EnemyColor)).IncreaseStat(UserStatistics.Gammons).Ignore();

            return HandleGameOver(EnemyColor, GameOverReason.Concede);
        }
    }
}
