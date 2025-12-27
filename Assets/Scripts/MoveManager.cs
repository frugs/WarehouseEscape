using System;
using UnityEngine;

[System.Serializable]
public class MoveManager {
    /// <summary>Applies ANY Sokoban move to grid state (PlayerController + Solver shared!)</summary>
    public static SokobanState ApplyMove(SokobanState state, SokobanMove move) {
        Cell[,] newGrid = CloneGrid(state.grid);
        
        switch(move.type) {
            case MoveType.PlayerMove:
                ApplyPlayerMove(newGrid, state.playerPos, move.playerTo);
                break;
            case MoveType.CratePush:
                ApplyCratePush(newGrid, state.playerPos, move);
                break;
            default:
                throw new ArgumentException($"Unknown move type: {move.type}");
        }
        
        return new SokobanState(newGrid, move.playerTo);
    }
    
    private static void ApplyPlayerMove(Cell[,] grid, Vector2Int fromPos, Vector2Int toPos) {
        grid[fromPos.x, fromPos.y].occupant = Occupant.Empty;
        grid[toPos.x, toPos.y].occupant = Occupant.Player;
    }
    
    private static void ApplyCratePush(Cell[,] grid, Vector2Int playerPos, SokobanMove move) {
        // 1. Clear player origin
        grid[playerPos.x, playerPos.y].occupant = Occupant.Empty;
        
        // 2. Player to crate position
        grid[move.playerTo.x, move.playerTo.y].occupant = Occupant.Player;
        
        // 3. Crate movement + hole filling
        Cell targetCell = grid[move.crateTo.x, move.crateTo.y];
        targetCell.FillHole();
        
        grid[move.crateFrom.x, move.crateFrom.y].occupant = Occupant.Empty;
        targetCell.occupant = Occupant.Crate;
    }
    
    // ========== UTILITIES ==========
    
    private static Cell[,] CloneGrid(Cell[,] original) {
        int width = original.GetLength(0);
        int height = original.GetLength(1);
        Cell[,] clone = new Cell[width, height];
        
        for(int x = 0; x < width; x++) {
            for(int y = 0; y < height; y++) {
                clone[x, y] = original[x, y].DeepClone();
            }
        }
        return clone;
    }
}
