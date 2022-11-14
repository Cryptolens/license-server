/**
 * Copyright (c) 2019 - 2021 Cryptolens AB
 * To use the license server, a separate subscription is needed. 
 * Pricing information can be found on the following page: https://cryptolens.io/products/license-server/
 * */

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

using MessagePack;

namespace LicenseServer
{
    public class Helpers
    {
        public static string ProcessActivateRequest(byte[] stream, Dictionary<LAKey, LAResult> licenseCache, int cacheLength, HttpWebRequest newRequest, HttpListenerContext context, ConcurrentDictionary<LAKey, string> keysToUpdate, bool attemptToRefresh, bool localFloatingServer, ConcurrentDictionary<LAKeyBase, ConcurrentDictionary<string, ActivationData>> activatedMachinesFloating, APIMethod method = APIMethod.Unknown)
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
            var friendlyName = nvc.Get("FriendlyName");
            int floatingTimeInterval = -1;
            int.TryParse(nvc.Get("FloatingTimeInterval"), out floatingTimeInterval);

            LAResult result = null;

            var key = new LAKey { Key = licenseKey, ProductId = productId, SignMethod = signMethod };

            var keyAlt = new LAKey { Key = licenseKey, ProductId = productId, SignMethod = Math.Abs(signMethod-1) };

            if ((method == APIMethod.GetKey || floatingTimeInterval > 0) && localFloatingServer)
            {
                if (signMethod == 0)
                {
                    var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "License server error: SignMethod=1 is needed to use floating licensing offline." });
                    ReturnResponse(error, context);
                    return $"SignMethod was not set to 1 for '{licenseKey}', which is needed to continue with the floating activation.";
                }

