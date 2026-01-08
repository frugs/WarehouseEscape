using System.Collections;
using JetBrains.Annotations;
using UnityEngine;

public class ExitBehaviour : MonoBehaviour {
  [field: SerializeField]
  [UsedImplicitly]
  private float OpenOffset { get; set; } = 0.8f;

  [field: SerializeField]
  [UsedImplicitly]
  private float AnimationSmoothTime { get; set; } = 0.5f;

  private GameSession _gameSession;

  private Transform _doorTransform;
  private Vector3 _doorOpenPos;
  private Vector3 _doorClosedPos;

  private bool _isOpen;

  [UsedImplicitly]
  private void Awake() {
    _gameSession = FindAnyObjectByType<GameSession>();
    _gameSession.StateChanged += OnStateChanged;

    _doorTransform = transform.Find("Door");
    if (_doorTransform != null) {
      _doorOpenPos = _doorTransform.localPosition + Vector3.right * OpenOffset;
      _doorClosedPos = _doorTransform.localPosition;
    }
  }

  [UsedImplicitly]
  private void OnDestroy() {
    _gameSession.StateChanged -= OnStateChanged;
  }

  private void OnStateChanged() {
    var isSolved = _gameSession.CurrentState.IsSolved();
    if (isSolved && !_isOpen) {
      OpenExit();
    } else if (!isSolved && _isOpen) {
      CloseExit();
    }
  }

  private void OpenExit() {
    StopAllCoroutines();
    _isOpen = true;
    StartCoroutine(MoveDoorCoroutine(_doorOpenPos));
  }

  private void CloseExit() {
    StopAllCoroutines();
    _isOpen = false;
    StartCoroutine(MoveDoorCoroutine(_doorClosedPos));
  }

  private IEnumerator MoveDoorCoroutine(Vector3 targetPos) {
    if (_doorTransform == null) yield break;

    Vector3 doorVelocity = Vector3.zero;

    while (_doorTransform.localPosition != targetPos) {
      _doorTransform.localPosition = Vector3.SmoothDamp(
          _doorTransform.localPosition,
          targetPos,
          ref doorVelocity,
          AnimationSmoothTime);

      yield return null;
    }
  }
}
