using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
    internal class Program
    {
        private List<GameRoom> gameRooms = new List<GameRoom>();

        static async Task Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 9000);
            listener.Start();
            Console.WriteLine("Master Server started, waiting for connections...");

            Program programInstance = new Program();
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"Worker server connected: {client.Client.RemoteEndPoint}");
                _ = programInstance.HandleWorkerAsync(client);
            }
        }

        private async Task HandleWorkerAsync(TcpClient worker)
        {
            NetworkStream stream = worker.GetStream();
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"Worker server disconnected: {worker.Client.RemoteEndPoint}");
                        break;
                    }

                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"Received: {request}");
                    await ProcessRequest(request, worker);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                worker.Close();
            }
        }

        private async Task ProcessRequest(string request, TcpClient worker)
        {
            string[] parts = request.Split(' ');
            string command = parts[0].ToUpper();

            switch (command)
            {
                case "CREATE_ROOM":
                    await HandleCreateRoom(parts, worker);
                    break;
                case "JOIN_ROOM":
                    await HandleJoinRoom(parts, worker);
                    break;
                case "MOVE":
                    await HandleMove(parts, worker);
                    break;
                case "CHAT":
                    await HandleChat(parts, worker);
                    break;
                case "EXIT_ROOM":
                    await HandleExitRoom(parts, worker);
                    break;
                case "RESTART":
                    await HandleRestart(parts, worker);
                    break;
                default:
                    Console.WriteLine("Unknown command received.");
                    break;
            }
        }
        private async Task HandleRestart(string[] parts, TcpClient worker)
        {
            if (parts.Length != 2)
            {
                await SendMessageToWorker(worker, "INVAID_RESTART_FORMAT");
                return;
            }
            var room = gameRooms.FirstOrDefault(g => g.RoomId == parts[1]);
            if (room != null)
            {
                room.SetPlayerWantsRestart(worker, true);
                if (room.AllPlayersWantRestart())
                {
                    room.SetPlayerWantsRestart(room.Worker1, false);
                    room.SetPlayerWantsRestart(room.Worker2, false);
                    await SendMessageToWorker(room.Worker2, $"SUCCESS_RESTART {parts[1]}");
                    await SendMessageToWorker(room.Worker1, $"SUCCESS_RESTART {parts[1]}");
                } else
                {
                    await SendMessageToWorker(worker, $"WAITING_RESTART {parts[1]}");
                }
            }

        }
        private async Task HandleExitRoom(string[] parts, TcpClient worker)
        {
            if (parts.Length != 2)
            {
                await SendMessageToWorker(worker, "INVAID_EXIT_ROOM_FORMAT");
                return;
            }
            var room = gameRooms.FirstOrDefault(g => g.RoomId == parts[1]);
            if (room != null)
            {
                if (room.Worker1 == room.Worker2 && room.Worker1 == worker)
                {
                    room.RemovePlayer(room.Worker1);
                }
                room.RemovePlayer(worker);
                if (room.IsEmpty())
                {
                    gameRooms.Remove(room);
                }
                await SendMessageToWorker(worker, $"SUCCESS_EXIT_ROOM {parts[1]}");
            }
        }
        private async Task HandleCreateRoom(string[] parts, TcpClient worker)
        {
            if (parts.Length != 2)
            {
                await SendMessageToWorker(worker, "INVAID_CREATE_ROOM_FORMAT");
                return;
            }

            string roomId = parts[1];
            if (gameRooms.Any(r => r.RoomId == roomId))
            {
                await SendMessageToWorker(worker, $"ROOM_ALREADY_EXIST {roomId}");
                return;
            }

            gameRooms.Add(new GameRoom(roomId, worker));
            await SendMessageToWorker(worker, $"ROOM_CREATED {roomId}");
        }

        private async Task HandleJoinRoom(string[] parts, TcpClient worker)
        {
            if (parts.Length != 2)
            {
                await SendMessageToWorker(worker, "INVAID_JOIN_ROOM_FORMAT");
                return;
            }

            string roomId = parts[1];
            var room = gameRooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room == null)
            {
                await SendMessageToWorker(worker, $"ROOM_NOT_FOUND {roomId}");
                return;
            }

            if (room.Worker2 != null)
            {
                await SendMessageToWorker(worker, $"ROOM_IS_FULL {roomId}");
                return;
            }

            room.Worker2 = worker;
            await SendMessageToWorker(worker, $"JOINED_ROOM {roomId}");
            await SendMessageToWorker(room.Worker1, $"OPPONENT_JOINED_ROOM {roomId}");
        }

        private async Task HandleMove(string[] parts, TcpClient worker)
        {
            if (parts.Length != 4)
            {
                await SendMessageToWorker(worker, "INVAID_MOVE_FORMAT");
                return;
            }

            string from = parts[2];
            string to = parts[3];

            var room = gameRooms.FirstOrDefault(r => r.RoomId == parts[1]);
            if (room == null)
            {
                await SendMessageToWorker(worker, "ROOM_NOT_FOUND");
                return;
            }

            TcpClient opponentWorker = room.GetOpponentWorker(worker);
            if (opponentWorker != null)
            {
                await SendMessageToWorker(opponentWorker, $"MOVE {room.RoomId} {from} {to}");
            }
        }

        private async Task HandleChat(string[] parts, TcpClient worker)
        {
            if (parts.Length < 3)
            {
                await SendMessageToWorker(worker, "INVAID_CHAT_FORMAT");
                return;
            }

            string message = string.Join(' ', parts.Skip(2));

            var room = gameRooms.FirstOrDefault(r => r.RoomId == parts[1]);
            if (room == null)
            {
                await SendMessageToWorker(worker, $"ROOM_NOT_FOUND {parts[1]}");
                return;
            }

            TcpClient opponentWorker = room.GetOpponentWorker(worker);
            if (opponentWorker != null)
            {
                await SendMessageToWorker(opponentWorker, $"CHAT {room.RoomId} {message}");
            }
        }

        private async Task SendMessageToWorker(TcpClient worker, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                NetworkStream stream = worker.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message to worker: {ex.Message}");
            }
        }
    }

    internal class GameRoom
    {
        public string RoomId { get; set; }
        public TcpClient Worker1 { get; set; }
        public TcpClient Worker2 { get; set; }

        public GameRoom(string roomId, TcpClient worker1)
        {
            RoomId = roomId;
            Worker1 = worker1;
            Worker2 = null;
        }

        public bool HasWorker(TcpClient worker)
        {
            return Worker1 == worker || Worker2 == worker;
        }
        public void RemovePlayer(TcpClient client)
        {
            if (Worker1 == client)
            {
                Worker1 = null;
                Console.WriteLine("Removed client from game room");
            }
            else
            if (Worker2 == client)
            {
                Worker2 = null;
                Console.WriteLine("Removed client from game room");
            }
        }
        public TcpClient GetOpponentWorker(TcpClient worker)
        {
            return Worker1 == worker ? Worker2 : Worker1;
        }
        public bool IsEmpty()
        {
            return Worker1 == null && Worker2 == null;
        }
        private Dictionary<TcpClient, bool> restartFlags = new Dictionary<TcpClient, bool>();

        public void SetPlayerWantsRestart(TcpClient client, bool wantsRestart)
        {
            if (restartFlags.ContainsKey(client))
                restartFlags[client] = wantsRestart;
            else
                restartFlags.Add(client, wantsRestart);
        }

        public bool AllPlayersWantRestart()
        {
            return restartFlags.ContainsKey(Worker1) && restartFlags[Worker1] && restartFlags.ContainsKey(Worker2) && restartFlags[Worker2];
        }

        public void ClearRestartFlags()
        {
            restartFlags.Clear();
        }
    }
}
