using System;
using System.Collections.Generic;
using System.Text;

namespace BackgammonLogic
{
    public enum MakeMoveResult
    {
        Success,
        NoMoveToMake,
        Win,
        Error_InvalidFrom,
        Error_InvalidTo,
        Error_CannotMoveBackwards,
        Error_HaveNoSuchDie,
        Error_NoCheckerAtPoint,
        Error_NoCheckerOnBar,
        Error_PointBlockedByOpponent,
        Error_CannotEnterAndBearOffAtSameTime,
        Error_MustEnterFirst,
        Error_CannotBearOffWhenCheckersOutsideHome,
        Error_CannotBearOffAsCheckersBehindCurrent,
        Error_MoveCausesUnusableDice,
        Error_IncorrectTurn,
        Error_CannotMoveInThisState,
        Error_Interal_FoundNoValidMoves
    }

    public static class MakeMoveResultExtensions
    {
        public static bool IsSuccess(this MakeMoveResult M) => M == MakeMoveResult.Win || M == MakeMoveResult.Success || M == MakeMoveResult.NoMoveToMake;
    }
}
