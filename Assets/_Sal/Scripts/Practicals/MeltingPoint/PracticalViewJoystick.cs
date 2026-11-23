using UnityEngine;
using UnityEngine.EventSystems;

public class PracticalViewJoystick : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
{
    public NaphthalenePracticalSession session;
    public RectTransform knob;
    int owner=int.MinValue;
    Vector2 value;
    public void OnPointerDown(PointerEventData e){if(owner!=int.MinValue||!session.CanChangeView)return;owner=e.pointerId;OnDrag(e);}
    public void OnDrag(PointerEventData e)
    {
        if(owner!=e.pointerId)return;
        Vector2 local;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out local);
        value=Vector2.ClampMagnitude(local/(((RectTransform)transform).rect.width*.32f),1);knob.anchoredPosition=value*(((RectTransform)transform).rect.width*.23f);
    }
    public void OnPointerUp(PointerEventData e){if(e.pointerId==owner)Release();}
    void Update(){if(!session.CanChangeView){Release();return;}session.cameraDirector.SetViewInput(value);}
    void OnDisable(){Release();}
    void OnApplicationFocus(bool focused){if(!focused)Release();}
    void Release(){owner=int.MinValue;value=Vector2.zero;if(knob!=null)knob.anchoredPosition=Vector2.zero;if(session!=null&&session.cameraDirector!=null)session.cameraDirector.StopView();}
}
