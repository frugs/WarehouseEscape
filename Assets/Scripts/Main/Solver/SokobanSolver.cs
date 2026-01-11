using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

public class SokobanSolver {
  private const int MAX_ITERATIONS = 10_000_000; // Limit total states explored
  private const long MAX_MS = 60_000; // 60s timeout

  private struct PathNode {
    public SokobanState? ParentState { get; set; }
    public SokobanMove? Move { get; set; }
  }

  private struct SolverContext {
    public SokobanState CanonicalState { get; set; }
    public SokobanState RawState { get; set; }
    public List<Vector2Int> WalkableArea { get; set; }
  }

  private DeadSquareMap DeadSquareMap { get; set; }

  /// <summary>
  /// Checks if a level is solvable within the given iteration limit.
  /// </summary>
  public bool IsSolvable(
      SokobanState state,
      int maxIterations = MAX_ITERATIONS,
      long timeoutMs = MAX_MS,
      CancellationToken cancellation = default) {
    var solution = FindSolutionPath(state, maxIterations, timeoutMs, cancellation);
    return solution != null;
  }

  /// <summary>
  /// Finds the shortest solution path using BFS with Canonical State Optimization.
  /// This treats all reachable player positions as a single state.
  /// </summary>
  public List<SokobanMove> FindSolutionPath(
      SokobanState initialState,
      int maxIterations = MAX_ITERATIONS,
      long timeoutMs = MAX_MS,
      CancellationToken cancellation = default) {
    // 1. Setup
    var parentMap = new Dictionary<SokobanState, PathNode>();
    var visited = new HashSet<SokobanState>();
    var queue = new PriorityQueue<SolverContext, int>();
    var width = initialState.GridWidth;
    var height = initialState.GridHeight;
    int statesExplored = 0;

    var walkableAreaScanner = new SolverReachabilityScanner();

    var targets = new List<Vector2Int>();
    var holes = new List<Vector2Int>();
    for (int x = 0; x < width; x++)
    for (int y = 0; y < height; y++) {
      var t = initialState.TerrainGrid[x, y];
      if (t.IsTarget()) {
        targets.Add(new Vector2Int(x, y));
      }

      if (t.IsTrueHole()) {
        holes.Add(new Vector2Int(x, y));
      }
    }

    int GetHeuristic(SokobanState s) {
      int cost = 0;
      foreach (var crate in s.CratePositions) {
        int best = int.MaxValue;

        foreach (var target in targets) {
          int d = Math.Abs(crate.x - target.x) + Math.Abs(crate.y - target.y);
          if (d < best) best = d;
        }

        foreach (var hole in holes) {
          int d = Math.Abs(crate.x - hole.x) + Math.Abs(crate.y - hole.y);
          if (d < best) best = d;
        }

        cost += best;
      }

      return cost;
    }

    // 2. Canonicalize Start State
    // We convert the initial raw state into a canonical state (player at top-left-most reachable pos)
    // This ensures our visited set works correctly immediately.
    var walkable =
        walkableAreaScanner.GetWalkableArea(
            initialState,
            out var startCanonicalPos);
    var canonicalStart = SokobanState.Create(
        initialState.TerrainGrid,
        startCanonicalPos,
        initialState.CratePositions,
        initialState.FilledHoles);

    queue.Enqueue(
        new SolverContext() {
            CanonicalState = canonicalStart, RawState = initialState, WalkableArea = walkable
        },
        GetHeuristic(canonicalStart));
    visited.Add(canonicalStart);

    // Note: The parentMap stores the path of CANONICAL states.
    // We treat the canonicalStart as the root (Parent = null).
    parentMap[canonicalStart] = new PathNode { ParentState = null, Move = null };

    int iterations = 0;
    Stopwatch timer = Stopwatch.StartNew();
    DeadSquareMap = new DeadSquareMap(initialState);

    // 3. BFS Loop
    while (queue.Count > 0) {
      if (cancellation.IsCancellationRequested) return null;

      if ((maxIterations > 0 && ++iterations > maxIterations) ||
          (timeoutMs > 0 && timer.ElapsedMilliseconds > MAX_MS)) {
        UnityEngine.Debug.Log(
            $"Solver Timeout! Checked {statesExplored} states in {timer.ElapsedMilliseconds}ms.");
        return null; // Give up
      }

      var currentContext = queue.Dequeue();
      var currentState = currentContext.CanonicalState;
      walkable = currentContext.WalkableArea;

      // Check Win on the canonical state (crates are the same)
      if (currentState.IsSolved(out var exitPos)) {
        if (exitPos == null || walkable.Contains(exitPos.Value)) {
          UnityEngine.Debug.Log(
              $"Solved - Checked {statesExplored} states in {timer.ElapsedMilliseconds}ms.");

          var solution = ReconstructPath(parentMap, currentContext.CanonicalState, initialState);
          if (exitPos != null) {
            var rawState = currentContext.RawState;
            var pathToExit = Pather.FindPath(
                rawState,
                rawState.PlayerPos,
                exitPos.Value);
            if (pathToExit is { Count: > 0 }) {
              // Convert raw coords into PlayerMove steps
              var walkPos = rawState.PlayerPos;
              foreach (var target in pathToExit) {
                solution.Add(SokobanMove.PlayerMove(walkPos, target));
                walkPos = target;
              }
            }
          }

          return solution;
        }
      }

      // 4. Generate Moves (Pushes Only)
      foreach (var standPos in walkable) {
        // Check all 4 directions for potential pushes
        foreach (var dir in Vector2IntExtensions.Cardinals) {
          var cratePos = standPos + dir;

          // Is there a crate here?
          if (currentState.IsCrateAt(cratePos.x, cratePos.y)) {
            var pushTo = cratePos + dir;

            // Can we push it? (Target cell must be valid and not a deadlock)
            bool valid = IsValidCratePush(currentState, pushTo) &&
                         !IsDeadlock(currentState, pushTo, width, height) &&
                         !IsCrateInDeadSquare(pushTo);

            if (valid) {
              // Construct the Push Move
              var pushMove = SokobanMove.CratePush(standPos, cratePos, cratePos, pushTo);

              // Apply the push to get the 'raw' next state
              var nextRawState = MoveRules.ApplyMove(currentState, pushMove);

              // 5. Canonicalize the Next State
              // After pushing, the player is at 'cratePos'. We flood fill from there
              // to find the new canonical player position.
              var nextWalkable = walkableAreaScanner.GetWalkableAreaNoCopy(
                  nextRawState,
                  out var nextCanonicalPos);

              var nextCanonical = nextRawState.WithPlayerMove(nextCanonicalPos);

              if (!visited.Contains(nextCanonical)) {
                var nextWalkableCopy = ListPool<Vector2Int>.Get();
                nextWalkableCopy.Capacity = nextWalkable.Count;
                nextWalkableCopy.AddRange(nextWalkable);

                queue.Enqueue(
                    new SolverContext() {
                        CanonicalState = nextCanonical,
                        RawState = nextRawState,
                        WalkableArea = nextWalkableCopy
                    },
                    GetHeuristic(nextCanonical));
                parentMap[nextCanonical] =
                    new PathNode { ParentState = currentState, Move = pushMove };
                visited.Add(nextCanonical);
                statesExplored++;
              }
            }
          }
        }
      }

      ListPool<Vector2Int>.Release(walkable);
    }

    UnityEngine.Debug.Log(
        $"Unsolvable - Checked {statesExplored} states in {timer.ElapsedMilliseconds}ms.");
    return null; // Unsolvable
  }

