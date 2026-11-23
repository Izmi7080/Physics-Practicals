using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BiologyLab
{
    internal sealed class ExternalAnnotationsController : IDisposable
    {
        private readonly VisualElement diagram;
        private readonly FrogPreviewController preview;
        private readonly List<Marking> markings = new List<Marking>();
        private AnnotationLines lines;
        private Label dorsal, ventral;
        private bool visible;
        private SkinnedMeshRenderer renderer;
        private Mesh mesh;

        public ExternalAnnotationsController(VisualElement diagram, FrogPreviewController preview)
        { this.diagram = diagram; this.preview = preview; }
        public void Init()
        {
            lines = new AnnotationLines(); lines.AddToClassList("anatomy-annotations");
            lines.pickingMode = PickingMode.Ignore; diagram.Add(lines);
            dorsal = new Label("Dorsal side (Upper Side)"); dorsal.AddToClassList("side-caption"); diagram.Add(dorsal);
            ventral = new Label("Ventral side (Lower Side)"); ventral.AddToClassList("side-caption"); diagram.Add(ventral);
        }
        public void Start()
        {
            renderer = preview.Skin.GetComponentInChildren<SkinnedMeshRenderer>();
            mesh = new Mesh(); renderer.BakeMesh(mesh);
            Add("Eye", new Vector3(1.90f,5.72f,4.98f), new Vector2(.82f,.18f), new Vector2(.24f,.28f));
            Add("External\nNostrils", new Vector3(.65f,5.30f,6.85f), new Vector2(.84f,.43f), new Vector2(.76f,.28f));
            Add("Nictitating\nMembrane", new Vector3(2.0089f,5.2347f,4.5863f), new Vector2(.78f,.65f), new Vector2(.80f,.65f));
            Add("Thigh", new Vector3(3.7f,3.1f,-2.6f), new Vector2(.12f,.56f), new Vector2(.17f,.38f));
            Add("Foot", new Vector3(4.2f,.85f,-5.2f), new Vector2(.20f,.83f), new Vector2(.20f,.72f));
            Add("Upper Arm", new Vector3(3.05f,2.7f,1.65f), new Vector2(.44f,.85f), new Vector2(.44f,.74f));
            Add("Fore Arm", new Vector3(4.1f,1.2f,2.0f), new Vector2(.67f,.85f), new Vector2(.80f,.80f));
            UnityEngine.Object.Destroy(mesh); mesh = null;
            SetVisible(false);
        }
        private void Add(string text, Vector3 sourcePoint, Vector2 landscape, Vector2 portrait)
        {
            Vector3 target = preview.Skin.transform.TransformPoint(sourcePoint);
            Vector3[] vertices = mesh.vertices;
            int nearest = 0; float distance = float.PositiveInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float value = (renderer.transform.TransformPoint(vertices[i]) - target).sqrMagnitude;
                if (value < distance) { distance = value; nearest = i; }
            }
            var label = new Label(text); label.AddToClassList("anatomy-marker"); label.pickingMode = PickingMode.Ignore; diagram.Add(label);
            markings.Add(new Marking(label, new SkinnedSurfaceAttachment(renderer, nearest, text), landscape, portrait));
        }
        public void SetVisible(bool value)
        {
            visible = value;
            DisplayStyle display = value ? DisplayStyle.Flex : DisplayStyle.None;
            lines.style.display = display; dorsal.style.display = display; ventral.style.display = display;
            foreach (Marking marking in markings) marking.Label.style.display = display;
        }
        public void Tick()
        {
            if (!visible || diagram.contentRect.width < 10f || diagram.contentRect.height < 10f) return;
            float width = diagram.contentRect.width, height = diagram.contentRect.height;
            bool portrait = width < height;
            float labelWidth = portrait ? Mathf.Min(150f,width*.32f) : Mathf.Min(220f,width*.19f);
            float labelHeight = portrait ? 66f : height < 450f ? 54f : 60f;
            lines.Leaders.Clear();
            foreach (Marking marking in markings)
            {
                Vector2 slot = portrait ? marking.Portrait : marking.Landscape;
                Vector2 center = new Vector2(width*slot.x, height*slot.y);
                if (!portrait && slot.y > .75f) center.y = Mathf.Min(center.y,height-90f);
                marking.Label.style.width = labelWidth; marking.Label.style.height = labelHeight;
                marking.Label.style.left = Mathf.Clamp(center.x-labelWidth*.5f,0f,width-labelWidth);
                marking.Label.style.top = Mathf.Clamp(center.y-labelHeight*.5f,0f,height-labelHeight);
                Vector3 viewport = preview.ViewCamera.WorldToViewportPoint(marking.Attachment.UpdatePosition());
                Vector2 anchor = new Vector2(viewport.x*width,(1f-viewport.y)*height);
                // End at the edge of the label, not through its text.
                Vector2 direction = center-anchor;
                float ratio = Mathf.Min(labelWidth*.5f/Mathf.Max(.001f,Mathf.Abs(direction.x)),labelHeight*.5f/Mathf.Max(.001f,Mathf.Abs(direction.y)));
                Vector2 end = center-direction*Mathf.Min(1f,ratio);
                lines.Leaders.Add(new AnnotationLines.Leader(anchor,end));
            }
            float captionWidth = portrait ? width*.96f : width*.65f;
            float captionLeft = (width-captionWidth)*.5f;
            dorsal.style.left = captionLeft; dorsal.style.top = height*.015f; dorsal.style.width = captionWidth; dorsal.style.height = 38f;
            ventral.style.left = captionLeft; ventral.style.top = height-42f; ventral.style.width = captionWidth; ventral.style.height = 38f;
            lines.BracketLeft = width*.28f; lines.BracketRight = width*.70f;
            lines.TopBracket = Mathf.Max(52f,height*(portrait?.11f:.09f)); lines.BottomBracket = height-62f;
            lines.MarkDirtyRepaint();
        }
        public void Dispose()
        {
            foreach (Marking marking in markings) { marking.Attachment.Dispose(); marking.Label.RemoveFromHierarchy(); }
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            lines.Dispose(); lines.RemoveFromHierarchy();
            dorsal.RemoveFromHierarchy(); ventral.RemoveFromHierarchy();
        }
        private sealed class Marking
        {
            public readonly Label Label;
            public readonly SkinnedSurfaceAttachment Attachment;
            public readonly Vector2 Landscape, Portrait;
            public Marking(Label label, SkinnedSurfaceAttachment attachment, Vector2 landscape, Vector2 portrait)
            { Label=label; Attachment=attachment; Landscape=landscape; Portrait=portrait; }
        }
    }

    // Evaluate just the seven selected vertices, rather than baking the entire skin every frame.
    internal sealed class SkinnedSurfaceAttachment : IDisposable
    {
        private readonly Transform[] bones;
        private readonly Matrix4x4[] bindposes;
        private readonly BoneWeight weight;
        private readonly Vector3 vertex;
        private readonly GameObject anchor;
        public SkinnedSurfaceAttachment(SkinnedMeshRenderer renderer, int vertexIndex, string name)
        {
            bones = renderer.bones; bindposes = renderer.sharedMesh.bindposes;
            weight = renderer.sharedMesh.boneWeights[vertexIndex]; vertex = renderer.sharedMesh.vertices[vertexIndex];
            anchor = new GameObject(name.Replace("\n"," ")+" Surface Anchor");
            anchor.transform.SetParent(bones[weight.boneIndex0],false);
        }
        private Vector3 Contribution(int index, float value)
        { return value > 0f ? bones[index].TransformPoint(bindposes[index].MultiplyPoint3x4(vertex))*value : Vector3.zero; }
        public Vector3 UpdatePosition()
        {
            Vector3 position = Contribution(weight.boneIndex0,weight.weight0)+Contribution(weight.boneIndex1,weight.weight1)
                +Contribution(weight.boneIndex2,weight.weight2)+Contribution(weight.boneIndex3,weight.weight3);
            anchor.transform.position = position;
            return position;
        }
        public void Dispose() { if (anchor != null) UnityEngine.Object.Destroy(anchor); }
    }

    internal sealed class AnnotationLines : VisualElement, IDisposable
    {
        public readonly List<Leader> Leaders = new List<Leader>();
        public float BracketLeft, BracketRight, TopBracket, BottomBracket;
        public AnnotationLines() { generateVisualContent += Draw; }
        private void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            painter.strokeColor = new Color(.1f,.4f,.51f); painter.lineWidth = 3f;
            foreach (Leader line in Leaders)
            {
                painter.BeginPath(); painter.MoveTo(line.Anchor); painter.LineTo(line.End); painter.Stroke();
                painter.fillColor = painter.strokeColor;
                painter.BeginPath(); painter.Arc(line.Anchor,5f,0f,360f); painter.Fill();
            }
            painter.strokeColor = new Color(.28f,.19f,.24f); painter.lineWidth = 4f;
            DrawBracket(painter,TopBracket,14f); DrawBracket(painter,BottomBracket,-14f);
        }
        private void DrawBracket(Painter2D painter, float y, float offset)
        {
            painter.BeginPath(); painter.MoveTo(new Vector2(BracketLeft,y+offset)); painter.LineTo(new Vector2(BracketLeft,y));
            painter.LineTo(new Vector2(BracketRight,y)); painter.LineTo(new Vector2(BracketRight,y+offset)); painter.Stroke();
        }
        public void Dispose() { generateVisualContent -= Draw; Leaders.Clear(); }
        public readonly struct Leader
        {
            public readonly Vector2 Anchor, End;
            public Leader(Vector2 anchor, Vector2 end) { Anchor=anchor; End=end; }
        }
    }
}
