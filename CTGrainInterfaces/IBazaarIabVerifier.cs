using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public interface IBazaarIabVerifier : IGrainWithIntegerKey
    {
        Task<bool?> VerifyBazaarPurchase(string Sku, string Token);
    }
}
