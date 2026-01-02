using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SokobanSolver {
  private const int MAX_ITERATIONS = 10_000_000; // Limit total states explored
  private const long MAX_MS = 60_000; // 60s timeout

  private struct PathNode {
    public SokobanState? ParentState;
    public SokobanMove? Move;
  }

  private DeadSquareMap DeadSquareMap { get; set; }

  /// <summary>
  /// Checks if a level is solvable within the given iteration limit.
  /// </summary>
  public bool IsSolvable(SokobanState state, int maxIterations = MAX_ITERATIONS) {
    var solution = FindSolutionPath(state, maxIterations);
    return solution != null;
  }

  /// <summary>
  /// Finds the shortest solution path using BFS with Canonical State Optimization.
  /// This treats all reachable player positions as a single state.
  /// </summary>
  public List<SokobanMove> FindSolutionPath(
      SokobanState initialState,
      int maxIterations = MAX_ITERATIONS) {
    // 1. Setup
    var parentMap = new Dictionary<SokobanState, PathNode>();
    var visited = new HashSet<SokobanState>();
    var queue = new Queue<SokobanState>();
    var width = initialState.GridWidth;
    var height = initialState.GridHeight;
    int statesExplored = 0;

    var walkableAreaScannerOuter = new WalkableAreaScanner();
    var walkableAreaScannerInner = new WalkableAreaScanner();

    // 2. Canonicalize Start State
    // We convert the initial raw state into a canonical state (player at top-left-most reachable pos)
    // This ensures our visited set works correctly immediately.
    _ = walkableAreaScannerOuter.GetWalkableAreaNoCopy(initialState, out var startCanonicalPos);
    var canonicalStart = new SokobanState(
        initialState.TerrainGrid,
        startCanonicalPos,
        initialState.CratePositions,
        initialState.FilledHoles);

    queue.Enqueue(canonicalStart);
    visited.Add(canonicalStart);

    // Note: The parentMap stores the path of CANONICAL states.
    // We treat the canonicalStart as the root (Parent = null).
    parentMap[canonicalStart] = new PathNode { ParentState = null, Move = null };

    int iterations = 0;
    Stopwatch timer = Stopwatch.StartNew();
    DeadSquareMap = new DeadSquareMap(initialState);

    // 3. BFS Loop
    while (queue.Count > 0) {
      if (++iterations > maxIterations || timer.ElapsedMilliseconds > MAX_MS) {
        UnityEngine.Debug.LogError(
            $"Solver Timeout! Checked {statesExplored} states in {timer.ElapsedMilliseconds}ms.");
        return null; // Give up
      }

      var currentState = queue.Dequeue();

      // Check Win on the canonical state (crates are the same)
      if (currentState.IsWin()) {
        UnityEngine.Debug.Log(
            $"Solved - Checked {statesExplored} states in {timer.ElapsedMilliseconds}ms.");
        return ReconstructPath(parentMap, currentState, initialState);
      }

      // 4. Generate Moves (Pushes Only)
      // We need to re-calculate reachable squares for the current canonical state
      // to find where we can push from.
      // (Optimization note: We could cache this in a wrapper class to avoid re-flood-filling)
      var reachable = walkableAreaScannerOuter.GetWalkableAreaNoCopy(currentState, out _);
      foreach (var standPos in reachable) {
        // Check all 4 directions for potential pushes
        foreach (var dir in Vector2IntExtensions.Cardinals) {
          var cratePos = standPos + dir;

          // Is there a crate here?
          if (currentState.IsCrateAt(cratePos.x, cratePos.y)) {
            var pushTo = cratePos + dir;

            // Can we push it? (Target cell must be valid and not a deadlock)
            if (IsValidCratePush(currentState, pushTo) &&
                !IsDeadlock(currentState, pushTo, width, height) &&
                !IsCrateInDeadSquare(pushTo)) {
              // Construct the Push Move
              var pushMove = SokobanMove.CratePush(standPos, cratePos, cratePos, pushTo);

              // Apply the push to get the 'raw' next state
              var nextRawState = MoveRules.ApplyMove(currentState, pushMove);

              // 5. Canonicalize the Next State
              // After pushing, the player is at 'cratePos'. We flood fill from there
              // to find the new canonical player position.
              _ = walkableAreaScannerInner.GetWalkableAreaNoCopy(
                  nextRawState,
                  out var nextCanonicalPos);
              var nextCanonical = new SokobanState(
                  nextRawState.TerrainGrid,
                  nextCanonicalPos,
                  nextRawState.CratePositions,
                  nextRawState.FilledHoles);

              if (!visited.Contains(nextCanonical)) {
                queue.Enqueue(nextCanonical);
                parentMap[nextCanonical] =
                    new PathNode { ParentState = currentState, Move = pushMove };
                visited.Add(nextCanonical);
                statesExplored++;
              }
            }
          }
        }
      }
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
      if (walkPathCoords != null && walkPathCoords.Count > 0) {
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
