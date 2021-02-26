using CTGrainInterfaces;
using LightMessage.Common.WireProtocol;
using LightMessage.OrleansUtils.GrainInterfaces;
using LightMessage.OrleansUtils.Grains;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    [StatelessWorker(128), EndPointName("sys")]
    class SystemEndPoint : EndPointGrain, ISystemEndPoint
    {
        static MemoryCache LeaderBoardInfoCache;

        static SystemEndPoint()
        {
            LeaderBoardInfoCache = new MemoryCache("LBInfo");
        }


        IConfigReader ConfigReader;

        public SystemEndPoint(IConfigReader ConfigReader)
        {
            this.ConfigReader = ConfigReader;
        }

        /* 
         * Params:
         * empty
         */
        [MethodName("prof")]
        public async Task<EndPointFunctionResult> GetProfileInfo(EndPointFunctionParams Params)
        {
            var Info = (await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetInfo()).Value;
            return Success(Info.ToMessageParams(ConfigReader.Config));
        }

        [MethodName("ver")]
        public Task<EndPointFunctionResult> GetClientVersion(EndPointFunctionParams Params)
        {
            var Conf = ConfigReader.Config.ConfigValues;
            return Task.FromResult(Success(Param.UInt(Conf.ClientLatestVersion), Param.UInt(Conf.ClientEarliestSupportedVersion)));
        }

        /* 
         * Params:
         * empty
         */
        [MethodName("start")]
        public async Task<EndPointFunctionResult> GetStartupInfo(EndPointFunctionParams Params)
        {
            var ConfigData = ConfigReader.Config;
            if (ConfigData.ConfigValues.IsServerUnderMaintenance)
                throw new Exception("Server under maintenance");

            var ProfileGrain = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);
            var ProfileInfo = (await ProfileGrain.PerformClientStartupTasksAndGetInfo()).Value;

            var PurchaseLimits = (await ProfileGrain.GetPurchaseLimits()).Value;

            return Success(
                Param.Array(ProfileInfo.ToMessageParams(ConfigReader.Config)),
                Param.Array(ConfigData.CustomizationItems.Values.Select(c => Param.Array( // Customizations
                    Param.Int(c.ID),
                    Param.String(c.Category.ToString()),
                    Param.Boolean(c.IsPurchasable),
                    Param.UInt((ulong)c.Price),
                    Param.String(c.PriceCurrency.ToString()),
                    Param.String(c.ResourceID),
                    Param.Float(c.Sort)
                    ))),
                Param.Array(ConfigData.Games.Values.Select(g => Param.Array( // Games
                    Param.Int(g.ID),
                    Param.String(g.BoardID),
                    Param.UInt(g.EntranceFee),
                    Param.String(g.Name),
                    Param.UInt(g.Reward),
                    Param.Float(g.Sort)
                    ))),
                Param.Array(ConfigData.PackCategories.Values.Select(c => Param.Array( // Pack categories
                    Param.Int(c.ID),
                    Param.Boolean(c.IsSpecial),
                    Param.String(c.Name),
                    Param.DateTime(c.PurchaseDeadline),
                    Param.Boolean(c.PurchaseDuration == null), // IsAvailableInShop
                    Param.Float(c.Sort)
                    ))),
                Param.Array(ConfigData.Packs.Values.Select(p => Param.Array( // Packs
                    Param.Int(p.ID),
                    Param.Int(p.Category.ID),
                    Param.Array(p.Contents_Currency.SelectMany(pair => new Param[] { Param.String(pair.Key.ToString()), Param.UInt((ulong)pair.Value) })),
                    Param.Array(p.Contents_CustomizationItem.Select(ci => Param.Int(ci.ID))),
                    Param.String(p.ImageID),
                    Param.String(p.Name),
                    Param.UInt((ulong)p.Price),
                    Param.String(p.IabSku),
                    Param.String(p.PriceCurrency.ToString()),
                    PurchaseLimits.ContainsKey(p.ID) ? Param.UInt((ulong)PurchaseLimits[p.ID]) : Param.Null(),
                    Param.String(p.Tag),
                    Param.String(p.ValueSpecifier),
                    Param.Float(p.Sort)
                    ))),
                Param.Array(ConfigData.SpinRewards.Values.Select(p => Param.Array( // Spin rewards
                    Param.Int(p.ID),
                    Param.UInt((uint)p.RewardType),
                    Param.UInt((uint)p.Count),
                    Param.Float(p.Chance)
                    ))),
                Param.Array(ConfigData.SpinMultipliers.Values.Select(p => Param.Array( // Spin multipliers
                    Param.Int(p.ID),
                    Param.UInt((uint)p.Multiplier),
                    Param.Float(p.Chance)
                    ))),
                Param.Boolean(ConfigData.ConfigValues.IsMultiplayerAllowed),
                Param.UInt(ConfigReader.Config.ConfigValues.MaximumNameLength),
                Param.UInt(ConfigReader.Config.ConfigValues.VideoAdReward),
                Param.UInt(ConfigReader.Config.ConfigValues.NumCheckersToHitPerDayForReward),
                Param.UInt(ConfigReader.Config.ConfigValues.CheckerHitRewardPerDay)
                );
        }

        /* 
         * Params: 
         * 0 -> Array SelectedItemID int
         * 
         * Result:
         * ActiveItems Array int
         */
        [MethodName("cust")]
        public async Task<EndPointFunctionResult> SetActiveCustomizations(EndPointFunctionParams Params)
        {
            var Items = new List<int>();

            foreach (var P in Params.Args[0].AsArray)
                Items.Add((int)P.AsInt.Value);

            var Result = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).SetActiveCustomizations(Items);

            return Success(Param.Array(Result.Select(i => Param.Int(i))));
        }

        /* 
         * Params:
         * 0 -> ItemID int
         * 
         * Result:
         * EPurchaseResult uint
         * Gold uint
         * Gems uint
         * Owned Items Array uint
         */
        [MethodName("buyci")]
        public async Task<EndPointFunctionResult> PurchaseCustomizationItem(EndPointFunctionParams Params)
        {
            var ItemID = Params.Args[0].AsInt.Value;
            var Profile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);

            var Result = await Profile.PurchaseCustomizationItem((int)ItemID);

            return Success(Result.ToMessageParams());
        }

        /* 
         * Params:
         * 0 -> ItemID int
         * 
         * Result:
         * EPurchaseResult uint
         * Gold uint
         * Gems uint
         * Owned Items Array uint
         */
        [MethodName("buyp")]
        public async Task<EndPointFunctionResult> PurchasePack(EndPointFunctionParams Params)
        {
            var PackID = Params.Args[0].AsInt.Value;
            var Profile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);

            var Result = await Profile.PurchasePack((int)PackID);

            return Success(Result.ToMessageParams());
        }

        /* 
         * Params:
         * 0 -> ItemID int
         * 1 -> IabToken string
         * 
         * Result:
         * EPurchaseResult uint
         * Gold uint
         * Gems uint
         * Owned Items Array uint
         */
        [MethodName("buyiab")]
        public async Task<EndPointFunctionResult> PurchasePackWithIab(EndPointFunctionParams Params)
        {
            var PackID = Params.Args[0].AsInt.Value;
            var IabToken = Params.Args[1].AsString;

            if (!ConfigReader.Config.Packs.TryGetValue((int)PackID, out var Pack))
                throw new Exception("Invalid pack ID");

            var VerifyResult = await GrainFactory.GetGrain<IBazaarIabVerifier>(0).VerifyBazaarPurchase(Pack.IabSku, IabToken);

            PurchaseResult Result;

            if (!VerifyResult.HasValue)
                Result = new PurchaseResult(EPurchaseResult.CannotVerifyIab);
            else if (VerifyResult.Value == false)
                Result = new PurchaseResult(EPurchaseResult.IabTokenIsInvalid);
            else
            {
                var Profile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);
                Result = await Profile.PurchaseIabPack((int)PackID, IabToken);
            }

            return Success(Result.ToMessageParams());
        }

        /* 
         * Params:
         * empty
         * 
         * Result:
         * RewardID int
         * TotalGold ulong
         * TotalXP ulong
         */
        [MethodName("spin")]
        public async Task<EndPointFunctionResult> RollSpinner(EndPointFunctionParams Params)
        {
            var Profile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);

            var Result = await Profile.RollSpinner();

            if (Result == null)
                throw new Exception("");

            return Success(Param.Int(Result.RewardID), Param.UInt(Result.TotalFunds[CurrencyType.Gold.AsIndex()]), Param.UInt(Result.TotalFunds[CurrencyType.Gem.AsIndex()]), Param.TimeSpan(Result.TimeUntilNextSpin));
        }

        /* 
         * Params:
         * 1 -> VideoAdID string //?? do we need to support multiple ad providers here?
         * 
         * Result:
         * RewardID int
         * TotalGold ulong
         * TotalXP ulong
         */
        [MethodName("spinm")]
        public async Task<EndPointFunctionResult> RollMultiplierSpinner(EndPointFunctionParams Params)
        {
            if (!await VideoAdUtils.VerifyTapsellAd(Params.Args[0].AsString))
                throw new Exception("Video ad not valid");

            var Profile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);

            var Result = await Profile.RollMultiplierSpinner();

            if (Result == null)
                throw new Exception("");

            return Success(Param.Int(Result.RewardID), Param.UInt(Result.TotalFunds[CurrencyType.Gold.AsIndex()]), Param.UInt(Result.TotalFunds[CurrencyType.Gem.AsIndex()]));
        }

        /*
         * Params:
         * 1 -> VideoAdID string //?? do we need to support multiple ad providers here?
         * 
         * Result:
         * TotalGold ulong
         */
        [MethodName("vid")]
        public async Task<EndPointFunctionResult> TakeVideoAdReward(EndPointFunctionParams Params)
        {
            if (!await VideoAdUtils.VerifyTapsellAd(Params.Args[0].AsString))
                throw new Exception("Video ad not valid");

            var Profile = GrainFactory.GetGrain<IUserProfile>(Params.ClientID);

            var Result = await Profile.TakeVideoAdReward();

            return Success(Param.UInt(Result));
        }

        [MethodName("name")]
        public async Task<EndPointFunctionResult> SetName(EndPointFunctionParams Params)
        {
            var Name = Params.Args[0].AsString ?? throw new ArgumentException("Name cannot be null");
            if (Name.Length > ConfigReader.Config.ConfigValues.MaximumNameLength)
                throw new Exception("Name too long");
            var Result = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).SetName(Name);
            return Success(Param.Boolean(Result));
        }

        /* 
         * Params:
         * empty
         * 
         * Result:
         * Array lifetime:
         *     OwnRank uint
         *     Scores array:
         *         array:
         *             Rank uint
         *             Score uint
         *             Name string
         *             Customizations array:
         *                 ID int
         *                 
         * Array this month:
         *     OwnRank uint
         *     Scores array:
         *         array:
         *             Rank uint
         *             Score uint
         *             Name string
         *             Customizations array:
         *                 ID int
         *                 
         * Array Last month:
         *     Scores array:
         *         array:
         *             Rank uint
         *             Score uint
         *             Name string
         *             Customizations array:
         *                 ID int
         */
        [MethodName("lb")]
        public async Task<EndPointFunctionResult> GetLeaderBoard(EndPointFunctionParams Params)
        {
            var Lifetime = (await GrainFactory.GetGrain<ILeaderBoard>(LeaderBoardUtil.LifetimeId).GetScoresForDisplay(Params.ClientID)).Value;
            var ThisMonth = (await GrainFactory.GetGrain<ILeaderBoard>(LeaderBoardUtil.GetThisMonthIdString()).GetScoresForDisplay(Params.ClientID)).Value;
            var LastMonth = (await GrainFactory.GetGrain<ILeaderBoardTop3Entry>(LeaderBoardUtil.GetLastMonthIdString()).Get()).Value;

            // Cache user info for one hour, evict and update afterwards in case they update their profiles
            var CacheExpiration = DateTimeOffset.Now.AddHours(1);
            var NumTop = ConfigReader.Config.ConfigValues.LeaderBoardNumTopScoresToReturn;

            var LifetimeProfiles = new UserProfileLeaderBoardInfo[Lifetime.Item2.Count];
            for (int Idx = 0; Idx < LifetimeProfiles.Length; ++Idx)
            {
                if (Lifetime.Item2[Idx].Id == Params.ClientID)
                    continue;

                if (Lifetime.Item2[Idx].Rank <= NumTop)
                    LifetimeProfiles[Idx] = (UserProfileLeaderBoardInfo)LeaderBoardInfoCache[Lifetime.Item2[Idx].Id.ToString()];

                if (LifetimeProfiles[Idx] == null)
                {
                    LifetimeProfiles[Idx] = (await GrainFactory.GetGrain<IUserProfile>(Lifetime.Item2[Idx].Id).GetLeaderBoardInfo()).Value;
                    if (Lifetime.Item2[Idx].Rank <= NumTop)
                        LeaderBoardInfoCache.Add(Lifetime.Item2[Idx].Id.ToString(), LifetimeProfiles[Idx], CacheExpiration);
                }
            }

            var ThisMonthProfiles = new UserProfileLeaderBoardInfo[ThisMonth.Item2.Count];
            for (int Idx = 0; Idx < ThisMonthProfiles.Length; ++Idx)
            {
                if (ThisMonth.Item2[Idx].Id == Params.ClientID)
                    continue;

                if (ThisMonth.Item2[Idx].Rank <= NumTop)
                    ThisMonthProfiles[Idx] = (UserProfileLeaderBoardInfo)LeaderBoardInfoCache[ThisMonth.Item2[Idx].Id.ToString()];

                if (ThisMonthProfiles[Idx] == null)
                {
                    ThisMonthProfiles[Idx] = (await GrainFactory.GetGrain<IUserProfile>(ThisMonth.Item2[Idx].Id).GetLeaderBoardInfo()).Value;
                    if (ThisMonth.Item2[Idx].Rank <= NumTop)
                        LeaderBoardInfoCache.Add(ThisMonth.Item2[Idx].Id.ToString(), ThisMonthProfiles[Idx], CacheExpiration);
                }
            }

            var LastMonthProfiles = new UserProfileLeaderBoardInfo[LastMonth.Length];
            for (int Idx = 0; Idx < LastMonthProfiles.Length; ++Idx)
            {
                if (LastMonth[Idx].Id == Params.ClientID)
                    continue;

                LastMonthProfiles[Idx] = (UserProfileLeaderBoardInfo)LeaderBoardInfoCache[LastMonth[Idx].Id.ToString()];

                if (LastMonthProfiles[Idx] == null)
                {
                    LastMonthProfiles[Idx] = (await GrainFactory.GetGrain<IUserProfile>(LastMonth[Idx].Id).GetLeaderBoardInfo()).Value;
                    LeaderBoardInfoCache.Add(LastMonth[Idx].Id.ToString(), LastMonthProfiles[Idx], CacheExpiration);
                }
            }

            return Success(Param.Array(
                Param.UInt(Lifetime.Item1),
                Param.Array(Lifetime.Item2.Select((l, idx) =>
                    Param.Array(
                        Param.UInt(l.Rank),
                        Param.UInt(l.Score),
                        Param.String(LifetimeProfiles[idx]?.Name),
                        Param.Array(LifetimeProfiles[idx]?.ActiveItems.Values.Select(i => Param.Int(i))))
                ))),
                Param.Array(
                Param.UInt(ThisMonth.Item1),
                Param.Array(ThisMonth.Item2.Select((l, idx) =>
                    Param.Array(
                        Param.UInt(l.Rank),
                        Param.UInt(l.Score),
                        Param.String(ThisMonthProfiles[idx]?.Name),
                        Param.Array(ThisMonthProfiles[idx]?.ActiveItems.Values.Select(i => Param.Int(i))))
                ))),
                Param.Array(LastMonth.Select((l, idx) =>
                    Param.Array(
                        Param.UInt(l.Rank),
                        Param.UInt(l.Score),
                        Param.String(LastMonthProfiles[idx]?.Name),
                        Param.Array(LastMonthProfiles[idx]?.ActiveItems.Values.Select(i => Param.Int(i))))
                )));
        }

        [MethodName("hitreward")]
        public async Task<EndPointFunctionResult> GetCheckerHitReward(EndPointFunctionParams Params)
        {
            var result = await GrainFactory.GetGrain<IUserProfile>(Params.ClientID).GetCheckerHitReward();

            return Success(
                Param.UInt(result.Item1),
                Param.Boolean(result.Item2)
            );
        }
    }
}
