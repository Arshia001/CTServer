using BackgammonLogic;
using CTGrainInterfaces;
using LightMessage.Common.Messages;
using LightMessage.OrleansUtils.GrainInterfaces;
using LightMessage.OrleansUtils.Grains;
using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    [StatelessWorker(128), EndPointName("bkgm")]
    public class BackgammonEndPoint : EndPointGrain, IBackgammonEndPoint
    {
        /* 
         * Params:
         * 0 -> Array:
         *     Array:
         *         0 -> From int
         *         1 -> To int
         * 1 -> To int
         * 
         * Returns uint (MakeMoveResult)
         */
        [MethodName("move")]
        public async Task<EndPointFunctionResult> MakeMove(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            var Array = Params.Args[0].AsArray;
            var From = new sbyte[Array.Count];
            var To = new sbyte[Array.Count];

            for (int i = 0; i < Array.Count; ++i)
            {
                From[i] = (sbyte)Array[i].AsArray[0].AsInt.Value;
                To[i] = (sbyte)Array[i].AsArray[1].AsInt.Value;
            }

            var Stamp = (byte)Params.Args[1].AsUInt.Value;

            var Res = await Game.MakeMove(Params.ClientID, From, To, Stamp);

            return Success(Param.UInt(Res));
        }

        [MethodName("dice")]
        public async Task<EndPointFunctionResult> RollDice(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            var Stamp = (byte)Params.Args[0].AsUInt.Value;

            var Res = await Game.RollDice(Params.ClientID, Stamp);

            return Success(Param.Boolean(Res));
        }

        [MethodName("undo")]
        public async Task<EndPointFunctionResult> UndoLastMove(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            var Stamp = (byte)Params.Args[0].AsUInt.Value;

            var Res = await Game.UndoLastMove(Params.ClientID, Stamp);

            return Success(Param.Boolean(Res));
        }

        [MethodName("turn")]
        public async Task<EndPointFunctionResult> EndTurn(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            var Stamp = (byte)Params.Args[0].AsUInt.Value;

            var Res = await Game.EndTurn(Params.ClientID, Stamp);

            return Success(Param.Boolean(Res));
        }

        /* 
         * Params:
         * empty
         * 
         * Returns:
         * uint Turn.AsIndex
         * uint State
         * int[] Board
         * uint[] Bar
         * uint[] BorneOff
         * uint[] RolledDice
         * uint[] RemainingDice
         * uint MyColor.AsIndex
         * TimeSpan RemainingTime
         * int[] OpponentCustomizations
         */
        [MethodName("state")]
        public async Task<EndPointFunctionResult> GetGameState(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            try
            {
                var Res = (await Game.GetGameState(Params.ClientID)).Value;

                return Success(
                    Res.Game == null ? Param.Null() : Param.Array(
                        Param.UInt(Res.Game.Turn.AsIndex()),
                        Param.UInt((uint)Res.Game.State),
                        Param.Array(Res.Game.Board.Select(s => Param.Int(s))),
                        Param.Array(Res.Game.Bar.Select(s => Param.UInt(s))),
                        Param.Array(Res.Game.BorneOff.Select(s => Param.UInt(s))),
                        Param.Array(Res.Game.RolledDice.Select(s => Param.UInt(s))),
                        Param.Array(Res.Game.RemainingDice.Select(s => Param.UInt(s))),
                        Param.UInt(Res.Game.Stamp),
                        Param.Array(Res.Game.CurrentTurnMoves.Select(m => Param.Array(Param.Int(m.From), Param.Int(m.To), Param.Boolean(m.PieceTaken), Param.UInt(m.Die))))
                    ),
                    Param.UInt(Res.PlayerColorIdx),
                    Param.TimeSpan(Res.CurrentTurnRemainingTime),
                    Param.Array(Param.UInt(Res.OpponentLevel), Param.String(Res.OpponentName), Param.Array(Res.OpponentItems.Select(i => Param.Int(i)))),
                    Param.Int(Res.GameID)
                    );
            }
            catch (Exception Ex)
            {
                return Failure(Ex.Message);
            }
        }

        [MethodName("opp")]
        public async Task<EndPointFunctionResult> GetOpponentInfo(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            try
            {
                var Res = (await Game.GetGameState(Params.ClientID)).Value;

                return Success(Param.UInt(Res.OpponentLevel), Param.String(Res.OpponentName), Param.Array(Res.OpponentItems.Select(i => Param.Int(i))));
            }
            catch (Exception Ex)
            {
                return Failure(Ex.Message);
            }
        }

        /* 
         * Params:
         * 0 -> EmoteID int
         * 
         * No result
         */
        [MethodName("emote")]
        public async Task<EndPointFunctionResult> Emote(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            Game.Emote(Params.ClientID, (int)Params.Args[0].AsInt.Value).Ignore();

            return NoResult();
        }

        /* 
         * Params:
         * empty
         * 
         * No result
         */
        [MethodName("concede")]
        public async Task<EndPointFunctionResult> Concede(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame() as IBackgammonGame;
            if (Game == null)
                return Failure("Client is not in a backgammon game");

            Game.Concede(Params.ClientID).Ignore();

            return NoResult();
        }


        public override Task OnDisconnect(Guid ClientID) // No need to notify game, it handles disconnects and idlers the same way
        {
            return Task.CompletedTask;
        }


        public Task SendStartGame(Guid ClientID, byte MyColor, uint OpponentLevel, string OpponentName, IEnumerable<int> OpponentItems, int GameID)
        {
            return SendMessage(ClientID,
                "start",
                Param.UInt(MyColor),
                Param.Array(Param.UInt(OpponentLevel), Param.String(OpponentName), Param.Array(OpponentItems.Select(i => Param.Int(i)))),
                Param.Int(GameID)
                );
        }

        public Task SendInitDiceRolled(Guid ClientID, byte My, byte Their, byte Stamp)
        {
            return SendMessage(ClientID,
                "initdice",
                Param.UInt(My),
                Param.UInt(Their),
                Param.UInt(Stamp)
                );
        }

        public Task SendStartTurn(Guid ClientID, bool My, byte Stamp, TimeSpan TurnTime)
        {
            return SendMessage(ClientID,
                "turn",
                Param.Boolean(My),
                Param.UInt(Stamp),
                Param.TimeSpan(TurnTime)
                );
        }

        public Task SendDiceRolled(Guid ClientID, bool My, byte Roll1, byte Roll2, byte Stamp)
        {
            return SendMessage(ClientID,
                "dice",
                Param.Boolean(My),
                Param.UInt(Roll1),
                Param.UInt(Roll2),
                Param.UInt(Stamp)
                );
        }

        public Task SendOpponentUndo(Guid ClientID, byte Stamp)
        {
            return SendMessage(ClientID,
                "undo",
                Param.UInt(Stamp)
                );
        }

        public Task SendOpponentMoved(Guid ClientID, sbyte From, sbyte To, byte Stamp)
        {
            return SendMessage(ClientID,
                "opmove",
                Param.Int(From),
                Param.Int(To),
                Param.UInt(Stamp)
                );
        }

        public Task SendForcedOwnMove(Guid ClientID, sbyte From, sbyte To, byte Stamp)
        {
            return SendMessage(ClientID,
                "forceme",
                Param.Int(From),
                Param.Int(To),
                Param.UInt(Stamp)
                );
        }

        public Task SendMatchResult(Guid ClientID, bool MyWin, EndMatchResults Rewards, GameOverReason reason)
        {
            return SendMessage(ClientID,
                "result",
                Param.Boolean(MyWin),
                Param.UInt((ulong)reason),
                Param.UInt(Rewards.TotalFunds[CurrencyType.Gold.AsIndex()]),
                Param.UInt(Rewards.TotalFunds[CurrencyType.Gem.AsIndex()]),
                Param.UInt(Rewards.TotalXP),
                Param.UInt(Rewards.Level),
                Param.UInt(Rewards.LevelUpDeltaFunds[CurrencyType.Gold.AsIndex()]),
                Param.UInt(Rewards.LevelUpDeltaFunds[CurrencyType.Gem.AsIndex()]),
                Param.UInt(Rewards.LevelXP),
                Param.Array(Rewards.UpdatedStatistics.Select(u => Param.UInt(u)))
                );
        }

        public Task SendEmote(Guid ClientID, int EmoteID)
        {
            return SendMessage(ClientID,
                "emote",
                Param.Int(EmoteID)
                );
        }
    }
}
