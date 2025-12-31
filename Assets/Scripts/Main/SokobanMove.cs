using System;
using UnityEngine;

/// <summary>
/// Single atomic Sokoban action (Player move or Crate push).
/// Pure data - used by PlayerController, Solver, Undo/Redo.
/// </summary>
[Serializable]
public struct SokobanMove {
  public Vector2Int playerFrom;
  public Vector2Int playerTo;
  public Vector2Int crateFrom;
  public Vector2Int crateTo;
  public Vector2Int direction;
  public MoveType type;

  public override string ToString() {
    if (type == MoveType.PlayerMove) {
      return $"[Move] Player {playerFrom} -> {playerTo}";
    }

    // CratePush
    return $"[Push] Player {playerFrom}->{playerTo} " +
           $"pushed Crate {crateFrom}->{crateTo} (Dir: {direction})";
  }

  /// <summary>Player-only move (no crate interaction)</summary>
  public static SokobanMove PlayerMove(Vector2Int from, Vector2Int to) {
    return new SokobanMove { playerFrom = from, playerTo = to, type = MoveType.PlayerMove };
  }

  /// <summary>Crate push move (player + crate movement)</summary>
  public static SokobanMove CratePush(
    Vector2Int playerFrom,
    Vector2Int playerTo,
    Vector2Int crateFrom,
    Vector2Int crateTo) {
    return new SokobanMove {
      playerFrom = playerFrom,
      playerTo = playerTo,
      crateFrom = crateFrom,
      crateTo = crateTo,
      direction = crateTo - crateFrom,
      type = MoveType.CratePush
    };
  }
}

public enum MoveType {
  PlayerMove, // Player steps to empty tile
  CratePush // Player pushes crate to empty tile/hole
}
