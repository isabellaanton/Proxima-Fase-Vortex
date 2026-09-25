#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Geoguess > Build UI and Game Manager
/// Creates the whole Canvas (menu, HUD, clues, answers, find-it, feedback, results), the EventSystem,
/// adds UIManager / ClueSystem / GameManager and wires every reference.
/// Requires: the "Game" object with GlobeController, CountryPicker, CountryHighlighter, CameraFocus.
/// </summary>
public static class GameBuilder
{
    static readonly Color Dark = new Color(0f, 0f, 0f, 0.55f);

    [MenuItem("Geoguess/Build UI and Game Manager")]
    public static void Build()
    {
        var game = GameObject.Find("Game");
        if (game == null) { Debug.LogError("Geoguess: não achei o objeto 'Game' na cena. Crie-o primeiro (com GlobeController, CountryPicker, CountryHighlighter, CameraFocus)."); return; }
        var globe = game.GetComponent<GlobeController>();
        var picker = game.GetComponent<CountryPicker>();
        var highlighter = game.GetComponent<CountryHighlighter>();
        var focus = game.GetComponent<CameraFocus>();
        if (globe == null || picker == null || highlighter == null || focus == null)
        { Debug.LogError("Geoguess: o objeto 'Game' precisa ter GlobeController, CountryPicker, CountryHighlighter e CameraFocus."); return; }

        var guids = AssetDatabase.FindAssets("t:CountryDatabase");
        if (guids.Length == 0) { Debug.LogError("Geoguess: CountryDatabase não encontrado. Rode Geoguess > Generate Sample Data."); return; }
        var db = AssetDatabase.LoadAssetAtPath<CountryDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));

        // remove a previous build
        var old = GameObject.Find("GeoguessCanvas"); if (old != null) Object.DestroyImmediate(old);

        var res = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };

        // ---------------- canvas + event system
        var canvasGO = new GameObject("GeoguessCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGO.transform;

        EnsureEventSystem();

        var ui = canvasGO.AddComponent<UIManager>();

        // ---------------- menu
        var menu = Box("MenuPanel", root, V(0, 0), V(1, 1), new Color(0, 0, 0, 0.35f));
        ui.menuPanel = menu;
        Label("Title", menu.transform, V(0.15f, 0.70f), V(0.85f, 0.88f), "FAKE EARTH", 84, TextAlignmentOptions.Center);
        ui.playButton = Button("PlayButton", menu.transform, V(0.38f, 0.50f), V(0.62f, 0.58f), "Play", 40, res);
        ui.modeButton = Button("ModeButton", menu.transform, V(0.32f, 0.39f), V(0.68f, 0.46f), "Mode", 32, res); ui.modeLabel = ui.modeButton.GetComponentInChildren<TMP_Text>();
        ui.answerModeButton = Button("AnswerModeButton", menu.transform, V(0.32f, 0.29f), V(0.68f, 0.36f), "Answers", 32, res); ui.answerModeLabel = ui.answerModeButton.GetComponentInChildren<TMP_Text>();
        ui.quitButton = Button("QuitButton", menu.transform, V(0.38f, 0.19f), V(0.62f, 0.26f), "Quit", 36, res);

        // ---------------- HUD (no background so it never blocks the globe)
        var hud = Rect("HUDPanel", root, V(0, 0), V(1, 1)); ui.hudPanel = hud;
        var top = Box("TopBar", hud.transform, V(0, 0.93f), V(1, 1), Dark);
        ui.roundText = Label("RoundText", top.transform, V(0.02f, 0), V(0.30f, 1), "Round 1/10", 40, TextAlignmentOptions.Left);
        ui.scoreText = Label("ScoreText", top.transform, V(0.35f, 0), V(0.65f, 1), "Score: 0", 40, TextAlignmentOptions.Center);
        ui.timerText = Label("TimerText", top.transform, V(0.70f, 0), V(0.98f, 1), "30", 48, TextAlignmentOptions.Right);

        var clueBox = Box("CluePanel", hud.transform, V(0.01f, 0.30f), V(0.28f, 0.91f), Dark);
        ui.clueTexts = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float y1 = 0.98f - i * 0.30f;
            var t = Label("Clue" + (i + 1) + "Text", clueBox.transform, V(0.04f, y1 - 0.28f), V(0.96f, y1), "— hidden —", 26, TextAlignmentOptions.TopLeft);
            t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 26; ui.clueTexts[i] = t;
        }
        ui.clueButton = Button("ClueButton", hud.transform, V(0.01f, 0.22f), V(0.28f, 0.29f), "Reveal clue 1", 26, res);
        ui.clueButtonLabel = ui.clueButton.GetComponentInChildren<TMP_Text>();
        ui.maxScoreText = Label("MaxScoreText", hud.transform, V(0.01f, 0.17f), V(0.28f, 0.22f), "Max points now: 1000", 26, TextAlignmentOptions.Left);

        var answers = Rect("AnswerPanel", hud.transform, V(0.10f, 0.03f), V(0.90f, 0.15f)); ui.answerPanel = answers;
        ui.answerButtons = new Button[4];
        for (int i = 0; i < 4; i++)
            ui.answerButtons[i] = Button("Answer" + (i + 1), answers.transform, V(i * 0.25f + 0.005f, 0), V((i + 1) * 0.25f - 0.005f, 1), "Option", 32, res);

        var typed = Rect("TypedPanel", hud.transform, V(0.25f, 0.03f), V(0.75f, 0.12f)); ui.typedPanel = typed;
        var inputGO = TMP_DefaultControls.CreateInputField(res); inputGO.name = "TypedInput"; Place(inputGO, typed.transform, V(0, 0), V(0.75f, 1));
        ui.typedInput = inputGO.GetComponent<TMP_InputField>();
        ui.typedInput.textComponent.fontSize = 34;
        var ph = ui.typedInput.placeholder as TMP_Text; if (ph != null) { ph.text = "Type the country name…"; ph.fontSize = 34; }
        ui.typedSubmitButton = Button("SubmitButton", typed.transform, V(0.77f, 0), V(1, 1), "Guess", 32, res);

        var find = Box("FindItPanel", hud.transform, V(0.33f, 0.80f), V(0.67f, 0.92f), Dark); ui.findItPanel = find;
        ui.findItFlag = Img("FindItFlag", find.transform, V(0.03f, 0.10f), V(0.30f, 0.90f));
        ui.findItName = Label("FindItName", find.transform, V(0.32f, 0), V(0.98f, 1), "Find: ?", 44, TextAlignmentOptions.Left);

        var fb = Box("FeedbackPanel", hud.transform, V(0.70f, 0.28f), V(0.98f, 0.76f), new Color(0, 0, 0, 0.75f)); ui.feedbackPanel = fb;
        ui.feedbackTitle = Label("FeedbackTitle", fb.transform, V(0.05f, 0.76f), V(0.95f, 0.96f), "Correct!", 52, TextAlignmentOptions.Center);
        ui.feedbackFlag = Img("FeedbackFlag", fb.transform, V(0.20f, 0.47f), V(0.80f, 0.74f));
        ui.feedbackDetail = Label("FeedbackDetail", fb.transform, V(0.05f, 0.22f), V(0.95f, 0.46f), "Country", 34, TextAlignmentOptions.Center);
        ui.nextButton = Button("NextButton", fb.transform, V(0.20f, 0.04f), V(0.80f, 0.18f), "Next", 34, res);

        // ---------------- results
        var results = Box("ResultsPanel", root, V(0, 0), V(1, 1), new Color(0, 0, 0, 0.78f)); ui.resultsPanel = results;
        Label("ResultsTitle", results.transform, V(0.2f, 0.72f), V(0.8f, 0.88f), "RESULTS", 80, TextAlignmentOptions.Center);
        ui.resultsScore = Label("ResultsScore", results.transform, V(0.2f, 0.60f), V(0.8f, 0.70f), "Total score", 56, TextAlignmentOptions.Center);
        ui.resultsAccuracy = Label("ResultsAccuracy", results.transform, V(0.2f, 0.51f), V(0.8f, 0.59f), "Accuracy", 44, TextAlignmentOptions.Center);
        ui.resultsDetail = Label("ResultsDetail", results.transform, V(0.2f, 0.44f), V(0.8f, 0.50f), "Details", 32, TextAlignmentOptions.Center);
        ui.playAgainButton = Button("PlayAgainButton", results.transform, V(0.38f, 0.29f), V(0.62f, 0.37f), "Play again", 38, res);
        ui.menuButton = Button("MenuButton", results.transform, V(0.38f, 0.18f), V(0.62f, 0.26f), "Main menu", 34, res);

        // initial visibility (GameManager also sets this at start)
        hud.SetActive(false); results.SetActive(false); fb.SetActive(false); find.SetActive(false); typed.SetActive(false);

        // ---------------- game manager wiring
        var clues = game.GetComponent<ClueSystem>() ?? Undo.AddComponent<ClueSystem>(game);
        var gm = game.GetComponent<GameManager>() ?? Undo.AddComponent<GameManager>(game);
        gm.database = db; gm.ui = ui; gm.clues = clues; gm.highlighter = highlighter;
        gm.cameraFocus = focus; gm.picker = picker; gm.globe = globe;

        Undo.RegisterCreatedObjectUndo(canvasGO, "Build Geoguess UI");
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = canvasGO;
        Debug.Log("Geoguess: UI e GameManager criados. Salve a cena (Ctrl+S) e aperte Play.");
    }

    // ---------------------------------------------------------------- helpers
    static Vector2 V(float x, float y) => new Vector2(x, y);

    static void EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var std = es.GetComponent<StandaloneInputModule>(); if (std != null) Object.DestroyImmediate(std);
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
#else
        if (es.GetComponent<BaseInputModule>() == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    static GameObject Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Place(go, parent, min, max); return go;
    }

    static void Place(GameObject go, Transform parent, Vector2 min, Vector2 max)
    {
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
    }

    static GameObject Box(string name, Transform parent, Vector2 min, Vector2 max, Color bg)
    {
        var go = Rect(name, parent, min, max); go.AddComponent<Image>().color = bg; return go;
    }

    static Image Img(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = Rect(name, parent, min, max); var img = go.AddComponent<Image>(); img.preserveAspect = true; img.raycastTarget = false; return img;
    }

    static TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max, string text, float size, TextAlignmentOptions align)
    {
        var go = Rect(name, parent, min, max);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false;
        return t;
    }

    static Button Button(string name, Transform parent, Vector2 min, Vector2 max, string label, float size, TMP_DefaultControls.Resources res)
    {
        var go = TMP_DefaultControls.CreateButton(res); go.name = name; Place(go, parent, min, max);
        var t = go.GetComponentInChildren<TMP_Text>(); t.text = label; t.fontSize = size;
        return go.GetComponent<Button>();
    }
}
#endif
