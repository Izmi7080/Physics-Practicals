using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualLab.Physics141
{
    internal sealed class OhmGuideHud
    {
        static readonly Color Gold = new(1f, .80f, .36f);
        static readonly Color Mint = new(.43f, 1f, .86f);
        static readonly Color Unfilled = new(.25f, .34f, .37f);
        readonly RectTransform safe, card, ringLayer, badge, summary, explanationRow, footer, promptDock;
        readonly RectTransform[] directionStrokes = new RectTransform[3];
        readonly Text step, title, cue, detail, count, actionLabel, whyLabel;
        readonly Button action, why;
        readonly Image[] milestones;
        readonly OhmGuideRing[] rings = new OhmGuideRing[8];
        readonly Bounds[] apparatus;
        readonly Rect[] occupied;
        Action onAction;
        int visibleRings, previousStage;
        float flashUntil;
        bool hasFocus, showWhy, inlineExplanation, showDirection, layoutDirty = true;
        Vector2 focus;
        Vector3 previousPosition;
        Quaternion previousRotation;
        Rect previousSafe;
        float previousFov;

        internal OhmGuideHud(RectTransform safe, Bounds[] apparatus, int stages = 8, RectTransform promptDock = null)
        {
            this.safe = safe;
            this.promptDock = promptDock;
            this.apparatus = apparatus;
            milestones = new Image[stages];
            occupied = new Rect[apparatus.Length];
            var rounded = PracticalUIController.RoundedSprite();
            ringLayer = OhmUi.Rect(safe, "Guided highlights", 0, 0, 1, 1);
            for (int i = 0; i < directionStrokes.Length; i++)
            {
                var stroke = OhmUi.Rect(ringLayer, "Drag direction", .5f, .5f, .5f, .5f);
                OhmUi.Paint(stroke, Mint, false);
                directionStrokes[i] = stroke;
            }
            for (int i = 0; i < rings.Length; i++)
            {
                var rect = OhmUi.Rect(ringLayer, "Guide ring " + (i + 1), .5f, .5f, .5f, .5f);
                rings[i] = rect.gameObject.AddComponent<OhmGuideRing>();
                rings[i].Configure();
                rect.gameObject.SetActive(false);
            }

            badge = promptDock != null ? OhmUi.Rect(safe, "Experiment progress badge", .012f, .897f, .145f, .986f)
                : OhmUi.Rect(safe, "Experiment progress badge", 0, 1, 0, 1);
            if (promptDock == null)
            {
                badge.offsetMin = new Vector2(24, -92);
                badge.offsetMax = new Vector2(196, -22);
            }
            PaintCard(badge);
            step = Label(badge, "Stage", .08f, .31f, .92f, .96f, 21, Mint);
            step.alignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < milestones.Length; i++)
            {
                float x = .09f + .832f / stages * i;
                milestones[i] = OhmUi.Paint(OhmUi.Rect(badge, "Experiment step " + (i + 1), x, .19f, x + .648f / stages, .26f),
                    Unfilled, false);
            }

            card = promptDock != null ? OhmUi.Rect(promptDock, "Current task", .5f, 1, .5f, 1)
                : OhmUi.Rect(safe, "Current task", .5f, .5f, .5f, .5f);
            if (promptDock != null) card.pivot = new Vector2(.5f, 1);
            PaintCard(card);
            summary = OhmUi.Rect(card, "Prompt summary", 0, 1, 1, 1);
            summary.offsetMin = new Vector2(14, promptDock != null ? -110 : -88);
            summary.offsetMax = new Vector2(-14, -7);
            title = Label(summary, "Current task title", 0, .57f, 1, .98f, 22, Mint);
            cue = Label(summary, "Current instruction", 0, .01f, 1, .57f, 25, Color.white);
            cue.alignment = TextAnchor.UpperLeft;
            count = Label(summary, "Task count", .65f, .57f, 1, .98f, 19, Mint);
            count.alignment = TextAnchor.MiddleRight;

            explanationRow = OhmUi.Rect(card, "Prompt explanation", 0, 0, 1, 0);
            explanationRow.offsetMin = new Vector2(14, 53);
            explanationRow.offsetMax = new Vector2(-14, 112);
            detail = Label(explanationRow, "Why this connection", 0, 0, 1, 1, 21, new Color(.75f, .88f, .86f));
            detail.alignment = TextAnchor.UpperLeft;
            footer = OhmUi.Rect(card, "Prompt controls", 0, 0, 1, 0);
            footer.offsetMin = new Vector2(14, 9);
            footer.offsetMax = new Vector2(-14, 49);
            why = OhmUi.Button(footer, "Why?", 0, 0, .25f, 1, ToggleExplanation);
            whyLabel = why.GetComponentInChildren<Text>();
            action = OhmUi.Button(footer, "Task action", .32f, 0, 1, 1, () => onAction?.Invoke());
            actionLabel = action.GetComponentInChildren<Text>();

            void PaintCard(RectTransform rect)
            {
                var face = OhmUi.Paint(rect, PracticalUiStyle.Document, false);
                face.sprite = rounded;
                face.type = Image.Type.Sliced;
            }
        }

        static Text Label(Transform parent, string name, float l, float b, float r, float t, int size, Color color)
        {
            var text = OhmUi.Label(parent, "", l, b, r, t, size);
            text.name = name;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }

        internal void Show(bool visible)
        {
            card.gameObject.SetActive(visible);
            badge.gameObject.SetActive(visible);
            ringLayer.gameObject.SetActive(visible);
            layoutDirty = true;
        }

        internal void ShowPrompt(bool visible)
        {
            if (card.gameObject.activeSelf == visible) return;
            card.gameObject.SetActive(visible);
            layoutDirty = true;
        }

        internal void Present(int stage, string heading, string instruction, int done, int total,
            string units, string button = null, Action callback = null, string context = "", bool showContext = false)
        {
            if (detail.text != context || previousStage != stage) showWhy = false;
            inlineExplanation = showContext && context.Length > 0;
            if (stage != previousStage) flashUntil = Time.unscaledTime + .5f;
            previousStage = stage;
            step.text = stage == 0 ? "PHYSICS 14.1" : $"Step {stage} of {milestones.Length}";
            title.text = heading;
            cue.text = instruction;
            detail.text = context;
            count.text = total > 0 ? $"{done} of {total} {units}" : "";
            title.rectTransform.anchorMax = new Vector2(total > 0 ? .65f : 1, .98f);
            why.gameObject.SetActive(context.Length > 0 && !inlineExplanation);
            onAction = callback;
            action.gameObject.SetActive(button != null);
            if (button != null)
            {
                action.name = button;
                actionLabel.text = button;
                action.GetComponent<RectTransform>().anchorMin = new Vector2(why.gameObject.activeSelf ? .32f : 0, 0);
            }
            for (int i = 0; i < milestones.Length; i++)
            {
                milestones[i].gameObject.SetActive(stage > 0);
                milestones[i].color = i + 1 < stage || stage == milestones.Length && done == total ? Mint
                    : i + 1 == stage ? Gold : Unfilled;
            }
            ApplyExplanation();
        }

        void ToggleExplanation()
        {
            showWhy = !showWhy;
            ApplyExplanation();
        }

        void ApplyExplanation()
        {
            bool controls = why.gameObject.activeSelf || action.gameObject.activeSelf;
            explanationRow.gameObject.SetActive(inlineExplanation || showWhy && why.gameObject.activeSelf);
            float bottom = controls ? 53 : 11;
            explanationRow.offsetMin = new Vector2(14, bottom);
            explanationRow.offsetMax = new Vector2(-14, bottom + 59);
            whyLabel.text = showWhy ? "Hide" : "Why?";
            footer.gameObject.SetActive(controls);
            layoutDirty = true;
        }

        internal void BeginRings()
        {
            visibleRings = 0;
            hasFocus = false;
            showDirection = false;
        }

        internal void EndRings(Camera camera, bool atBench, bool holdPosition)
        {
            foreach (var stroke in directionStrokes)
                if (stroke.gameObject.activeSelf != showDirection) stroke.gameObject.SetActive(showDirection);
            for (int i = 0; i < rings.Length; i++)
            {
                bool visible = (visibleRings & 1 << i) != 0;
                if (rings[i].gameObject.activeSelf != visible) rings[i].gameObject.SetActive(visible);
            }
            if (!badge.gameObject.activeInHierarchy || !card.gameObject.activeInHierarchy) return;
            if (previousStage > 0 && milestones[previousStage - 1].color != Mint)
            {
                float flash = Mathf.Clamp01((flashUntil - Time.unscaledTime) / .5f);
                milestones[previousStage - 1].color = Color.Lerp(Gold, Color.white, flash * .7f);
            }
            if (promptDock != null)
            {
                if (layoutDirty || promptDock.rect != previousSafe)
                {
                    previousSafe = promptDock.rect;
                    layoutDirty = false;
                    float scale = Mathf.Clamp(promptDock.rect.height / PromptHeight(), .01f, 1);
                    card.sizeDelta = new Vector2(promptDock.rect.width / scale, PromptHeight());
                    card.localScale = Vector3.one * scale;
                    card.anchoredPosition = Vector2.zero;
                }
                return;
            }
            if (holdPosition) return;
            var view = camera.transform;
            if (!layoutDirty && safe.rect == previousSafe && view.position == previousPosition
                && view.rotation == previousRotation && camera.fieldOfView == previousFov) return;
            previousSafe = safe.rect;
            previousPosition = view.position;
            previousRotation = view.rotation;
            previousFov = camera.fieldOfView;
            layoutDirty = false;
            PlacePrompt(camera, atBench);
        }

        internal void Ring(int index, Camera camera, Vector3 world, float diameter, bool destination = false, bool draw = true)
        {
            var screen = camera.WorldToScreenPoint(world);
            if (promptDock != null && !camera.pixelRect.Contains(screen)) return;
            if (!Project(screen, out var point) || !safe.rect.Contains(point)) return;
            if (!hasFocus || index == 0)
            {
                focus = point;
                hasFocus = true;
            }
            if (!draw) return;
            var ring = rings[index];
            ring.rectTransform.anchoredPosition = point;
            float pulse = 1 + Mathf.Sin(Time.unscaledTime * 5) * .045f;
            ring.rectTransform.sizeDelta = Vector2.one * diameter * pulse;
            ring.color = destination ? Mint : Gold;
            visibleRings |= 1 << index;
        }

        internal void DragDirection(Camera camera, Vector3 from, Vector3 to)
        {
            if (!Project(camera.WorldToScreenPoint(from), out var start) ||
                !Project(camera.WorldToScreenPoint(to), out var end)) return;
            if (Vector2.Distance(start, end) < 52) return;
            var direction = (end - start).normalized;
            var edge = new Vector2(-direction.y, direction.x) * 6;
            var tip = end - direction * 24 + Vector2.up * 20;
            var tail = tip - direction * 14;
            Stroke(directionStrokes[0], start + direction * 24 + Vector2.up * 20, tip);
            Stroke(directionStrokes[1], tail + edge, tip);
            Stroke(directionStrokes[2], tail - edge, tip);
            showDirection = true;

            static void Stroke(RectTransform line, Vector2 a, Vector2 b)
            {
                var delta = b - a;
                line.anchoredPosition = (a + b) * .5f;
                line.sizeDelta = new Vector2(delta.magnitude + 2, 2.5f);
                line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }

        void PlacePrompt(Camera camera, bool atBench)
        {
            var area = safe.rect;
            if (area.width <= 0 || area.height <= 0) return;
            float width = Mathf.Min(452, area.width * .43f);
            float height = PromptHeight();
            var preferred = hasFocus ? focus : new Vector2(0, -area.height * .23f);
            float minX = area.xMin + 20 + width * .5f;
            float maxX = area.xMax - 20 - width * .5f;
            float minY = area.yMin + 75 + height * .5f;
            float maxY = area.yMax - 107 - height * .5f;
            if (atBench)
                for (int i = 0; i < occupied.Length; i++) occupied[i] = ProjectBounds(camera, apparatus[i]);

            var best = new Vector2(0, minY);
            float bestScore = float.PositiveInfinity;
            for (int row = 0; row <= 8; row++)
            for (int col = 0; col <= 10; col++)
            {
                var center = new Vector2(Mathf.Lerp(minX, maxX, col / 10f), Mathf.Lerp(minY, maxY, row / 8f));
                var candidate = new Rect(center.x - width / 2, center.y - height / 2, width, height);
                float score = Vector2.Distance(preferred, center);
                if (atBench)
                {
                    foreach (var obstacle in occupied)
                    {
                        if (!candidate.Overlaps(obstacle)) continue;
                        float overlapWidth = Mathf.Min(obstacle.xMax, candidate.xMax) - Mathf.Max(obstacle.xMin, candidate.xMin);
                        float overlapHeight = Mathf.Min(obstacle.yMax, candidate.yMax) - Mathf.Max(obstacle.yMin, candidate.yMin);
                        score += 10000 + overlapWidth * overlapHeight;
                    }
                }
                if (score >= bestScore) continue;
                bestScore = score;
                best = center;
            }
            card.sizeDelta = new Vector2(width, height);
            card.anchoredPosition = best;
        }

        float PromptHeight() => (promptDock != null ? 116 : 94) + (footer.gameObject.activeSelf ? 42 : 0)
            + (explanationRow.gameObject.activeSelf ? 64 : 0);

        Rect ProjectBounds(Camera camera, Bounds world)
        {
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? world.min.x : world.max.x,
                    (i & 2) == 0 ? world.min.y : world.max.y, (i & 4) == 0 ? world.min.z : world.max.z);
                if (!Project(camera.WorldToScreenPoint(corner), out var point)) continue;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return float.IsPositiveInfinity(min.x) ? Rect.zero :
                Rect.MinMaxRect(min.x - 20, min.y - 20, max.x + 20, max.y + 20);
        }

        bool Project(Vector3 screen, out Vector2 point)
        {
            point = default;
            return screen.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out point);
        }
    }
}
