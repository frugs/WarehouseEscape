using System;
using System.Linq;

public class MoveManager {
  /// <summary>Applies ANY Sokoban move to grid state (PlayerController + Solver shared!)</summary>
  public static SokobanState ApplyMove(SokobanState state, SokobanMove move) {
    switch (move.type) {
      case MoveType.PlayerMove:
        return ApplyPlayerMove(state, move);
      case MoveType.CratePush:
        return ApplyCratePush(state, move);
      default:
        throw new ArgumentException($"Unknown move type: {move.type}");
    }
  }

  private static SokobanState ApplyPlayerMove(SokobanState state, SokobanMove move) {
    return new SokobanState(
              state.TerrainGrid,
              move.playerTo,
              state.CratePositions,
              state.FilledHoles);
  }

  private static SokobanState ApplyCratePush(SokobanState state, SokobanMove move) {
    var crates = state.CratePositions.ToList();
    var filledHoles = state.FilledHoles.ToList();

    crates.Remove(move.crateFrom);

    var terrain = state.TerrainGrid[move.crateTo.x, move.crateTo.y];
    if (terrain.IsHole() && !state.IsFilledHoleAt(move.crateTo.x, move.crateTo.y)) {
      filledHoles.Add(move.crateTo);
    } else {
      crates.Add(move.crateTo);
    }

    return new SokobanState(state.TerrainGrid, move.playerTo, crates, filledHoles);
  }
}
