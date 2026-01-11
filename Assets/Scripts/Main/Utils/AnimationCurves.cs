using UnityEngine;

public static class AnimationCurves
{
    public static readonly AnimationCurve Linear = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public static readonly AnimationCurve QuadraticEaseIn = AnimationCurve.EaseIn(0f, 0f, 1f, 1f);
    public static readonly AnimationCurve QuadraticEaseOut = AnimationCurve.EaseOut(0f, 0f, 1f, 1f);
    public static readonly AnimationCurve QuadraticEaseInOut = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Custom: constant speed but with tiny ease to avoid snapping
    public static readonly AnimationCurve SmoothLinear = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f, 0f, 0f)
    );
}