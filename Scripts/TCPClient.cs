using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Globalization;
using System.Threading;
using System.Security.Cryptography;

public class TCPClient : MonoBehaviour
{
    private TcpClient tcpClient;
    public NetworkStream stream;
    private byte[] buffer = new byte[1024];
    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    private string serverAddress = "127.0.0.1";  //171.246.125.209
    private int serverPort = 8080;           

    public bool isConnected()
    {
        return tcpClient.Connected;
    }
    public async Task ConnectToServer()
    {
        try
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                Debug.Log("Already connected to server.");
                return;
            }

            tcpClient = new TcpClient();
            Debug.Log($"Connecting to server {serverAddress}:{serverPort}...");
            await tcpClient.ConnectAsync(serverAddress, serverPort);
            stream = tcpClient.GetStream();

            Debug.Log("Connected to server successfully.");
        }
        catch (SocketException ex)
        {
            Debug.LogError($"SocketException: {ex.Message}. Ensure the server is running and reachable.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Unexpected error during connection: {ex.Message}");
        }
    }
    public void DisconnectFromServer()
    {
        try
        {
            if (tcpClient != null && tcpClient.Connected)
            {
                stream?.Close();
                tcpClient.Close();
                Debug.Log("Connection to server closed.");
            }

            tcpClient = new TcpClient();
            Debug.Log($"Reconnecting to server {serverAddress}:{serverPort}...");
            tcpClient.Connect(serverAddress, serverPort);
            stream = tcpClient.GetStream();
            Debug.Log("Reconnected to server successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reconnecting to server: {ex.Message}");
        }
    }    
       
    public async Task<string> Register(string username, string password)
    {
        string request = $"REGISTER {username} {password}";
        await SendRequest(request);
        return await ReceiveResponseAsync();
    }

    public async Task<string> Login(string username, string password)
    {
        string request = $"LOGIN {username} {password}";
        await SendRequest(request);
        return await ReceiveResponseAsync();
    }

    public async Task<string> FindMatch()
    {
        string request = "FIND_MATCH";
        await SendRequest(request);
        return await ReceiveResponseAsync();
    }
    public async Task<string> LogOutAsync()
    {
        string request = "LOGOUT";
        await SendRequest(request);
        return await ReceiveResponseAsync();
    }

    public async Task<string> CreateRoom(string roomId)
    {
        string request = $"CREATE_ROOM {roomId}";
        await SendRequest(request);
        return await ReceiveResponseAsync();
    }

    public async Task<string> JoinRoom(string roomId)
    {
        string request = $"JOIN_ROOM {roomId}";
        await SendRequest(request);
        return await ReceiveResponseAsync();
    }

    public async void SendMove(string from, string to)
    {
        string request = $"MOVE {from} {to}";
        await SendRequest(request);
    }
    public async void SendChatMessage(string message)
    {
        string request = $"CHAT {message}";
        await SendRequest(request);
    }
    public async void HandleExitRoom()
    {
        string request = $"EXIT_ROOM";
        await SendRequest(request);
    }
    public async void HandleGameOver()
    {
        string request = $"GAME_OVER";
        await SendRequest(request);
    }
    public async Task<string> HandleExitRoom2()
    {
        string request = $"EXIT_ROOM_2";
        await SendRequest(request);
        return await ReceiveResponseAsync();
    }
    public async Task<string> HandleRestart()
    {
        string request = $"RESTART";
        await SendRequest(request);
        return await ReceiveResponseAsync();
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

    public async Task SendRequest(string request)
    {
        try
        {
            if (tcpClient == null || !tcpClient.Connected)
            {
                Debug.LogWarning("Not connected to server. Attempting to reconnect...");
                await ConnectToServer();

                if (tcpClient == null || !tcpClient.Connected)
                {
                    Debug.LogError("Failed to connect to server. Cannot send request.");
                    return;
                }
            }
            
            byte[] data = Encoding.UTF8.GetBytes(EncryptData(request, "3x@mpl3$tr0ngK#y!") + "\n");
            await stream.WriteAsync(data, 0, data.Length);

            Debug.Log($"Sent: {request}");
            //StartCoroutine(ReceiveResponse());
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error sending request: {ex.Message}");
        }
    }

    public async Task<string> ReceiveResponseAsync()
    {
        StringBuilder response = new StringBuilder();
        int bytesRead;

        while (true)
        {
            try
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    response.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    if (response.ToString().Contains("\n"))
                    {
                        break;
                    }
                }
                else
                {
                    Debug.LogWarning("Server closed the connection.");
                    return "ERROR: Server closed connection.";
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("Receive operation was canceled.");
                return "CANCELED: Operation was canceled.";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error receiving response: {ex.Message}");
                return $"ERROR: {ex.Message}";
            }
        }
        Debug.Log(response.ToString());
        return DecryptData(response.ToString().Trim(), "3x@mpl3$tr0ngK#y!");
    }
    public void CancelReceive()
    {
        if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
        {
            cancellationTokenSource.Cancel();
            Debug.Log("Receive operation canceled.");
        }
    }

    public void StartReceive()
    {
        cancellationTokenSource = new CancellationTokenSource();
        Debug.Log("Starting receive operation...");
    }
    public void OnApplicationQuit()
    {
        try
        {
            if (tcpClient != null)
            {
                stream?.Close();
                tcpClient?.Close();
                Debug.Log("Connection to server closed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during application quit: {ex.Message}");
        }
    }
    public void Disconnect()
    {
           try
        {
            if (tcpClient != null)
            {
                stream?.Close();
                tcpClient?.Close();
                Debug.Log("Connection to server closed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during disconnect: {ex.Message}");
        }
    }
}
