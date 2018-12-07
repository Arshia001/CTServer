using Bond;
using Bond.Tag;
using CTGrainInterfaces;
using Orleans;
using OrleansBondUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.Concurrency;

namespace CTGrains
{
    class UserProfile : Grain<UserProfileState>, IUserProfile
    {
        IConfigReader ConfigReader;
        Random Random;

        public UserProfile(IConfigReader ConfigReader)
        {
            this.ConfigReader = ConfigReader;
            Random = new Random();
        }

        public Task<bool> IsInitialized()
        {
            return Task.FromResult(State.Level > 0);
        }

        async Task<Immutable<UserProfileState>> GetInfo_Impl(bool ClientStartup)
        {
            bool WriteState = false;

            if (State.Level <= 0)
            {
                State.Level = 1;
                State.NextSpinTime = DateTime.Now;

                var Conf = ConfigReader.Config;

                State.Name = "مهمان" + Random.Next(100000000).ToString();

                State.Funds[CurrencyType.Gold.AsIndex()] = Conf.ConfigValues.UserInitialGold;
                State.Funds[CurrencyType.Gem.AsIndex()] = Conf.ConfigValues.UserInitialGems;

                foreach (var ID in Conf.ConfigValues.UserInitialInventory)
                    State.OwnedItems.Add(ID, 1);

                foreach (var ID in Conf.ConfigValues.UserInitialActiveItems)
                    State.ActiveItems.Add(Conf.CustomizationItems[ID].Category, ID);

                WriteState = true;
            }

            if (ClientStartup)
            {
                // Perhaps a silo shutdown? We don't correctly handle serializing games yet
                if (State.CurrentGame != null && !await State.CurrentGame.IsActive())
                {
                    State.CurrentGame = null;
                    WriteState = true;
                }

                if (State.QueueID != null)
                {
                    State.QueueID = null;
                    WriteState = true;
                }
            }

            if (await ResetToday(false))
            {
                WriteState = true;
            }

            if (WriteState)
                await WriteStateAsync();

            return State.AsImmutable();
        }

        public Task<Immutable<UserProfileState>> GetInfo()
        {
            return GetInfo_Impl(false);
        }

        public Task<Immutable<UserProfileState>> PerformClientStartupTasksAndGetInfo()
        {
            return GetInfo_Impl(true);
        }

        public Task<Immutable<UserProfileLeaderBoardInfo>> GetLeaderBoardInfo()
        {
            return Task.FromResult(new UserProfileLeaderBoardInfo { Name = State.Name, ActiveItems = State.ActiveItems }.AsImmutable());
        }

        public Task Destroy()
        {
            if (State.PlayGamesId != null)
                throw new ErrorCodeException(ErrorCode.CannotDestroyAccountWithSocialLink);

            return ClearStateAsync();
        }


        public async Task<bool> EnterQueue(int GameID)
        {
            if (!ConfigReader.Config.Games.TryGetValue(GameID, out var Game))
                return false;

            if (!(State.CurrentGame == null && State.QueueID == null))
                return false;

            await GrainFactory.GetGrain<IMatchmakerGrain>(GameID).EnterQueue(this.AsReference<IUserProfile>(), State.AsImmutable());
            State.QueueID = GameID;
            await WriteStateAsync();
            return true;
        }

        public async Task LeaveQueue()
        {
            // VERY rare case where the queue is already matching this profile and we arrive here.
            // The client will need to be responsive to join messages after a leave request.
            if (State.QueueID != null)
            {
                // await GrainFactory.GetGrain<IMatchmakerGrain>(State.QueueID.Value).LeaveQueue(this.AsReference<IUserProfile>(), State.AsImmutable());

                // To leave queue, simply set queue ID to null. The queue will pick it up and remove me.
                State.QueueID = null;
                await WriteStateAsync();
            }
        }

        public Task<bool> IsInQueue(int QueueID)
        {
            return Task.FromResult(State.QueueID == QueueID);
        }


        public Task<bool> CanStartGame(int GameID)
        {
            return Task.FromResult(State.Funds[CurrencyType.Gold.AsIndex()] >= ConfigReader.Config.Games[GameID].EntranceFee);
        }

        public Task<IGame> GetGame()
        {
            return Task.FromResult(State.CurrentGame);
        }

        public async Task<bool> EnterGame(IGame Game, ulong EntranceFee)
        {
            if (State.CurrentGame != null)
                return false;

            State.Funds[CurrencyType.Gold.AsIndex()] -= EntranceFee;

            State.QueueID = null;
            State.CurrentGame = Game;
            await WriteStateAsync();

            return true;
        }

