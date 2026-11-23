using System;
using System.Collections;
using UnityEngine;

// Put this on anything the student can pick up (beaker, pestle, capillary tube, thermometer...).
// The object also needs a normal (non-trigger) Collider so a finger can hit it.
public enum DragMovement { Bench, FacingCamera, Vertical }
public class Draggable : MonoBehaviour
{
    public DragMovement movement = DragMovement.FacingCamera;
    public float minY = 1.10f, maxY = 1.30f, minimumHeight = .83f;
    [Tooltip("Matches a SnapZone's 'acceptsId'. Example: \"beaker\".")]
    public string id = "";
    public bool canDrag = true;
    [Tooltip("How far the object lifts off the table while held.")]
    public float liftHeight = 0.04f;
    [Tooltip("If dropped nowhere useful, glide back to where it started.")]
    public bool returnHomeOnMiss = true;

    public bool IsDragging { get; private set; }
    public SnapZone CurrentZone { get; private set; }
    public bool IsSnapped { get { return CurrentZone != null; } }
    public Vector3 HomePosition { get; private set; }
    public Quaternion HomeRotation { get; private set; }
    public float RestHeight { get; private set; }

    public event Action<Draggable> PickedUp;
    public event Action<Draggable, SnapZone> Dropped; // zone is null when dropped on nothing

    Coroutine glide;
    public bool IsGliding { get; private set; }
    public void CancelMotion()
    {
        if (glide != null) StopCoroutine(glide);
        glide = null; IsGliding = false;
    }

    void Awake()
    {
        RememberHome();
    }

    public void RememberHome()
    {
        HomePosition = transform.position;
        HomeRotation = transform.rotation;
        RestHeight = transform.position.y;
    }

    public void BeginDrag()
    {
        if (!canDrag) return;
        CancelMotion();
        IsDragging = true;
        if (CurrentZone != null)
        {
            CurrentZone.Release(this);
            CurrentZone = null;
        }
        if (PickedUp != null) PickedUp(this);
    }

    public void EndDrag(SnapZone zone)
    {
        IsDragging = false;
        if (zone != null)
        {
            CurrentZone = zone;
        }
        else if (movement == DragMovement.Vertical)
        {
            // Keep the selected immersion depth; confirmation validates it.
        }
        else if (returnHomeOnMiss)
        {
            GlideTo(HomePosition, HomeRotation);
        }
        else
        {
            GlideTo(new Vector3(transform.position.x, RestHeight, transform.position.z), transform.rotation);
        }
        if (Dropped != null) Dropped(this, zone);
    }

    public void GlideTo(Vector3 pos, Quaternion rot, float duration = 0.28f)
    {
        CancelMotion(); IsGliding = true;
        glide = StartCoroutine(GlideRoutine(pos, rot, duration));
    }

    IEnumerator GlideRoutine(Vector3 pos, Quaternion rot, float duration)
    {
        Vector3 p0 = transform.position;
        Quaternion r0 = transform.rotation;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            // A short lift, horizontal alignment and gentle settling, without rigidbody jitter.
            float arc=movement==DragMovement.Vertical?0:Mathf.Sin(k*Mathf.PI)*liftHeight;
            transform.SetPositionAndRotation(Vector3.Lerp(p0, pos, k)+Vector3.up*arc, Quaternion.Slerp(r0, rot, k));
            yield return null;
        }
        transform.SetPositionAndRotation(pos, rot);
        IsGliding = false; glide = null;
    }
}
