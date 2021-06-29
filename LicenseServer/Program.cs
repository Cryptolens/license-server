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


        // A configuration can be generated on https://app.cryptolens.io/extensions/licenseserver
        // More instructions can be found here: https://github.com/Cryptolens/license-server/#floating-licenses-offline
        public static string ConfigurationFromCryptolens = "";


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

                if(config.ValidUntil < DateTimeOffset.UtcNow)
                {
                    WriteMessage($"Configuration data is outdated. Please contact the vendor to receive a new version of the license server.");
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
