/**
 * Copyright (c) 2019 - 2021 Cryptolens AB
 * To use the license server, a separate subscription is needed. 
 * Pricing information can be found on the following page: https://cryptolens.io/products/license-server/
 * */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Threading;
using System.IO;
using System.Web;
using System.Net.Sockets;

using System.Collections.Concurrent;

using SKM.V3;
using SKM.V3.Models;

namespace LicenseServer
{
    class Program
    {
        static HttpListener httpListener = new HttpListener();
        public static int port = 8080;
        public static Dictionary<LAKey, LAResult> licenseCache = new Dictionary<LAKey, LAResult>();
        public static ConcurrentDictionary<LAKey, string> keysToUpdate = new ConcurrentDictionary<LAKey, string>();
        public static ConcurrentDictionary<LAKeyBase, ConcurrentDictionary<string, ActivationData>> activatedMachinesFloating = new ConcurrentDictionary<LAKeyBase, ConcurrentDictionary<string, ActivationData>>();
        public static int cacheLength = 0;
        public static bool attemptToRefresh = true;
        public static bool localFloatingServer = true;
        public static string RSAServerKey = "";
        public static string RSAPublicKey = "";


        public static string ConfigurationFromCryptolens = "ksUFTpjNH5DNAW3DkMPaAZ88UlNBS2V5VmFsdWU+PE1vZHVsdXM+d2ZscVZmYis2Z2tiMWxIcjFmeXN1eDhvK29KUkV5ME0yaE0zeUVLaDkydzVyWG9jNjczWlJJOUpUZzcyNUlxSHZyLzAzMVRNcUZBTXZmZFE4WDVnQjBMM2diM0UxdFkvUXZwWnh3b2JSczZYcHozWHh1R2haN2NJaE85dVdMWnV5a1N2b0QrUFFaTU9zZHArTTA1cDlLUzRlVHNidVNvMXcwa3drcDZBbnVtTWlHN0lDT3hZc2RHZzdZbHpYNUROM202UWdHOUZnKzZmYUlPTHlCeFhrdEJLNCtsaWdDTU5ZZFJMZDN6OVVxaEdHeTR1L0huejNWRDJieko5OUpXUVl4MDBIdFh5b2ptVUhpMjVZazJHNWVraTNzUVhNN2dZeldrWk96YUVRNS9yTWdocDBPMy9lSmJsV3RyYW54UXY0SEJDbS9iOGw0b1ZoNEdUY04weDd3PT08L01vZHVsdXM+PEV4cG9uZW50PkFRQUI8L0V4cG9uZW50PjwvUlNBS2V5VmFsdWU+2gOTPFJTQUtleVZhbHVlPjxNb2R1bHVzPnR1WU5qOEZwcm5yYzBnQTc0NlJUV2Y3emhlRUl1a3l3anpuUFdXWEduTFhzMkFzQUZuUjJOWGVRSlF5VzR1bEZxcm43UTRZMXZteEUwWVMxRmR6YjBvalgzRmwzMnpjeXNLbjcxdG1zRmZkdktJQ21TQlBVK2ZYLzVhanQ3bFQrZDRBQW0zelBhUWx2NzdScWhPWDhnM3FWVWFVbjhEUy82ZTlOUVVaWFBKYz08L01vZHVsdXM+PEV4cG9uZW50PkFRQUI8L0V4cG9uZW50PjxQPjd4bUZ6cGs3T1R1Z0g4V1FiZGFJcXhYVjhzMjZ1bnhWYkM3bEVLUElPSlFJYzNPZmJDT09QUGpvdFNVUW9raDZNeVpPN1RIYXc1UmNpQjdFaUxMZXd3PT08L1A+PFE+dzlPVVpoVktWWFNUZVlrVEQ3L0g3YjJyZnJFMXlRNmpiTDlLUEZ2VHFqLzA0Znd6K0kyM1lmSDNaQWIzcndnMk9Sa0oxbEpVY3Y1VCtvOEVpWHoxblE9PTwvUT48RFA+b3NNb0VGVEZPUmU2MEFZSVpJMEhLaGZLTmVnUWRnUlBqajU2RnBLM3FMUTA4SXJEK3JSVmpuSU96OGswZ3BSK0F6WFB3cTRPY3RjYkNscVBoVkxtU3c9PTwvRFA+PERRPm5vM0dIdTFvRlU3aFJVdEdJcjlPR21FeTVNNkkxUFRFUDMyV2xzNW16L2NleUliU2NaSVZBL25OSzdFMGhBU3JHb242WEF5bkVjQjRzR3R4NmZ4Q1JRPT08L0RRPjxJbnZlcnNlUT4zSnFDY2ZQZks3VHhZd1VoRWVoS3VsTGpBc3N6aUwzdlNDeDBwejNrL1NFRUUwdHNPT2JCNG90TzBVK3RvNHl3cVNjTmVISDlDc0w4a3JTMGJEemJ2Zz09PC9JbnZlcnNlUT48RD5FT0hkKysybnlrbmZwWDFpdEhuYWttMWlIOGdoMGhza3dQNWVacXRxVUg5MUdUYTFaTEtOL0JRZWpnenN4ZUtGRFE2VnVYODRsQmNucUY5T01uSnNmVE9zWG1IME9QVTdvT2V2MVJSNWswK2tTOHRZOVByYlJJeGhCNEFuZzNJSS9HQit5ZzdQMlYwS1l6ZTZrTmY0U1p4WDBJZjR0dVpxQTVSRWlWMDhZS0U9PC9EPjwvUlNBS2V5VmFsdWU+ktf/ku4a8GK0flMAxQEAH520wuO4Za9P7PmR4b3/vrz9thFdi6MGshqjVd/2zwHwbp8yatirXVhK0OlYm0Ng7UmIJM5E3LHa7NeFtltLJtBUsa+DPf72u7WHP7dU3sfJkeHXIra5+KmnFxX4jrdI4/79Yncr6Jx9CNupTXI0rsytLU95XxeLGkQWuqPAzHTRslqhzpEdF2gjK3YQSEET53D2NJhwgA+9WK6FoL+Bcyt6jXULrGwKfTgSg+h0+yzsCJ/Y5djvJ7+6vhj6N1ON+AOygRaKeOejPmut2jNNH9GBs+r71qKV19rzvtqgrOcXy3cilrjmbBPqTlTqQL4Dpfi2/d1sbGE6/92kawPJpA==";


