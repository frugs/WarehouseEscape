using System.Diagnostics;
using System.Threading;
using Random = System.Random;

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
      out SokobanSolution solution,
      out int attempts,
      out int totalStatesExplored,
      int minSize = 6,
      int maxSize = 12,
      int targetCount = 5,
      int holeCount = 2,
      bool useEntranceExit = true,
      int? seed = null,
      CancellationToken cancellation = default,
      bool logVerbose = false) {
    solution = null;
    attempts = 1;
    totalStatesExplored = 0;
    const int TimeoutMs = 60_000;
    Stopwatch timer = Stopwatch.StartNew();

    var random = seed.HasValue ? new Random(seed.Value) : new Random();
    if (seed.HasValue) {
      UnityEngine.Debug.Log($"Generator seeded with: {seed.Value}");
    }

    for (int i = 0; i < AttemptsPerLevel; i++, attempts++) {
      if (cancellation.IsCancellationRequested) {
        return null;
      }

      var attemptStart = timer.ElapsedMilliseconds;

      if (timer.ElapsedMilliseconds > TimeoutMs) {
        if (logVerbose) {
          UnityEngine.Debug.LogError(
              $"Timeout! {i} attempts in {timer.ElapsedMilliseconds}ms.");
        }

        return null; // Give up
      }

      // 1. Create Room Structure
      int maxWidth = random.Next(minSize, maxSize);
      int maxHeight = random.Next(minSize, maxSize);
      var roomLayout = _roomLayoutGenerator.GenerateLayout(maxWidth, maxHeight, random);

      if (logVerbose) {
        UnityEngine.Debug.Log($"Generated layout in {timer.ElapsedMilliseconds - attemptStart}ms");
      }

      var placeFeaturesStart = timer.ElapsedMilliseconds;

      // 2. Populate (Player, crates, Goals)
      var maybeState = _roomFeaturePlacer.PlaceFeatures(
          roomLayout,
          targetCount,
          holeCount,
          useEntranceExit,
          random);

      if (logVerbose)
        UnityEngine.Debug.Log(
            $"Placed features in {timer.ElapsedMilliseconds - placeFeaturesStart}ms");

      if (maybeState == null) continue; // Population failed (no space)

      // 3. Verify Solvability
      var solver = new SokobanSolver();
      var state = (SokobanState)maybeState;
      var isSolvable = solver.IsSolvable(
          state,
          out solution,
          out var statesExplored,
          maxIterations: GeneratorSolverLimit,
          cancellation: cancellation);

      totalStatesExplored += statesExplored;

      if (isSolvable) {
        if (logVerbose) {
          UnityEngine.Debug.Log(
              $"Generated solvable level in {i + 1} attempts and {timer.ElapsedMilliseconds}ms.");
        }

        // Final Polish Step
        PostProcessPerimeterWalls(state.TerrainGrid);

        return state;
      }
    }

    if (logVerbose) {
      UnityEngine.Debug.LogError("Failed to generate a solvable level within attempt limit.");
    }

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
