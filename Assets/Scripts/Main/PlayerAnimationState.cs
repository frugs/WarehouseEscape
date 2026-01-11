using System;
using JetBrains.Annotations;

public class PlayerAnimationState {
  public enum State {
    Idle,
    Walking,
    Pushing,
  }

  private State _currentState = State.Idle;

  public State CurrentState {
    get => _currentState;
    set {
      if (_currentState != value) {
        _currentState = value;
        StateChanged?.Invoke(value);
      }
    }
  }

  public event Action<State> StateChanged;

  public void Reset() => ToIdle();

  [UsedImplicitly]
  public void ToIdle() => CurrentState = State.Idle;

  [UsedImplicitly]
  public void ToWalking() => CurrentState = State.Walking;

  [UsedImplicitly]
  public void ToPushing() => CurrentState = State.Pushing;
}
