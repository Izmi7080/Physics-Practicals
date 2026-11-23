using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static VirtualLab.Physics141.OhmSheet;

namespace VirtualLab.Physics141
{
    public sealed class OhmCircuitPage
    {
        readonly struct Connection
        {
            internal readonly int From, To;
            internal readonly string Heading, Reason;
            internal readonly Vector2[] Corners;
            internal Connection(int from, int to, string heading, string reason, params Vector2[] corners)
            {
                From = from;
                To = to;
                Heading = heading;
                Reason = reason;
                Corners = corners;
            }

            internal bool Matches(Lead link) => link.Joins((Terminal)From, (Terminal)To);
        }

        const int AllTiles = (1 << 6) - 1;
        const float SymbolSize = 84;
        const float TerminalRadius = 43;
        const float DragThreshold = 12;
        static readonly string[] Symbols = { "Supply", "Key", "Rheostat", "Ammeter", "Nichrome", "Voltmeter" };
        static readonly string[] Equipment = { "DC power supply", "Plug key", "Sliding rheostat", "Ammeter", "Nichrome wire", "Voltmeter" };
        static readonly string[] Roles = { "Provides voltage", "Opens or closes the loop", "Adjusts the current",
            "Measures current", "Sample being tested", "Measures voltage" };
        static readonly string[] Jacks = { "−", "+", "In", "Out", "Fixed", "Wiper", "+", "COM", "R", "L", "COM", "V+" };
        static readonly Vector2[] Centers =
        {
            new(.76f, .20f), new(.51f, .20f), new(.255f, .20f), new(.115f, .475f), new(.515f, .69f), new(.515f, .91f)
        };
        // These IDs match the saved diagram format. The left and right sample labels match the bench.
        static readonly Vector2[] Ports =
        {
            new(.85f, .20f), new(.67f, .20f), new(.60f, .20f), new(.42f, .20f),
            new(.35f, .20f), new(.16f, .20f), new(.115f, .35f), new(.115f, .60f),
            new(.615f, .69f), new(.415f, .69f), new(.615f, .91f), new(.415f, .91f)
        };
        static readonly Connection[] Sequence =
        {
            new(1, 2, "Supply + to Key In",
                "The red + terminal is at a higher voltage than the black − terminal. Connect + to the key so the switch controls whether current can flow."),
            new(3, 4, "Key Out to Rheostat Fixed",
                "A closed key lets current continue to the rheostat. The rheostat is an adjustable resistor, so it controls how much current flows."),
            new(5, 6, "Rheostat Wiper to Ammeter +",
                "The wiper is the moving contact on the rheostat. Connect it to the ammeter’s + input so the loop current flows through the meter.",
                new Vector2(.115f, .20f)),
            new(7, 9, "Ammeter COM to Nichrome L",
                "COM is the ammeter’s black jack. Current leaves here and enters the left end of the sample, so the ammeter measures the current through the sample.",
                new Vector2(.115f, .69f)),
            new(8, 0, "Nichrome R to Supply −",
                "The sample’s right end returns to the black − terminal. This completes the main loop. Opening the key breaks that loop.",
                new(.94f, .69f), new(.94f, .20f)),
            new(9, 11, "Nichrome L to Voltmeter V+",
                "Current enters the sample at L, the higher-voltage end. Connect L to the voltmeter’s red V+ input."),
            new(8, 10, "Nichrome R to Voltmeter COM",
                "R is the lower-voltage end. With COM connected here, the voltmeter compares both ends of the sample. This separate branch is called a parallel connection.")
        };

        static readonly Color Gold = new(.66f, .35f, .08f);
        static readonly Color Loop = new(.06f, .40f, .32f);
        static readonly Color Branch = new(.12f, .34f, .58f);
        readonly RectTransform panel, board, linkLayer, hintLayer, preview;
        readonly Button[] tiles = new Button[6];
        readonly Image[] glyphs = new Image[6];
        readonly Button[] ends = new Button[12];
        readonly Image[] terminalDots = new Image[12];
        readonly Image[] terminalHalos = new Image[12];
        readonly RectTransform[] terminalStubs = new RectTransform[12];
        readonly Text heading, reason, message, progress;
        readonly Button undo, hint;
        readonly Text hintLabel;
        readonly List<Lead> links = new();
        readonly List<GameObject> strokes = new();
        readonly Func<bool> isGuided;
        readonly MonoBehaviour runner;
        Coroutine hintTimer;
        int? selected, dragging;
        readonly Image previewInk;
        int dragPointer;
        Vector2 dragOrigin;
        bool wireMoved;
        int placed;

