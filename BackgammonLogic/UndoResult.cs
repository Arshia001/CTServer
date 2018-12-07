using System;
using System.Collections.Generic;
using System.Text;

namespace BackgammonLogic
{
    public enum UndoResultType
    {
        Success,
        Error_HaveNoMovesToUndo,
        Error_CannotUndoInThisState
    }

    public struct UndoResult
    {
        public UndoResultType Result { get; }
        public Move? Move { get; }


        public UndoResult(UndoResultType result, Move? move)
        {
            Result = result;
            Move = move;
        }

        public static implicit operator UndoResult(UndoResultType result)
        {
            return new UndoResult(result, null);
        }

        public bool IsSuccess()
        {
            return Result == UndoResultType.Success;
        }
    }
}
