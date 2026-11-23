using UnityEngine;

// Measures how far a finger/mouse has travelled in a circle around a centre point.
// Used for grinding with the pestle and stirring with the glass stirrer.
// Plain C# class (not a component): create one, call Begin(), then Feed() every frame.
public class CircularMotionTracker
{
    public float minRadius = 30f;   // screen pixels: ignore jitter too close to the centre

    public float TotalDegrees { get; private set; }     // all rotation, either direction
    public float SignedDegrees { get; private set; }    // + anticlockwise, - clockwise
    public float DegreesPerSecond { get; private set; } // smoothed speed
    public float Revolutions { get { return TotalDegrees / 360f; } }

    Vector2 center;
    float lastAngle;
    bool hasLast;

    public void Begin(Vector2 screenCenter)
    {
        center = screenCenter;
        hasLast = false;
        DegreesPerSecond = 0f;
    }

    public void ResetProgress()
    {
        TotalDegrees = 0f;
        SignedDegrees = 0f;
    }

    // Returns how many degrees of circular motion this call added.
    public float Feed(Vector2 screenPos, float dt)
    {
        Vector2 v = screenPos - center;
        if (v.magnitude < minRadius)
        {
            hasLast = false;
            DegreesPerSecond = Mathf.Lerp(DegreesPerSecond, 0f, 0.2f);
            return 0f;
        }

        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        float delta = 0f;
        if (hasLast) delta = Mathf.DeltaAngle(lastAngle, angle);
        lastAngle = angle;
        hasLast = true;

        if (Mathf.Abs(delta) > 120f) delta = 0f; // a huge jump in one frame is noise, not stirring

        TotalDegrees += Mathf.Abs(delta);
        SignedDegrees += delta;
        float instant = dt > 0f ? Mathf.Abs(delta) / dt : 0f;
        DegreesPerSecond = Mathf.Lerp(DegreesPerSecond, instant, 0.2f);
        return Mathf.Abs(delta);
    }
}
