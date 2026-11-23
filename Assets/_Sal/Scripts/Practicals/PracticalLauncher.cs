using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PracticalLauncher : MonoBehaviour
{
    public const string MenuScene = "Practical_Launcher";
    public const string ChemistryScene = "Melting_Point_Naphthalene";
    public const string PhysicsScene = "Physics_8_1_Specific_Heat";

    public const string OhmScene = "Physics_14_1_Ohms_Law";

    static readonly Color Background = new(.035f, .09f, .12f);
    Font font;
    Sprite rounded;
    Button chemistry, ohm;

    // INTEGRATION (Tajarbagah): inside the app the "All practicals" buttons return to the app's
    // main menu (AppManager_Azm); the stand-alone launcher scene is still used when there is no app.
    public static bool Available =>
        VirtualLab.Azm.Core.AppManager_Azm.Instance != null || Application.CanStreamedLevelBeLoaded(MenuScene);

    public static void ReturnToMenu()
    {
        Time.timeScale = 1f;
        var app = VirtualLab.Azm.Core.AppManager_Azm.Instance;
        if (app != null) { app.GoToMainMenu(); return; }
        if (!Application.CanStreamedLevelBeLoaded(MenuScene)) return;
        SceneManager.LoadScene(MenuScene);
    }

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rounded = PracticalUIController.RoundedSprite();
        var camera = gameObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        camera.cullingMask = 0;
        gameObject.AddComponent<AudioListener>();

        var ui = new GameObject("Practical selector", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        ui.transform.SetParent(transform, false);
        ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        var safe = Rect("Safe area", ui.transform, Vector2.zero, Vector2.one);
        safe.gameObject.AddComponent<PracticalSafeArea>();

        Label(safe, "VIRTUAL LAB", new Vector2(.10f, .77f), new Vector2(.90f, .87f), 28, new Color(.59f, .91f, .85f));
        Label(safe, "Choose a practical", new Vector2(.10f, .62f), new Vector2(.90f, .78f), 62, Color.white);
        chemistry = Card(safe, "Chemistry", "Melting and boiling points", ChemistryScene,
            new Vector2(.16f, .26f), new Vector2(.48f, .58f), new Color(.075f, .37f, .37f));
        ohm = Card(safe, "Physics 14.1", "Verify Ohm’s Law", OhmScene,
            new Vector2(.52f, .26f), new Vector2(.84f, .58f), new Color(.095f, .35f, .40f));

        var events = new GameObject("Menu input", typeof(EventSystem));
        events.transform.SetParent(transform, false);
        events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    Button Card(Transform parent, string title, string detail, string scene, Vector2 min, Vector2 max, Color tint)
    {
        var rect = Rect(title, parent, min, max);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = tint;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.interactable = Application.CanStreamedLevelBeLoaded(scene);
        button.onClick.AddListener(() =>
        {
            chemistry.interactable = ohm.interactable = false;
            SceneManager.LoadSceneAsync(scene);
        });
        Label(rect, title, new Vector2(.08f, .42f), new Vector2(.92f, .84f), 48, Color.white);
        Label(rect, detail, new Vector2(.08f, .16f), new Vector2(.92f, .48f), 26, new Color(.87f, .96f, .97f));
        return button;
    }

    void Label(Transform parent, string value, Vector2 min, Vector2 max, int size, Color colour)
    {
        var label = Rect(value, parent, min, max).gameObject.AddComponent<Text>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.color = colour;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 18;
        label.resizeTextMaxSize = size;
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