        internal int Placed => placed;
        internal List<Lead> Links => links;
        internal int LinkCount => links.Count;
        internal bool IsComplete(bool guided) => placed == AllTiles && links.Count == Sequence.Length
            && (!guided || NextLinkIndex() < 0);
        internal event Action Changed;

        internal OhmCircuitPage(Transform parent, MonoBehaviour runner, Action closed, Func<bool> guided)
        {
            isGuided = guided;
            this.runner = runner;
            panel = OhmUi.Rect(parent, "Circuit workbook", .052f, .09f, .948f, .91f);
            var sheet = PinnedPaper(panel, "Circuit worksheet paper");
            var title = Label(sheet, "Circuit diagram", .046f, .875f, .48f, .97f, 36);
            title.text = "Circuit diagram";
            title.alignment = TextAnchor.MiddleLeft;
            title.fontStyle = FontStyle.Bold;
            var tackFace = PracticalUIController.CircleSprite();
            PaperButton(OhmUi.Button(sheet, "Close diagram", .805f, .885f, .953f, .964f, () => { CancelDrag(); closed(); }), new Color(.86f, .89f, .837f), Ink);
            board = OhmUi.Rect(sheet, "Diagram work area", .038f, .295f, .692f, .855f);
            OhmUi.Paint(board, GraphPaper, false);
            var grid = OhmUi.Rect(board, "Workbook grid", 0, 0, 1, 1).gameObject.AddComponent<OhmDiagramPaper>();
            grid.color = new Color(.72f, .79f, .76f, .54f);
            grid.raycastTarget = false;
            linkLayer = OhmUi.Rect(board, "Drawn wires", 0, 0, 1, 1);
            for (int i = 0; i < terminalStubs.Length; i++)
            {
                terminalStubs[i] = OhmUi.Rect(linkLayer, "Component pin", .5f, .5f, .5f, .5f);
                OhmUi.Paint(terminalStubs[i], new Color(.47f, .54f, .50f), false);
            }
            hintLayer = OhmUi.Rect(board, "Temporary circuit hint", 0, 0, 1, 1);
            hintLayer.gameObject.SetActive(false);
            preview = OhmUi.Rect(board, "Diagram wire preview", .5f, .5f, .5f, .5f);
            previewInk = OhmUi.Paint(preview, Gold, false);
            preview.gameObject.SetActive(false);

            CreateEquipmentKey(sheet);
            var instructions = OhmUi.Rect(sheet, "Connection explanation", .038f, .133f, .953f, .274f);
            Surface(instructions, Note);
            progress = Label(sheet, "Diagram progress", .485f, .885f, .775f, .96f, 23);
            progress.alignment = TextAnchor.MiddleRight;
            progress.color = Loop;
            heading = Label(instructions, "Connection heading", .019f, .62f, .69f, .95f, 25);
            heading.fontStyle = FontStyle.Bold;
            reason = Label(instructions, "Connection reason", .019f, .06f, .70f, .61f, 23);
            message = Label(instructions, "Diagram instruction", .737f, .10f, .979f, .89f, 22);
            message.alignment = TextAnchor.MiddleLeft;
            message.color = Loop;
            Legend(sheet, .043f, "Main loop", Loop);
            Legend(sheet, .201f, "Voltage branch", Branch);
            hint = OhmUi.Button(sheet, "Circuit hint", .534f, .043f, .727f, .12f, ToggleHint);
            PaperButton(hint, Loop, Color.white);
            hintLabel = hint.GetComponentInChildren<Text>();
            undo = OhmUi.Button(sheet, "Undo diagram link", .749f, .043f, .951f, .12f, Undo);
            PaperButton(undo, new Color(.86f, .89f, .837f), Ink);

            for (int i = 0; i < Symbols.Length; i++)
            {
                int id = i;
                var p = Centers[i];
                tiles[i] = OhmUi.Button(board, "Place " + Symbols[i], p.x - .044f, p.y - .075f, p.x + .044f, p.y + .075f,
                    () => { placed |= 1 << id; Refresh(); Changed?.Invoke(); });
                PaperButton(tiles[i], new Color(.87f, .92f, .882f), Ink);
                tiles[i].GetComponentInChildren<Text>().gameObject.SetActive(false);
                glyphs[i] = OhmCircuitSymbols.Create(tiles[i].transform, (OhmSymbol)i, SymbolSize, Ink);
            }
            for (int i = 0; i < ends.Length; i++)
            {
                int end = i;
                var at = Ports[i];
                ends[i] = OhmUi.Button(board, Jacks[i], at.x, at.y, at.x, at.y, () => Choose(end));
                ((RectTransform)ends[i].transform).sizeDelta = Vector2.one * (2 * TerminalRadius);
                PaperButton(ends[i], Color.clear, Ink);
                ends[i].gameObject.name = "Diagram terminal " + i;
                var label = ends[i].GetComponentInChildren<Text>().rectTransform;
                label.anchorMin = label.anchorMax = new Vector2(.5f, .5f);
                label.sizeDelta = new Vector2(58, 24);
                label.anchoredPosition = i is 6 or 7 ? new Vector2(-38, 0)
                    : i >= 8 ? new Vector2(at.x < Centers[i / 2].x ? -34 : 34, -18)
                    : new Vector2(0, -23);
                terminalHalos[i] = TerminalDot(ends[i].transform, tackFace, 27);
                terminalDots[i] = TerminalDot(ends[i].transform, tackFace, 12);
                var drag = ends[i].gameObject.AddComponent<OhmDiagramTerminal>();
                drag.HitTest = (point, camera) => TryBoardPoint(point, camera, out var local) && NearestTerminal(local) == end;
                drag.Started = e => BeginDrag(end, e);
                drag.Moved = MoveDrag;
                drag.Released = EndDrag;
            }
            Refresh();
            Hide();
        }

