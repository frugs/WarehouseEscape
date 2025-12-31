using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class SokobanGenerator {
  private const int AttemptsPerLevel = 100;
  private const int GeneratorSolverLimit = 100_000;

  /// <summary>
  /// Generates a valid, solvable Sokoban level.
  /// </summary>
  /// <param name="boxCount">Number of crates/goals to place.</param>
  /// <returns>A fully constructed SokobanState, or null if generation failed.</returns>
  public SokobanState? Generate(
    int minSize = 6, int maxSize = 12, int targetCount = 3, int holeCount = 2) {
    const int TimeoutMs = 60_000;
    Stopwatch timer = Stopwatch.StartNew();

    for (int i = 0; i < AttemptsPerLevel; i++) {
      if (timer.ElapsedMilliseconds > TimeoutMs) {
        UnityEngine.Debug.LogError(
          $"Timeout! {i} attempts in {timer.ElapsedMilliseconds}ms.");
        return null; // Give up
      }

      // 1. Create Room Structure
      int maxWidth = Random.Range(minSize, maxSize);
      int maxHeight = Random.Range(minSize, maxSize);
      var roomShape = GenerateRoomShape(maxWidth, maxHeight);

      // 2. Populate (Player, crates, Goals)
      var maybeState = PopulateLevel(roomShape, targetCount, holeCount);

      if (maybeState == null) continue; // Population failed (no space)
      var state = (SokobanState)maybeState;

      // 3. Verify Solvability
      var solver = new SokobanSolver();
      if (solver.IsSolvable(state, GeneratorSolverLimit)) {
        UnityEngine.Debug.Log($"Generated solvable level in {i + 1} attempts.");
        return state;
      }
    }

    UnityEngine.Debug.LogError("Failed to generate a solvable level within attempt limit.");
    return null;
  }

  private int[,] GenerateRoomShape(int w, int h) {
    int[,] map = new int[w, h]; // 0 by default (Wall)

    // Try to place templates until full or timeout
    int failureCount = 0;
    int placedCount = 0;

    // Stop if density > 40% (example) or failures > 100
    while (failureCount < 100 && placedCount < (w * h) / 3) {
      var templates = GeneratorTemplates.Templates;
      var template = templates[Random.Range(0, templates.Count)];
      template = Rotate(template, Random.Range(0, 4));

      int tW = template.GetLength(0);
      int tH = template.GetLength(1);
      int x = Random.Range(0, w - tW);
      int y = Random.Range(0, h - tH);

      if (CanPlace(map, template, x, y)) {
        Place(map, template, x, y);
        placedCount++;
        failureCount = 0; // Reset failure count on success to encourage clustering
      } else {
        failureCount++;
      }
    }

    // Ensure center is valid if map is somehow empty
    if (map[w / 2, h / 2] == 0 && placedCount == 0) {
      map[w / 2, h / 2] = 1;
    }

    // Remove disconnected islands
    int[,] connectedMap = PruneDisconnectedParts(map);

    // Trim excess walls (shrink to fit)
    return TrimMap(connectedMap);
  }

  private bool CanPlace(int[,] map, int[,] temp, int startX, int startY) {
    int tW = temp.GetLength(0);
    int tH = temp.GetLength(1);

    for (int i = 0; i < tW; i++) {
      for (int j = 0; j < tH; j++) {
        int mapVal = map[startX + i, startY + j];
        int tempVal = temp[i, j];

        // Rule: If Map has Floor (1), Template CANNOT be Wall (0).
        // Existing floors must be preserved or matched.
        if (mapVal == 1 && tempVal == 0) return false;
      }
    }
    return true;
  }

  private void Place(int[,] map, int[,] template, int startX, int startY) {
    int tW = template.GetLength(0);
    int tH = template.GetLength(1);
    for (int i = 0; i < tW; i++) {
      for (int j = 0; j < tH; j++) {
        if (template[i, j] == 1) {
          map[startX + i, startY + j] = 1;
        }
      }
    }
  }

  // BFS to find largest connected component and remove the rest
  private int[,] PruneDisconnectedParts(int[,] map) {
    int w = map.GetLength(0);
    int h = map.GetLength(1);
    int[,] visited = new int[w, h]; // 0=Unvisited, 1=Visited

    List<Vector2Int> largestRegion = new List<Vector2Int>();

    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (map[x, y] == 1 && visited[x, y] == 0) {
          var region = GetRegion(map, visited, x, y);
          if (region.Count > largestRegion.Count) {
            largestRegion = region;
          }
        }
      }
    }

    // Rebuild map with ONLY the largest region
    int[,] newMap = new int[w, h];
    foreach (var pos in largestRegion) {
      newMap[pos.x, pos.y] = 1;
    }
    return newMap;
  }

  private List<Vector2Int> GetRegion(int[,] map, int[,] visited, int startX, int startY) {
    var region = new List<Vector2Int>();
    var queue = new Queue<Vector2Int>();
    queue.Enqueue(new Vector2Int(startX, startY));
    visited[startX, startY] = 1;

    int w = map.GetLength(0);
    int h = map.GetLength(1);

    while (queue.Count > 0) {
      var p = queue.Dequeue();
      region.Add(p);

      foreach (
        var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }) {
        Vector2Int n = p + dir;
        if (n.x >= 0 && n.y >= 0 && n.x < w && n.y < h) {
          if (map[n.x, n.y] == 1 && visited[n.x, n.y] == 0) {
            visited[n.x, n.y] = 1;
            queue.Enqueue(n);
          }
        }
      }
    }
    return region;
  }

  private int[,] TrimMap(int[,] map) {
    int w = map.GetLength(0);
    int h = map.GetLength(1);
    int minX = w, maxX = 0, minY = h, maxY = 0;
    bool foundFloor = false;

    // 1. Calculate Bounds
    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (map[x, y] == 1) // If Floor
        {
          if (x < minX) minX = x;
          if (x > maxX) maxX = x;
          if (y < minY) minY = y;
          if (y > maxY) maxY = y;
          foundFloor = true;
        }
      }
    }

    if (!foundFloor) return map; // Empty map, return as is

    // 2. Create new grid with 1-tile padding
    int padding = 1;
    int newWidth = (maxX - minX + 1) + (padding * 2);
    int newHeight = (maxY - minY + 1) + (padding * 2);

    int[,] newMap = new int[newWidth, newHeight];

    // 3. Copy content
    for (int x = minX; x <= maxX; x++) {
      for (int y = minY; y <= maxY; y++) {
        if (map[x, y] == 1) {
          // Shift to new coordinates (0,0 becomes padding,padding)
          newMap[x - minX + padding, y - minY + padding] = 1;
        }
      }
    }

    return newMap;
  }

  private int[,] Rotate(int[,] matrix, int n) {
    int[,] ret = (int[,])matrix.Clone();
    for (int i = 0; i < n; i++) {
      ret = Rotate90(ret);
    }
    return ret;
  }

  private int[,] Rotate90(int[,] matrix) {
    int w = matrix.GetLength(0);
    int h = matrix.GetLength(1);
    int[,] ret = new int[h, w];
    for (int i = 0; i < w; i++) {
      for (int j = 0; j < h; j++) {
        ret[j, w - 1 - i] = matrix[i, j];
      }
    }
    return ret;
  }

  /// <summary>
  /// Populates a room shape with Player, Holes, Targets, and Boxes using structural heuristics.
  /// </summary>
  private SokobanState? PopulateLevel(int[,] roomShape, int targetCount, int holeCount) {
    int w = roomShape.GetLength(0);
    int h = roomShape.GetLength(1);
    TerrainType[,] grid = new TerrainType[w, h];
    List<Vector2Int> floors = new List<Vector2Int>();

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

    int totalBoxes = targetCount + holeCount;
    // Basic capacity check: 1 Player + N Targets + M Holes + (N+M) Boxes
    if (floors.Count < 1 + targetCount + holeCount + totalBoxes) return null;

    // 2. Place Player FIRST (Randomly)
    // We need the player position to determine "Player Side" vs "Behind Hole"
    int pIdx = Random.Range(0, floors.Count);
    Vector2Int playerPos = floors[pIdx];
    floors.RemoveAt(pIdx);

    // 3. Identify Structural Cut Vertices (Hole Candidates)
    // A cut vertex is a floor tile that, if removed, splits the reachable area from the player.
    List<Vector2Int> validCuts = new List<Vector2Int>();
    foreach (var f in floors) {
      if (IsCutVertexForPlayer(grid, f, playerPos, floors))
        validCuts.Add(f);
    }

    // Shuffle cuts for variety
    validCuts = validCuts.OrderBy(x => Random.value).ToList();

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

    return new SokobanState(grid, playerPos, boxes);
  }

  // ---------------- Helper Methods ----------------

  /// <summary>
  /// Returns true if removing 'candidate' makes some floors unreachable from 'playerPos'.
  /// </summary>
  private bool IsCutVertexForPlayer(TerrainType[,] grid, Vector2Int candidate, Vector2Int playerPos, List<Vector2Int> availableFloors) {
    // availableFloors contains ALL floors currently available (including candidate).
    // If graph is connected, CountReachable should equal (availableFloors.Count - 1).
    // If it's less, removing 'candidate' disconnected something.

    // Note: We +1 to availableFloors count to account for the PlayerPos itself which isn't in 'availableFloors' list
    // but IS reachable.
    int totalReachableNodes = availableFloors.Count; // floors list + player

    int reachable = CountReachable(grid, playerPos, ignore: candidate);

    // If reachable nodes < total nodes (minus the one we ignored), we have a cut.
    return reachable < totalReachableNodes;
  }

  private Vector2Int? GetPositionBehindHole(TerrainType[,] grid, Vector2Int hole, Vector2Int playerPos, List<Vector2Int> availableFloors) {
    // 1. Get reachable set ignoring the hole
    HashSet<Vector2Int> reachable = GetReachableSet(grid, playerPos, ignore: hole);

    // 2. Find floors NOT in reachable set (The "Behind" side)
    var candidates = availableFloors.Where(f => f != hole && !reachable.Contains(f)).ToList();

    if (candidates.Count > 0)
      return candidates[Random.Range(0, candidates.Count)];

    return null;
  }

  private Vector2Int? GetPositionOnPlayerSide(TerrainType[,] grid, Vector2Int hole, Vector2Int playerPos, List<Vector2Int> availableFloors) {
    // 1. Get reachable set ignoring the hole
    HashSet<Vector2Int> reachable = GetReachableSet(grid, playerPos, ignore: hole);

    // 2. Find floors IN the reachable set (The "Player" side)
    var candidates = availableFloors.Where(f => f != hole && reachable.Contains(f)).ToList();

    if (candidates.Count > 0)
      return candidates[Random.Range(0, candidates.Count)];

    return null;
  }

  private int CountReachable(TerrainType[,] grid, Vector2Int start, Vector2Int ignore) {
    return GetReachableSet(grid, start, ignore).Count;
  }

  private HashSet<Vector2Int> GetReachableSet(TerrainType[,] grid, Vector2Int start, Vector2Int ignore) {
    int w = grid.GetLength(0);
    int h = grid.GetLength(1);
    HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
    Queue<Vector2Int> q = new Queue<Vector2Int>();

    if (start == ignore) return visited;

    q.Enqueue(start);
    visited.Add(start);

    while (q.Count > 0) {
      var p = q.Dequeue();
      foreach (var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }) {
        Vector2Int n = p + dir;

        // Bounds Check
        if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;

        // Ignore the candidate hole/blocker
        if (n == ignore) continue;

        // Wall Check
        if (grid[n.x, n.y] == TerrainType.Wall) continue;

        // Note: We treat existing Targets/Floors as walkable.
        // Since we haven't placed Targets yet in the grid (mostly), this primarily checks walls.
        // But if we placed some Targets already, they are walkable.

        if (!visited.Contains(n)) {
          visited.Add(n);
          q.Enqueue(n);
        }
      }
    }
    return visited;
  }

  private int[,] RotateTemplate(int[,] original, int times) {
    if (times == 0) return original;
    int w = original.GetLength(0);
    int h = original.GetLength(1);
    int[,] newArr = new int[h, w];

    for (int i = 0; i < w; i++) {
      for (int j = 0; j < h; j++) {
        newArr[j, w - 1 - i] = original[i, j];
      }
    }
    return RotateTemplate(newArr, times - 1);
  }
}
