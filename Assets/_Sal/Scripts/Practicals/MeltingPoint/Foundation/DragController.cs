using UnityEngine;
public class DragController : MonoBehaviour
{
    public static DragController Instance { get; private set; }
    public Camera cam;
    public LayerMask pickMask = ~0;
    public bool inputEnabled = true;
    public float followSpeed = 25f, maxPickDistance = 20f;
    Draggable held;
    Plane plane;
    Vector3 offset, start;
    Vector2 startPointer;
    Vector2 pointer;
    bool handleDrag;
    Transform pressedObject;
    Draggable pressedItem;
    Vector2 pressPosition;
    bool pressMoved;
    public int ActivePointer { get; private set; } = int.MinValue;
    public bool IsHolding { get { return held != null; } }
    public bool HasGesture => held != null || pressedObject != null;
    public Draggable HeldItem { get { return held; } }
    void Awake() { Instance = this; if (cam == null) cam = Camera.main; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    void Update()
    {
        if (!inputEnabled || cam == null) return;
        if(handleDrag)return;
        if (held == null)
        {
            if (PointerInput.Down && !PointerInput.OverUI()) WorldPointerDown(PointerInput.Position);
            if (pressedObject != null)
            {
                if (PointerInput.Up || !PointerInput.Held) WorldPointerUp(PointerInput.Position);
                else WorldPointerMove(PointerInput.Position);
            }
            return;
        }
        if (PointerInput.Up || !PointerInput.Held) { Drop(); return; }
        Drag();
    }
    public void WorldPointerDown(Vector2 position)
    {
        var session=GetComponent<NaphthalenePracticalSession>();
        if (held!=null || session==null || !session.CanChangeView || !cam.pixelRect.Contains(position)) return;
        pressedObject=null;pressedItem=null;pressMoved=false;pressPosition=position;
        if (session.HitWaterTap(position)) { pressedObject = session.tapRoot; return; }
        if (session.HitLooseThread(position)) { pressedObject = session.rubberBand; return; }
        if (session.At("pack") && TryPickPackingTube(session, position, out var tube))
        {
            pressedObject=tube.transform;pressedItem=tube;return;
        }
        RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(position), maxPickDistance, pickMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
        Transform inspectionFallback=null;Draggable fallbackItem=null;
        foreach (var hit in hits)
        {
            var parents=hit.collider.GetComponentsInParent<Draggable>();
            foreach(var d in parents)if(d.canDrag)
            {pressedObject=d.transform;pressedItem=d;return;}
            if(inspectionFallback==null&&session.IsInspectable(hit.transform))
            {inspectionFallback=hit.transform;fallbackItem=parents.Length>0?parents[0]:null;}
        }
        pressedObject=inspectionFallback;pressedItem=fallbackItem;

    }
    bool TryPickPackingTube(NaphthalenePracticalSession session,Vector2 position,out Draggable tube)
    {
        tube=session.BoundItem("capillary");
        if(tube==null||!tube.gameObject.activeInHierarchy)return false;
        var glass=tube.transform.Find("VisibleCapillary");
        if(glass==null||!glass.TryGetComponent<MeshFilter>(out var filter)||filter.sharedMesh==null)return false;
        var bounds=filter.sharedMesh.bounds;
        var bottom=cam.WorldToScreenPoint(glass.TransformPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)));
        var top=cam.WorldToScreenPoint(glass.TransformPoint(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z)));
        if(bottom.z<=cam.nearClipPlane||top.z<=cam.nearClipPlane)return false;
        Vector2 segment=top-bottom;
        float along=Mathf.Clamp01(Vector2.Dot(position-(Vector2)bottom,segment)/Mathf.Max(1f,segment.sqrMagnitude));
        float radius=Mathf.Max(16f,cam.pixelHeight*.027f);
        return (position-((Vector2)bottom+segment*along)).sqrMagnitude<=radius*radius;
    }
    public void WorldPointerMove(Vector2 position)
    {
        if (handleDrag) return;
        if (held!=null) { DragAt(position);return; }
        if (pressedObject==null || Vector2.Distance(pressPosition,position)<10f) return;
        pressMoved=true;
        if (pressedItem==null || !pressedItem.canDrag) return;
        var item=pressedItem;pressedObject=null;pressedItem=null;
        Begin(item,pressPosition);DragAt(position);
    }
    public void WorldPointerUp(Vector2 position)
    {
        if (handleDrag) return;
        if (held!=null) { pointer=position;Drop(position);return; }
        var target=pressedObject;
        bool tap=target!=null&&!pressMoved&&Vector2.Distance(pressPosition,position)<10f;
        pressedObject=null;pressedItem=null;
        if(tap)GetComponent<NaphthalenePracticalSession>().InspectObject(target);
    }
    void Drag()
    {
        pointer=PointerInput.Position;DragAt(pointer);
    }
    void Begin(Draggable d,Vector2 position)
    {
        held=d;d.BeginDrag();start=d.transform.position;startPointer=position;
        plane=new Plane(d.movement==DragMovement.Bench?Vector3.up:cam.transform.forward,start);
        float enter;offset=Vector3.zero;var ray=cam.ScreenPointToRay(position);
        if(plane.Raycast(ray,out enter))offset=start-ray.GetPoint(enter);
        if(d.movement!=DragMovement.Vertical)offset+=Vector3.up*d.liftHeight;
        ShowHints(true);
    }
    public bool BeginTrayDrag(Draggable d,Vector2 position,int pointerId)
    {
        if(!BeginHandle(d,position,pointerId))return false;
        // Unlike grabbing a model, the thumbnail has no world-space grab offset.
        // Project onto the model's movement plane so it follows the retrieving finger.
        offset=d.movement==DragMovement.Vertical?Vector3.zero:Vector3.up*d.liftHeight;
        startPointer=position-new Vector2(11,0);DragAt(position);return true;
    }
    public bool BeginHandle(Draggable d,Vector2 position,int pointerId)
    {
        var session=GetComponent<NaphthalenePracticalSession>();
        if(held!=null||!d.canDrag||session==null||!session.CanInteract)return false;
        handleDrag=true;ActivePointer=pointerId;pointer=position;Begin(d,position);return true;
    }
    public void MoveHandle(Vector2 position,int pointerId)
    {if(handleDrag&&held!=null&&ActivePointer==pointerId){pointer=position;DragAt(position);}}
    public void EndHandle(Vector2 position,int pointerId)
    {if(handleDrag&&held!=null&&ActivePointer==pointerId){pointer=position;Drop();}}
    void DragAt(Vector2 position)
    {
        if(TryDragTarget(position,out var target))
            held.transform.position=Vector3.Lerp(held.transform.position,target,1f-Mathf.Exp(-followSpeed*Time.deltaTime));
    }
    bool TryDragTarget(Vector2 position,out Vector3 target)
    {
        if (held.movement == DragMovement.Vertical)
        {
            float pixels = Mathf.Max(1f, Vector2.Distance(cam.WorldToScreenPoint(start), cam.WorldToScreenPoint(start + Vector3.up)));
            target = start + Vector3.up * ((position.y - startPointer.y) / pixels);
            target.y = Mathf.Clamp(target.y, held.minY, held.maxY);
        }
        else
        {
            float enter; Ray ray = cam.ScreenPointToRay(position);
            if (!plane.Raycast(ray, out enter)){target=default;return false;}
            target = ray.GetPoint(enter) + offset;
            target.y=Mathf.Max(target.y,held.minimumHeight);
        }
        var session=GetComponent<NaphthalenePracticalSession>();
        if(session!=null)target=session.ConstrainDrag(held,target);
        return true;
    }
    void Drop() { Drop(handleDrag?pointer:PointerInput.Position); }
    void Drop(Vector2 position)
    {
        var d=held;
        Vector3 aim=TryDragTarget(position,out var target)?target:d.transform.position;
        held=null;handleDrag=false;ActivePointer=int.MinValue;ShowHints(false);
        if(Vector2.Distance(startPointer,position)<10f)
        {
            d.EndDrag(null);
            return;
        }
        if (d.movement == DragMovement.Vertical)
        {
            d.transform.position=aim;
            d.EndDrag(null);
            return;
        }
        var session = GetComponent<NaphthalenePracticalSession>();
        if(session!=null&&session.TryThermometerDrop(d,position,aim,out var clampTarget))
        {
            d.EndDrag(clampTarget);
            return;
        }
        var waterTarget = session != null ? session.WaterDropAt(d, position) : null;
        if (waterTarget != null && waterTarget.TrySnap(d, waterTarget.transform.position))
        {
            d.EndDrag(waterTarget);
            return;
        }
        SnapZone best = null; float distance = float.MaxValue;
        foreach (var zone in SnapZone.All)
        {
            if (!zone.Accepts(d)) continue;
            float dist = zone.Distance(aim);
            if (dist <= zone.snapRadius && dist < distance) { best = zone; distance = dist; }
        }
        if(best!=null&&!best.TrySnap(d,aim))best=null;
        d.EndDrag(best);
    }
    void ShowHints(bool on)
    {
        foreach (var zone in SnapZone.All)
            zone.SetHighlight(on && StepController.Instance != null && StepController.Instance.ShowHints && held != null && zone.Accepts(held));
    }
    public void Cancel()
    {
        pressedObject=null;pressedItem=null;
        handleDrag=false;ActivePointer=int.MinValue;
        if (held != null) { var d = held; held = null; d.EndDrag(null); }
        ShowHints(false);
    }
    public void SetEnabled(bool value) { if (!value && (held != null || pressedObject!=null)) Cancel(); inputEnabled = value; }
}
