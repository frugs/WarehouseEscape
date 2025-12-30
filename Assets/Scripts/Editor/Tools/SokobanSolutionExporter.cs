using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SokobanSolutionExporter {

  /// <summary>
  /// Saves a list of moves to a JSON file in StreamingAssets/Solutions.
  /// </summary>
  /// <param name="levelName">Name of the level (used for filename)</param>
  /// <param name="moves">The solution path</param>
  /// <param name="solveTimeMs">Optional: How long it took to solve</param>
  public static void Export(string levelName, List<SokobanMove> moves, long solveTimeMs = 0) {
    if (moves == null || moves.Count == 0) {
      Debug.LogWarning($"[SolutionExporter] No moves to export for {levelName}.");
      return;
    }

    SolutionData data = new SolutionData {
      LevelName = levelName,
      StepCount = moves.Count,
      SolveTimeMs = solveTimeMs,
      Moves = moves
    };

    // Format: StreamingAssets/Solutions/LevelName_Solution.json
    string folder = Path.Combine(Application.streamingAssetsPath, "Solutions");
    string fileName = $"{levelName}_Solution.json";
    string fullPath = Path.Combine(folder, fileName);

    try {
      if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

      string json = JsonUtility.ToJson(data, true);
      File.WriteAllText(fullPath, json);

      Debug.Log($"[SolutionExporter] Saved {moves.Count} moves to: {fullPath}");
    } catch (System.Exception e) {
      Debug.LogError($"[SolutionExporter] Failed to save solution: {e.Message}");
    }
  }
}
