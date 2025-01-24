using chess;
using System;
using System.Threading.Tasks;

namespace chess 
{
    class Program
    {
        static async Task Main(string[] args)
        {

            int port = args.Length > 0 ? int.Parse(args[0]) : 8080;

            Console.WriteLine($"Starting worker server on port {port}...");
            TCPServer server = new TCPServer(port);
            await server.StartAsync();
        }
    }
}