        public async Task<EndMatchResults> OnGameResult(IGame Game, bool Win, ulong Reward, uint XP)
        {
            var Result = new EndMatchResults();

            if (State.CurrentGame.Equals(Game))
            {
                await ResetToday();

                if (Win)
                {
                    IncreaseStat_Internal(UserStatistics.GamesWon);
                    IncreaseStat_Internal(UserStatistics.WinStreak);
                    State.Statistics[UserStatistics.MaxWinStreak.AsIndex()] = Math.Max(State.Statistics[UserStatistics.MaxWinStreak.AsIndex()], State.Statistics[UserStatistics.WinStreak.AsIndex()]);
                    State.Funds[CurrencyType.Gold.AsIndex()] += Reward;

                    GrainFactory.GetGrain<ILeaderBoard>(LeaderBoardUtil.LifetimeId).AddDelta(this.GetPrimaryKey(), Reward).Ignore();
                    GrainFactory.GetGrain<ILeaderBoard>(LeaderBoardUtil.GetIdString(DateTime.Now)).AddDelta(this.GetPrimaryKey(), Reward).Ignore();
                }
                else
                    State.Statistics[UserStatistics.WinStreak.AsIndex()] = 0;

                AddXP(XP, out var LevelCurrencies);

                IncreaseStat_Internal(UserStatistics.GamesPlayed);
                State.CurrentGame = null;

                State.WinFactor = Math.Max(Math.Min(State.WinFactor + (Win ? -0.5f : 0.5f), 3), -3);

                await WriteStateAsync();

                LevelConfig.GetLevelInfo(ConfigReader.Config, (int)State.Level, out var LevelXP, out _, out _);

                return new EndMatchResults
                {
                    TotalFunds = (ulong[])State.Funds.Clone(),
                    TotalXP = State.XP,
                    Level = State.Level,
                    LevelXP = LevelXP,
                    LevelUpDeltaFunds = LevelCurrencies,
                    UpdatedStatistics = State.Statistics
                };
            }

            return null;
        }

        void AddXP(uint DeltaXP, out ulong[] DeltaCurrencies)
        {
            DeltaCurrencies = new ulong[2];
            State.XP += DeltaXP;

            while (true)
            {
                LevelConfig.GetLevelInfo(ConfigReader.Config, (int)State.Level, out var LevelXP, out var Reward, out var Type);
                if (State.XP >= LevelXP)
                {
                    State.XP -= LevelXP;
                    ++State.Level;

                    State.Funds[Type.AsIndex()] += Reward;
                    DeltaCurrencies[Type.AsIndex()] = Reward;

                    continue;
                }
                break;
            }
        }

        void IncreaseStat_Internal(UserStatistics Stat)
        {
            ++State.Statistics[Stat.AsIndex()];

            if (Stat == UserStatistics.CheckersHit)
                ++State.Statistics[UserStatistics.CheckersHitToday.AsIndex()];
        }

        public Task IncreaseStat(UserStatistics Stat)
        {
            IncreaseStat_Internal(Stat);
            return WriteStateAsync();
        }

        public Task LeaveGame(IGame Game)
        {
            if (State.CurrentGame.Equals(Game))
            {
                State.CurrentGame = null;
                return WriteStateAsync();
            }

            return Task.CompletedTask;
        }


        public Task<List<int>> SetActiveCustomizations(List<int> IDs)
        {
            var Conf = ConfigReader.Config;

            foreach (var ID in IDs)
                if (Conf.CustomizationItems.TryGetValue(ID, out var Item) // The item ID is correct and known...
                    && (State.OwnedItems.TryGetValue(ID, out _) || Item.Price == 0)) // ... and the player actually owns the item or it's free
                    State.ActiveItems[Item.Category] = ID;

            return Task.FromResult(State.ActiveItems.Select(KV => KV.Value).ToList());
        }

        public async Task<PurchaseResult> PurchaseCustomizationItem(int ItemID)
        {
            if (State.OwnedItems.ContainsKey(ItemID))
                return new PurchaseResult(EPurchaseResult.AlreadyOwnsItem);

            var Conf = ConfigReader.Config;

            if (!Conf.CustomizationItems.TryGetValue(ItemID, out var Item) || !Item.IsPurchasable)
                return new PurchaseResult(EPurchaseResult.InvalidID);

            if (State.Funds[Item.PriceCurrency.AsIndex()] < (ulong)Item.Price)
                return new PurchaseResult(EPurchaseResult.InsufficientFunds);

            State.Funds[Item.PriceCurrency.AsIndex()] -= (ulong)Item.Price;
            State.OwnedItems[ItemID] = 1;
            await WriteStateAsync();

            return new PurchaseResult(EPurchaseResult.Success) { OwnedItems = State.OwnedItems, TotalFunds = (ulong[])State.Funds.Clone() };
        }

        public async Task<PurchaseResult> PurchasePack(int PackID)
        {
            var Conf = ConfigReader.Config;

            if (!Conf.Packs.TryGetValue(PackID, out var Pack) || Pack.PriceCurrency == CurrencyType.IRR) //?? implement purchase limits here
                return new PurchaseResult(EPurchaseResult.InvalidID);

            if (State.Funds[Pack.PriceCurrency.AsIndex()] < (ulong)Pack.Price)
                return new PurchaseResult(EPurchaseResult.InsufficientFunds);

            State.Funds[Pack.PriceCurrency.AsIndex()] -= (ulong)Pack.Price;

            foreach (var Cur in Pack.Contents_Currency)
                if (Cur.Key != CurrencyType.IRR) // Dafuq, DB?
                    State.Funds[Cur.Key.AsIndex()] += (ulong)Cur.Value;

            foreach (var Item in Pack.Contents_CustomizationItem)
                State.OwnedItems[Item.ID] = 1;

            await WriteStateAsync();

            return new PurchaseResult(EPurchaseResult.Success) { OwnedItems = State.OwnedItems, TotalFunds = (ulong[])State.Funds.Clone() };
        }

