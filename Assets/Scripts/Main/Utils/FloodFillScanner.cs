using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

// Define the "Graph" contract.
// This allows the Scanner to ask "Where can I go from here?"
public interface IGridGraph {
  int Width { get; }
  int Height { get; }
  bool IsValid(int x, int y);
}

public class FloodFillScanner {
  private int[] _visitMap;
  private int _currentGen = 1;
  private int _width;
  private int _height;

  // We reuse these collections to avoid GC pressure
  private readonly Queue<Vector2Int> _queue = new Queue<Vector2Int>(256);
  private readonly List<Vector2Int> _reached = new List<Vector2Int>(256);

  // Public Read-Only View
  public IReadOnlyList<Vector2Int> Reached => _reached;
  public int Count => _reached.Count;

  public void EnsureSize(int w, int h) {
    if (_visitMap == null || _width != w || _height != h) {
      _width = w;
      _height = h;
      _visitMap = new int[w * h];
      _currentGen = 1;
    }
  }

  /// <summary>
  /// Performs a BFS flood fill.
  /// </summary>
  /// <param name="graph">The data source (Grid/State)</param>
  /// <param name="start">Starting coordinates</param>
  /// <param name="obstacles">Optional: Coordinates to treat as a unreachable</param>
  public void Scan<TGraph>(TGraph graph, Vector2Int start, IEnumerable<Vector2Int> obstacles = null)
      where TGraph : IGridGraph {
    // 1. Setup
    EnsureSize(graph.Width, graph.Height);
    _currentGen++; // Cheap "Clear"
    _queue.Clear();
    _reached.Clear();

    // 2. Validate Start
    if (!IsValid(graph, start.x, start.y)) return;

    if (obstacles != null) {
      foreach (var obs in obstacles) {
        if (obs.x >= 0 && obs.x < _width && obs.y >= 0 && obs.y < _height) {
          _visitMap[obs.y * _width + obs.x] = -_currentGen;
        }
      }
    }

    Push(start);

    while (_queue.Count > 0) {
      Vector2Int current = _queue.Dequeue();
      _reached.Add(current);

      // Manual neighbor unrolling is usually faster than iterating an array of directions
      // inside tight loops, but for cleanliness, we'll iterate.
      // Optimization: Inline this if profiling shows it's a hotspot.
      CheckAndPush(graph, current.x + 1, current.y);
      CheckAndPush(graph, current.x - 1, current.y);
      CheckAndPush(graph, current.x, current.y + 1);
      CheckAndPush(graph, current.x, current.y - 1);
    }
  }

  [UsedImplicitly]
  public void Scan<TGraph>(TGraph graph, Vector2Int start, Vector2Int obstacle)
      where TGraph : IGridGraph {
    Scan(graph, start, new[] { obstacle });
  }

  private void CheckAndPush(IGridGraph graph, int x, int y) {
    // Bounds & Ignore
    if (x < 0 || x >= _width || y < 0 || y >= _height) return;

    // Visited Check (Fastest check first)
    int idx = y * _width + x;
    int val = _visitMap[idx];
    if (Math.Abs(val) == _currentGen) return;

    // Walkability Check (Slower interface call)
    if (!graph.IsValid(x, y)) return;

    // Visit
    _visitMap[idx] = _currentGen;
    _queue.Enqueue(new Vector2Int(x, y));
  }

  private bool IsValid(IGridGraph graph, int x, int y) {
    if (x < 0 || x >= _width || y < 0 || y >= _height) return false;
    return graph.IsValid(x, y);
  }

  private void Push(Vector2Int pos) {
    int idx = pos.y * _width + pos.x;
    _visitMap[idx] = _currentGen;
    _queue.Enqueue(pos);
  }

  /// <summary>
  /// O(1) check if a specific position was reached in the last scan.
  /// </summary>
  public bool IsReached(int x, int y) {
    if (x < 0 || x >= _width || y < 0 || y >= _height) return false;
    return _visitMap[y * _width + x] == _currentGen;
  }

  public bool IsReached(Vector2Int pos) {
    return IsReached(pos.x, pos.y);
  }
}
