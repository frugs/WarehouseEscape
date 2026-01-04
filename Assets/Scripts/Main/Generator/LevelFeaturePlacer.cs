using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelFeaturePlacer {
  private readonly struct FloodFillGridAdapter : IGridGraph {
    private readonly TerrainType[,] _grid;

    public int Width => _grid.GetLength(0);
    public int Height => _grid.GetLength(1);

    public FloodFillGridAdapter(TerrainType[,] grid) {
      _grid = grid;
    }

    public bool IsValid(int x, int y) {
      var t = _grid[x, y];
      return !t.IsWall() && !t.IsHole();
    }
  }

  private readonly FloodFillScanner _floodFillScanner = new();

  /// <summary>
  /// Populates a room shape with Player, Holes, Targets, and Boxes using structural heuristics.
  /// </summary>
  public SokobanState? PlaceFeatures(
      int[,] roomShape,
      int targetCount,
      int holeCount,
      bool useEntranceExit) {
    int w = roomShape.GetLength(0);
    int h = roomShape.GetLength(1);
    var grid = new TerrainType[w, h];
    var floors = new List<Vector2Int>();
    var entranceExitCandidates = new List<Vector2Int>();

    // 1. Initialize Grid & Collect Floor Candidates
    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (roomShape[x, y] == 1) {
          // 1 = Floor
          grid[x, y] = TerrainType.Floor;
          floors.Add(new Vector2Int(x, y));
        } else {
          grid[x, y] = TerrainType.Wall;
          // 2. Identify valid Entrance/Exit candidates (Walls adjacent to at least one floor)
          if (HasAdjacentFloor(grid, x, y)) {
            entranceExitCandidates.Add(new Vector2Int(x, y));
          }
        }
      }
    }

    int totalBoxes = targetCount + holeCount;
    // Basic capacity check: 1 Player + N Targets + M Holes + (N+M) Boxes
    if (floors.Count < 1 + targetCount + holeCount + totalBoxes) return null;

    Vector2Int playerPos;
    bool entrancePlaced = false;

    // 3. Place Entrance and Exit (Deterministic Min/Max)
    if (useEntranceExit && entranceExitCandidates.Count >= 2) {
      var bestEnt = entranceExitCandidates
          .OrderBy(v => v.x)
          .ThenBy(v => v.y)
          .First(); // Entrance: Min X -> Min Y

      var bestExit = entranceExitCandidates
          .OrderByDescending(v => v.x)
          .ThenByDescending(v => v.y)
          .First(); // Exit: Max X -> Max Y

      // Edge case safety (e.g. tiny 1x2 map): Ensure Exit != Entrance
      if (bestEnt == bestExit && entranceExitCandidates.Count > 1) {
        bestExit = entranceExitCandidates
            .OrderByDescending(v => v.x)
            .ThenByDescending(v => v.y)
            .Skip(1) // Pick the second best
            .First();
      }

      grid[bestEnt.x, bestEnt.y] = TerrainType.Entrance;
      grid[bestExit.x, bestExit.y] = TerrainType.Exit;

      playerPos = bestEnt; // Player starts on the Entrance tile
      entrancePlaced = true;
    } else {
      // Fallback: Random floor start
      int pIdx = Random.Range(0, floors.Count);
      playerPos = floors[pIdx];
      floors.RemoveAt(pIdx);
    }

    // 4. Identify Structural Cut Vertices (Hole Candidates) & Place Logic
    // Entrance & Exit placed = we have 2 extra reachable nodes that aren't in the floors list.
    int extraNodes = entrancePlaced ? 2 : 0;

    var holes = new List<Vector2Int>();
    var boxes = new List<Vector2Int>();
    int targetsPlaced = 0;

    // Optimization: Create the adapter once
    var gridAdapter = new FloodFillGridAdapter(grid);

    // Shuffle floors once to randomize candidates
    floors = floors.OrderBy(_ => Random.value).ToList();

    int attempts = 0;
    int maxAttempts = floors.Count; // Try every floor once

    while (holes.Count < holeCount && attempts < maxAttempts && floors.Count > 0) {
      // Pick a candidate
      int idx = attempts % floors.Count; // Simple iteration since we shuffled
      Vector2Int candidate = floors[idx];

      // --- OPTIMIZED CUT CHECK ---
      // 1. Run the scan IGNORING the candidate
      _floodFillScanner.Scan(gridAdapter, start: playerPos, obstacle: candidate);

      // 2. Check connectivity
      // totalReachable = (All Floors) - (Candidate) + (Entrance + Exit if exists)
      // Note: 'floors' list still contains 'candidate' at this point
      int totalReachableNodes = floors.Count - 1 + extraNodes;
      bool isCutVertex = _floodFillScanner.Count < totalReachableNodes;

      if (isCutVertex) {
        // We found a Cut Vertex!
        // The scanner state currently represents the "Player Side".

        // A. Force a Target BEHIND the hole (The "Lock")
        // Only if we still need targets
        if (targetsPlaced < targetCount) {
          // Find a floor NOT reachable by the scan (Behind Side)
          Vector2Int? behindPos = GetUnreachedFloor(floors, candidate);

          if (behindPos.HasValue) {
            grid[behindPos.Value.x, behindPos.Value.y] = TerrainType.Target;
            floors.Remove(behindPos.Value);
            targetsPlaced++;
          }
        }

        // B. Force a Box on PLAYER SIDE (The "Key")
        // Only if we still need boxes
        if (boxes.Count < totalBoxes) {
          // Find a floor REACHABLE by the scan (Player Side)
          // We can pick directly from the scanner's result list
          Vector2Int? playerSidePos = GetReachedFloor(floors, candidate);

          if (playerSidePos.HasValue) {
            boxes.Add(playerSidePos.Value);
            floors.Remove(playerSidePos.Value);
          }
        }

        // Place the Hole
        holes.Add(candidate);
        floors.Remove(candidate); // Remove from available floors

        // Since we modified the grid/floors, we should restart/reset our loop logic slightly
        // or just continue. The graph topology changed, so previous assumptions might be invalid.
        // For a generator, just continuing is usually acceptable entropy.
      }

      attempts++;
    }

    // 5. Apply Holes to Grid
    foreach (var hole in holes) {
      grid[hole.x, hole.y] = TerrainType.Hole;
    }

    // --- FALLBACK FILLING ---
    // If we failed to place smart holes/targets/boxes, fill the rest randomly.

    // Fill remaining holes (randomly)
    while (holes.Count < holeCount && floors.Count > 0) {
      int r = Random.Range(0, floors.Count);
      var hole = floors[r];
      grid[hole.x, hole.y] = TerrainType.Hole;
      holes.Add(hole);
      floors.RemoveAt(r);
    }

    // Fill remaining targets (randomly)
    while (targetsPlaced < targetCount && floors.Count > 0) {
      int r = Random.Range(0, floors.Count);
      grid[floors[r].x, floors[r].y] = TerrainType.Target;
      floors.RemoveAt(r);
      targetsPlaced++;
    }

    // Fill remaining boxes (randomly)
    while (boxes.Count < totalBoxes && floors.Count > 0) {
      int r = Random.Range(0, floors.Count);
      boxes.Add(floors[r]);
      floors.RemoveAt(r);
    }

    // Ensure all boxes are on floors
    foreach (var box in boxes) {
      grid[box.x, box.y] = TerrainType.Floor;
    }

    return SokobanState.Create(grid, playerPos, boxes);
  }

  // --- OPTIMIZED HELPERS ---

  /// <summary>
  /// Finds a floor in the list that was visited by the last scan.
  /// Uses O(1) lookup via scanner.IsVisited.
  /// </summary>
  private Vector2Int? GetReachedFloor(List<Vector2Int> availableFloors, Vector2Int excludePos) {
    // Optimization: Pick a random index and linear scan to avoid re-allocating a list
    int startIdx = Random.Range(0, availableFloors.Count);
    for (int i = 0; i < availableFloors.Count; i++) {
      int idx = (startIdx + i) % availableFloors.Count;
      Vector2Int f = availableFloors[idx];

      if (f != excludePos && _floodFillScanner.IsReached(f.x, f.y)) {
        return f;
      }
    }

    return null;
  }

  /// <summary>
  /// Finds a floor in the list that was NOT visited by the last scan.
  /// </summary>
  private Vector2Int? GetUnreachedFloor(List<Vector2Int> availableFloors, Vector2Int excludePos) {
    int startIdx = Random.Range(0, availableFloors.Count);
    for (int i = 0; i < availableFloors.Count; i++) {
      int idx = (startIdx + i) % availableFloors.Count;
      Vector2Int f = availableFloors[idx];

      if (f != excludePos && !_floodFillScanner.IsReached(f.x, f.y)) {
        return f;
      }
    }

    return null;
  }

  private bool HasAdjacentFloor(TerrainType[,] grid, int x, int y) {
    int w = grid.GetLength(0);
    int h = grid.GetLength(1);
    // Up, Down, Left, Right
    int[] dx = { 0, 0, 1, -1 };
    int[] dy = { 1, -1, 0, 0 };

    for (int i = 0; i < 4; i++) {
      int nx = x + dx[i];
      int ny = y + dy[i];

      if (nx >= 0 && nx < w && ny >= 0 && ny < h) {
        if (grid[nx, ny] == TerrainType.Floor) return true;
      }
    }

    return false;
  }
}
