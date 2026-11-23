using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BiologyLab
{
    // Both organ points and staggered card positions belong to the specimen; text faces the camera for readability.
    internal sealed class OrganLabelingController : IDisposable
    {
        private readonly VisualElement root;
        private readonly FrogPreviewController preview;
        private readonly List<Target> targets = new List<Target>();
        private readonly List<Action> cleanup = new List<Action>();
        private readonly List<ButtonFeedback> feedback = new List<ButtonFeedback>();
        private static readonly float[] Stagger = {0f,.045f,-.045f,.025f,-.02f,.035f,-.04f,.02f,-.01f};
        private VisualElement diagram;
        private ScrollView bank;
        private Label score, ghost, status;
        private OrganLeaderLines lines;
        private GameObject anchors;
        private Vector3 initialPosition, frameCenter;
        private float frameSize;
        private Button dragSource;
        private string dragging, selected;
        private Target dragOrigin, selectedOrigin;
        private Vector2 dragStart;
        private int pointer = -1;
        private bool visible, activeDrag, touch;
        public OrganLabelingController(VisualElement root, FrogPreviewController preview) { this.root=root;this.preview=preview; }
        public void Init()
        {
            diagram=root.Q("diagram");bank=root.Q<ScrollView>("organ-labels");score=root.Q<Label>("label-score");status=root.Q<Label>("internal-note");
            lines=new OrganLeaderLines();lines.AddToClassList("organ-leaders");lines.pickingMode=PickingMode.Ignore;diagram.Add(lines);
            ghost=new Label();ghost.AddToClassList("drag-ghost");ghost.pickingMode=PickingMode.Ignore;root.Add(ghost);ghost.style.display=DisplayStyle.None;
        }
        public void Start()
        {
            anchors=new GameObject("Organ Label World Attachments");anchors.transform.SetParent(preview.Anatomy.transform,false);
            initialPosition=preview.Anatomy.transform.position;
            // Coordinates are in the imported anatomy's local space. Chamber points share the combined Heart mesh.
            Add("Right Atrium",new Vector3(-.44f,5.75f,10.18f),1,true);
            Add("Left Atrium",new Vector3(.43f,5.76f,10.18f),1,false);
            Add("Ventricle",new Vector3(.03f,5.90f,9.15f),3,true);
            Add("Pulmonary Artery",new Vector3(.60f,4.68f,10.85f),0,true);
            Add("Pulmonary Vein",new Vector3(-.73f,4.36f,9.90f),0,false);
            Add("Liver",new Vector3(.94f,5.50f,7.85f),3,false);
            Add("Right Lung",new Vector3(-1.43f,4.65f,9.65f),2,true);
            Add("Left Lung",new Vector3(1.42f,4.68f,9.65f),2,false);
            Add("Right Kidney",new Vector3(-.49f,3.12f,5.05f),4,true);
            Add("Left Kidney",new Vector3(.50f,3.13f,5.05f),4,false);
            Add("Stomach",new Vector3(1.60f,4.95f,7.10f),5,true);
            Add("Small Intestine",new Vector3(-.78f,4.89f,6.03f),5,false);
            Add("Large Intestine",new Vector3(.02f,4.77f,4.20f),6,true);
            Add("Fat Bodies",new Vector3(1.25f,3.72f,6.51f),6,false);
            Add("Gall Bladder",new Vector3(-1.13f,5.19f,7.16f),7,true);
            Add("Spleen",new Vector3(.56f,5.40f,5.65f),7,false);
            Add("Bladder",new Vector3(-.24f,4.90f,3.01f),8,true);
            Add("Cloaca",new Vector3(.03f,4.55f,2.43f),8,false);
            // Read gestures before Button's Clickable consumes pointer events.
            root.RegisterCallback<PointerMoveEvent>(Move,TrickleDown.TrickleDown);root.RegisterCallback<PointerUpEvent>(Up,TrickleDown.TrickleDown);root.RegisterCallback<PointerCancelEvent>(Cancel,TrickleDown.TrickleDown);
            root.Q<Button>("reset-labels").clicked+=Reset;
            SetVisible(false);
        }
        private void Add(string name,Vector3 point,int row,bool left)
        {
            var anchor=new GameObject(name+" Anchor");anchor.transform.SetParent(anchors.transform,false);anchor.transform.localPosition=point;
            var card=new GameObject(name+" Label Card Anchor");card.transform.SetParent(anchors.transform,false);
            var box=new Button { text="Drop label",name="target-"+name.Replace(" ","-").ToLowerInvariant() };box.AddToClassList("organ-target");diagram.Add(box);
            var source=new Button { text=name,name="label-"+name.Replace(" ","-").ToLowerInvariant(),tooltip="Drag into a box; touch: drag sideways, swipe vertically to scroll" };source.AddToClassList("organ-source");bank.Add(source);
            var target=new Target(name,anchor.transform,card.transform,box,source,row,left);targets.Add(target);
            RegisterDrag(source,target,false); RegisterDrag(box,target,true);
            Action choose=()=>{if(!activeDrag){selected=name;selectedOrigin=null;status.text="Selected: "+name+" • Drag, or tap a target box";}};
            source.clicked+=choose;cleanup.Add(()=>source.clicked-=choose);
            Action drop=()=>{if(selected!=null){Submit(target,selected,selectedOrigin);selected=null;selectedOrigin=null;}else if(!target.Correct&&target.Value!=null){selected=target.Value;selectedOrigin=target;}};box.clicked+=drop;cleanup.Add(()=>box.clicked-=drop);
            foreach(Button button in new[]{box,source}){var child=new ButtonFeedback(button);child.Init();child.Start();feedback.Add(child);}
        }
        private void RegisterDrag(Button button,Target target,bool placed)
        {
            EventCallback<PointerDownEvent> down=evt=>Down(evt,target,button,placed);
            button.RegisterCallback(down,TrickleDown.TrickleDown);
            button.RegisterCallback<PointerMoveEvent>(Move,TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(Up,TrickleDown.TrickleDown);
            button.RegisterCallback<PointerCancelEvent>(Cancel,TrickleDown.TrickleDown);
            cleanup.Add(()=>{button.UnregisterCallback(down,TrickleDown.TrickleDown);button.UnregisterCallback<PointerMoveEvent>(Move,TrickleDown.TrickleDown);button.UnregisterCallback<PointerUpEvent>(Up,TrickleDown.TrickleDown);button.UnregisterCallback<PointerCancelEvent>(Cancel,TrickleDown.TrickleDown);});
        }
        public void SetVisible(bool value)
        {
            visible=value;lines.style.display=value?DisplayStyle.Flex:DisplayStyle.None;
            foreach(Target target in targets)target.Box.style.display=value?DisplayStyle.Flex:DisplayStyle.None;
            CancelDrag();
            if(value)UpdateScore();
        }
        private void Down(PointerDownEvent evt,Target target,Button button,bool placed)
        {
            if(!visible||evt.button!=0||pointer>=0||placed&&(target.Correct||target.Value==null))return;
            dragging=placed?target.Value:target.Name;dragOrigin=placed?target:null;dragSource=button;pointer=evt.pointerId;dragStart=evt.position;touch=evt.pointerType!="mouse";activeDrag=false;
        }
        private void Move(PointerMoveEvent evt)
        {
            if(pointer!=evt.pointerId||dragSource==null)return;
            Vector2 position=evt.position,difference=position-dragStart;
            if(!activeDrag)
            {
                if(difference.sqrMagnitude<64f)return;
                // Let a vertical finger swipe scroll the bank; a sideways movement begins the label drag.
                if(touch&&dragOrigin==null&&Mathf.Abs(difference.y)>Mathf.Abs(difference.x)*1.4f){CancelDrag();return;}
                activeDrag=true;dragSource.CapturePointer(pointer);ghost.text=dragging;ghost.style.display=DisplayStyle.Flex;
            }
            Vector2 local=root.WorldToLocal(position);ghost.style.left=local.x-85f;ghost.style.top=local.y-22f;
            foreach(Target target in targets)target.Box.EnableInClassList("drop-hover",!target.Correct&&target.Box.worldBound.Contains(position));
            evt.StopPropagation();
        }
        private void Up(PointerUpEvent evt)
        {
            if(evt.pointerId!=pointer)return;
            if(activeDrag)
            {
                bool dropped=false;
                foreach(Target target in targets)if(!target.Correct&&target.Box.resolvedStyle.display!=DisplayStyle.None&&target.Box.worldBound.Contains(evt.position)){Submit(target,dragging,dragOrigin);dropped=true;break;}
                if(!dropped&&dragOrigin!=null&&bank.worldBound.Contains(evt.position)){Clear(dragOrigin);RefreshBank();UpdateScore();}
                evt.StopPropagation();
            }
            CancelDrag();
        }
        private void Cancel(PointerCancelEvent evt){if(evt.pointerId==pointer)CancelDrag();}
        private void CancelDrag()
        {
            if(activeDrag&&dragSource!=null&&pointer>=0&&dragSource.HasPointerCapture(pointer))dragSource.ReleasePointer(pointer);
            pointer=-1;dragSource=null;dragging=null;dragOrigin=null;activeDrag=false;ghost.style.display=DisplayStyle.None;
            foreach(Target target in targets)target.Box.RemoveFromClassList("drop-hover");
        }
        private void Submit(Target target,string value,Target origin=null)
        {
            if(target.Correct||target==origin)return;
            if(origin!=null)Clear(origin);
            target.Value=value;
            target.Box.text=value;
            target.Correct=string.Equals(target.Name,value,StringComparison.Ordinal);
            target.Box.EnableInClassList("correct",target.Correct);target.Box.EnableInClassList("incorrect",!target.Correct);
            if(target.Correct)
            {status.text="Correct: "+value;}
            else{status.text="Move the red label to another box to try again.";}
            RefreshBank();UpdateScore();
        }
        private static void Clear(Target target)
        {target.Value=null;target.Correct=false;target.Box.text="Drop label";target.Box.RemoveFromClassList("correct");target.Box.RemoveFromClassList("incorrect");}
        private void RefreshBank()
        {
            foreach(Target source in targets)
            {
                bool used=false;foreach(Target placement in targets)if(placement.Value==source.Name){used=true;break;}
                source.Source.style.display=used?DisplayStyle.None:DisplayStyle.Flex;
            }
        }
        public void Reset()
        {
            CancelDrag();selected=null;selectedOrigin=null;
            foreach(Target target in targets)
            {Clear(target);target.Source.SetEnabled(true);target.Source.RemoveFromClassList("used");}
            bank.scrollOffset=Vector2.zero;RefreshBank();status.text="Drag labels into the organ boxes.";UpdateScore();
        }
        private void UpdateScore()
        {
            int count=0;foreach(Target target in targets)if(target.Correct)count++;
            score.text=count+" / 18 correct";
            if(count==18)status.text="All 18 organs correctly labeled!";
        }
        public void SetFrame(Vector3 center,float size) { frameCenter=center-initialPosition;frameSize=size; }
        public void Tick(float delta)
        {
            foreach(ButtonFeedback child in feedback)child.Tick(delta);
            if(!visible)return;
            lines.Leaders.Clear();
            Image image=preview.ViewImage;Rect rect=image.contentRect;
            if(rect.width<10f||rect.height<10f)return;
            Vector2 offset=diagram.WorldToLocal(image.LocalToWorld(rect.position));
            float boxWidth=Mathf.Clamp(rect.width*.23f,105f,180f),boxHeight=38f;
            boxHeight=Mathf.Min(boxHeight,rect.height*.087f);
            foreach(Target target in targets)
            {
                Vector3 anchor=preview.ViewCamera.WorldToViewportPoint(target.Anchor.position);
                int row=target.Row;
                float variation=Stagger[row];
                float x=target.Left?.18f+variation:.82f-variation;
                Vector2 center=offset+new Vector2(x*rect.width,rect.height*(.085f+row*.10f+(target.Left?0f:.018f)));
                // Keep the lower left boxes outside the zoom pad on short landscape displays.
                if(Screen.width>Screen.height&&row>=6&&center.y+boxHeight*.5f>diagram.contentRect.height-150f&&target.Left)
                    center.x=Mathf.Max(center.x,152f+boxWidth*.5f);
                // Convert the initial, responsive stagger into specimen-local world positions.
                // Never inverse-transform through the current rotation: that would pin the cards to the screen.
                float slotX=(center.x-offset.x)/rect.width,slotY=(center.y-offset.y)/rect.height;
                float aspect=rect.width/rect.height;
                target.CardAnchor.localPosition=frameCenter+new Vector3((slotX*2f-1f)*frameSize*aspect,0f,(1f-slotY*2f)*frameSize);
                Vector3 card=preview.ViewCamera.WorldToViewportPoint(target.CardAnchor.position);
                center=offset+new Vector2(card.x*rect.width,(1f-card.y)*rect.height);
                // Screen controls stay fixed. Nudge a rotating card clear of the lower-left zoom pad.
                if(Screen.width>Screen.height&&center.y+boxHeight*.5f>diagram.contentRect.height-112f&&center.x-boxWidth*.5f<112f)
                    center.x=120f+boxWidth*.5f;
                bool inView=anchor.z>0f&&card.z>0f&&card.x>=0f&&card.x<=1f&&card.y>=0f&&card.y<=1f;
                target.Box.style.display=inView?DisplayStyle.Flex:DisplayStyle.None;
                target.Box.style.left=center.x-boxWidth*.5f;target.Box.style.top=center.y-boxHeight*.5f;
                target.Box.style.width=boxWidth;target.Box.style.height=boxHeight;
                if(inView)
                {
                    Vector2 origin=offset+new Vector2(anchor.x*rect.width,(1f-anchor.y)*rect.height);
                    Vector2 end=center+new Vector2(origin.x>center.x?boxWidth*.5f:-boxWidth*.5f,0f);
                    lines.Leaders.Add(new OrganLeaderLines.Leader(origin,end));
                }
            }
            lines.MarkDirtyRepaint();
        }
        public void Dispose()
        {
            CancelDrag();root.UnregisterCallback<PointerMoveEvent>(Move,TrickleDown.TrickleDown);root.UnregisterCallback<PointerUpEvent>(Up,TrickleDown.TrickleDown);root.UnregisterCallback<PointerCancelEvent>(Cancel,TrickleDown.TrickleDown);
            root.Q<Button>("reset-labels").clicked-=Reset;
            foreach(Action action in cleanup)action();foreach(ButtonFeedback child in feedback)child.Dispose();
            foreach(Target target in targets){target.Box.RemoveFromHierarchy();target.Source.RemoveFromHierarchy();}
            lines.Dispose();lines.RemoveFromHierarchy();ghost.RemoveFromHierarchy();UnityEngine.Object.Destroy(anchors);
        }
        private sealed class Target
        {
            public readonly string Name;public readonly Transform Anchor,CardAnchor;public readonly Button Box,Source;
            public readonly int Row;public readonly bool Left;
            public bool Correct;public string Value;
            public Target(string name,Transform anchor,Transform card,Button box,Button source,int row,bool left){Name=name;Anchor=anchor;CardAnchor=card;Box=box;Source=source;Row=row;Left=left;}
        }
    }
    internal sealed class OrganLeaderLines : VisualElement,IDisposable
    {
        public readonly List<Leader> Leaders=new List<Leader>();
        public OrganLeaderLines(){generateVisualContent+=Draw;}
        private void Draw(MeshGenerationContext context)
        {
            Painter2D painter=context.painter2D;painter.lineWidth=2f;painter.strokeColor=new Color(.38f,.35f,.55f);
            foreach(Leader leader in Leaders)
            {
                painter.BeginPath();painter.MoveTo(leader.Anchor);painter.LineTo(leader.Junction ?? new Vector2((leader.Anchor.x+leader.End.x)*.5f,leader.End.y));painter.LineTo(leader.End);painter.Stroke();
                painter.fillColor=new Color(.55f,.55f,.78f);painter.BeginPath();painter.Arc(leader.Anchor,leader.DotRadius,0f,360f);painter.Fill();
            }
        }
        public void Dispose(){generateVisualContent-=Draw;Leaders.Clear();}
        public readonly struct Leader
        {public readonly Vector2 Anchor,End;public readonly Vector2? Junction;public readonly float DotRadius;public Leader(Vector2 anchor,Vector2 end,Vector2? junction=null,float dotRadius=3.5f){Anchor=anchor;End=end;Junction=junction;DotRadius=dotRadius;}}
    }
}
