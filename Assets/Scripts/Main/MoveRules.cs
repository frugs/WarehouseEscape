using System;
using System.Linq;
using UnityEngine;

public static class MoveRules {
  public static bool TryBuildMove(SokobanState state, Vector2Int direction, out SokobanMove move) {
    move = default;

    var from = state.PlayerPos;
    var to = from + direction;

    if (!state.IsValidPos(to.x, to.y)) return false;

    if (state.CanPlayerWalk(to.x, to.y)) {
      move = SokobanMove.PlayerMove(from, to);
      return true;
    }

    if (state.IsCrateAt(to.x, to.y)) {
      var crateTo = to + direction;
      if (state.CanReceiveCrate(crateTo.x, crateTo.y)) {
        move = SokobanMove.CratePush(from, to, to, crateTo);
        return true;
      }
    }

    return false;
  }

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
