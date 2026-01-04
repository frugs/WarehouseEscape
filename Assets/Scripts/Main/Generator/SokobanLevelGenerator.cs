using System.Diagnostics;
using UnityEngine;

public class SokobanLevelGenerator {
  private const int AttemptsPerLevel = 5000;
  private const int GeneratorSolverLimit = 1500;
  private readonly LevelLayoutGenerator _roomLayoutGenerator = new();
  private readonly LevelFeaturePlacer _roomFeaturePlacer = new();

  /// <summary>
  /// Generates a valid, solvable Sokoban level.
  /// </summary>
  /// <returns>A fully constructed SokobanState, or null if generation failed.</returns>
  public SokobanState? GenerateLevel(
      int minSize = 6,
      int maxSize = 12,
      int targetCount = 3,
      int holeCount = 2,
      bool useEntranceExit = true,
      int? seed = null) {
    const int TimeoutMs = 60_000;
    Stopwatch timer = Stopwatch.StartNew();

    if (seed.HasValue) {
      Random.InitState(seed.Value);
      UnityEngine.Debug.Log($"Generator seeded with: {seed.Value}");
    }

    for (int i = 0; i < AttemptsPerLevel; i++) {
      var attemptStart = timer.ElapsedMilliseconds;

      if (timer.ElapsedMilliseconds > TimeoutMs) {
        UnityEngine.Debug.LogError(
            $"Timeout! {i} attempts in {timer.ElapsedMilliseconds}ms.");
        return null; // Give up
      }

      // 1. Create Room Structure
      int maxWidth = Random.Range(minSize, maxSize);
      int maxHeight = Random.Range(minSize, maxSize);
      var roomLayout = _roomLayoutGenerator.GenerateLayout(maxWidth, maxHeight);

      UnityEngine.Debug.Log($"Generated layout in {timer.ElapsedMilliseconds - attemptStart}ms");

      var placeFeaturesStart = timer.ElapsedMilliseconds;

      // 2. Populate (Player, crates, Goals)
      var maybeState = _roomFeaturePlacer.PlaceFeatures(
          roomLayout,
          targetCount,
          holeCount,
          useEntranceExit);

      UnityEngine.Debug.Log(
          $"Placed features in {timer.ElapsedMilliseconds - placeFeaturesStart}ms");

      if (maybeState == null) continue; // Population failed (no space)

      // 3. Verify Solvability
      var solver = new SokobanSolver();
      var state = (SokobanState)maybeState;

      if (solver.IsSolvable(state, GeneratorSolverLimit)) {
        UnityEngine.Debug.Log(
            $"Generated solvable level in {i + 1} attempts and {timer.ElapsedMilliseconds}ms.");

        // Final Polish Step
        PostProcessPerimeterWalls(state.TerrainGrid);

        return state;
      }
    }

    UnityEngine.Debug.LogError("Failed to generate a solvable level within attempt limit.");
    return null;
  }

  private void PostProcessPerimeterWalls(TerrainType[,] grid) {
    int w = grid.GetLength(0);
    int h = grid.GetLength(1);

    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        // Check if on the outermost edge
        bool isEdge = (x == 0 || x == w - 1 || y == 0 || y == h - 1);

        if (isEdge) {
          // Force edge to be Wall (unless it is Entrance/Exit)
          // Note: Entrance/Exit are critical, do not overwrite them with Wall.
          if (grid[x, y] != TerrainType.Entrance && grid[x, y] != TerrainType.Exit) {
            grid[x, y] = TerrainType.Wall;
          }
        } else {
          // Inner tile: If it is currently a Wall, convert to FakeHole.
          if (grid[x, y] == TerrainType.Wall) {
            grid[x, y] = TerrainType.FakeHole;
          }
        }
      }
    }
  }
}
