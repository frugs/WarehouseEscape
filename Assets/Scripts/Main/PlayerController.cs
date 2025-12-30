using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class PlayerController : MonoBehaviour {
  [Header("Dependencies")]
  [SerializeField]

  private GridManager gridManager;
  private GameInput inputActions; // The generated C# class from your Input Action Asset

  // State flags
  public bool IsBusy { get; private set; } // Processing a move/animation

  // Input & Movement Queues
  private Vector2Int? moveBuffer; // Single buffered keyboard input
  private List<Vector2Int> pathQueue; // List of steps for mouse pathing

  [UsedImplicitly]
  private void Awake() {
    gridManager = FindAnyObjectByType<GridManager>();
    pathQueue = new List<Vector2Int>();

    // Initialize the generated input class
    inputActions = new GameInput();
  }

  [UsedImplicitly]
  private void OnEnable() {
    inputActions.Player.Enable();
  }

  [UsedImplicitly]
  private void OnDisable() {
    inputActions.Player.Disable();
  }

  [UsedImplicitly]
  private void Update() {
    HandleInput();
    ProcessMovement();
  }

  private void HandleInput() {
    // MOVEMENT INPUT (Priority: High)
    if (inputActions.Player.Move.WasPerformedThisFrame()) {
      Vector2 rawInput = inputActions.Player.Move.ReadValue<Vector2>();
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
        pathQueue.Clear();
        // Buffer the input
        moveBuffer = new Vector2Int(x, y);
        return;
      }
    }

    // MOUSE INPUT (Priority: Low)
    if (inputActions.Player.Click.WasPerformedThisFrame()) {
      if (Camera.main == null) return;

      Vector2 mousePos = inputActions.Player.MousePosition.ReadValue<Vector2>();
      Ray ray = Camera.main.ScreenPointToRay(mousePos);

      if (Physics.Raycast(ray, out RaycastHit hit)) {
        // Clear existing buffer/path
        moveBuffer = null;
        pathQueue.Clear();

        // Calculate new path
        var startCell = gridManager.WorldToGrid(transform.position);
        var targetCell = gridManager.WorldToGrid(hit.point);

        if (startCell != null && targetCell != null) {
          var newPath = gridManager.GetPath(new Vector2Int(startCell.x, startCell.y),
            new Vector2Int(targetCell.x, targetCell.y));
          if (newPath != null) {
            pathQueue = newPath;
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
    if (moveBuffer.HasValue) {
      moveDir = moveBuffer.Value;
      moveBuffer = null;
      pathQueue.Clear(); // Manual move cancels remaining path
    }
    // 2. Check Auto-Path Queue (Mouse Control)
    else if (pathQueue.Count > 0) {
      Vector2Int nextPos = pathQueue[0];
      Vector2Int currentPos = GetPlayerGridPos();

      // Convert target position to direction
      moveDir = nextPos - currentPos;

      // Remove the step we are about to take
      pathQueue.RemoveAt(0);

      // Safety check: if path implies a jump or diagonal (invalid), abort
      if (moveDir.sqrMagnitude != 1) {
        pathQueue.Clear();
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
    if (!gridManager.IsValidPos(targetPos)) {
      IsBusy = false;
      pathQueue.Clear(); // Abort path if we hit a wall unexpectedly
      yield break;
    }

    SokobanMove? maybeMove = null;

    // Case A: Player Move
    if (gridManager.GridState.CanPlayerWalk(targetPos.x, targetPos.y)) {
      maybeMove = SokobanMove.PlayerMove(currentPos, targetPos);
    }
    // Case B: Crate Push
    else if (gridManager.GridState.IsCrateAt(targetPos.x, targetPos.y)) {
      // Pushing a crate cancels the mouse path
      pathQueue.Clear();

      // Check if crate destination is valid
      Vector2Int crateTarget = targetPos + direction;
      if (gridManager.GridState.CanReceiveCrate(crateTarget.x, crateTarget.y)) {
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
    gridManager.RegisterMoveUpdates(move, out GameObject playerObj, out GameObject crateObj);

    List<Coroutine> activeAnimations = new List<Coroutine>();

    // Queue Player Animation
    if (playerObj != null) {
      activeAnimations.Add(StartCoroutine(gridManager.AnimateTransform(playerObj, move.playerTo)));
    }

    // Queue Crate Animation
    if (move.type == MoveType.CratePush && crateObj != null) {
      // FIX: Only play fall animation if the crate "disappeared" (became the floor).
      // If occupant is Crate, it means it's sitting on TOP of a filled hole/floor.
      bool fellInHole = gridManager.GridState.IsFilledHoleAt(move.crateTo.x, move.crateTo.y) &&
                        !gridManager.GridState.IsCrateAt(move.crateTo.x, move.crateTo.y);

      activeAnimations.Add(fellInHole
        ? StartCoroutine(gridManager.AnimateCrateFall(crateObj, move.crateTo))
        : StartCoroutine(gridManager.AnimateTransform(crateObj, move.crateTo)));
    }

    // Wait for all animations to finish
    foreach (var anim in activeAnimations) {
      yield return anim;
    }

    // --- 4. FINALIZE ---
    gridManager.CheckWinCondition();
    IsBusy = false; // Unlock input
  }

  private Vector2Int GetPlayerGridPos() {
    return gridManager.WorldToGrid(transform.position);
  }
}
