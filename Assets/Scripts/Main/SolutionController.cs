using System.Collections;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(GridManager))]
public class SolutionController : MonoBehaviour {
  [Header("Settings")]
  [Tooltip("Name of the level solution to play (without .json extension)")]
  [SerializeField] private string levelName = "Level1";

  [Tooltip("Time to wait between each move")]
  [SerializeField] private float stepDelay = 0.2f;

  [Tooltip("If true, starts playing automatically when the scene starts")]
  [SerializeField] private bool autoPlay = false;

  private bool isPlaying;

  private GridManager gridManager;
  private Coroutine playbackCoroutine;

  private void Awake() {
    gridManager = GetComponent<GridManager>();
  }

  private void Start() {
    if (autoPlay) {
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
    isPlaying = true;
    if (playbackCoroutine != null) {
      StopCoroutine(playbackCoroutine);
    }
    playbackCoroutine = StartCoroutine(PlaybackRoutine());
  }

  [ContextMenu("Stop Playback")]
  public void StopPlayback() {
    if (playbackCoroutine != null) {
      StopCoroutine(playbackCoroutine);
      playbackCoroutine = null;
    }
  }

  private IEnumerator PlaybackRoutine() {
    // 1. Locate File
    string fileName = $"{levelName}_Solution.json";
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
    gridManager.ResetLevel();
    yield return new WaitForSeconds(0.5f); // Wait for reset animation/logic

    // 4. Execute Moves
    foreach (SokobanMove move in data.Moves) {
      // Execute Logic & Get Visuals
      gridManager.RegisterMoveUpdates(move, out GameObject playerObj, out GameObject crateObj);

      // Animate Player
      if (playerObj != null) {
        StartCoroutine(gridManager.AnimateTransform(playerObj, move.playerTo));
      }

      // Animate Crate (Push)
      if (move.type == MoveType.CratePush && crateObj != null) {
        // Note: If you want the "Fall in Hole" visual logic, verify GridManager handles it
        // or replicate the check here. Assuming simple translation for now:
        StartCoroutine(gridManager.AnimateTransform(crateObj, move.crateTo));
      }

      // Wait for step duration
      yield return new WaitForSeconds(stepDelay);

      // Check if this move caused a win
      gridManager.CheckWinCondition();
    }

    Debug.Log("[SolutionController] Playback Complete.");

    // Return control (optional, usually you want to leave it disabled if they won)
    // if (playerController) playerController.enabled = true;

    playbackCoroutine = null;
    isPlaying = false;
  }
}
