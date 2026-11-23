using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

// A spot where a Draggable can be dropped and "click" into place
// (e.g. the wire gauze on the tripod, the thermometer in the clamp).
public class SnapZone : MonoBehaviour
{
    public static readonly List<SnapZone> All = new List<SnapZone>();

    [Tooltip("Only Draggables with this id fit here. Leave empty to accept anything.")]
    public string acceptsId = "";
    public bool canSnap = true;
    public bool projectedDistance = true;
    [Tooltip("How close (metres) the object must be dropped for it to snap.")]
    public float snapRadius = 0.12f;
    [Tooltip("Where the object ends up. Defaults to this object's own position.")]
    public Transform snapPoint;
    public bool alignRotation = true;
    [Tooltip("Optional ghost/glow object shown as a hint while a matching item is held (Guided mode).")]
    public GameObject highlight;

    public Draggable Occupant { get; private set; }
    public bool IsOccupied { get { return Occupant != null; } }
    Coroutine feedback;
    bool hintRequested;

    // accuracy: 1 = dropped exactly on target, 0 = right at the edge of the radius
    public event Action<SnapZone, Draggable, float> Snapped;
    public event Action<SnapZone, Draggable> Released;

    Transform Target { get { return snapPoint != null ? snapPoint : transform; } }

    void OnEnable()
    {
        All.Add(this);
        SetHighlight(false);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    public bool Accepts(Draggable d)
    {
        return canSnap && !IsOccupied && (string.IsNullOrEmpty(acceptsId) || acceptsId == d.id);
    }

    public float Distance(Vector3 worldPos)
    {
        Vector3 delta = worldPos - Target.position;
        if (projectedDistance && Camera.main != null) delta = Vector3.ProjectOnPlane(delta, Camera.main.transform.forward);
        return delta.magnitude;
    }

    public bool TrySnap(Draggable d)
    {
        return TrySnap(d,d.transform.position);
    }

    internal bool TrySnap(Draggable d,Vector3 dropPosition)
    {
        if (!Accepts(d)) return false;
        float dist = Distance(dropPosition);
        if (dist > snapRadius) return false;

        Occupant = d;
        float accuracy = 1f - Mathf.Clamp01(dist / snapRadius);
        d.GlideTo(Target.position, alignRotation ? Target.rotation : d.transform.rotation);
        if(StepController.Instance!=null&&StepController.Instance.ShowHints)
        {
            if(feedback!=null)StopCoroutine(feedback);
            feedback=StartCoroutine(PlacementFeedback(d));
        }
        if (Snapped != null) Snapped(this, d, accuracy);
        return true;
    }

    public void Release(Draggable d)
    {
        if (Occupant != d) return;
        Occupant = null;
        if (Released != null) Released(this, d);
    }
    public void Clear()
    {
        if(feedback!=null)StopCoroutine(feedback);feedback=null;
        Occupant = null; SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        hintRequested=on;
        if (highlight != null) highlight.SetActive(on||feedback!=null);
    }
    IEnumerator PlacementFeedback(Draggable item)
    {
        while(item.IsGliding)yield return null;
        if(highlight!=null)highlight.SetActive(true);
        yield return new WaitForSeconds(.45f);feedback=null;SetHighlight(hintRequested);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.7f);
        Gizmos.DrawWireSphere((snapPoint != null ? snapPoint : transform).position, snapRadius);
    }
}
