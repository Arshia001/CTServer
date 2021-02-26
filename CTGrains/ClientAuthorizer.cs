using CTGrainInterfaces;
using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LightMessage.Common.MessagingProtocol;
using Cassandra;
using OrleansCassandraUtils.Utils;

namespace CTGrains
{
    [StatelessWorker]
    public class ClientAuthorizer : Grain, IClientAuthorizer
    {
        //static object LockObj = new object();
        //static ISession Session;
        //static PreparedStatement InsertClientStatsQuery;


        //public override async Task OnActivateAsync()
        //{
        //    if (Session == null || InsertClientStatsQuery == null)
        //        lock (LockObj)
        //        {
        //            if (Session == null)
        //                Session = await CassandraSessionFactory.CreateSession(CTSettings.Instance.SystemDataConnectionString);

        //            if (InsertClientStatsQuery == null)
        //                InsertClientStatsQuery = await Session.PrepareAsync("update ") ....... CONTINUE HERE, DESIGN QUERY AND TABLE LAYOUT
        //        }
        //}


        public async Task<Guid?> Authorize(AuthRequestMessage AuthMessage)
        {
            //?? add google play sign-in

            // Client already has an ID, use it as profile ID and check if it's in use
            var ClientID = AuthMessage.Params.Count > 0 ? AuthMessage.Params[0].AsGuid : default;
            if (ClientID.HasValue)
                return await GrainFactory.GetGrain<IUserProfile>(ClientID.Value).IsInitialized() ? ClientID : null;

            // Assign new, unused profile ID
            IUserProfile Profile;
            while (true)
            {
                ClientID = Guid.NewGuid();
                Profile = GrainFactory.GetGrain<IUserProfile>(ClientID.Value);
                if (!await Profile.IsInitialized())
                    break;
            }
            return ClientID;
        }
    }
}
