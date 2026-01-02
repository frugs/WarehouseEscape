using System;
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
        return state.WithPlayerMove(move.playerTo);
      case MoveType.CratePush:
        return state.WithCratePush(move.playerTo, move.crateFrom, move.crateTo);
      default:
        throw new ArgumentException($"Unknown move type: {move.type}");
    }
  }
}
