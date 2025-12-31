using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct SokobanState : IEquatable<SokobanState> {
  // 1. Static Reference (Shared by all states, zero memory cost per state)
  public readonly TerrainType[,] TerrainGrid;

  // 2. Dynamic Data (The only things that change)
  public readonly Vector2Int PlayerPos;
  public readonly Vector2Int[] CratePositions; // Array is slightly faster/lighter than List for fixed counts
  public readonly HashSet<Vector2Int> FilledHoles; // Tracks holes that have been filled

  // Constructor
  public SokobanState(TerrainType[,] terrainGrid, Vector2Int playerPos, IEnumerable<Vector2Int> crates, IEnumerable<Vector2Int> filledHoles = null) {
    TerrainGrid = terrainGrid;
    PlayerPos = playerPos;

    // Always sort crates to ensure Hash consistency (State A with crates at [1,2] is identical to State B with crates at [2,1])
    var sortedCrates = crates.ToArray();
    Array.Sort(sortedCrates, (a, b) => {
      int cmp = a.x.CompareTo(b.x);
      return cmp != 0 ? cmp : a.y.CompareTo(b.y);
    });
    CratePositions = sortedCrates;

    // Copy filled holes (or create empty if null)
    FilledHoles = filledHoles != null ? new HashSet<Vector2Int>(filledHoles) : new HashSet<Vector2Int>();
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
    for (int i = 0; i < CratePositions.Length; i++) {
      if (CratePositions[i].x == x && CratePositions[i].y == y) return true;
    }
    return false;
  }

  public bool IsPlayerAt(int x, int y) {
    return PlayerPos.x == x  &&  PlayerPos.y == y;
  }

  public bool IsFilledHoleAt(int x, int y) {
    return FilledHoles.Contains(new Vector2Int(x, y));
  }


  /// <summary>
  /// Does this state represent a Win?
  /// </summary>
  public bool IsWin() {
    // A win requires every Target to be covered by a Crate.
    // (Note: This logic assumes num_crates >= num_targets. If crates fall in holes,
    // we need to be careful. Ideally, we check if all TARGETS are satisfied.)

    int targetsSatisfied = 0;
    int totalTargets = 0; // Ideally cache this in the Solver, not calc every frame

    int width = TerrainGrid.GetLength(0);
    int height = TerrainGrid.GetLength(1);

    for (int x = 0; x < width; x++) {
      for (int y = 0; y < height; y++) {
        if (TerrainGrid[x, y].IsTarget()) {
          totalTargets++;
          if (IsCrateAt(x, y)) {
            targetsSatisfied++;
          }
        }
      }
    }

    // Win if we satisfied all targets.
    // (Or if targetsSatisfied == CratePositions.Length if we want "All crates docked")
    // Let's stick to "All Targets Covered"
    return totalTargets > 0 && targetsSatisfied == totalTargets;
  }

  public bool IsValidPos(int x, int y) {
    return x >= 0 && x < TerrainGrid.GetLength(0) && y >= 0 && y < TerrainGrid.GetLength(1);
  }

  // ========== EQUALITY & HASHING (CRITICAL FOR SOLVER) ==========

  public bool Equals(SokobanState other) {
    // 1. Player check
    if (PlayerPos != other.PlayerPos) return false;

    // 2. Crate check (Arrays are sorted, so direct index comparison works)
    if (CratePositions.Length != other.CratePositions.Length) return false;
    for (int i = 0; i < CratePositions.Length; i++) {
      if (CratePositions[i] != other.CratePositions[i]) return false;
    }

    // 3. Filled Holes check
    // (Count check first for speed)
    if (FilledHoles.Count != other.FilledHoles.Count) return false;
    // (Set equality)
    return FilledHoles.SetEquals(other.FilledHoles);
  }

  public override bool Equals(object obj) => obj is SokobanState other && Equals(other);

  public override int GetHashCode() {
    var hashCode = new HashCode();
    hashCode.Add(PlayerPos);

    // Hash all crates
    for (int i = 0; i < CratePositions.Length; i++) {
      hashCode.Add(CratePositions[i]);
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
