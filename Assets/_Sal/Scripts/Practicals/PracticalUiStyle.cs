using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

internal static class PracticalUiStyle
{
    internal static readonly Color Panel = new(.045f, .13f, .17f, .96f);
    internal static readonly Color Document = new(.033f, .10f, .13f);
    internal static readonly Color ButtonFace = new(.08f, .42f, .44f);
    internal static readonly Color Selected = new(.14f, .60f, .49f);
    internal static readonly Color Entry = new(.91f, .94f, .94f);
    internal static readonly Color EntryHighlight = new(.61f, .91f, .83f);
    internal static readonly Color Ink = new(.045f, .11f, .14f);
    internal static readonly Vector2 ReferenceResolution = new(1600, 900);
    static Sprite navigationFace;
    static Sprite roundedFace;

    internal static void StyleTextFace(Image face)
    {
        if(roundedFace==null)roundedFace=PracticalUIController.RoundedSprite();
        face.sprite=roundedFace;
        face.type=Image.Type.Sliced;
        face.color=ButtonFace;
        face.raycastPadding=new Vector4(-8,-6,-8,-6);
        var sheen=Rect("Glass highlight",face.transform,new Vector2(.035f,.74f),new Vector2(.965f,.96f)).gameObject.AddComponent<Image>();
        sheen.sprite=roundedFace;
        sheen.type=Image.Type.Sliced;
        sheen.color=new Color(1,1,1,.075f);
        sheen.raycastTarget=false;
    }

    internal static void ApplyButtonStates(Button button)
    {
        var colors = button.colors;
        colors.highlightedColor = new Color(.91f, 1f, 1f);
        colors.pressedColor = new Color(.72f, .90f, .94f);
        colors.disabledColor = new Color(.55f, .62f, .65f, .55f);
        button.colors = colors;
    }

    internal static Button[] Navigation(Transform parent, Action clickSound, params (string name, string icon, UnityAction action)[] actions)
    {
        var row = Rect("CornerNavigation", parent, new Vector2(.59f, .89f), new Vector2(.96f, .99f));
        if (navigationFace == null) navigationFace = PracticalUIController.CircleSprite();
        var buttons = new Button[actions.Length];
        for (int i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            Vector2 position = new((i + .5f) / actions.Length, .5f);
            var rect = Rect(action.name, row, position, position);
            rect.sizeDelta = new Vector2(58, 58);
            var face = rect.gameObject.AddComponent<Image>();
            face.sprite = navigationFace;
            face.color = ButtonFace;
            face.raycastPadding = new Vector4(-4, -4, -4, -4);
            var icon = Rect("Icon", rect, new Vector2(.20f, .20f), new Vector2(.80f, .80f)).gameObject.AddComponent<Image>();
            icon.sprite = PracticalUIController.EmojiSprite(action.icon);
            icon.raycastTarget = false;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(()=>{clickSound?.Invoke();action.action();});
            ApplyButtonStates(button);
            buttons[i] = button;
        }
        return buttons;
    }

    internal static ScrollRect VerticalScroll(RectTransform root, RectTransform viewport, RectTransform content)
    {
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 58f;
        viewport.offsetMax = new Vector2(-27, 0);
        var track = Rect("Scroll track", root, Vector2.right, Vector2.one);
        track.offsetMin = new Vector2(-19, 0);
        track.gameObject.AddComponent<Image>().color = Panel;
        var handle = Rect("Scroll thumb", track, Vector2.zero, Vector2.one);
        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle;
        bar.targetGraphic = handle.gameObject.AddComponent<Image>();
        bar.targetGraphic.color = Selected;
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return scroll;
    }

    internal static (Tajurbah_Gah.calc engine, Text result, Text operation, Text target, Button assign)
        Calculator(RectTransform parent, Action assignResult, Action refresh, Action clickSound=null)
    {
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var display=Rect("Calculator display",parent,new Vector2(0,.79f),Vector2.one);
        display.gameObject.AddComponent<Image>().color=Entry;
        var result=Caption(display,"Result","",new Vector2(.04f,0),new Vector2(.96f,.70f),36,TextAnchor.MiddleRight);
        result.resizeTextForBestFit=true;result.resizeTextMinSize=19;result.resizeTextMaxSize=36;
        var operand=Caption(display,"Pending operand","",new Vector2(.04f,.67f),new Vector2(.86f,1),20,TextAnchor.MiddleRight);
        var operation=Caption(display,"Pending operation","",new Vector2(.86f,.67f),new Vector2(.96f,1),20);
        result.color=operand.color=operation.color=Ink;
        var engine=parent.gameObject.AddComponent<Tajurbah_Gah.calc>();
        engine.BindDisplay(result,operand,operation);
        Key("C",0,4,2,()=>{engine.resetCalc();operand.text="";});
        Key("Delete",2,4,2,()=>engine.digitClick("<<<"));
        string[] keys={"7","8","9","÷","4","5","6","×","1","2","3","−",".","0","=","+"};
        for(int i=0;i<keys.Length;i++)
        {
            string key=keys[i];
            Key(key,i%4,3-i/4,1,()=>{
                switch(key)
                {
                    case "÷":engine.opClick("/");break;
                    case "×":engine.opClick("*");break;
                    case "−":engine.opClick("-");break;
                    case "+":
                    case "=":engine.opClick(key);break;
                    default:engine.digitClick(key);break;
                }
            });
        }
        var target=Caption(parent,"Assignment target","",new Vector2(0,.105f),new Vector2(1,.19f),20);
        target.resizeTextForBestFit=true;target.resizeTextMinSize=15;target.resizeTextMaxSize=20;
        var assign=MakeButton(Rect("btnAssign",parent,Vector2.zero,new Vector2(1,.10f)),"Assign result",assignResult);
        return (engine,result,operation,target,assign);

        void Key(string key,int column,int row,int span,Action action)
        {
            float y=.19f+row*.118f;
            var rect=Rect("Calculator key "+key,parent,new Vector2(column*.25f,y),new Vector2((column+span)*.25f,y+.118f));
            rect.offsetMin=new Vector2(2,2);rect.offsetMax=new Vector2(-2,-2);
            MakeButton(rect,key,()=>{action();refresh();});
        }
        Button MakeButton(RectTransform rect,string title,Action action)
        {
            var face=rect.gameObject.AddComponent<Image>();StyleTextFace(face);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=face;
            button.onClick.AddListener(()=>{clickSound?.Invoke();action();});ApplyButtonStates(button);
            Caption(rect,"Label",title,Vector2.zero,Vector2.one,26).fontStyle=FontStyle.Bold;
            return button;
        }
        Text Caption(Transform container,string name,string value,Vector2 min,Vector2 max,int size,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var label=Rect(name,container,min,max).gameObject.AddComponent<Text>();
            label.font=font;label.text=value;label.fontSize=size;label.alignment=alignment;label.color=Color.white;
            label.raycastTarget=false;
            return label;
        }
    }

    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
