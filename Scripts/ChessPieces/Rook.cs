using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UIElements;


public class Rook : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        for (int i = crrY - 1; i >= 0; i--)
        {
            if (board[crrX, i] == null)
            {
                r.Add(new Vector2Int(crrX, i));
            } 
            if (board[crrX, i] != null)
            {
                if (board[crrX, i].team != team)
                {
                    r.Add(new Vector2Int(crrX, i));
                }
                break;
            }
        }
        for (int i = crrY + 1; i < tileCountY; i++)
        {
            if (board[crrX, i] == null)
            {
                r.Add(new Vector2Int(crrX, i));
            }
            if (board[crrX, i] != null)
            {
                if (board[crrX, i].team != team)
                {
                    r.Add(new Vector2Int(crrX, i));
                }
                break;
            }
        }
        for (int i = crrX - 1; i >= 0; i--)
        {
            if (board[i, crrY] == null)
            {
                r.Add(new Vector2Int(i, crrY));
            }
            if (board[i, crrY] != null)
            {
                if (board[i, crrY].team != team)
                {
                    r.Add(new Vector2Int(i, crrY));
                }
                break;
            }
        }
        for (int i = crrX + 1; i < tileCountX; i++)
        {
            if (board[i, crrY] == null)
            {
                r.Add(new Vector2Int(i, crrY));
            }
            if (board[i, crrY] != null)
            {
                if (board[i, crrY].team != team)
                {
                    r.Add(new Vector2Int(i, crrY));
                }
                break;
            }
        }
        return r;
    }
}
