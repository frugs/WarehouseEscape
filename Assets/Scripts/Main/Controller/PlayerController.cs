using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Handles player input with isometric pointer/touch controls.
/// Supports walking to accessible tiles and pushing crates with directional indicators.
/// </summary>
public class PlayerController : MonoBehaviour {
  private const string PUSH_INDICATOR_LAYER = "PushIndicator";

  [field: Header("Dependencies")]
  [field: SerializeField]
  private GameSession GameSession { get; set; }

  [field: SerializeField] private MoveScheduler MoveScheduler { get; set; }

  [field: Header("UI References")]
  [field: SerializeField]
  [UsedImplicitly]
  private GameObject WalkIndicatorPrefab { get; set; }

  [field: SerializeField] [UsedImplicitly]
  private GameObject PushIndicatorPrefab;

  [field: Header("Push Indicator Animation")]
  [field: SerializeField]
  [UsedImplicitly]
  private float IndicatorBobDistance { get; set; } = 0.075f;

  [field: SerializeField]
  [UsedImplicitly]
  private float IndicatorBobSpeed { get; set; } = 5f;

  // State machine
  private enum InteractionState {
    Idle,
    ShowingCratePushIndicators
  }

  private GameInput _inputActions;
  private InteractionState _currentState = InteractionState.Idle;
  private Vector2Int _currentCratePos = Vector2Int.zero;
  private GameObject _walkIndicator;
  private Vector2? _pointerLastPos;
  private int _pushIndicatorLayerMask;

  // Data structure for crate push indicators with animation tracking
  private class CratePushIndicator {
    public Vector2Int Direction { get; set; }
    public GameObject Visual { get; set; }
    public Vector3 BasePosition { get; set; }
    public bool IsHovered { get; set; }
    public float AnimationTime { get; set; }
  }

  private List<CratePushIndicator> _pushIndicators = new List<CratePushIndicator>();
  private CratePushIndicator _previouslyHoveredIndicator;

  [UsedImplicitly]
  private void Awake() {
    GameSession = GetComponent<GameSession>();
    MoveScheduler = GetComponent<MoveScheduler>();

    _inputActions = new GameInput();
    _pushIndicatorLayerMask = LayerMask.GetMask(PUSH_INDICATOR_LAYER);
  }

  [UsedImplicitly]
  private void OnEnable() {
    _inputActions.Player.Enable();
  }

  [UsedImplicitly]
  private void OnDisable() {
    _inputActions.Player.Disable();
    RemoveWalkIndicator();
    DismissPushIndicators();
  }

  [UsedImplicitly]
  private void Update() {
    if (HandleRestartInput()) {
      return;
    }

    if (HandleDirectionInput()) {
      return;
    }

    // Pointer/Touch input handles both floor movement and crate interaction
    HandlePointerInput();
  }

  [UsedImplicitly]
  private void LateUpdate() {
    // Update animations for hovered push indicators
    if (_currentState == InteractionState.ShowingCratePushIndicators) {
      UpdatePushIndicatorAnimations();
    }
  }

  private bool HandleRestartInput() {
    if (_inputActions.Player.Restart.WasPerformedThisFrame()) {
      if (_inputActions.Player.Restart.IsPressed()) {
        MoveScheduler.ClearInterrupt();
        DismissPushIndicators();
        GameSession.ResetLevel();
        return true;
      }
    }

    return false;
  }

  private bool HandleDirectionInput() {
    if (_inputActions.Player.Move.WasPerformedThisFrame()) {
      if (GameSession.CurrentState.IsWin()) return true;

      Vector2 raw = _inputActions.Player.Move.ReadValue<Vector2>();
      Vector2Int dir = Vector2Int.zero;
      if (Mathf.Abs(raw.x) > 0.5f) {
        dir.x = (int)Mathf.Sign(raw.x);
      } else if (Mathf.Abs(raw.y) > 0.5f) {
        dir.y = (int)Mathf.Sign(raw.y);
      }

      if (dir != Vector2Int.zero) {
        DismissPushIndicators();
        RemoveWalkIndicator();

        if (MoveRules.TryBuildMove(GameSession.CurrentState, dir, out SokobanMove move)) {
          MoveScheduler.Clear();
          MoveScheduler.StepDelay = 0f;
          MoveScheduler.Enqueue(move);
          return true;
        }
      }
    }

    return false;
  }

