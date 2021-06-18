using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using SKM.V3;
using SKM.V3.Models;

using System.Collections.Concurrent;
using System.Web;

namespace LicenseServer
{
    public class Helpers
    {
        public static string ProcessActivateRequest(byte[] stream, Dictionary<LAKey, LAResult> licenseCache, int cacheLength, HttpWebRequest newRequest, HttpListenerContext context, ConcurrentDictionary<LAKey, string> keysToUpdate, bool attemptToRefresh, bool localFloatingServer, ConcurrentDictionary<LAKeyBase, ConcurrentDictionary<string, ActivationData>> activatedMachinesFloating)
        {
            string bodyParams = System.Text.Encoding.Default.GetString(stream);
            var nvc = HttpUtility.ParseQueryString(bodyParams);

            int productId = -1;
            int.TryParse(nvc.Get("ProductId"), out productId);
            int signMethod = -1;
            int.TryParse(nvc.Get("SignMethod"), out signMethod);

            if(nvc.Get("SignMethod") == "StringSign")
            {
                signMethod = 1;
            }

            var licenseKey = nvc.Get("Key");
            var machineCode = nvc.Get("MachineCode");
            int floatingTimeInterval = -1;
            int.TryParse(nvc.Get("FloatingTimeInterval"), out floatingTimeInterval);

            LAResult result = null;

            var key = new LAKey { Key = licenseKey, ProductId = productId, SignMethod = signMethod };

            var keyAlt = new LAKey { Key = licenseKey, ProductId = productId, SignMethod = Math.Abs(signMethod-1) };

            if (floatingTimeInterval > 0 && localFloatingServer)
            {
                if (signMethod == 0)
                {
                    var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "License server error: SignMethod=1 is needed to use floating licensing offline." });
                    ReturnResponse(error, context);
                    return $"SignMethod was not set to 1 for '{licenseKey}', which is needed to continue with the floating activation.";
                }

                //floating license

