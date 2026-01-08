using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles pointer/click/touch input for the player.
/// Supports:
/// - Walking to accessible tiles via pathfinding
/// - Clicking crates to show directional push indicators
/// - Hovering feedback to show reachable tiles
/// - Crate push execution when indicators are clicked
/// </summary>
public class PointerPlayerInputHandler {
  // State machine
  private enum InteractionState {
    Idle,
    ShowingCratePushIndicators
  }

  private readonly GameInput _inputActions;
  private readonly GameSession _gameSession;
  private readonly MoveScheduler _moveScheduler;
  private readonly PushIndicatorManager _pushIndicatorManager;
  private readonly WalkableAreaScanner _walkableAreaScanner;
  private readonly Camera _mainCamera;
  private readonly GameObject _walkIndicatorPrefab;

  private InteractionState _currentState = InteractionState.Idle;
  private Vector2Int _currentCratePos = Vector2Int.zero;
  private GameObject _walkIndicator;
  private Vector2? _pointerLastPos;

  // ===== CACHED WALKABLE AREA (BFS optimization) =====
  private IReadOnlyList<Vector2Int> _cachedWalkableArea;
  private SokobanState? _cachedWalkableAreaForState;

  public PointerPlayerInputHandler(
      GameInput inputActions,
      GameSession gameSession,
      MoveScheduler moveScheduler,
      PushIndicatorManager pushIndicatorManager,
      GameObject walkIndicatorPrefab,
      Camera mainCamera) {
    _inputActions = inputActions;
    _gameSession = gameSession;
    _moveScheduler = moveScheduler;
    _pushIndicatorManager = pushIndicatorManager;
    _walkIndicatorPrefab = walkIndicatorPrefab;
    _mainCamera = mainCamera;
    _walkableAreaScanner = new WalkableAreaScanner();
  }

  /// <summary>
  /// Attempts to process pointer input (hover updates and clicks).
  /// </summary>
  /// <returns>True if a move was executed, false otherwise.</returns>
  public bool TryExecute() {
    UpdatePointerFeedback();
    HandlePointerClick();
    return
        false; // Pointer input returns false - it doesn't "consume" the frame like directional does
  }

  /// <summary>
  /// Updates hover feedback and indicator hover state based on mouse position.
  /// </summary>
  private void UpdatePointerFeedback() {
    var mousePos = _inputActions.Player.MousePosition.ReadValue<Vector2>();

    // Only update hover feedback when mouse position actually changes
    if (_pointerLastPos == null || !_pointerLastPos.Value.EqualsWithThreshold(mousePos, 10f)) {
      Vector2Int? maybeGridPos = GetGridPosFromMousePos(mousePos);

      // Handle hover feedback when NOT showing indicators
      if (_currentState == InteractionState.Idle) {
        UpdateHoverFeedback(maybeGridPos);
      }

      _pointerLastPos = mousePos;

      // Handle hover over indicators (single raycast on mouse move, not every frame)
      if (_currentState == InteractionState.ShowingCratePushIndicators) {
        _pushIndicatorManager.UpdateHoverOnMouseMove(mousePos);
      }
    }
  }

  /// <summary>
  /// Handles click/tap input for either selecting a tile or choosing a push indicator.
  /// </summary>
  private void HandlePointerClick() {
    if (!_inputActions.Player.Click.WasPerformedThisFrame()) {
      return;
    }

    if (_gameSession.CurrentState.IsWin()) {
      return;
    }

    if (_currentState == InteractionState.ShowingCratePushIndicators) {
      // Check if player clicked on a hovered indicator
      var hoveredDir = _pushIndicatorManager.GetHoveredDirection();
      if (hoveredDir.HasValue) {
        ExecuteCratePush(hoveredDir.Value);
        _pushIndicatorManager.DismissAll();
        return;
      }

      // Player clicked elsewhere - dismiss indicators
      _pushIndicatorManager.DismissAll();
      _currentState = InteractionState.Idle;
      return;
    }

    // Normal mode: check what was clicked
    var mousePos = _inputActions.Player.MousePosition.ReadValue<Vector2>();
    Vector2Int? maybeGridPos = GetGridPosFromMousePos(mousePos);
    if (maybeGridPos.HasValue) {
      HandleTileClick(maybeGridPos.Value);
    }
  }

