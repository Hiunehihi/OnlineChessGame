using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

class LoadBalancer
{
    private static List<IPEndPoint> serverEndpoints = new List<IPEndPoint>
    {
        new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8001),
        new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8002),
        new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8003)
    };

    private static int roundRobinIndex = 0; 
    private static readonly object lockObject = new object(); 

    static void Main(string[] args)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, 8080);
        listener.Start();
        Console.WriteLine("Load Balancer is running on port 8080...");

        while (true)
        {
            var client = listener.AcceptTcpClient();
            Console.WriteLine("Client connected.");

            ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
        }
    }

    private static IPEndPoint SelectServer()
    {
        lock (lockObject) 
        {
            var selectedServer = serverEndpoints[roundRobinIndex];
            roundRobinIndex = (roundRobinIndex + 1) % serverEndpoints.Count; 
            return selectedServer;
        }
    }

    private static void HandleClient(TcpClient client)
    {
        var serverEndpoint = SelectServer();
        try
        {
            using (TcpClient server = new TcpClient())
            {
                Console.WriteLine($"Attempting to connect to server {serverEndpoint.Address}:{serverEndpoint.Port}...");
                server.Connect(serverEndpoint);
                Console.WriteLine($"Connected to server {serverEndpoint.Address}:{serverEndpoint.Port}.");

                using (NetworkStream clientStream = client.GetStream())
                using (NetworkStream serverStream = server.GetStream())
                {
                    RelayData(clientStream, serverStream);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to server {serverEndpoint.Address}:{serverEndpoint.Port}. Error: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }

    private static void RelayData(NetworkStream clientStream, NetworkStream serverStream)
    {
        var clientToServer = new Thread(() => ForwardData(clientStream, serverStream));
        var serverToClient = new Thread(() => ForwardData(serverStream, clientStream));

        clientToServer.Start();
        serverToClient.Start();

        clientToServer.Join();
        serverToClient.Join();
    }

    private static void ForwardData(NetworkStream input, NetworkStream output)
    {
        try
        {
            byte[] buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, bytesRead);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Data relay error: {ex.Message}");
        }
    }
}
