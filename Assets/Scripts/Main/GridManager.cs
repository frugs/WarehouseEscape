using System.Collections;
using JetBrains.Annotations;
using UnityEngine;

public class GridManager : MonoBehaviour {
  [Header("Level Settings")]
  [SerializeField]
  private int LevelNumber = 1;

  [Header("References")]
  [SerializeField] private LevelLoader LevelLoader;
  [SerializeField] private MenuManager menuManager;

  [Header("Animation Timing")]
  [SerializeField]
  private readonly float MoveAnimationDuration = 0.2f; // Snappy movement

  [SerializeField] private readonly float FallAnimationDuration = 0.15f;

  // ================= STATE =================
  private SokobanState CurrentState;
  private GameObject[,] VisualGrid; // Visual representation only
  private int GridWidth;
  private int GridHeight;

  public SokobanState GridState => CurrentState;

  [UsedImplicitly]
  private void Awake() {
    if (LevelLoader == null) LevelLoader = GetComponent<LevelLoader>();
    if (menuManager == null) menuManager = GetComponent<MenuManager>();
  }

  [UsedImplicitly]
  private void Start() {
    LoadLevel();
  }

  private void LoadLevel() {
    LevelLoader.CleanupLevel(VisualGrid);

    if (LevelLoader.LoadLevel(LevelNumber, out var initialState, out var visualGrid)) {
      CurrentState = initialState;
      VisualGrid = visualGrid;
    }

    menuManager.ResumeGame();
  }

  // ================= CORE UPDATE LOGIC =================

  /// <summary>
  /// Updates BOTH the Logic Data (Cells) and the Visual Array (GameObject references).
  /// Returns the GameObjects that need to be animated so the caller doesn't need to look them up.
  /// </summary>
  public void RegisterMoveUpdates(
      SokobanMove move,
      out GameObject playerObj,
      out GameObject crateObj) {
    playerObj = null;
    crateObj = null;

    // 1. Capture Objects (before we clear the grid cells)
    if (CurrentState.IsValidPos(move.playerFrom)) {
      playerObj = VisualGrid[move.playerFrom.x, move.playerFrom.y];
    }

    if (move.type == MoveType.CratePush && CurrentState.IsValidPos(move.crateFrom)) {
      crateObj = VisualGrid[move.crateFrom.x, move.crateFrom.y];
    }

    // 2. Update Data Model (The Truth)
    CurrentState = MoveRules.ApplyMove(CurrentState, move);

    // 3. Update Visual Grid Pointers (The References)
    VisualGrid[move.playerFrom.x, move.playerFrom.y] = null;
    if (move.type == MoveType.CratePush)
      VisualGrid[move.crateFrom.x, move.crateFrom.y] = null;

    if (playerObj != null)
      VisualGrid[move.playerTo.x, move.playerTo.y] = playerObj;

    if (crateObj != null) {
      // We keep tracking the visual object even if it "falls in a hole"
      // so we don't lose the reference until we destroy/change it.
      VisualGrid[move.crateTo.x, move.crateTo.y] = crateObj;
    }
  }

  // ================= PURE ANIMATION =================

  public IEnumerator AnimateTransform(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    Vector3 startPos = obj.transform.position;
    Vector3 endPos = GridUtils.GridToWorld(targetGridPos.x, targetGridPos.y, 0.5f);
    float elapsed = 0f;

    while (elapsed < MoveAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / MoveAnimationDuration;
      // Quadratic ease-out for smoother feel
      t = t * (2 - t);
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;
  }

  public IEnumerator AnimateCrateFall(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    // 1. Slide to the hole position
    yield return AnimateTransform(obj, targetGridPos);

    // 2. Sink down
    Vector3 startPos = obj.transform.position;
    Vector3 endPos = startPos + Vector3.down * 1.0f; // Sink depth
    float elapsed = 0f;

    while (elapsed < FallAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / FallAnimationDuration;
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;

    // Optional: Rename for debugging
    obj.name = $"FilledHole_{targetGridPos.x}_{targetGridPos.y}";
  }

  // ================= UTILS =================

  public void CheckWinCondition() {
    if (CurrentState.IsWin()) {
      Debug.Log("Level Complete!");
      if (menuManager) {
        menuManager.WinGame();
      }
    }
  }

  [UsedImplicitly]
  public void ResetLevel() => LoadLevel();

  [UsedImplicitly]
  public void NextLevel() {
    LevelNumber++;
    LoadLevel();
  }
}
