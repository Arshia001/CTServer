using CTGrainInterfaces;
using LightMessage.Common.Util;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    public abstract class Game : Grain, IGame
    {
        protected Random Random = new Random();


        protected Dictionary<Guid, byte> PlayerIdx = new Dictionary<Guid, byte>();
        protected bool[] Ready;
        protected bool Started;

        protected GameConfig GameConfig;

        IDisposable joinTimer;


        protected abstract Task StartGame();
        protected abstract Task OnPlayersFailedToJoin();

        public virtual Task Start(List<Guid> Players, int GameID)
        {
            var config = ServiceProvider.GetRequiredService<IConfigReader>().Config;

            GameConfig = config.Games[GameID];

            Ready = new bool[Players.Count];

            byte Idx = 0;
            while (Players.Count > 0)
            {
                int PIdx = Random.Next(Players.Count);
                PlayerIdx[Players[PIdx]] = Idx++;
                Players.RemoveAt(PIdx);
            }

            joinTimer = RegisterTimer(JoinTimerExpired, null, config.ConfigValues.JoinGameTimeout, TimeSpan.MaxValue);

            return Task.CompletedTask;
        }

        Task JoinTimerExpired(object state)
        {
            joinTimer.Dispose();
            return OnPlayersFailedToJoin();
        }

        public virtual async Task<bool> SetReady(Guid ClientID)
        {
            if (Started)
                return false;
            else if (Ready == null)
                throw new Exception("Game not in progress");

            Ready[PlayerIdx[ClientID]] = true;
            if (Ready.All(b => b))
            {
                Ready = null;
                Started = true;
                joinTimer.Dispose();
                await StartGame();
            }

            return true;
        }

        public Task<Immutable<GameConfig>> GetGameConfig()
        {
            return Task.FromResult(GameConfig.AsImmutable());
        }

        public Task<bool> IsActive()
        {
            return Task.FromResult(Started || Ready != null);
        }
    }
}
