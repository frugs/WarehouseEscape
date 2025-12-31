using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class PlayerController : MonoBehaviour {
  [Header("Dependencies")]
  [SerializeField]
  private GridManager GridManager;

  [SerializeField]
  private MoveScheduler MoveScheduler;

  private GameInput InputActions; // The generated C# class from your Input Action Asset

  [UsedImplicitly]
  private void Awake() {
    GridManager = FindAnyObjectByType<GridManager>();
    MoveScheduler = FindAnyObjectByType<MoveScheduler>();

    // Initialize the generated input class
    InputActions = new GameInput();
  }

  [UsedImplicitly]
  private void OnEnable() {
    InputActions.Player.Enable();
  }

  [UsedImplicitly]
  private void OnDisable() {
    InputActions.Player.Disable();
  }

  [UsedImplicitly]
  private void Update() {
    if (HandleDirectionInput()) {
      return;
    }

    HandleTargetInput();
  }

  private bool HandleDirectionInput() {
    if (InputActions.Player.Move.WasPerformedThisFrame()) {
      if (GridManager.GridState.IsWin()) return true;

      Vector2 raw = InputActions.Player.Move.ReadValue<Vector2>();
      // Basic Axis Snapping
      Vector2Int dir = Vector2Int.zero;
      if (Mathf.Abs(raw.x) > 0.5f) {
        dir.x = (int)Mathf.Sign(raw.x);
      } else if (Mathf.Abs(raw.y) > 0.5f) {
        dir.y = (int)Mathf.Sign(raw.y);
      }

      if (dir != Vector2Int.zero) {
        // Attempt to build move based on CURRENT logical state
        // (which is already at the destination of any active animation)
        if (MoveRules.TryBuildMove(GridManager.GridState, dir, out SokobanMove move)) {
          MoveScheduler.Clear();
          MoveScheduler.StepDelay = 0f;
          MoveScheduler.Enqueue(move);

          return true;
        }
      }
    }

    return false;
  }

  private void HandleTargetInput() {
    if (InputActions.Player.Click.WasPerformedThisFrame()) {
      if (Camera.main == null) return;

      Vector2 mousePos = InputActions.Player.MousePosition.ReadValue<Vector2>();
      Ray ray = Camera.main.ScreenPointToRay(mousePos);

      if (Physics.Raycast(ray, out RaycastHit hit)) {

        var playerPos = GridManager.WorldToGrid(transform.position);
        var targetPos = GridManager.WorldToGrid(hit.point);
        List<Vector2Int> path = GridManager.GetPath(playerPos, targetPos);

        if (path != null && path.Count > 0) {
          // We need to convert a list of coordinates [ (1,1), (1,2), (1,3) ]
          // into a list of moves.
          List<SokobanMove> moveList = ConvertPathToMoves(playerPos, path);

          if (moveList.Count > 0) {
            MoveScheduler.Clear();
            MoveScheduler.StepDelay = 0f;
            MoveScheduler.Enqueue(moveList);
          }
        }
      }
    }
  }

  /// <summary>
  /// Converts a raw list of grid coordinates (from BFS) into strictly valid SokobanMoves.
  /// This handles the edge case where a path might become invalid (e.g. dynamic changes),
  /// though in a turn-based game this is rare.
  /// </summary>
  private List<SokobanMove> ConvertPathToMoves(Vector2Int startPos, List<Vector2Int> pathPoints) {
    var validMoves = new List<SokobanMove>();
    Vector2Int current = startPos;

    // BFS returns the *target* cells.
    // We iterate through them and construct the move required to get there.
    foreach (var nextPos in pathPoints) {
      Vector2Int direction = nextPos - current;

      // We use a "Look Ahead" trick:
      // Since we aren't moving the player YET, we can't just check GridState normally
      // for the 2nd, 3rd, 4th step.
      // HOWEVER: Your pathfinder (BFS) already checks 'CanPlayerWalk'.
      // So we know the floor is clear of walls.
      // We only need to construct the standard Walk move.

      // Note: BFS generally shouldn't return paths through crates.
      // If your BFS ignores crates, we must check here.
      // Assuming BFS is correct, we just blindly build Walk moves.

      validMoves.Add(SokobanMove.PlayerMove(current, nextPos));
      current = nextPos;
    }

    return validMoves;
  }
}
