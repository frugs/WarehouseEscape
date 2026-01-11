using UnityEngine;

// ReSharper disable UnusedMember.Global
public static class AnimationCurves {
  public static AnimationCurve Linear => AnimationCurve.Linear(0f, 0f, 1f, 1f);

  public static AnimationCurve EaseInQuad => new AnimationCurve(
      new Keyframe(0f, 0f, 0f, 0f),
      new Keyframe(1f, 1f, 2f, 2f));

  public static AnimationCurve EaseOutQuad => new AnimationCurve(
      new Keyframe(0f, 0f, 2f, 2f),
      new Keyframe(1f, 1f, 0f, 0f));

  public static AnimationCurve EaseInOut =
      AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

  // Custom: constant speed but with tiny ease to avoid snapping
  public static AnimationCurve SmoothLinear => new AnimationCurve(
      new Keyframe(0f, 0f, 0f, 0f, 0f, 0f),
      new Keyframe(1f, 1f, 0f, 0f, 0f, 0f)
  );
}
