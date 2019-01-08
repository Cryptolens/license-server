using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Threading;
using System.IO;
using System.Web;

namespace LicenseServer
{
    class Program
    {
        static HttpListener httpListener = new HttpListener();

        public static int port = 80;

        static void Main(string[] args)
        {
            Console.WriteLine("Cryptolens License Server v1.0\n");

            if (args.Length == 1)
            {
                port = Convert.ToInt32(args[0]);
            }

            // inspired by https://www.codeproject.com/Tips/485182/%2FTips%2F485182%2FCreate-a-local-server-in-Csharp.

            try
            {
                httpListener.Prefixes.Add($"http://+:{port}/"); 
                httpListener.Start();
                Console.WriteLine("Starting server...");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Please run the license server as an administrator.");
                Console.WriteLine("\nDetailed error shown below:");
                Console.WriteLine(ex.StackTrace);
                Console.ReadLine();
                return;
            }
            Console.WriteLine("Server started.");
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
                        Console.WriteLine(context.Request.Url);

                        // adapted from https://stackoverflow.com/a/700307/1275924
                        var original = context.Request;
                        HttpWebRequest newRequest = (HttpWebRequest)WebRequest.Create("https://app.cryptolens.io/");

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
                        }
                    }
                    else
                    {
                        byte[] responseArray = Encoding.UTF8.GetBytes($"<html><head><title>Cryptolens License Server -- port {port}</title></head>" +
                        $"<body>Welcome to the <strong>Cryptolens License Server</strong> -- port {port}!. If you see this message, it means " +
                        "everything is working properly.</em></body></html>");
                        context.Response.OutputStream.Write(responseArray, 0, responseArray.Length); 
                        context.Response.KeepAlive = false; 
                        context.Response.Close(); 
                        Console.WriteLine("Respone given to a request.");
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.InnerException);
                    Console.WriteLine(ex.StackTrace);
                }
                finally
                {
                    context.Response.Close();
                }
            }
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
    }
}
