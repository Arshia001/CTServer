using Cassandra;
using CTGrainInterfaces;
using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;
using OrleansCassandraUtils.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CTGrains
{
    public class SystemConfig : Grain, ISystemConfig
    {
        ConfigData Data;


        public override async Task OnActivateAsync()
        {
            DelayDeactivation(TimeSpan.MaxValue);

            await InternalUpdateConfig();

            await base.OnActivateAsync();
        }

        public Task<Immutable<ConfigData>> GetConfig()
        {
            return Task.FromResult(Data.AsImmutable());
        }

        static Dictionary<int, CustomizationItemConfig> ReadCustomizationItems(RowSet Rows)
        {
            var Result = new Dictionary<int, CustomizationItemConfig>();

            foreach (var Row in Rows)
            {
                var ID = Convert.ToInt32(Row["id"]);
                Result.Add(ID, new CustomizationItemConfig
                {
                    ID = ID,
                    Category = (CustomizationItemCategory)Enum.Parse(typeof(CustomizationItemCategory), Convert.ToString(Row["category"]).Replace("_", ""), true),
                    IsPurchasable = Convert.ToBoolean(Row["is_purchasable"]),
                    Price = Convert.ToInt32(Row["price"]),
                    PriceCurrency = (CurrencyType)Enum.Parse(typeof(CurrencyType), Convert.ToString(Row["price_currency"]), true),
                    ResourceID = Convert.ToString(Row["resource_id"]),
                    Sort = Convert.ToSingle(Row["sort"])
                });
            }

            return Result;
        }

        static Dictionary<int, GameConfig> ReadGames(RowSet Rows)
        {
            var Result = new Dictionary<int, GameConfig>();

            foreach (var Row in Rows)
            {
                if (Convert.ToBoolean(Row["is_active"]))
                {
                    var ID = Convert.ToInt32(Row["id"]);
                    Result.Add(ID, new GameConfig
                    {
                        ID = ID,
                        EntranceFee = Convert.ToUInt64(Row["entrance_fee"]),
                        Reward = Convert.ToUInt64(Row["reward"]),
                        Name = Convert.ToString(Row["name"]),
                        BoardID = Convert.ToString(Row["board_id"]),
                        Sort = Convert.ToSingle(Row["sort"]),
                        MinLevel = Convert.ToUInt32(Row["min_level"]),
                        XP = Convert.ToUInt32(Row["xp"])
                    });
                }
            }

            return Result;
        }

        static Dictionary<int, PackCategoryConfig> ReadPackCategories(RowSet Rows)
        {
            var Result = new Dictionary<int, PackCategoryConfig>();

            foreach (var Row in Rows)
            {
                var ID = Convert.ToInt32(Row["id"]);
                Result.Add(ID, new PackCategoryConfig
                {
                    ID = ID,
                    IsSpecial = Convert.ToBoolean(Row["is_special"]),
                    Name = Convert.ToString(Row["name"]),
                    PurchaseDeadline = Row["purchase_deadline"] == null ? default(DateTime?) : ((DateTimeOffset)(Row["purchase_deadline"])).UtcDateTime,
                    PurchaseDuration = Row["purchase_duration_minutes"] == null ? default(TimeSpan?) : TimeSpan.FromMinutes(Convert.ToInt32(Row["purchase_duration_minutes"])),
                    Sort = Convert.ToSingle(Row["sort"]),
                    Packs = new List<PackConfig>()
                });
            }

            return Result;
        }

        static Dictionary<int, PackConfig> ReadPacks(RowSet Rows, Dictionary<int, CustomizationItemConfig> CustomizationItems, Dictionary<int, PackCategoryConfig> PackCategories)
        {
            var Result = new Dictionary<int, PackConfig>();

            foreach (var Row in Rows)
                if (Convert.ToBoolean(Row["is_active"]))
                {
                    var Item = new PackConfig
                    {
                        ID = Convert.ToInt32(Row["id"])
                    };

                    if (!PackCategories.TryGetValue(Convert.ToInt32(Row["category"]), out Item.Category))
                        throw new FormatException($"Category ID '{Row["category"]}' not found while reading Pack with ID {Item.ID}");

                    var CurrencyContents = (IDictionary<string, int>)Row["currency_contents"];
                    Item.Contents_Currency = new Dictionary<CurrencyType, int>();
                    if (CurrencyContents != null)
                        foreach (var C in CurrencyContents)
                            Item.Contents_Currency.Add((CurrencyType)Enum.Parse(typeof(CurrencyType), C.Key, true), C.Value);

                    var ItemContents = (IEnumerable<int>)Row["item_contents"];
                    Item.Contents_CustomizationItem = new HashSet<CustomizationItemConfig>();
                    if (ItemContents != null)
                        foreach (var I in ItemContents)
                            if (CustomizationItems.TryGetValue(I, out var CustItem))
                                Item.Contents_CustomizationItem.Add(CustItem);
                            else
                                throw new FormatException($"Customization item ID '{I}' not found while reading Pack with ID {Item.ID}");

                    Item.ImageID = Convert.ToString(Row["image_id"]);
                    Item.Name = Convert.ToString(Row["name"]);

                    Item.PriceCurrency = (CurrencyType)Enum.Parse(typeof(CurrencyType), Convert.ToString(Row["price_currency"]), true);
                    if (Item.PriceCurrency == CurrencyType.IRR)
                    {
                        Item.IabSku = Convert.ToString(Row["iab_sku"]);
                        if (string.IsNullOrEmpty(Item.IabSku))
                            throw new InvalidOperationException($"SKU not specified for IAB item {Item.ID}:{Item.Name}");
                    }
                    else
                        Item.Price = Convert.ToInt32(Row["price"]);

                    Item.PurchaseCountLimit = Convert.ToInt32(Row["purchase_count_limit"]);
                    Item.Tag = Convert.ToString(Row["tag"]);
                    Item.ValueSpecifier = Convert.ToString(Row["value_specifier"]);
                    Item.Sort = Convert.ToSingle(Row["sort"]);

                    Result.Add(Item.ID, Item);

                    Item.Category.Packs.Add(Item);
                }

            return Result;
        }

        static Dictionary<int, SpinMultiplierConfig> ReadSpinMultipliers(RowSet Rows)
        {
            var Result = new Dictionary<int, SpinMultiplierConfig>();

            foreach (var Row in Rows)
            {
                if (Convert.ToBoolean(Row["is_active"]))
                {
                    var ID = Convert.ToInt32(Row["id"]);
                    Result.Add(ID, new SpinMultiplierConfig
                    {
                        ID = ID,
                        Chance = Convert.ToSingle(Row["chance"]),
                        Multiplier = Convert.ToInt32(Row["multiplier"])
                    });
                }
            }

            return Result;
        }

        static Dictionary<int, SpinRewardConfig> ReadSpinRewards(RowSet Rows)
        {
            var Result = new Dictionary<int, SpinRewardConfig>();

            foreach (var Row in Rows)
            {
                if (Convert.ToBoolean(Row["is_active"]))
                {
                    var ID = Convert.ToInt32(Row["id"]);
                    Result.Add(ID, new SpinRewardConfig
                    {
                        ID = ID,
                        Chance = Convert.ToSingle(Row["chance"]),
                        Count = Convert.ToInt32(Row["count"]),
                        Name = Convert.ToString(Row["name"]),
                        RewardType = (CurrencyType)Enum.Parse(typeof(CurrencyType), Convert.ToString(Row["reward_type"]), true)
                    });
                }
            }

            return Result;
        }

        static List<LevelConfig> ReadLevels(RowSet Rows)
        {
            var List = new List<LevelConfig>();

            foreach (var Row in Rows)
                List.Add(new LevelConfig
                {
                    Level = Convert.ToInt32(Row["level"]),
                    RewardCurrency = (CurrencyType)Enum.Parse(typeof(CurrencyType), Convert.ToString(Row["reward_currency"]), true),
                    RewardAmount = Convert.ToInt32(Row["reward"]),
                    XP = Convert.ToInt32(Row["xp"]),
                });

            var Result = new LevelConfig[List.Count];
            foreach (var L in List)
                Result[L.Level] = L;

            return Result.ToList();
        }

        static ConfigValues ReadConfigValues(RowSet Rows, Dictionary<int, CustomizationItemConfig> Items)
        {
            var Result = new ConfigValues();
            foreach (var Row in Rows)
            {
                switch (Convert.ToString(Row["key"]))
                {
                    case "BkgmTurnTimeSeconds":
                        Result.BackgammonTurnTime = TimeSpan.FromSeconds(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "BkgmPenalizedTurnTimeSeconds":
                        Result.BackgammonPenalizedTurnTime = TimeSpan.FromSeconds(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "BkgmExtraTimePerDiceRollSeconds":
                        Result.BackgammonExtraTimePerDiceRoll = TimeSpan.FromSeconds(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "BkgmExtraWaitTimePerTurnSeconds":
                        Result.BackgammonExtraWaitTimePerTurn = TimeSpan.FromSeconds(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "BkgmAIInitPlayTimeSeconds":
                        Result.BackgammonAIInitPlayTimeSeconds = int.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "BkgmAITimeBetweenPlaysSeconds":
                        Result.BackgammonAITimeBetweenPlaysSeconds = int.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "BkgmAIPlayTimeVariation":
                        Result.BackgammonAIPlayTimeVariation = float.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "BkgmNumInactiveTurnsToLose":
                        Result.BackgammonNumInactiveTurnsToLose = int.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "JoinGameTimeoutSeconds":
                        Result.JoinGameTimeout = TimeSpan.FromSeconds(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "LeaderBoardSaveIntervalSeconds":
                        Result.LeaderBoardSaveInterval = TimeSpan.FromSeconds(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "LeaderBoardNumTopScoresToReturn":
                        Result.LeaderBoardNumTopScoresToReturn = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "LeaderBoardNumAroundScoresToReturn":
                        Result.LeaderBoardNumAroundScoresToReturn = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "MatchMakingInitWindowSize":
                        Result.MatchMakingInitWindowSize = int.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "MatchMakingWindowIncrement":
                        Result.MatchMakingWindowIncrement = int.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "MatchMakingNumUnsuccessfulAttemptsToMatchAI":
                        Result.MatchMakingNumUnsuccessfulAttemptsToMatchAI = int.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "MatchMakingIntervalSeconds":
                        Result.MatchMakingInterval = TimeSpan.FromSeconds(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "SpinnerIntervalMinutes":
                        Result.SpinnerInterval = TimeSpan.FromMinutes(int.Parse(Convert.ToString(Row["value"])));
                        break;
                    case "BazaarClientID":
                        Result.BazaarClientID = Convert.ToString(Row["value"]);
                        break;
                    case "BazaarClientSecret":
                        Result.BazaarClientSecret = Convert.ToString(Row["value"]);
                        break;
                    case "BazaarRefreshCode":
                        Result.BazaarRefreshCode = Convert.ToString(Row["value"]);
                        break;
                    case "ClientLatestVersion":
                        Result.ClientLatestVersion = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "ClientEarliestSupportedVersion":
                        Result.ClientEarliestSupportedVersion = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "UserInitialInventory":
                        Result.UserInitialInventory = Convert.ToString(Row["value"]).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var i) ? i : int.MinValue).Where(i => Items.ContainsKey(i)).ToArray();
                        break;
                    case "UserInitialActiveItems":
                        Result.UserInitialActiveItems = Convert.ToString(Row["value"]).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => int.TryParse(s, out var i) ? i : int.MinValue).Where(i => Items.ContainsKey(i)).ToArray();
                        break;
                    case "UserInitialGold":
                        Result.UserInitialGold = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "UserInitialGems":
                        Result.UserInitialGems = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "IsMultiplayerAllowed":
                        Result.IsMultiplayerAllowed = bool.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "IsServerUnderMaintenance":
                        Result.IsServerUnderMaintenance = bool.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "VideoAdReward":
                        Result.VideoAdReward = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "MaximumNameLength":
                        Result.MaximumNameLength = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "NumCheckersToHitPerDayForReward":
                        Result.NumCheckersToHitPerDayForReward = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                    case "CheckerHitRewardPerDay":
                        Result.CheckerHitRewardPerDay = uint.Parse(Convert.ToString(Row["value"]));
                        break;
                }
            }
            return Result;
        }

        async Task InternalUpdateConfig()
        {
            var connectionString = CTSettings.Instance.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("Config connection string is null");
            var Session = await CassandraSessionFactory.CreateSession(connectionString);

            var NewData = new ConfigData();

            NewData.CustomizationItems = ReadCustomizationItems(await Session.ExecuteAsync(new SimpleStatement("select * from ct_customization_items")));
            NewData.Games = ReadGames(await Session.ExecuteAsync(new SimpleStatement("select * from ct_games")));
            NewData.PackCategories = ReadPackCategories(await Session.ExecuteAsync(new SimpleStatement("select * from ct_pack_categories")));
            NewData.Packs = ReadPacks(await Session.ExecuteAsync(new SimpleStatement("select * from ct_packs")), NewData.CustomizationItems, NewData.PackCategories);
            NewData.SpinMultipliers = ReadSpinMultipliers(await Session.ExecuteAsync(new SimpleStatement("select * from ct_spin_multipliers")));
            NewData.SpinRewards = ReadSpinRewards(await Session.ExecuteAsync(new SimpleStatement("select * from ct_spin_rewards")));
            NewData.Levels = ReadLevels(await Session.ExecuteAsync(new SimpleStatement("select * from ct_levels")));
            NewData.ConfigValues = ReadConfigValues(await Session.ExecuteAsync(new SimpleStatement("select * from ct_config")), NewData.CustomizationItems);

            foreach (var PC in NewData.PackCategories.Values.ToArray())
                if ((PC.PurchaseDeadline != null && PC.PurchaseDeadline < DateTime.Now) || PC.Packs.Count == 0)
                {
                    foreach (var _P in PC.Packs)
                        NewData.Packs.Remove(_P.ID);
                    NewData.PackCategories.Remove(PC.ID);
                }

            NewData.Version = (Data?.Version ?? 0) + 1;

            Data = NewData;
        }

        public async Task UpdateConfig()
        {
            await InternalUpdateConfig();

            await GrainFactory.GetGrain<IConfigUpdaterGrain>(0).PushUpdateToAllSilos(Data.Version);
        }
    }
}
