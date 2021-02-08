using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using System.Web;

using Newtonsoft.Json;
using SKM.V3;
using SKM.V3.Models;

using System.Collections.Concurrent;

namespace LicenseServer
{
    public class Helpers
    {
        public static string ProcessActivateRequest(byte[] stream, Dictionary<LAKey, LAResult> licenseCache, int cacheLength, HttpWebRequest newRequest, HttpListenerContext context, ConcurrentDictionary<LAKey, string> keysToUpdate)
        {
            string bodyParams = System.Text.Encoding.Default.GetString(stream);
            var nvc = HttpUtility.ParseQueryString(bodyParams);

            int productId = -1;
            int.TryParse(nvc.Get("ProductId"), out productId);
            int signMethod = -1;
            int.TryParse(nvc.Get("SignMethod"), out signMethod);
            var licenseKey = nvc.Get("Key");
            var machineCode = nvc.Get("MachineCode");

            LAResult result = null;

            var key = new LAKey { Key = licenseKey, ProductId = productId, SignMethod = signMethod };
            
            if (licenseCache.TryGetValue(key, out result) && result?.LicenseKey?.ActivatedMachines.Any(x=> x.Mid == machineCode) == true && cacheLength > 0)
            {
                TimeSpan ts = DateTime.UtcNow - result.SignDate;
                if (ts.Days >= cacheLength)
                {
                    // need to obtain new license

                    result.Response = ObtainNewLicense(stream, newRequest, context);

                    if (signMethod == 1)
                    {
                        var resultObject = JsonConvert.DeserializeObject<RawResponse>(result.Response);
                        var license2 = JsonConvert.DeserializeObject<LicenseKeyPI>(resultObject.LicenseKey).ToLicenseKey();
                        result.SignDate = license2.SignDate;
                        result.LicenseKey = license2;
                    }
                    else
                    {
                        var resultObject = JsonConvert.DeserializeObject<KeyInfoResult>(result.Response);
                        result.SignDate = resultObject.LicenseKey.SignDate;
                        result.LicenseKey = resultObject.LicenseKey;
                    }

                    keysToUpdate.AddOrUpdate(key, x=> result.Response, y=> result.Response);


                    return $"Cache updated for license '{licenseKey}' and machine code '{machineCode}'.";

                }
                else
                {
                    ReturnResponse(result.Response, context);
                    return $"Retrieved cached version of the license '{licenseKey}' and machine code '{machineCode}'.";
                }
            }
            else
            {
                result = new LAResult();

                result.Response = ObtainNewLicense(stream, newRequest, context);

                if (cacheLength > 0)
                {
                    if (signMethod == 1)
                    {
                        var resultObject = JsonConvert.DeserializeObject<RawResponse>(result.Response);
                        var license2 = JsonConvert.DeserializeObject<LicenseKeyPI>(resultObject.LicenseKey).ToLicenseKey();
                        result.SignDate = license2.SignDate;
                        result.LicenseKey = license2;
                    }
                    else
                    {
                        var resultObject = JsonConvert.DeserializeObject<KeyInfoResult>(result.Response);
                        result.SignDate = resultObject.LicenseKey.SignDate;
                        result.LicenseKey = resultObject.LicenseKey;
                    }

                    licenseCache.Add(key, result);
                    return $"Added to the cache the license '{licenseKey}' and machine code '{machineCode}'.";
                }
                return null;
            }
        }


        public static bool LoadLicenseFromFile(Dictionary<LAKey, LAResult> licenseCache, string pathToFile)
        {
            try
            {
                var file = System.IO.File.ReadAllText(pathToFile);

                var response = Newtonsoft.Json.JsonConvert.DeserializeObject<RawResponse>(file);

                if (!string.IsNullOrEmpty(response.LicenseKey))
                {
                    // SignMethod = 1

                    var licenseBytes = Convert.FromBase64String(response.LicenseKey);
                    var licenseKey = JsonConvert.DeserializeObject<LicenseKeyPI>(System.Text.UTF8Encoding.UTF8.GetString(licenseBytes)).ToLicenseKey();
                    licenseKey.RawResponse = response;

                    licenseCache.Add(new LAKey { Key = licenseKey.Key, ProductId = licenseKey.ProductId, SignMethod = 1 },
                      new LAResult
                      {
                          LicenseKey = licenseKey,
                          SignDate = licenseKey.SignDate,
                          Response = file
                      });
                }
                else
                {
                    // SignMethod = 0

                    var licenseKey = Newtonsoft.Json.JsonConvert.DeserializeObject<LicenseKey>(file);

                    licenseCache.Add(new LAKey { Key = licenseKey.Key, ProductId = licenseKey.ProductId, SignMethod = 0 },
                        new LAResult { LicenseKey = licenseKey, SignDate = licenseKey.SignDate,  Response = 
                        JsonConvert.SerializeObject(new KeyInfoResult { Result = ResultType.Success, LicenseKey = licenseKey  })});
                }
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public static string ObtainNewLicense(byte[] originalStream, HttpWebRequest newRequest, HttpListenerContext context)
        {
            Stream reqStream = newRequest.GetRequestStream();

            reqStream.Write(originalStream, 0, originalStream.Length);
            reqStream.Close();

            var output = ReadToByteArray(newRequest.GetResponse().GetResponseStream());

            context.Response.OutputStream.Write(output, 0, output.Length);
            context.Response.KeepAlive = false;
            context.Response.Close();

            return System.Text.Encoding.Default.GetString(output);
        }

        public static void ReturnResponse(string response, HttpListenerContext context)
        {
            var output = System.Text.Encoding.UTF8.GetBytes(response);
            context.Response.OutputStream.Write(output, 0, output.Length);
            context.Response.KeepAlive = false;
            context.Response.Close();
        }

        public static APIMethod GetAPIMethod(string path)
        {
            if(path.ToLower().StartsWith("/api/key/activate"))
            {
                return APIMethod.Activate;
            }

            return APIMethod.Unknown;
        }

        public static byte[] ReadToByteArray(Stream inputStream, int v = 1024)
        {
            // from: https://stackoverflow.com/a/221941/1275924
            byte[] buffer = new byte[16 * v];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static void UpdateLocalCache(ConcurrentDictionary<LAKey, string> keysToUpdate)
        {
            
        }
    }

    public enum APIMethod
    {
        Unknown = 0,
        Activate = 1
    }
}
