using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Handles player input with isometric pointer/touch controls.
/// Supports walking to accessible tiles and pushing crates with directional indicators.
/// </summary>
public class PlayerController : MonoBehaviour {
  [Header("Dependencies")] [SerializeField]
  private GameSession GameSession;

  [SerializeField] private MoveScheduler MoveScheduler;

  [Header("UI References")] [SerializeField]
  private Mesh WalkIndicatorMesh;

  [SerializeField][UsedImplicitly] private GameObject PushIndicatorPrefab;

  private GameInput InputActions;

  // State machine
  private enum InteractionState {
    Idle,
    ShowingCrateIndicators
  }

  private InteractionState _currentState = InteractionState.Idle;
  private Vector2Int _currentCratePos = Vector2Int.zero;
  private GameObject _ghostPlayer; // Visual feedback when hovering over accessible tiles
  private List<CrateIndicator> _crateIndicators = new List<CrateIndicator>();

  // Data structure for crate push indicators
  private class CrateIndicator {
    public Vector2Int Direction { get; set; }
    public GameObject Visual { get; set; }
    public bool IsHovered { get; set; }
  }

  [UsedImplicitly]
  private void Awake() {
    GameSession = GetComponent<GameSession>();
    MoveScheduler = GetComponent<MoveScheduler>();

    InputActions = new GameInput();
  }

  [UsedImplicitly]
  private void OnEnable() {
    InputActions.Player.Enable();
  }

