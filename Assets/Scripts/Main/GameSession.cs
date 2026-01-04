using JetBrains.Annotations;
using UnityEngine;

public class GameSession : MonoBehaviour {
  [Header("Level Settings")]
  [field: SerializeField]
  private int LevelNumber { get; set; } = 1;

  [Header("References")]
  [field: SerializeField]
  private LevelLoader LevelLoader { get; set; }

  [Header("References")]
  [field: SerializeField]
  private SolutionController SolutionController { get; set; }

  [field: SerializeField] private MenuManager MenuManager { get; set; }

  // ================= STATE =================
  private GameObject[,] VisualGrid;

  public SokobanState CurrentState { get; private set; }

  [UsedImplicitly]
  private void Awake() {
    if (LevelLoader == null) LevelLoader = GetComponent<LevelLoader>();
    if (SolutionController == null) SolutionController = GetComponent<SolutionController>();
    if (MenuManager == null) MenuManager = GetComponent<MenuManager>();
  }

  [UsedImplicitly]
  private void Start() {
    LoadLevel();
  }

  private void LoadLevel() {
    LevelLoader.CleanupLevel(VisualGrid);

    if (LevelLoader.LoadLevel(
            LevelNumber,
            out var initialState,
            out var visualGrid,
            out var levelName)) {
      CurrentState = initialState;
      VisualGrid = visualGrid;
      SolutionController.LevelName = levelName;
    }

    MenuManager.ResumeGame();
  }

  // ================= CORE UPDATE LOGIC =================

  /// <summary>
  /// Updates BOTH the Logic Data (Cells) and the Visual Array (GameObject references).
  /// Returns the GameObjects that need to be animated so the caller doesn't need to look them up.
  /// </summary>
  public void ApplyMoveToCurrentState(
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

  public void CheckWinCondition() {
    if (CurrentState.IsWin()) {
      Debug.Log("Level Complete!");
      if (MenuManager) {
        MenuManager.WinGame();
      }
    }
  }

  public void ResetLevel() => LoadLevel();

  public void NextLevel() {
    LevelNumber++;
    LoadLevel();
  }
}
