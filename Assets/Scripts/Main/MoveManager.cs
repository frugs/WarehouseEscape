using System;
using UnityEngine;

public class MoveManager {
  /// <summary>Applies ANY Sokoban move to grid state (PlayerController + Solver shared!)</summary>
  public static SokobanState ApplyMove(SokobanState state, SokobanMove move) {
    Cell[,] newGrid = CloneGrid(state.grid);

    switch (move.type) {
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
    // Clear old positions
    // We must clear the crate's old position BEFORE placing the player there,
    // otherwise we would overwrite the player with 'Empty' because playerTo == crateFrom.
    grid[playerPos.x, playerPos.y].occupant = Occupant.Empty;
    grid[move.crateFrom.x, move.crateFrom.y].occupant = Occupant.Empty;

    // Move Player (Player enters the tile the crate just left)
    grid[move.playerTo.x, move.playerTo.y].occupant = Occupant.Player;

    // Move Crate + Handle Hole Logic
    Cell targetCell = grid[move.crateTo.x, move.crateTo.y];

    if (targetCell.terrain == TerrainType.Hole) {
      // Crate falls in: Hole becomes FilledHole, Crate disappears (Empty)
      targetCell.FillHole();
      targetCell.occupant = Occupant.Empty;
    } else {
      // Crate moves to floor
      targetCell.occupant = Occupant.Crate;
    }
  }

  // ========== UTILITIES ==========

  private static Cell[,] CloneGrid(Cell[,] original) {
    int width = original.GetLength(0);
    int height = original.GetLength(1);
    Cell[,] clone = new Cell[width, height];

    for (int x = 0; x < width; x++) {
      for (int y = 0; y < height; y++) {
        clone[x, y] = original[x, y].DeepClone();
      }
    }

    return clone;
  }
}
