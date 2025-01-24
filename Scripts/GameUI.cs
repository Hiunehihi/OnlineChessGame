using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Collections;


public enum CameraAngle
{
    menu = 0,
    whiteTeam = 1,
    blackTeam = 2,
    WhiteteamAI = 3
}
public class GameUI : MonoBehaviour
{
    public TCPClient client;
    [SerializeField] private TMP_InputField usernameLoginInput;
    [SerializeField] private TMP_InputField passwordLoginInput;
    [SerializeField] private TMP_InputField usernameRegisterInput;
    [SerializeField] private TMP_InputField passwordRegisterInput;
    [SerializeField] private TMP_InputField rePasswordRegisterInput;
    [SerializeField] public TMP_InputField roomCodeInput;
    [SerializeField] private GameObject[] cameraAngles;
    [SerializeField] public GameObject errorPanel;
    [SerializeField] public TextMeshProUGUI errorMessage;
    [SerializeField] private GameObject chessBoard;
    [SerializeField] private GameObject chessAI;
    [SerializeField] private GameObject[] pieces;
    [SerializeField] private TextMeshPro text;


    public string response = "";
    public int currentTeam = -1;
    public string roomCode = "";
    private string currentPanel = "StartMenu";
    public static GameUI Instance { set; get; }
    private void Start()
    {
        errorMessage.text = "Welcome to Unity!";
    }
    private void Awake()
    {
        if (client == null)
        {
            client = Object.FindAnyObjectByType<TCPClient>();
            if (client == null)
            {
                Debug.LogError("No TCPClient instance found in the scene.");
            }
            else
            {
                Debug.Log("TCPClient instance assigned automatically.");
            }
        }
        usernameLoginInput.text = "";
        passwordLoginInput.text = "";
        usernameRegisterInput.text = "";
        passwordRegisterInput.text = "";
        rePasswordRegisterInput.text = "";
        roomCodeInput.text = "";
        Instance = this;
    }
    [SerializeField] public Animator menuAnimator;
    public void OnLocalGameButton()
    {
        menuAnimator.SetTrigger("HideRoom");
        SetCamera(CameraAngle.WhiteteamAI);
        ChessAI chessAI = FindAnyObjectByType<ChessAI>();
        if (chessAI != null)
        {
            chessAI.ResetBoard();
        }
    }
    public void OnOnlineGameButton()
    {
        menuAnimator.SetTrigger("OnlineGame");
    }
    public async void OnLoginButton()
    {
        await client.ConnectToServer();
        currentPanel = "OnlineGame";
        if (usernameLoginInput == null || passwordLoginInput == null)
        {
            errorMessage.text = "Input fields are not assigned in the Inspector.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Input fields are not assigned in the Inspector.");
            return;
        }

        if (client == null)
        {
            errorMessage.text = "TCPClient instance is not assigned.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("TCPClient instance is not assigned.");
            return;
        }

        string username = usernameLoginInput.text;
        string password = passwordLoginInput.text;

        Debug.Log("Username: " + username);
        Debug.Log("Password: " + password);
        response = await client.Login(username, password);

