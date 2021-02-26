using CTGrainInterfaces;
using LightMessage.Common.WireProtocol;
using LightMessage.OrleansUtils.GrainInterfaces;
using LightMessage.OrleansUtils.Grains;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    [StatelessWorker(128), EndPointName("mmk")]
    class MatckmakingEndPoint : EndPointGrain, IMatchmakingEndPoint
    {
        IConfigReader ConfigReader;

        public MatckmakingEndPoint(IConfigReader ConfigReader)
        {
            this.ConfigReader = ConfigReader;
        }

        /* 
         * Params:
         * 0 -> GameID int
         */
        [MethodName("queue")]
        public async Task<EndPointFunctionResult> EnterQueue(EndPointFunctionParams Params)
        {
            var GameID = Params.Args[0].AsInt;
            if (!GameID.HasValue)
                throw new Exception("");

            var UserProfile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);

            if (!ConfigReader.Config.Games.TryGetValue((int)GameID, out var GameConfig))
                throw new Exception("Invalid game ID");

            if ((await UserProfile.GetInfo()).Value.Level < GameConfig.MinLevel)
                throw new Exception("User level too low for game");

            if (await UserProfile.EnterQueue((int)GameID.Value))
                return Success();
            else
                throw new Exception("Cannot enter queue while in another queue or game is in progress");
        }

        [MethodName("leave")]
        public async Task<EndPointFunctionResult> LeaveQueue(EndPointFunctionParams Params)
        {
            var UserProfile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);
            await UserProfile.LeaveQueue();

            return NoResult();
        }

        /* 
         * Params:
         * empty
         * 
         * result:
         * bool -> true if ready state set as normal, false if game was already in progress
         */
        [MethodName("ready")]
        public async Task<EndPointFunctionResult> ClientReady(EndPointFunctionParams Params)
        {
            var Game = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetGame();
            if (Game == null)
                return Success(Param.UInt((ulong)ReadyResponse.NotInGame));

            return Success(Param.UInt((ulong)(await Game.SetReady(Params.ClientID) ? ReadyResponse.OK : ReadyResponse.AlreadyInProgress)));
        }


        public override Task OnDisconnect(Guid ClientID)
        {
            return GrainFactory.GetGrain<IUserProfile>(ClientID).LeaveQueue();
        }


        public Task SendJoinGame(Guid ClientID, uint OpponentLevel, string OpponentName, IEnumerable<int> OpponentCustomizations, ulong TotalGold)
        {
            return SendMessage(ClientID,
                "join",
                Param.UInt(OpponentLevel),
                Param.String(OpponentName),
                Param.Array(OpponentCustomizations.Select(i => Param.Int(i))),
                Param.UInt(TotalGold));
        }

        public Task SendRemovedFromQueue(Guid ClientID)
        {
            return SendMessage(ClientID,
                "kick");
        }
    }
}