                if(Program.ConfigurationExpires != null && Program.ConfigurationExpires < DateTimeOffset.UtcNow)
                {
                    var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "License server error: The configuration has expired. Please contact the vendor to receive a new version of the license server to continue to use floating licenses offline." });
                    ReturnResponse(error, context);
                    return $"The configuration has expired. Please contact the vendor to receive a new version of the license server to continue to use floating licenses offline.";
                }

                //floating license

                if (!licenseCache.TryGetValue(key, out result) && !licenseCache.TryGetValue(keyAlt, out result))
                {
                    var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "License server error: could not find the license file (floating license)." });
                    ReturnResponse(error, context);
                    return $"Could not find the license file for '{licenseKey}' to continue with the floating activation.";
                }

                if(result.LicenseKey.MaxNoOfMachines > 0 || method == APIMethod.GetKey)
                {
                    var activationData = new ConcurrentDictionary<string, ActivationData>();
                    activatedMachinesFloating.TryGetValue(key, out activationData);

                    if(activationData == null)
                    {
                        activationData = activatedMachinesFloating.AddOrUpdate(key, x =>  new ConcurrentDictionary<string, ActivationData>(), (x, y) => y);
                    }

                    if (method == APIMethod.GetKey)
                    {
                        FloatingResult(result, null, null, context, 1, activationData.Values.Where(x => x.FloatingExpires > DateTime.UtcNow).Select(x => new ActivationData { Time = x.Time, FloatingExpires = x.FloatingExpires, FriendlyName = x.FriendlyName, IP = x.IP, Mid = x.Mid }).ToList());
                        return $"Floating license {licenseKey} returned successfully using a GetKey request. The data from the local license server is used.";
                    }

                    var activation = new ActivationData();

                    activationData.TryGetValue(machineCode, out activation);

                    if(activation != null && activation.FloatingExpires >  DateTime.UtcNow)
                    {
                        activation.FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval);
                        activation.FriendlyName = friendlyName;
                        activation.IP = context.Request.RemoteEndPoint.ToString();
                        FloatingResult(result, activation, machineCode, context);
                        return $"Floating license {licenseKey} returned successfully.";
                    }
                    else if(activationData.Count(x=> x.Value.FloatingExpires > DateTime.UtcNow) < result.LicenseKey.MaxNoOfMachines)
                    {
                        activation = activationData.AddOrUpdate(machineCode, x=> new ActivationData { Mid = machineCode, Time = DateTime.UtcNow, FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval), FriendlyName = friendlyName, IP = context.Request.RemoteEndPoint.ToString() }, (x,y) => new ActivationData { Mid = machineCode, Time = y.Time, FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval), FriendlyName = friendlyName, IP = context.Request.RemoteEndPoint.ToString() });

                        FloatingResult(result, activation, machineCode, context);
                        return $"Floating license {licenseKey} returned successfully.";
                    }
                    else
                    {
                        var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "Cannot activate the new device as the limit has been reached." });
                        ReturnResponse(error, context);
                        return $"The limit of the number of concurrent devices for '{licenseKey}' was reached. Activation failed.";
                    }

                    // return new license
                }
                else
                {
                    var activation = new ActivationData { Mid = machineCode, FriendlyName = friendlyName, IP = context.Request.RemoteEndPoint.ToString(), Time = DateTime.UtcNow, FloatingExpires = DateTime.UtcNow.AddSeconds(floatingTimeInterval) };
                    FloatingResult(result, activation, machineCode, context);
                    return $"Floating license {licenseKey} returned successfully.";
                }

            }
            else if (licenseCache.TryGetValue(key, out result) && (method == APIMethod.GetKey || result?.LicenseKey?.ActivatedMachines.Any(x=> x.Mid == machineCode) == true) && cacheLength > 0)
            {
                TimeSpan ts = DateTime.UtcNow - result.SignDate;
                if (ts.Days >= cacheLength || attemptToRefresh)
                {
                    // need to obtain new license

                    try
                    {
                        result.Response = ForwardRequest(stream, newRequest, context);
                    }
                    catch (Exception ex) 
                    { 
                        if(ts.Days >= cacheLength)
                        {
                            if(method == APIMethod.GetKey)
                                return $"Could not retrieve an updated license '{licenseKey}'.";
                            else
                                return $"Could not retrieve an updated license '{licenseKey}' and machine code '{machineCode}'.";

                        }
                        else if (attemptToRefresh)
                        {
                            ReturnResponse(result.Response, context);

                            if(method == APIMethod.GetKey)
                                return $"Retrieved cached version of the license '{licenseKey}'.";
                            else
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

                    if (method == APIMethod.GetKey)
                        return $"Cache updated for license '{licenseKey}'.";
                    else
                        return $"Cache updated for license '{licenseKey}' and machine code '{machineCode}'.";

                }
                else
                {
                    ReturnResponse(result.Response, context);
                    if (method == APIMethod.GetKey)
                        return $"Retrieved cached version of the license '{licenseKey}'.";
                    else
                        return $"Retrieved cached version of the license '{licenseKey}' and machine code '{machineCode}'.";
                }
            }
            else
            {
                result = new LAResult();

                try
                {
                    result.Response = ForwardRequest(stream, newRequest, context);
                }
                catch (WebException ex)
                {
                    try
                    {
                        var output = ReadToByteArray(ex.Response.GetResponseStream());

                        context.Response.OutputStream.Write(output, 0, output.Length);
                        context.Response.KeepAlive = false;
                        context.Response.Close();

                        return $"Could not contact the server '{licenseKey}' and machine code '{machineCode}'. Error message {ex?.Message}.";
                    }
                    catch (Exception ex2)
                    {
                        ReturnResponse(JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "Could not contact the central sever (api.cryptolens.io:443)." }), context);
                        return $"Could not contact the server '{licenseKey}' and machine code '{machineCode}'. Error message {ex2?.Message} and stack trace {ex2?.StackTrace}";
                    }
                }
                catch (Exception ex)
                {
                    ReturnResponse(JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "Could not contact the central sever (api.cryptolens.io:443)." }), context);
                    return $"Could not contact the server '{licenseKey}' and machine code '{machineCode}'. Error message {ex?.Message} and stack trace {ex?.StackTrace}";
                }

                if (cacheLength > 0)
                {
                    if (signMethod == 1)
                    {
                        var resultObject = JsonConvert.DeserializeObject<RawResponse>(result.Response);

                        if(!SKM.V3.Methods.Helpers.IsSuccessful(resultObject))
                        {
                            return $"The error '{resultObject?.Message}' was received from the server for license '{licenseKey}' and machine code '{machineCode}'. The method {method} was used. Read more at https://help.cryptolens.io/faq/index#troubleshooting-api-errors.";
                        }

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

                    if(licenseCache.ContainsKey(key))
                    {
                        licenseCache[key] = result;
                    }
                    else
                    {
                        licenseCache.Add(key, result);
                    }

                    keysToUpdate.AddOrUpdate(key, x => result.Response, (x, y) => result.Response);

                    return $"Added to the cache the license '{licenseKey}' and machine code '{machineCode}'.";
                }

                return null;
            }
        }

        public static void FloatingResult(LAResult result, ActivationData activation, string machineCode, HttpListenerContext context, int signmetod = 1, List<ActivationData> activations = null)
        {
            if (signmetod == 1)
            {
                var licenseKeyToReturn = new LicenseKeyPI { };

                if (activations != null)
                {
                    licenseKeyToReturn.ActivatedMachines = activations.Select(x => new ActivationDataPIV3 {  FriendlyName = x.FriendlyName, IP = x.IP, Time = ToUnixTimestamp(x.Time.Value), Mid = $"floating:{x.Mid}", FloatingExpires = ToUnixTimestamp(x.FloatingExpires.Value)}).ToList();
                }
                else
                {
                    licenseKeyToReturn.ActivatedMachines = new List<ActivationDataPIV3>() { new ActivationDataPIV3 { Mid = $"floating:{machineCode}", Time =
                          ToUnixTimestamp(activation.Time.Value), FriendlyName = activation.FriendlyName, IP = activation.IP, FloatingExpires = ToUnixTimestamp(activation.FloatingExpires.Value)  } };
                }

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

                rsa.FromXmlString(Program.RSAServerKey);

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

        public static string ProcessIncrementDecrementValueRequest(byte[] stream, HttpWebRequest newRequest, HttpListenerContext context, APIMethod method)
        {
            string bodyParams = System.Text.Encoding.Default.GetString(stream);
            var nvc = HttpUtility.ParseQueryString(bodyParams);

            int productId = -1;
            int.TryParse(nvc.Get("ProductId"), out productId);

            var licenseKey = nvc.Get("Key");
            int intValue = -1;
            int.TryParse(nvc.Get("IntValue"), out intValue);

            int doId = -1;
            int.TryParse(nvc.Get("Id"), out doId);

            if(method == APIMethod.DecrementIntValueToKey)
            {
                doId = -1 * Math.Abs(doId);
            }
            else
            {
                doId = Math.Abs(doId);
            }

            if (!Program.attemptToRefresh && !string.IsNullOrEmpty(Program.RSAPublicKey))
            {

                LAResult result = null;

                var key = new LAKey { Key = licenseKey, ProductId = productId, SignMethod = 0 };

                var keyAlt = new LAKey { Key = licenseKey, ProductId = productId, SignMethod = 1 };

                if (!Program.licenseCache.TryGetValue(key, out result) && !Program.licenseCache.TryGetValue(keyAlt, out result))
                {
                    var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "License server error: could not find the license file (to increment/decrement the data object)." });
                    ReturnResponse(error, context);
                    return $"Could not find the license file for '{licenseKey}' to continue with the increment or decrement operation.";
                }

                if(!result.LicenseKey.DataObjects.Any(x=> x.Id == doId))
                {
                    var error = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Error, Message = "License server error: could not find the data object in the license file (to increment/decrement the data object)." });
                    ReturnResponse(error, context);
                    return $"Could not find the data object with ID {doId} in the license file for '{licenseKey}' to continue with the increment or decrement operation.";
                }

                var doKey = new DOKey { DOID = doId, Key = licenseKey, ProductId = productId };
                Program.DOOperations.AddOrUpdate(doKey, doId, (x, y) => y + doId);

                var success = JsonConvert.SerializeObject(new BasicResult { Result = ResultType.Success, Message = "" });
                ReturnResponse(success, context);
                return $"Updated data object successfully.";
            }
            else
            {
                // for now, just forward the request.
                ForwardRequest(stream, newRequest, context);

                //try
                //{
                //}
                //catch (Exception ex)
                //{

                //}
            }


            // add and store log.


            return null;
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
                        string errorMessage;
                        string result = LoadLicenseFromFile(licenseCache, keysToUpdate, file, out errorMessage) ? "OK" : "Error";
                        updates($"File '{path}' {result}.");
                        if(errorMessage != null)
                        {
                            updates(errorMessage);
                        }
                    }
                }
                else
                {
                    string errorMessage;
                    string result = LoadLicenseFromFile(licenseCache, keysToUpdate, path, out errorMessage) ? "OK" : "Error";
                    updates($"File '{path}' {result}.");

                    if (errorMessage != null)
                    {
                        updates(errorMessage);
                    }
                }
            }
            catch(Exception ex) { return false; }

            return true;
        }

        public static bool LoadLicenseFromFile(Dictionary<LAKey, LAResult> licenseCache, ConcurrentDictionary<LAKey, string> keysToUpdate, string pathToFile, out string errorMessage)
        {
            errorMessage = null;

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

                if (!string.IsNullOrEmpty(Program.RSAPublicKey) && !result.LicenseKey.HasValidSignature(Program.RSAPublicKey, Program.cacheLength).IsValid())
                {
                    errorMessage = $"The file '{pathToFile}' was not loaded from the cache. The signature check failed.";
                    return false; 
                }

                if (licenseCache.ContainsKey(key))
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

        public static string ForwardRequest(byte[] originalStream, HttpWebRequest newRequest, HttpListenerContext context)
        {

            Stream reqStream = newRequest.GetRequestStream();

            reqStream.Write(originalStream, 0, originalStream.Length);
            reqStream.Close();

            var output = ReadToByteArray(newRequest.GetResponse().GetResponseStream());

            context.Response.OutputStream.Write(output, 0, output.Length);
            context.Response.KeepAlive = false;
            context.Response.ContentType = "application/json";
            context.Response.Close();

            return System.Text.Encoding.Default.GetString(output);
        }

        public static void ReturnResponse(string response, HttpListenerContext context)
        {
            var output = System.Text.Encoding.UTF8.GetBytes(response);
            context.Response.OutputStream.Write(output, 0, output.Length);
            context.Response.ContentType = "application/json";
            context.Response.KeepAlive = false;
            context.Response.Close();
        }

        public static void HTMLResponse(string title, string body, HttpListenerContext context)
        {
            ReturnResponse($"<html><head><title>{title}</title></head>" +
                        $"<body>{body}</body></html>", context);
        }

        public static APIMethod GetAPIMethod(string path)
        {
            if(path.ToLower().Replace("//","/").Contains("/api/key/activate"))
            {
                return APIMethod.Activate;
            }

            if (path.ToLower().Replace("//", "/").Contains("/api/key/getkey"))
            {
                return APIMethod.GetKey;
            }

            if (path.ToLower().Replace("//", "/").Contains("/api/data/incrementintvaluetokey"))
            {
                return APIMethod.IncrementIntValueToKey;
            }

            if (path.ToLower().Replace("//", "/").Contains("/api/data/decrementintvaluetokey"))
            {
                return APIMethod.DecrementIntValueToKey;
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

        public static void UpdateLocalCache(ConcurrentDictionary<LAKey, string> keysToUpdate, string pathToCacheFolder = null)
        {
            var keysToSave = keysToUpdate.Keys.ToList();

            string path = "";

            if (!string.IsNullOrWhiteSpace(pathToCacheFolder))
            {
                path = Path.Combine(pathToCacheFolder, "cache");
            }
            else
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            
            foreach (var key in keysToSave)
            {
                string res = null;
                if (keysToUpdate.TryRemove(key, out res))
                {
                    System.IO.File.WriteAllText(Path.Combine(path, $"{key.ProductId}.{key.Key}.{key.SignMethod}.skm"), res);
                }
            }
        }

        public static void SaveDOLogToFile()
        {
            if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "usage")))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "usage"));
            }

            var keys = Program.DOOperations;

            foreach (var key in keys.Keys)
            {
                byte[] previousBlock = new byte[] { 0 };

                int res = 0;

                if (keys.TryRemove(key, out res))
                {
                    try
                    {
                        previousBlock = File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "usage", $"{key.ProductId}.{key.Key}-usage.skm"));
                    }
                    catch (Exception ex) { previousBlock = new byte[] { 0 }; }

                    var res2 = DOOperations.AddDataDOOP(new DataObjectOperation { DataObjectId = key.DOID, Increment = res }, previousBlock, Program.RSAPublicKey);

                    File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "usage", $"{key.ProductId}.{key.Key}-usage.skm"), res2);

                }
            }
        }

        public static string LoadFromLocalCache(Dictionary<LAKey, LAResult> licenseCache, Action<string> updates, string pathToCacheFolder = null)
        {
            string path = "";

            if (!string.IsNullOrWhiteSpace(pathToCacheFolder))
            {
                path = Path.Combine(pathToCacheFolder, "cache");
            }
            else
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!Directory.Exists(path)) { return "Could not load the cache."; }

            var files = Directory.GetFiles(path);

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
                    result.LicenseKey = Newtonsoft.Json.JsonConvert.DeserializeObject<KeyInfoResult>(fileContent).LicenseKey;
                    result.SignDate = result.LicenseKey.SignDate;
                }
                else
                {
                    var response = Newtonsoft.Json.JsonConvert.DeserializeObject<RawResponse>(fileContent);
                    result.LicenseKey = JsonConvert.DeserializeObject<LicenseKeyPI>(System.Text.UTF8Encoding.UTF8.GetString(Convert.FromBase64String(response.LicenseKey))).ToLicenseKey();
                    result.SignDate = result.LicenseKey.SignDate;
                }


                if (!string.IsNullOrEmpty(Program.RSAPublicKey) && !result.LicenseKey.HasValidSignature(Program.RSAPublicKey, Program.cacheLength).IsValid())
                {
                    updates($"The file '{file}' was not loaded from the cache. The signature check failed or the file is too old.");
                    continue;
                }

                if (licenseCache.ContainsKey(key))
                {
                    if(licenseCache[key].SignDate < result.SignDate)
                    {
                        filecount++;
                        licenseCache[key] = result;
                        updates($"The cache was updated with '{file}'. The license was previously loaded from file was overridden.");
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

        public static LicenseServerConfiguration ReadConfiguration(string config, string existingRSAPublicKey = null)
        {
            try
            {
                var serializedData = MessagePackSerializer.Deserialize<SerializedLSC>(Convert.FromBase64String(config));

                var extractedConfig = MessagePackSerializer.Deserialize<LicenseServerConfiguration>(serializedData.LSC);

                using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048))
                {
                    if (existingRSAPublicKey != null)
                    {
                        rsa.FromXmlString(existingRSAPublicKey);
                    }
                    else
                    {
                        rsa.FromXmlString(extractedConfig.RSAPublicKey);
                    }

                    if (rsa.VerifyData(serializedData.LSC, serializedData.Signature, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1))
                    {
                        // ok
                        return extractedConfig;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch(Exception ex) { return null; }

            return null;
        }
    }

    public enum APIMethod
    {
        Unknown = 0,
        Activate = 1,
        IncrementIntValueToKey = 2,
        DecrementIntValueToKey = 3,
        GetKey = 4,
        Deactivate = 5
    }
}
