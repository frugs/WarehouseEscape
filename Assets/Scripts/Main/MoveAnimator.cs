using System.Collections;
using UnityEngine;

public class MoveAnimator {
  public IEnumerator AnimateMoveTransform(
      GameObject obj,
      Vector2Int targetGridPos,
      float duration, 
      AnimationCurve curve = null) {
    if (obj == null) yield break;

    Vector3 startPos = obj.transform.position;
    Vector3 endPos = GridUtils.GridToWorld(
        targetGridPos.x,
        targetGridPos.y,
        obj.transform.position.y);

    float elapsed = 0f;
    while (elapsed < duration) {
      elapsed += Time.deltaTime;
      float t = elapsed / duration;
      float curvedT = curve != null ? curve.Evaluate(t) : t;
        
      obj.position = Vector3.Lerp(startPos, endPos, curvedT);

      yield return null;
    }

    obj.transform.position = endPos;
  }

  public IEnumerator AnimateRotateTransform(
      GameObject obj,
      Vector2Int targetGridPos,
      float duration) {
    Vector3 startPos = obj.transform.position;
    Vector3 endPos = GridUtils.GridToWorld(
        targetGridPos.x,
        targetGridPos.y,
        obj.transform.position.y);

    var lookDirection = endPos - startPos;
    lookDirection.y = 0f;

    var targetRot = Quaternion.LookRotation(lookDirection, Vector3.up);
    return AnimateRotateTransform(obj, targetRot, duration);
  }

  public IEnumerator AnimateRotateTransform(GameObject obj, Quaternion targetRot, float duration) {
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

    while (elapsed < duration) {
      elapsed += Time.deltaTime;
      float t = elapsed / duration;

      float angle = Mathf.LerpAngle(startAngle, targetAngle, t);
      obj.transform.rotation = Quaternion.Euler(0, angle, 0);

      yield return null;
    }

    obj.transform.rotation = targetRot;
  }

  public IEnumerator AnimateTransformFall(
      GameObject obj,
      Vector2Int targetGridPos,
      float duration, 
      AnimationCurve curve = null) {
    if (obj == null) yield break;

    // Sink down
    Vector3 startPos = obj.transform.position;
    Vector3 endPos = startPos + Vector3.down * 1.0f; // Sink depth
    float elapsed = 0f;

    while (elapsed < fallDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / fallDuration;
      float curvedT = curve != null ? curve.Evaluate(t) : t;
      obj.transform.position = Vector3.Lerp(startPos, endPos, curvedT);
      yield return null;
    }

    obj.transform.position = endPos;
  }
}
