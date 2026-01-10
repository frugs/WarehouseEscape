using JetBrains.Annotations;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerBehaviour : MonoBehaviour {
  private int _walkingParam;
  private int _pushingParam;

  [field: Header("References")]
  [field: SerializeField]
  [UsedImplicitly]
  public Animator PlayerAnimator { get; set; }


  [field: SerializeField]
  [UsedImplicitly]
  public GameSession GameSession { get; set; }

  [UsedImplicitly]
  private void Awake() {
    if (PlayerAnimator == null) {
      PlayerAnimator = GetComponent<Animator>();
    }

    if (GameSession == null) GameSession = FindAnyObjectByType<GameSession>();

    GameSession.PlayerAnimationState.StateChanged += OnStateChanged;

    _walkingParam = Animator.StringToHash("IsWalking");
    _pushingParam = Animator.StringToHash("IsPushing");
  }

  [UsedImplicitly]
  private void OnDestroy() {
    GameSession.PlayerAnimationState.StateChanged -= OnStateChanged;
  }

  private void OnStateChanged(PlayerAnimationState.State state) {
    PlayerAnimator.SetBool(_pushingParam, state == PlayerAnimationState.State.Pushing);
    PlayerAnimator.SetBool(_walkingParam, state == PlayerAnimationState.State.Walking);
  }
}
