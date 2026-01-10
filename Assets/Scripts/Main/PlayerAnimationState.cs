using System;

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

  public void ToIdle() => CurrentState = State.Idle;

  public void ToWalking() => CurrentState = State.Walking;

  public void ToPushing() => CurrentState = State.Pushing;
}
