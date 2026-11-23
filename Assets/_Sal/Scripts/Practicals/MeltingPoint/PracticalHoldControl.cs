using UnityEngine;
using UnityEngine.EventSystems;

// A deliberate press-and-hold action; releasing, leaving or hiding the control stops it.
public class PracticalHoldControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public NaphthalenePracticalSession session;
    public int OwnerPointer { get; private set; } = int.MinValue;
    public void OnPointerDown(PointerEventData data)
    {if(OwnerPointer!=int.MinValue||session==null||(session.At("heat")&&session.HandsFreeStirring))return;session.SetToolHeld(true);if(session.ToolHeld)OwnerPointer=data.pointerId;}
    public void OnPointerUp(PointerEventData data){if(data.pointerId==OwnerPointer)Stop();}
    public void OnPointerExit(PointerEventData data){if(data.pointerId==OwnerPointer)Stop();}
    void OnDisable(){Stop();}
    void Stop(){OwnerPointer=int.MinValue;if(session!=null)session.SetToolHeld(false);}
}
