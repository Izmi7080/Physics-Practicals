using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VirtualLab.Azm.EditorTools
{
    /*
     * Layer: Editor tooling (development only; never in a build).
     * Purpose: Captures the Game view (including Screen Space - Overlay UI) at several phone and
     * tablet aspect ratios in one go, so the HCI work has comparable "before" and "after" screenshots
     * and anchoring problems show up at every aspect ratio:
     *   16:9 (1920x1080), 20:9 (2400x1080), 21:9 (2520x1080), 4:3 tablet (2048x1536).
     * It switches the Game view's resolution through Unity's internal GameViewSizes API (reflection;
     * missing sizes are added as custom sizes), waits a few frames for the canvas to re-layout, then
     * calls ScreenCapture. Run it in Play mode: CaptureAll("label") from code or the menu item.
     * Files go to Docs/HCI/Screens/<label>_<width>x<height>.png (outside Assets/).
     * Talks to: the Game view window only.
     */
    public static class MultiAspectCapture_Azm
    {
        public static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(2400, 1080),
            new Vector2Int(2520, 1080),
            new Vector2Int(2048, 1536),
        };

        private const int FramesToSettle = 12;
        public static bool Busy { get; private set; }
        public static string LastResult { get; private set; } = "";

        private static readonly Queue<Vector2Int> pending = new Queue<Vector2Int>();
        private static string currentLabel;
        private static int waitFrames;
        private static Vector2Int current;
        private static bool captureRequested;
        private static int restoreIndex = -1;

        [MenuItem("VirtualLab_Azm/Capture Game View At All Aspects")]
        private static void CaptureFromMenu() => CaptureAll("manual_" + DateTime.Now.ToString("HHmmss"));

        public static string OutputFolder =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Docs", "HCI", "Screens");

        // Starts an asynchronous capture of every size in Sizes (or the given subset).
        public static void CaptureAll(string label, Vector2Int[] sizes = null)
        {
            if (!Application.isPlaying) { LastResult = "Enter Play mode first."; Debug.LogWarning("[MultiAspectCapture_Azm] " + LastResult); return; }
            if (Busy) { LastResult = "Already capturing."; return; }

            Directory.CreateDirectory(OutputFolder);
            pending.Clear();
            foreach (var s in sizes ?? Sizes) pending.Enqueue(s);
            currentLabel = label;
            restoreIndex = GetSelectedIndex();
            LastResult = "";
            Busy = true;
            NextSize();
            EditorApplication.update += Tick;
        }

        private static void NextSize()
        {
            current = pending.Dequeue();
            SelectSize(current.x, current.y);
            waitFrames = FramesToSettle;
            captureRequested = false;
        }

        private static void Tick()
        {
            if (!Application.isPlaying) { Finish("Play mode ended."); return; }
            // Force the Game view to repaint so frames advance even when the Editor is unfocused.
            GetGameView()?.Repaint();

            if (waitFrames > 0) { waitFrames--; return; }

            if (!captureRequested)
            {
                string file = Path.Combine(OutputFolder, $"{currentLabel}_{current.x}x{current.y}.png");
                ScreenCapture.CaptureScreenshot(file);
                LastResult += file + "\n";
                captureRequested = true;
                waitFrames = 4;   // CaptureScreenshot writes at the end of a frame
                return;
            }

            if (pending.Count > 0) NextSize();
            else Finish(null);
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            if (restoreIndex >= 0) SetSelectedIndex(restoreIndex);
            Busy = false;
            if (error != null) LastResult += "ERROR: " + error;
            Debug.Log("[MultiAspectCapture_Azm] Done.\n" + LastResult);
        }

        // ----- Game view size plumbing (internal Unity API, via reflection) -----

        private static Assembly EditorAssembly => typeof(Editor).Assembly;

        private static EditorWindow GetGameView()
        {
            var type = EditorAssembly.GetType("UnityEditor.GameView");
            var windows = Resources.FindObjectsOfTypeAll(type);
            return windows.Length > 0 ? (EditorWindow)windows[0] : EditorWindow.GetWindow(type);
        }

        private static object GetGroup()
        {
            var sizesType = EditorAssembly.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var groupType = (GameViewSizeGroupType)sizesType.GetProperty("currentGroupType").GetValue(instance);
            return sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { groupType });
        }

        private static int FindOrAddSize(int width, int height)
        {
            var group = GetGroup();
            var groupType = group.GetType();
            int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < total; i++)
            {
                var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var st = size.GetType();
                if ((int)st.GetProperty("width").GetValue(size) == width &&
                    (int)st.GetProperty("height").GetValue(size) == height &&
                    st.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution")
                    return i;
            }

            var sizeTypeEnum = EditorAssembly.GetType("UnityEditor.GameViewSizeType");
            var ctor = EditorAssembly.GetType("UnityEditor.GameViewSize")
                .GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            var newSize = ctor.Invoke(new[] { Enum.Parse(sizeTypeEnum, "FixedResolution"), (object)width, height, $"{width}x{height}" });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
            return total;
        }

        private static void SelectSize(int width, int height) => SetSelectedIndex(FindOrAddSize(width, height));

        private static int GetSelectedIndex()
        {
            var view = GetGameView();
            var prop = view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop != null ? (int)prop.GetValue(view) : -1;
        }

        private static void SetSelectedIndex(int index)
        {
            var view = GetGameView();
            var callback = view.GetType().GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (callback != null) callback.Invoke(view, new object[] { index, null });
            else view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(view, index);
            view.Repaint();
        }
    }
}