        static Image TerminalDot(Transform terminal, Sprite circle, float size)
        {
            var dot = OhmUi.Rect(terminal, "Terminal marker", .5f, .5f, .5f, .5f);
            dot.sizeDelta = Vector2.one * size;
            var image = OhmUi.Paint(dot, Ink, false);
            image.sprite = circle;
            return image;
        }

        static void CreateEquipmentKey(RectTransform sheet)
        {
            var key = OhmUi.Rect(sheet, "Equipment key", .715f, .295f, .953f, .855f);
            Surface(key, Note);
            var heading = Label(key, "Equipment key title", .07f, .865f, .93f, .98f, 26);
            heading.text = "Equipment key";
            heading.fontStyle = FontStyle.Bold;
            for (int i = 0; i < Equipment.Length; i++)
            {
                float top = .865f - i * .137f;
                var row = OhmUi.Rect(key, "Equipment key " + Equipment[i], .057f, top - .128f, .943f, top);
                var image = OhmUi.Rect(row, "Symbol column", 0, 0, .245f, 1);
                OhmCircuitSymbols.Create(image, (OhmSymbol)i, 45, Ink);
                var name = Label(row, "Equipment name", .28f, .43f, .995f, .98f, 21);
                name.text = Equipment[i];
                name.fontStyle = FontStyle.Bold;
                var role = Label(row, "Equipment role", .28f, .05f, .995f, .46f, 16);
                role.text = Roles[i];
                if (i != Equipment.Length - 1)
                    OhmUi.Paint(OhmUi.Rect(row, "Key row divider", 0, -.029f, 1, -.012f), new Color(.75f, .77f, .71f), false);
            }
        }

        static Text Label(Transform parent, string name, float a, float b, float c, float d, int size)
        {
            var label = OhmUi.Label(parent, "", a, b, c, d, size);
            label.name = name;
            label.color = Ink;
            label.alignment = TextAnchor.UpperLeft;
            return label;
        }

