using System;
using System.Collections.Generic;
using System.Text;

namespace BackgammonLogic
{
    public enum RollDiceResult
    {
        Success,
        HaveNoMovesToMake,
        Error_CannotRollInThisState
    }

    public static class RollDiceResultExtensions
    {
        public static bool IsSuccess(this RollDiceResult Value) => Value != RollDiceResult.Error_CannotRollInThisState;
    }
}
