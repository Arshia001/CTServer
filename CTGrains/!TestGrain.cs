using CTGrainInterfaces;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans.Runtime;
using System.IO;
using LightMessage.Common.MessagingProtocol;
using System.Threading;
using Cassandra;
using Bond;
using OrleansBondUtils;
using Bond.Tag;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CTGrains
{
    [Schema, BondSerializationTag("#__tg1__")]
    public class C : IGenericSerializable
    {
        [Id(0)]
        public int i { get; set; }
    }

    [Schema, BondSerializationTag("#__tg2__")]
    public class C2 : IGenericSerializable
    {
        [Id(0), Type(typeof(nullable<C>))]
        public C c { get; set; }

        public C2()
        {
        }
    }

    public class TestGrain : Grain<C2>, ITestGrain, ITestGrain2, IRemindable
    {
        GrainObserverManager<ITestObserver> SubscriptionManager = new GrainObserverManager<ITestObserver>();

        ILogger Logger;

        public TestGrain(ILogger<TestGrain> logger)
        {
            Logger = logger;
        }

        public Task<int> Add(int a, int b)
        {
            Logger.Info("delaying deactivation");
            DelayDeactivation(TimeSpan.FromMinutes(2));

            return Task.FromResult(a + b);
        }

        public Task<ITestGrain> GetSelf(ITestGrain TG)
        {
            return Task.FromResult(TG);
        }

        public Task Pub(string Data)
        {
            SubscriptionManager.Notify(o => o.Event(Data));
            return Task.CompletedTask;
        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
        {
            return Task.CompletedTask;
        }

        public Task Save()
        {
            return WriteStateAsync();
        }

        public Task SetReminder()
        {
            return RegisterOrUpdateReminder("sample_reminder_name", TimeSpan.FromDays(1), TimeSpan.FromMinutes(5));
        }

        public Task Sub(ITestObserver Observer)
        {
            SubscriptionManager.Subscribe(Observer);
            ((IGrainReferenceConverter)ServiceProvider.GetService(typeof(IGrainReferenceConverter))).GetGrainFromKeyString(((GrainReference)Observer).ToKeyString()).AsReference<ITestObserver>().Event("asdf");
            return Task.CompletedTask;
        }

        public async Task TestDB()
        {
            var Session = await OrleansCassandraUtils.Utils.CassandraSessionFactory.CreateSession("Contact Point=localhost;Username=ct_server_dev_access;Password=V6Ji9e9DfwHGiWoohL;KeySpace=ct_server_dev;Compression=LZ4");

        }

        //public Task TestStream()
        //{
        //    var FS = File.OpenWrite("C:\\Test\\x.txt");
        //    return GrainFactory.GetGrain<ITestGrain>(Guid.NewGuid()).InternalWriteToStream(FS).ContinueWith(t => FS.Close());
        //}

        //public Task InternalWriteToStream(Stream Stream)
        //{
        //    var SW = new StreamWriter(Stream);
        //    SW.WriteLine("Hello!");
        //    SW.Flush();
        //    return Task.CompletedTask;
        //}

        public Task<ProtocolMessage> TestMessage(ProtocolMessage Message)
        {
            return Task.FromResult<ProtocolMessage>(new AckMessage(1));
        }

        public Task TestIab(string Sku, string Token)
        {
            throw new NotImplementedException();
            // return IabUtils.VerifyBazaarPurchase(Sku, Token, ServiceProvider.GetRequiredService<IConfigReader>().Config.ConfigValues, GetLogger("IAB"));
        }

        //public Task Reactivate()
        //{
        //    GetLogger().Info("--- reactivated -----------------------");
        //    return Task.CompletedTask;
        //}

        //public override Task OnActivateAsync()
        //{
        //    GetLogger().Info("--- active! ---------------------------");

        //    return base.OnActivateAsync();
        //}

        //public override async Task OnDeactivateAsync()
        //{
        //    GetLogger().Info("--- calling self ----------------------");
        //    GrainFactory.GetGrain<ITestGrain>(this.GetPrimaryKey()).Reactivate().Ignore();

        //    await base.OnDeactivateAsync();
        //}

        public Task TestService()
        {
            return Task.CompletedTask;
            // return ServiceProvider.GetRequiredService<IKeepAliveServiceClient>().DoSomething();
        }

        public Task<object> Deserialize(Type type, byte[] data)
        {
            return Task.FromResult(BondSerializer.Deserialize(type, new MemoryStream(data)));
        }
    }

    public class TestGenericGrain<T1, T2, T3> : Grain, ITestGenericGrain<T1, T2, T3>
    {

    }
}
