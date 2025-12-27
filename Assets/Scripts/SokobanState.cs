using System;
using UnityEngine;

/// <summary>
/// Immutable snapshot of complete Sokoban game state.
/// Used by Solver, Undo/Redo, Network sync, Level serialization.
/// </summary>
[System.Serializable]
public class SokobanState {
    public Cell[,] grid;
    public Vector2Int playerPos;
    
    public SokobanState(Cell[,] grid, Vector2Int playerPos) {
        this.grid = grid;
        this.playerPos = playerPos;
    }
    
    /// <summary>Hash for visited state detection (BFS)</summary>
    public string StateHash() {
        return $"{playerPos.x},{playerPos.y}|" +
               string.Join(",", FlattenOccupants(grid));
    }
    
    private static string[] FlattenOccupants(Cell[,] grid) {
        var occupants = new System.Collections.Generic.List<string>();
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        
        for(int x = 0; x < width; x++) {
            for(int y = 0; y < height; y++) {
                occupants.Add($"{x},{y}:{grid[x,y].occupant}");
            }
        }
        return occupants.ToArray();
    }
}
