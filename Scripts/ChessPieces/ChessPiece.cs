using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
public enum ChessPieceType
{
    None = 0,
    Pawn = 1,
    Rook = 2,
    Knight = 3,
    Bishop = 4,
    Queen = 5,
    King = 6
}

public class
 ChessPiece : MonoBehaviour
{
    public int team;
    public int crrX;

    public int crrY;
    public ChessPieceType type;

    private Vector3 desiredPosition;
    private Vector3 desiredScale = Vector3.one * 2;
    private Vector2Int initialPosition; // Store the initial position
    
    private void Start()
    {
        // Store the initial position when the piece is created
        initialPosition = new Vector2Int(crrX, crrY);
      // transform.rotation = Quaternion.Euler((team==0)?Vector3.zero : new Vector3(0,180,0));
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 10);
        transform.localScale = Vector3.Lerp(transform.localScale, desiredScale, Time.deltaTime * 10);
    }
    public string CurrentPosition { get; private set; }
    public void SetPosition(string position)
    {
        CurrentPosition = position;
    }

    public virtual List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        return r;
    }
    public virtual SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        return SpecialMove.None;
    }
    public virtual void SetPosition(Vector3 position, bool force = false)
    {
        desiredPosition = position;
        if (force)

        {
            transform.position = desiredPosition;
        }
    }

    public virtual void SetScale(Vector3 scale, bool force = false)
    {
        desiredScale = scale;
        if (force)
        {
            transform.localScale = desiredScale;

        }
    }

    // Call this method when a piece is moved to a valid position
    public void UpdatePosition(int newX, int newY)
    {
        crrX = newX;
        crrY = newY;
        initialPosition = new Vector2Int(newX, newY); // Update initial position
    }

    public void ResetPosition()
    {
        crrX = initialPosition.x;
        crrY = initialPosition.y;
        desiredPosition = new Vector3(initialPosition.x, 0, initialPosition.y);
    }
}