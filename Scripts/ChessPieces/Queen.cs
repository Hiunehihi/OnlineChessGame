using System.Collections.Generic;
using UnityEngine;

public class Queen : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int>
 r = new List<Vector2Int>();


        // Queen combines the movement of Rook and Bishop

        // Check horizontal and vertical moves (like a Rook)
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                if (i == 0 && j == 0) continue; // Skip the current position

                int newX = crrX + i;
                int newY = crrY + j;

                while (newX >= 0 && newX < tileCountX && newY >= 0 && newY < tileCountY)
                {
                    if (board[newX, newY] == null)
                    {
                        r.Add(new Vector2Int(newX, newY));
                    }
                    else
                    {
                        if (board[newX, newY].team != team)
                        {
                            r.Add(new Vector2Int(newX, newY));
                        }
                        break;
                    }

                    // Move one step further in the same direction
                    newX += i;
                    newY += j;
                }
            }
        }

        return r;
    }
}