  private void HandlePointerInput() {
    // Update mouse position for hover feedback
    var mousePos = _inputActions.Player.MousePosition.ReadValue<Vector2>();

    // Only update hover feedback when mouse position actually changes
    if (_pointerLastPos == null || !_pointerLastPos.Value.EqualsWithThreshold(mousePos, 1f)) {
      Vector2Int? maybeGridPos = GetGridPosFromMousePos(mousePos);

      // Handle hover feedback when NOT showing indicators
      if (_currentState == InteractionState.Idle) {
        UpdateHoverFeedback(maybeGridPos);
      }

      _pointerLastPos = mousePos;

      // Handle hover over indicators (raycast only on mouse move, not every frame)
      if (_currentState == InteractionState.ShowingCratePushIndicators) {
        UpdatePushIndicatorHoverOnMouseMove(mousePos);
      }
    }

    // Handle click/tap input
    if (_inputActions.Player.Click.WasPerformedThisFrame()) {
      if (GameSession.CurrentState.IsWin()) return;

      Vector2Int? maybeGridPos =
          GetGridPosFromMousePos(_inputActions.Player.MousePosition.ReadValue<Vector2>());

      if (_currentState == InteractionState.ShowingCratePushIndicators) {
        // We're showing indicators - check if player clicked on one
        if (TryHandleIndicatorClick()) {
          return;
        }

        // Player clicked elsewhere - dismiss indicators
        DismissPushIndicators();
        return;
      }

      // Normal mode: check what was clicked
      if (maybeGridPos.HasValue) {
        HandleClick(maybeGridPos.Value);
      }
    }
  }

  private void UpdateHoverFeedback(Vector2Int? maybeGridPos) {
    if (!maybeGridPos.HasValue) {
      RemoveWalkIndicator();
      _pointerLastPos = null;
      return;
    }

    var state = GameSession.CurrentState;
    var gridPos = maybeGridPos.Value;

    // Check if tile is accessible (walkable)
    if (state.CanPlayerWalk(gridPos.x, gridPos.y)) {
      // Create or update glowing floor indicator
      if (_walkIndicator == null) {
        _walkIndicator = Instantiate(WalkIndicatorPrefab);

        // Add collider to prevent raycast issues
        var indicatorCollider = _walkIndicator.AddComponent<BoxCollider>();
        indicatorCollider.isTrigger = true;
      }

      // Position at floor tile, slightly above ground
      _walkIndicator.transform.position = gridPos.GridToWorld();

      _walkIndicator.SetActive(true);
    } else {
      // Inaccessible tile - don't show glow
      RemoveWalkIndicator();
    }
  }

  private void HandleClick(Vector2Int tile) {
    var state = GameSession.CurrentState;

    // Check if clicking on a crate
    if (state.IsCrateAt(tile.x, tile.y)) {
      ShowCrateIndicators(tile);
      return;
    }

    // Check if it's an accessible floor tile
    if (state.CanPlayerWalk(tile.x, tile.y)) {
      List<Vector2Int> path = Pather.FindPath(state, state.PlayerPos, tile);

      if (path is { Count: > 0 }) {
        RemoveWalkIndicator();
        List<SokobanMove> moveList = ConvertPathToMoves(state.PlayerPos, path);

        if (moveList.Count > 0) {
          MoveScheduler.Clear();
          MoveScheduler.StepDelay = 0f;
          MoveScheduler.Enqueue(moveList);
        }
      }
    }
  }

  private void ShowCrateIndicators(Vector2Int cratePos) {
    _currentState = InteractionState.ShowingCratePushIndicators;
    _currentCratePos = cratePos;
    _pushIndicators.Clear();
    _previouslyHoveredIndicator = null;

    var state = GameSession.CurrentState;

    foreach (var dir in Vector2IntExtensions.Cardinals) {
      var pushTarget = cratePos + dir;
      var pushStandPos = cratePos - dir;

      // Check if push target is valid
      if (!state.IsValidPos(pushTarget.x, pushTarget.y)) continue;
      if (!state.CanReceiveCrate(pushTarget.x, pushTarget.y)) continue;

      // Check if stand position is valid
      if (!state.IsValidPos(pushStandPos.x, pushStandPos.y)) continue;
      if (!state.CanPlayerWalk(pushStandPos.x, pushStandPos.y)) continue;

      if (pushTarget == state.PlayerPos) continue;

      // Can player actually reach this position?
      List<Vector2Int> walkPath = Pather.FindPath(state, state.PlayerPos, pushStandPos);
      if (walkPath == null || walkPath.Count < 0) {
        if (!(state.PlayerPos == pushStandPos)) continue;
      }

      // Valid push direction - create indicator
      CreatePushIndicator(dir, cratePos);
    }

    RemoveWalkIndicator();
  }