  /// <summary>
  /// Updates walk indicator visibility based on reachability without moving crates.
  /// Uses cached BFS results to avoid repeated pathfinding.
  /// </summary>
  private void UpdateHoverFeedback(Vector2Int? maybeGridPos) {
    if (!maybeGridPos.HasValue) {
      RemoveWalkIndicator();
      _pointerLastPos = null;
      return;
    }

    var gridPos = maybeGridPos.Value;

    // Check if tile is in cached walkable area (no crate movement needed)
    if (IsGridPosReachable(gridPos)) {
      // Create or update glowing floor indicator
      if (_walkIndicator == null) {
        _walkIndicator = Object.Instantiate(_walkIndicatorPrefab);
        var indicatorCollider = _walkIndicator.AddComponent<BoxCollider>();
        indicatorCollider.isTrigger = true;
      }

      _walkIndicator.transform.position = gridPos.GridToWorld();
      _walkIndicator.SetActive(true);
    } else {
      RemoveWalkIndicator();
    }
  }

  /// <summary>
  /// Checks if a grid position is reachable without moving crates.
  /// Uses cached BFS results.
  /// </summary>
  private bool IsGridPosReachable(Vector2Int gridPos) {
    var state = _gameSession.CurrentState;

    // Refresh cache if crate positions have changed
    if (_cachedWalkableArea == null || !IsCacheValid(state)) {
      RefreshWalkableAreaCache(state);
    }

    // Check if position is in cached walkable area
    if (_cachedWalkableArea != null) {
      foreach (var reachablePos in _cachedWalkableArea) {
        if (reachablePos == gridPos) {
          return true;
        }
      }
    }

    return false;
  }

  /// <summary>
  /// Validates if the cached walkable area is still valid.
  /// Invalid if crates have moved.
  /// </summary>
  private bool IsCacheValid(SokobanState currentState) {
    if (_cachedWalkableAreaForState == null) {
      return false;
    }

    // Cache is valid if crate positions haven't changed
    if (_cachedWalkableAreaForState?.CratePositions.Length != currentState.CratePositions.Length) {
      return false;
    }

    for (int i = 0; i < currentState.CratePositions.Length; i++) {
      if (_cachedWalkableAreaForState?.CratePositions[i] != currentState.CratePositions[i]) {
        return false;
      }
    }

    return true;
  }

  /// <summary>
  /// Performs BFS to find all reachable tiles (excluding crate-pushing).
  /// Results are cached until crates move.
  /// </summary>
  private void RefreshWalkableAreaCache(SokobanState state) {
    _cachedWalkableAreaForState = state;
    _cachedWalkableArea = _walkableAreaScanner.GetWalkableAreaNoCopy(state, out _);
  }

  /// <summary>
  /// Handles clicking on a tile - either a crate or a floor.
  /// </summary>
  private void HandleTileClick(Vector2Int tile) {
    var state = _gameSession.CurrentState;

    // Check if clicking on a crate
    if (state.IsCrateAt(tile.x, tile.y)) {
      ShowCrateIndicators(tile);
      return;
    }

    // Check if it's an accessible floor tile
    if (IsGridPosReachable(tile)) {
      List<Vector2Int> path = Pather.FindPath(state, state.PlayerPos, tile);

      if (path != null && path.Count > 0) {
        RemoveWalkIndicator();
        List<SokobanMove> moveList = ConvertPathToMoves(state.PlayerPos, path);

        if (moveList.Count > 0) {
          _moveScheduler.Clear();
          _moveScheduler.StepDelay = 0f;
          _moveScheduler.Enqueue(moveList);
          InvalidateWalkableAreaCache();
        }
      }
    }
  }

