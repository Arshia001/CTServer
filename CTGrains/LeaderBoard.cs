using Bond;
using Bond.Tag;
using CTGrainInterfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;
using OrleansBondUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    public class LeaderBoardConverter
    {
        public static ArraySegment<byte> Convert(SkipList<Guid, ulong> value, ArraySegment<byte> unused, Type ExpectedType)
        {
            if (value == null)
                return new ArraySegment<byte>();

            var Result = new byte[value.Length * 24];
            int Idx = 0;
            foreach (var KV in value)
            {
                Array.Copy(KV.Key.ToByteArray(), 0, Result, Idx, 16);
                Array.Copy(BitConverter.GetBytes(KV.Value), 0, Result, Idx + 16, 8);
                Idx += 24;
            }

            return new ArraySegment<byte>(Result);
        }

        public static SkipList<Guid, ulong> Convert(ArraySegment<byte> value, SkipList<Guid, ulong> unused, Type ExpectedType)
        {
            if (value.Count == 0)
                return new SkipList<Guid, ulong>();

            var Result = new SkipList<Guid, ulong>();
            var Stream = new MemoryStream(value.Array, value.Offset, value.Count);
            var IdBuf = new byte[16];
            var ScoreBuf = new byte[16];
            while (Stream.Position < Stream.Length)
            {
                Stream.Read(IdBuf, 0, 16);
                Stream.Read(ScoreBuf, 0, 8);
                Result.AddLast_ForDeserialization(new Guid(IdBuf), BitConverter.ToUInt64(ScoreBuf, 0));
            }

            Result.FinalizeDeserialization();
            return Result;
        }
    }

    [Schema, BondSerializationTag("#lb")]
    public class LeaderBoardState : IGenericSerializable
    {
        static LeaderBoardState()
        {
            CustomTypeRegistry.RegisterTypeConverter(typeof(LeaderBoardConverter));
            CustomTypeRegistry.AddTypeMapping(typeof(SkipList<Guid, ulong>), typeof(nullable<blob>), false);
        }

        [Id(0)]
        public SkipList<Guid, ulong> Scores { get; set; }

        public LeaderBoardState()
        {
            Scores = new SkipList<Guid, ulong>();
        }
    }

    public class LeaderBoard : Grain<LeaderBoardState>, ILeaderBoard, IRemindable
    {
        IDisposable WriteStateTimer, RetryCreditEntryTimer;
        bool ChangedSinceLastWrite;

        IConfigReader ConfigReader;
        ILogger Logger;


        public LeaderBoard(IConfigReader ConfigReader, ILogger<LeaderBoard> Logger)
        {
            this.ConfigReader = ConfigReader;
            this.Logger = Logger;
        }

        bool IsActive()
        {
            var PK = this.GetPrimaryKeyString();
            return PK == LeaderBoardUtil.LifetimeId || PK == LeaderBoardUtil.GetThisMonthIdString();
        }

        public override async Task OnActivateAsync()
        {
            // With 100,000 entries, a leaderboard takes ~0.130 seconds to serialize and 
            // write to DB on my development system. This number is expected to scale
            // linearly, which means we'd be doing well even if the number reaches 1M entries.
            // From there, care should be taken and the write timer adjusted, or new storage 
            // strategies devised.

            if (IsActive())
                WriteStateTimer = RegisterTimer(WriteStateTimerCallback, null, ConfigReader.Config.ConfigValues.LeaderBoardSaveInterval, ConfigReader.Config.ConfigValues.LeaderBoardSaveInterval);

            if (this.GetPrimaryKeyString() == LeaderBoardUtil.GetThisMonthIdString())
            {
                if (await GetReminder("end") == null)
                {
                    var StartOfNextMonth = LeaderBoardUtil.GetStartOfMonth(1);
                    var Interval = new[] { StartOfNextMonth - DateTime.Now, TimeSpan.FromMinutes(5) }.Max();
                    Logger.Info($"Setting leaderboard next month reminder for {Interval}, at {DateTime.Now.Add(Interval)}");
                    await RegisterOrUpdateReminder("end", Interval, TimeSpan.FromMinutes(1));
                }
            }

            await base.OnActivateAsync();
        }

        protected override async Task WriteStateAsync()
        {
            var S = new Stopwatch();
            S.Start();
            await base.WriteStateAsync();
            S.Stop();
            Logger.Info($"Serializing leaderboard with {State.Scores.Length} entries took {S.Elapsed}");
        }

        Task WriteStateTimerCallback(object State)
        {
            if (ChangedSinceLastWrite)
            {
                ChangedSinceLastWrite = false;
                return WriteStateAsync();
            }

            return Task.CompletedTask;
        }

        public override async Task OnDeactivateAsync()
        {
            WriteStateTimer?.Dispose();
            await WriteStateTimerCallback(null);
            await base.OnDeactivateAsync();
        }

        public Task Set(Guid Id, ulong Score)
        {
            if (State.Scores.GetScore(Id) < Score)
            {
                State.Scores.Add(Id, Score);
                ChangedSinceLastWrite = true;
            }

            return Task.CompletedTask;
        }

        public Task<ulong> AddDelta(Guid Id, ulong DeltaScore)
        {
            var Score = State.Scores.GetScore(Id);
            Score += DeltaScore;
            State.Scores.Add(Id, Score);
            ChangedSinceLastWrite = true;

            return Task.FromResult(Score);
        }

        ulong GetRankImpl(Guid Id)
        {
            return State.Scores.GetRank(Id);
        }

        public Task<ulong> GetRank(Guid Id)
        {
            return Task.FromResult(GetRankImpl(Id));
        }

        List<LeaderBoardEntry> GetScoresAroundImpl(Guid Id, uint CountInEachDirection)
        {
            var Rank = State.Scores.GetRank(Id) - CountInEachDirection;
            return State.Scores.GetRangeByRank((long)Rank, (long)Rank + CountInEachDirection * 2)
                .Select(s => new LeaderBoardEntry() { Id = s.Value, Score = s.Score, Rank = ++Rank }) // ++Rank because the ranks in the skiplist are zero based, so we add one BEFORE assigning to each entry
                .ToList();
        }

        public Task<Immutable<List<LeaderBoardEntry>>> GetScoresAround(Guid Id, uint CountInEachDirection)
        {
            return Task.FromResult(GetScoresAroundImpl(Id, CountInEachDirection).AsImmutable());
        }

        List<LeaderBoardEntry> GetTopScoresImpl(uint Count)
        {
            ulong Rank = 1;
            return State.Scores.GetRangeByRank(0, Count - 1)
                .Select(s => new LeaderBoardEntry() { Id = s.Value, Score = s.Score, Rank = Rank++ })
                .ToList();
        }

        public Task<Immutable<List<LeaderBoardEntry>>> GetTopScores(uint Count)
        {
            return Task.FromResult(GetTopScoresImpl(Count).AsImmutable());
        }

        public Task<Immutable<Tuple<ulong, List<LeaderBoardEntry>>>> GetScoresForDisplay(Guid UserID)
        {
            var TopCount = ConfigReader.Config.ConfigValues.LeaderBoardNumTopScoresToReturn;
            var AroundCount = ConfigReader.Config.ConfigValues.LeaderBoardNumAroundScoresToReturn;

            var OwnRank = GetRankImpl(UserID);
            if (OwnRank == 0)
                return Task.FromResult(new Tuple<ulong, List<LeaderBoardEntry>>(OwnRank, GetTopScoresImpl(TopCount)).AsImmutable());
            if (OwnRank < TopCount + AroundCount)
                return Task.FromResult(new Tuple<ulong, List<LeaderBoardEntry>>(OwnRank, GetTopScoresImpl((uint)OwnRank + AroundCount)).AsImmutable());
            else
                return Task.FromResult(new Tuple<ulong, List<LeaderBoardEntry>>(OwnRank, GetTopScoresImpl(TopCount).Concat(GetScoresAroundImpl(UserID, AroundCount)).ToList()).AsImmutable());

        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }

        public async Task CheckEndMonthHighScores()
        {
            if (this.GetPrimaryKeyString() == LeaderBoardUtil.GetLastMonthIdString() &&
                State.Scores.Length > 0 &&
                !await GrainFactory.GetGrain<ILeaderBoardTop3Entry>(this.GetPrimaryKeyString()).IsSet())
                await CreateTop3Entry(null);
        }

        public async Task ReceiveReminder(string reminderName, TickStatus status)
        {
            if (reminderName == "end")
            {
                Logger.Warn(0, $"End of month reached, will now create top 3 entry for leaderboard {this.GetPrimaryKeyString()}");

                var reminder = await GetReminder("end");
                if (reminder != null)
                    await UnregisterReminder(reminder);

                WriteStateTimer?.Dispose();
                WriteStateTimer = null;
                await CreateTop3Entry(null);
            }
        }

        // We could change this to a pull-based model with the top3entry object querying the leaderboard,
        // but that would delay the scoring process until someone opens the leaderboard after the new month
        async Task CreateTop3Entry(object State)
        {
            try
            {
                await GrainFactory.GetGrain<ILeaderBoardTop3Entry>(this.GetPrimaryKeyString()).Set((await GetTopScores(3)).Value);
                RetryCreditEntryTimer?.Dispose();
                RetryCreditEntryTimer = null;
                DeactivateOnIdle(); // This is probably the last time this grain is ever needed
            }
            catch
            {
                if (RetryCreditEntryTimer == null)
                    RetryCreditEntryTimer = RegisterTimer(CreateTop3Entry, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }
        }

        //public Task<Tuple<TimeSpan, TimeSpan>> TestSerialization(int count)
        //{
        //    var R = new Random();
        //    for (int i = 0; i < count; ++i)
        //        State.Scores.Add(Guid.NewGuid(), (ulong)R.Next());

        //    BondSerializer.Serialize(State);
        //    JsonConvert.SerializeObject(State);

        //    var t = new Stopwatch();
        //    t.Start();
        //    var o = BondSerializer.Serialize(State);
        //    t.Stop();
        //    Logger.Info($"binary ser, {t.Elapsed}, {o.Count}");
        //    t.Reset();
        //    t.Start();
        //    BondSerializer.Deserialize(typeof(LeaderBoardState), new ArraySegmentReaderStream(o));
        //    t.Stop();
        //    Logger.Info($"binary des, {t.Elapsed}");
        //    t.Reset();
        //    t.Start();
        //    var s = JsonConvert.SerializeObject(State);
        //    t.Stop();
        //    Logger.Info($"json ser  , {t.Elapsed}, {Encoding.UTF8.GetByteCount(s)}");
        //    t.Reset();
        //    t.Start();
        //    var jd = JsonConvert.DeserializeAnonymousType(s, new { Scores = new KeyValuePair<Guid, ulong>[0] });
        //    t.Stop();
        //    Logger.Info($"json des  , {t.Elapsed}");
        //    t.Reset();
        //    t.Start();
        //    var ss = new LeaderBoardState();
        //    foreach (var sss in jd.Scores)
        //        ss.Scores.Add(sss.Key, sss.Value);
        //    t.Stop();
        //    Logger.Info($"json des 2, {t.Elapsed}");

        //    return Task.FromResult(new Tuple<TimeSpan, TimeSpan>(TimeSpan.Zero, TimeSpan.Zero));
        //}
    }
}