        static void Legend(RectTransform sheet, float x, string name, Color ink)
        {
            OhmUi.Paint(OhmUi.Rect(sheet, name + " swatch", x, .08f, x + .019f, .088f), ink, false);
            var label = OhmUi.Label(sheet, name, x + .026f, .042f, x + .155f, .12f, 21);
            label.color = Ink;
            label.alignment = TextAnchor.MiddleLeft;
        }

        internal void Show() { panel.gameObject.SetActive(true); panel.SetAsLastSibling(); Refresh(); }
        internal void Hide() { CancelDrag(); HideHint(); selected = null; panel.gameObject.SetActive(false); }
        internal void Restore(int tilesMask, IEnumerable<Lead> saved)
        {
            placed = tilesMask;
            links.Clear();
            if (saved != null) links.AddRange(saved);
            CancelDrag();
            HideHint();
            selected = null;
            Refresh();
        }

        void Undo()
        {
            if (links.Count == 0) return;
            CancelDrag();
            HideHint();
            links.RemoveAt(links.Count - 1);
            selected = null;
            Refresh();
            Changed?.Invoke();
        }

        void ToggleHint()
        {
            if (hintLayer.gameObject.activeSelf)
            {
                HideHint();
                return;
            }
            int next = NextLinkIndex();
            if (!isGuided() || placed != AllTiles || next < 0 || dragging.HasValue) return;
            var route = Sequence[next];
            hintLayer.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            DrawRoute(hintLayer, route.From, route.To, route.Corners, Gold, true);
            hintLabel.text = "Hide hint";
            hintTimer = runner.StartCoroutine(ExpireHint());
        }

        IEnumerator ExpireHint()
        {
            yield return new WaitForSecondsRealtime(5);
            hintTimer = null;
            HideHint();
        }

        void HideHint()
        {
            if (hintTimer != null) runner.StopCoroutine(hintTimer);
            hintTimer = null;
            hintLayer.gameObject.SetActive(false);
            hintLabel.text = "Show wiring hint";
            foreach (Transform dash in hintLayer)
            {
                dash.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(dash.gameObject);
            }
        }

        bool CanStart(int end)
        {
            if (placed != AllTiles) { message.text = "Place all six components before drawing wires."; return false; }
            if (links.Count >= Sequence.Length) { message.text = "Undo a wire before replacing it."; return false; }
            int next = NextLinkIndex();
            if (!isGuided() || next < 0 || end == Sequence[next].From || end == Sequence[next].To) return true;
            message.text = "Start on one of the two highlighted terminals.";
            return false;
        }

        void Choose(int end)
        {
            if (selected == end) { selected = null; Refresh(); return; }
            if (selected is int from) { Connect(from, end); return; }
            if (!CanStart(end)) return;
            selected = end;
            Refresh();
        }

        void BeginDrag(int end, PointerEventData e)
        {
            if (dragging.HasValue || !CanStart(end)) return;
            dragging = end;
            dragPointer = e.pointerId;
            if (TryBoardPoint(e.pressPosition, e.pressEventCamera, out var point)) dragOrigin = point;
            wireMoved = false;
            MoveDrag(e);
        }

        void MoveDrag(PointerEventData e)
        {
            if (dragging is not int end || dragPointer != e.pointerId || !TryBoardPoint(e.position, e.pressEventCamera, out var point)) return;
            if (!wireMoved)
            {
                if ((point - dragOrigin).sqrMagnitude < DragThreshold * DragThreshold) return;
                wireMoved = true;
                selected = null;
                preview.gameObject.SetActive(true);
            }
            var rect = board.rect;
            var target = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x), Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
            int next = isGuided() ? NextLinkIndex() : -1;
            int port = NearestTerminal(point) ?? end;
            bool snapped = port / 2 != end / 2 &&
                (next < 0 || Sequence[next].Matches(new Lead((Terminal)end, (Terminal)port)));
            if (snapped) target = Ports[port];
            previewInk.color = snapped ? Loop : Gold;
            PlaceSegment(preview, Ports[end], target, rect.size, 3);
        }

