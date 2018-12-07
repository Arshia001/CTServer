using CTGrainInterfaces;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Bond;
using OrleansBondUtils;

namespace CTGrains
{
    [Schema, BondSerializationTag("#lbt3")]
    public class LeaderBoardTop3EntryState : IGenericSerializable
    {
        // Data is split into two arrays to simplify serialization

        [Id(0)]
        public Guid[] IDs { get; set; }

        [Id(1)]
        public ulong[] Scores { get; set; }

        [Id(2)]
        public bool[] UserStatsApplied { get; set; }
    }

    public class LeaderBoardTop3Entry : Grain<LeaderBoardTop3EntryState>, ILeaderBoardTop3Entry
    {
        IDisposable RetryCreditTop3Timer;


        public override Task OnActivateAsync()
        {
            if (IsSetImpl() && !AllUserStatsApplied())
                RetryCreditTop3Timer = RegisterTimer(GiveCreditToTop3, null, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(60));

            return base.OnActivateAsync();
        }

        public Task<Immutable<LeaderBoardEntry[]>> Get()
        {
            if (!IsSetImpl())
                return Task.FromResult(new LeaderBoardEntry[0].AsImmutable());

            return Task.FromResult(Enumerable.Range(0, State.IDs.Length).Select(i => new LeaderBoardEntry() { Id = State.IDs[i], Rank = (ulong)i + 1, Score = State.Scores[i] }).ToArray().AsImmutable());
        }

        bool IsSetImpl()
        {
            return State.IDs != null && State.Scores != null;
        }

        public Task<bool> IsSet()
        {
            return Task.FromResult(IsSetImpl());
        }

        public Task Set(IEnumerable<LeaderBoardEntry> Entries)
        {
            if (IsSetImpl())
                return Task.CompletedTask;

            State.IDs = Entries.Take(3).Select(e => e.Id).ToArray();
            State.Scores = Entries.Take(3).Select(e => e.Score).ToArray();

            RetryCreditTop3Timer = RegisterTimer(GiveCreditToTop3, null, TimeSpan.FromMilliseconds(1), TimeSpan.FromSeconds(60));

            return WriteStateAsync();
        }

        bool AllUserStatsApplied()
        {
            return State.UserStatsApplied != null && State.UserStatsApplied.All(b => b);
        }

        async Task GiveCreditToTop3(object _State)
        {
            if (AllUserStatsApplied())
            {
                RetryCreditTop3Timer?.Dispose();
                RetryCreditTop3Timer = null;
                return;
            }

            if (State.UserStatsApplied == null)
                State.UserStatsApplied = new bool[State.IDs.Length];

            if (State.UserStatsApplied.Length > 0 && !State.UserStatsApplied[0])
            {
                try
                {
                    if (State.IDs[0] != Guid.Empty)
                        await GrainFactory.GetGrain<IUserProfile>(State.IDs[0]).IncreaseStat(UserStatistics.FirstPlace);
                    State.UserStatsApplied[0] = true;
                }
                catch { }
            }
            if (State.UserStatsApplied.Length > 1 && !State.UserStatsApplied[1])
            {
                try
                {
                    if (State.IDs[1] != Guid.Empty)
                        await GrainFactory.GetGrain<IUserProfile>(State.IDs[1]).IncreaseStat(UserStatistics.SecondPlace);
                    State.UserStatsApplied[1] = true;
                }
                catch { }
            }
            if (State.UserStatsApplied.Length > 2 && !State.UserStatsApplied[2])
            {
                try
                {
                    if (State.IDs[2] != Guid.Empty)
                        await GrainFactory.GetGrain<IUserProfile>(State.IDs[2]).IncreaseStat(UserStatistics.ThirdPlace);
                    State.UserStatsApplied[2] = true;
                }
                catch { }
            }

            if (AllUserStatsApplied())
            {
                RetryCreditTop3Timer?.Dispose();
                RetryCreditTop3Timer = null;
            }

            await WriteStateAsync();
        }
    }
}
