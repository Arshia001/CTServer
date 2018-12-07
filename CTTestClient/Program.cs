using BackgammonLogic;
using CTGrainInterfaces;
using CTGrains;
using LightMessage.Client;
using LightMessage.Client.EndPoints;
using LightMessage.Common.Connection;
using LightMessage.Common.Messages;
using LightMessage.Common.ProtocolMessages;
using LightMessage.Common.Util;
using Orleans;
using Orleans.Runtime;
using Orleans.Runtime.Configuration;
using OrleansBondUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CTOrleansTestClient
{
    //class Key
    //{
    //    public class Comp : IComparer<Key>
    //    {
    //        public int Compare(Key x, Key y)
    //        {
    //            var Res = x.II.CompareTo(y.II);
    //            if (Res == 0)
    //                Res = x.Id.CompareTo(y.Id);

    //            return Res;
    //        }
    //    }

    //    public int II;
    //    public Guid Id;

    //    public override string ToString()
    //    {
    //        return $"{II} {Id}";
    //    }
    //}

    class Program
    {
        static void Main(string[] args)
        {
            var b = new BackgammonGameLogic();
            b.RestoreGameState(Color.White, GameState.WaitMove, new sbyte[] { -15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 14 }, new byte[] { 0, 0 }, new byte[] { 0, 1 }, new byte[] { 2, 6 }, new byte[] { }, 0, new Move[] { new Move(18, -1, false, 6) });
            b.UndoLastMove();

            //var PC = new PersianCalendar();
            //var D = new DateTime(2018, 3, 20, 0, 0, 0);
            //Console.WriteLine($"{PC.GetYear(D)}/{PC.GetMonth(D)}/{PC.GetDayOfMonth(D)}");

            //var G = new BackgammonGameLogic();
            //G.RestoreGameState(Color.White, GameState.WaitMove, new sbyte[]
            //{
            //    -15,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,0
            //}, new byte[] { 0, 0 }, new byte[] { 0, 14 }, new byte[] { 1, 4 }, new byte[] { 1, 4 }, 0);

            //var XX = G.GetMoveList(21, -1);

            //G.ServerDiceRolled(4, 4);

            //Console.WriteLine(G.AIMakeAllMoves().Aggregate("", (acc, t) => acc + $",({t.Item1},{t.Item2}) "));
            //Console.WriteLine(G.MakeMove(16, 20, out _));
            //Console.WriteLine(G.MakeMove(16, 20, out _));

            //G.ServerDiceRolled(6, 6);
            //var Moves = G.AIMakeAllMoves();

            //var SW = new Stopwatch();
            //SW.Start();
            //int NumMoves = 0;
            //for (int i = 0; i < 100; ++i)
            //{
            //    var G = new BackgammonGameLogic();
            //    while (true)
            //    {
            //        switch (G.State)
            //        {
            //            case GameState.Init:
            //                G.RollInitDice(out var T, out var W, out var B);
            //                break;

            //            case GameState.WaitDice:
            //                G.RollDice(out var F, out var S);
            //                break;

            //            case GameState.WaitMove:
            //                ++NumMoves;
            //                var M = G.AIMakeAllMoves();
            //                foreach (var TT in M)
            //                    G.MakeMove(TT.Item1, TT.Item2);
            //                break;

            //            case GameState.Complete:
            //                goto EndLoop;
            //        }
            //    }
            //EndLoop:;
            //}
            //SW.Stop();
            //Console.WriteLine($"{NumMoves} {SW.ElapsedMilliseconds} {SW.ElapsedMilliseconds / (float)NumMoves}");


            //var C = new EndPointClient(new ConsoleLogProvider(LogLevel.Verbose));
            //var P = C.CreateProxy("sys");
            //C.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1020), CancellationToken.None, new AuthRequestMessage(), true).Wait();
            //P.SendInvocationForReply("start", CancellationToken.None).Wait();
            //var Res = P.SendInvocationForReply("lb", CancellationToken.None, Param.Boolean(false)).Result;

            //Thread.Sleep(1000000);


            //var clientConfig = ClientConfiguration.LoadFromFile("ClientConfiguration.xml");
            //IClusterClient Client;

            //while (true)
            //    try
            //    {
            //        Client = new ClientBuilder().UseConfiguration(clientConfig).Build();
            //        Client.Connect().Wait();
            //        break;
            //    }
            //    catch (Exception Ex)
            //    {
            //        Console.WriteLine("Unable to connect due to exception: " + Ex.ToString());
            //        Thread.Sleep(1000);
            //        continue;
            //    }

            //var pp = Client.GetGrain<ITestGrain>(Guid.Empty).Deserialize(typeof(UserProfileState), bb).Result;

            //Client.GetGrain<ILeaderBoard>("sdifhkadskfldsaarvffsdfasdfsfeduhsj").TestSerialization(1000000).Wait();



            //var SW = new Stopwatch();
            //SW.Start();
            //var R = new Random();
            //// Client.GetGrain<ITestGrain>(Guid.Empty).TestIab("testsku1", "34123sdfasdf3kgh14234").Wait();
            //for (int i = 0; i < 100000; ++i)
            //    Client.GetGrain<ILeaderBoard>("asdfsadfsafa").Set(Guid.NewGuid(), (ulong)R.Next()).Wait();
            //SW.Stop();
            //Console.WriteLine(SW.Elapsed);

            //var TG = Client.GetGrain<ITestGrain>(Guid.Empty);
            //TG.Save().Wait();

            //var Mid = Guid.NewGuid();
            //var LB = Client.GetGrain<ILeaderBoard>("xxx");
            //LB.Add(Guid.NewGuid(), 100).Wait();
            //LB.Add(Guid.NewGuid(), 200).Wait();
            //LB.Add(Guid.NewGuid(), 300).Wait();
            //LB.Add(Guid.NewGuid(), 400).Wait();
            //LB.Add(Mid, 500).Wait();
            //LB.Add(Guid.NewGuid(), 600).Wait();
            //LB.Add(Guid.NewGuid(), 700).Wait();
            //LB.Add(Guid.NewGuid(), 800).Wait();
            //LB.Add(Guid.NewGuid(), 900).Wait();

            //Console.WriteLine(LB.GetRank(Mid).Result);
            //Console.WriteLine("-----");

            //foreach (var l in LB.GetScoresAround(Mid, 2).Result)
            //    Console.WriteLine($"{l.Id} {l.Score} {l.Rank}");
            //Console.WriteLine("-----");

            //foreach (var l in LB.GetTopScores(4).Result)
            //    Console.WriteLine($"{l.Id} {l.Score} {l.Rank}");

            //Console.ReadLine();

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