        void EndDrag(PointerEventData e)
        {
            if (dragging is not int from || dragPointer != e.pointerId) return;
            bool moved = wireMoved;
            CancelDrag();
            int? target = TryBoardPoint(e.position, e.pressEventCamera, out var point) ? NearestTerminal(point) : null;
            if (!moved && target == from) { Choose(from); return; }
            if (target is int end && end != from) { Connect(from, end); return; }
            selected = from;
            Refresh();
        }

        bool TryBoardPoint(Vector2 screen, Camera camera, out Vector2 point) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, camera, out point);

        int? NearestTerminal(Vector2 point)
        {
            var rect = board.rect;
            if (!rect.Contains(point)) return null;
            float closest = TerminalRadius * TerminalRadius;
            int? hit = null;
            for (int i = 0; i < ends.Length; i++)
            {
                if (!ends[i].gameObject.activeInHierarchy) continue;
                var center = rect.min + Vector2.Scale(Ports[i], rect.size);
                float distance = (point - center).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                hit = i;
            }
            return hit;
        }

        void CancelDrag()
        {
            dragging = null;
            preview.gameObject.SetActive(false);
        }

        void Connect(int from, int to)
        {
            selected = null;
            var link = new Lead((Terminal)from, (Terminal)to);
            int next = NextLinkIndex();
            string error = from / 2 == to / 2 ? "Connect two different components."
                : isGuided() && next >= 0 && !Sequence[next].Matches(link) ? "That wire does not reach the other highlighted terminal."
                : links.Exists(wire => wire.Joins(link.from, link.to)) ? "That wire is already drawn."
                : links.Count >= Sequence.Length ? "Undo a wire before replacing it." : null;
            if (error == null)
            {
                HideHint();
                links.Add(link);
            }
            Refresh();
            if (error == null) Changed?.Invoke();
            else message.text = error;
        }

        int NextLinkIndex()
        {
            for (int i = 0; i < Sequence.Length; i++)
                if (!links.Exists(Sequence[i].Matches)) return i;
            return -1;
        }

        void Refresh()
        {
            bool guided = isGuided();
            bool ready = placed == AllTiles;
            int next = NextLinkIndex();
            for (int i = 0; i < tiles.Length; i++)
            {
                bool active = (placed & 1 << i) != 0;
                tiles[i].interactable = !active;
                tiles[i].image.color = active ? Color.clear : new Color(.87f, .92f, .882f);
                glyphs[i].color = active ? Ink : new Color(.33f, .42f, .39f);
                ends[i * 2].gameObject.SetActive(active);
                ends[i * 2 + 1].gameObject.SetActive(active);
                Tint(tiles[i], guided && !active ? Loop : (Color?)null);
            }
            for (int i = 0; i < ends.Length; i++)
            {
                Color? color = ready && guided && next >= 0
                    ? i == Sequence[next].From ? Gold : i == Sequence[next].To ? Loop : (Color?)null
                    : null;
                if (selected == i) color = Gold;
                terminalDots[i].color = color ?? Ink;
                terminalHalos[i].gameObject.SetActive(color.HasValue);
                if (color is Color ring) terminalHalos[i].color = new Color(ring.r, ring.g, ring.b, .24f);
            }
            DrawWires();
            undo.interactable = links.Count != 0;
            hint.gameObject.SetActive(ready && guided && next >= 0 && links.Count < Sequence.Length);
            progress.text = ready ? $"{links.Count} of 7 wires" : $"{TileCount()} of 6 components";
            if (!ready)
            {
                heading.text = "Place the components";
                reason.text = "The drawing shows how the supply, switch, rheostat, sample, and two meters fit together before you wire the bench.";
                message.text = "Tap each symbol on the paper to place it. The equipment key shows what each one represents.";
            }
            else if (IsComplete(guided))
            {
                heading.text = "Circuit diagram complete";
                reason.text = guided ? "The ammeter is in the main loop (in series). The voltmeter is across the sample (in parallel)."
                    : "Check that the current meter is in the main loop and the voltage meter spans the sample.";
                message.text = "Close the diagram to return to the bench.";
            }
            else if (guided && next >= 0)
            {
                heading.text = Sequence[next].Heading;
                reason.text = Sequence[next].Reason;
                message.text = links.Count == Sequence.Length ? "Undo a wrong wire to make space."
                    : selected.HasValue ? "Tap the other highlighted terminal to finish the wire."
                    : "Tap the gold terminal, then the green one. You can also drag between them.";
            }
            else
            {
                heading.text = "Draw the circuit";
                reason.text = "Use the main loop to measure current. Put the voltmeter across the points you want to compare.";
                message.text = selected.HasValue ? "Tap the second terminal." : "Tap two terminals or drag between them to draw a wire.";
            }
        }

        int TileCount()
        {
            int count = 0;
            for (int bits = placed & AllTiles; bits != 0; bits >>= 1) count += bits & 1;
            return count;
        }

        void DrawWires()
        {
            foreach (var stroke in strokes)
            {
                stroke.SetActive(false);
                UnityEngine.Object.Destroy(stroke);
            }
            strokes.Clear();
            Canvas.ForceUpdateCanvases();
            for (int i = 0; i < terminalStubs.Length; i++)
            {
                int component = i / 2;
                var stub = terminalStubs[i];
                bool placedOnBoard = (placed & 1 << component) != 0;
                stub.gameObject.SetActive(placedOnBoard);
                if (!placedOnBoard) continue;
                var center = Centers[component];
                var direction = (Ports[i] - center).normalized;
                var boardSize = board.rect.size;
                if (boardSize.x <= 0 || boardSize.y <= 0) continue;
                float reach = SymbolSize * OhmCircuitSymbols.LeadReach;
                Vector2 intoComponent = center + new Vector2(direction.x * reach / boardSize.x, direction.y * reach / boardSize.y);
                PlaceSegment(stub, Ports[i], intoComponent, board.rect.size, 3);
            }
            foreach (var link in links)
            {
                if ((uint)link.from >= Ports.Length || (uint)link.to >= Ports.Length) continue;
                int route = Array.FindIndex(Sequence, connection => connection.Matches(link));
                int from = route >= 0 ? Sequence[route].From : (int)link.from;
                int to = route >= 0 ? Sequence[route].To : (int)link.to;
                var corners = route >= 0 ? Sequence[route].Corners : Array.Empty<Vector2>();
                var root = OhmUi.Rect(linkLayer, "Circuit wire", 0, 0, 1, 1);
                strokes.Add(root.gameObject);
                DrawRoute(root, from, to, corners, route >= 5 ? Branch : Loop, false);
            }
        }

        void DrawRoute(RectTransform root, int from, int to, Vector2[] corners, Color ink, bool dashed)
        {
            var start = Ports[from];
            for (int i = 0; i <= corners.Length; i++)
            {
                var stop = i < corners.Length ? corners[i] : Ports[to];
                if (dashed)
                {
                    float length = Vector2.Scale(stop - start, board.rect.size).magnitude;
                    int count = Mathf.Max(2, Mathf.CeilToInt(length / 19));
                    for (int dash = 0; dash < count; dash++)
                        DrawSegment(root, "Hint dash", Vector2.Lerp(start, stop, (dash + .10f) / count),
                            Vector2.Lerp(start, stop, (dash + .66f) / count), ink, 3);
                }
                else DrawSegment(root, "Wire segment", start, stop, ink, 4);
                start = stop;
            }
        }

        void DrawSegment(RectTransform root, string name, Vector2 start, Vector2 stop, Color ink, float width)
        {
            var segment = OhmUi.Rect(root, name, .5f, .5f, .5f, .5f);
            OhmUi.Paint(segment, ink, false);
            PlaceSegment(segment, start, stop, board.rect.size, width);
        }

        static void PlaceSegment(RectTransform segment, Vector2 start, Vector2 stop, Vector2 size, float width)
        {
            var delta = Vector2.Scale(stop - start, size);
            segment.anchorMin = segment.anchorMax = (start + stop) * .5f;
            segment.anchoredPosition = Vector2.zero;
            segment.sizeDelta = new Vector2(delta.magnitude + width, width);
            segment.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        static void Tint(Button button, Color? guide)
        {
            var outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(2, 2);
            }
            outline.enabled = guide.HasValue;
            if (guide.HasValue) outline.effectColor = guide.Value;
        }
    }
}
