using LightMessage.Common.MessagingProtocol;
using Orleans;
using Orleans.CodeGeneration;
using Orleans.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    //[Serializer(typeof(FileStream))]
    //class CopyByReferenceSerializer
    //{
    //    [CopierMethod]
    //    public static object DeepCopier(object original, ICopyContext context)
    //    {
    //        return original;
    //    }

    //    [SerializerMethod]
    //    public static void Serializer(object untypedInput, ISerializationContext context, Type expected)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    [DeserializerMethod]
    //    public static object Deserializer(Type expected, IDeserializationContext context)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    public interface ITestObserver : IGrainObserver
    {
        void Event(string Data);
    }

    public interface ITestGrain : IGrainWithGuidKey
    {
        Task<int> Add(int a, int b);
        Task SetReminder();

        Task Sub(ITestObserver Observer);
        Task Pub(string Data);

        //Task TestStream();
        //Task InternalWriteToStream(Stream Stream);

        Task<ProtocolMessage> TestMessage(ProtocolMessage Message);

        Task<ITestGrain> GetSelf(ITestGrain TG);

        Task TestDB();

        Task Save();

        Task TestIab(string Sku, string Token);

        //Task Reactivate();

        Task TestService();

        Task<object> Deserialize(Type type, byte[] data);
    }

    public interface ITestGrain2 : ITestGrain
    {

    }

    public interface ITestGenericGrain<T1, T2, T3> : IGrainWithGuidKey
    {
    }
}
