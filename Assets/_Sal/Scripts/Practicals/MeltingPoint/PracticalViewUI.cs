using UnityEngine;
using UnityEngine.UI;

public partial class PracticalUIController
{
    RectTransform viewJoystick;
    Button viewOptions;
    void BuildViewJoystick()
    {
        viewJoystick=Rect("View joystick",run,new Vector2(.125f,.258f),new Vector2(.125f,.258f));viewJoystick.sizeDelta=new Vector2(90,90);
        var image=viewJoystick.gameObject.AddComponent<Image>();image.sprite=circleSprite;image.color=new Color(.10f,.38f,.40f,.65f);
        var knob=Rect("View knob",viewJoystick,new Vector2(.5f,.5f),new Vector2(.5f,.5f));knob.sizeDelta=new Vector2(64,64);
        var centre=knob.gameObject.AddComponent<Image>();centre.sprite=circleSprite;centre.color=new Color(.65f,.90f,.91f,.8f);centre.raycastTarget=false;
        var arrows=Rect("View arrows",viewJoystick,new Vector2(.12f,.12f),new Vector2(.88f,.88f)).gameObject.AddComponent<Image>();arrows.sprite=EmojiSprite("view");arrows.color=new Color(1,1,1,.8f);arrows.raycastTarget=false;
        var control=viewJoystick.gameObject.AddComponent<PracticalViewJoystick>();control.session=session;control.knob=knob;
        Label(viewJoystick,"View",new Vector2(0,-.28f),new Vector2(1,0),20).alignment=TextAnchor.MiddleCenter;
    }
    void RefreshViewJoystick(){viewJoystick.gameObject.SetActive(viewOptionsOpen && session.cameraDirector.IsInspection && !PageVisible);}
}