                if (!licenseCache.TryGetValue(key, out result) && !licenseCache.TryGetValue(keyAlt, out result))
                {
                    var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "License server error: could not find the license file (floating license)." });
                    ReturnResponse(error, context);
                    return $"Could not find the license file for '{licenseKey}' to continue with the floating activation.";
                }

                if(result.LicenseKey.MaxNoOfMachines > 0)
                {
                    var activationData = new ConcurrentDictionary<string, ActivationData>();
                    activatedMachinesFloating.TryGetValue(key, out activationData);

                    if(activationData == null)
                    {
                        activationData = activatedMachinesFloating.AddOrUpdate(key, x =>  new ConcurrentDictionary<string, ActivationData>(), (x, y) => y);
                    }

                    var activation = new ActivationData();

                    activationData.TryGetValue(machineCode, out activation);

                    if(activation != null && activation.FloatingExpires >  DateTime.UtcNow)
                    {
                        activation.FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval);
                        FloatingResult(result, activation, machineCode, context);
                        return $"Floating license {licenseKey} returned successfully.";
                    }
                    else if(activationData.Count(x=> x.Value.FloatingExpires > DateTime.UtcNow) < result.LicenseKey.MaxNoOfMachines)
                    {
                        activation = activationData.AddOrUpdate(machineCode, x=> new ActivationData { Mid = machineCode, Time = DateTime.UtcNow, FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval) }, (x,y) => new ActivationData { Mid = machineCode, Time = y.Time, FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval) });

                        FloatingResult(result, activation, machineCode, context);
                        return $"Floating license {licenseKey} returned successfully.";
                    }
                    else
                    {
                        var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "Cannot activate the new device as the limit has been reached." });
                        ReturnResponse(error, context);
                        return $"The limit of the number of concurrent devices for '{licenseKey}' was reached. Activation failed.";
                    }


                    //if (activationData.Any(x => x.FloatingExpires > DateTime.UtcNow && x.Mid == machineCode))
                    //{
                    //    // maybe put this into concurrent dict instead.
                    //    var activation = activatedMachinesFloating[key].Where(x => x.FloatingExpires > DateTime.UtcNow && x.Mid == machineCode)
                    //                                  .OrderBy(x => x.Time).First();
                    //    activation.FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval);

                    //    FloatingResult(result, activation, machineCode, context);
 
                    //    return $"Floating license {licenseKey} returned successfully.";
                    //}
                    //else if (activatedMachinesFloating[key].Count(x => x.FloatingExpires > DateTime.UtcNow) < result.LicenseKey.MaxNoOfMachines)
                    //{
                    //    //activatedMachinesFloating.AddOrUpdate

                    //    activationData.Add(new ActivationData { Mid = machineCode, Time = DateTime.UtcNow,  FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval) });


                    //    //FloatingResult(result, activation, machineCode, context);

                    //    return $"Floating license {licenseKey} returned successfully.";
                    //}


                    // return new license
                }
                else
                {
                    var activation = new ActivationData { Mid = machineCode, Time = DateTime.UtcNow, FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval) };
                    FloatingResult(result, activation, machineCode, context);
                    return $"Floating license {licenseKey} returned successfully.";
                }

                return null;

            }
            else if (licenseCache.TryGetValue(key, out result) && result?.LicenseKey?.ActivatedMachines.Any(x=> x.Mid == machineCode) == true && cacheLength > 0)
            {
                TimeSpan ts = DateTime.UtcNow - result.SignDate;
                if (ts.Days >= cacheLength || attemptToRefresh)
                {
                    // need to obtain new license

                    try
                    {
                        result.Response = ObtainNewLicense(stream, newRequest, context);
                    }
                    catch (Exception ex) 
                    { 
                        if(ts.Days >= cacheLength)
                        {
                            return $"Could not retrieve an updated license '{licenseKey}' and machine code '{machineCode}'.";
                        }
                        else if (attemptToRefresh)
                        {
                            ReturnResponse(result.Response, context);
                            return $"Retrieved cached version of the license '{licenseKey}' and machine code '{machineCode}'.";
                        }
                    }


                    if (signMethod == 1)
                    {
                        var resultObject = JsonConvert.DeserializeObject<RawResponse>(result.Response);
                        var license2 = JsonConvert.DeserializeObject<LicenseKeyPI>(System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(resultObject.LicenseKey))).ToLicenseKey();
                        result.SignDate = license2.SignDate;
                        result.LicenseKey = license2;
                    }
                    else
                    {
                        var resultObject = JsonConvert.DeserializeObject<KeyInfoResult>(result.Response);
                        result.SignDate = resultObject.LicenseKey.SignDate;
                        result.LicenseKey = resultObject.LicenseKey;
                    }

                    keysToUpdate.AddOrUpdate(key, x => result.Response, (x, y) => result.Response);

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
                        var license2 = JsonConvert.DeserializeObject<LicenseKeyPI>(System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(resultObject.LicenseKey))).ToLicenseKey();
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
                    keysToUpdate.AddOrUpdate(key, x => result.Response, (x, y) => result.Response);

                    return $"Added to the cache the license '{licenseKey}' and machine code '{machineCode}'.";
                }
                return null;
            }
        }

        public static void FloatingResult(LAResult result, ActivationData activation, string machineCode, HttpListenerContext context, int signmetod = 1)
        {
            if (signmetod == 1)
            {
                var licenseKeyToReturn = new LicenseKeyPI { };

                licenseKeyToReturn.ActivatedMachines = new List<ActivationDataPI>() { new ActivationDataPI { Mid = $"floating:{machineCode}", Time =
                          ToUnixTimestamp(activation.Time.Value)} };
                licenseKeyToReturn.Block = result.LicenseKey.Block;
                licenseKeyToReturn.Created = ToUnixTimestamp(result.LicenseKey.Created);
                licenseKeyToReturn.Expires = ToUnixTimestamp(result.LicenseKey.Expires);

                if (licenseKeyToReturn != null)
                {
                    licenseKeyToReturn.Customer = new CustomerPI { CompanyName = result.LicenseKey.Customer.CompanyName, Created = ToUnixTimestamp(result.LicenseKey.Customer.Created), Email = result.LicenseKey.Customer.Email, Id = result.LicenseKey.Customer.Id, Name = result.LicenseKey.Customer.Name };
                }

                licenseKeyToReturn.DataObjects = result.LicenseKey.DataObjects;
                licenseKeyToReturn.F1 = result.LicenseKey.F1;
                licenseKeyToReturn.F2 = result.LicenseKey.F2;
                licenseKeyToReturn.F3 = result.LicenseKey.F3;
                licenseKeyToReturn.F4 = result.LicenseKey.F4;
                licenseKeyToReturn.F5 = result.LicenseKey.F5;
                licenseKeyToReturn.F6 = result.LicenseKey.F6;
                licenseKeyToReturn.F7 = result.LicenseKey.F7;
                licenseKeyToReturn.F8 = result.LicenseKey.F8;

                licenseKeyToReturn.GlobalId = result.LicenseKey.GlobalId;
                licenseKeyToReturn.MaxNoOfMachines = result.LicenseKey.MaxNoOfMachines;
                licenseKeyToReturn.ID = result.LicenseKey.ID;
                licenseKeyToReturn.Key = result.LicenseKey.Key;

                licenseKeyToReturn.Notes = result.LicenseKey.Notes;
                licenseKeyToReturn.Period = result.LicenseKey.Period;
                licenseKeyToReturn.TrialActivation = result.LicenseKey.TrialActivation;
                licenseKeyToReturn.ProductId = result.LicenseKey.ProductId;
                licenseKeyToReturn.SignDate = ToUnixTimestamp(DateTime.UtcNow);

                var data = Newtonsoft.Json.JsonConvert.SerializeObject(licenseKeyToReturn);

                var signature = "";

                byte[] dataSign = System.Text.UTF8Encoding.UTF8.GetBytes(data);

                System.Security.Cryptography.RSACryptoServiceProvider rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);

                rsa.FromXmlString("<RSAKeyValue><Modulus>4sjUnI6Qq3p+hoBkUAja4Ba2CXeFXSt3QVScRsDJwp+7IjrEpr35n2jlM6KTjjjVs7z3SoCK07xxpjfPcIHcJpOlDEW03mgLSAGmuB1JvtDpR1jwATVNtfvvLuJb6Ayt4RmfGnkU78129daRWOZJykKFbrPiSZOSKMf0UQzhyNC2f8K3sLi1DMRhGHDlH+IbJGz6nPP/ZE5oS7npNbXEy848OJKw/czEj7MWP0MbE0a4Qx3Gmky1XyLcBKVz1O8lO9u7p3XgY2EElCjICPgI93X8tEL+DNk7ZQrsQFOnxnr+IACxUDM760VSM1hR8d5FiMk6YemBn40wgeiv6y1eUQ==</Modulus><Exponent>AQAB</Exponent><P>57nk2JP6gVWZ6Mc4JhtVXN88O7hO021iNkQm/VAdZAsr9cwkQi+jSVdBvZAL961e/ZP5CYDxIl/fcmSy4i5MRnoanZ90D7zW/mRx209bfwtVtEtPCaGXqYqePAdnDYLMqAUWF9GmNjEqlB03/R3ndSBgsjRV01LAwz8V5bvTvEs=</P><Q>+oprEsZlf9d8rgdX/3fiREBNpiZm3kziLphbDoVZNsW8lFrF/OswiQIc+GGrTflJudJuGvZlJOWHsZDSX8324pmKTwIVKNgCre9PJ+6+ervYOVYVqmToC7vYNb2g4jNHRoBkSDFOGTNmV2/aIUPtG+tMFZKQmhcdj+7UHnq+tlM=</Q><DP>g4roWPWv58mDJDwrKJ6tl5n15GTdAnJ+pRWNGJFpDci1vMOU9al7RP/uhsCFuqTFXqeoYHe86umHu7VkQrdLf1qDT2UcCm8FkMXOSFPFOdpiXYW+qVX89TaGWsdM/cN5kAvLHdxaQTsp04i+psZaBQhLO/4vllXMrUlbkd1M9f8=</DP><DQ>R9noiTLiqv42oIY0o2xTNLWoTy0WNUyhVTGWc5ykkEO3KGi7/SPKAJDdlBIWmb8TeLozn4HoUeONvcvFuXoNAsF729rCDLueURmfftlGQVab1R2uCvbzYWIWyJrAh/6iw0JRAC87sZh/EjZevUmIt4gMgudMlxRoAv5AURlslkc=</DQ><InverseQ>FLmeZ5fDVqdK7ozK8GRLgl2fGK3a8HWu0Uj/KRbPh0x+3dBookRA5QezYRcrDfNZdsDysy1iSB7ZnOPYqDmm+FWcVZrnRQPpGKZtX7T1C5mE24eMx9nN5SS7/QgnujcvcvoqqDFi8y7KH70PrgV6sMksMclN9sCUAEglUssfPsY=</InverseQ><D>qvYU+XSr8OlmCoUtmfwi7D2Su25Dtmn2++QJ73iUYMjDbNl6t+yNCrQr3RIZRGTqDRZOIfbnMRllX5XBJqJu0RIKoUbHQ8aRgpXkFfXWSyf4RBXy0CZbz+39cI2qFTPBvOjwvSc8Nk7g+BDp/2eThwtAxaSL2UWLMH0UXClm6FerPKmdCxbRerTrw2fPXv6A/8AnPft/1si3sBcgJtdXEVIpPBCM0jszdOuEsdDRcpFmOvzfG5ZEh9ewuI5fpWR5seMni+LM2e/EAsF3ZCukrZQU8PdsPH8ukU0DTIFXX94hsC7DiYT/D8ldfMl9Q39dddeFOgBHMvUA2roovPzw5Q==</D></RSAKeyValue>");

                byte[] signedData = rsa.SignData(dataSign, "SHA256");
                signature = Convert.ToBase64String(signedData);

                var result2 = Newtonsoft.Json.JsonConvert.SerializeObject(new RawResponse
                {
                    LicenseKey = Convert.ToBase64String(dataSign),
                    Signature = signature,
                    Message = "",
                    Result = ResultType.Success
                });

                ReturnResponse(result2, context);
            }
            else
            {


            }
        }

        public static bool LoadLicenseFromPath(Dictionary<LAKey, LAResult> licenseCache, ConcurrentDictionary<LAKey, string> keysToUpdate, string path, Action<string> updates)
        {
            try
            {
                if(System.IO.File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                {
                    var files = Directory.GetFiles(path, "*.skm");

                    foreach (var file in files)
                    {
                        string result = LoadLicenseFromFile(licenseCache, keysToUpdate, file) ? "OK" : "Error";
                        updates($"File '{path}' {result}.");
                    }
                }
                else
                {
                    string result = LoadLicenseFromFile(licenseCache, keysToUpdate, path) ? "OK" : "Error";
                    updates($"File '{path}' {result}.");
                }
            }
            catch(Exception ex) { return false; }

            return true;
        }

        public static bool LoadLicenseFromFile(Dictionary<LAKey, LAResult> licenseCache, ConcurrentDictionary<LAKey, string> keysToUpdate, string pathToFile)
        {
            try
            {
                var file = System.IO.File.ReadAllText(pathToFile);

                var response = Newtonsoft.Json.JsonConvert.DeserializeObject<RawResponse>(file);

                LAKey key;
                LAResult result;

                if (!string.IsNullOrEmpty(response.LicenseKey))
                {
                    // SignMethod = 1

                    var licenseBytes = Convert.FromBase64String(response.LicenseKey);
                    var licenseKey = JsonConvert.DeserializeObject<LicenseKeyPI>(System.Text.UTF8Encoding.UTF8.GetString(licenseBytes)).ToLicenseKey();
                    licenseKey.RawResponse = response;

                    key = new LAKey { Key = licenseKey.Key, ProductId = licenseKey.ProductId, SignMethod = 1 };
                    
                    result = new LAResult
                    {
                        LicenseKey = licenseKey,
                        SignDate = licenseKey.SignDate,
                        Response = file
                    };

                }
                else
                {
                    // SignMethod = 0

                    var licenseKey = Newtonsoft.Json.JsonConvert.DeserializeObject<LicenseKey>(file);

                    key = new LAKey { Key = licenseKey.Key, ProductId = licenseKey.ProductId, SignMethod = 0 };
                    result = new LAResult
                    {
                        LicenseKey = licenseKey,
                        SignDate = licenseKey.SignDate,
                        Response =
                        JsonConvert.SerializeObject(new KeyInfoResult { Result = ResultType.Success, LicenseKey = licenseKey })
                    };

                }

                if(licenseCache.ContainsKey(key))
                {
                    if(licenseCache[key].SignDate < result.SignDate)
                    {
                        licenseCache[key] = result;
                        keysToUpdate[key] = result.Response;

                    }
                }
                else
                {
                    licenseCache.Add(key, result);
                    keysToUpdate.TryAdd(key, result.Response);
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
            if(path.ToLower().Replace("//","/").Contains("/api/key/activate"))
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
            var keysToSave = keysToUpdate.Keys.ToList();

            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "cache")))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "cache"));
            }

            foreach (var key in keysToSave)
            {
                string res = null;
                if (keysToUpdate.TryRemove(key, out res))
                {
                    System.IO.File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "cache", $"{key.ProductId}.{key.Key}.{key.SignMethod}.skm"), res);
                }
            }
        }

        public static string LoadFromLocalCache(Dictionary<LAKey, LAResult> licenseCache, Action<string> updates)
        {
            if(!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "cache"))) { return "Could not load the cache."; }

            var files = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "cache"));

            int filecount = 0;
            foreach (var file in files)
            {
                var fileContent = File.ReadAllText(file);
                string[] extractedFileInfo = Path.GetFileName(file).Split('.');

                LicenseKey license = new LicenseKey();
                
                var key = new LAKey
                {
                    ProductId = Convert.ToInt32(extractedFileInfo[0]),
                    Key = extractedFileInfo[1],
                    SignMethod = Convert.ToInt32(extractedFileInfo[2])
                };

                var result = new LAResult();
                result.Response = fileContent;

                if (key.SignMethod == 0)
                {
                    result.LicenseKey = Newtonsoft.Json.JsonConvert.DeserializeObject<LicenseKey>(fileContent);
                    result.SignDate = result.LicenseKey.SignDate;
                }
                else
                {
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<RawResponse>(fileContent);
                    result.LicenseKey = JsonConvert.DeserializeObject<LicenseKeyPI>(System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(response.LicenseKey))).ToLicenseKey();
                    result.SignDate = result.LicenseKey.SignDate;
                }

                if (licenseCache.ContainsKey(key))
                {
                    if(licenseCache[key].SignDate < result.SignDate)
                    {
                        filecount++;
                        licenseCache[key] = result;
                        updates($"The cache was updated with '{file}'. The license was previously loaded from file was overriden.");
                    }
                    else
                    {
                        updates($"The file '{file}' was not loaded from the cache, since a newer version was provided manually.");
                    }
                }
                else
                {
                    licenseCache.Add(key, result);
                    filecount++;
                    updates($"The file '{file}' was loaded from the cache.");
                }
                
            }

            return $"Loaded {filecount}/{files.Count()} file(s) from cache.";
        }

        public static long ToUnixTimestamp(DateTime time)
        {
            var epoch = time - new DateTime(1970, 1, 1, 0, 0, 0);

            return (long)epoch.TotalSeconds;
        }
    }

    public enum APIMethod
    {
        Unknown = 0,
        Activate = 1
    }
}
