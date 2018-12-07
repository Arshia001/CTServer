using System;
using System.Collections.Generic;
using System.Text;

namespace BackgammonLogic
{
    public enum EndTurnResult
    {
        Success,
        Error_HaveRemainingDiceToPlay,
        Error_CannotEndTurnInThisState
    }

    public static class EndTurnResultExtensions
    {
        public static bool IsSuccess(this EndTurnResult Value) => Value == EndTurnResult.Success;
    }
}
