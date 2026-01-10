using System.Collections;
using UnityEngine;

public class MoveAnimator : MonoBehaviour {
  [SerializeField] private float MoveAnimationDuration = 0.2f;
  [SerializeField] private float RotationAnimationDuration = 0.05f;
  [SerializeField] private float FallAnimationDuration = 0.15f;

  public IEnumerator AnimateMoveTransform(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    Vector3 startPos = obj.transform.position;
    Vector3 endPos = GridUtils.GridToWorld(
        targetGridPos.x,
        targetGridPos.y,
        obj.transform.position.y);

    float elapsed = 0f;
    while (elapsed < MoveAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / MoveAnimationDuration;
      t = t * (2 - t); // Quadratic ease-out

      obj.transform.position = Vector3.Lerp(startPos, endPos, t);

      yield return null;
    }

    obj.transform.position = endPos;
  }

  public IEnumerator AnimateRotateTransform(GameObject obj, Vector2Int targetGridPos) {
    Vector3 startPos = obj.transform.position;
    Vector3 endPos = GridUtils.GridToWorld(
        targetGridPos.x,
        targetGridPos.y,
        obj.transform.position.y);

    var lookDirection = endPos - startPos;
    lookDirection.y = 0f;

    var targetRot = Quaternion.LookRotation(lookDirection, Vector3.up);
    return AnimateRotateTransform(obj, targetRot);
  }

  public IEnumerator AnimateRotateTransform(GameObject obj, Quaternion targetRot) {
    if (obj == null) yield break;

    float elapsed = 0f;
    float startAngle = obj.transform.eulerAngles.y;
    float targetAngle = targetRot.eulerAngles.y;

    // Ensure the difference is within -180 to 180 degrees
    // This pre-calculates the shortest path so standard Lerp works
    float deltaAngle = Mathf.DeltaAngle(startAngle, targetAngle);

    // Recalculate the target angle relative to the start angle
    // e.g. if start=0, target=270, delta is -90. New target becomes -90.
    targetAngle = startAngle + deltaAngle;

    while (elapsed < RotationAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / RotationAnimationDuration;

      float angle = Mathf.LerpAngle(startAngle, targetAngle, t);
      obj.transform.rotation = Quaternion.Euler(0, angle, 0);

      yield return null;
    }

    obj.transform.rotation = targetRot;
  }

  public IEnumerator AnimateCrateFall(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    // 1. Slide to the hole position
    yield return AnimateMoveTransform(obj, targetGridPos);

    // 2. Sink down
    Vector3 startPos = obj.transform.position;
    Vector3 endPos = startPos + Vector3.down * 1.0f; // Sink depth
    float elapsed = 0f;

    while (elapsed < FallAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / FallAnimationDuration;
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;

    // Optional: Rename for debugging
    obj.name = $"FilledHole_{targetGridPos.x}_{targetGridPos.y}";
  }
}
