using UnityEngine;
public class PracticalSafeArea : MonoBehaviour
{
    Rect last;
    Vector2Int size;
    void Update()
    {
        if (Screen.width<=0||Screen.height<=0)return;
        Rect area=Screen.safeArea;
        if(area==last&&size.x==Screen.width&&size.y==Screen.height)return;
        last=area;size=new Vector2Int(Screen.width,Screen.height);
        var rect=(RectTransform)transform;
        // INTEGRATION (Tajarbagah): ignore an invalid safe area (the Editor can report one larger than
        // the screen after the Game view is resized) and clamp, so the UI never stretches off-screen.
        if(area.width<=0||area.height<=0||area.xMax>Screen.width+1||area.yMax>Screen.height+1)
            area=new Rect(0,0,Screen.width,Screen.height);
        rect.anchorMin=new Vector2(Mathf.Clamp01(area.x/Screen.width),Mathf.Clamp01(area.y/Screen.height));
        rect.anchorMax=new Vector2(Mathf.Clamp01(area.xMax/Screen.width),Mathf.Clamp01(area.yMax/Screen.height));
        rect.offsetMin=rect.offsetMax=Vector2.zero;
    }
}
