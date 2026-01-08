using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Handles player input with isometric pointer/touch controls.
/// Supports walking to accessible tiles and pushing crates with directional indicators.
/// </summary>
public class PlayerController : MonoBehaviour {
  private const string PUSH_INDICATOR_LAYER = "PushIndicator";

  [Header("Dependencies")]
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

  // ===== CACHED WALKABLE AREA (BFS optimization) =====
  private IReadOnlyList<Vector2Int> _cachedWalkableArea;
  private SokobanState? _cachedWalkableAreaForState;

  private readonly WalkableAreaScanner _walkableAreaScanner = new WalkableAreaScanner();

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
    InvalidateWalkableAreaCache();
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
        InvalidateWalkableAreaCache();
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
          InvalidateWalkableAreaCache();
          return true;
        }
      }
    }

    return false;
  }

  private void HandlePointerInput() {
    var mousePos = _inputActions.Player.MousePosition.ReadValue<Vector2>();

    // Only update hover feedback when mouse position actually changes
    if (_pointerLastPos == null || !_pointerLastPos.Value.EqualsWithThreshold(mousePos, 1f)) {
      Vector2Int? maybeGridPos = GetGridPosFromMousePos(mousePos);

      // Handle hover feedback when NOT showing indicators
      if (_currentState == InteractionState.Idle) {
        UpdateHoverFeedback(maybeGridPos);
      }

      _pointerLastPos = mousePos;

      // Handle hover over indicators (single raycast on mouse move, not every frame)
      if (_currentState == InteractionState.ShowingCratePushIndicators) {
        UpdatePushIndicatorHoverOnMouseMove(mousePos);
      }
    }

    // Handle click/tap input
    if (_inputActions.Player.Click.WasPerformedThisFrame()) {
      if (GameSession.CurrentState.IsWin()) return;

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
      Vector2Int? maybeGridPos = GetGridPosFromMousePos(mousePos);
      if (maybeGridPos.HasValue) {
        HandleClick(maybeGridPos.Value);
      }
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
        _walkIndicator = Instantiate(WalkIndicatorPrefab);
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
    var state = GameSession.CurrentState;

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
    if (_cachedWalkableAreaForState == null) return false;

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
  /// Invalidates the cached walkable area (call when crates move).
  /// </summary>
  private void InvalidateWalkableAreaCache() {
    _cachedWalkableArea = null;
    _cachedWalkableAreaForState = null;
  }

  private void HandleClick(Vector2Int tile) {
    var state = GameSession.CurrentState;

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
          MoveScheduler.Clear();
          MoveScheduler.StepDelay = 0f;
          MoveScheduler.Enqueue(moveList);
          InvalidateWalkableAreaCache();
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
    indicator.layer = LayerMask.NameToLayer(PUSH_INDICATOR_LAYER);

    var basePosition = (cratePos + direction).GridToWorld(0.5f);
    indicator.transform.position = basePosition;
    indicator.transform.LookAt((cratePos + direction * 2).GridToWorld(0.5f));

    // Ensure collider exists for raycasting
    var indicatorCollider = indicator.GetComponent<Collider>();
    if (indicatorCollider == null) {
      indicatorCollider = indicator.AddComponent<BoxCollider>();
    }

    indicatorCollider.isTrigger = true;

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
    if (_previouslyHoveredIndicator != null) {
      ExecuteCratePush(_previouslyHoveredIndicator.Direction);
      DismissPushIndicators();
      return true;
    }

    return false;
  }

  private void UpdatePushIndicatorHoverOnMouseMove(Vector2 mousePos) {
    // Clear previous hover state
    if (_previouslyHoveredIndicator != null) {
      _previouslyHoveredIndicator.IsHovered = false;
      UpdateIndicatorMaterial(_previouslyHoveredIndicator, false);
      _previouslyHoveredIndicator = null;
    }

    if (Camera.main == null) return;

    Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));

    // Raycast only against PushIndicator layer
    if (Physics.Raycast(ray, out RaycastHit hit, 100f, _pushIndicatorLayerMask)) {
      GameObject hitObject = hit.collider.gameObject;

      // Find the corresponding CratePushIndicator
      foreach (var indicator in _pushIndicators) {
        if (indicator.Visual == hitObject) {
          indicator.IsHovered = true;
          _previouslyHoveredIndicator = indicator;
          UpdateIndicatorMaterial(indicator, true);
          break;
        }
      }
    }
  }

  private void UpdateIndicatorMaterial(CratePushIndicator indicator, bool isHovered) {
    var indicatorRenderer = indicator.Visual.GetComponent<MeshRenderer>();
    var indicatorPrefabRenderer = PushIndicatorPrefab.GetComponent<MeshRenderer>();

    if (indicatorRenderer != null && indicatorPrefabRenderer != null) {
      if (isHovered) {
        var mat = new Material(indicatorPrefabRenderer.sharedMaterial);
        mat.color = new Color(0.8f, 1f, 0.2f, 0.9f);
        indicatorRenderer.material = mat;
      } else {
        indicatorRenderer.material = indicatorPrefabRenderer.sharedMaterial;
      }
    }
  }

  private void UpdatePushIndicatorAnimations() {
    foreach (var indicator in _pushIndicators) {
      if (indicator.IsHovered) {
        indicator.AnimationTime += Time.deltaTime * IndicatorBobSpeed;

        float bobOffset = Mathf.Sin(indicator.AnimationTime) * IndicatorBobDistance;
        Vector3 directionVector =
            new Vector3(indicator.Direction.x, 0, indicator.Direction.y).normalized;
        indicator.Visual.transform.position = indicator.BasePosition + directionVector * bobOffset;
      }
    }
  }

  private void ExecuteCratePush(Vector2Int pushDirection) {
    var state = GameSession.CurrentState;
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
      MoveScheduler.Clear();
      MoveScheduler.StepDelay = 0f;
      MoveScheduler.Enqueue(moveList);
      InvalidateWalkableAreaCache();
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
