using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VirtualLab.Azm.Core;

namespace VirtualLab.Azm.UI
{
    /*
     * Layer: UI (animation helpers; no practical logic).
     * Purpose: The ONLY place the app talks to the tween library (DOTween, free version, in
     * Assets/Plugins/Demigiant). Every HUD, panel and feedback effect calls these named presets, so:
     *   - motion feels the same everywhere (durations come from UITheme_Azm motion tokens);
     *   - the library can be replaced by changing this one file;
     *   - the Reduce motion setting (SettingsManager_Azm.ReduceMotion) is honoured in one place:
     *     every preset then jumps straight to its end state.
     * All tweens use unscaled time (they keep working while a practical is paused) and are linked to
     * their GameObject, so destroying an object kills its tweens (no errors after a scene change).
     * A new preset on the same target kills the previous one first (no fighting animations).
     * Talks to: DOTween; SettingsManager_Azm (reduce motion); called by FeedbackFX_Azm, ButtonFeel_Azm,
     * InstructionCardUI_Azm, PanelFeedback_Azm and the HUDs.
     */
    public static class Motion_Azm
    {
        // Durations used when no theme is passed (same values as UITheme_Azm's defaults).
        public const float Fast = 0.12f;
        public const float Normal = 0.25f;
        public const float Slow = 0.45f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // Safe mode on: a tween whose target was destroyed is skipped instead of throwing.
            // Capacity: a practical screen runs at most a few dozen UI tweens at once.
            DOTween.Init(recycleAllByDefault: false, useSafeMode: true, logBehaviour: LogBehaviour.ErrorsOnly)
                   .SetCapacity(64, 16);
        }

        public static bool ReduceMotion =>
            SettingsManager_Azm.Instance != null && SettingsManager_Azm.Instance.ReduceMotion;

        private static T Setup<T>(T tween, Component target) where T : Tween
        {
            tween.SetUpdate(true).SetLink(target.gameObject);
            return tween;
        }

        // ----- scale -----

        // A quick squash used while a finger presses a button (released by Release).
        public static void Press(Transform target, float scale = 0.94f)
        {
            target.DOKill(true);
            if (ReduceMotion) return;
            Setup(target.DOScale(scale, Fast).SetEase(Ease.OutQuad), target);
        }

        public static void Release(Transform target)
        {
            target.DOKill();
            if (ReduceMotion) { target.localScale = Vector3.one; return; }
            Setup(target.DOScale(1f, Normal).SetEase(Ease.OutBack, 3f), target);
        }

        // A short "boing" that draws the eye (a correct value, a count going up).
        public static void Punch(Transform target, float strength = 0.12f)
        {
            target.DOKill(true);
            target.localScale = Vector3.one;
            if (ReduceMotion) return;
            Setup(target.DOPunchScale(Vector3.one * strength, Normal + 0.05f, 6, 0.6f), target);
        }

        // Grows in from small (a panel or chip appearing).
        public static void PopIn(Transform target, float from = 0.85f)
        {
            target.DOKill();
            if (ReduceMotion) { target.localScale = Vector3.one; return; }
            target.localScale = Vector3.one * from;
            Setup(target.DOScale(1f, Normal).SetEase(Ease.OutBack, 1.6f), target);
        }

        // A gentle repeating pulse that invites a tap (e.g. "Show me" after several wrong tries).
        // Returns the tween so the caller can stop it with StopPulse.
        public static Tween Pulse(Transform target, float scale = 1.06f, float period = 0.9f)
        {
            target.DOKill(true);
            target.localScale = Vector3.one;
            if (ReduceMotion) return null;
            return Setup(target.DOScale(scale, period * 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo), target);
        }

        public static void StopPulse(Transform target)
        {
            if (target == null) return;
            target.DOKill();
            target.localScale = Vector3.one;
        }

        // A ring that grows and fades from its centre (the pendulum's tap button, like a touch ripple).
        public static void Ripple(Transform ring, CanvasGroup group, float scale = 1.4f, float duration = 0.45f)
        {
            if (ReduceMotion) { ring.gameObject.SetActive(false); return; }
            ring.DOKill();
            group.DOKill();
            ring.gameObject.SetActive(true);
            ring.localScale = Vector3.one;
            group.alpha = 0.6f;
            Setup(ring.DOScale(scale, duration).SetEase(Ease.OutCubic), ring);
            Setup(group.DOFade(0f, duration).SetEase(Ease.OutQuad), group);
        }

