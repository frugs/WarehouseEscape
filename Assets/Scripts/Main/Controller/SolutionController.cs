using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

[RequireComponent(typeof(GameSession))]
public class SolutionController : MonoBehaviour {
  [Header("Settings")]
  [Tooltip("Name of the level solution to play (without .json extension)")]
  [SerializeField]
  private string LevelName = "Level1";

  [Tooltip("Time to wait between each move")] [SerializeField]
  private float StepDelay = 0.12f;

  [Tooltip("If true, starts playing automatically when the scene starts")] [SerializeField]
  private bool AutoPlay = false;

  [SerializeField] private GameSession GameSession;

  [SerializeField] private MoveScheduler MoveScheduler;

  private Coroutine PlaybackCoroutine;

  [UsedImplicitly]
  private void Awake() {
    GameSession = GetComponent<GameSession>();
    MoveScheduler = GetComponent<MoveScheduler>();
  }

  [UsedImplicitly]
  private void Start() {
    if (AutoPlay) {
      // Wait one frame to ensure level is loaded
      StartCoroutine(WaitAndPlay());
    }
  }

  private IEnumerator WaitAndPlay() {
    yield return null;
    PlaySolution();
  }

  [ContextMenu("Play Solution")]
  public void PlaySolution() {
    if (PlaybackCoroutine != null) {
      StopCoroutine(PlaybackCoroutine);
    }

    PlaybackCoroutine = StartCoroutine(PlaybackRoutine());
  }

  [ContextMenu("Stop Playback")]
  public void StopPlayback() {
    if (PlaybackCoroutine != null) {
      StopCoroutine(PlaybackCoroutine);
      PlaybackCoroutine = null;
    }
  }

  private IEnumerator PlaybackRoutine() {
    // 1. Locate File
    string fileName = $"{LevelName}_Solution.json";
    string path = Path.Combine(Application.streamingAssetsPath, "Solutions", fileName);

    if (!File.Exists(path)) {
      Debug.LogError($"[SolutionController] Solution file not found: {path}\n" +
                     "Make sure to run the Solver from the Test Runner or Editor Menu first.");
      yield break;
    }

    // 2. Deserialize
    string json = File.ReadAllText(path);
    SolutionData data = JsonUtility.FromJson<SolutionData>(json);

    if (data == null || data.Moves == null || data.Moves.Count == 0) {
      Debug.LogError("[SolutionController] Solution data is empty or invalid.");
      yield break;
    }

    Debug.Log(
      $"[SolutionController] Playing solution for '{data.LevelName}' ({data.StepCount} moves)...");

    // 3. Prepare Game State
    // Disable player input so they don't interfere

    // Optional: Reset level to ensure we start from the beginning
    GameSession.ResetLevel();
    yield return new WaitForSeconds(0.5f); // Wait for reset animation/logic

    // 4. Schedule Moves
    PushMovesToScheduler(data.Moves);
  }

  private void PushMovesToScheduler(List<SokobanMove> moves) {
    // Clear any player inputs or partial paths
    MoveScheduler.Clear();

    // Set the cinematic playback speed
    MoveScheduler.StepDelay = StepDelay;

    // Queue the entire solution
    MoveScheduler.Enqueue(moves);
  }
}
