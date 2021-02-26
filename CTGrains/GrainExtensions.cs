using CTGrainInterfaces;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    static class GrainExtensions
    {
        public static async Task SafeExecute(this IGrain Grain, Func<Task> Task, Func<Task> OnFail = null)
        {
            try
            {
                await Task();
            }
            catch (Exception Ex)
            {
                var T = OnFail?.Invoke();
                await T;
                throw new ErrorCodeException(CTGrainInterfaces.ErrorCode.InternalError, Ex);
            }
        }
    }
}