        // Throws a small piece from where it is to 'to' (canvas units), spinning and fading out
        // (confetti). Skipped entirely with Reduce motion.
        public static void Fling(RectTransform piece, Graphic graphic, Vector2 to, float spinDeg, float duration)
        {
            piece.DOKill();
            graphic.DOKill();
            if (ReduceMotion) { piece.gameObject.SetActive(false); return; }
            piece.gameObject.SetActive(true);
            Setup(piece.DOAnchorPos(to, duration).SetEase(Ease.OutCubic), piece);
            Setup(piece.DOLocalRotate(new Vector3(0f, 0f, spinDeg), duration, RotateMode.FastBeyond360), piece);
            Setup(graphic.DOFade(0f, duration).SetEase(Ease.InQuad), graphic)
                .OnComplete(() => { if (piece != null) piece.gameObject.SetActive(false); });
        }

        // A quick squeeze of a 3D object that keeps its own scale (the screw gauge's strip pinched by
        // the thimble). amount = fraction of its size.
        public static void Squash(Transform target, float amount = 0.12f, float duration = 0.3f)
        {
            target.DOComplete();
            if (ReduceMotion) return;
            Setup(target.DOPunchScale(-target.localScale * amount, duration, 5, 0.5f), target);
        }

        // ----- position -----

        // A small horizontal shake: "not quite" (a wrong reading, a disabled button). Gentle on
        // purpose - the student should feel corrected, not punished.
        public static void Shake(RectTransform target, float strength = 14f)
        {
            target.DOKill(true);
            if (ReduceMotion) return;
            Setup(target.DOShakeAnchorPos(Normal + 0.1f, new Vector2(strength, 0f), 18, 0f, false, true), target);
        }

        // Slides a rect's anchored position to a new value (layout regions moving aside).
        public static void SlideTo(RectTransform target, Vector2 anchoredPosition, float duration = Normal)
        {
            target.DOKill();
            if (ReduceMotion) { target.anchoredPosition = anchoredPosition; return; }
            Setup(target.DOAnchorPos(anchoredPosition, duration).SetEase(Ease.OutCubic), target);
        }

        // ----- alpha / colour -----

        public static void FadeIn(CanvasGroup group, float duration = Normal)
        {
            group.DOKill();
            group.gameObject.SetActive(true);
            if (ReduceMotion) { group.alpha = 1f; return; }
            Setup(group.DOFade(1f, duration).SetEase(Ease.OutQuad), group);
        }

        // Fades out and (optionally) deactivates the object at the end.
        public static void FadeOut(CanvasGroup group, bool deactivate = true, float duration = Normal)
        {
            group.DOKill();
            if (ReduceMotion || !group.gameObject.activeInHierarchy)
            {
                group.alpha = 0f;
                if (deactivate) group.gameObject.SetActive(false);
                return;
            }
            var tween = Setup(group.DOFade(0f, duration).SetEase(Ease.InQuad), group);
            if (deactivate) tween.OnComplete(() => { if (group != null) group.gameObject.SetActive(false); });
        }

        // Flashes a graphic towards a colour and back (a cell that just got its value).
        public static void Flash(Graphic graphic, Color flash, float duration = Slow)
        {
            graphic.DOKill(true);
            if (ReduceMotion) return;
            Color normal = graphic.color;
            graphic.color = flash;
            Setup(graphic.DOColor(normal, duration).SetEase(Ease.OutQuad), graphic);
        }

        // Tints a 3D object's colour (URP Lit _BaseColor) towards 'flash' and back, without touching
        // the shared material (a property block). Used for "careful!" moments on the apparatus.
        public static void FlashRenderer(Renderer renderer, Color flash, float duration = Slow)
        {
            if (renderer == null || ReduceMotion) return;
            var block = new MaterialPropertyBlock();
            Color normal = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor")
                ? renderer.sharedMaterial.GetColor("_BaseColor") : Color.white;
            Setup(DOVirtual.Float(1f, 0f, duration, k =>
            {
                if (renderer == null) return;
                block.SetColor("_BaseColor", Color.Lerp(normal, flash, k));
                renderer.SetPropertyBlock(block);
            }).SetEase(Ease.InQuad).OnComplete(() => { if (renderer != null) renderer.SetPropertyBlock(null); }), renderer);
        }

        // ----- numbers -----

        // Counts a number up to its value (results). format e.g. "F2"; suffix e.g. " cm²".
        public static void CountUp(TMP_Text label, float from, float to, string format, string prefix = "", string suffix = "", float duration = 0.9f)
        {
            label.DOKill();
            if (ReduceMotion)
            {
                label.text = prefix + to.ToString(format, System.Globalization.CultureInfo.InvariantCulture) + suffix;
                return;
            }
            float value = from;
            Setup(DOTween.To(() => value, v =>
            {
                value = v;
                label.text = prefix + v.ToString(format, System.Globalization.CultureInfo.InvariantCulture) + suffix;
            }, to, duration).SetEase(Ease.OutCubic), label);
        }

        // Runs an action after a delay in unscaled time (e.g. collapse a hint after reading time).
        public static Tween Delay(Component owner, float seconds, TweenCallback action)
        {
            return Setup(DOVirtual.DelayedCall(seconds, action, true), owner);
        }
    }
}
