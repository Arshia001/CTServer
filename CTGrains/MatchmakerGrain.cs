using C5;
using CTGrainInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrains
{
    class MatchmakerGrain : Grain, IMatchmakerGrain
    {
        class MatchmakingData
        {
            public int Level;
            public int WindowSize;

            public bool CompatibleWith(int Level)
            {
                return this.Level - WindowSize <= Level &&
                    Level <= this.Level + WindowSize;
            }

            public override string ToString()
            {
                return $"l:{Level}, ws:{WindowSize}";
            }
        }

        class MatchmakingOrderEntry : IComparable<MatchmakingOrderEntry>
        {
            public int Level;
            public Guid ID;

            public int CompareTo(MatchmakingOrderEntry other)
            {
                var Res = Level.CompareTo(other.Level);
                if (Res == 0)
                    Res = ID.CompareTo(other.ID);

                return Res;
            }
        }


        ILogger Logger;
        Dictionary<Guid, MatchmakingData> Queue = new Dictionary<Guid, MatchmakingData>();
        TreeSet<MatchmakingOrderEntry> OrderByLevel = new TreeSet<MatchmakingOrderEntry>();
        Random Random = new Random();

        IConfigReader ConfigReader;


        public MatchmakerGrain(IConfigReader ConfigReader, ILogger<MatchmakerGrain> Logger)
        {
            this.ConfigReader = ConfigReader;
            this.Logger = Logger;
        }

        public override Task OnActivateAsync()
        {
            RegisterTimer(ProcessQueue, null, ConfigReader.Config.ConfigValues.MatchMakingInterval, ConfigReader.Config.ConfigValues.MatchMakingInterval);

            return base.OnActivateAsync();
        }

        async Task ProcessQueue(object State)
        {
            var ToRemove = new System.Collections.Generic.HashSet<Guid>();
            var EndPoint = GrainFactory.GetGrain<IMatchmakingEndPoint>(0);

            var MyID = (int)this.GetPrimaryKeyLong();

            using (Logger.BeginScope($"Matchmaking queue {this.GetPrimaryKeyLong()}")) //?? test
            {
#if DEBUG
                if (Queue.Any())
                    Logger.Info("Starting matchmaking sequence");
#endif

                foreach (var KV in Queue)
                {
                    try
                    {
#if DEBUG
                        Logger.Info($"Processing client {KV.Key} with matchmaking data {KV.Value}");
                        Logger.Info("Queue: " + string.Join(", ", Queue.Select(kv => $"({kv.Key}:{kv.Value})")));
                        Logger.Info("OrderByLevel: " + string.Join(", ", OrderByLevel.Select(i => $"[{i.ID},{i.Level}]")));
                        Logger.Info("ToRemove: " + string.Join(", ", ToRemove.Select(i => i.ToString())));
#endif

                        var ID = KV.Key;
                        var Data = KV.Value;

                        if (!await GrainFactory.GetGrain<IUserProfile>(ID).IsInQueue(MyID))
                        {
#if DEBUG
                            Logger.Info($"No longer in queue, won't process");
#endif
                            OrderByLevel.Remove(new MatchmakingOrderEntry { ID = ID, Level = Data.Level });
                            ToRemove.Add(ID);
                            continue;
                        }

                        if (ToRemove.Contains(ID))
                        {
#if DEBUG
                            Logger.Info($"Already matched, won't process");
#endif
                            continue;
                        }

                        var BottomEntry = new MatchmakingOrderEntry { Level = Data.Level - Data.WindowSize, ID = Guid.Empty };
                        var TopEntry = new MatchmakingOrderEntry { Level = Data.Level + Data.WindowSize + 1, ID = Guid.Empty };

                        var BottomIdx = OrderByLevel.CountTo(BottomEntry);
                        var TopIdx = OrderByLevel.CountTo(TopEntry);
                        var StartIdx = Random.Next(TopIdx - BottomIdx);

                        MatchmakingOrderEntry Match = null;
                        // This is perhaps not the best possible solution, but it's the most simple one.
                        // If multiplayer is disabled, the matchmaking timer should be set to a much lower value.
                        if (ConfigReader.Config.ConfigValues.IsMultiplayerAllowed)
                        {
                            foreach (var E in OrderByLevel[StartIdx, TopIdx - StartIdx])
                            {
#if DEBUG
                                Logger.Info($"Trying {E.ID}");
#endif
                                if (E.ID != ID && !ToRemove.Contains(E.ID) && Queue[E.ID].CompatibleWith(Data.Level))
                                {
#if DEBUG
                                    Logger.Info($"Matching with {E.ID} with matchmaking data {Queue[E.ID]}");
#endif
                                    Match = E;
                                    break;
                                }
                            }

                            if (Match == null)
                            {
                                foreach (var E in OrderByLevel[BottomIdx, StartIdx - BottomIdx])
                                {
                                    if (E.ID != ID && !ToRemove.Contains(E.ID) && Queue[E.ID].CompatibleWith(Data.Level))
                                    {
#if DEBUG
                                        Logger.Info($"Matching with {E.ID} with matchmaking data {Queue[E.ID]}");
#endif
                                        Match = E;
                                        break;
                                    }
                                }
                            }

                            if (Match == null)
                            {
#if DEBUG
                                Logger.Info($"No match found");
#endif
                                if (Data.WindowSize - ConfigReader.Config.ConfigValues.MatchMakingInitWindowSize < ConfigReader.Config.ConfigValues.MatchMakingWindowIncrement * ConfigReader.Config.ConfigValues.MatchMakingNumUnsuccessfulAttemptsToMatchAI)
                                {
#if DEBUG
                                    Logger.Info($"Incrementing window size");
#endif
                                    Data.WindowSize += ConfigReader.Config.ConfigValues.MatchMakingWindowIncrement;
                                }
                                else
                                {
#if DEBUG
                                    Logger.Info($"Window size too large, matching with AI");
#endif
                                    Match = new MatchmakingOrderEntry { ID = Guid.Empty };
                                }
                            }
                        }
                        else
                        {
#if DEBUG
                            Logger.Info($"Multiplayer disabled, matching with AI");
#endif
                            Match = new MatchmakingOrderEntry { ID = Guid.Empty };
                        }

                        if (Match != null)
                        {
                            var GameID = (int)this.GetPrimaryKeyLong();
                            var GameConfig = ServiceProvider.GetRequiredService<IConfigReader>().Config.Games[GameID];

                            var Game = GrainFactory.GetGrain<IBackgammonGame>(Guid.NewGuid());

#if DEBUG
                            Logger.Info($"Matched {KV.Key} with {Match.ID}, sending both to game with ID {Game.GetPrimaryKey()}");
#endif

                            var Profiles = Match.ID == Guid.Empty ?
                                new List<IUserProfile>
                                {
                            GrainFactory.GetGrain<IUserProfile>(ID),
                                } :
                                new List<IUserProfile>
                                {
                            GrainFactory.GetGrain<IUserProfile>(ID),
                            GrainFactory.GetGrain<IUserProfile>(Match.ID)
                                };

                            try
                            {
                                if (!await Profiles[0].EnterGame(Game, GameConfig.EntranceFee))
                                {
#if DEBUG
                                    Logger.Info($"{Profiles[0].GetPrimaryKey()} failed to enter game, will cancel");
#endif
                                    EndPoint.SendRemovedFromQueue(ID).Ignore();
                                    ToRemove.Add(ID);
                                    OrderByLevel.Remove(new MatchmakingOrderEntry { ID = ID, Level = Data.Level });
                                    continue;
                                }
                                else if (Profiles.Count > 1 && !await Profiles[1].EnterGame(Game, GameConfig.EntranceFee))
                                {
#if DEBUG
                                    Logger.Info($"{Profiles[1].GetPrimaryKey()} failed to enter game, will cancel");
#endif
                                    EndPoint.SendRemovedFromQueue(Match.ID).Ignore();
                                    ToRemove.Add(Match.ID);
                                    OrderByLevel.Remove(Match);
                                    await Profiles[0].LeaveGame(Game);
                                    continue;
                                }
                                else
                                {
                                    await Game.Start(Profiles.Select(up => up.GetPrimaryKey()).ToList(), (int)this.GetPrimaryKeyLong());

                                    ToRemove.Add(ID);
                                    OrderByLevel.Remove(new MatchmakingOrderEntry { ID = ID, Level = Data.Level });
                                    if (Match.ID != Guid.Empty)
                                    {
                                        ToRemove.Add(Match.ID);
                                        OrderByLevel.Remove(Match);
                                    }

#if DEBUG
                                    Logger.Info($"Game started");
#endif
                                }
                            }
                            catch (Exception Ex)
                            {
                                await Profiles[0].LeaveGame(Game);
                                await Profiles[1].LeaveGame(Game);
                                Logger.Error(0, "Failed to start game", Ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(0, "######### EXCEPTION IN MATCHMAKING", ex);
                    }
                }

                foreach (var ID in ToRemove)
                    Queue.Remove(ID);
            }
        }

        public Task<bool> EnterQueue(IUserProfile User, Immutable<UserProfileState> ProfileInfo)
        {
            if (Queue.ContainsKey(User.GetPrimaryKey()))
                return Task.FromResult(false);

            var ID = User.GetPrimaryKey();
            var data = new MatchmakingData() { Level = (int)ProfileInfo.Value.Level - (int)ProfileInfo.Value.WinFactor, WindowSize = ConfigReader.Config.ConfigValues.MatchMakingInitWindowSize };
            Queue.Add(ID, data);
            OrderByLevel.Add(new MatchmakingOrderEntry { ID = ID, Level = data.Level });
            return Task.FromResult(true);
        }

        //public Task LeaveQueue(IUserProfile User, Immutable<UserProfileState> ProfileInfo)
        //{
        //    var ID = User.GetPrimaryKey();
        //    Queue.Remove(ID);
        //    OrderByLevel.Remove(new MatchmakingOrderEntry { ID = ID, Level = (int)ProfileInfo.Value.Level });
        //    return Task.CompletedTask;
        //}
    }
}
