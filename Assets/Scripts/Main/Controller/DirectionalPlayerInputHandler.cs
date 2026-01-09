using UnityEngine;

/// <summary>
/// Handles directional input (WASD/Arrow keys/Gamepad D-pad) for the player.
/// Converts directional input into immediate cardinal movement commands.
/// </summary>
public class DirectionalPlayerInputHandler {
  private readonly GameInput _inputActions;
  private readonly GameSession _gameSession;
  private readonly MoveScheduler _moveScheduler;

  public DirectionalPlayerInputHandler(
      GameInput inputActions,
      GameSession gameSession,
      MoveScheduler moveScheduler) {
    _inputActions = inputActions;
    _gameSession = gameSession;
    _moveScheduler = moveScheduler;
  }

  /// <summary>
  /// Attempts to read directional input and execute a move if a direction is pressed.
  /// </summary>
  /// <returns>True if directional input was detected and a move was scheduled, false otherwise.</returns>
  public bool TryExecute() {
    if (!_inputActions.Player.Move.WasPressedThisFrame()) return false;

    var direction = ReadDirectionalInput();

    if (direction != Vector2Int.zero) {
      if (MoveRules.TryBuildMove(_gameSession.CurrentState, direction, out var move)) {
        _moveScheduler.Clear();
        _moveScheduler.Enqueue(move);
        return true;
      }
    }

    return false;
  }

  /// <summary>
  /// Reads the current directional input from the input system.
  /// Supports WASD, arrow keys, gamepad D-pad, and analog sticks.
  /// </summary>
  /// <returns>
  /// A Vector2Int representing the direction:
  /// (1,0) = Right, (-1,0) = Left, (0,1) = Up, (0,-1) = Down, (0,0) = None
  /// </returns>
  private Vector2Int ReadDirectionalInput() {
    var moveValue = _inputActions.Player.Move.ReadValue<Vector2>();

    // Snap analog stick to cardinal directions
    // Only recognize "strong enough" inputs to avoid drift
    const float deadzone = 0.5f;

    float x = Mathf.Abs(moveValue.x) > deadzone ? Mathf.Sign(moveValue.x) : 0f;
    float y = Mathf.Abs(moveValue.y) > deadzone ? Mathf.Sign(moveValue.y) : 0f;

    // Prefer horizontal over vertical if both are pressed
    if (x != 0 && y != 0) {
      y = 0; // Only move horizontally
    }

    return new Vector2Int((int)x, (int)y);
  }
}
