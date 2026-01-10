using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class MoveScheduler : MonoBehaviour {
  [field: Header("References")]
  [field: SerializeField]
  [UsedImplicitly]
  private GameSession GameSession { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private UndoBehaviour UndoBehaviour { get; set; }

  [field: Header("Animation Configuration")]
  [field: SerializeField]
  [UsedImplicitly]
  private float MoveAnimationDuration { get; set; } = 0.2f;

  [field: SerializeField]
  [UsedImplicitly]
  private float PushAnimationDuration { get; set; } = 0.5f;

  [field: SerializeField]
  [UsedImplicitly]
  private float RotationAnimationDuration { get; set; } = 0.05f;

  [field: SerializeField]
  [UsedImplicitly]
  private float FallAnimationDuration { get; set; } = 0.25f;

  // The unified queue
  private readonly Queue<SokobanMove> MoveQueue = new Queue<SokobanMove>();

  // Configurable delay for solution playback
  public float StepDelay { get; set; } = 0f;

  private UndoManager _undoManager;
  private Coroutine _currentProcess;
  private MoveAnimator _moveAnimator = new MoveAnimator();

  [UsedImplicitly]
  private void Awake() {
    if (GameSession == null) GameSession = GetComponent<GameSession>();
    if (UndoBehaviour == null) UndoBehaviour = GetComponent<UndoBehaviour>();

    _undoManager = UndoBehaviour.UndoManager;
  }

  public void Enqueue(SokobanMove move) {
    MoveQueue.Enqueue(move);
    TryProcessQueue();
  }

  public void Enqueue(IEnumerable<SokobanMove> moves) {
    foreach (var move in moves) {
      MoveQueue.Enqueue(move);
    }

    TryProcessQueue();
  }

  public void Clear() {
    MoveQueue.Clear();
    // Note: We deliberately do NOT stop the currently playing animation
    // to avoid visual snapping/glitches. We just clear future steps.
  }

  public void ClearInterrupt() {
    StopAllCoroutines();
    _currentProcess = null;
    Clear();
  }

  private void TryProcessQueue() {
    if (_currentProcess == null && MoveQueue.Count > 0) {
      _currentProcess = StartCoroutine(ProcessQueueRoutine());
    }
  }

  private IEnumerator ProcessQueueRoutine() {
    while (MoveQueue.Count > 0) {
      var move = MoveQueue.Dequeue();

      var beforeState = GameSession.CurrentState;
      GameSession.ApplyMoveToCurrentState(move, out GameObject playerObj, out GameObject crateObj);
      _undoManager?.RecordMove(beforeState, move);

      // 2. Animations (The "Over Time" part)
      var anims = new List<Coroutine>();

      if (playerObj != null) {
        var moveDuration = move.type == MoveType.PlayerMove
            ? MoveAnimationDuration
            : PushAnimationDuration;

        anims.Add(
            StartCoroutine(
                _moveAnimator.AnimateMoveTransform(
                    playerObj,
                    move.playerTo,
                    moveDuration)));
        anims.Add(
            StartCoroutine(
                _moveAnimator.AnimateRotateTransform(
                    playerObj,
                    move.playerTo,
                    RotationAnimationDuration)));
      }

      if (move.type == MoveType.CratePush && crateObj != null) {
        // Handle the "fell in hole" logic here centrally
        bool fellInHole = GameSession.CurrentState.IsFilledHoleAt(move.crateTo.x, move.crateTo.y)
                          && !GameSession.CurrentState.IsCrateAt(move.crateTo.x, move.crateTo.y);

        anims.Add(
            fellInHole
                ? StartCoroutine(
                    _moveAnimator.AnimateCrateFall(
                        crateObj,
                        move.crateTo,
                        PushAnimationDuration,
                        FallAnimationDuration))
                : StartCoroutine(
                    _moveAnimator.AnimateMoveTransform(
                        crateObj,
                        move.crateTo,
                        PushAnimationDuration)));
      }

      GameSession.PlayerAnimationState.CurrentState = move.type switch {
          MoveType.PlayerMove => PlayerAnimationState.State.Walking,
          MoveType.CratePush => PlayerAnimationState.State.Pushing,
          _ => PlayerAnimationState.State.Idle,
      };

      // Wait for animations
      foreach (var c in anims) yield return c;

      GameSession.PlayerAnimationState.ToIdle();

      // Optional delay (useful for solution playback)
      if (StepDelay > 0) yield return new WaitForSeconds(StepDelay);

      // Check win after every move
      GameSession.CheckWinCondition();
    }

    _currentProcess = null;
  }
}