        if (string.IsNullOrEmpty(response))
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "No response received from server.";
            errorPanel.SetActive(true);
            Debug.LogError("No response received from server.");
            return;
        }

        if (response.StartsWith("SUCCESS"))
        {
            menuAnimator.SetTrigger("FindPlayers");
        }
        else
        {
            errorMessage.text = "Login failed: " + response;
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Login failed: " + response);
        }
    }
    public void OnRegisterButton()
    {
        menuAnimator.SetTrigger("Register");
    }
    public async void OnJoinButton()
    {
        currentPanel = "FindPlayers";
        if (client == null)
        {
            errorMessage.text = "TCPClient instance is not assigned.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("TCPClient instance is not assigned.");
            return;
        }

        if (string.IsNullOrWhiteSpace(roomCodeInput.text))
        {
            errorMessage.text = "Room code is empty or invalid.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Room code is empty or invalid.");
            return;
        }

        string roomCode = roomCodeInput.text;
        Debug.Log("Joining room with code: " + roomCode);
        response = await client.JoinRoom(roomCode);

        if (string.IsNullOrEmpty(response))
        {
            errorMessage.text = "No response from server.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("No response from server.");
            return;
        }

        if (response.StartsWith("SUCCESS"))
        {
            Debug.Log("Joined room successfully. Room code: " + roomCode);
            menuAnimator.SetTrigger("HideRoom");
            currentTeam = 1;
            StartGame();
        }
        else if (response.StartsWith("FAILED"))
        {
            errorMessage.text = "Failed to join room: " + response;
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Failed to join room: " + response);
        }
        else
        {
            errorMessage.text = "Unexpected response: " + response;
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Unexpected response: " + response);
        }
    }
    public async void OnCreateRoomButton()
    {
        currentPanel = "FindPlayers";
        if (client == null)
        {
            errorMessage.text = "TCPClient instance is not assigned.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("TCPClient instance is not assigned.");
            return;
        }

        if (string.IsNullOrWhiteSpace(roomCodeInput.text))
        {
            errorMessage.text = "Room code is empty or invalid.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Room code is empty or invalid.");
            return;
        }

        roomCode = roomCodeInput.text;
        Debug.Log("Creating room with code: " + roomCode);
        response = await client.CreateRoom(roomCode);

        if (string.IsNullOrEmpty(response))
        {
            errorMessage.text = "No response from server.";
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("No response from server.");
            return;
        }
        if (response.StartsWith("JOIN_ROOM"))
        {
            menuAnimator.SetTrigger("HideRoom");
            currentTeam = 0;
            StartGame();
        }
        if (response.StartsWith("ROOM_CREATED"))
        {
            Debug.Log("Room created successfully. Room code: " + roomCode);
            menuAnimator.SetTrigger("WaitingMenu");
            response = await client.ReceiveResponseAsync();
            if (response.StartsWith("JOIN_ROOM"))
            {
                menuAnimator.SetTrigger("HideRoom");
                currentTeam = 0;
                StartGame();
            }
            if (response.StartsWith("EXIT_WAITING"))
            {
                menuAnimator.SetTrigger("FindPlayers");
            }
        }
        else if (response.StartsWith("ERROR"))
        {
            errorMessage.text = "Failed to create room: " + response;
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Failed to create room: " + response);
        }
        else
        {
            errorMessage.text = "Unexpected response: " + response;
            errorPanel.SetActive(true);
            menuAnimator.SetTrigger("HideRoom");
            Debug.LogError("Unexpected response: " + response);
        }
    }
    public async void OnFindPlayerButton()
    {
        currentPanel = "FindPlayers";
        if (client == null)
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "TCPClient instance is not assigned.";
            errorPanel.SetActive(true);
            Debug.LogError("TCPClient instance is not assigned.");
            return;
        }

        Debug.Log("Finding players...");
        response = await client.FindMatch();

        if (string.IsNullOrEmpty(response))
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "No response from server.";
            errorPanel.SetActive(true);
            Debug.LogError("No response from server.");
            return;
        }
        while (response.StartsWith("WAITING"))
        {
            Debug.Log("Waiting for another player...");
            response = await client.ReceiveResponseAsync();  
        }

        if (response.StartsWith("SUCCESS"))
        {
            if (response == "SUCCESS: white")
            {
                Debug.Log(response);
                menuAnimator.SetTrigger("HideRoom");
                currentTeam = 0;
                StartGame();
            }
            else if (response == "SUCCESS: black")
            {
                Debug.Log(response);
                menuAnimator.SetTrigger("HideRoom");
                currentTeam = 1;
                StartGame();
            }
        }
        else if (response.StartsWith("ERROR"))
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = response;
            errorPanel.SetActive(true);
            Debug.LogError($"{response}");
        }
    }
    public async void OnExitWaitingButton()
    {
        await client.SendRequest("EXIT_WAITING");
        
    }    
    public void StartGame()
    {
        if (currentTeam == 1)
        {
            SetCamera(CameraAngle.blackTeam);
        } else if (currentTeam == 0)
        {
            SetCamera(CameraAngle.whiteTeam);
        }
        else
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "Error: Can't join game";
            errorPanel.SetActive(true);
            Debug.Log("Error: Can't join game");
        }
        Chessboard chessboard =  FindAnyObjectByType<Chessboard>();
        
        if (chessboard != null)
        {
            chessboard.chatPanel.SetActive(true);
            chessboard.ResetBoard();
            chessboard.StartReceivingMoves();
        }
    }
    public void SetCamera(CameraAngle angle)
    {
        for (int i = 0; i < cameraAngles.Length; i++)
        {
            cameraAngles[i].SetActive(i == (int)angle);
        }
    }
    public async void OnLogoutButton()
    {
        response = await client.LogOutAsync();
        if (response.StartsWith("SUCCESS"))
        {
            Debug.Log(response);
            usernameLoginInput.text= "";
            passwordLoginInput.text = "";
            client.DisconnectFromServer();
            menuAnimator.SetTrigger("OnlineGame");
        }
        else
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = response;
            errorPanel.SetActive(true);
            Debug.LogError($"{response}");
        }
    }
    public async void OnRegisterNowButton()
    {
        currentPanel = "Register";
        await client.ConnectToServer();
        if (usernameRegisterInput == null || passwordRegisterInput == null || rePasswordRegisterInput == null)
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "Input fields are not assigned in the Inspector.";
            errorPanel.SetActive(true);
            Debug.LogError("Input fields are not assigned in the Inspector.");
            return;
        }

        if (client == null)
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "TCPClient instance is not assigned.";
            errorPanel.SetActive(true);
            Debug.LogError("TCPClient instance is not assigned.");
            return;
        }

        string username = usernameRegisterInput.text;
        string password = passwordRegisterInput.text;
        string confirmPassword = rePasswordRegisterInput.text;
        Debug.Log("Username: " + username);
        Debug.Log("Password: " + password);
        Debug.Log("Repassword: " + confirmPassword);

        // Kiểm tra dữ liệu đầu vào
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "Please fill out all fields.";
            errorPanel.SetActive(true);
            Debug.LogError("Please fill out all fields.");
            return;
        }

        if (password != confirmPassword)
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "Passwords do not match.";
            errorPanel.SetActive(true);
            Debug.LogError("Passwords do not match.");
            return;
        }

        Debug.Log($"Registering user: {username}");
        response = await client.Register(username, password);

        if (string.IsNullOrEmpty(response))
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "No response received from server.";
            errorPanel.SetActive(true);
            Debug.LogError("No response received from server.");
            return;
        }

        if (response.StartsWith("SUCCESS"))
        {
            Debug.Log("Registration successful!");
            menuAnimator.SetTrigger("OnlineGame");
        }
        else if (response.StartsWith("ERROR"))
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "Registration failed: " + response;
            errorPanel.SetActive(true);
            Debug.LogError("Registration failed: " + response);
        }
        else
        {
            menuAnimator.SetTrigger("HideRoom");
            errorMessage.text = "Unexpected response: " + response;
            errorPanel.SetActive(true);
            Debug.LogError("Unexpected response: " + response);
        }
    }
    public void OnBackButton()
    {
        menuAnimator.SetTrigger("StartMenu");
    }
    public void ResetUI()
    {
        if (roomCodeInput != null)
        {
            roomCodeInput.text = "";
        }
        currentTeam = -1;
        SetCamera(CameraAngle.menu);
    }
    public void OnOkButton()
    {
        errorPanel.SetActive(false);
        menuAnimator.SetTrigger(currentPanel);
    }
}