  [UsedImplicitly]
  private void OnDisable() {
    InputActions.Player.Disable();
    CleanupGhostPlayer();
    DismissIndicators();
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

  private bool HandleRestartInput() {
    if (InputActions.Player.Restart.WasPerformedThisFrame()) {
      if (InputActions.Player.Restart.IsPressed()) {
        MoveScheduler.ClearInterrupt();
        DismissIndicators();
        GameSession.ResetLevel();
        return true;
      }
    }

    return false;
  }

  private bool HandleDirectionInput() {
    if (InputActions.Player.Move.WasPerformedThisFrame()) {
      if (GameSession.CurrentState.IsWin()) return true;

      Vector2 raw = InputActions.Player.Move.ReadValue<Vector2>();
      Vector2Int dir = Vector2Int.zero;
      if (Mathf.Abs(raw.x) > 0.5f) {
        dir.x = (int)Mathf.Sign(raw.x);
      } else if (Mathf.Abs(raw.y) > 0.5f) {
        dir.y = (int)Mathf.Sign(raw.y);
      }

      if (dir != Vector2Int.zero) {
        DismissIndicators();

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
    var mousePos = InputActions.Player.MousePosition.ReadValue<Vector2>();
    Vector2Int? hoveredTile = GetTileFromMousePosition(mousePos);

    // Handle hover feedback when NOT showing indicators
    if (_currentState == InteractionState.Idle) {
      UpdateHoverFeedback(hoveredTile);
    }

    // Handle click/tap input
    if (InputActions.Player.Click.WasPerformedThisFrame()) {
      if (GameSession.CurrentState.IsWin()) return;

      if (_currentState == InteractionState.ShowingCrateIndicators) {
        // We're showing indicators - check if player clicked on one
        if (TryHandleIndicatorClick(mousePos)) {
          return;
        }

        // Player clicked elsewhere - dismiss indicators
        DismissIndicators();
        return;
      }

      // Normal mode: check what was clicked
      if (hoveredTile.HasValue) {
        HandleTileClick(hoveredTile.Value);
      }
    }

    // Handle hover over indicators
    if (_currentState == InteractionState.ShowingCrateIndicators) {
      UpdateIndicatorHover(mousePos);
    }
  }

  private void UpdateHoverFeedback(Vector2Int? tile) {
    if (!tile.HasValue) {
      CleanupGhostPlayer();
      return;
    }

    var state = GameSession.CurrentState;

    // Check if tile is accessible (walkable)
    if (state.CanPlayerWalk(tile.Value.x, tile.Value.y)) {
      // Show ghost player
      if (_ghostPlayer == null) {
        _ghostPlayer = new GameObject("GhostPlayer");
        var _ghostPlayerRenderer = _ghostPlayer.AddComponent<MeshRenderer>();
        var filter = _ghostPlayer.AddComponent<MeshFilter>();

        filter.mesh = WalkIndicatorMesh;

        var mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3); // Transparent mode
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.color = new Color(1, 1, 1, 0.3f);
        _ghostPlayerRenderer.material = mat;
      }

      _ghostPlayer.transform.position = tile.Value.GridToWorld();
      _ghostPlayer.SetActive(true);
    } else {
      // Inaccessible tile - don't show ghost
      CleanupGhostPlayer();
    }
  }

  private void HandleTileClick(Vector2Int tile) {
    var state = GameSession.CurrentState;

    // Check if clicking on a crate
    if (state.IsCrateAt(tile.x, tile.y)) {
      ShowCrateIndicators(tile);
      return;
    }

    // Check if it's an accessible floor tile
    if (state.CanPlayerWalk(tile.x, tile.y)) {
      List<Vector2Int> path = Pather.FindPath(state, state.PlayerPos, tile);

      if (path != null && path.Count > 0) {
        CleanupGhostPlayer();
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
    _currentState = InteractionState.ShowingCrateIndicators;
    _currentCratePos = cratePos;
    _crateIndicators.Clear();

    var state = GameSession.CurrentState;

    // Determine which directions the crate can be pushed
    // A crate can be pushed in direction D if:
    // 1. The tile at (crate + D) is valid for the crate to move to
    // 2. The player can reach the tile at (crate - D) to push from that direction

    Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    foreach (var dir in directions) {
      var pushTarget = cratePos + dir;
      var pushStandPos = cratePos - dir;

      // Check if push target is valid
      if (!state.IsValidPos(pushTarget.x, pushTarget.y)) continue;
      if (!state.CanReceiveCrate(pushTarget.x, pushTarget.y)) continue;

      // Check if stand position is valid
      if (!state.IsValidPos(pushStandPos.x, pushStandPos.y)) continue;
      if (!state.CanPlayerWalk(pushStandPos.x, pushStandPos.y)) continue;

      // Can player actually reach this position?
      List<Vector2Int> walkPath = Pather.FindPath(state, state.PlayerPos, pushStandPos);
      if (walkPath == null || walkPath.Count < 0) {
        // Can't reach - don't show this indicator
        // Note: FindPath returns non-null even if already at destination
        if (!(state.PlayerPos == pushStandPos)) continue;
      }

      // Valid push direction - create indicator
      CreatePushIndicator(dir, cratePos);
    }

    CleanupGhostPlayer();
  }

  private void CreatePushIndicator(Vector2Int direction, Vector2Int cratePos) {
    var indicator = Instantiate(PushIndicatorPrefab);

    // Position indicator slightly elevated
    var indicatorPos = (cratePos + direction).GridToWorld(0.5f);
    indicator.gameObject.name = $"PushIndicator_{direction}";
    indicator.transform.position = indicatorPos;

    // Rotate to point in the direction
    // if (direction.x != 0 || direction.y != 0) {
    //   float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    //   indicator.transform.rotation = Quaternion.Euler(0, angle, 0);
    // }

    var crateIndicator = new CrateIndicator {
        Direction = direction, Visual = indicator, IsHovered = false
    };

    _crateIndicators.Add(crateIndicator);
  }

  private bool TryHandleIndicatorClick(Vector2 mousePos) {
    foreach (var indicator in _crateIndicators) {
      if (IsMouseOverIndicator(indicator, mousePos)) {
        ExecuteCratePush(indicator.Direction);
        DismissIndicators();
        return true;
      }
    }

    return false;
  }

  private void UpdateIndicatorHover(Vector2 mousePos) {
    foreach (var indicator in _crateIndicators) {
      bool wasHovered = indicator.IsHovered;
      indicator.IsHovered = IsMouseOverIndicator(indicator, mousePos);

      // Update visual when hover state changes
      if (indicator.IsHovered != wasHovered) {
        var indicatorRenderer = indicator.Visual.GetComponent<MeshRenderer>();
        var indicatorPrefabRenderer = PushIndicatorPrefab.GetComponent<MeshRenderer>();
        if (indicatorRenderer != null && indicatorPrefabRenderer != null) {
          if (indicator.IsHovered) {
            var mat = new Material(indicatorPrefabRenderer.sharedMaterial);
            mat.color = new Color(0.8f, 1f, 0.2f, 0.9f); // Bright yellow on hover
            indicatorRenderer.material = mat;
          } else {
            indicatorRenderer.material = indicatorPrefabRenderer.sharedMaterial;
          }
        }
      }
    }
  }

  private bool IsMouseOverIndicator(CrateIndicator indicator, Vector2 mousePos) {
    if (Camera.main == null) return false;

    Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y));
    var indicatorCollider = indicator.Visual.GetComponent<Collider>();

    // Add collider if not present
    if (indicatorCollider == null) {
      indicatorCollider = indicator.Visual.AddComponent<BoxCollider>();
    }

    return indicatorCollider.Raycast(ray, out _, 100f);
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
    var pushMove = SokobanMove.CratePush(
        pushStandPos,
        cratePos,
        cratePos,
        pushTarget);

    moveList.Add(pushMove);

    // Execute
    if (moveList.Count > 0) {
      MoveScheduler.Clear();
      MoveScheduler.StepDelay = 0f;
      MoveScheduler.Enqueue(moveList);
    }
  }

  private void DismissIndicators() {
    foreach (var indicator in _crateIndicators) {
      if (indicator.Visual != null) {
        Destroy(indicator.Visual);
      }
    }

    _crateIndicators.Clear();
    _currentState = InteractionState.Idle;
  }

  private void CleanupGhostPlayer() {
    if (_ghostPlayer != null) {
      Destroy(_ghostPlayer);
      _ghostPlayer = null;
    }
  }

  private Vector2Int? GetTileFromMousePosition(Vector2 mousePos) {
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
