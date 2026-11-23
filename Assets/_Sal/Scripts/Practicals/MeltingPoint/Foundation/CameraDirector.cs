using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Moves the one real camera between named "shots" (Wide, Mortar, Rig...) with a smooth glide.
// Each shot is just an empty GameObject in the scene: move/rotate it in the Scene view
// to change what the student sees. Select the Main Camera... no, select the anchor!
public class CameraDirector : MonoBehaviour
{
    [Serializable]
    public class Anchor
    {
        public string name;
        public Transform point;
        [Tooltip("Field of view for this shot. 0 = keep the current one.")]
        public float fov = 0f;
    }

    public Camera cam;
    public List<Anchor> anchors = new List<Anchor>();
    public float defaultDuration = 0.8f;

    public string CurrentAnchorName { get; private set; }
    public bool IsMoving { get; private set; }
    public event Action<string> Arrived;

    Coroutine running;
    Vector2 viewInput,viewVelocity;
    readonly Dictionary<string,Vector2> viewAngles=new Dictionary<string,Vector2>();
    public void StopView(){viewInput=viewVelocity=Vector2.zero;}
    public void SetViewInput(Vector2 value){viewInput=Vector2.ClampMagnitude(value,1);}
    readonly Dictionary<string,float> zooms=new Dictionary<string,float>();
    readonly Dictionary<string,Vector3> inspectionTargets=new Dictionary<string,Vector3>();
    public float ZoomFactor { get { return CurrentAnchorName!=null&&zooms.ContainsKey(CurrentAnchorName)?zooms[CurrentAnchorName]:1f; } }
    public bool IsInspection { get { return CurrentAnchorName!=null&&CurrentAnchorName!="Overview"; } }
    public void SetInspectionTarget(string name,Vector3 target){inspectionTargets[name]=target;}
    public void ResetShot(string name){zooms.Remove(name);viewAngles.Remove(name);StopView();}
    public void ClearZoom(){zooms.Clear();viewAngles.Clear();viewInput=viewVelocity=Vector2.zero;}
    public void Zoom(float direction)
    {
        if(!IsInspection||IsMoving||!inspectionTargets.ContainsKey(CurrentAnchorName))return;
        float min=CurrentAnchorName=="Sample"?.65f:.6f;
        zooms[CurrentAnchorName]=Mathf.Clamp(ZoomFactor+direction*.18f,min,2.5f);
        Apply(Find(CurrentAnchorName));
    }
    public void ResetInspection()
    {
        if(!IsInspection||IsMoving)return;
        zooms[CurrentAnchorName]=1f;Apply(Find(CurrentAnchorName));
    }
    Vector3 Position(Anchor a)
    {
        var centre=Target(a);
        float factor=zooms.ContainsKey(a.name)?zooms[a.name]:1;
        var angle=viewAngles.ContainsKey(a.name)?viewAngles[a.name]:Vector2.zero;
        return centre+Quaternion.Euler(angle.y,angle.x,0)*(a.point.position-centre)*factor;
    }
    Vector3 Target(Anchor a){return inspectionTargets.ContainsKey(a.name)?inspectionTargets[a.name]:a.point.position+a.point.forward*1.6f;}
    Quaternion Rotation(Anchor a){return Quaternion.LookRotation(Target(a)-Position(a),Vector3.up);}
    void LateUpdate()
    {
        if(IsMoving||string.IsNullOrEmpty(CurrentAnchorName))return;
        viewVelocity=Vector2.Lerp(viewVelocity,viewInput,1-Mathf.Exp(-12*Time.deltaTime));
        if(viewVelocity.sqrMagnitude<.00001f)return;
        Vector2 angle=viewAngles.ContainsKey(CurrentAnchorName)?viewAngles[CurrentAnchorName]:Vector2.zero;
        angle+=new Vector2(viewVelocity.x*26,-viewVelocity.y*19)*Time.deltaTime;
        angle.x=Mathf.Clamp(angle.x,-22,22);angle.y=Mathf.Clamp(angle.y,-12,12);viewAngles[CurrentAnchorName]=angle;
        Apply(Find(CurrentAnchorName));
    }

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    Anchor Find(string anchorName)
    {
        return anchors.Find(a => a.name == anchorName);
    }

    public bool Has(string anchorName)
    {
        Anchor a = Find(anchorName);
        return a != null && a.point != null;
    }

    void Apply(Anchor a)
    {
        cam.transform.SetPositionAndRotation(Position(a), Rotation(a));
        if (a.fov > 0f) cam.fieldOfView = a.fov;
    }

    // Jump straight to a shot (no glide).
    public void SnapTo(string anchorName)
    {
        Anchor a = Find(anchorName);
        if (a == null || a.point == null)
        {
            Debug.LogWarning("CameraDirector: no anchor named '" + anchorName + "'");
            return;
        }
        if (running != null) StopCoroutine(running);
        running = null;
        StopView();
        IsMoving = false;
        Apply(a);
        CurrentAnchorName = anchorName;
        if (Arrived != null) Arrived(anchorName);
    }

    // Glide to a shot. duration < 0 uses defaultDuration.
    public void MoveTo(string anchorName, float duration = -1f)
    {
        viewInput=viewVelocity=Vector2.zero;
        if(anchorName=="Overview")viewAngles.Clear();
        Anchor a = Find(anchorName);
        if (a == null || a.point == null)
        {
            Debug.LogWarning("CameraDirector: no anchor named '" + anchorName + "'");
            return;
        }
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Glide(a, anchorName, duration < 0f ? defaultDuration : duration));
    }

    IEnumerator Glide(Anchor a, string anchorName, float duration)
    {
        Vector3 p0 = cam.transform.position;
        Quaternion r0 = cam.transform.rotation;
        float f0 = cam.fieldOfView;
        float f1 = a.fov > 0f ? a.fov : f0;

        IsMoving = true;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            cam.transform.position = Vector3.Lerp(p0, Position(a), k);
            cam.transform.rotation = Quaternion.Slerp(r0, Rotation(a), k);
            cam.fieldOfView = Mathf.Lerp(f0, f1, k);
            yield return null;
        }
        Apply(a);
        IsMoving = false;
        CurrentAnchorName = anchorName;
        running = null;
        if (Arrived != null) Arrived(anchorName);
    }
}
