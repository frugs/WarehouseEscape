using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct SokobanState : IEquatable<SokobanState> {
  public static readonly Comparison<Vector2Int> CrateComparer = (a, b) => {
    int cmp = a.x.CompareTo(b.x);
    return cmp != 0 ? cmp : a.y.CompareTo(b.y);
  };

  // 1. Static Reference (Shared by all states, zero memory cost per state)
  public TerrainType[,] TerrainGrid { get; }

  // 2. Dynamic Data (The only things that change)
  public Vector2Int PlayerPos { get; }

  // Array is slightly faster/lighter than List for fixed counts
  public Vector2Int[] CratePositions { get; }

  public CowHashSet<Vector2Int> FilledHoles { get; } // Tracks holes that have been filled

  public static SokobanState Create(
      TerrainType[,] terrainGrid,
      Vector2Int playerPos,
      IEnumerable<Vector2Int> crates,
      CowHashSet<Vector2Int>? filledHoles = null) {
    var sortedCrates = crates.ToArray();
    Array.Sort(sortedCrates, CrateComparer);
    return new SokobanState(terrainGrid, playerPos, sortedCrates, filledHoles);
  }


  private SokobanState(
      TerrainType[,] terrainGrid,
      Vector2Int playerPos,
      Vector2Int[] crates,
      CowHashSet<Vector2Int>? filledHoles = null) {
    TerrainGrid = terrainGrid;
    PlayerPos = playerPos;
    CratePositions = crates;
    FilledHoles = filledHoles ?? CowHashSet<Vector2Int>.Empty;
  }

  public int GridWidth => TerrainGrid.GetLength(0);
  public int GridHeight => TerrainGrid.GetLength(1);

  // ========== LOGIC HELPERS ==========

  /// <summary>
  /// Can the player walk onto this coordinate?
  /// </summary>
  public bool CanPlayerWalk(int x, int y) {
    if (!IsValidPos(x, y)) return false;

    var terrain = TerrainGrid[x, y];

    // 1. Check basic terrain walkability (Floor/FilledHole checks)
    // If it's a Wall, this returns false immediately.
    if (!terrain.PlayerCanWalk()) {
      // 2. Exception: It IS a Hole, but it is currently FILLED.
      // If it's a Hole and FilledHoles DOES NOT contain it, we return false.
      // If it contains it, we proceed (it acts like a Floor).
      if (!terrain.IsHole() || !IsFilledHoleAt(x, y)) {
        return false;
      }
    }

    // 3. Crate Check (Blocked if occupied by a crate)
    if (IsCrateAt(x, y)) return false;

    return true;
  }

  /// <summary>
  /// Can a crate be pushed onto this coordinate?
  /// </summary>
  public bool CanReceiveCrate(int x, int y) {
    if (!IsValidPos(x, y)) return false;

    // 1. Static Terrain Check
    var terrain = TerrainGrid[x, y];
    if (!terrain.CanReceiveCrate()) return false;

    // 3. Crate Check (Blocked if ANOTHER crate is already there)
    if (IsCrateAt(x, y)) return false;

    return true;
  }

  /// <summary>
  /// Does a crate exist at this specific coordinate?
  /// </summary>
  public bool IsCrateAt(int x, int y) {
    // Linear scan is faster than Hash lookup for small N (N < 20)
    foreach (var crate in CratePositions) {
      if (crate.x == x && crate.y == y) return true;
    }

    return false;
  }

  public bool IsPlayerAt(int x, int y) {
    return PlayerPos.x == x && PlayerPos.y == y;
  }

  public bool IsFilledHoleAt(int x, int y) {
    return FilledHoles.Contains(new Vector2Int(x, y));
  }

  public bool IsSolved(out Vector2Int? exitPos) {
    exitPos = null;
    int targetsSatisfied = 0;
    int totalTargets = 0;

    int width = TerrainGrid.GetLength(0);
    int height = TerrainGrid.GetLength(1);

    for (int x = 0; x < width; x++) {
      for (int y = 0; y < height; y++) {
        var terrain = TerrainGrid[x, y];
        if (terrain.IsTarget()) {
          totalTargets++;
          if (IsCrateAt(x, y)) {
            targetsSatisfied++;
          }
        } else if (terrain.IsExit()) {
          exitPos = new Vector2Int(x, y);
        }
      }
    }
    
    return targetsSatisfied == totalTargets;
  }

  /// <summary>
  /// Does this state represent a Win?
  /// </summary>
  public bool IsWin() {
    var solved = IsSolved(out var exitPos);
    if (!solved) return false;

    return exitPos == null || PlayerPos == exitPos;
  }

  public bool IsValidPos(int x, int y) {
    return GridUtils.IsInBounds(x, y, TerrainGrid);
  }

  public bool IsValidPos(Vector2Int pos) {
    return IsValidPos(pos.x, pos.y);
  }

  public SokobanState WithPlayerMove(Vector2Int newPlayerPos) {
    return new SokobanState(TerrainGrid, newPlayerPos, CratePositions, FilledHoles);
  }

  public SokobanState WithCratePush(
      Vector2Int newPlayerPos,
      Vector2Int oldCratePos,
      Vector2Int newCratePos) {
    // 1. Check for Hole Interaction
    var terrain = TerrainGrid[newCratePos.x, newCratePos.y];

    // Note: IsFilledHoleAt uses our own FilledHoles wrapper
    bool fellInHole = terrain.IsHole() && !IsFilledHoleAt(newCratePos.x, newCratePos.y);

    CowHashSet<Vector2Int> newHoles;
    Vector2Int[] newCrates;
    int oldLen = CratePositions.Length;

    // --- Logic Branch A: Crate Removed (Fell in Hole) ---
    if (fellInHole) {
      newHoles = FilledHoles.Add(newCratePos); // CowHashSet handles copy
      newCrates = new Vector2Int[oldLen - 1];

      int dst = 0;
      for (int i = 0; i < oldLen; i++) {
        if (CratePositions[i] == oldCratePos) continue;
        newCrates[dst++] = CratePositions[i];
      }
    }
    // --- Logic Branch B: Crate Moved (Standard Push) ---
    else {
      newHoles = FilledHoles; // Share reference
      newCrates = new Vector2Int[oldLen];

      int dst = 0;
      bool inserted = false;

      for (int i = 0; i < oldLen; i++) {
        Vector2Int current = CratePositions[i];

        // Skip the old crate position
        if (current == oldCratePos) continue;

        // Check insertion point
        if (!inserted) {
          if (CrateComparer(newCratePos, current) < 0) {
            newCrates[dst++] = newCratePos;
            inserted = true;
          }
        }

        newCrates[dst++] = current;
      }

      // If new crate is the largest, append at end
      if (!inserted) {
        newCrates[dst] = newCratePos;
      }
    }

    return new SokobanState(TerrainGrid, newPlayerPos, newCrates, newHoles);
  }

  public bool Equals(SokobanState other) {
    if (PlayerPos != other.PlayerPos) return false;

    // Fast Array Compare (Valid only if sorted!)
    if (CratePositions.Length != other.CratePositions.Length) return false;
    for (int i = 0; i < CratePositions.Length; i++) {
      if (CratePositions[i] != other.CratePositions[i]) return false;
    }

    // Fast Hash/Set Compare
    return FilledHoles.Equals(other.FilledHoles);
  }

  public override bool Equals(object obj) => obj is SokobanState other && Equals(other);

  public override int GetHashCode() {
    var hashCode = new HashCode();
    hashCode.Add(PlayerPos);

    // Hash all crates
    foreach (var crate in CratePositions) {
      hashCode.Add(crate);
    }

    // Hash holes (XOR or aggregate to be order-independent)
    int holeHash = 0;
    foreach (var hole in FilledHoles) {
      holeHash ^= hole.GetHashCode();
    }

    hashCode.Add(holeHash);

    return hashCode.ToHashCode();
  }
}
