using System;
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
  private GameObject[,] _visualGrid;
  private GameObject _entrance;
  private GameObject _exit;
  private SokobanState _currentState;

  public SokobanState CurrentState => _currentState;

  public event Action StateChanged;

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
    LevelLoader.CleanupLevel(_visualGrid, _entrance, _exit);

    if (LevelLoader.LoadLevel(
            LevelNumber,
            out var initialState,
            out var visualGrid,
            out var entrance,
            out var exit,
            out var levelName)) {
      _currentState = initialState;
      _visualGrid = visualGrid;
      _entrance = entrance;
      _exit = exit;
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

    // Capture Objects (before we clear the grid cells)
    if (_currentState.IsValidPos(move.playerFrom)) {
      playerObj = _visualGrid[move.playerFrom.x, move.playerFrom.y];
    }

    if (move.type == MoveType.CratePush && _currentState.IsValidPos(move.crateFrom)) {
      crateObj = _visualGrid[move.crateFrom.x, move.crateFrom.y];
    }

    // Update Data Model (The Truth)
    _currentState = MoveRules.ApplyMove(_currentState, move);

    // Update Visual Grid Pointers (The References)
    _visualGrid[move.playerFrom.x, move.playerFrom.y] = null;
    if (move.type == MoveType.CratePush) {
      _visualGrid[move.crateFrom.x, move.crateFrom.y] = null;
      _visualGrid[move.crateTo.x, move.crateTo.y] = crateObj;
    }

    _visualGrid[move.playerTo.x, move.playerTo.y] = playerObj;

    // Run callbacks
    if (StateChanged != null) {
      StateChanged();
    }
  }

  public void CheckWinCondition() {
    if (_currentState.IsWin()) {
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
