using System;

/// <summary>Pure terrain types - no visual dependencies</summary>
[Serializable]
public enum TerrainType
{
    Floor,
    Wall,
    Hole,
    Target,
    Entrance,   // new
    Exit        // new
}

public static class TerrainTypeExtensions {
  /// <summary>Can player step on this cell?</summary>
  public static bool PlayerCanWalk(this TerrainType terrain) =>
    terrain == TerrainType.Floor || terrain == TerrainType.Target;

  /// <summary>Can a crate be pushed here? (Floor or fillable Hole)</summary>
  public static bool CanReceiveCrate(this TerrainType terrain) =>
    terrain == TerrainType.Floor ||
    terrain == TerrainType.Target ||
    terrain == TerrainType.Hole;

  public static bool IsHole(this TerrainType terrain) => terrain == TerrainType.Hole;
  public static bool IsTarget(this TerrainType terrain) => terrain == TerrainType.Target;
  public static bool IsEntrance(this TerrainType t)
        => t == TerrainType.Entrance;
  public static bool IsExit(this TerrainType t)
        => t == TerrainType.Exit;
}
