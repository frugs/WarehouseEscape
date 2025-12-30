using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class PlayerController : MonoBehaviour {
  [Header("Dependencies")]
  [SerializeField]

  private GridManager GridManager;
  private SolutionController SolutionController;
  private GameInput InputActions; // The generated C# class from your Input Action Asset

  // State flags
  public bool IsBusy { get; private set; } // Processing a move/animation

  // Input & Movement Queues
  private Vector2Int? MoveBuffer; // Single buffered keyboard input
  private List<Vector2Int> PathQueue; // List of steps for mouse pathing

  [UsedImplicitly]
  private void Awake() {
    GridManager = FindAnyObjectByType<GridManager>();
    SolutionController = FindAnyObjectByType<SolutionController>();
    PathQueue = new List<Vector2Int>();

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
    HandleInput();
    ProcessMovement();
  }

  private void HandleInput() {
    // MOVEMENT INPUT (Priority: High)
    if (InputActions.Player.Move.WasPerformedThisFrame()) {
      Vector2 rawInput = InputActions.Player.Move.ReadValue<Vector2>();
      int x = 0;
      int y = 0;

      // Simple threshold check
      if (Mathf.Abs(rawInput.x) > 0.5f) {
        x = (int)Mathf.Sign(rawInput.x);
      } else if (Mathf.Abs(rawInput.y) > 0.5f) {
        y = (int)Mathf.Sign(rawInput.y);
      }

      if (x != 0 || y != 0) {
        // User took control: Clear the path
        PathQueue.Clear();
        // Buffer the input
        MoveBuffer = new Vector2Int(x, y);
        return;
      }
    }

    // MOUSE INPUT (Priority: Low)
    if (InputActions.Player.Click.WasPerformedThisFrame()) {
      if (Camera.main == null) return;

      Vector2 mousePos = InputActions.Player.MousePosition.ReadValue<Vector2>();
      Ray ray = Camera.main.ScreenPointToRay(mousePos);

      if (Physics.Raycast(ray, out RaycastHit hit)) {
        // Clear existing buffer/path
        MoveBuffer = null;
        PathQueue.Clear();

        // Calculate new path
        var startCell = GridManager.WorldToGrid(transform.position);
        var targetCell = GridManager.WorldToGrid(hit.point);

        if (startCell != null && targetCell != null) {
          var newPath = GridManager.GetPath(new Vector2Int(startCell.x, startCell.y),
            new Vector2Int(targetCell.x, targetCell.y));
          if (newPath != null) {
            PathQueue = newPath;
          }
        }
      }
    }
  }

  private void ProcessMovement() {
    // If busy, we can't start a new move yet.
    if (IsBusy) return;

    Vector2Int moveDir = Vector2Int.zero;

    // 1. Check Keyboard Buffer first (Manual Control)
    if (MoveBuffer.HasValue) {
      moveDir = MoveBuffer.Value;
      MoveBuffer = null;
      PathQueue.Clear(); // Manual move cancels remaining path
    }
    // 2. Check Auto-Path Queue (Mouse Control)
    else if (PathQueue.Count > 0) {
      Vector2Int nextPos = PathQueue[0];
      Vector2Int currentPos = GetPlayerGridPos();

      // Convert target position to direction
      moveDir = nextPos - currentPos;

      // Remove the step we are about to take
      PathQueue.RemoveAt(0);

      // Safety check: if path implies a jump or diagonal (invalid), abort
      if (moveDir.sqrMagnitude != 1) {
        PathQueue.Clear();
        return;
      }
    }

    // 3. Execute
    if (moveDir != Vector2Int.zero) {
      StartCoroutine(AttemptMove(moveDir));
    }
  }

  private IEnumerator AttemptMove(Vector2Int direction) {
    IsBusy = true; // Lock input immediately

    Vector2Int currentPos = GetPlayerGridPos();
    Vector2Int targetPos = currentPos + direction;



    // If invalid, unlock and exit
    if (!GridManager.IsValidPos(targetPos)) {
      IsBusy = false;
      PathQueue.Clear(); // Abort path if we hit a wall unexpectedly
      yield break;
    }

    SokobanMove? maybeMove = null;

    // Case A: Player Move
    if (GridManager.GridState.CanPlayerWalk(targetPos.x, targetPos.y)) {
      maybeMove = SokobanMove.PlayerMove(currentPos, targetPos);
    }
    // Case B: Crate Push
    else if (GridManager.GridState.IsCrateAt(targetPos.x, targetPos.y)) {
      // Pushing a crate cancels the mouse path
      PathQueue.Clear();

      // Check if crate destination is valid
      Vector2Int crateTarget = targetPos + direction;
      if (GridManager.GridState.CanReceiveCrate(crateTarget.x, crateTarget.y)) {
        maybeMove = SokobanMove.CratePush(currentPos, targetPos, targetPos, crateTarget);
      }
    }

    // If move isn't valid (e.g. pushing crate into wall), exit
    if (maybeMove == null) {
      IsBusy = false;
      yield break;
    }

    // This updates the Grid Logic AND the Visual Array pointers instantly.
    // It returns the GameObjects we need to animate.
    var move = (SokobanMove)maybeMove;
    GridManager.RegisterMoveUpdates(move, out GameObject playerObj, out GameObject crateObj);

    List<Coroutine> activeAnimations = new List<Coroutine>();

    // Queue Player Animation
    if (playerObj != null) {
      activeAnimations.Add(StartCoroutine(GridManager.AnimateTransform(playerObj, move.playerTo)));
    }

    // Queue Crate Animation
    if (move.type == MoveType.CratePush && crateObj != null) {
      // FIX: Only play fall animation if the crate "disappeared" (became the floor).
      // If occupant is Crate, it means it's sitting on TOP of a filled hole/floor.
      bool fellInHole = GridManager.GridState.IsFilledHoleAt(move.crateTo.x, move.crateTo.y) &&
                        !GridManager.GridState.IsCrateAt(move.crateTo.x, move.crateTo.y);

      activeAnimations.Add(fellInHole
        ? StartCoroutine(GridManager.AnimateCrateFall(crateObj, move.crateTo))
        : StartCoroutine(GridManager.AnimateTransform(crateObj, move.crateTo)));
    }

    // Wait for all animations to finish
    foreach (var anim in activeAnimations) {
      yield return anim;
    }

    // --- 4. FINALIZE ---
    GridManager.CheckWinCondition();
    IsBusy = false; // Unlock input
  }

  private Vector2Int GetPlayerGridPos() {
    return GridManager.WorldToGrid(transform.position);
  }
}
