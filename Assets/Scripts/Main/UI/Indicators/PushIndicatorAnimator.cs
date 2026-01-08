using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Handles animation for a single push indicator.
/// Bobs the indicator back and forth in its facing direction when hovered.
/// </summary>
public class PushIndicatorAnimator : MonoBehaviour {
  [field: SerializeField]
  [UsedImplicitly]
  private float BobDistance { get; set; } = 0.075f;

  [field: SerializeField]
  [UsedImplicitly]
  private float BobSpeed { get; set; } = 5f;

  private Vector3 _basePosition;
  private Vector2Int _direction;
  private float _animationTime;
  private bool _isAnimating;

  [UsedImplicitly]
  private void LateUpdate() {
    if (_isAnimating) {
      UpdateAnimation();
    }
  }

  /// <summary>
  /// Initialize the animator with position and facing direction.
  /// </summary>
  public void Initialize(Vector3 basePosition, Vector2Int direction) {
    _basePosition = basePosition;
    _direction = direction;
    _animationTime = 0f;
    _isAnimating = false;
  }

  /// <summary>
  /// Start animating this indicator.
  /// </summary>
  public void StartAnimation() {
    _isAnimating = true;
    _animationTime = 0f;
  }

  /// <summary>
  /// Stop animating this indicator and return to base position.
  /// </summary>
  public void StopAnimation() {
    _isAnimating = false;
    transform.position = _basePosition;
    _animationTime = 0f;
  }

  private void UpdateAnimation() {
    _animationTime += Time.deltaTime * BobSpeed;

    // Calculate bob offset using sine wave
    float bobOffset = Mathf.Sin(_animationTime) * BobDistance;

    // Apply bob in the direction the indicator is facing
    Vector3 directionVector = new Vector3(_direction.x, 0, _direction.y).normalized;
    transform.position = _basePosition + directionVector * bobOffset;
  }
}
