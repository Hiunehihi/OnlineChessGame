using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.UI;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class Chessboard : MonoBehaviour
{
    [Header("Art stuff")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private Material highlightMaterial; // New variable for highlight material
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private float dragOffset = 0.75f;
    [SerializeField] private GameObject victoryScreen;
    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;
    [SerializeField] private GameObject[] cameraAngles;
    [SerializeField] private TMP_Text notify;
    [SerializeField] public GameObject chatPanel;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Transform chatContent;
    [SerializeField] private GameObject chatMessagePrefab;
    [SerializeField] private ScrollRect ScrollRect;
    [SerializeField] private TMP_Text player1TimerText;
    [SerializeField] private TMP_Text player2TimerText;

    private ChessPiece[,] chessPieces;
    private Dictionary<GameObject, Vector2Int> tileIndexLookup = new Dictionary<GameObject, Vector2Int>();
    private ChessPiece crrlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private Color originalColor = Color.white;
    private bool isWhiteTurn;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();
    private SpecialMove specialMove;
    private byte[] buffer = new byte[1024];
    private static SynchronizationContext unityContext;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private bool gameOver = false;
    private float player1TimeRemaining = 300f;
    private float player2TimeRemaining = 300f;
    public int crrTeam;
    private void Awake()
    {
        isWhiteTurn = true;
        GenerateAllTiles(tileSize, 8, 8);
        SpawnAllPieces();
        PositionAllPieces();
        unityContext = SynchronizationContext.Current;
        chatPanel.SetActive(false);

    }
    private void UpdateTimerUI()
    {
        player1TimerText.text = FormatTime(player1TimeRemaining);
        player2TimerText.text = FormatTime(player2TimeRemaining);
    }
    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    private void Update()
    {
        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }
        if (crrTeam == 0 && !gameOver)
        {
            player1TimeRemaining -= Time.deltaTime;
            if (player1TimeRemaining <= 0)
            {
                PlayerTimeout(0);
            }
        }
        else if (crrTeam == 1 && !gameOver)
        {
            player2TimeRemaining -= Time.deltaTime;
            if (player2TimeRemaining <= 0)
            {
                PlayerTimeout(1);
            }
        }

        UpdateTimerUI();
        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].GetComponent<Renderer>().material = highlightMaterial;
            }

            if (currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].GetComponent<Renderer>().material = tileMaterial;
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].GetComponent<Renderer>().material = highlightMaterial;
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (chessPieces[hitPosition.x, hitPosition.y] != null)
                {
                    if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && GameUI.Instance.currentTeam == 0) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && GameUI.Instance.currentTeam == 1))
                    {

                        crrlyDragging = chessPieces[hitPosition.x, hitPosition.y];
                        availableMoves = crrlyDragging.GetAvailableMoves(ref chessPieces, 8, 8);
                        specialMove = crrlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);
                        PreventCheck();
                        HighlightTiles();
                    }
                    else
                    {
                        Debug.Log("Not your turn!");
                    }
                }
            }

            if (crrlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(crrlyDragging.crrX, crrlyDragging.crrY);
                bool validMove = MoveTo(crrlyDragging, hitPosition.x, hitPosition.y);
                if (!validMove)
                {
                    crrlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                }
                crrlyDragging = null;
                RemoveHighlightTiles();
            }
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].GetComponent<Renderer>().material = tileMaterial;
                currentHover = -Vector2Int.one;
            }
            if (crrlyDragging && Input.GetMouseButtonUp(0))
            {
                crrlyDragging.SetPosition(GetTileCenter(crrlyDragging.crrX, crrlyDragging.crrY));
                crrlyDragging = null;
                RemoveHighlightTiles();
            }
        }

        if (crrlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))

            {
                crrlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
            }
        }
    }

    private void PlayerTimeout(int player)
    {
        gameOver = true;

        if (player == 0)
        {
            Debug.Log("Player 1 has run out of time. Player 2 wins!");
            CheckMate(1);
        }
        else
        {
            CheckMate(0);
            Debug.Log("Player 2 has run out of time. Player 1 wins!");
        }

        GameUI.Instance.ResetUI();
    }
    // Generate the board
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {
        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int i = 0; i < tileCountX; i++)
            for (int j = 0; j < tileCountY; j++)
            {
                tiles[i, j] = GenerateSingleTile(tileSize, i, j);
            }
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tile = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tile.transform.parent = transform;
        Mesh mesh = new Mesh();
        tile.AddComponent<MeshFilter>().mesh = mesh;
        tile.AddComponent<MeshRenderer>().material = tileMaterial;
        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        int[] tris = new int[] { 0, 1, 2, 1, 3, 2 };
        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        tile.layer = LayerMask.NameToLayer("Tile");
        tile.AddComponent<BoxCollider>();
        tileIndexLookup.Add(tile, new Vector2Int(x, y));
        return tile;
    }
    // Spawning of the pieces
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[8, 8];

        int whiteTeam = 0, blackTeam = 1;
        //white team
        chessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        for (int i = 0; i < 8; i++)
        {
            chessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);
        }
        // black team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        for (int i = 0; i < 8; i++)
        {
            chessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);
        }
    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();
        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];
        return cp;
    }

    // Positioning
    private void PositionAllPieces()
    {
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (chessPieces[i, j] != null) PositionSinglePieces(i, j, true);
            }
        }
    }
    private void PositionSinglePieces(int x, int y, bool force = false)
    {
        chessPieces[x, y].crrX = x;
        chessPieces[x, y].crrY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }
    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }
    private void HighlightTiles()
    {
        foreach (var move in availableMoves)
        {
            if (move.x >= 0 && move.x < 8 && move.y >= 0 && move.y < 8 && tiles[move.x, move.y] != null)
            {
                var renderer = tiles[move.x, move.y].GetComponent<Renderer>();
                originalColor = renderer.material.color; // Lưu lại màu gốc
                renderer.material.color = Color.green; // Đổi màu thành xanh lá
                Debug.Log("Setting tile at position " + move + " to green color.");
            }
        }
    }

    private void RemoveHighlightTiles()
    {
        foreach (var move in availableMoves)
        {
            if (move.x >= 0 && move.x < 8 && move.y >= 0 && move.y < 8 && tiles[move.x, move.y] != null)
            {
                var renderer = tiles[move.x, move.y].GetComponent<Renderer>();
                renderer.material.color = originalColor; // Đổi lại màu gốc
                Debug.Log("Setting tile at position " + move + " back to original color.");
            }
        }
        availableMoves.Clear();
    }
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos)
    {
        for (int i = 0; i < moves.Count; i++)
        {
            if (moves[i].x == pos.x && moves[i].y == pos.y)
            {
                return true;
            }
        }
        return false;
    }
    //Checkmate
    private void CheckMate(int team)
    {
        unityContext.Post(_ =>
        {
            DisplayVictory(team);
           // StopReceiving();
           // GameUI.Instance.client.HandleGameOver();
            gameOver = true;
        }, null
        );
    }
    private void DisplayVictory(int winTeam)
    {
        chatPanel.SetActive(false);
        ClearContent();
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winTeam).gameObject.SetActive(true);
    }
    public async void OnResetButton()
    {
        StopReceiving();
        GameUI.Instance.client.HandleGameOver();
        try
        {
            notify.text = "";
            string response = await GameUI.Instance.client.HandleRestart();
            notify.text = response;
            if (response.StartsWith("SUCCESS"))
            {
                notify.text = response;
                Debug.Log($"Restart successful: {response}");
                ResetBoard();
                StartReceivingMoves();
                chatPanel.SetActive(true);
            }
            else if (response.StartsWith("WAITING"))
            {
                notify.text = response;
                Debug.Log($"Server response: {response}");

                string responses = await GameUI.Instance.client.ReceiveResponseAsync();

                if (responses.StartsWith("FAILED"))
                {
                    notify.text = responses;
                    Debug.LogError($"Restart failed: {responses}");
                    victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
                    victoryScreen.transform.GetChild(1).gameObject.SetActive(false);

                    victoryScreen.SetActive(false);
                    GameUI.Instance.menuAnimator.SetTrigger("FindPlayers");
                    GameUI.Instance.SetCamera(CameraAngle.menu);
                }
                else if (responses.StartsWith("SUCCESS"))
                {
                    notify.text = responses;
                    Debug.Log($"Restart successful: {responses}");
                    ResetBoard();
                    StartReceivingMoves();
                    chatPanel.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"Unexpected response: {response}");
                }
            }
            else
            {
                Debug.LogWarning($"Initial response not 'WAITING': {response}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error while handling restart: {ex.Message}");
        }
    }
    public void ClearContent()
    {
        if (chatContent.childCount > 0)
        {
            foreach (Transform child in chatContent)
            {
                Destroy(child.gameObject);
            }
        }
    }
    public void OnExitGameButton()
    {
        GameUI.Instance.client.HandleExitRoom();
        StopReceiving();
        GameUI.Instance.menuAnimator.SetTrigger("FindPlayers");
        GameUI.Instance.SetCamera(0);
        gameOver = true;
        ClearContent();
        chatPanel.SetActive(false);
        GameUI.Instance.ResetUI();
    }
    public void ResetBoard()
    {
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(4).gameObject.SetActive(false);

        victoryScreen.SetActive(false);
        // Field reset
        crrlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();

        //Clean up
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
            {
                if (chessPieces[i, j] != null)
                {
                    Destroy(chessPieces[i, j].gameObject);
                    chessPieces[i, j] = null;
                }
            }
        for (int i = 0; i < deadWhites.Count; i++)
        {
            Destroy(deadWhites[i].gameObject);
        }
        for (int i = 0; i < deadBlacks.Count; i++)
        {
            Destroy(deadBlacks[i].gameObject);
        }
        deadWhites.Clear();
        deadBlacks.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        player1TimeRemaining = 300f;
        player2TimeRemaining = 300f;
        gameOver = false;
        isWhiteTurn = true;
        ResetReceiving();
    }
    public async void OnExitButton()
    {
        StopReceiving();
        GameUI.Instance.client.HandleGameOver();
        string response = await GameUI.Instance.client.HandleExitRoom2();
        GameUI.Instance.menuAnimator.SetTrigger("FindPlayers");
        GameUI.Instance.ResetUI();
        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);
        //Debug.Log(response);
    }

    public void OnSendButton()
    {
        string message = chatInputField.text;
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        chatInputField.text = "";
        Canvas.ForceUpdateCanvases();
        ScrollRect.verticalNormalizedPosition = 0f;
        GameObject gameObject = Instantiate(chatMessagePrefab, chatContent);
        TMP_Text texts = gameObject.GetComponentInChildren<TMP_Text>();
        texts.color = Color.black;
        texts.text = "Bạn: " + message;
        GameUI.Instance.client.SendChatMessage(message);
    }
    //Special Moves
    private void ProcessSpecialMove()
    {
        if (specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count - 1];
            ChessPiece myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if (myPawn.crrX == enemyPawn.crrX)
            {
                if (myPawn.crrY == enemyPawn.crrY - 1 || myPawn.crrY == enemyPawn.crrY + 1)
                {
                    if (enemyPawn.team == 0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadBlacks.Count);
                    }
                    chessPieces[enemyPawn.crrX, enemyPawn.crrY] = null;
                }
            }
        }


        if (specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            if (lastMove[1].x == 2)
            {
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;
                    PositionSinglePieces(3, 0);
                    chessPieces[0, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionSinglePieces(3, 7);
                    chessPieces[0, 7] = null;
                }
            }
            else if (lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0)
                {
                    ChessPiece rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionSinglePieces(5, 0);
                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7)
                {
                    ChessPiece rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionSinglePieces(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (chessPieces[i, j] != null)
                {
                    if (chessPieces[i, j].type == ChessPieceType.King)
                    {
                        if (chessPieces[i, j].team == crrlyDragging.team)
                        {
                            targetKing = chessPieces[i, j];
                        }
                    }
                }
            }
        }
        //since we're sending ref availableMoves, we will be deleting moves that are putting us in check
        SimulateMoveForSinglePiece(crrlyDragging, ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        // Save the current values, to reset after the function call
        int actualX = cp.crrX;
        int actualY = cp.crrY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();
        // Going through all the moves, simulate them and check if we're in check
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;
            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.crrX, targetKing.crrY);
            if (cp.type == ChessPieceType.King)
            {
                kingPositionThisSim = new Vector2Int(simX, simY);
            }
            ChessPiece[,] simulation = new ChessPiece[8, 8];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    if (chessPieces[x, y] != null)
                    {
                        simulation[x, y] = chessPieces[x, y];
                        if (simulation[x, y].team != cp.team)
                        {
                            simAttackingPieces.Add(simulation[x, y]);
                        }
                    }
                }
            }
            simulation[actualX, actualY] = null;
            cp.crrX = simX;
            cp.crrY = simY;
            simulation[simX, simY] = cp;

            var deadPiece = simAttackingPieces.Find(c => c.crrX == simX && c.crrY == simY);
            if (deadPiece != null)
            {
                simAttackingPieces.Remove(deadPiece);
            }

            List<Vector2Int> simMoves = new List<Vector2Int>();
            for (int a = 0; a < simAttackingPieces.Count; a++)
            {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, 8, 8);
                for (int b = 0; b < pieceMoves.Count; b++)
                {
                    simMoves.Add(pieceMoves[b]);
                }
            }

            if (ContainsValidMove(ref simMoves, kingPositionThisSim))
            {
                movesToRemove.Add(moves[i]);
            }

            cp.crrX = actualX;
            cp.crrY = actualY;
        }
        for (int i = 0; i < movesToRemove.Count; i++)
        {
            moves.Remove(movesToRemove[i]);
        }
    }
    private bool CheckForCheckMate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (chessPieces[i, j] != null)
                {
                    if (chessPieces[i, j].team == targetTeam)
                    {
                        defendingPieces.Add(chessPieces[i, j]);
                        if (chessPieces[i, j].type == ChessPieceType.King)
                        {
                            targetKing = chessPieces[i, j];
                        }
                    }
                    else
                    {
                        attackingPieces.Add(chessPieces[i, j]);
                    }
                }
            }
        }
        List<Vector2Int> crrAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, 8, 8);
            for (int b = 0; b < pieceMoves.Count; b++)
            {
                crrAvailableMoves.Add(pieceMoves[b]);
            }
        }
        if (ContainsValidMove(ref crrAvailableMoves, new Vector2Int(targetKing.crrX, targetKing.crrY)))
        {
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, 8, 8);
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);
                if (defendingMoves.Count != 0)
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }
    // Operation
    private bool MoveTo(ChessPiece cp, int x, int y)
    {
        if (gameOver)
        {
            Debug.LogWarning("Game is over. No moves can be made.");
            return false;
        }
        if (!ContainsValidMove(ref availableMoves, new Vector2Int(x, y)))
        {
            return false;
        }
        Vector2Int previousPosition = new Vector2Int(cp.crrX, cp.crrY);
        if (chessPieces[x, y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];

            if (cp.team == ocp.team)
            {
                return false;
            }
            // if its the enemy team
            if (ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);
                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);
                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlacks.Count);
            }
        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;
        PositionSinglePieces(x, y);
        isWhiteTurn = !isWhiteTurn;
        crrTeam = isWhiteTurn ? 0 : 1;
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });
        ProcessSpecialMove();

        if (CheckForCheckMate())
        {
            CheckMate(cp.team);
        }
        SendMove(previousPosition, new Vector2Int(x, y));
        return true;
    }
    private bool MoveTo1(ChessPiece cp, int x, int y)
    {
        Vector2Int previousPosition = new Vector2Int(cp.crrX, cp.crrY);
        if (chessPieces[x, y] != null)
        {
            ChessPiece ocp = chessPieces[x, y];

            if (cp.team == ocp.team)
            {
                return false;
            }
            // if its the enemy team
            if (ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);
                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
            }
            else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);
                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlacks.Count);
            }
        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;
        PositionSinglePieces(x, y);
        isWhiteTurn = !isWhiteTurn;
        crrTeam = isWhiteTurn ? 0 : 1;
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });
        ProcessSpecialMove();

        if (CheckForCheckMate())
        {
            CheckMate(cp.team);
        }
        return true;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        if (hitInfo == null) return -Vector2Int.one;
        if (tileIndexLookup.ContainsKey(hitInfo))
        {
            return tileIndexLookup[hitInfo];
        }
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                if (tiles[i, j] == hitInfo)
                {
                    return new Vector2Int(i, j);
                }
            }
        }
        return -Vector2Int.one; //Invaild
    }
    private void SendMove(Vector2Int from, Vector2Int to)
    {
        if (gameOver)
        {
            Debug.LogWarning("Game is over. No moves can be sent.");
            return;
        }
        string fromStr = $"{from.x},{from.y}";
        string toStr = $"{to.x},{to.y}";

        if (GameUI.Instance.client != null && GameUI.Instance.client.isConnected())
        {
            GameUI.Instance.client.SendMove(fromStr, toStr);
            Debug.Log($"Server response after sending move");
        }
        else
        {
            Debug.LogError("Client is not connected to the server.");
        }
    }
    public void StartReceivingMoves()
    {
        Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                ReceiveResponse();
            }
            Debug.Log("Receive task exited safely.");
        }, cts.Token);
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

    private void ReceiveResponse()
    {
        try
        {
            int bytesRead = GameUI.Instance.client.stream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                Debug.Log($"Received: {response}");
                ListenForOpponentMove(DecryptData(response, "3x@mpl3$tr0ngK#y!"));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error receiving response: {ex.Message}");
        }
    }
    private void DisplayOpponentLeftMessage()
    {
        chatPanel.SetActive(false);
        ClearContent();
        GameUI.Instance.errorPanel.SetActive(true);
        GameUI.Instance.errorMessage.text = "Đối thủ đã thoát. Trận đấu kết thúc.";
        GameUI.Instance.errorPanel.transform.GetChild(1).gameObject.SetActive(true);
        Debug.Log("Displayed opponent left message.");
    }
    public void ListenForOpponentMove(string response)
    {
        if (response.StartsWith("MOVE"))
        {
            string[] parts = response.Split(' ');
            string[] fromParts = parts[1].Split(',');
            string[] toParts = parts[2].Split(',');
            Vector2Int from = new Vector2Int(int.Parse(fromParts[0]), int.Parse(fromParts[1]));
            Vector2Int to = new Vector2Int(int.Parse(toParts[0]), int.Parse(toParts[1]));
            ChessPiece piece = chessPieces[from.x, from.y];
            bool x = MoveTo1(piece, to.x, to.y);
            Debug.Log(x);
        }
        if (response.StartsWith("GAMEOVER"))
        {
            string[] parts = response.Split(' ');
            string message = parts[1];
            Debug.Log($"Game over: {message}");
            gameOver = true;
            StopReceiving();
        }

        if (response.StartsWith("NOTIFY"))
        {
            Debug.Log("Opponent has left the game. Ending the match...");
            unityContext.Post(_ =>
            {
                StopReceiving();
                GameUI.Instance.client.HandleGameOver();
                DisplayOpponentLeftMessage();
                GameUI.Instance.ResetUI();
            }, null);
        }
        if (response.StartsWith("CHAT"))
        {
            string messages = response.Substring(5);
            Debug.Log($"Received chat message: {messages}");
            unityContext.Post(_ =>
            {
                Debug.Log(response);
                chatInputField.text = "";
                string messages = response.Substring(5);
                Canvas.ForceUpdateCanvases();
                ScrollRect.verticalNormalizedPosition = 0f;
                GameObject chassmessage = Instantiate(chatMessagePrefab, chatContent);
                TMP_Text text = chassmessage.GetComponentInChildren<TMP_Text>();
                text.text = "Đối thủ: " + messages;
                text.color = Color.black;
            }, null);
        }
    }
    public void StopReceiving()
    {
        if (cts != null && !cts.Token.IsCancellationRequested)
        {
            cts.Cancel();
            Debug.Log("Stopping receiving moves...");
        }
    }
    public void ResetReceiving()
    {
        cts = new CancellationTokenSource();
        Debug.Log("Receiving system reset for new game.");
    }
}



