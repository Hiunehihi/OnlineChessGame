using System.Collections.Generic;
using UnityEngine;

public class Bishop : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        int[] dx = { -1, 1, -1, 1 };
        int[] dy = { -1, -1, 1, 1 };

        for (int i = 0; i < 4; i++)
        {
            int newX = crrX + dx[i];
            int newY = crrY + dy[i];

            // Continue moving in the same direction until hitting an obstacle or the edge of the board
            while (newX >= 0 && newX < tileCountX && newY >= 0 && newY < tileCountY)
            {
                if (board[newX, newY] == null)
                {
                    r.Add(new Vector2Int(newX, newY));
                }
                else
                {
                    // If there's a piece on the target tile...
                    if (board[newX, newY].team != team)
                    {
                        // If it's an opponent's piece, add the move and stop checking in this direction
                        r.Add(new Vector2Int(newX, newY));
                    }
                    break; // Stop checking in this direction
                }

                newX += dx[i];
                newY += dy[i];
            }
        }

        return r;
    }
}
