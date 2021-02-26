using Bond;
using Bond.Tag;
using LightMessage.Common.MessagingProtocol;
using LightMessage.Common.WireProtocol;
using Orleans;
using Orleans.Concurrency;
using OrleansBondUtils;
using OrleansIndexingGrainInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public enum EPurchaseResult
    {
        Success,
        InsufficientFunds,
        InvalidID,
        AlreadyOwnsItem,
        IabTokenIsInvalid,
        CannotVerifyIab,
        IabTokenAlreadyProcessed
    }

    public enum UserStatistics
    {
        CheckersHit = 0,
        Gammons = 1,
        GamesPlayed = 2,
        GamesWon = 3,
        DoubleSixes = 4,
        WinStreak = 5,
        MaxWinStreak = 6,
        FirstPlace = 7,
        SecondPlace = 8,
        ThirdPlace = 9,
        CheckersHitToday = 10,
        CheckersHitRewardCollectedForToday = 11,
        MAX = 12
    }

    public static class UserStatisticsExtensions
    {
        public static int AsIndex(this UserStatistics Value) => (int)Value;
    }

    [Schema, BondSerializationTag("#up")]
    public class UserProfileState : IGenericSerializable, IOnDeserializedHandler
    {
        [Id(2)]
        public uint XP { get; set; }
        [Id(3)]
        public uint Level { get; set; }

        [Id(5)]
        public IGame CurrentGame { get; set; }

        [Id(6)]
        public DateTime NextSpinTime { get; set; }

        [Id(7)]
        public bool HasRatedOurApp { get; set; }

        [Id(8), Type(typeof(nullable<wstring>))]
        public string PlayGamesId { get; set; }

        [Id(9), Type(typeof(nullable<wstring>))]
        public string Name { get; set; }

        [Id(10)]
        public bool IsTutorialFinished { get; set; }

        [Id(12), Type(typeof(nullable<int>))]
        public int? QueueID { get; set; }

        [Id(13)]
        public float WinFactor { get; set; }

        [Id(16)]
        public Dictionary<CustomizationItemCategory, int> ActiveItems { get; set; } = new Dictionary<CustomizationItemCategory, int>();

        [Id(17)]
        public Dictionary<int, int> OwnedItems { get; set; } = new Dictionary<int, int>();

        [Id(19), Type(typeof(nullable<CurrencyAmount>))]
        public CurrencyAmount LastSpinResult { get; set; }

        [Id(20)]
        public uint[] Statistics { get; set; }

        [Id(21)]
        public ulong[] Funds { get; set; }

        [Id(22)]
        public HashSet<string> ProcessedIabTokens { get; set; }

        [Id(23)]
        public bool IsNameSet { get; set; }

        [Id(24)]
        public DateTime TodayEnd { get; set; }


        public UserProfileState()
        {
            Statistics = new uint[UserStatistics.MAX.AsIndex()];
            Funds = new ulong[2];
            ProcessedIabTokens = new HashSet<string>();
        }

        public void OnDeserialized()
        {
            if (Statistics.Length != UserStatistics.MAX.AsIndex())
            {
                var Previous = Statistics;
                Statistics = new uint[UserStatistics.MAX.AsIndex()];
                Array.Copy(Previous, Statistics, Math.Min(Previous.Length, Statistics.Length));
            }

            if (Funds.Length != 2)
            {
                var Previous = Funds;
                Funds = new ulong[2];
                Array.Copy(Previous, Funds, Math.Min(Previous.Length, Funds.Length));
            }

            if (ProcessedIabTokens == null)
                ProcessedIabTokens = new HashSet<string>();
        }

        public IEnumerable<Param> ToMessageParams(ReadOnlyConfigData Config)
        {
            LevelConfig.GetLevelInfo(Config, (int)Level, out var LevelXP, out _, out _);
            return new Param[] {
                Param.UInt(Funds[CurrencyType.Gem.AsIndex()]),
                Param.UInt(Funds[CurrencyType.Gold.AsIndex()]),
                Param.UInt(Level),
                Param.UInt(XP),
                Param.UInt(LevelXP),
                Param.String(Name),
                // We compensate for out-of-sync device clocks by sending a duration.
                // The client will convert this back to a timestamp with its local clock.
                Param.TimeSpan(NextSpinTime - DateTime.Now),
                Param.Boolean(IsTutorialFinished),
                Param.Boolean(HasRatedOurApp),
                Param.Boolean(CurrentGame != null),
                Param.Array(ActiveItems.Select(KV => Param.Int(KV.Value))),
                Param.Array(OwnedItems.Select(KV => Param.Int(KV.Key))),
                Param.Array(Statistics.Select(i => Param.UInt(i))),
                Param.Boolean(IsNameSet)
            };
        }

        public IEnumerable<Param> ToOpponentInfoParams()
        {
            return new Param[] {
                Param.UInt(Level),
                Param.UInt(XP),
                Param.String(Name),
                Param.Array(ActiveItems.SelectMany(KV => new Param[] { Param.UInt((uint)KV.Key), Param.Int(KV.Value) }))
            };
        }
    }

    public class UserProfileLeaderBoardInfo
    {
        public string Name { get; set; }
        public Dictionary<CustomizationItemCategory, int> ActiveItems { get; set; }
    }

    public class EndMatchResults
    {
        public ulong[] TotalFunds { get; set; }
        public uint TotalXP { get; set; }
        public uint Level { get; set; }
        public ulong[] LevelUpDeltaFunds { get; set; }
        public uint LevelXP { get; set; }
        public uint[] UpdatedStatistics { get; set; }
    }

    public class PurchaseResult
    {
        public EPurchaseResult Result { get; set; }
        public ulong[] TotalFunds { get; set; }
        public IReadOnlyDictionary<int, int> OwnedItems { get; set; }

        public PurchaseResult(EPurchaseResult Result)
        {
            this.Result = Result;
        }

        public Param[] ToMessageParams()
        {
            return new Param[]
            {
                Param.UInt((ulong)Result),
                Param.UInt(TotalFunds?[CurrencyType.Gold.AsIndex()]),
                Param.UInt(TotalFunds?[CurrencyType.Gem.AsIndex()]),
                Param.Array(OwnedItems?.Select(kv => new ParamInt(kv.Key)))
            };
        }
    }

    public class SpinResults
    {
        public int RewardID { get; set; }
        public ulong[] TotalFunds { get; set; }
        public TimeSpan TimeUntilNextSpin { get; set; }
    }

    [Schema]
    public class CurrencyAmount
    {
        [Id(0)]
        public CurrencyType Type;
        [Id(1)]
        public ulong Amount;

        public CurrencyAmount() { }

        public CurrencyAmount(CurrencyType Type, ulong Amount)
        {
            this.Type = Type;
            this.Amount = Amount;
        }
    }

    [BondSerializationTag("@up")]
    public interface IUserProfile : IGrainWithGuidKey
    {
        Task<bool> IsInitialized();
        Task<Immutable<UserProfileState>> GetInfo();
        Task<Immutable<UserProfileState>> PerformClientStartupTasksAndGetInfo();
        Task<Immutable<UserProfileLeaderBoardInfo>> GetLeaderBoardInfo();
        Task Destroy(); // Used when a device is linked with Google Play and the old account is no longer accessible

        Task<bool> EnterQueue(int GameID);
        Task LeaveQueue();
        Task<bool> IsInQueue(int QueueID);

        Task<bool> CanStartGame(int GameID);
        Task<IGame> GetGame();
        Task<bool> EnterGame(IGame Game, ulong EntranceFee);
        Task LeaveGame(IGame Game);
        Task<EndMatchResults> OnGameResult(IGame Game, bool Win, ulong Reward, uint XP);
        Task IncreaseStat(UserStatistics Stat);

        Task<List<int>> SetActiveCustomizations(List<int> IDs);
        Task<PurchaseResult> PurchaseCustomizationItem(int ItemID);
        Task<PurchaseResult> PurchasePack(int PackID);
        Task<PurchaseResult> PurchaseIabPack(int PackID, string Token);
        Task<Immutable<Dictionary<int, int>>> GetPurchaseLimits();

        Task<SpinResults> RollSpinner();
        Task<SpinResults> RollMultiplierSpinner();

        Task<ulong> TakeVideoAdReward();

        Task<bool> SetName(string Name);

        Task<Tuple<ulong, bool>> GetCheckerHitReward();
    }

    public static class UserProfileUtils
    {
        public static GrainIndexManager_Unique<string, IUserProfile> ByGooglePlayId = new GrainIndexManager_Unique<string, IUserProfile>("up_gp", 16384, new StringHashGenerator());
        public static GrainIndexManager_Unique<string, IUserProfile> ByUsername = new GrainIndexManager_Unique<string, IUserProfile>("up_un", 16384, new StringHashGenerator());

        public static async Task<IUserProfile> VerifyGooglePlayId(IGrainFactory GrainFactory, string GooglePlayId, string AuthCode)
        {
            var Profile = await ByGooglePlayId.GetGrain(GrainFactory, GooglePlayId);

            if (Profile != null /* ?? && await VerifyAuthCode(AuthCode)*/)
                return Profile;

            return null;
        }

        public static Task<bool> SetNameIfUnique(IGrainFactory GrainFactory, IUserProfile UserProfile, string Name)
        {
            return ByUsername.UpdateIndexIfUnique(GrainFactory, Name, UserProfile);
        }
    }
}