  /// <summary>
  /// Shows push indicators for all valid push directions from a crate.
  /// </summary>
  private void ShowCrateIndicators(Vector2Int cratePos) {
    _currentState = InteractionState.ShowingCratePushIndicators;
    _currentCratePos = cratePos;
    _pushIndicatorManager.DismissAll();

    var state = _gameSession.CurrentState;

    foreach (var dir in Vector2IntExtensions.Cardinals) {
      var pushTarget = cratePos + dir;
      var pushStandPos = cratePos - dir;

      // Check if push target is valid
      if (!state.IsValidPos(pushTarget.x, pushTarget.y)) {
        continue;
      }

      if (!state.CanReceiveCrate(pushTarget.x, pushTarget.y)) {
        continue;
      }

      // Check if stand position is valid
      if (!state.IsValidPos(pushStandPos.x, pushStandPos.y)) {
        continue;
      }

      if (!state.CanPlayerWalk(pushStandPos.x, pushStandPos.y)) {
        continue;
      }

      if (pushTarget == state.PlayerPos) {
        continue;
      }

      // Can player actually reach this position?
      List<Vector2Int> walkPath = Pather.FindPath(state, state.PlayerPos, pushStandPos);
      if (walkPath == null || walkPath.Count < 0) {
        if (!(state.PlayerPos == pushStandPos)) {
          continue;
        }
      }

      // Valid push direction - create indicator
      _pushIndicatorManager.CreateIndicator(dir, cratePos);
    }

    RemoveWalkIndicator();
  }

  /// <summary>
  /// Executes a crate push in the given direction.
  /// Generates walk moves to the stand position, then the push move.
  /// </summary>
  private void ExecuteCratePush(Vector2Int pushDirection) {
    var state = _gameSession.CurrentState;
    var cratePos = _currentCratePos;
    var pushTarget = cratePos + pushDirection;
    var pushStandPos = cratePos - pushDirection;

    var walkPath = Pather.FindPath(state, state.PlayerPos, pushStandPos);
    var moveList = new List<SokobanMove>();

    if (walkPath is { Count: > 0 }) {
      var walkPos = state.PlayerPos;
      foreach (var target in walkPath) {
        moveList.Add(SokobanMove.PlayerMove(walkPos, target));
        walkPos = target;
      }
    }

    var pushMove = SokobanMove.CratePush(pushStandPos, cratePos, cratePos, pushTarget);
    moveList.Add(pushMove);

    if (moveList.Count > 0) {
      _moveScheduler.Clear();
      _moveScheduler.StepDelay = 0f;
      _moveScheduler.Enqueue(moveList);
      InvalidateWalkableAreaCache();
      _currentState = InteractionState.Idle;
    }
  }

  /// <summary>
  /// Invalidates the cached walkable area (call when crates move).
  /// </summary>
  private void InvalidateWalkableAreaCache() {
    _cachedWalkableArea = null;
    _cachedWalkableAreaForState = null;
  }

  /// <summary>
  /// Removes the walk indicator GameObject.
  /// </summary>
  private void RemoveWalkIndicator() {
    if (_walkIndicator != null) {
      Object.Destroy(_walkIndicator);
      _walkIndicator = null;
    }
  }

  /// <summary>
  /// Converts screen position to grid position via raycasting.
  /// </summary>
  private Vector2Int? GetGridPosFromMousePos(Vector2 mousePos) {
    if (_mainCamera == null) {
      return null;
    }

    Ray ray = _mainCamera.ScreenPointToRay(mousePos);

    if (Physics.Raycast(ray, out RaycastHit hit)) {
      return hit.point.WorldToGrid();
    }

    return null;
  }

  /// <summary>
  /// Converts a list of path coordinates to atomic player move commands.
  /// </summary>
  private List<SokobanMove> ConvertPathToMoves(Vector2Int startPos, List<Vector2Int> pathPoints) {
    var validMoves = new List<SokobanMove>();
    Vector2Int current = startPos;

    foreach (var nextPos in pathPoints) {
      validMoves.Add(SokobanMove.PlayerMove(current, nextPos));
      current = nextPos;
    }

    return validMoves;
  }

  /// <summary>
  /// Resets the pointer handler to idle state and hides all UI elements.
  /// Called by PlayerController when directional input is executed.
  /// </summary>
  public void ResetToIdle() {
    if (_currentState == InteractionState.ShowingCratePushIndicators) {
      _currentState = InteractionState.Idle;
      _pushIndicatorManager.DismissAll();
    }

    RemoveWalkIndicator();
  }
}
