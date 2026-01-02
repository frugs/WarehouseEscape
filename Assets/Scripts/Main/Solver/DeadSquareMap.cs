using System.Collections.Generic;
using UnityEngine;

public class DeadSquareMap {
  private readonly bool[,] IsDeadMap;
  public int Width { get; }
  public int Height { get; }

  public DeadSquareMap(SokobanState initialState) {
    Width = initialState.TerrainGrid.GetLength(0);
    Height = initialState.TerrainGrid.GetLength(1);
    IsDeadMap = new bool[Width, Height];

    CalculateDeadSquares(initialState);
  }

  public bool IsDeadSquare(int x, int y) {
    if (x < 0 || x >= Width || y < 0 || y >= Height) return true;
    return IsDeadMap[x, y];
  }

  private void CalculateDeadSquares(SokobanState state) {
    // 1. Initialize: Everything is Dead (true) by default
    // We will "flood fill" safety starting from targets.
    for (int x = 0; x < Width; x++) {
      for (int y = 0; y < Height; y++) {
        // Walls are dead.
        // Floors are dead (until proven safe).
        IsDeadMap[x, y] = true;
      }
    }

    Queue<Vector2Int> safeQueue = new Queue<Vector2Int>();

    // 2. Seed with Targets (Targets are always safe destinations)
    for (int x = 0; x < Width; x++) {
      for (int y = 0; y < Height; y++) {
        if (state.TerrainGrid[x, y].IsTarget()) {
          SetSafe(x, y, safeQueue);
        }
      }
    }

    // 3. Reverse Reachability Search
    while (safeQueue.Count > 0) {
      Vector2Int b = safeQueue.Dequeue(); // B is a known Safe spot

      // Check all neighbors A to see if they can push INTO B
      foreach (var dir in new[] {
                   Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
               }) {
        // Geometric Layout: [Player C] -> [Crate A] -> [Dest B]
        // We know B. We want to verify A.
        // A is the neighbor of B (B - dir).
        // C is the neighbor of A (A - dir = B - 2*dir).

        Vector2Int a = b - dir;
        Vector2Int c = b - (dir * 2);

        if (IsValidFloor(state, a) && IsValidFloor(state, c)) {
          // If we haven't marked A as safe yet, do it now
          if (IsDeadMap[a.x, a.y]) {
            SetSafe(a.x, a.y, safeQueue);
          }
        }
      }
    }
  }

  private void SetSafe(int x, int y, Queue<Vector2Int> queue) {
    IsDeadMap[x, y] = false;
    queue.Enqueue(new Vector2Int(x, y));
  }

  private bool IsValidFloor(SokobanState state, Vector2Int pos) {
    if (pos.x < 0 || pos.x >= Width || pos.y < 0 || pos.y >= Height) return false;
    var t = state.TerrainGrid[pos.x, pos.y];
    return t != TerrainType.Wall; // Any non-wall is potentially valid for pushing/standing
  }
}