  /// <summary>
  /// Reconstructs the full solution (Walks + Pushes) from the map of Canonical States.
  /// </summary>
  private List<SokobanMove> ReconstructPath(
      Dictionary<SokobanState, PathNode> parentMap,
      SokobanState endState,
      SokobanState realStartState) {
    var pushSequence = new List<SokobanMove>();
    var current = endState;

    // 1. Extract the sequence of PUSH moves (backwards)
    while (parentMap.ContainsKey(current)) {
      var node = parentMap[current];
      if (node.ParentState == null || node.Move == null) break; // Reached canonical start

      pushSequence.Add(node.Move.Value);
      current = node.ParentState.Value;
    }

    pushSequence.Reverse();

    // 2. Fill in the WALK moves between pushes
    var fullPath = new List<SokobanMove>();
    var simState = realStartState; // Simulate strictly to ensure path validity

    foreach (var pushMove in pushSequence) {
      // The player is at simState.PlayerPos.
      // They need to walk to pushMove.playerFrom to perform the push.

      // Note: Pather.FindPath returns a list of coordinates.
      var walkPathCoords = Pather.FindPath(simState, simState.PlayerPos, pushMove.playerFrom);

      // Pather returns null if already there or unreachable (should be reachable by definition of our solver)
      if (walkPathCoords is { Count: > 0 }) {
        // Convert coords to atomic PlayerMoves
        var walkPos = simState.PlayerPos;
        foreach (var target in walkPathCoords) {
          fullPath.Add(SokobanMove.PlayerMove(walkPos, target));
          walkPos = target;
        }
      }

      // Now Add the Push
      fullPath.Add(pushMove);

      // Update simulation state
      simState = MoveRules.ApplyMove(simState, pushMove);
    }

    return fullPath;
  }

  // --- Helpers (Unchanged logic, just copied for context) ---

  private bool IsValidCratePush(SokobanState state, Vector2Int targetPos) {
    return state.CanReceiveCrate(targetPos.x, targetPos.y);
  }

  private bool IsDeadlock(SokobanState state, Vector2Int pos, int width, int height) {
    // If it's on a target, it's not a deadlock (usually)
    if (state.TerrainGrid[pos.x, pos.y].IsTarget()) return false;

    // Check axes (Horizontal and Vertical neighbors)
    // blocked if Wall or existing Crate (that isn't moving)
    bool blockedLeft = IsBlocking(state.TerrainGrid, pos.x - 1, pos.y, width, height);
    bool blockedRight = IsBlocking(state.TerrainGrid, pos.x + 1, pos.y, width, height);
    bool blockedUp = IsBlocking(state.TerrainGrid, pos.x, pos.y + 1, width, height);
    bool blockedDown = IsBlocking(state.TerrainGrid, pos.x, pos.y - 1, width, height);

    // Corner Deadlock: Blocked vertically AND horizontally
    // Top-Left, Top-Right, Bottom-Left, Bottom-Right
    if ((blockedLeft || blockedRight) && (blockedUp || blockedDown)) {
      return true;
    }

    return false;
  }

  private bool IsBlocking(TerrainType[,] grid, int x, int y, int width, int height) {
    // Check bounds
    if (x < 0 || x >= width || y < 0 || y >= height) return true; // Edge is a wall

    return grid[x, y].IsWall();
    // Note: Simple deadlocks focus on Walls.
    // Crates can be moved, so they aren't permanent deadlocks unless frozen.
  }

  private bool IsCrateInDeadSquare(Vector2Int pos) {
    return DeadSquareMap.IsDeadSquare(pos.x, pos.y);
  }
}
