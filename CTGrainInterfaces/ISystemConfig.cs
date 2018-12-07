using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public enum CurrencyType
    {
        Gold = 0,
        Gem = 1,
        IRR
    }

    public static class CurrencyTypeExtensions
    {
        public static int AsIndex(this CurrencyType Value) => (int)Value;
    }

    public enum CustomizationItemCategory
    {
        CheckerFrame,
        CheckerImage,
        CheckerGem,
        ProfileGender
        //?? other entries here
    }

    public class CustomizationItemConfig
    {
        public int ID;
        public float Sort;
        public string ResourceID; // Could be an image, a 3D model with textures, etc. Will be stored as a Unity3D Resources folder entry for now. Client will know the format based on category.
        public CustomizationItemCategory Category;
        public CurrencyType PriceCurrency;
        public int Price;
        public bool IsPurchasable;
    }

    public class GameConfig
    {
        public int ID;
        public float Sort;
        public string Name;
        public string BoardID;
        public ulong EntranceFee;
        public ulong Reward;
        public uint MinLevel;
        public uint XP;
    }

    public class PackCategoryConfig
    {
        public int ID;
        public float Sort;
        public string Name;
        public DateTime? PurchaseDeadline;
        public TimeSpan? PurchaseDuration;
        public bool IsSpecial;

        public List<PackConfig> Packs;
    }

    public class PackConfig
    {
        public int ID;
        public float Sort;
        public string Name;
        public PackCategoryConfig Category;
        public string Tag;
        public string ImageID;
        public CurrencyType PriceCurrency;
        public int Price;
        public string IabSku;
        public Dictionary<CurrencyType, int> Contents_Currency;
        public HashSet<CustomizationItemConfig> Contents_CustomizationItem;
        public string ValueSpecifier;
        public int PurchaseCountLimit;
    }

    public class SpinMultiplierConfig
    {
        public int ID;
        public int Multiplier;
        public float Chance;
    }

    public class SpinRewardConfig
    {
        public int ID;
        public string Name;
        public CurrencyType RewardType;
        public int Count;
        public float Chance; //?? change to int
    }

    public class LevelConfig
    {
        public static void GetLevelInfo(ReadOnlyConfigData Config, int Level, out uint XP, out uint RewardAmount, out CurrencyType RewardType)
        {
            var Levels = Config.Levels;
            if (Level < Levels.Count)
            {
                RewardAmount = (uint)Levels[Level].RewardAmount;
                RewardType = Levels[Level].RewardCurrency;
                XP = (uint)Levels[Level].XP;
            }
            else
            {
                var Last = Levels.Count - 1;
                RewardAmount = (uint)(Levels[Last].RewardAmount + Levels[0].RewardAmount * Level - Last);
                RewardType = Levels[Last].RewardCurrency;
                XP = (uint)(Levels[Last].XP + Levels[0].XP * Level - Last);
            }
        }


        public int Level;
        public int XP;
        public CurrencyType RewardCurrency;
        public int RewardAmount;
    }

    public class ConfigValues
    {
        public TimeSpan BackgammonTurnTime;
        public TimeSpan BackgammonPenalizedTurnTime;
        public TimeSpan BackgammonExtraTimePerDiceRoll;
        public TimeSpan BackgammonExtraWaitTimePerTurn;
        public int BackgammonAIInitPlayTimeSeconds;
        public int BackgammonAITimeBetweenPlaysSeconds;
        public float BackgammonAIPlayTimeVariation;
        public int BackgammonNumInactiveTurnsToLose;

        public TimeSpan JoinGameTimeout;

        public TimeSpan LeaderBoardSaveInterval;
        public uint LeaderBoardNumTopScoresToReturn;
        public uint LeaderBoardNumAroundScoresToReturn;

        public TimeSpan MatchMakingInterval;
        public int MatchMakingInitWindowSize;
        public int MatchMakingWindowIncrement;
        public int MatchMakingNumUnsuccessfulAttemptsToMatchAI;

        public TimeSpan SpinnerInterval;

        public string BazaarClientID;
        public string BazaarClientSecret;
        public string BazaarRefreshCode;

        public uint ClientLatestVersion;
        public uint ClientEarliestSupportedVersion;

        public int[] UserInitialInventory;
        public int[] UserInitialActiveItems;
        public uint UserInitialGold;
        public uint UserInitialGems;

        public bool IsMultiplayerAllowed;
        public bool IsServerUnderMaintenance;

        public uint VideoAdReward;

        public uint MaximumNameLength;

        public uint NumCheckersToHitPerDayForReward;
        public uint CheckerHitRewardPerDay;
    }

    public class ConfigData
    {
        public Dictionary<int, CustomizationItemConfig> CustomizationItems;
        public Dictionary<int, GameConfig> Games;
        public Dictionary<int, PackCategoryConfig> PackCategories;
        public Dictionary<int, PackConfig> Packs;
        public Dictionary<int, SpinMultiplierConfig> SpinMultipliers;
        public Dictionary<int, SpinRewardConfig> SpinRewards;
        public List<LevelConfig> Levels;
        public ConfigValues ConfigValues;

        public int Version;
    }

    public class ReadOnlyConfigData
    {
        public IReadOnlyDictionary<int, CustomizationItemConfig> CustomizationItems => Data.CustomizationItems;
        public IReadOnlyDictionary<int, GameConfig> Games => Data.Games;
        public IReadOnlyDictionary<int, PackCategoryConfig> PackCategories => Data.PackCategories;
        public IReadOnlyDictionary<int, PackConfig> Packs => Data.Packs;
        public IReadOnlyDictionary<int, SpinMultiplierConfig> SpinMultipliers => Data.SpinMultipliers;
        public IReadOnlyDictionary<int, SpinRewardConfig> SpinRewards => Data.SpinRewards;
        public IReadOnlyList<LevelConfig> Levels => Data.Levels;
        public ConfigValues ConfigValues => Data.ConfigValues;

        public int Version => Data.Version;


        public WeightedRandom<SpinRewardConfig> SpinRewardRandom { get; }
        public WeightedRandom<SpinMultiplierConfig> SpinMultiplierRandom { get; }

        ConfigData Data;

        public ReadOnlyConfigData(ConfigData Data)
        {
            this.Data = Data;
            SpinRewardRandom = new WeightedRandom<SpinRewardConfig>(SpinRewards.Values, s => (int)s.Chance);
            SpinMultiplierRandom = new WeightedRandom<SpinMultiplierConfig>(SpinMultipliers.Values, s => (int)s.Chance);
        }
    }


    public interface ISystemConfig : IGrainWithIntegerKey
    {
        Task UpdateConfig();

        Task<Immutable<ConfigData>> GetConfig();
    }
}
