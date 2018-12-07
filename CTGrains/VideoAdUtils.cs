using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CTGrains
{
    static class VideoAdUtils
    {
        static HttpClient Client = new HttpClient();

        public static Task<bool> VerifyTapsellAd(string AdID)
        {
            // The idiots just can't handle their shit, the API keeps failing to verify genuine ads, so we'll disable this check for now
            return Task.FromResult(true);

            //try
            //{
            //    var Content = new StringContent($@"{{""suggestionId"":""{AdID}""}}", Encoding.ASCII, "application/json");
            //    var CTS = new CancellationTokenSource();
            //    CTS.CancelAfter(TimeSpan.FromSeconds(3));
            //    var Resp = await Client.PostAsync("http://api.tapsell.ir/v2/suggestions/validate-suggestion", Content, CTS.Token);
            //    var ResultJson = await Resp.Content.ReadAsStringAsync();
            //    var Result = Newtonsoft.Json.Linq.JObject.Parse(ResultJson);
            //    return (bool)Result["valid"];
            //}
            //catch
            //{
            //    // If we can't access the tapsell API (looking more likely by the second :| )
            //    // we'll just let the player take the reward. Better to have a seldom-usable
            //    // security problem than a lot of unsatisfied users.
            //    return true;
            //}
        }
    }
}
