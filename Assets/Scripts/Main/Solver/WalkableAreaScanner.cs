using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public class WalkableAreaScanner {
  private int[] _skipMap;
  private int _currentGen = 1;
  private int _width;
  private int _height;

  private readonly Queue<Vector2Int> _queue = new Queue<Vector2Int>(100);
  private readonly List<Vector2Int> _walkable = new List<Vector2Int>(100);

  public WalkableAreaScanner(int width = 0, int height = 0) {
    _width = width;
    _height = height;
    _skipMap = new int[width * height];
  }

  public void Reset(int width, int height) {
    _width = width;
    _height = height;
    _skipMap = new int[width * height];
    _currentGen = 1;
  }

  public List<Vector2Int> GetWalkableArea(
      SokobanState state,
      out Vector2Int canonicalPlayerPos) {
    return new List<Vector2Int>(GetWalkableAreaNoCopy(state, out canonicalPlayerPos));
  }

  /// <summary>
  /// Performs a flood fill to find all reachable squares from the player's current position.
  /// Returns the list of reachable squares AND the "Canonical" position (Min X, then Min Y).
  /// </summary>
  public List<Vector2Int> GetWalkableAreaNoCopy(
      SokobanState state,
      out Vector2Int canonicalPlayerPos) {
    // Profiler.BeginSample("Scanner.Setup");

    // Ensure Array Capacity
    int w = state.GridWidth;
    int h = state.GridHeight;
    if (_skipMap == null || _width != w || _height != h) {
      Reset(w, h);
    } else {
      _currentGen++;
    }

    canonicalPlayerPos = state.PlayerPos;

    _queue.Clear();
    _walkable.Clear();

    var start = state.PlayerPos;
    _queue.Enqueue(start);

    // Mark Start Visited
    _skipMap![start.y * w + start.x] = _currentGen;

    // Always skip crate positions
    foreach (var c in state.CratePositions) {
      _skipMap[c.y * w + c.x] = _currentGen;
    }

    Vector2Int minPos = start;
    // Profiler.EndSample();

    while (_queue.Count > 0) {
      var current = _queue.Dequeue();
      _walkable.Add(current);

      // Canonical check: "min x > min y"
      if (current.x < minPos.x || (current.x == minPos.x && current.y < minPos.y)) {
        minPos = current;
      }

      foreach (var dir in Vector2IntExtensions.Cardinals) {
        // Profiler.BeginSample("Scanner.Neighbor");
        var neighbor = current + dir;

        // Inline Bounds Check for speed
        if (neighbor.x < 0 || neighbor.x >= w || neighbor.y < 0 || neighbor.y >= h) {
          // Profiler.EndSample();
          continue;
        }

        // Profiler.BeginSample("Scanner.SkipCheck");
        int idx = neighbor.y * _width + neighbor.x;
        bool skip = _skipMap[idx] == _currentGen;
        // Profiler.EndSample();

        if (!skip) {
          // Profiler.BeginSample("Scanner.CanWalk");
          var terrain = state.TerrainGrid[neighbor.x, neighbor.y];
          bool canWalk = terrain.PlayerCanWalk() ||
                         (terrain.IsHole() && state.IsFilledHoleAt(neighbor.x, neighbor.y));
          // Profiler.EndSample();

          if (canWalk) {
            _skipMap[idx] = _currentGen; // Mark Visited
            _queue.Enqueue(neighbor);
          }
        }

        // Profiler.EndSample(); // Scanner.Neighbor
      }
    }

    canonicalPlayerPos = minPos;
    return _walkable;
  }
}
