using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Orchestrates player input handling by coordinating between directional and pointer input handlers.
/// Manages input priority, handler cleanup, and input enable/disable lifecycle.
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
  private PushIndicatorManager PushIndicatorManager { get; set; }

  [field: Header("Input Handler Prefabs")]
  [field: SerializeField]
  [UsedImplicitly]
  private GameObject WalkIndicatorPrefab { get; set; }

  private GameInput _inputActions;
  private DirectionalPlayerInputHandler _directionalHandler;
  private PointerPlayerInputHandler _pointerHandler;

  [UsedImplicitly]
  private void Awake() {
    GameSession = GetComponent<GameSession>();
    MoveScheduler = GetComponent<MoveScheduler>();
    PushIndicatorManager = GetComponent<PushIndicatorManager>();

    _inputActions = new GameInput();

    // Initialize input handlers
    _directionalHandler = new DirectionalPlayerInputHandler(
        _inputActions,
        GameSession,
        MoveScheduler);

    _pointerHandler = new PointerPlayerInputHandler(
        _inputActions,
        GameSession,
        MoveScheduler,
        PushIndicatorManager,
        WalkIndicatorPrefab,
        Camera.main);
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