        public async Task<PurchaseResult> PurchaseIabPack(int PackID, string IabToken)
        {
            if (IabToken == null)
                throw new ArgumentNullException(nameof(IabToken));

            if (State.ProcessedIabTokens.Contains(IabToken))
                return new PurchaseResult(EPurchaseResult.IabTokenAlreadyProcessed);

            var Conf = ConfigReader.Config;

            if (!Conf.Packs.TryGetValue(PackID, out var Pack)) //?? implement purchase limits here
                return new PurchaseResult(EPurchaseResult.InvalidID);

            State.ProcessedIabTokens.Add(IabToken);

            foreach (var Cur in Pack.Contents_Currency)
                if (Cur.Key != CurrencyType.IRR) // Dafuq, DB?
                    State.Funds[Cur.Key.AsIndex()] += (ulong)Cur.Value;

            foreach (var Item in Pack.Contents_CustomizationItem)
                State.OwnedItems[Item.ID] = 1;

            await WriteStateAsync();

            return new PurchaseResult(EPurchaseResult.Success) { OwnedItems = State.OwnedItems, TotalFunds = (ulong[])State.Funds.Clone() };
        }

        public Task<Immutable<Dictionary<int, int>>> GetPurchaseLimits()
        {
            return Task.FromResult(new Dictionary<int, int>().AsImmutable()); //??
        }


        public async Task<SpinResults> RollSpinner()
        {
            if (DateTime.Now < State.NextSpinTime)
                return null;

            var Result = ConfigReader.Config.SpinRewardRandom.Get(Random.Next());

            State.Funds[Result.RewardType.AsIndex()] += (ulong)Result.Count;

            State.NextSpinTime = DateTime.Now.Add(ConfigReader.Config.ConfigValues.SpinnerInterval);
            State.LastSpinResult = new CurrencyAmount(Result.RewardType, (ulong)Result.Count);
            await WriteStateAsync();

            return new SpinResults { RewardID = Result.ID, TotalFunds = (ulong[])State.Funds.Clone(), TimeUntilNextSpin = State.NextSpinTime - DateTime.Now };
        }

        public async Task<SpinResults> RollMultiplierSpinner()
        {
            if (State.LastSpinResult == null)
                return null;

            var Result = ConfigReader.Config.SpinMultiplierRandom.Get(Random.Next());

            State.Funds[State.LastSpinResult.Type.AsIndex()] += State.LastSpinResult.Amount * (ulong)(Result.Multiplier - 1);

            State.LastSpinResult = null;
            await WriteStateAsync();

            return new SpinResults { RewardID = Result.ID, TotalFunds = (ulong[])State.Funds.Clone() };
        }

        public async Task<ulong> TakeVideoAdReward()
        {
            State.Funds[CurrencyType.Gold.AsIndex()] += ConfigReader.Config.ConfigValues.VideoAdReward;
            await WriteStateAsync();
            return State.Funds[CurrencyType.Gold.AsIndex()];
        }

        public async Task<bool> SetName(string Name)
        {
            if (await UserProfileUtils.SetNameIfUnique(GrainFactory, this, Name))
            {
                State.Name = Name;
                State.IsNameSet = true;
                await WriteStateAsync();
                return true;
            }

            return false;
        }

        async Task<bool> ResetToday(bool writeState = true)
        {
            var tomorrow = DateTime.Today.AddDays(1);
            if (State.TodayEnd != tomorrow)
            {
                State.TodayEnd = tomorrow;
                State.Statistics[UserStatistics.CheckersHitToday.AsIndex()] = 0;
                State.Statistics[UserStatistics.CheckersHitRewardCollectedForToday.AsIndex()] = 0;

                if (writeState)
                    await WriteStateAsync();

                return true;
            }

            return false;
        }

        public async Task<Tuple<ulong, bool>> GetCheckerHitReward()
        {
            bool rewardsGiven = false;

            if (!await ResetToday() && State.Statistics[UserStatistics.CheckersHitRewardCollectedForToday.AsIndex()] == 0 &&
                State.Statistics[UserStatistics.CheckersHitToday.AsIndex()] >= ConfigReader.Config.ConfigValues.NumCheckersToHitPerDayForReward)
            {
                State.Funds[CurrencyType.Gold.AsIndex()] += ConfigReader.Config.ConfigValues.CheckerHitRewardPerDay;
                State.Statistics[UserStatistics.CheckersHitRewardCollectedForToday.AsIndex()] = 1;

                await WriteStateAsync();

                rewardsGiven = true;
            }

            return new Tuple<ulong, bool>(State.Funds[CurrencyType.Gold.AsIndex()], rewardsGiven);
        }
    }
}
