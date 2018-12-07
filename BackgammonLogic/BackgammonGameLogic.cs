#if UNITY_5_3_OR_NEWER || DEBUG
#define CLIENT
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BackgammonLogic
{
    public struct Move
    {
        public readonly sbyte From;
        public readonly sbyte To;
        public readonly bool PieceTaken;
        public readonly byte Die;

        public Move(sbyte From, sbyte To, bool PieceTaken, byte Die)
        {
            this.From = From;
            this.To = To;
            this.PieceTaken = PieceTaken;
            this.Die = Die;
        }
    }


    public class BackgammonGameLogic
    {
        public IEnumerable<byte> RolledDice { get { return rolledDice; } }
        public IEnumerable<byte> RemainingDice { get { return remainingDice; } }
        public IEnumerable<Move> CurrentTurnMoves { get { return currentTurnMoves; } }


        public Color Turn { get; private set; }
        public GameState State { get; private set; }
        public byte Stamp { get; private set; } = 0;

        // The points start from black's ace and continue until white's ace.
        // That is, white moves forward in the indices, while black moves back.
        // The presence or absence of checkers is specified using numbers. Zero
        // is an empty point, positive numbers are whites, negatives are blacks.
        public sbyte[] Board = new sbyte[24];

        // Number of checkers on the bar for each player
        public byte[] Bar = new byte[2];

        // Number of borne off checkers for each player
        public byte[] BorneOff = new byte[2];

        List<byte> rolledDice = new List<byte>(4);
        Multiset<byte> remainingDice = new Multiset<byte>(2);

        Stack<Move> currentTurnMoves = new Stack<Move>(4);

        Random Random = new Random();

        int BarMultiplier = 5000;
        int BlotMultiplier = 400;
        int BlockedPointMultiplier = 200;
        int BorneOffMultiplier = 6000;
        int UnreachableBlotReductionFactor = 2;
        int CheckerDistanceFactor = 1;


        public BackgammonGameLogic()
        {
            Board[0] = 2;
            Board[5] = -5;
            Board[7] = -3;
            Board[11] = 5;
            Board[12] = -5;
            Board[16] = 3;
            Board[18] = 5;
            Board[23] = -2;

            State = GameState.Init;

            BarMultiplier = (int)(Random.NextDouble() * 3000) + 2000;
            BlotMultiplier = (int)(Random.NextDouble() * 400) + 200;
            BlockedPointMultiplier = (int)(Random.NextDouble() * 400) + 200;
            BorneOffMultiplier = (int)(Random.NextDouble() * 3000) + 2000;
        }

        BackgammonGameLogic(BackgammonGameLogic Other)
        {
            RestoreState(Other);
        }

        public void RestoreGameState(Color Turn, GameState State, sbyte[] Board, byte[] Bar, byte[] BorneOff, IEnumerable<byte> Rolled, IEnumerable<byte> Remaining, byte Stamp, IEnumerable<Move> currentTurnMoves)
        {
            var _Rolled = new List<byte>(Rolled);
            var _Remaining = new Multiset<byte>(Remaining);

            var WhiteIdx = Color.White.AsIndex();
            var BlackIdx = Color.Black.AsIndex();

            if (Board.Length != 24 ||
                Bar.Length != 2 ||
                BorneOff.Length != 2 ||
                Board.Sum(b => Math.Sign(b) == Color.White.AsSign() ? Math.Abs(b) : 0) + BorneOff[WhiteIdx] + Bar[WhiteIdx] != 15 ||
                Board.Sum(b => Math.Sign(b) == Color.Black.AsSign() ? Math.Abs(b) : 0) + BorneOff[BlackIdx] + Bar[BlackIdx] != 15 ||
                (_Rolled.Count != 2 && _Rolled.Count != 4) ||
                (_Rolled.Count == 2 && _Rolled[0] == _Rolled[1]) ||
                (_Rolled.Count == 4 && _Rolled.Any(b => b != _Rolled[0])) ||
                _Rolled.Any(b => b <= 0 || b > 6) ||
                _Remaining.Count > _Rolled.Count ||
                _Remaining.Any(b => !Rolled.Contains(b)) ||
                currentTurnMoves.Any(m => !Rolled.Contains(m.Die))) // could probably add a hundred more checks to the move stack, but we should be OK with just this check
                throw new ArgumentException("Invalid data passed in to RestoreGameState");

            RestoreGameState_Impl(Turn, State, Board, Bar, BorneOff, _Rolled, _Remaining, Stamp, currentTurnMoves);
        }

        void RestoreGameState_Impl(Color Turn, GameState State, sbyte[] Board, byte[] Bar, byte[] BorneOff, List<byte> Rolled, Multiset<byte> Remaining, byte Stamp, IEnumerable<Move> currentTurnMoves)
        {
            this.Turn = Turn;
            this.State = State;
            Array.Copy(Board, this.Board, Board.Length);
            Array.Copy(Bar, this.Bar, Bar.Length);
            Array.Copy(BorneOff, this.BorneOff, BorneOff.Length);
            this.rolledDice = Rolled;
            this.remainingDice = Remaining;
            this.Stamp = Stamp;
            this.currentTurnMoves = new Stack<Move>(currentTurnMoves);
        }

        void RestoreState(BackgammonGameLogic Other)
        {
            RestoreGameState_Impl(Other.Turn, Other.State, Other.Board, Other.Bar, Other.BorneOff, new List<byte>(Other.rolledDice), new Multiset<byte>(Other.remainingDice), Other.Stamp, Other.currentTurnMoves);
        }

        void IncrementStamp()
        {
            Stamp = (byte)((Stamp + 1) % 256);
        }

        byte Roll()
        {
            return (byte)(Random.Next(6) + 1);
        }

        bool RegisterInitDice(byte White, byte Black)
        {
            if (State != GameState.Init)
                return false;

            if (White > Black)
                Turn = Color.White;
            else if (Black > White)
                Turn = Color.Black;
            else
                return false;

            RegisterDice(White, Black);
            State = GameState.WaitMove;

            IncrementStamp();

            return true;
        }

        public void RollInitDice(out Color? Turn, out byte White, out byte Black)
        {
            White = Black = 0;

            while (White == Black)
            {
                White = Roll();
                Black = Roll();
            }

            if (!RegisterInitDice(White, Black))
            {
                Turn = null;
                White = 0;
                Black = 0;
                return;
            }

            Turn = this.Turn;
        }

        void RegisterDice(byte First, byte Second)
        {
            remainingDice.Clear();
            rolledDice.Clear();

            if (First == Second)
            {
                remainingDice.Add(First, 4);

                rolledDice.Add(First);
                rolledDice.Add(First);
                rolledDice.Add(First);
                rolledDice.Add(First);
            }
            else
            {
                remainingDice.Add(First);
                remainingDice.Add(Second);

                rolledDice.Add(First);
                rolledDice.Add(Second);
            }

            IncrementStamp();
        }

        Multiset<byte> GetBestPlay(Multiset<byte> D1, Multiset<byte> D2)
        {
            if (D1.Count > D2.Count)
                return D1;
            else if (D2.Count > D1.Count)
                return D2;
            else
                return D1.Unique.First() > D2.Unique.First() ? D1 : D2;
        }

        Multiset<byte> getPossibleDiceToPlay(int FromIndex)
        {
            Debug.Assert(0 <= FromIndex && FromIndex <= 23);

            var Best = new Multiset<byte>();

            if (remainingDice.Count == 0)
                return Best;

            var TurnSign = Turn.AsSign();
            var TurnIndex = Turn.AsIndex();

            if (Bar[TurnIndex] > 0)
            {
                var From = Turn == Color.White ? -1 : 24;
                foreach (var D in remainingDice.Unique.ToList())
                {
                    var To = From + D * TurnSign;
                    var ToVal = Board[To];
                    if (Math.Sign(ToVal) != TurnSign && Math.Abs(ToVal) > 1)
                        continue;

                    remainingDice.Remove(D);
                    --Bar[TurnIndex];
                    Board[To] = TurnSign;

                    var Rest = getPossibleDiceToPlay(FromIndex);

                    Rest.Add(D);
                    Best = GetBestPlay(Rest, Best);

                    Board[To] = ToVal;
                    ++Bar[TurnIndex];
                    remainingDice.Add(D);

                    if (Best.Count == remainingDice.Count)
                        return Best;
                }
            }
            else
            {
                for (int i = FromIndex; i < 24; ++i)
                {
                    var From = Turn == Color.White ? i : 23 - i;
                    var FromVal = Board[From];
                    if (Math.Sign(FromVal) != TurnSign)
                        continue;

                    // We don't need to check a die multiple times even if there's more than one of it, subsequent calls will handle the rest
                    foreach (var D in remainingDice.Unique.ToList())
                    {
                        var To = From + D * TurnSign;
                        if (To < 0 || To > 23)
                        {
                            if (EligibleForBearOff())
                            {
                                remainingDice.Remove(D);
                                Board[From] -= TurnSign;
                                ++BorneOff[TurnIndex];

                                var Rest = getPossibleDiceToPlay(i);

                                Rest.Add(D);
                                Best = GetBestPlay(Rest, Best);

                                --BorneOff[TurnIndex];
                                Board[From] += TurnSign;
                                remainingDice.Add(D);

                                if (Best.Count == remainingDice.Count)
                                    return Best;
                            }
                            else
                                continue;
                        }
                        else
                        {
                            var ToVal = Board[To];
                            if (Math.Sign(ToVal) != TurnSign && Math.Abs(ToVal) > 1)
                                continue;

                            remainingDice.Remove(D);
                            Board[From] -= TurnSign;
                            Board[To] = Board[To] == -TurnSign ? TurnSign : (sbyte)(Board[To] + TurnSign);

                            var Rest = getPossibleDiceToPlay(i);

                            Rest.Add(D);
                            Best = GetBestPlay(Rest, Best);

                            Board[To] = ToVal;
                            Board[From] += TurnSign;
                            remainingDice.Add(D);

                            if (Best.Count == remainingDice.Count)
                                return Best;
                        }
                    }
                }
            }

            return Best;
        }

        bool EligibleForBearOff()
        {
            var TurnSign = Turn.AsSign();
            var NumInHome = Math.Abs((Turn == Color.White ? Board.Skip(18) : Board.Take(6)).Sum(s => Math.Sign(s) == TurnSign ? s : 0));
            return NumInHome + BorneOff[Turn.AsIndex()] == 15;
        }

        RollDiceResult ProcessDiceRoll(byte First, byte Second)
        {
            if (State != GameState.WaitDice)
                return RollDiceResult.Error_CannotRollInThisState;

            State = GameState.WaitMove;

            RegisterDice(First, Second);
            remainingDice = getPossibleDiceToPlay(0);

            if (remainingDice.Count == 0)
                return RollDiceResult.HaveNoMovesToMake;

            return RollDiceResult.Success;
        }

        public RollDiceResult RollDice(out byte First, out byte Second)
        {
            First = Roll();
            Second = Roll();

            var Result = ProcessDiceRoll(First, Second);

            if (!Result.IsSuccess())
                First = Second = 0;

            return Result;
        }

        public bool CanEndTurn()
        {
            return State == GameState.WaitMove && remainingDice.Count == 0;
        }

        public EndTurnResult EndTurn()
        {
            if (State != GameState.WaitMove)
                return EndTurnResult.Error_CannotEndTurnInThisState;

            if (remainingDice.Count > 0)
                return EndTurnResult.Error_HaveRemainingDiceToPlay;

            Turn = Turn.Flip();
            currentTurnMoves.Clear();
            State = GameState.WaitDice;

            IncrementStamp();

            return EndTurnResult.Success;
        }

        public bool CanUndo() => currentTurnMoves.Count > 0;

        public UndoResult UndoLastMove()
        {
            if (State != GameState.WaitMove)
                return UndoResultType.Error_CannotUndoInThisState;

            if (currentTurnMoves.Count == 0)
                return UndoResultType.Error_HaveNoMovesToUndo;

            var moveToUndo = currentTurnMoves.Pop();
            var currentTurnIndex = Turn.AsIndex();
            var currentTurnSign = Turn.AsSign();

            if (moveToUndo.From == -1)
                ++Bar[currentTurnIndex];
            else
                Board[moveToUndo.From] += currentTurnSign;

            if (moveToUndo.To == -1)
                --BorneOff[currentTurnIndex];
            else
                Board[moveToUndo.To] -= currentTurnSign;

            if (moveToUndo.PieceTaken)
            {
                --Bar[Turn.Flip().AsIndex()];
                Board[moveToUndo.To] = Turn.Flip().AsSign();
            }

            remainingDice.Add(moveToUndo.Die);

            IncrementStamp();

            return new UndoResult(UndoResultType.Success, moveToUndo);
        }

        MakeMoveResult MakeMove_Impl(sbyte From, sbyte To, bool DryRun, out bool CheckerTaken)
        {
            CheckerTaken = false;

            if (State != GameState.WaitMove)
                return MakeMoveResult.Error_CannotMoveInThisState;

            var TurnSign = Turn.AsSign();
            var TurnIndex = Turn.AsIndex();

            if (To == -1 && From == -1)
                return MakeMoveResult.Error_CannotEnterAndBearOffAtSameTime;

            if (From < -1 || From > 23)
                return MakeMoveResult.Error_InvalidFrom;
            if (From >= 0 && Math.Sign(Board[From]) != TurnSign)
                return MakeMoveResult.Error_NoCheckerAtPoint;

            if (From == -1 && Bar[TurnIndex] == 0)
                return MakeMoveResult.Error_NoCheckerOnBar;
            if (From != -1 && Bar[TurnIndex] > 0)
                return MakeMoveResult.Error_MustEnterFirst;

            if (To < -1 || To > 23)
                return MakeMoveResult.Error_InvalidTo;
            if (To >= 0 && Math.Sign(Board[To]) != TurnSign && Math.Abs(Board[To]) >= 2)
                return MakeMoveResult.Error_PointBlockedByOpponent;

            byte Delta;

            if (To == -1)
            {
                if (!EligibleForBearOff())
                    return MakeMoveResult.Error_CannotBearOffWhenCheckersOutsideHome;

                if (Turn == Color.White)
                    To = 24;

                if (Math.Sign(To - From) != TurnSign)
                    return MakeMoveResult.Error_CannotMoveBackwards;

                Delta = (byte)Math.Abs(From - To);

                if (!remainingDice.Contains(Delta))
                {
                    var Max = remainingDice.Unique.Max();
                    if (Delta > Max)
                        return MakeMoveResult.Error_HaveNoSuchDie;

                    var FromPoint = From - TurnSign;
                    var Until = Turn == Color.Black ? 6 : 17;
                    for (int i = FromPoint; i != Until; i -= TurnSign)
                        if (Math.Sign(Board[i]) == TurnSign)
                            return MakeMoveResult.Error_CannotBearOffAsCheckersBehindCurrent;

                    Delta = Max;
                }

                remainingDice.Remove(Delta);
                Board[From] -= TurnSign;
                ++BorneOff[TurnIndex];

                bool CanMakeMove = getPossibleDiceToPlay(0).Count == remainingDice.Count || BorneOff[TurnIndex] == 15;
                if (!CanMakeMove || DryRun)
                {
                    remainingDice.Add(Delta);
                    Board[From] += TurnSign;
                    --BorneOff[TurnIndex];
                    return CanMakeMove ? MakeMoveResult.Success : MakeMoveResult.Error_MoveCausesUnusableDice;
                }

                if (BorneOff[TurnIndex] == 15)
                {
                    State = GameState.Complete;
                    return MakeMoveResult.Win;
                }
            }
            else
            {
                if (From == -1 && Turn == Color.Black)
                    From = 24;

                if (Math.Sign(To - From) != TurnSign)
                    return MakeMoveResult.Error_CannotMoveBackwards;

                Delta = (byte)Math.Abs(From - To);

                if (!remainingDice.Contains(Delta))
                    return MakeMoveResult.Error_HaveNoSuchDie;

                var RevTurnIndex = Turn.Flip().AsIndex();

                remainingDice.Remove(Delta);

                var BarVal = Bar[TurnIndex];
                var RevBarVal = Bar[RevTurnIndex];
                var ToVal = Board[To];

                sbyte FromVal = 0;
                if (From >= 0 && From <= 23)
                    FromVal = Board[From];

                if (From == -1 || From == 24)
                    --Bar[TurnIndex];
                else
                    Board[From] -= TurnSign;

                if (Board[To] == -TurnSign)
                {
                    CheckerTaken = true;
                    Board[To] = TurnSign;
                    ++Bar[RevTurnIndex];
                }
                else
                    Board[To] += TurnSign;

                bool CanMakeMove = getPossibleDiceToPlay(0).Count == remainingDice.Count;
                if (!CanMakeMove || DryRun)
                {
                    Bar[TurnIndex] = BarVal;
                    Bar[RevTurnIndex] = RevBarVal;
                    if (From >= 0 && From <= 23)
                        Board[From] = FromVal;
                    Board[To] = ToVal;

                    remainingDice.Add(Delta);

                    return CanMakeMove ? MakeMoveResult.Success : MakeMoveResult.Error_MoveCausesUnusableDice;
                }
            }

            IncrementStamp();

            currentTurnMoves.Push(new Move(From == 24 ? (sbyte)-1 : From, To == 24 ? (sbyte)-1 : To, CheckerTaken, Delta));

            return MakeMoveResult.Success;
        }

        public bool IsGammon()
        {
            return State == GameState.Complete && BorneOff.Any(b => b == 0);
        }

        static void GetAllPossibleStates(BackgammonGameLogic BaseState, List<Tuple<sbyte, sbyte>> CurrentMoves, List<Tuple<BackgammonGameLogic, List<Tuple<sbyte, sbyte>>>> Results)
        {
            var StateCache = new BackgammonGameLogic(BaseState);
            var TurnIndex = StateCache.Turn.AsIndex();
            var TurnSign = StateCache.Turn.AsSign();
            bool Taken;

            if (StateCache.Bar[TurnIndex] > 0)
            {
                foreach (var D in StateCache.remainingDice.Unique.ToList())
                {
                    var To = (sbyte)((StateCache.Turn == Color.White ? -1 : 24) + D * TurnSign);
                    var MoveResult = StateCache.MakeMove_Impl(-1, To, false, out Taken);
                    if (MoveResult.IsSuccess())
                    {
                        var Moves = new List<Tuple<sbyte, sbyte>>(CurrentMoves);
                        Moves.Add(new Tuple<sbyte, sbyte>(-1, To));

                        if (MoveResult == MakeMoveResult.Success && !StateCache.CanEndTurn())
                            GetAllPossibleStates(StateCache, Moves, Results);
                        else
                            Results.Add(new Tuple<BackgammonGameLogic, List<Tuple<sbyte, sbyte>>>(StateCache, Moves));

                        StateCache = new BackgammonGameLogic(BaseState);
                    }
                }
            }
            else
            {
                for (sbyte i = 0; i < 24; ++i)
                {
                    if (Math.Sign(StateCache.Board[i]) == TurnSign)
                        foreach (var D in StateCache.remainingDice.Unique.ToList())
                        {
                            var To = (sbyte)(i + D * TurnSign);
                            if (To < -1 || To > 23)
                                To = -1;
                            var MoveResult = StateCache.MakeMove_Impl((sbyte)i, To, false, out Taken);
                            if (MoveResult.IsSuccess())
                            {
                                var Moves = new List<Tuple<sbyte, sbyte>>(CurrentMoves);
                                Moves.Add(new Tuple<sbyte, sbyte>(i, To));

                                if (MoveResult == MakeMoveResult.Success && !StateCache.CanEndTurn())
                                    GetAllPossibleStates(StateCache, Moves, Results);
                                else
                                    Results.Add(new Tuple<BackgammonGameLogic, List<Tuple<sbyte, sbyte>>>(StateCache, Moves));

                                StateCache = new BackgammonGameLogic(BaseState);
                            }
                        }
                }
            }
        }

        public List<Tuple<sbyte, sbyte>> AIMakeAllMoves()
        {
            if (State != GameState.WaitMove)
                return null;

            var AllStates = new List<Tuple<BackgammonGameLogic, List<Tuple<sbyte, sbyte>>>>();
            GetAllPossibleStates(this, new List<Tuple<sbyte, sbyte>>(), AllStates);

            int BestScore = int.MinValue;
            var BestPlay = default(List<Tuple<sbyte, sbyte>>);
            foreach (var T in AllStates)
            {
                var Score = T.Item1.GetScore(Turn);
                if (Score > BestScore)
                {
                    BestScore = Score;
                    BestPlay = T.Item2;
                }
            }

            return BestPlay;
        }

        int GetScore(Color Turn)
        {

            var Result = 0;

            var TurnSign = Turn.AsSign();
            var RevTurnSign = Turn.Flip().AsSign();

            Result += Bar[Turn.Flip().AsIndex()] * BarMultiplier;
            Result += BorneOff[Turn.AsIndex()] * BorneOffMultiplier;

            var Range = Enumerable.Range(0, 24);
            if (Turn == Color.Black)
                Range = Range.Reverse();

            bool HaveCheckersBehindEnemyLines = false, SeenOwnCheckers = false;
            sbyte LastOpponentCheckerIndex = -1;
            if (Bar[Turn.Flip().AsIndex()] > 0)
            {
                HaveCheckersBehindEnemyLines = true;
                LastOpponentCheckerIndex = Turn == Color.White ? (sbyte)24 : (sbyte)-1;
            }
            else
            {
                foreach (var i in Range)
                {
                    if (Math.Sign(Board[i]) == RevTurnSign)
                    {
                        LastOpponentCheckerIndex = (sbyte)i;
                        if (SeenOwnCheckers)
                            HaveCheckersBehindEnemyLines = true;
                    }
                    else if (Math.Sign(Board[i]) == TurnSign)
                        SeenOwnCheckers = true;
                }
            }

            for (int i = 0; i < 24; ++i)
            {
                if (Math.Sign(Board[i]) == TurnSign)
                {
                    var Dist = Turn == Color.White ? i : 23 - i;
                    Result -= (int)Math.Pow((23 - Dist), 2) * Math.Abs(Board[i]) * CheckerDistanceFactor;

                    if (Math.Abs(Board[i]) == 1)
                    {
                        if (Math.Sign(LastOpponentCheckerIndex - i) == TurnSign)
                            Result -= BlotMultiplier * Dist;
                        else if (HaveCheckersBehindEnemyLines)
                            Result -= BlotMultiplier * Dist / UnreachableBlotReductionFactor;
                    }
                    else
                    {
                        if (HaveCheckersBehindEnemyLines)
                            Result += BlockedPointMultiplier * Dist;
                    }
                }
            }

            return Result;
        }

        public MakeMoveResult MakeRandomMove(out sbyte From, out sbyte To)
        {
            From = To = 0;

            if (State != GameState.WaitMove)
                return MakeMoveResult.Error_CannotMoveInThisState;

            if (remainingDice.Count == 0)
                return MakeMoveResult.NoMoveToMake;

            var TurnIndex = Turn.AsIndex();
            var TurnSign = Turn.AsSign();
            bool Taken;

            if (Bar[TurnIndex] > 0)
            {
                foreach (var D in remainingDice.Unique.ToList())
                {
                    To = (sbyte)((Turn == Color.White ? -1 : 24) + D * TurnSign);
                    var MoveResult = MakeMove_Impl(-1, To, false, out Taken);
                    if (MoveResult.IsSuccess())
                    {
                        From = -1;
                        return MoveResult;
                    }
                }
            }

            var Range = Enumerable.Range(0, 24);
            if (Turn == Color.Black)
                Range = Range.Reverse();
            foreach (var i in Range)
                if (Math.Sign(Board[i]) == TurnSign)
                    foreach (var D in remainingDice.Unique.ToList())
                    {
                        To = (sbyte)(i + D * TurnSign);
                        if (To < 0 || To > 23)
                            To = -1;
                        var MoveResult = MakeMove_Impl((sbyte)i, To, false, out Taken);
                        if (MoveResult.IsSuccess())
                        {
                            From = (sbyte)i;
                            return MoveResult;
                        }
                    }

            return MakeMoveResult.Error_Interal_FoundNoValidMoves;
        }

        public MakeMoveResult MakeMove(sbyte From, sbyte To, out bool CheckerTaken)
        {
            return MakeMove_Impl(From, To, false, out CheckerTaken);
        }

#if CLIENT
        public RollDiceResult ServerDiceRolled(byte First, byte Second)
        {
            return ProcessDiceRoll(First, Second);
        }

        public bool ServerInitDiceRolled(byte White, byte Black)
        {
            return RegisterInitDice(White, Black);
        }

        bool GetPossibleMoves_Impl_Int(sbyte From, Dictionary<sbyte, List<List<sbyte>>> Results, HashSet<sbyte> TakePositions)
        {
            var TurnIndex = Turn.AsIndex();
            var TurnSign = Turn.AsSign();
            bool Taken;

            if (From == -1)
            {
                if (Bar[TurnIndex] <= 0)
                    return false;
            }
            else if (From < 0 || From > 23 || Math.Sign(Board[From]) != TurnSign)
                return false;

            var MyState = new BackgammonGameLogic(this);

            foreach (var D in remainingDice.Unique.ToList())
            {
                sbyte To;
                if (From == -1)
                    To = (sbyte)((Turn == Color.White ? -1 : 24) + D * TurnSign);
                else
                {
                    To = (sbyte)(From + D * TurnSign);
                    if (To < 0 || To > 23)
                        To = -1;
                }

                if (MakeMove_Impl(From, To, false, out Taken).IsSuccess())
                {
                    if (Taken)
                        TakePositions.Add(To);

                    List<sbyte> MoveList;
                    if (Results.ContainsKey(From))
                        MoveList = new List<sbyte>(Results[From][0]);
                    else // Otherwise, it is assumed we are starting the move from the current point
                        MoveList = new List<sbyte>();

                    MoveList.Add(To);
                    List<List<sbyte>> AllMoves;
                    if (!Results.TryGetValue(To, out AllMoves))
                    {
                        AllMoves = new List<List<sbyte>>();
                        Results[To] = AllMoves;
                    }
                    AllMoves.Add(MoveList);

                    GetPossibleMoves_Impl_Int(To, Results, TakePositions);

                    RestoreState(MyState);
                }
            }

            return false;
        }

        Dictionary<sbyte, List<sbyte>> GetPossibleMoves_Impl(sbyte From)
        {
            var AllMoves = new Dictionary<sbyte, List<List<sbyte>>>();
            var TakePositions = new HashSet<sbyte>();

            GetPossibleMoves_Impl_Int(From, AllMoves, TakePositions);

            var Result = new Dictionary<sbyte, List<sbyte>>();
            foreach (var M in AllMoves)
            {
                if (M.Value.Count == 1)
                {
                    Result.Add(M.Key, M.Value[0]);
                    continue;
                }

                bool CanTakeAlongPath = false;
                foreach (var L in M.Value)
                    for (int i = 0; i < L.Count - 1; ++i)
                        if (TakePositions.Contains(L[i]))
                            CanTakeAlongPath = true;

                if (!CanTakeAlongPath)
                    Result.Add(M.Key, M.Value.OrderBy(l => l.Count).First());
            }

            return Result;
        }

        public HashSet<Tuple<sbyte, bool>> GetPossibleMoves(sbyte From)
        {
            if (State != GameState.WaitMove)
                return null;

            var TurnSign = Turn.Flip().AsSign();
            var Result = new HashSet<Tuple<sbyte, bool>>();
            foreach (var k in GetPossibleMoves_Impl(From).Keys)
                Result.Add(new Tuple<sbyte, bool>(k, k >= 0 ? Board[k] == TurnSign : false));

            return Result;
        }

        public List<sbyte> GetMoveList(sbyte From, sbyte To)
        {
            if (State != GameState.WaitMove)
                return null;

            List<sbyte> Res;
            if (GetPossibleMoves_Impl(From).TryGetValue(To, out Res))
                return Res;
            else
                return null;
        }

        public HashSet<sbyte> GetPossibleSelections()
        {
            if (State != GameState.WaitMove)
                return null;

            var Result = new HashSet<sbyte>();

            var TurnIndex = Turn.AsIndex();
            var TurnSign = Turn.AsSign();
            bool Taken;

            if (Bar[TurnIndex] > 0)
            {
                foreach (var D in remainingDice.Unique.ToList())
                {
                    var To = (sbyte)((Turn == Color.White ? -1 : 24) + D * TurnSign);
                    if (MakeMove_Impl(-1, To, true, out Taken).IsSuccess())
                    {
                        Result.Add(-1);
                        break;
                    }
                }
            }

            for (sbyte i = 0; i < 24; ++i)
                if (Math.Sign(Board[i]) == TurnSign)
                    foreach (var D in remainingDice.Unique.ToList())
                    {
                        var To = (sbyte)(i + D * TurnSign);
                        if (To < 0 || To > 23)
                            To = -1;
                        if (MakeMove_Impl(i, To, true, out Taken).IsSuccess())
                        {
                            Result.Add(i);
                            break;
                        }
                    }

            return Result;
        }

        public override string ToString()
        {
            return $"{Turn} {State} {Board.Aggregate("", (s, b) => s + "," + b.ToString())} {Bar.Aggregate("", (s, b) => s + "," + b.ToString())} {BorneOff.Aggregate("", (s, b) => s + "," + b.ToString())} {rolledDice.Aggregate("", (s, b) => s + "," + b.ToString())} {remainingDice.Aggregate("", (s, b) => s + "," + b.ToString())}";
        }
#endif
    }
}
