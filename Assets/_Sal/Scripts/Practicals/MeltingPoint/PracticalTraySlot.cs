using UnityEngine;
using UnityEngine.EventSystems;

// A click retrieves. A drag retrieves and transfers this SAME pointer to the model.
public class PracticalTraySlot : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    public NaphthalenePracticalSession session;
    public Draggable item;
    int owner=int.MinValue;
    Vector2 press;
    bool dragging;
    public void OnPointerDown(PointerEventData e)
    {
        if(owner!=int.MinValue||!session.CanChangeView)return;
        owner=e.pointerId;press=e.position;dragging=false;
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if(e.pointerId!=owner||!item.canDrag||!session.CanInteract)return;
        session.workspace.TakeOut(item.id);
        dragging=session.drag.BeginTrayDrag(item,e.position,e.pointerId);
    }
    public void OnDrag(PointerEventData e) { if(dragging&&e.pointerId==owner)session.drag.MoveHandle(e.position,e.pointerId); }
    public void OnPointerUp(PointerEventData e)
    {
        if(e.pointerId!=owner)return;
        if(dragging)session.drag.EndHandle(e.position,e.pointerId);
        else if(Vector2.Distance(press,e.position)<10f&&session.CanChangeView)session.workspace.TakeOut(item.id);
        dragging=false;owner=int.MinValue;
    }
    public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
    void OnDisable()
    {
        if(dragging&&session!=null&&session.drag.ActivePointer==owner)session.drag.Cancel();
        owner=int.MinValue;dragging=false;
    }
}