        static void Main(string[] args)
        {
            Console.WriteLine("Cryptolens License Server v2.1\n");

            if (!string.IsNullOrEmpty(ConfigurationFromCryptolens))
            {
                var config = Helpers.ReadConfiguration(ConfigurationFromCryptolens);

                if(config == null)
                {
                    WriteMessage($"Configuration data could not be read.");
                    return;
                }

                cacheLength = config.CacheLength;
                WriteMessage($"Cache length set to {cacheLength}");
                port = config.Port;
                WriteMessage($"Port set to {port}");
                attemptToRefresh = !config.OfflineMode;
                WriteMessage($"Offline mode is set to {!attemptToRefresh}");
                localFloatingServer = config.LocalFloatingServer;
                WriteMessage($"Local floating license server is set to {localFloatingServer}");

                RSAServerKey = config.ServerKey;
                RSAPublicKey = config.RSAPublicKey;

                foreach (var file in config.ActivationFiles)
                {
                    string result = Helpers.LoadLicenseFromPath(licenseCache, keysToUpdate, file, WriteMessage) ? "Processed" : "Error";
                    WriteMessage($"Path '{file}' {result}");
                }
            }
            else
            {

                try
                {
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(System.IO.File.ReadAllText((Path.Combine(Directory.GetCurrentDirectory(), "config.json"))));

                    WriteMessage("Loading settings from config.json.");

                    if (config != null)
                    {
                        cacheLength = config.CacheLength;
                        WriteMessage($"Cache length set to {cacheLength}");
                        port = config.Port;
                        WriteMessage($"Port set to {port}");
                        attemptToRefresh = !config.OfflineMode;
                        WriteMessage($"Offline mode is set to {!attemptToRefresh}");

                        localFloatingServer = config.LocalFloatingServer;
                        WriteMessage($"Local floating license server is set to {localFloatingServer}");

                        foreach (var file in config.ActivationFiles)
                        {
                            string result = Helpers.LoadLicenseFromPath(licenseCache, keysToUpdate, file, WriteMessage) ? "Processed" : "Error";
                            WriteMessage($"Path '{file}' {result}");
                        }
                    }
                }
                catch (Exception ex3)
                {
                    if (args.Length == 4)
                    {
                        port = Convert.ToInt32(args[0]);
                        cacheLength = Convert.ToInt32(args[1]);
                        attemptToRefresh = args[2] == "work-offline" ? false : true;

                        var paths = args[3].Split(';');

                        foreach (var path in paths)
                        {
                            string result = Helpers.LoadLicenseFromPath(licenseCache, keysToUpdate, path, WriteMessage) ? "Processed" : "Error";
                            WriteMessage($"Path '{path}' {result}");
                        }
                    }
                    if (args.Length == 3)
                    {
                        port = Convert.ToInt32(args[0]);
                        cacheLength = Convert.ToInt32(args[1]);
                        attemptToRefresh = args[2] == "work-offline" ? false : true;
                    }
                    else if (args.Length == 2)
                    {
                        port = Convert.ToInt32(args[0]);
                        cacheLength = Convert.ToInt32(args[1]);
                    }
                    else if (args.Length == 1)
                    {
                        port = Convert.ToInt32(args[0]);
                    }
                    else
                    {
                        Console.WriteLine("\nPlease enter the port on which the server will run (default is 8080):");

                        try
                        {
                            var portString = Console.ReadLine();

                            if (!string.IsNullOrWhiteSpace(portString))
                            {
                                port = Convert.ToInt32(portString);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteMessage("The port was incorrect.");
                            Console.ReadLine();
                            return;
                        }

                        Console.WriteLine("\nWould you like to enable caching of license files? If yes, please specify how often a license file should be updated. If you are not sure, keep the default value (default is 0):");

                        try
                        {
                            var cacheLengthString = Console.ReadLine();

                            if (!string.IsNullOrWhiteSpace(cacheLengthString))
                            {
                                cacheLength = Convert.ToInt32(cacheLengthString);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteMessage("The cache value could not be parsed. The default value will be used.");
                            Console.ReadLine();
                            return;
                        }

                        if (cacheLength > 0)
                        {
                            Console.WriteLine("\nWould you like the server to work offline? If yes, the server will always try to use cache before contacting Cryptolens [y/N] (default is N):");

                            if (Console.ReadLine() == "y")
                            {
                                attemptToRefresh = false;
                            }
                        }

                        Console.WriteLine("\nIf you have received a license file from your vendor, you can load it into the license server so that other " +
                            "applications on your network can access it. If you have multiple license files, they can either be separated with a semi-colon ';' or by specifying the folder (by default, no files will be loaded):");

                        var licenseFilePaths = Console.ReadLine();

                        if (string.IsNullOrWhiteSpace(licenseFilePaths))
                        {
                            WriteMessage("No license files were provided.");
                        }
                        else
                        {
                            var paths = licenseFilePaths.Split(';');

                            foreach (var path in paths)
                            {
                                string result = Helpers.LoadLicenseFromPath(licenseCache, keysToUpdate, path, WriteMessage) ? "Processed" : "Error";
                                WriteMessage($"Path '{path}' {result}");
                            }
                        }

                    }
                }
            }

            // inspired by https://www.codeproject.com/Tips/485182/%2FTips%2F485182%2FCreate-a-local-server-in-Csharp.

            try
            {
                httpListener.Prefixes.Add($"http://+:{port}/");
                httpListener.Start();
                WriteMessage("Starting server...");
            }
            catch (Exception ex)
            {
                WriteMessage("Error: Please run the license server as an administrator.\n\nDetailed error shown below: " + ex.StackTrace.ToString());
                Console.ReadLine();
                return;
            }
            WriteMessage("Server started.");

            try
            {
                WriteMessage($"Server address is: {GetLocalIPAddress()}:{port}");
            }
            catch (Exception ex)
            {
                WriteMessage("Could not get the IP of the license server.");
            }

            if (cacheLength > 0)
            {
                WriteMessage(Helpers.LoadFromLocalCache(licenseCache, WriteMessage));

                var tm = new System.Timers.Timer(3000);
                tm.Elapsed += Tm_Elapsed;
                tm.AutoReset = true;
                tm.Enabled = true;
            }

            Thread responseThread = new Thread(ResponseThread);
            responseThread.Start(); // start the response thread

        }

        private static void Tm_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Helpers.UpdateLocalCache(keysToUpdate);
        }

        static void ResponseThread()
        {
            while (true)
            {
                HttpListenerContext context = httpListener.GetContext(); // get a context
                try
                {
                    if (context.Request.Url.OriginalString.Contains("/api/"))
                    {
                        WriteMessage(context.Request.Url.ToString());

                        // adapted from https://stackoverflow.com/a/700307/1275924
                        var original = context.Request;
                        var pathAndQuery = new Uri(context.Request.Url.OriginalString).PathAndQuery;
                        HttpWebRequest newRequest = (HttpWebRequest)WebRequest.Create("https://app.cryptolens.io/" + pathAndQuery);

                        newRequest.ContentType = original.ContentType;
                        newRequest.Method = original.HttpMethod;
                        newRequest.UserAgent = original.UserAgent;

                        byte[] originalStream = Helpers.ReadToByteArray(original.InputStream, 1024);

                        if (original.HttpMethod == "GET")
                        {
                            throw new ArgumentException("GET requests are not supported.");
                        }
                        else
                        {
                            // for POST

                            if(Helpers.GetAPIMethod(pathAndQuery) == APIMethod.Activate) 
                            {
                                var activateResponse = Helpers.ProcessActivateRequest(originalStream, licenseCache, cacheLength, newRequest, context, keysToUpdate, attemptToRefresh, localFloatingServer, activatedMachinesFloating);

                                if (activateResponse != null)
                                {
                                    WriteMessage(activateResponse);
                                }
                            }
                            else
                            {
                                Stream reqStream = newRequest.GetRequestStream();

                                reqStream.Write(originalStream, 0, originalStream.Length);
                                reqStream.Close();

                                var output = Helpers.ReadToByteArray(newRequest.GetResponse().GetResponseStream());

                                context.Response.OutputStream.Write(output, 0, output.Length);
                                context.Response.KeepAlive = false;
                                context.Response.Close();
                            }

                        }
                    }
                    else
                    {
                        byte[] responseArray = Encoding.UTF8.GetBytes($"<html><head><title>Cryptolens License Server 2.0 -- port {port}</title></head>" +
                        $"<body><p>Welcome to the <strong>Cryptolens License Server 2.0</strong> -- port {port}! If you see this message, it means " +
                        "everything is working properly.</em></p><p>" +
                        "If you can find its documentation <a href='https://github.com/cryptolens/license-server'>here</a>." +
                        "</p></body></html>");
                        context.Response.OutputStream.Write(responseArray, 0, responseArray.Length); 
                        context.Response.KeepAlive = false; 
                        context.Response.Close();
                        WriteMessage("Web dashboard served successfully.");
                    }
                }
                catch(Exception ex)
                {
                    WriteMessage(ex.Message + "\n" + ex.InnerException.ToString() + "\n" + ex.StackTrace);
                }
                finally
                {
                    context.Response.Close();
                }
            }
        }

        private static void WriteMessage(string message)
        {
            var time = DateTime.UtcNow;
            Console.WriteLine($"[{time.ToShortDateString()} {time.ToShortTimeString()}] {message}");
        }


        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}
