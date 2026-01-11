using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class LevelGeneratorWindow : EditorWindow {
  private int MaxSize { get; set; } = 40;
  private int TargetCount { get; set; } = 5;
  private int HoleCount { get; set; } = 2;
  private bool UseEntranceExit { get; set; } = true;
  private string LevelName { get; set; } = "GeneratedLevel";
  private bool MultiThreading { get; set; } = true;
  private bool UseFixedSeed { get; set; }
  private int Seed { get; set; } = 12345;

  private bool _isGenerating;

  [MenuItem("Sokoban/Open Generator")]
  public static void ShowWindow() {
    GetWindow<LevelGeneratorWindow>("Sokoban Generator");
  }

  [UsedImplicitly]
  private void OnGUI() {
    LevelName = EditorGUILayout.TextField("Level Name", LevelName);

    GUILayout.Label("Level Settings", EditorStyles.boldLabel);
    MaxSize = EditorGUILayout.IntField("Max Size", MaxSize);
    TargetCount = EditorGUILayout.IntField("Target Count", TargetCount);
    HoleCount = EditorGUILayout.IntField("Hole Count", HoleCount);
    UseEntranceExit = EditorGUILayout.Toggle("Add Entrance/Exit", UseEntranceExit);

    EditorGUILayout.Space();
    GUILayout.Label("Debug Settings", EditorStyles.boldLabel);

    MultiThreading = EditorGUILayout.Toggle("Use multi-threaded generation", MultiThreading);

    EditorGUILayout.BeginHorizontal();
    UseFixedSeed = EditorGUILayout.Toggle("Use Fixed Seed", UseFixedSeed);
    if (UseFixedSeed) {
      if (GUILayout.Button("Pick Random", GUILayout.Width(100))) {
        Seed = Random.Range(0, 999999999);
        // Defocus controls to update the UI immediately if the field was focused
        GUI.FocusControl(null);
      }

      Seed = EditorGUILayout.IntField(Seed);
    }

    EditorGUILayout.EndHorizontal();

    EditorGUILayout.Space();

    EditorGUI.BeginDisabledGroup(_isGenerating);

    if (GUILayout.Button("Generate & Print to Log")) {
      GenerateAndLog();
    }

    if (GUILayout.Button("Generate & Save to File")) {
      GenerateAndSave();
    }

    EditorGUI.EndDisabledGroup();

    // Spinner
    if (_isGenerating) {
      EditorGUILayout.Space();

      DrawSpinner();
    }
  }

  private async Task<SokobanState?> GenerateState() {
    try {
      _isGenerating = true;

      int seedToUse = UseFixedSeed ? Seed : Random.Range(0, 999999999);
      const int seedOffset = 1123;

      if (MultiThreading) {
        int threadCount = 2; // Testing showed 2 threads optimal

        var (result, _, _) = await AsyncLevelGenerator.GenerateLevelAsync(
            MaxSize,
            MaxSize,
            TargetCount,
            HoleCount,
            UseEntranceExit,
            seedToUse,
            seedOffset,
            threadCount,
            waitForFullCompletion: false);

        return result;
      } else {
        var generator = new SokobanLevelGenerator();

        var result = generator.GenerateLevel(
            out var solution,
            out _,
            out var statesExplored,
            MaxSize,
            MaxSize,
            TargetCount,
            HoleCount,
            UseEntranceExit,
            seedToUse);

        Debug.Log($"Solver explored {statesExplored} total states");

        if (result != null) {
          Debug.Log($"Generated level difficulty: {solution?.Difficulty}");
        }

        return result;
      }
    } finally {
      _isGenerating = false;
    }
  }

  private async void GenerateAndLog() {
    try {
      var maybeState = await GenerateState();
      if (maybeState == null) return;

      var state = (SokobanState)maybeState;
      var stringWriter = new StringWriter();
      WriteOutState(state, stringWriter);

      Debug.Log(stringWriter.ToString());
    } catch (Exception e) {
      Debug.LogError(e);
    }
  }

  private async void GenerateAndSave() {
    try {
      var maybeState = await GenerateState();
      if (maybeState == null) return;

      var state = (SokobanState)maybeState;

      string folder = Path.Combine(Application.dataPath, "Levels");
      if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

      string path = Path.Combine(folder, LevelName + ".txt");

      await using (StreamWriter writer = new StreamWriter(path)) {
        WriteOutState(state, writer);
      }

      AssetDatabase.Refresh();
      Debug.Log($"Saved level to {path}");
    } catch (Exception e) {
      Debug.LogError(e);
    }
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

    if (t == TerrainType.Entrance) return '>';
    if (t == TerrainType.Exit) return '<';

    if (t == TerrainType.Wall) return '#';
    if (t == TerrainType.Hole) return 'H';
    if (t == TerrainType.FakeHole) return 'h';
    if (t == TerrainType.Target) {
      if (isPlayer) return 'p';
      if (isCrate) return 'b';
      return 'T'; // Standard Goal
    }

    if (isPlayer) return 'P';
    if (isCrate) return 'B';
    return '.'; // Empty Floor
  }

  private void DrawSpinner() {
    // 1. Calculate the frame index (0 to 11) based on time
    // Adjust '12.0f' to make it faster or slower
    int frame = (int)(EditorApplication.timeSinceStartup * 12.0f) % 12;

    // 2. Get the internal Unity icon for this frame
    // "WaitSpin00" through "WaitSpin11" are standard editor assets
    string iconName = "WaitSpin" + frame.ToString("00");
    Texture2D icon = EditorGUIUtility.FindTexture(iconName);

    // 3. Center the spinner in the layout
    GUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace(); // Push to center

    if (icon != null) {
      // Draw the texture with a specific size (e.g., 24x24)
      GUILayout.Label(icon, GUILayout.Width(24), GUILayout.Height(24));
    }

    // Optional: Add text next to it
    GUILayout.Label(" Generating Level...", EditorStyles.boldLabel);

    GUILayout.FlexibleSpace(); // Push to center
    GUILayout.EndHorizontal();

    // CRITICAL: Force the window to redraw immediately so the animation plays
    // Without this, it will only update when you move the mouse
    Repaint();
  }
}
