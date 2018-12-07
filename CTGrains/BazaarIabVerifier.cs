using CTGrainInterfaces;
using Orleans;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Orleans.Runtime;
using Microsoft.Extensions.Logging;

namespace CTGrains
{
    public class BazaarIabVerifier : Grain, IBazaarIabVerifier
    {
        string BazaarAccessCode = null;
        HttpClient Client = new HttpClient();

        IConfigReader ConfigReader;
        ILogger Logger;


        public BazaarIabVerifier(IConfigReader ConfigReader, ILogger<BazaarIabVerifier> Logger)
        {
            this.ConfigReader = ConfigReader;
            this.Logger = Logger;
        }

        async Task<bool> RefreshBazaarAccessCode()
        {
            try
            {
                if (BazaarAccessCode != null)
                    return true;

                var Conf = ConfigReader.Config.ConfigValues;
                var Values = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "client_id", Conf.BazaarClientID },
                    { "client_secret", Conf.BazaarClientSecret },
                    { "refresh_token", Conf.BazaarRefreshCode }
                };
                var Content = new FormUrlEncodedContent(Values);

                var Resp = await Client.PostAsync("https://pardakht.cafebazaar.ir/devapi/v2/auth/token/", Content);
                var ResultJson = await Resp.Content.ReadAsStringAsync();
                var Result = Newtonsoft.Json.Linq.JObject.Parse(ResultJson);

                if (Resp.IsSuccessStatusCode)
                {
                    BazaarAccessCode = (string)Result["access_token"] ?? throw new Exception("failed to read access_token from response");
                    return true;
                }
                else
                {
                    Logger.Error(0, $"Failed to refresh bazaar access code with status {Resp.StatusCode} and error response {Result["error"]} - {Result["error_description"]}");
                    return false;
                }
            }
            catch (Exception Ex)
            {
                Logger.Error(0, "Failed to refresh bazaar access code", Ex);
                return false;
            }
        }

        public async Task<bool?> VerifyBazaarPurchase(string Sku, string Token)
        {
            if (BazaarAccessCode == null)
            {
                await RefreshBazaarAccessCode();
                if (BazaarAccessCode == null)
                {
                    Logger.Error(0, "Failed to get Bazaar access code, check configured credentials");
                    return null;
                }
            }

            try
            {
                var Resp = await Client.GetAsync($"https://pardakht.cafebazaar.ir/devapi/v2/api/validate/com.sperlous.ctclient/inapp/{Sku}/purchases/{Token}/?access_token={BazaarAccessCode}");

                if (Resp.IsSuccessStatusCode)
                {
                    Logger.Info(0, $"Bazaar purchase {Sku} {Token} verified");
                    // We could get additional info regarding the purchase here, but I don't think it's necessary
                    return true;
                }
                else
                {
                    var ResultJson = await Resp.Content.ReadAsStringAsync();
                    var Result = Newtonsoft.Json.Linq.JObject.Parse(ResultJson);

                    Logger.Error(0, $"Validating bazaar purchase {Sku} {Token} failed with {Resp.StatusCode} {ResultJson}");

                    var Error = (string)Result["error"];
                    if (Error == "not_found")
                        return false;
                    else if (Error == "invalid_credentials")
                    {
                        BazaarAccessCode = null;
                        return await VerifyBazaarPurchase(Sku, Token);
                    }
                    else
                        return null;
                }
            }
            catch (Exception Ex)
            {
                Logger.Error(0, "Failed to get purchase status from Bazaar", Ex);
                return null;
            }
        }
    }
}
