using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BiologyLab
{
    // Read-only study labels have specimen-local endpoints; their text faces the viewer.
    internal sealed class OrganSystemAnnotationsController : IDisposable
    {
        private readonly VisualElement root;
        private readonly FrogPreviewController preview;
        private readonly List<Attachment> attachments = new List<Attachment>();
        private readonly Dictionary<MeshFilter, MeshCollider> surfaceQueries = new Dictionary<MeshFilter, MeshCollider>();
        private VisualElement diagram;
        private OrganLeaderLines lines;
        private GameObject anchors;
        private bool visible, female;
        private readonly bool respiratory, circulatory, nervous, urogenital;
        private string SystemName => urogenital ? "Urogenital" : nervous ? "Nervous" : circulatory ? "Circulatory" : respiratory ? "Respiratory" : "Digestive";
        private Vector3 localCenter;
        private Bounds focusBounds, femaleBounds;
        public Bounds FocusBounds => urogenital && female ? femaleBounds : focusBounds;
        public OrganSystemAnnotationsController(VisualElement root, FrogPreviewController preview, bool respiratory = false, bool circulatory = false, bool nervous = false, bool urogenital = false)
        { this.root = root; this.preview = preview; this.respiratory = respiratory; this.circulatory = circulatory; this.nervous = nervous; this.urogenital = urogenital; }
        public void Init()
        {
            diagram = root.Q("diagram");
            lines = new OrganLeaderLines { pickingMode = PickingMode.Ignore };
            lines.AddToClassList("organ-leaders"); diagram.Add(lines);
        }
        public void Start()
        {
            anchors = new GameObject(SystemName + " System World Attachments");
            anchors.transform.SetParent(preview.Anatomy.transform, false);
            Transform organs = preview.Anatomy.transform.Find("Internal Organs");
            focusBounds = organs.Find("Stomach and Intestines").GetComponent<Renderer>().bounds;
            if (respiratory)
            {
                focusBounds = preview.Anatomy.transform.Find("Respiratory Head").GetComponentInChildren<Renderer>().bounds;
                foreach (Renderer renderer in preview.Anatomy.transform.Find("Respiratory Head").GetComponentsInChildren<Renderer>()) { var expanded = focusBounds; expanded.Encapsulate(renderer.bounds); focusBounds = expanded; }
                foreach (string name in new[] { "Lung", "Lung 1" }) { var expanded = focusBounds; expanded.Encapsulate(organs.Find(name).GetComponent<Renderer>().bounds); focusBounds = expanded; }
            }
            if (circulatory)
            {
                focusBounds = organs.Find("Heart").GetComponent<Renderer>().bounds;
                foreach (string name in new[] { "Vein", "Vein 1", "Pulmonary Vein", "Abdominal Vein" }) { var expanded = focusBounds; expanded.Encapsulate(organs.Find(name).GetComponent<Renderer>().bounds); focusBounds = expanded; }
            }
            if (nervous)
            {
                focusBounds = organs.Find("Spinal Cord").GetComponent<Renderer>().bounds;
                foreach (string name in new[] { "Cerebrum", "Cerebellum", "Diencephalon", "Hypothalamus", "Olfactory Lobe", "Olfactory Lobe 1", "Optic Lobe", "Optic Lobe 1" })
                { var expanded = focusBounds; expanded.Encapsulate(organs.Find(name).GetComponent<Renderer>().bounds); focusBounds = expanded; }
            }
            if (urogenital)
            {
                focusBounds = organs.Find("Kidney").GetComponent<Renderer>().bounds;
                foreach (string name in new[] { "Kidney 1", "Fat Body", "Fat Body 1", "Wolffian Duct", "Wolffian Duct 1", "Urinary Bladder" })
                    focusBounds.Encapsulate(organs.Find(name).GetComponent<Renderer>().bounds);
                Transform cloaca = preview.Anatomy.transform.Find("Urogenital Cloaca (Schematic Segment)");
                focusBounds.Encapsulate(cloaca.GetComponent<Renderer>().bounds);
                Transform femaleParts = preview.Anatomy.transform.Find(FemaleAnatomyModel.RootName);
                femaleBounds = focusBounds;
                foreach (Renderer renderer in femaleParts.GetComponentsInChildren<Renderer>(true))
                    femaleBounds.Encapsulate(renderer.bounds);

                // The specimen is viewed ventrally. Its anatomical right appears on the left of the screen.
                Add("Right fat bodies", new Vector3(-1.14f,3.45f,6.46f),true,.22f);
                Add("Left fat bodies", new Vector3(1.14f,3.45f,6.43f),false,.22f);
                Add("Right kidney", new Vector3(-.38f,3.15f,5.13f),true,.44f);
                Add("Left kidney", new Vector3(.34f,3.15f,5.13f),false,.44f);
                Add("Right urogenital duct", new Vector3(-.44f,3.75f,3.72f),true,.66f,true);
                Add("Left urogenital duct", new Vector3(.33f,3.75f,3.64f),false,.66f,true);
                Add("Right ureter", new Vector3(-.636f,3.12f,4.005f),true,.66f,false,true);
                Add("Left ureter", new Vector3(.361f,3.11f,3.988f),false,.66f,false,true);
                Add("Urinary bladder", new Vector3(-.25f,4.90f,3.00f),true,.90f);
                Add("Cloaca", new Vector3(0f,4.36f,2.23f),false,.90f, surfaceLandmark:true);
                foreach (string side in new[] { "Right", "Left" })
                {
                    bool leftColumn = side == "Right";
                    AddFemalePart(femaleParts, side + " oviduct funnel", leftColumn, .08f);
                    AddFemalePart(femaleParts, side + " ovary", leftColumn, .33f);
                    AddFemalePart(femaleParts, side + " oviduct", leftColumn, .55f);
                    AddFemalePart(femaleParts, side + " ovisac", leftColumn, .78f, side + " ovisac (uterus)");
                }
                localCenter = preview.Anatomy.transform.InverseTransformPoint(focusBounds.center);
                ReleaseSurfaceQueries(); SetVisible(false); return;
            }
            localCenter = preview.Anatomy.transform.InverseTransformPoint(focusBounds.center);
            if (nervous)
            {
                Add("Olfactory lobe", new Vector3(-.29f,3.59f,13.56f),false,.13f);
                Add("Brain", new Vector3(0f,3.43f,12.42f),true,.20f);
                Add("Optic lobe", new Vector3(.16f,3.22f,11.90f),true,.38f);
                Add("Cerebellum", new Vector3(-.01f,3.14f,11.69f),true,.56f);
                Add("Hypothalamus", new Vector3(-.01f,3.31f,11.64f),false,.56f);
                Add("Spinal cord", new Vector3(-.01f,2.55f,9.16f),false,.78f);
                ReleaseSurfaceQueries(); SetVisible(false); return;
            }
            if (circulatory)
            {
                Add("Pulmonary artery", new Vector3(.60f,4.68f,10.85f),true,.16f);
                Add("Pulmonary vein", new Vector3(-.60f,4.68f,10.85f),false,.16f);
                Add("Left atrium", new Vector3(.43f,5.76f,10.18f),true,.24f);
                Add("Right atrium", new Vector3(-.44f,5.75f,10.18f),false,.24f);
                Add("Aorta", new Vector3(.80f,4.7f,8.65f),true,.34f);
                Add("Ventricle", new Vector3(.03f,5.90f,9.15f),true,.42f);
                Add("Vein", new Vector3(-.20f,4.50f,7.65f),false,.47f);
                Add("Abdominal vein", new Vector3(-.13f,5.0f,6.20f),false,.60f);
                ReleaseSurfaceQueries(); SetVisible(false); return;
            }
            if (respiratory)
            {
                Add("Internal nostrils", new Vector3(-.55f,5.5f,14.9f),false,.06f);
                Add("Eyeball bulge", new Vector3(.76f,5.3f,14.42f),true,.18f);
                Add("Vomerine teeth", new Vector3(0f,5.2f,14.46f),false,.24f);
                Add("Upper jaw", new Vector3(1.42f,5.1f,13.95f),true,.32f);
                Add("Eustachian tube opening", new Vector3(-.98f,5.44f,13.32f),false,.40f);
                Add("Vocal sac opening (male)", new Vector3(.935f,5.44f,12.975f),true,.46f,true);
                Add("Lower jaw", new Vector3(.85f,5.1f,11.785f),true,.60f);
                Add("Tongue", new Vector3(0f,5.44f,12.21f),false,.62f);
                Add("Right lung", new Vector3(-1.43f,4.65f,9.65f),true,.83f);
                Add("Left lung", new Vector3(1.42f,4.68f,9.65f),false,.83f);
                ReleaseSurfaceQueries(); SetVisible(false); return;
            }
            Add("Esophagus", new Vector3(.02f,4.7f,11.8f), false, .08f);
            Add("Three-lobed liver", new Vector3(.94f,5.50f,7.85f), true, .31f);
            Add("Gall bladder", new Vector3(-1.13f,5.19f,7.16f), true, .43f);
            Add("Stomach", new Vector3(1.60f,4.95f,7.10f), false, .45f);
            Add("Pancreas", new Vector3(.71f,4.78f,6.81f), false, .56f);
            Add("Small intestine", new Vector3(-.78f,4.89f,6.03f), true, .57f);
            Add("Large intestine", new Vector3(.02f,4.77f,4.20f), true, .72f);
            Add("Cloaca", new Vector3(.03f,4.55f,2.43f), false, .86f);
            ReleaseSurfaceQueries(); SetVisible(false);
        }
        private void AddFemalePart(Transform femaleParts, string part, bool left, float row, string caption = null)
        {
            Transform landmark = femaleParts.Find(part + "/Surface Landmark");
            Vector3 point = preview.Anatomy.transform.InverseTransformPoint(landmark.position);
            Add(caption ?? part, point, left, row, femaleOnly:true, surfaceLandmark:true);
        }
        private void Add(string name, Vector3 point, bool left, float row, bool maleOnly = false, bool femaleOnly = false,
            bool surfaceLandmark = false)
        {
            string meshName = name == "Three-lobed liver" ? "Liver" : name == "Gall bladder" ? "Gall Bladder" : name == "Pancreas" ? "Pancreas" : "Stomach and Intestines";
            Transform meshPart = preview.Anatomy.transform.Find("Internal Organs").Find(meshName);
            if (respiratory)
            {
                if (name == "Right lung" || name == "Left lung") meshPart = preview.Anatomy.transform.Find("Internal Organs/" + (name == "Right lung" ? "Lung" : "Lung 1"));
                else meshPart = preview.Anatomy.transform.Find("Respiratory Head").GetComponentInChildren<MeshFilter>().transform;
            }
            bool mouth = respiratory && name != "Right lung" && name != "Left lung";
            if (mouth) point.z = 13.84f + (point.z - 13.4f) * 1.2f;
            Vector3 landmark = point;
            if (circulatory)
            {
                string part = name == "Left atrium" || name == "Right atrium" || name == "Ventricle" ? "Heart" : name == "Abdominal vein" ? "Abdominal Vein" : name == "Pulmonary vein" ? "Vein 1" : name == "Pulmonary artery" || name == "Aorta" ? "Vein 1" : "Vein";
                meshPart = preview.Anatomy.transform.Find("Internal Organs/" + part);
            }
            if (nervous)
            {
                string part = name == "Brain" ? "Cerebrum" : name == "Olfactory lobe" ? "Olfactory Lobe 1" : name == "Optic lobe" ? "Optic Lobe" : name == "Spinal cord" ? "Spinal Cord" : name;
                meshPart = preview.Anatomy.transform.Find("Internal Organs/" + part);
            }
            if (urogenital)
            {
                bool right = name.StartsWith("Right");
                string part = name.Contains("fat bodies") ? right ? "Fat Body 1" : "Fat Body"
                    : name.Contains("kidney") ? right ? "Kidney 1" : "Kidney"
                    : name.Contains("urogenital duct") || name.Contains("ureter") ? right ? "Wolffian Duct 1" : "Wolffian Duct"
                    : "Urinary Bladder";
                meshPart = preview.Anatomy.transform.Find("Internal Organs/" + part);
            }
            MeshFilter filter = surfaceLandmark ? null : meshPart.GetComponent<MeshFilter>();
            // Snap onto the supplied surface, rather than leaving a leader floating near the organ.
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
            {
                MeshCollider surface;
                if (!surfaceQueries.TryGetValue(filter, out surface))
                {
                    surface = filter.gameObject.AddComponent<MeshCollider>(); surface.sharedMesh = filter.sharedMesh;
                    surfaceQueries.Add(filter, surface); Physics.SyncTransforms();
                }
                Vector3 worldPoint = preview.Anatomy.transform.TransformPoint(point);
                Vector3 up = preview.Anatomy.transform.TransformDirection(Vector3.up);
                RaycastHit hit;
                bool previousBackfaces = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                bool foundSurface = surface.Raycast(new Ray(worldPoint + up * 20f, -up), out hit, 40f);
                Physics.queriesHitBackfaces = previousBackfaces;
                if (foundSurface)
                    point = preview.Anatomy.transform.InverseTransformPoint(hit.point);
                else
                {
                float closest = float.PositiveInfinity;
                Vector3 requested = point;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 local = preview.Anatomy.transform.InverseTransformPoint(meshPart.TransformPoint(vertex));
                    float distance = (local-requested).sqrMagnitude;
                    if (distance < closest) { closest = distance; point = local; }
                }
                }
            }
            // Preserve the chosen mouth landmark in the visible plane even if a surface ray misses.
            if (mouth || circulatory || nervous) { point.x = landmark.x; point.z = landmark.z; }
            Transform saved = preview.Anatomy.transform.Find("Study Anchor Overrides/" + (SystemName + "/") + name);
            if (saved != null) point = saved.localPosition;
            var anchor = new GameObject(name + " Study Anchor"); anchor.transform.SetParent(anchors.transform, false); anchor.transform.localPosition = point;
            Transform second = null;
            if (respiratory && (name == "Internal nostrils" || name == "Vomerine teeth"))
            {
                var other = new GameObject(name + " 2 Study Anchor"); other.transform.SetParent(anchors.transform, false);
                Transform savedSecond = preview.Anatomy.transform.Find("Study Anchor Overrides/Respiratory/" + name + " 2");
                Vector3 mirrored = point; mirrored.x = Mathf.Abs(point.x) > .05f ? -point.x : point.x + .35f;
                other.transform.localPosition = savedSecond != null ? savedSecond.localPosition : mirrored;
                second = other.transform;
            }
            var card = new GameObject(name + " Study Card"); card.transform.SetParent(anchors.transform, false);
            var label = new Label(name) { pickingMode = PickingMode.Ignore }; label.AddToClassList("system-label"); if (urogenital) label.AddToClassList("urogenital-label"); diagram.Add(label);
            attachments.Add(new Attachment(anchor.transform, card.transform, label, left, row, maleOnly, femaleOnly, second));
        }
        private void ReleaseSurfaceQueries()
        { foreach (MeshCollider surface in surfaceQueries.Values) UnityEngine.Object.Destroy(surface); surfaceQueries.Clear(); }
        public void SetVisible(bool value)
        {
            visible = value; lines.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (Attachment item in attachments) item.Label.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }
        public void SetFemale(bool value) { female = value; }
        public void Tick()
        {
            if (!visible) return;
            Rect rect = preview.ViewImage.contentRect; if (rect.width < 10f || rect.height < 10f) return;
            Camera camera = preview.ViewCamera;
            Vector2 offset = diagram.WorldToLocal(preview.ViewImage.LocalToWorld(rect.position));
            Vector3 center = localCenter;
            float size = Mathf.Max(FocusBounds.extents.z, 3.4f / camera.aspect) * 1.12f;
            float width = Mathf.Clamp(rect.width * .24f, 100f, 175f);
            lines.Leaders.Clear();
            foreach (Attachment item in attachments)
            {
                item.Card.localPosition = center + new Vector3((item.Left ? -1f : 1f) * size * camera.aspect * (Screen.width < Screen.height ? .68f : .48f), 0f, (1f-item.Row*2f)*size);
                Vector3 point = camera.WorldToViewportPoint(item.Anchor.position), card = camera.WorldToViewportPoint(item.Card.position);
                if (urogenital)
                {
                    // Keep captions in readable columns while their endpoints follow the specimen.
                    float column = Screen.width < Screen.height ? .15f : .25f;
                    card.x = item.Left ? column : 1f - column;
                    card.y = 1f - item.Row;
                }
                bool onScreen = !(female && item.MaleOnly) && !(!female && item.FemaleOnly) && card.x >= 0f && card.x <= 1f && card.y >= 0f && card.y <= 1f && point.z > 0f;
                item.Label.style.display = onScreen ? DisplayStyle.Flex : DisplayStyle.None;
                Vector2 position = offset + new Vector2(card.x*rect.width, (1f-card.y)*rect.height);
                item.Label.style.left = position.x-width*.5f; item.Label.style.top = position.y-(circulatory || urogenital ? 14f : 19f);
                item.Label.style.width = width; item.Label.style.height = circulatory || urogenital ? 28f : 38f;
                if (onScreen)
                {
                    Vector2 origin = offset + new Vector2(point.x*rect.width,(1f-point.y)*rect.height);
                    Vector2 end = position + new Vector2(point.x > card.x ? width*.5f : -width*.5f,0f);
                    if (item.Second != null)
                    {
                        Vector3 other = camera.WorldToViewportPoint(item.Second.position);
                        Vector2 otherOrigin = offset + new Vector2(other.x*rect.width,(1f-other.y)*rect.height);
                        Vector2 junction = new Vector2((origin.x+otherOrigin.x)*.5f,end.y);
                        lines.Leaders.Add(new OrganLeaderLines.Leader(origin,end,junction,1.5f));
                        lines.Leaders.Add(new OrganLeaderLines.Leader(otherOrigin,end,junction,1.5f));
                    }
                    else lines.Leaders.Add(new OrganLeaderLines.Leader(origin,end));
                }
            }
            lines.MarkDirtyRepaint();
        }
        public void Dispose()
        { foreach (Attachment item in attachments) item.Label.RemoveFromHierarchy(); lines.Dispose(); lines.RemoveFromHierarchy(); UnityEngine.Object.Destroy(anchors); }
        private sealed class Attachment
        {
            public readonly Transform Anchor, Card, Second; public readonly Label Label; public readonly bool Left, MaleOnly, FemaleOnly; public readonly float Row;
            public Attachment(Transform anchor, Transform card, Label label, bool left, float row, bool maleOnly = false, bool femaleOnly = false, Transform second = null)
            { Anchor=anchor; Card=card; Label=label; Left=left; Row=row; MaleOnly=maleOnly; FemaleOnly=femaleOnly; Second=second; }
        }
    }
}
