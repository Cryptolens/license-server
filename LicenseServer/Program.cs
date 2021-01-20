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
using System.Collections.Specialized;

namespace LicenseServer
{
    class Program
    {
        static HttpListener httpListener = new HttpListener();

        public static int port = 8080;
        public static bool logging = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Cryptolens License Server v1.0\n");

            if(args.Length == 2)
            {
                port = Convert.ToInt32(args[0]);
                logging = Convert.ToBoolean(args[1]);
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

                    if(!string.IsNullOrWhiteSpace(portString))
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

                Console.WriteLine("\nWould you like to enable local logging [y/N]? This will create a local sqlite database in the same folder as the executable.");

                if(Console.ReadLine() == "y")
                {
                    logging = true;
                    WriteMessage("Logging enabled.");
                    Helpers.InitDB();
                }
                else
                {
                    WriteMessage("Logging disabled.");
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
            catch(Exception ex)
            {
                WriteMessage("Could not get the IP of the license server.");
            }

            Thread responseThread = new Thread(ResponseThread);
            responseThread.Start(); // start the response thread
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
                        HttpWebRequest newRequest = (HttpWebRequest)WebRequest.Create("https://app.cryptolens.io/" + new Uri(context.Request.Url.OriginalString).PathAndQuery);

                        newRequest.ContentType = original.ContentType;
                        newRequest.Method = original.HttpMethod;
                        newRequest.UserAgent = original.UserAgent;

                        byte[] originalStream = ReadToByteArray(original.InputStream, 1024);

                        if (original.HttpMethod == "GET")
                        {
                            throw new ArgumentException("GET requests are not supported.");
                        }
                        else
                        {
                            // for POST

                            Stream reqStream = newRequest.GetRequestStream();

                            reqStream.Write(originalStream, 0, originalStream.Length);
                            reqStream.Close();

                            var output = ReadToByteArray(newRequest.GetResponse().GetResponseStream());

                            context.Response.OutputStream.Write(output, 0, output.Length);
                            context.Response.KeepAlive = false; 
                            context.Response.Close();

                            if (logging)
                            {
                                NameValueCollection coll = HttpUtility.ParseQueryString(System.Text.UTF8Encoding.UTF8.GetString(originalStream));

                                try
                                {
                                    Helpers.UpdateUser(Convert.ToInt32(coll["ProductId"]), coll["Key"], coll["MachineCode"], "no user");
                                }
                                catch(Exception ex)
                                {

                                }
                            }
                        }
                    }
                    else
                    {
                        byte[] responseArray = Encoding.UTF8.GetBytes($"<html><head><title>Cryptolens License Server -- port {port}</title></head>" +
                        $"<body>Welcome to the <strong>Cryptolens License Server</strong> -- port {port}! If you see this message, it means " +
                        "everything is working properly.</em></body></html>");
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

        private static byte[] ReadToByteArray(Stream inputStream, int v = 1024)
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
