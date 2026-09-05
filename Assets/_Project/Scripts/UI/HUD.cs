using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Runtime UI with a shared visual language and explicit run-state screens.</summary>
public class HUD : MonoBehaviour
{
    private Font _font;
    private RectTransform _root;
    private GameObject _menu;
    private Text _title, _body, _actionText, _stats, _status, _prompt, _banner;
    private Button _action;
    private Image[] _health;
    private GameObject _outfit;
    private Text _outfitTitle;
    private Text[] _outfitLines;
    private Image _dash, _time;
    private float _bannerUntil;
    private GameManager.RunState _shown = (GameManager.RunState)(-1);
    private static readonly Color Ink = new Color(0.025f, 0.043f, 0.065f, 0.96f);
    private static readonly Color Muted = new Color(0.53f, 0.65f, 0.71f);

    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scale = go.GetComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1600, 900);
        scale.matchWidthOrHeight = 0.5f;
        _root = go.GetComponent<RectTransform>();
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var top = Panel(_root, "Run info", new Vector2(0, 1), new Vector2(30, -30), new Vector2(350, 115), Ink);
        Label(top, "S T I L L   /   VITALS", 16, new Vector2(18, -12), new Vector2(310, 24), Muted);
        _health = new Image[12];
        for (int i = 0; i < _health.Length; i++)
            _health[i] = Panel(top, "Health", new Vector2(0, 1), new Vector2(18 + i * 26, -50), new Vector2(20, 8), Palette.Heart).GetComponent<Image>();
        _stats = Label(top, "", 17, new Vector2(18, -78), new Vector2(315, 25), Color.white);
        var bottom = Panel(_root, "Ability readout", new Vector2(0, 0), new Vector2(30, 30), new Vector2(280, 95), Ink);
        _status = Label(bottom, "", 15, new Vector2(18, -12), new Vector2(250, 24), Palette.PlayerGlow);
        _time = Panel(bottom, "World time", new Vector2(0, 1), new Vector2(18, -42), new Vector2(244, 3), Palette.PlayerGlow).GetComponent<Image>();
        Label(bottom, "SPACE  /  DASH        SHIFT  /  WALK", 14, new Vector2(18, -54), new Vector2(244, 20), Muted);
        _dash = Panel(bottom, "Dash readiness", new Vector2(0, 1), new Vector2(18, -82), new Vector2(244, 3), Color.white).GetComponent<Image>();
        _prompt = CenterLabel(_root, "", 20, new Vector2(0, -315), new Vector2(800, 60), Color.white);
        _banner = CenterLabel(_root, "", 44, new Vector2(0, 240), new Vector2(800, 90), Color.white);
        var pause = MakeButton(_root, "ESC  /  PAUSE", new Vector2(1, 1), new Vector2(-30, -30), new Vector2(180, 44), () => GameManager.Instance?.Pause());

        _menu = Panel(_root, "Menu", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5000, 5000), new Color(0.012f, 0.023f, 0.036f, 0.86f)).gameObject;
        _menu.GetComponent<Image>().raycastTarget = true;
        var card = Panel((RectTransform)_menu.transform, "Menu card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780, 680), Ink);
        Panel(card, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(780, 3), Palette.PlayerGlow);
        CenterLabel(card, "A   T A C T I C A L   D E S C E N T", 15, new Vector2(0, 265), new Vector2(700, 25), Palette.PlayerGlow);
        _title = CenterLabel(card, "S T I L L", 76, new Vector2(0, 184), new Vector2(730, 100), Color.white);
        _body = CenterLabel(card, "", 20, new Vector2(0, 53), new Vector2(670, 135), Muted);
        _action = MakeButton(card, "BEGIN DESCENT", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(350, 56), PrimaryAction);
        _actionText = _action.GetComponentInChildren<Text>();
        CenterLabel(card, "WASD  Move     SHIFT  Walk slowly     MOUSE  Aim     CLICK  Strike\nSPACE  Dash     E  Threshold     ESC  Pause", 17, new Vector2(0, -144), new Vector2(700, 70), Muted);
        MakeButton(card, "VOLUME -", new Vector2(0.5f, 0.5f), new Vector2(-185, -222), new Vector2(150, 38), () => Volume(-0.1f));
        MakeButton(card, "VOLUME +", new Vector2(0.5f, 0.5f), new Vector2(0, -222), new Vector2(150, 38), () => Volume(0.1f));
        var effects = MakeButton(card, "", new Vector2(0.5f, 0.5f), new Vector2(185, -222), new Vector2(170, 38), () => {});
        var effectsText = effects.GetComponentInChildren<Text>();
        effectsText.text = ReducedEffects ? "FX: REDUCED" : "FX: FULL";
        effects.onClick.AddListener(() => {
            PlayerPrefs.SetInt("Still.ReducedEffects", ReducedEffects ? 0 : 1);
            PlayerPrefs.Save();
            effectsText.text = ReducedEffects ? "FX: REDUCED" : "FX: FULL";
        });
        CenterLabel(card, "STOP TO THINK. MOVE TO COMMIT.", 13, new Vector2(0, -294), new Vector2(680, 24), Muted);

        BuildThreshold();
    }

    public static bool ReducedEffects => PlayerPrefs.GetInt("Still.ReducedEffects", 0) == 1;
    private static void Volume(float amount)
    {
        AudioListener.volume = Mathf.Clamp01(AudioListener.volume + amount);
        PlayerPrefs.SetFloat("Still.Volume", AudioListener.volume);
        PlayerPrefs.Save();
    }
    private void PrimaryAction()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (gm.State == GameManager.RunState.Paused) gm.Resume();
        else gm.StartRun();
        EventSystem.current?.SetSelectedGameObject(null);
    }
    private void OnEnable() { GameEvents.FloorChanged += FloorChanged; }
    private void OnDisable() { GameEvents.FloorChanged -= FloorChanged; }
    private void FloorChanged(int floor) { _banner.text = $"DEPTH {floor:00}"; _bannerUntil = Time.unscaledTime + 2.4f; }
    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        bool playing = gm.State == GameManager.RunState.Playing;
        bool outfitting = gm.State == GameManager.RunState.Outfitting;
        _action.interactable = gm.CanRestart;

        // The pause card shows for every non-Playing state, so it has to be told about the
        // threshold explicitly or both would stack.
        _menu.SetActive(!playing && !outfitting);
        _outfit.SetActive(outfitting);
        if (outfitting) UpdateThreshold(gm);
        if (_shown != gm.State)
        {
            _shown = gm.State;
            _title.text = gm.IsGameOver ? "FRACTURED" : gm.State == GameManager.RunState.Paused ? "SUSPENDED" : "S T I L L";
            _actionText.text = gm.IsGameOver ? "DESCEND AGAIN  /  R" : gm.State == GameManager.RunState.Paused ? "RESUME  /  ESC" : "BEGIN DESCENT  /  ENTER";
        }
        _body.text = gm.IsGameOver
            ? $"Depth {gm.Floor:00}   /   {gm.Shards} shards   /   {Mathf.FloorToInt(gm.RunSeconds / 60):00}:{Mathf.FloorToInt(gm.RunSeconds % 60):00}\nBest depth: {gm.BestFloor:00}\nEvery descent is a new chance."
            : gm.State == GameManager.RunState.Paused ? "Take a breath. Your descent is waiting.\nStopping slows time. Attacking and dashing advance it."
            : "Explore the ruins. Collect shards. Find the way below.\nStopping slows the world; movement, aim and attacks advance it.\nRead the danger, then commit.";
        _stats.text = $"DEPTH {gm.Floor:00}     /     {gm.Shards:000} SHARDS";
        var hp = GameManager.PlayerHealth;
        for (int i = 0; i < _health.Length; i++)
        {
            _health[i].gameObject.SetActive(hp != null && i < hp.Max);
            _health[i].color = hp != null && i < hp.Current ? Palette.Heart : new Color(0.22f, 0.16f, 0.2f);
        }
        var player = GameManager.PlayerTransform != null ? GameManager.PlayerTransform.GetComponent<PlayerController>() : null;
        _dash.rectTransform.localScale = new Vector3(player != null ? player.DashReady01 : 1, 1, 1);
        _time.rectTransform.localScale = new Vector3(Mathf.Max(0.02f, WorldTime.Flow), 1, 1);
        _status.text = WorldTime.Flow < 0.15f ? "TIME  /  STILL" : "TIME  /  FLOWING";
        _prompt.text = playing && Stairs.NearExit ? "[ E ]  THE THRESHOLD   /   Spend shards, then descend" : playing && gm.Floor == 1 && gm.RunSeconds < 15 ? "Find the green beacon. Collect shards along the way." : "";
        _banner.color = new Color(1, 1, 1, playing ? Mathf.Clamp01(_bannerUntil - Time.unscaledTime) : 0);
    }
    /// <summary>
    /// The threshold: shown when the player has chosen the stairs but not yet committed.
    ///
    /// Structurally a clone of the pause card — full-screen scrim, Ink card, accent stripe —
    /// because that is the only modal shape this project has. The rows are four fixed Texts
    /// rather than any kind of list, since there is no list, layout or scroll UI anywhere here
    /// and four never needs one.
    /// </summary>
    private void BuildThreshold()
    {
        _outfit = Panel(_root, "Threshold", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5000, 5000),
                        new Color(0.012f, 0.023f, 0.036f, 0.86f)).gameObject;
        _outfit.GetComponent<Image>().raycastTarget = true;

        var card = Panel((RectTransform)_outfit.transform, "Threshold card", new Vector2(0.5f, 0.5f),
                         Vector2.zero, new Vector2(780, 560), Ink);
        Panel(card, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(780, 3), Palette.Exit);

        CenterLabel(card, "T H E   T H R E S H O L D", 15, new Vector2(0, 216), new Vector2(700, 25), Palette.Exit);
        _outfitTitle = CenterLabel(card, "", 44, new Vector2(0, 158), new Vector2(730, 70), Color.white);

        _outfitLines = new Text[PlayerUpgrades.LineCount];
        for (int i = 0; i < _outfitLines.Length; i++)
            _outfitLines[i] = CenterLabel(card, "", 20, new Vector2(0, 62 - i * 42), new Vector2(700, 34), Color.white);

        CenterLabel(card, "Spend what you found before you go deeper.", 17, new Vector2(0, -122), new Vector2(700, 28), Muted);
        CenterLabel(card, "What you buy lasts only as long as this run.", 17, new Vector2(0, -150), new Vector2(700, 28), Muted);
        CenterLabel(card, "ENTER  /  DESCEND          ESC  /  NOT YET", 17, new Vector2(0, -206), new Vector2(700, 28), Palette.PlayerGlow);
    }

    /// <summary>Refresh the threshold rows. Cheap enough to just rebuild the strings each frame.</summary>
    private void UpdateThreshold(GameManager gm)
    {
        _outfitTitle.text = $"{gm.Shards:000} SHARDS";

        var upgrades = GameManager.Upgrades;
        for (int i = 0; i < _outfitLines.Length; i++)
        {
            if (upgrades == null) { _outfitLines[i].text = ""; continue; }

            var kind = (UpgradeKind)i;
            _outfitLines[i].text = upgrades.Describe(kind);

            // Dim what cannot be bought right now, so cost reads at a glance.
            bool affordable = !upgrades.IsMaxed(kind) && gm.Shards >= upgrades.CostOf(kind);
            _outfitLines[i].color = upgrades.IsMaxed(kind) ? Palette.Exit : affordable ? Color.white : Muted;
        }
    }

    private RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = offset; rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        return rt;
    }
    private Text Label(RectTransform parent, string value, int size, Vector2 offset, Vector2 dimensions, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = offset; rt.sizeDelta = dimensions;
        var text = go.GetComponent<Text>();
        text.font = _font; text.fontSize = size; text.text = value; text.color = color; text.raycastTarget = false;
        return text;
    }
    private Text CenterLabel(RectTransform parent, string value, int size, Vector2 offset, Vector2 dimensions, Color color)
    {
        var t = Label(parent, value, size, offset, dimensions, color);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        t.alignment = TextAnchor.MiddleCenter;
        return t;
    }
    private Button MakeButton(RectTransform parent, string value, Vector2 anchor, Vector2 offset, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var rt = Panel(parent, value, anchor, offset, size, new Color(0.09f, 0.22f, 0.28f));
        rt.GetComponent<Image>().raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = rt.GetComponent<Image>();
        var colors = b.colors; colors.highlightedColor = new Color(0.5f, 0.9f, 1f); colors.pressedColor = new Color(0.25f, 0.65f, 0.8f); b.colors = colors;
        b.onClick.AddListener(action);
        CenterLabel(rt, value, 16, Vector2.zero, size, Color.white);
        return b;
    }
}