  private void CreatePushIndicator(Vector2Int direction, Vector2Int cratePos) {
    var indicator = Instantiate(PushIndicatorPrefab);

    indicator.gameObject.name = $"PushIndicator_{direction}";
    var basePosition = (cratePos + direction).GridToWorld(0.5f);
    indicator.transform.position = basePosition;
    indicator.transform.LookAt((cratePos + direction * 2).GridToWorld(0.5f));

    var crateIndicator = new CratePushIndicator {
        Direction = direction,
        Visual = indicator,
        BasePosition = basePosition,
        IsHovered = false,
        AnimationTime = 0f
    };

    _pushIndicators.Add(crateIndicator);
  }

  private bool TryHandleIndicatorClick() {
    // Get the hovered indicator from the last mouse move check
    // If we have a previously hovered indicator, that's the one that was clicked
    if (_previouslyHoveredIndicator != null) {
      ExecuteCratePush(_previouslyHoveredIndicator.Direction);
      DismissPushIndicators();
      return true;
    }

    return false;
  }

  private void UpdatePushIndicatorHoverOnMouseMove(Vector2 mousePos) {
    if (Camera.main == null) return;

    // Clear previous hover state
    if (_previouslyHoveredIndicator != null) {
      _previouslyHoveredIndicator.IsHovered = false;
      _previouslyHoveredIndicator = null;
    }

    Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
    if (Physics.Raycast(ray, out RaycastHit hit, 100f, _pushIndicatorLayerMask)) {
      // Found a hit - look up which indicator this is
      GameObject hitObject = hit.collider.gameObject;

      // Find the corresponding CratePushIndicator
      foreach (var indicator in _pushIndicators) {
        if (indicator.Visual == hitObject) {
          indicator.IsHovered = true;
          _previouslyHoveredIndicator = indicator;
          break;
        }
      }
    }
  }

  private void UpdatePushIndicatorAnimations() {
    foreach (var indicator in _pushIndicators) {
      if (indicator.IsHovered) {
        // Update animation time
        indicator.AnimationTime += Time.deltaTime * IndicatorBobSpeed;

        // Calculate bob offset using sine wave
        float bobOffset = Mathf.Sin(indicator.AnimationTime) * IndicatorBobDistance;

        // Apply bob in the direction the indicator is facing
        Vector3 directionVector =
            new Vector3(indicator.Direction.x, 0, indicator.Direction.y).normalized;
        indicator.Visual.transform.position = indicator.BasePosition + directionVector * bobOffset;
      } else {
        indicator.Visual.transform.position = indicator.BasePosition;
        indicator.AnimationTime = 0;
      }
    }
  }

  private void ExecuteCratePush(Vector2Int pushDirection) {
    var state = GameSession.CurrentState;
    var cratePos = _currentCratePos;
    var pushTarget = cratePos + pushDirection;
    var pushStandPos = cratePos - pushDirection;

    // Find path to stand position
    var walkPath = Pather.FindPath(state, state.PlayerPos, pushStandPos);

    var moveList = new List<SokobanMove>();

    // Add walk moves to get into push position
    if (walkPath != null && walkPath.Count > 0) {
      var walkPos = state.PlayerPos;
      foreach (var target in walkPath) {
        moveList.Add(SokobanMove.PlayerMove(walkPos, target));
        walkPos = target;
      }
    }

    // Add the push move itself
    var pushMove = SokobanMove.CratePush(pushStandPos, cratePos, cratePos, pushTarget);
    moveList.Add(pushMove);

    // Execute
    if (moveList.Count > 0) {
      MoveScheduler.Clear();
      MoveScheduler.StepDelay = 0f;
      MoveScheduler.Enqueue(moveList);
    }
  }

  private void DismissPushIndicators() {
    foreach (var indicator in _pushIndicators) {
      if (indicator.Visual != null) {
        Destroy(indicator.Visual);
      }
    }

    _pushIndicators.Clear();
    _previouslyHoveredIndicator = null;
    _currentState = InteractionState.Idle;
  }

  private void RemoveWalkIndicator() {
    if (_walkIndicator != null) {
      Destroy(_walkIndicator);
      _walkIndicator = null;
    }
  }

  private Vector2Int? GetGridPosFromMousePos(Vector2 mousePos) {
    if (Camera.main == null) return null;

    Ray ray = Camera.main.ScreenPointToRay(mousePos);

    if (Physics.Raycast(ray, out RaycastHit hit)) {
      return hit.point.WorldToGrid();
    }

    return null;
  }

  private List<SokobanMove> ConvertPathToMoves(Vector2Int startPos, List<Vector2Int> pathPoints) {
    var validMoves = new List<SokobanMove>();
    Vector2Int current = startPos;

    foreach (var nextPos in pathPoints) {
      validMoves.Add(SokobanMove.PlayerMove(current, nextPos));
      current = nextPos;
    }

    return validMoves;
  }
}
