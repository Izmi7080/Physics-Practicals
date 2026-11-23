using UnityEngine;

namespace VirtualLab.Physics81
{
    // Retains the scene's existing component identity and pan anchors.
    public sealed class LabBeamBalance : MonoBehaviour
    {
        Transform powerButton, tareButton;
        TextMesh display, powerLabel;
        float zeroOffset, tare, settledFor;
        public bool Powered { get; private set; }
        public float LoadGrams { get; private set; }
        public float Reading => Mathf.Round((LoadGrams + zeroOffset - tare) * 100f) / 100f;
        public bool Settled => Powered && settledFor >= .65f;
        public Transform PowerButton => powerButton;
        public Transform TareButton => tareButton;

        public void InstallDigitalFace(Transform cupSeat, Transform bobSeat)
        {
            var material = transform.Find("Balance plinth").GetComponent<Renderer>().sharedMaterial;
            cupSeat.SetParent(transform, false);
            bobSeat.SetParent(transform, false);
            cupSeat.localPosition = bobSeat.localPosition = new Vector3(0, .060f, .012f);
            foreach (Transform child in transform)
                if (child != cupSeat && child != bobSeat) child.gameObject.SetActive(false);

            Face("Scale body", new Vector3(0, .026f, 0), new Vector3(.218f, .052f, .158f), new Color(.88f, .93f, .93f));
            Face("Digital scale platform", new Vector3(0, .055f, .013f), new Vector3(.205f, .010f, .132f), new Color(.18f, .40f, .43f));
            Face("LCD", new Vector3(-.033f, .026f, -.080f), new Vector3(.119f, .031f, .003f), new Color(.73f, .88f, .73f));
            powerButton = Face("ON / OFF", new Vector3(.063f, .037f, -.082f), new Vector3(.060f, .019f, .006f), new Color(.06f, .36f, .28f));
            tareButton = Face("TARE", new Vector3(.063f, .014f, -.082f), new Vector3(.060f, .021f, .006f), new Color(1f, .70f, .21f));
            display = Write("Mass display", new Vector3(-.033f, .025f, -.083f), .00265f, new Color(.04f, .13f, .09f));
            powerLabel = Write("ON", new Vector3(.063f, .037f, -.086f), .0029f, Color.white);
            Write("TARE", new Vector3(.063f, .014f, -.086f), .0030f, new Color(.12f, .08f, .04f));

            Transform Face(string label, Vector3 position, Vector3 size, Color tint)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = label;
                part.transform.SetParent(transform, false);
                part.transform.localPosition = position;
                part.transform.localScale = size;
                var collider = part.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                var renderer = part.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", tint);
                renderer.SetPropertyBlock(block);
                return part.transform;
            }

            TextMesh Write(string label, Vector3 position, float size, Color color)
            {
                var item = new GameObject(label);
                item.transform.SetParent(transform, false);
                item.transform.localPosition = position;
                var text = item.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                item.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
                text.anchor = TextAnchor.MiddleCenter;
                text.fontSize = 56;
                text.fontStyle = FontStyle.Bold;
                text.characterSize = size;
                text.color = color;
                text.text = label;
                return text;
            }
        }

        public void ResetScale()
        {
            Powered = false;
            zeroOffset = 0;
            tare = 0;
            Load(0);
            Animate(0);
        }

        public void SwitchOn()
        {
            Powered = true;
            settledFor = 0;
        }

        public void BeginWeighing(float offsetGrams)
        {
            zeroOffset = offsetGrams;
            tare = 0;
            settledFor = 0;
        }

        public void Tare()
        {
            if (!Powered || LoadGrams > 0) return;
            tare = zeroOffset;
            settledFor = 0;
        }

        public void Load(float mass)
        {
            LoadGrams = mass;
            settledFor = 0;
        }

        public void Animate(float dt)
        {
            if (Powered) settledFor += dt;
            if (display == null) return;
            string value = !Powered ? "" : Settled ? $"{Reading:F2} g" : "— —";
            if (display.text != value) display.text = value;
            powerLabel.text = Powered ? "OFF" : "ON";
        }
    }
}
