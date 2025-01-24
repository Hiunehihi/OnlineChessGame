using UnityEngine;
using System.Collections.Generic;
using NUnit.Framework.Interfaces;


public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        int direction = (team == 0) ? 1 : -1;
        if (crrY + direction >= 0 && crrY + direction < tileCountY)
        {
            if (board[crrX, crrY + direction] == null)
            {
                r.Add(new Vector2Int(crrX, crrY + direction));
                if ((team == 0 && crrY == 1 || team == 1 && crrY == 6) &&
                    board[crrX, crrY + (direction * 2)] == null)
                {
                    r.Add(new Vector2Int(crrX, crrY + (direction * 2)));
                }
            }
        }
        if (crrX + 1 < tileCountX && crrY + direction >= 0 && crrY + direction < tileCountY)
        {
            if (board[crrX + 1, crrY + direction] != null && board[crrX + 1, crrY + direction].team != team)
            {
                r.Add(new Vector2Int(crrX + 1, crrY + direction));
            }
        }
        if (crrX - 1 >= 0 && crrY + direction >= 0 && crrY + direction < tileCountY)
        {
            if (board[crrX - 1, crrY + direction] != null && board[crrX - 1, crrY + direction].team != team)
            {
                r.Add(new Vector2Int(crrX - 1, crrY + direction));
            }
        }

        return r;
    }
 
    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        int direction = (team == 0) ? 1 : -1;
        if ((team == 0 && crrY == 6) || (team == 1 && crrY == 1))
        {
            return SpecialMove.Promotion;
        }
        if (moveList.Count > 0)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            if (lastMove[1].y >= 0 && lastMove[1].y < board.GetLength(1)) 
            {
                if (board[lastMove[1].x, lastMove[1].y].type == ChessPieceType.Pawn &&
                    Mathf.Abs(lastMove[0].y - lastMove[1].y) == 2 &&
                    board[lastMove[1].x, lastMove[1].y].team != team)
                {
                    if (lastMove[1].y == crrY)
                    {
                        if (lastMove[1].x == crrX - 1 && crrX - 1 >= 0)
                        {
                            availableMoves.Add(new Vector2Int(crrX - 1, crrY + direction));
                            return SpecialMove.EnPassant;
                        }
                        if (lastMove[1].x == crrX + 1 && crrX + 1 < board.GetLength(0))
                        {
                            availableMoves.Add(new Vector2Int(crrX + 1, crrY + direction));
                            return SpecialMove.EnPassant;
                        }
                    }
                }
            }
        }

        return SpecialMove.None;
    }
}
