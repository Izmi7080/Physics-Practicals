using System;
using UnityEngine;

/*
 * Layer: Interaction (apparatus pickup, same pattern as DraggableBobHandle - a reusable
 * "select this instrument, then select the thing it measures" handle, not tied to the pendulum
 * specifically beyond living in this practical's folder for now).
 * Purpose: Lets the student select the vernier caliper prop, then select the bob, to open the
 * caliper reading view (see VernierCaliperReadingUI). Optionally plays a simple jaw-close
 * animation if jaw transforms are assigned; the animation is purely cosmetic; the caliper never
 * decides the physics itself, only whether it should show the reading UI.
 *
 * NOTE (flagged per PRACTICAL_AUTHORING_GUIDE.md section 6 - asset budget): there is currently NO
 * 3D caliper model in the project. This script is written to attach to whatever low-poly caliper
 * prop is eventually authored/sourced (budget: 1-3k triangles, one material, per the guide) - it
 * does not invent or place that geometry. Until a prop exists, VernierCaliperReadingUI can still
 * be opened directly for testing (see its own summary comment).
 *
 * Talks to: reads events from an IPracticalInput source (assigned in the Inspector), exactly
 * like DraggableBobHandle; raises CaliperSelected for VernierCaliperReadingUI to open on.
 */
public class VernierCaliperTool : MonoBehaviour
{
    [Tooltip("Any component implementing IPracticalInput, e.g. MouseTouchInput.")]
    public MonoBehaviour inputSource;

    [Tooltip("The bob's collider GameObject - selecting the caliper THEN the bob is what completes the 'measure the bob' gesture.")]
    public GameObject bobTarget;

    [Header("Optional cosmetic jaw animation")]
    public Transform jawUpper;
    public Transform jawLower;
    public float closedJawGapMeters = 0.02f; // matches the true bob diameter, so jaws visually just touch it
    public float openJawGapMeters = 0.06f;
    public float jawAnimateSpeed = 4f;

    // Fired once the student has selected the caliper and then the bob, in that order - the
    // reading view should open at this point.
    public event Action CaliperSelected;

    private IPracticalInput input;
    private bool caliperArmed;
    private float targetJawGap;

    void Awake()
    {
        input = inputSource as IPracticalInput;
        if (input == null)
            Debug.LogError("VernierCaliperTool: inputSource does not implement IPracticalInput.", this);
        targetJawGap = openJawGapMeters;
    }

    void OnEnable()
    {
        if (input == null) return;
        input.PointerDown += HandlePointerDown;
    }

    void OnDisable()
    {
        if (input == null) return;
        input.PointerDown -= HandlePointerDown;
    }

    void Update()
    {
        // Purely cosmetic jaw lerp - never read by the logic layer, so it is safe to skip
        // entirely when no jaw transforms are assigned (e.g. before a real prop exists).
        if (jawUpper == null || jawLower == null) return;

        float currentGap = Vector3.Distance(jawUpper.position, jawLower.position);
        float newGap = Mathf.MoveTowards(currentGap, targetJawGap, jawAnimateSpeed * Time.deltaTime);
        float delta = (newGap - currentGap) * 0.5f;
        jawUpper.position += jawUpper.up * delta;
        jawLower.position -= jawUpper.up * delta;
    }

    private void HandlePointerDown(GameObject hitObject)
    {
        if (hitObject == gameObject)
        {
            caliperArmed = true;
            return;
        }

        if (caliperArmed && hitObject == bobTarget)
        {
            caliperArmed = false;
            targetJawGap = closedJawGapMeters;
            CaliperSelected?.Invoke();
            return;
        }

        // Selecting anything else cancels the two-step gesture rather than leaving it armed
        // indefinitely, so a stray tap elsewhere doesn't silently "measure" the bob later.
        caliperArmed = false;
    }

    // Called by VernierCaliperReadingUI when the reading panel closes, so the jaws visually
    // reopen for the next attempt if the reading was rejected.
    public void ReopenJaws()
    {
        targetJawGap = openJawGapMeters;
    }
}
