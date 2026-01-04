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
      // In generation, Holes are Walls logic
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
    TerrainType[,] grid = new TerrainType[w, h];
    List<Vector2Int> floors = new List<Vector2Int>();
    List<Vector2Int> entranceExitCandidates = new List<Vector2Int>();

    // 1. Initialize Grid & Collect Floor Candidates
    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (roomShape[x, y] == 1) // 1 = Floor
        {
          grid[x, y] = TerrainType.Floor;
          floors.Add(new Vector2Int(x, y));
        } else {
          grid[x, y] = TerrainType.Wall;
        }
      }
    }

    // 2. Identify valid Entrance & Exit candidates (Walls adjacent to at least one floor)
    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (grid[x, y] == TerrainType.Wall && HasAdjacentFloor(grid, x, y)) {
          entranceExitCandidates.Add(new Vector2Int(x, y));
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
      // Entrance: Min X -> Min Y
      Vector2Int bestEnt = entranceExitCandidates
          .OrderBy(v => v.x)
          .ThenBy(v => v.y)
          .First();

      // Exit: Max X -> Max Y
      Vector2Int bestExit = entranceExitCandidates
          .OrderByDescending(v => v.x)
          .ThenByDescending(v => v.y)
          .First();

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

    // 3. Identify Structural Cut Vertices (Hole Candidates)
    // A cut vertex is a floor tile that, if removed, splits the reachable area from the player.
    List<Vector2Int> validCuts = new List<Vector2Int>();

    // Entrance/Exit placed, we have 1 extra reachable node (Exit)
    // that isn't in the 'floors' list.
    int extraNodes = entrancePlaced ? 1 : 0;

    foreach (var f in floors) {
      if (IsCutVertexForPlayer(grid, f, playerPos, floors, extraNodes))
        validCuts.Add(f);
    }

    // Shuffle cuts for variety
    validCuts = validCuts.OrderBy(_ => Random.value).ToList();

    List<Vector2Int> holes = new List<Vector2Int>();
    List<Vector2Int> boxes = new List<Vector2Int>();
    int targetsPlaced = 0;

    // 4. Place Holes using "Lock and Key" heuristic
    for (int i = 0; i < holeCount; i++) {
      if (i < validCuts.Count) {
        // --- SMART HOLE PLACEMENT ---
        Vector2Int holePos = validCuts[i];
        holes.Add(holePos);
        floors.Remove(holePos);

        // A. Force a Target BEHIND the hole (The "Lock")
        // This forces the player to fill the hole to solve the level.
        if (targetsPlaced < targetCount) {
          Vector2Int? behindPos = GetPositionBehindHole(grid, holePos, playerPos, floors);
          if (behindPos.HasValue) {
            grid[behindPos.Value.x, behindPos.Value.y] = TerrainType.Target;
            floors.Remove(behindPos.Value);
            targetsPlaced++;

            // B. Force a Box on PLAYER SIDE (The "Key")
            // Ensures the player has access to a box to fill the hole.
            if (boxes.Count < totalBoxes) {
              Vector2Int? playerSidePos = GetPositionOnPlayerSide(grid, holePos, playerPos, floors);
              if (playerSidePos.HasValue) {
                boxes.Add(playerSidePos.Value);
                floors.Remove(playerSidePos.Value);
              }
            }
          }
        }
      } else {
        // --- FALLBACK: RANDOM HOLE ---
        // If we run out of smart cuts, just pick a random floor.
        if (floors.Count == 0) break;
        int r = Random.Range(0, floors.Count);
        holes.Add(floors[r]);
        floors.RemoveAt(r);
      }
    }

    // 5. Apply Holes to Grid
    foreach (var hole in holes) {
      grid[hole.x, hole.y] = TerrainType.Hole;
    }

    // 6. Place Remaining Targets (Randomly)
    while (targetsPlaced < targetCount) {
      if (floors.Count == 0) break;
      int r = Random.Range(0, floors.Count);
      grid[floors[r].x, floors[r].y] = TerrainType.Target;
      floors.RemoveAt(r);
      targetsPlaced++;
    }

    // 7. Place Remaining Boxes (Randomly)
    while (boxes.Count < totalBoxes) {
      if (floors.Count == 0) break;
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
        // We only care about base floors, but technically Targets/Holes haven't been placed yet.
        // At this stage, everything non-wall is just 'Floor'.
        if (grid[nx, ny] == TerrainType.Floor) return true;
      }
    }

    return false;
  }

  /// <summary>
  /// Returns true if removing 'candidate' makes some floors unreachable from 'playerPos'.
  /// </summary>
  private bool IsCutVertexForPlayer(
      TerrainType[,] grid,
      Vector2Int candidate,
      Vector2Int playerPos,
      List<Vector2Int> availableFloors,
      int extraNodesCount = 0) {
    // availableFloors contains ALL floors currently available (including candidate).
    // If graph is connected, CountReachable should equal (availableFloors.Count - 1).
    // If it's less, removing 'candidate' disconnected something.
    int totalReachableNodes = availableFloors.Count + extraNodesCount; // floors list + player

    _floodFillScanner.Scan(new FloodFillGridAdapter(grid), start: playerPos, obstacle: candidate);

    // If reachable nodes < total nodes (minus the one we ignored), we have a cut.
    return _floodFillScanner.Count < totalReachableNodes;
  }

  private Vector2Int? GetPositionBehindHole(
      TerrainType[,] grid,
      Vector2Int hole,
      Vector2Int playerPos,
      List<Vector2Int> availableFloors) {
    // 1. Get reachable set ignoring the hole
    _floodFillScanner.Scan(new FloodFillGridAdapter(grid), start: playerPos, obstacle: hole);

    // 2. Find floors NOT in reachable set (The "Behind" side)
    var candidates =
        availableFloors.Where(f => f != hole && !_floodFillScanner.IsReached(f)).ToList();

    if (candidates.Count > 0) return candidates[Random.Range(0, candidates.Count)];

    return null;
  }

  private Vector2Int? GetPositionOnPlayerSide(
      TerrainType[,] grid,
      Vector2Int hole,
      Vector2Int playerPos,
      List<Vector2Int> availableFloors) {
    // 1. Get reachable set ignoring the hole
    _floodFillScanner.Scan(new FloodFillGridAdapter(grid), start: playerPos, obstacle: hole);

    // 2. Find floors IN the reachable set (The "Player" side)
    var candidates =
        availableFloors.Where(f => f != hole && _floodFillScanner.IsReached(f)).ToList();

    if (candidates.Count > 0)
      return candidates[Random.Range(0, candidates.Count)];

    return null;
  }
}
