using UnityEngine;
using UnityEngine.EventSystems;

// Each UI gesture owns a pointer; another finger cannot release its object.
public class PracticalDragHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public DragController controller;
    public Draggable item;
    public bool fromDock;
    int owner=int.MinValue;
    public void OnPointerDown(PointerEventData e)
    {
        if(owner!=int.MinValue)return;
        var started=fromDock?controller.BeginTrayDrag(item,e.position,e.pointerId):controller.BeginHandle(item,e.position,e.pointerId);
        if(started)owner=e.pointerId;
    }
    public void OnDrag(PointerEventData e){if(owner==e.pointerId)controller.MoveHandle(e.position,e.pointerId);}
    public void OnPointerUp(PointerEventData e){if(owner!=e.pointerId)return;controller.EndHandle(e.position,e.pointerId);owner=int.MinValue;}
    void OnDisable(){if(owner!=int.MinValue&&controller!=null)controller.Cancel();owner=int.MinValue;}
}
