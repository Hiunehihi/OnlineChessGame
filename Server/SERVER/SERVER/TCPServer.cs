using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
namespace chess
{
    internal class TCPServer
    {
        public enum PieceColor
        {
            White,
            Black
        }

        private TcpListener tcpListener;
        private Queue<TcpClient> waitingPlayers = new Queue<TcpClient>();
        private const string connectionString = "Data Source=players.db;Version=3;";
        private List<GameRoom> gameRooms = new List<GameRoom>();
        private ConcurrentDictionary<string, TcpClient> activeSessions = new ConcurrentDictionary<string, TcpClient>();
        private TcpClient tcpClient;
        private NetworkStream stream2;
        public TCPServer(int localPort)
        {
            tcpListener = new TcpListener(IPAddress.Any, localPort);
            CreateDatabase();
        }

        public async Task StartAsync()
        {
            tcpListener.Start();
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", 9000);
            stream2 = tcpClient.GetStream();
            Console.WriteLine("Server started, waiting for connections...");
            _ = ListenToMasterServer();

            while (true)
            {
                TcpClient client = await tcpListener.AcceptTcpClientAsync();
                Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");
                _ = HandleClientAsync(client); // Xử lý mỗi client trong một task riêng biệt
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"Client disconnected: {client.Client.RemoteEndPoint}");
                        HandleDisconnectedPlayer(client);
                        break;
                    }

                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"Received: {request}");
                    string response = await ProcessRequest(request, client);
                    if (response == "") continue;
                    byte[] responseData = Encoding.UTF8.GetBytes(EncryptData(response, "3x@mpl3$tr0ngK#y!") + "\n");
                    await stream.WriteAsync(responseData, 0, responseData.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                HandleDisconnectedPlayer(client); 
            }
            finally
            {
                if (client.Connected)
                {
                    client.Close();
                }
                Console.WriteLine("Client disconnected.");
            }
        }
        private async Task ListenToMasterServer()
        {
            while (true)
            {
                try
                {
                    string response = await ReceiveResponseAsync();

                    if (!string.IsNullOrEmpty(response))
                    {
                        Console.WriteLine($"Processed message from Master Server: {response}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in ListenToMasterServer: {ex.Message}");
                    break;
                }
            }
        }
        private void HandleDisconnectedPlayer(TcpClient client)
        {
            lock (waitingPlayers)
            {
                if (waitingPlayers.Contains(client))
                {
                    waitingPlayers = new Queue<TcpClient>(waitingPlayers.Where(p => p != client));
                    Console.WriteLine("Client removed from waiting queue.");
                }
            }
            var room = gameRooms.FirstOrDefault(g => g.HasPlayer(client));
            if (room != null)
            {
                TcpClient opponent = (room.Player1 == client) ? room.Player2 : room.Player1;
                room.RemovePlayer(client);
                Console.WriteLine("Client removed from room.");

                if (opponent != null)
                {
                    SendMessage(opponent, "NOTIFY: Opponent has left the game.");
                    Console.WriteLine("Notified opponent about disconnection.");
                }

                if (room.IsEmpty())
                {
                    gameRooms.Remove(room);
                    Console.WriteLine($"Room {room.RoomId} has been removed.");
                }
            }
            var username = activeSessions.FirstOrDefault(x => x.Value == client).Key;
            if (!string.IsNullOrEmpty(username))
            {
                activeSessions.TryRemove(username, out _);
                Console.WriteLine($"User {username} logged out.");
            }
        }


        private async Task<string> ProcessRequest(string EncryptRequest, TcpClient client)
        {
            string request = DecryptData(EncryptRequest, "3x@mpl3$tr0ngK#y!");
            Console.WriteLine(request);
            string[] parts = request.Split(' ');
            string command = parts[0].ToUpper(); // Chuyển sang chữ hoa để so sánh không phân biệt chữ hoa chữ thường
            string message = request.Substring(5);
            switch (command)
            {
                case "REGISTER":
                    return parts.Length >= 3 ? RegisterPlayer(parts[1], parts[2]) : "ERROR: Invalid request format";
                case "LOGIN":
                    return parts.Length >= 3 ? LoginPlayer(parts[1], parts[2], client) : "ERROR: Invalid request format";
                case "CREATE_ROOM":
                    return parts.Length == 2 ? await CreateRoom(parts[1], client) : "ERROR: Invalid request format";
                case "JOIN_ROOM":
                    return parts.Length == 2 ? await JoinRoom(parts[1], client) : "ERROR: Invalid request format";
                case "MOVE":
                    return parts.Length >= 3 ? await HandleMove(parts[1], parts[2], client) : "ERROR: Invalid move format";
                case "CHAT":
                    return await HandleChat(message, client);
                case "RESTART":
                    return await HandleRestart(client);
                case "EXIT_ROOM":
                    return HandleExitRoom(client);
                case "EXIT_ROOM_2":
                    return await HandleExitRoom2(client);
                case "LOGOUT":
                    return LogoutPlayer(client);
                case "EXIT_WAITING":
                    return await HandleExitWaiting(client);
                default:
                    return "ERROR: Unknown command";
            }
        }
        private async Task<string> HandleExitRoom2(TcpClient client)
        {
            var room = gameRooms.FirstOrDefault(g => g.HasPlayer(client));
            if (room == null)
                return "Error: Room not found.";
            if (room.Player1 != null && room.Player2 != null)
            {
                room.RemovePlayer(client);
                await SendRequest($"EXIT_ROOM {room.RoomId}");
                return "SUCCESS";
            }
            await SendRequest($"EXIT_ROOM {room.RoomId}");
            room.RemovePlayer(client);
            if (room.IsEmpty())
            {
                gameRooms.Remove(room);
            }
            return "SUCCESS";
        }
        private async Task<string> HandleExitWaiting(TcpClient client)
        {
            var room = gameRooms.FirstOrDefault(g => g.HasPlayer(client));
            await SendRequest($"EXIT_WAITING");
            if (room == null)
            {
                return "ERROR: Room not found.";
            }
            room.RemovePlayer(client);
            if (room.IsEmpty())
            {
                gameRooms.Remove(room);
            }
            return "EXIT_WAITING";
        }
        private string HandleExitRoom(TcpClient client)
        {
            var room = gameRooms.FirstOrDefault(g => g.HasPlayer(client));
            if (room == null)
                return "EXITROOM: Room not found.";
            room.RemovePlayer(client);
            if (room.Player1 != null)
            {
                SendMessage(room.Player1, "NOTIFY: Player has left the room");
                room.RemovePlayer(room.Player1);
            } else if (room.Player2 != null)
            {
                SendMessage(room.Player2, "NOTIFY: Player has left the room");
                room.RemovePlayer(room.Player2);
            }
            if (room.IsEmpty())
            {
                gameRooms.Remove(room);
            }
            return $"EXIT_ROOM";

        }
        private string LogoutPlayer(TcpClient client)
        {
            string username = activeSessions.FirstOrDefault(x => x.Value == client).Key;

            if (string.IsNullOrEmpty(username))
            {
                return "ERROR: User not logged in.";
            }
            bool removedFromActiveSessions = activeSessions.TryRemove(username, out _);
            if (removedFromActiveSessions)
            {
                Console.WriteLine($"User {username} has been removed from active sessions.");
            }
            lock (gameRooms)
            {
                var room = gameRooms.FirstOrDefault(g => g.HasPlayer(client));
                if (room != null)
                {
                    room.RemovePlayer(client);
                    if (room.Player1 == null && room.Player2 == null)
                    {
                        gameRooms.Remove(room);
                        Console.WriteLine($"Game room {room.RoomId} has been removed.");
                    }
                }
            }

            Console.WriteLine($"User {username} has successfully logged out.");
            return "SUCCESS: Logged out.";
        }
        private async Task<string> HandleRestart(TcpClient client)
        {
            var room = gameRooms.FirstOrDefault(g => g.HasPlayer(client));
            if (room == null)
                return "Error: Room not found.";
            if (room.Player1 != null && room.Player2 != null)
            {
                room.SetPlayerWantsRestart(client, true);
                if (room.AllPlayersWantRestart())
                {
                    room.SetPlayerWantsRestart(room.Player1, false);
                    room.SetPlayerWantsRestart(room.Player2, false);
                    SendMessage(room.Player1, "SUCCESS: Restart game.");
                    SendMessage(room.Player2, "SUCCESS: Restart game.");
                    return "";
                }
                else
                {
                    return "WAITING: Waiting for other to restart game.";
                }
            }
            await SendRequest($"RESTART {room.RoomId}");
            return "";
        }
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower(); // Chuyển thành chuỗi hex
            }
        }
        private string RegisterPlayer(string username, string password)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "INSERT INTO players (username, password) VALUES (@username, @password)";
                using (var command = new SQLiteCommand(query, connection))
                {
                    string hashedPassword = HashPassword(password); // Băm mật khẩu
                    command.Parameters.AddWithValue("@username", username);
                    command.Parameters.AddWithValue("@password", hashedPassword);
                    try
                    {
                        command.ExecuteNonQuery();
                        return "SUCCESS: Registered";
                    }
                    catch (SQLiteException ex)
                    {
                        // Lỗi khóa duy nhất (unique constraint)
                        if (ex.ResultCode == SQLiteErrorCode.Constraint)
                        {
                            return "ERROR: Username already exists";
                        }
                        return "ERROR: Database error: " + ex.Message;
                    }
                }
            }
        }

        private string LoginPlayer(string username, string password, TcpClient client)
        {
            if (activeSessions.ContainsKey(username))
            {
                Console.WriteLine($"User {username} is already logged in.");
                return "ERROR: Tài khoản đã được đăng nhập trên một thiết bị khác.";
            }
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM players WHERE username = @username AND password = @password";
                using (var command = new SQLiteCommand(query, connection))
                {
                    string hashedPassword = HashPassword(password); // Băm mật khẩu trước khi so sánh
                    command.Parameters.AddWithValue("@username", username);
                    command.Parameters.AddWithValue("@password", hashedPassword);
                    long count = (long)command.ExecuteScalar();
                    if (count > 0)
                    {
                        activeSessions[username] = client;
                        return "SUCCESS: logged in";
                    }
                    else return "ERROR: Error from log in!";
                }
            }
        }


        private async Task<string> CreateRoom(string roomId, TcpClient client)
        {
            if (gameRooms.Any(r => r.RoomId == roomId))
            {
                return "ERROR: Room already exists.";
            }
            GameRoom g = new GameRoom(roomId, client);
            gameRooms.Add(g);
            await SendRequest($"CREATE_ROOM {roomId}");
            return "";
        }

        private async Task<string> JoinRoom(string roomId, TcpClient client)
        {
            var room = gameRooms.FirstOrDefault(r => r.RoomId == roomId);
            if (room == null)
            {
                GameRoom g = new GameRoom(roomId, client);
                gameRooms.Add(g);
                await SendRequest($"JOIN_ROOM {roomId}");
                return "";
            }
            room.AddPlayer(client);
            await SendRequest($"JOIN_ROOM {roomId}");
            return "";
        }
        private async Task<string> HandleMove(string from, string to, TcpClient client)
        {
            var room = gameRooms.FirstOrDefault(r => r.HasPlayer(client));
            if (room != null)
            {
                if (room.Player1 != null && room.Player2 != null)
                {
                    if (room.Player1 == client)
                    {
                        SendMessage(room.Player2, $"MOVE {from} {to}");
                        return "SUCCESS: Move sent.";
                    } else
                    {
                        SendMessage(room.Player1, $"MOVE {from} {to}");
                        return "SUCCESS: Move sent.";
                    }
                }
                await SendRequest($"MOVE {room.RoomId} {from} {to}");
                return "";
            }
            return "ERROR: Error from send move";
        }

        private async Task<string> HandleChat(string message, TcpClient client)
        {
            var room = gameRooms.FirstOrDefault(g => g.HasPlayer(client));
            if (room == null)
            {
                return "ERROR: Not in a game room.";
            }

            if (room.Player1 != null && room.Player2 != null) 
            {
                room.SendChatMessage(message, client);
                return "SUCCESS: Message sent";
            }
            await SendRequest($"CHAT {room.RoomId} {message}");
            return "";
        }
        private string DecryptData(string encryptedText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
                byte[] fullCipher = Convert.FromBase64String(encryptedText);

                byte[] iv = new byte[16];
                byte[] cipherText = new byte[fullCipher.Length - iv.Length];
                Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(fullCipher, iv.Length, cipherText, 0, cipherText.Length);

                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    byte[] plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                    return Encoding.UTF8.GetString(plainBytes);
                }
            }
        }
        private string EncryptData(string plainText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    byte[] result = new byte[iv.Length + encryptedBytes.Length];
                    Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                    Buffer.BlockCopy(encryptedBytes, 0, result, iv.Length, encryptedBytes.Length);

                    return Convert.ToBase64String(result);
                }
            }
        }
        private async void SendMessage(TcpClient client, string message)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(EncryptData(message, "3x@mpl3$tr0ngK#y!") + "\n");
                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }
        private async Task SendRequest(string request)
        {
            try
            {
                if (tcpClient == null || !tcpClient.Connected)
                {
                    Console.WriteLine("Not connected to Master Server. Attempting to reconnect...");
                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync("127.0.0.1", 9000);
                    stream2 = tcpClient.GetStream();
                    if (tcpClient == null || !tcpClient.Connected)
                    {
                        Console.WriteLine("Failed to connect to server. Cannot send request.");
                        return;
                    }
                }
                byte[] data = Encoding.UTF8.GetBytes(request + '\n');
                await stream2.WriteAsync(data, 0, data.Length);
            } catch (Exception ex)
            {
                Console.WriteLine($"Error sending request: {ex.Message}");
            }
        }
        public async Task<string> ReceiveResponseAsync()
        {
            StringBuilder response = new StringBuilder();
            int bytesRead;
            byte[] buffer = new byte[1024];
            while (true)
            {
                try
                {
                    bytesRead = await stream2.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        response.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                        if (response.ToString().Contains("\n"))
                        {
                            string fullResponse = response.ToString().Trim();
                            Console.WriteLine($"Received from master server: {fullResponse}");

                            ProcessMasterMessage(fullResponse);
                            return fullResponse;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Master server closed connection.");
                        return "ERROR: Master server closed connection.";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving response: {ex.Message}");
                    return $"ERROR: {ex.Message}";
                }
            }
        }
        private void ProcessMasterMessage(string message)
        {
            string[] parts = message.Split(' ');
            string command = parts[0].ToUpper();

            switch (command)
            {
                case "ROOM_ALREADY_EXIST":
                    {
                        var room = gameRooms.FirstOrDefault(g => g.RoomId == parts[1]);
                        if (room != null)
                        {
                            SendMessage(room.Player1, "ERROR: Room already exist.");
                            Console.WriteLine("Message sent: ERROR: Room already exist.");
                            gameRooms.Remove(room);
                        }
                    }
                    break;
                case "SUCCESS":
                    break;
                case "MOVE":
                    if (parts.Length == 4)
                    {
                        string roomId = parts[1];
                        string from = parts[2];
                        string to = parts[3];
                        var room = gameRooms.FirstOrDefault(r => r.RoomId == roomId);
                        if (room != null)
                        {
                            TcpClient opponent = room.Player1;
                            SendMessage(opponent, $"MOVE {from} {to}");
                            if (opponent != null)
                            {
                                Console.WriteLine($"MOVE {from}->{to} sent to opponent in Room {roomId}.");
                            }
                        }
                    }
                    break;
                case "ROOM_CREATED":
                    {
                        var room = gameRooms.FirstOrDefault(r => r.RoomId == parts[1]);
                        if (room != null)
                        {
                            SendMessage(room.Player1, $"ROOM_CREATED {parts[1]}");
                            Console.WriteLine("Message sent: ROOM_CREATED");
                        }
                    }
                    break;
                case "CHAT":
                    {
                        var room = gameRooms.FirstOrDefault(r => r.RoomId == parts[1]);
                        if (room != null) SendMessage(room.Player1, parts[0] + " " + parts[2]);
                    }
                    break;
                case "ROOM_NOT_FOUND":
                    {
                        var room = gameRooms.FirstOrDefault(g => g.RoomId == parts[1]);
                        if (room != null)
                        {
                            SendMessage(room.Player1, "FAILED: Failed to join room.");
                            Console.WriteLine("Message sent: FAILED: Failed to join room.");
                            gameRooms.Remove(room);
                        }
                    }
                    break;
                case "ROOM_IS_FULL":
                    {
                        string roomId = parts[1];
                        var room = gameRooms.FirstOrDefault(g => g.RoomId == roomId);
                        if (room != null)
                        {
                            SendMessage(room.Player1, "FAILED: Failed to join room.");
                            Console.WriteLine("Message sent: FAILED: Failed to join room.");
                            gameRooms.Remove(room);
                        }
                    }
                    break;
                case "JOINED_ROOM":
                    {
                        string roomId = parts[1];
                        var room = gameRooms.FirstOrDefault(g => g.RoomId == roomId);
                        if (room != null)
                        {
                            SendMessage(room.Player1, "SUCCESS: Joined room.");
                            Console.WriteLine("Message sent: SUCCESS: Joined room.");
                        }
                    }
                    break;
                case "OPPONENT_JOINED_ROOM":
                    {
                        string roomId = parts[1];
                        var room = gameRooms.FirstOrDefault(g => g.RoomId == roomId);
                        if (room != null)
                        {
                            SendMessage(room.Player1, "JOIN_ROOM: A player has joined the room.");
                            Console.WriteLine("Message sent: JOIN_ROOM: A player has joined the room.");
                        }
                    }
                    break;
                case "SUCCESS_RESTART":
                    {
                        string roomId = parts[1];
                        var room = gameRooms.FirstOrDefault(g => g.RoomId == roomId);
                        if (room != null)
                        {
                            SendMessage(room.Player1, "SUCCESS: Restart game");
                            Console.WriteLine("Message sent: SUCCESS: Restart game");
                        }
                    }
                    break;
                case "WAITING_RESTART":
                    {
                        string roomId = parts[1];
                        var room = gameRooms.FirstOrDefault(g => g.RoomId == roomId);
                        if (room != null)
                        {
                            SendMessage(room.Player1, "WAITING: Waiting for other to restart game.");
                            Console.WriteLine("Message sent: WAITING: Waiting for other to restart game.");
                        }
                    }
                    break;
                case "ERROR":
                    {
                        if (message == $"ERROR {parts[1]}: Room already exists." || message == $"ERROR {parts[1]}: Room not found." || message == $"ERROR {parts[1]}: Room is full.")
                        {
                            var room = gameRooms.FirstOrDefault(r => r.RoomId == parts[1]);
                            if (room != null)
                            {
                                SendMessage(room.Player1, message);
                                gameRooms.Remove(room);
                            }
                        } 
                    }
                    break;
                case "INFO":
                    {                     
                        var room = gameRooms.FirstOrDefault(r => r.RoomId == parts[1]);
                        if (room != null) SendMessage(room.Player1, $"JOIN_ROOM {parts[1]}");
                    }
                    break;
                default:
                    Console.WriteLine($"Unknown command from master server: {message}");
                    break;
            }
        }
        private void CreateDatabase()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS players (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        username TEXT UNIQUE NOT NULL,
                        password TEXT NOT NULL
                    );";
                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }
    }

    internal class GameRoom
    {
        public string RoomId { get; set; }
        public TcpClient Player1 { get;  set; }
        public TcpClient Player2 { get;set; }
        private int currentPlayerIndex = 0;

        public GameRoom(string roomId, TcpClient player1)
        {
            RoomId = roomId;
            Player1 = player1;
        }
        public GameRoom(TcpClient player1, TcpClient player2)
        {
            Player1 = player1;
            Player2 = player2;
        }
        public bool IsFull()
        {
            return Player2 != null;
        }

        public void AddPlayer(TcpClient client)
        {
            if (Player2 == null)
            {
                Player2 = client;
            }
        }

        public bool HasPlayer(TcpClient client)
        {
            return Player1 == client || Player2 == client;
        }

        public bool IsCurrentPlayer(TcpClient client)
        {
            return (currentPlayerIndex == 0 && Player1 == client) || (currentPlayerIndex == 1 && Player2 == client);
        }

        public void SendMove(string from, string to, TcpClient client)
        {
            TcpClient opponent = (client == Player1) ? Player2 : Player1;
            SendMessage(opponent, $"MOVE {from} {to}");
            UpdateCurrentPlayer();
        }

        private void UpdateCurrentPlayer()
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % 2;
        }
        private string EncryptData(string plainText, string key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
                aes.IV = new byte[16];

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    return Convert.ToBase64String(encryptedBytes);
                }
            }
        }
        private async void SendMessage(TcpClient client, string message)
        {
            
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(EncryptData(message, "3x@mpl3$tr0ngK#y!") + "\n");
                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public void SendChatMessage(string message, TcpClient sender)
        {
            TcpClient recipient = sender == Player1 ? Player2 : Player1;
            SendMessage(recipient, $"CHAT {message}");
        }

        public void RemovePlayer(TcpClient client)
        {
            if (Player1 == client)
            {
                Player1 = null;
                Console.WriteLine("Removed client from game room");
            } else
            if (Player2 == client)
            {
                Player2 = null;
                Console.WriteLine("Removed client from game room");
            }
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
            return restartFlags.ContainsKey(Player1) && restartFlags[Player1] && restartFlags.ContainsKey(Player2) && restartFlags[Player2];
        }

        public void ClearRestartFlags()
        {
            restartFlags.Clear();
        }
        public bool IsEmpty()
        {
            return Player1 == null && Player2 == null;
        }
    }
}
