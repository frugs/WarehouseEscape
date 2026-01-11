using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Orchestrates player input handling by coordinating between directional and pointer input handlers.
/// Manages input priority, handler cleanup, and input enable/disable lifecycle.
/// Also manages undo functionality via the UndoManager.
/// </summary>
public class PlayerController : MonoBehaviour {
  [field: Header("Dependencies")]
  [field: SerializeField]
  [UsedImplicitly]
  private GameSession GameSession { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private MoveScheduler MoveScheduler { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private UndoBehaviour UndoBehaviour { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private PushIndicatorManager PushIndicatorManager { get; set; }

  [field: Header("Input Handler Prefabs")]
  [field: SerializeField]
  [UsedImplicitly]
  private GameObject WalkIndicatorPrefab { get; set; }

  private GameInput _inputActions;
  private DirectionalPlayerInputHandler _directionalHandler;
  private WalkableAreaCache _walkableAreaCache;
  private PointerPlayerInputHandler _pointerHandler;
  private UndoManager _undoManager;

  [UsedImplicitly]
  private void Awake() {
    if (GameSession == null) GameSession = GetComponent<GameSession>();
    if (MoveScheduler == null) MoveScheduler = GetComponent<MoveScheduler>();
    if (UndoBehaviour == null) UndoBehaviour = GetComponent<UndoBehaviour>();

    PushIndicatorManager = GetComponent<PushIndicatorManager>();

    _inputActions = new GameInput();

    // Initialize input handlers
    _directionalHandler = new DirectionalPlayerInputHandler(
        _inputActions,
        GameSession,
        MoveScheduler);

    _walkableAreaCache = new WalkableAreaCache(GameSession);

    _pointerHandler = new PointerPlayerInputHandler(
        _inputActions,
        GameSession,
        MoveScheduler,
        PushIndicatorManager,
        _walkableAreaCache,
        WalkIndicatorPrefab,
        Camera.main);

    if (UndoBehaviour != null) {
      _undoManager = UndoBehaviour.UndoManager;
    }
  }

  [UsedImplicitly]
  private void OnDestroy() {
    _walkableAreaCache?.Dispose();
    _walkableAreaCache = null;
  }

  [UsedImplicitly]
  private void OnEnable() {
    _inputActions.Player.Enable();
  }

  [UsedImplicitly]
  private void OnDisable() {
    _inputActions.Player.Disable();
    _pointerHandler.ResetToIdle();
  }

  [UsedImplicitly]
  private void Update() {
    if (HandleUndoInput()) {
      return;
    }

    if (HandleRestartInput()) {
      return;
    }

    // Directional input takes priority
    if (_directionalHandler.TryExecute()) {
      // When directional input executes, clean up pointer state to prevent stale UI
      _pointerHandler.ResetToIdle();
      return;
    }

    // Then handle pointer input
    _pointerHandler.TryExecute();
  }

  /// <summary>
  /// Handles undo input (Ctrl+Z or Cmd+Z).
  /// Records the move that's being undone so we can animate the reversal.
  /// </summary>
  /// <returns>True if undo was triggered, false otherwise.</returns>
  private bool HandleUndoInput() {
    if (_undoManager == null) return false;

    bool isUndoPressed = _inputActions.Player.Undo.WasPressedThisFrame();

    if (!isUndoPressed) {
      return false;
    }

    if (!_undoManager.CanUndo) {
      Debug.Log("[PlayerController] No moves to undo");
      return false;
    }

    // Reset pointer state
    _pointerHandler.ResetToIdle();
    MoveScheduler.ClearInterrupt();

    // Get the before-state from UndoManager
    SokobanState beforeState = _undoManager.Undo(GameSession.CurrentState, out var moveToReverse);

    // Apply the before-state to GameSession
    GameSession.RestoreState(beforeState);

    Debug.Log($"[PlayerController] Undid move: {moveToReverse}");
    return true;
  }

  /// <summary>
  /// Handles restart input (R key) to reset the current level.
  /// </summary>
  /// <returns>True if restart was triggered, false otherwise.</returns>
  private bool HandleRestartInput() {
    if (_inputActions.Player.Restart.WasPerformedThisFrame()) {
      _pointerHandler.ResetToIdle();
      MoveScheduler.ClearInterrupt();
      GameSession.ResetLevel();
      return true;
    }

    return false;
  }
}
