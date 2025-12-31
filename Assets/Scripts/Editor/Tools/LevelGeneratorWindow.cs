using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class LevelGeneratorWindow : EditorWindow {
  private int MinSize = 30;
  private int MaxSize = 40;
  private int TargetCount = 3;
  private int HoleCount = 2;
  private string LevelName = "GeneratedLevel";

  [MenuItem("Sokoban/Open Generator")]
  public static void ShowWindow() {
    GetWindow<LevelGeneratorWindow>("Sokoban Generator");
  }

  private void OnGUI() {
    GUILayout.Label("Level Settings", EditorStyles.boldLabel);
    MinSize = EditorGUILayout.IntField("Min Size", MinSize);
    MaxSize = EditorGUILayout.IntField("Max Size", MaxSize);
    TargetCount = EditorGUILayout.IntField("Target Count", TargetCount);
    HoleCount = EditorGUILayout.IntField("Hole Count", HoleCount);
    LevelName = EditorGUILayout.TextField("Level Name", LevelName);

    EditorGUILayout.Space();

    if (GUILayout.Button("Generate & Print to Log")) {
      GenerateAndLog();
    }

    if (GUILayout.Button("Generate & Save to File")) {
      GenerateAndSave();
    }
  }

  private SokobanState? GenerateState() {
    var generator = new SokobanGenerator();
    return generator.Generate(MinSize, MaxSize, TargetCount, HoleCount);
  }

  private void GenerateAndLog() {
    var maybeState = GenerateState();
    if (maybeState == null) return;

    var state = (SokobanState)maybeState;
    var stringWriter = new StringWriter();
    WriteOutState(state, stringWriter);

    Debug.Log(stringWriter.ToString());
  }

  private void GenerateAndSave() {
    var maybeState = GenerateState();
    if (maybeState == null) return;

    var state = (SokobanState)maybeState;

    string folder = Path.Combine(Application.dataPath, "Levels");
    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

    string path = Path.Combine(folder, LevelName + ".txt");

    using (StreamWriter writer = new StreamWriter(path)) {
      WriteOutState(state, writer);
    }

    AssetDatabase.Refresh();
    Debug.Log($"Saved level to {path}");
  }

  private void WriteOutState(SokobanState state, TextWriter writer) {
    int w = state.TerrainGrid.GetLength(0);
    int h = state.TerrainGrid.GetLength(1);

    writer.WriteLine($"{w} {h}");

    // Note: Grid parsing usually reads row by row (y), but access is [x,y].
    // We must write Y lines (Top to Bottom or Bottom to Top? Standard is usually Top-Down visual)
    for (int y = h - 1; y >= 0; y--) {
      List<char> chars = new List<char>(w);
      for (int x = 0; x < w; x++) {
        chars.Add(GetCharForTile(state, x, y));
      }
      writer.WriteLine(string.Join(" ", chars));
    }
  }

  private char GetCharForTile(SokobanState state, int x, int y) {
    TerrainType t = state.TerrainGrid[x, y];
    bool isPlayer = state.IsPlayerAt(x, y);
    bool isCrate = state.IsCrateAt(x, y);

    if (t == TerrainType.Wall) return '#';
    if (t == TerrainType.Hole) return 'H';
    if (t == TerrainType.Target) {
      if (isPlayer) return 'p';
      if (isCrate) return 'b';
      return 'T'; // Standard Goal
    }

    if (isPlayer) return 'P';
    if (isCrate) return 'B';
    return '.'; // Empty Floor
  }
}
