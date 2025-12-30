using System.Collections.Generic;
using System.Diagnostics; // For Stopwatch
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LevelSolverMenu {
  [MenuItem("Assets/Sokoban/Solve Level", false, 10)]
  private static void SolveSelectedLevel() {
    // 1. Get the selected file path
    string path = AssetDatabase.GetAssetPath(Selection.activeObject);
    if (string.IsNullOrEmpty(path)) return;

    UnityEngine.Debug.Log($"<color=orange>Solving Level: {Path.GetFileName(path)}...</color>");

    // 2. Parse the level (Uses your existing LevelParser)
    LevelData rawData = LevelParser.ParseLevelFile(path);

    if (rawData == null) {
      UnityEngine.Debug.LogError("Failed to parse level file.");
      return;
    }

    // 3. Convert to Solver State
    SokobanState startState = new SokobanState(rawData.grid, rawData.playerPos, rawData.crates);

    // 4. Run Solver
    Stopwatch sw = Stopwatch.StartNew();
    SokobanSolver solver = new SokobanSolver();

    // Use a generous timeout for Editor operations (e.g., 10 seconds)
    // You might need to adjust your Solver's internal safety checks if they are hardcoded
    List<SokobanMove> solution = solver.FindSolutionPath(startState);

    sw.Stop();

    // 5. Handle Result
    if (solution != null) {
      UnityEngine.Debug.Log(
        $"<color=green>SOLVED!</color> Found solution " +
        $"({solution.Count} steps) in {sw.ElapsedMilliseconds}ms.");

      // Export using your new SolutionExporter
      string levelName = Path.GetFileNameWithoutExtension(path);
      SokobanSolutionExporter.Export(levelName, solution, sw.ElapsedMilliseconds);

      // Refresh Asset Database so the new .json file appears in Unity immediately
      AssetDatabase.Refresh();
    } else {
      UnityEngine.Debug.LogError(
        $"<color=red>UNSOLVABLE</color> or Timed Out (Time: {sw.ElapsedMilliseconds}ms).");
    }
  }

  // Only show this menu item if a text file is selected
  [MenuItem("Assets/Sokoban/Solve Level", true)]
  private static bool ValidateSolveSelectedLevel() {
    string path = AssetDatabase.GetAssetPath(Selection.activeObject);
    return !string.IsNullOrEmpty(path) && path.EndsWith(".txt");
  }
}
