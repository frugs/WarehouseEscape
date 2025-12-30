using System;

/// <summary>
/// Pure Sokoban cell logic - NO Unity GameObject dependencies.
/// Represents one grid position with terrain + occupant state.
/// </summary>
[Serializable]
public class Cell {
  public TerrainType terrain = TerrainType.Floor;
  public Occupant occupant = Occupant.Empty;
  public bool isTarget;
  public int x, y;

  // ========== MOVEMENT VALIDATION ==========

  /// <summary>Can player step on this cell?</summary>
  public bool PlayerCanWalk =>
    terrain == TerrainType.Floor || terrain == TerrainType.FilledHole;

  /// <summary>Can a crate be pushed here? (Floor or fillable Hole)</summary>
  public bool CanReceiveCrate =>
    (terrain == TerrainType.Floor ||
     terrain == TerrainType.Hole ||
     terrain == TerrainType.FilledHole) &&
    occupant == Occupant.Empty;

  /// <summary>Pathfinding: empty cell player can reach</summary>
  public bool IsPathableForPlayer => PlayerCanWalk && occupant == Occupant.Empty;

  /// <summary>Valid push target (empty Floor/Hole)</summary>
  public bool IsPushTarget => CanReceiveCrate;

  // ========== HOLE FILLING ==========

  /// <summary>Fills Hole → Floor when crate lands here</summary>
  public void FillHole() {
    if (terrain == TerrainType.Hole) {
      terrain = TerrainType.FilledHole;
    }
  }

  // ========== DEBUGGING ==========

  public override string ToString() {
    return $"[{x},{y}] {terrain}({occupant}){(isTarget ? "T" : "")}";
  }

  public Cell DeepClone() {
    return new Cell {
      x = this.x,
      y = this.y,
      terrain = this.terrain,
      occupant = this.occupant,
      isTarget = this.isTarget
    };
  }
}

/// <summary>Pure terrain types - no visual dependencies</summary>
[Serializable]
public enum TerrainType {
  Floor, // Always passable
  Wall,
  Hole, // Impassable until filled by crate
  FilledHole,
}

/// <summary>Pure occupant types - no Unity GameObjects</summary>
[Serializable]
public enum Occupant {
  Empty, // No object
  Player, // Player position
  Crate, // Pushable box
}
