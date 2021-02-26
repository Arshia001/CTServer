using LightMessage.Common.MessagingProtocol;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public interface IClientAuthorizer : IGrainWithIntegerKey
    {
        Task<Guid?> Authorize(AuthRequestMessage AuthMessage);
    }
}
