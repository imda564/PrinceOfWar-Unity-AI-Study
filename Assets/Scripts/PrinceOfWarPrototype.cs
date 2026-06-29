using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PrinceOfWarPrototype : MonoBehaviour
{
    private const float LaneY = -1.95f;
    private const float HeroMinY = -3.25f;
    private const float HeroMaxY = -0.8f;
    private const float OriginalEnemyLaneReferenceY = 225.2f;
    private const float OriginalPixelsPerWorldUnit = 36f;
    private const float RoamingOrcMoveSpeed = 0.62f;
    private const float PlayerBaseX = -6.45f;
    private const float EnemyBaseX = 6.45f;
    private const float EnemySpawnX = EnemyBaseX + 1.35f;
    private const float MeteorTestGolemX = 3.35f;
    private const float EnemyRangedHoldX = EnemyBaseX - 1.65f;
    private const int HealCost = 25;
    private const int MaxStage = 20;
    private const int GoldPerSecond = 5;
    private const float EnemyRespawnDelay = 1.15f;
    private const string MusicVolumePrefsKey = "PrinceOfWar.MusicVolume";
    private const string SfxVolumePrefsKey = "PrinceOfWar.SfxVolume";
    private const int MeteorWarningClusterCount = 5;
    private const int MeteorRangedClusterCount = 7;
    private const float MeteorClusterRadius = 1.05f;
    private const float MeteorStrikeRadius = 1.12f;
    private const float MeteorWarningDelay = 2.7f;
    private const float MeteorCooldown = 7.5f;
    private const float MeteorClusterWarningCooldown = 2.8f;
    private const float ArcherCooldownPerExtraUnit = 1.1f;
    private const float HealerCooldownPerExtraUnit = 1.6f;
    public const float EnemyDamageToHero = 2f;
    public const float CharacterMoveSpeedMultiplier = 0.46f;
    private static readonly Color AllyColor = new Color(0.2f, 0.55f, 1f);
    private static readonly Color EnemyColor = new Color(0.92f, 0.22f, 0.2f);
    private static readonly bool BossMeteorEnabled = false;

    private readonly List<LaneUnit> units = new();
    private readonly List<RoamingOrcUnit> roamingOrcs = new();
    private readonly List<FloatingText> floatingTexts = new();
    private readonly List<PendingEnemySpawn> pendingEnemySpawns = new();
    private readonly List<PendingMeteor> pendingMeteors = new();
    private readonly Dictionary<string, float> trainingCooldowns = new();
    private readonly Dictionary<string, CharacterAnimationSet> characterAnimations = new();
    private readonly Dictionary<string, Sprite> recruitIcons = new();
    private readonly Dictionary<int, AudioClip> soundClips = new();
    private HeroUnit hero;
    private AudioSource musicSource;
    private AudioSource sfxSource;
    private Sprite squareSprite;
    private Texture2D whitePixel;
    private Texture2D stageIntroTexture;
    private readonly Dictionary<string, Texture2D> uiTextures = new();
    private readonly Dictionary<string, GUIStyle> uiButtonStyles = new();
    private Camera mainCamera;
    private Rect uiPanel;
    private Sprite meteorCoreSprite;
    private Sprite meteorGlowSprite;
    private Sprite meteorRingSprite;
    private Sprite lightningBoltSprite;
    private Sprite lightningGlowSprite;

    private GameScreen screen = GameScreen.MainMenu;
    private MainMenuPopup mainMenuPopup = MainMenuPopup.None;
    private int playerGold = 120;
    private int stage = 1;
    private int highestUnlockedStage = 1;
    private int morale = 6;
    private int enemiesToSpawn = 18;
    private int enemyInitialSlotsToSpawn;
    private int enemyReserveGold;
    private int enemiesSpawned;
    private int enemiesKilled;
    private int soldiersEscorted;
    private float enemySpawnTimer;
    private float passiveGoldTimer;
    private float musicVolume = 0.32f;
    private float sfxVolume = 0.75f;
    private float meteorCooldownTimer;
    private float meteorClusterWarningTimer;
    private bool forceWizardLightningOnce;
    private bool stageCleared;
    private bool gameOver;
    private bool isPaused;
    private string resultText = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<PrinceOfWarPrototype>() != null)
        {
            return;
        }

        var game = new GameObject("Prince Of War Prototype");
        game.AddComponent<PrinceOfWarPrototype>();
    }

    private void Awake()
    {
        squareSprite = CreateSquareSprite();
        CreateMeteorSprites();
        LoadCharacterSprites();
        whitePixel = new Texture2D(1, 1);
        whitePixel.SetPixel(0, 0, Color.white);
        whitePixel.Apply();
        LoadAudioSettings();
        LoadOriginalSounds();
        highestUnlockedStage = Mathf.Clamp(PlayerPrefs.GetInt("PrinceOfWar.HighestUnlockedStage", 1), 1, MaxStage);
        LoadOriginalUi();

        SetupCamera();
        BuildWorld();
        StartBackgroundMusic();
    }

    private void Update()
    {
        if (screen == GameScreen.MainMenu)
        {
            if (mainMenuPopup != MainMenuPopup.None)
            {
                if (WasKeyPressed(Key.Escape) || WasKeyPressed(Key.Enter) || WasKeyPressed(Key.Space))
                {
                    mainMenuPopup = MainMenuPopup.None;
                }

                return;
            }

            if (WasKeyPressed(Key.Enter) || WasKeyPressed(Key.Space))
            {
                StartBattle(Mathf.Clamp(highestUnlockedStage, 1, MaxStage));
                return;
            }

            if (WasKeyPressed(Key.S))
            {
                ShowStageSelect();
            }

            if (WasKeyPressed(Key.C))
            {
                mainMenuPopup = MainMenuPopup.Credits;
            }

            if (WasKeyPressed(Key.O))
            {
                mainMenuPopup = MainMenuPopup.Options;
            }

            return;
        }

        if (screen == GameScreen.StageSelect)
        {
            if (WasKeyPressed(Key.Escape))
            {
                ShowMainMenu();
                return;
            }

            HandleStageSelectHotkeys();
            return;
        }

        if (gameOver)
        {
            if (stageCleared && WasKeyPressed(Key.Enter))
            {
                StartNextStage();
                return;
            }

            if (WasKeyPressed(Key.R))
            {
                ResetBattle();
            }

            if (WasKeyPressed(Key.Escape))
            {
                ShowStageSelect();
            }

            return;
        }

        if (WasKeyPressed(Key.Escape))
        {
            isPaused = !isPaused;
            return;
        }

        if (isPaused)
        {
            return;
        }

        enemySpawnTimer += Time.deltaTime;
        TickPassiveGoldIncome(Time.deltaTime);
        TickTrainingCooldowns(Time.deltaTime);
        TickPendingEnemyRespawns(Time.deltaTime);
        TickBossMeteors(Time.deltaTime);

        if (HasInitialEnemiesLeftToSpawn() && enemySpawnTimer >= Mathf.Max(0.9f, 3.2f - stage * 0.12f))
        {
            enemySpawnTimer = 0f;
            TrySpawnNextEnemy();

            if (enemiesSpawned % 6 == 0 && HasInitialEnemiesLeftToSpawn())
            {
                SpawnEnemyWave();
            }
        }

        if (hero != null)
        {
            hero.ManualUpdate(Time.deltaTime);
        }

        TickRoamingOrcs(Time.deltaTime);

        var roster = UnitStats.AllyRoster();
        for (var i = 0; i < roster.Length; i++)
        {
            if (WasRecruitHotkeyPressed(i))
            {
                TryBuy(roster[i]);
                break;
            }
        }

        TickUnits();
        TickFloatingTexts();
        CheckMissionState();
    }

    private void OnGUI()
    {
        EnsureGuiSkin();
        var guiScale = UiScale();

        if (screen == GameScreen.MainMenu)
        {
            DrawMainMenu();
            return;
        }

        if (screen == GameScreen.StageSelect)
        {
            DrawStageSelect();
            return;
        }

        DrawStatusPanel();
        DrawRecruitButtons();
        DrawMeteorTestControls();
        DrawFloatingText();

        if (isPaused)
        {
            DrawPauseMenu();
            return;
        }

        if (gameOver)
        {
            var box = new Rect(Screen.width * 0.5f - 190f * guiScale, Screen.height * 0.5f - 75f * guiScale, 380f * guiScale, 150f * guiScale);
            GUI.Box(box, string.Empty);
            GUI.Label(new Rect(box.x + 20f * guiScale, box.y + 25f * guiScale, box.width - 40f * guiScale, 36f * guiScale), resultText, CenterLabel(28, Color.white));
            GUI.Label(new Rect(box.x + 20f * guiScale, box.y + 72f * guiScale, box.width - 40f * guiScale, 24f * guiScale), stageCleared ? "Enter: next stage / Esc: stages" : "R: restart / Esc: stages", CenterLabel(18, Color.yellow));
            if (GUI.Button(new Rect(box.x + 35f * guiScale, box.y + 105f * guiScale, 145f * guiScale, 30f * guiScale), "Stage Select"))
            {
                ShowStageSelect();
            }

            if (GUI.Button(new Rect(box.x + 200f * guiScale, box.y + 105f * guiScale, 145f * guiScale, 30f * guiScale), "Restart"))
            {
                ResetBattle();
            }
        }
    }

    public bool WasKeyPressed(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }

    public bool IsKeyHeld(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].isPressed;
    }

    public bool WasPrimaryAttackPressed()
    {
        return WasKeyPressed(Key.Space) || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    public bool IsPrimaryAttackHeld()
    {
        return IsKeyHeld(Key.Space) || Mouse.current != null && Mouse.current.leftButton.isPressed;
    }

    private void HandleStageSelectHotkeys()
    {
        for (var i = 0; i <= MaxStage; i++)
        {
            if (WasStageHotkeyPressed(i))
            {
                if (i == 0 || i <= highestUnlockedStage)
                {
                    StartBattle(i);
                }

                return;
            }
        }
    }

    private bool WasStageHotkeyPressed(int stageNumber)
    {
        return stageNumber switch
        {
            0 => WasKeyPressed(Key.Digit0) || WasKeyPressed(Key.Numpad0),
            1 => WasKeyPressed(Key.Digit1) || WasKeyPressed(Key.Numpad1),
            2 => WasKeyPressed(Key.Digit2) || WasKeyPressed(Key.Numpad2),
            3 => WasKeyPressed(Key.Digit3) || WasKeyPressed(Key.Numpad3),
            4 => WasKeyPressed(Key.Digit4) || WasKeyPressed(Key.Numpad4),
            5 => WasKeyPressed(Key.Digit5) || WasKeyPressed(Key.Numpad5),
            6 => WasKeyPressed(Key.Digit6) || WasKeyPressed(Key.Numpad6),
            7 => WasKeyPressed(Key.Digit7) || WasKeyPressed(Key.Numpad7),
            8 => WasKeyPressed(Key.Digit8) || WasKeyPressed(Key.Numpad8),
            9 => WasKeyPressed(Key.Digit9) || WasKeyPressed(Key.Numpad9),
            _ => false
        };
    }

    private bool WasRecruitHotkeyPressed(int rosterIndex)
    {
        return rosterIndex switch
        {
            0 => WasKeyPressed(Key.Digit1) || WasKeyPressed(Key.Numpad1),
            1 => WasKeyPressed(Key.Digit2) || WasKeyPressed(Key.Numpad2),
            2 => WasKeyPressed(Key.Digit3) || WasKeyPressed(Key.Numpad3),
            3 => WasKeyPressed(Key.Digit4) || WasKeyPressed(Key.Numpad4),
            4 => WasKeyPressed(Key.Digit5) || WasKeyPressed(Key.Numpad5),
            5 => WasKeyPressed(Key.Digit6) || WasKeyPressed(Key.Numpad6),
            6 => WasKeyPressed(Key.Digit7) || WasKeyPressed(Key.Numpad7),
            7 => WasKeyPressed(Key.Digit8) || WasKeyPressed(Key.Numpad8),
            8 => WasKeyPressed(Key.Digit9) || WasKeyPressed(Key.Numpad9),
            9 => WasKeyPressed(Key.Digit0) || WasKeyPressed(Key.Numpad0),
            _ => false
        };
    }

    private void DrawMainMenu()
    {
        DrawMainMenuBackdrop();

        var titleRect = OriginalRect(new Rect(58f, 54f, 430f, 54f));
        DrawShadowLabel(titleRect, "Prince of War", 44, new Color(1f, 0.78f, 0.28f), TextAnchor.MiddleLeft);
        DrawShadowLabel(OriginalRect(new Rect(62f, 112f, 380f, 24f)), "Raise the line. Hold the western gate.", 15, new Color(0.86f, 0.82f, 0.72f), TextAnchor.MiddleLeft);
        DrawMainMenuProgress(OriginalRect(new Rect(62f, 286f, 330f, 34f)));

        if (mainMenuPopup != MainMenuPopup.None)
        {
            DrawMainMenuPopup();
            return;
        }

        var continueStage = Mathf.Clamp(highestUnlockedStage, 1, MaxStage);
        if (DrawMainMenuButton(OriginalRect(new Rect(514f, 118f, 202f, 44f)), "Continue", "Stage " + continueStage.ToString("00"), true))
        {
            StartBattle(continueStage);
        }

        if (DrawMainMenuButton(OriginalRect(new Rect(514f, 170f, 202f, 40f)), "Stage Select", "Choose battlefield", false))
        {
            ShowStageSelect();
        }

        if (DrawMainMenuButton(OriginalRect(new Rect(514f, 218f, 202f, 40f)), "Options", "Sound levels", false))
        {
            mainMenuPopup = MainMenuPopup.Options;
        }

        if (DrawMainMenuButton(OriginalRect(new Rect(514f, 266f, 202f, 40f)), "Credits", "Project notes", false))
        {
            mainMenuPopup = MainMenuPopup.Credits;
        }

        if (DrawMainMenuButton(OriginalRect(new Rect(514f, 314f, 202f, 40f)), "Exit", "Close game", false))
        {
            Application.Quit();
        }
    }

    private void DrawStageSelect()
    {
        DrawOriginalScreen(stageIntroTexture);
        DrawDarkOverlay(0.55f);

        var previewStage = Mathf.Clamp(highestUnlockedStage, 1, MaxStage);
        var gridPanel = OriginalRect(new Rect(34f, 78f, 496f, 282f));
        var detailPanel = OriginalRect(new Rect(548f, 78f, 218f, 282f));

        GUI.Label(OriginalRect(new Rect(40f, 26f, 720f, 34f)), "Choose Your Battlefield", CenterLabel(30, new Color(1f, 0.84f, 0.38f)));
        DrawStageSelectPanel(gridPanel, new Color(0.035f, 0.04f, 0.045f, 0.94f), new Color(0.72f, 0.56f, 0.26f, 0.95f));
        DrawStageSelectPanel(detailPanel, new Color(0.04f, 0.035f, 0.03f, 0.95f), new Color(0.72f, 0.56f, 0.26f, 0.95f));

        var testRect = OriginalRect(new Rect(224f, 326f, 126f, 28f));
        var testHovered = testRect.Contains(Event.current.mousePosition);
        if (testHovered)
        {
            previewStage = 0;
        }

        if (GUI.Button(testRect, "Meteor Test", OriginalSmallButtonStyle(true)))
        {
            StartBattle(0);
        }

        for (var i = 1; i <= MaxStage; i++)
        {
            var column = (i - 1) % 5;
            var row = (i - 1) / 5;
            var rect = OriginalRect(new Rect(58f + column * 92f, 116f + row * 52f, 72f, 38f));
            var unlocked = i <= highestUnlockedStage;
            var hovered = rect.Contains(Event.current.mousePosition);
            if (hovered)
            {
                previewStage = i;
            }

            DrawStageCard(i, rect, unlocked, hovered);
            var previousEnabled = GUI.enabled;
            GUI.enabled = unlocked;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                StartBattle(i);
            }

            GUI.enabled = previousEnabled;
        }

        DrawStageDetails(detailPanel, previewStage);

        if (GUI.Button(OriginalRect(new Rect(64f, 326f, 124f, 28f)), "Back", OriginalSmallButtonStyle(true)))
        {
            ShowMainMenu();
        }

        var canStartPreview = previewStage == 0 || previewStage <= highestUnlockedStage;
        var previousEnabledForStart = GUI.enabled;
        GUI.enabled = canStartPreview;
        if (GUI.Button(OriginalRect(new Rect(622f, 326f, 112f, 28f)), canStartPreview ? "Start" : "Locked", OriginalSmallButtonStyle(canStartPreview)))
        {
            StartBattle(previewStage);
        }

        GUI.enabled = previousEnabledForStart;
    }

    private void DrawStageSelectPanel(Rect rect, Color fill, Color border)
    {
        var previousColor = GUI.color;
        GUI.color = fill;
        GUI.DrawTexture(rect, whitePixel);
        GUI.color = new Color(0f, 0f, 0f, 0.34f);
        GUI.DrawTexture(new Rect(rect.x + rect.width * 0.025f, rect.y + rect.height * 0.04f, rect.width * 0.95f, rect.height * 0.12f), whitePixel);
        GUI.color = border;
        var line = Mathf.Max(1f, rect.width * 0.006f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, line), whitePixel);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - line, rect.width, line), whitePixel);
        GUI.DrawTexture(new Rect(rect.x, rect.y, line, rect.height), whitePixel);
        GUI.DrawTexture(new Rect(rect.xMax - line, rect.y, line, rect.height), whitePixel);
        GUI.color = previousColor;
    }

    private void DrawStageCard(int stageNumber, Rect rect, bool unlocked, bool hovered)
    {
        var previousColor = GUI.color;
        var active = stageNumber == highestUnlockedStage && unlocked;
        GUI.color = !unlocked
            ? new Color(0.05f, 0.05f, 0.055f, 0.92f)
            : hovered
            ? new Color(0.2f, 0.17f, 0.1f, 0.98f)
            : active
            ? new Color(0.13f, 0.11f, 0.075f, 0.96f)
            : new Color(0.08f, 0.085f, 0.085f, 0.94f);
        GUI.DrawTexture(rect, whitePixel);

        GUI.color = unlocked ? new Color(0.88f, 0.64f, 0.27f, hovered ? 1f : 0.78f) : new Color(0.22f, 0.22f, 0.22f, 0.9f);
        var line = Mathf.Max(1f, rect.width * 0.035f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, line), whitePixel);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - line, rect.width, line), whitePixel);
        GUI.DrawTexture(new Rect(rect.x, rect.y, line, rect.height), whitePixel);
        GUI.DrawTexture(new Rect(rect.xMax - line, rect.y, line, rect.height), whitePixel);

        GUI.color = unlocked ? new Color(0.88f, 0.64f, 0.27f, 0.9f) : new Color(0.18f, 0.18f, 0.18f, 0.9f);
        GUI.DrawTexture(new Rect(rect.x + line, rect.y + line, rect.width - line * 2f, Mathf.Max(2f, rect.height * 0.12f)), whitePixel);
        GUI.color = previousColor;

        GUI.Label(rect, unlocked ? stageNumber.ToString("00") : "--", CenterLabel(19, unlocked ? Color.white : new Color(0.42f, 0.42f, 0.42f)));
    }

    private void DrawStageDetails(Rect panel, int stageNumber)
    {
        var unlocked = stageNumber == 0 || stageNumber <= highestUnlockedStage;
        GUI.Label(new Rect(panel.x + 16f, panel.y + 18f, panel.width - 32f, 28f), "Stage " + stageNumber.ToString("00"), Label(22, unlocked ? new Color(1f, 0.84f, 0.38f) : new Color(0.5f, 0.5f, 0.5f)));
        GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, panel.width - 32f, 22f), GetStageSelectTitle(stageNumber), Label(13, unlocked ? Color.white : new Color(0.45f, 0.45f, 0.45f)));

        DrawStageMetric(panel.x + 16f, panel.y + 86f, "Gold", UnitStats.StartingGold(stageNumber).ToString(), unlocked);
        DrawStageMetric(panel.x + 112f, panel.y + 86f, "Reserve", UnitStats.EnemyGoldBudget(stageNumber).ToString(), unlocked);
        DrawStageMetric(panel.x + 16f, panel.y + 136f, "Roamers", UnitStats.RoamingOrcCount(stageNumber).ToString(), unlocked);
        DrawStageMetric(panel.x + 112f, panel.y + 136f, "Roster", UnitStats.EnemyRoster(stageNumber).Length.ToString(), unlocked);

        GUI.Label(new Rect(panel.x + 16f, panel.y + 194f, panel.width - 32f, 18f), "Enemy Force", Label(12, unlocked ? new Color(0.88f, 0.64f, 0.27f) : new Color(0.45f, 0.45f, 0.45f)));
        GUI.Label(new Rect(panel.x + 16f, panel.y + 214f, panel.width - 32f, 52f), unlocked ? BuildEnemySummary(stageNumber) : "Locked", WrappedLabel(12, unlocked ? Color.white : new Color(0.45f, 0.45f, 0.45f)));
    }

    private void DrawStageMetric(float x, float y, string label, string value, bool unlocked)
    {
        var rect = new Rect(x, y, 78f, 38f);
        var previousColor = GUI.color;
        GUI.color = unlocked ? new Color(0.08f, 0.085f, 0.085f, 0.94f) : new Color(0.045f, 0.045f, 0.045f, 0.9f);
        GUI.DrawTexture(rect, whitePixel);
        GUI.color = unlocked ? new Color(0.48f, 0.36f, 0.18f, 0.9f) : new Color(0.16f, 0.16f, 0.16f, 0.9f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, rect.height * 0.045f)), whitePixel);
        GUI.color = previousColor;
        GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 14f), label, Label(10, unlocked ? new Color(0.72f, 0.72f, 0.72f) : new Color(0.36f, 0.36f, 0.36f)));
        GUI.Label(new Rect(rect.x + 6f, rect.y + 17f, rect.width - 12f, 18f), value, Label(15, unlocked ? Color.white : new Color(0.42f, 0.42f, 0.42f)));
    }

    private string GetStageSelectTitle(int stageNumber)
    {
        if (stageNumber == 0)
        {
            return "Meteor Test";
        }

        return Mathf.Clamp(stageNumber, 1, MaxStage) switch
        {
            8 => "Stone Golem Line",
            12 => "Dark Knight Watch",
            14 => "Red Dragon Pass",
            16 => "Green Dragon Nest",
            18 => "Golem Hold",
            19 => "Black Dragon Gate",
            20 => "Final Siege",
            _ => "Orc Warfront"
        };
    }

    private string BuildEnemySummary(int stageNumber)
    {
        var roster = UnitStats.EnemyRoster(stageNumber);
        var names = new List<string>();
        for (var i = 0; i < roster.Length; i++)
        {
            if (!names.Contains(roster[i].displayName))
            {
                names.Add(roster[i].displayName);
            }
        }

        return string.Join(", ", names);
    }

    private void DrawPauseMenu()
    {
        DrawDarkOverlay(0.48f);
        var guiScale = UiScale();

        var box = new Rect(Screen.width * 0.5f - 150f * guiScale, Screen.height * 0.5f - 110f * guiScale, 300f * guiScale, 220f * guiScale);
        GUI.Box(box, string.Empty);
        GUI.Label(new Rect(box.x + 20f * guiScale, box.y + 20f * guiScale, box.width - 40f * guiScale, 32f * guiScale), "Paused", CenterLabel(26, Color.white));
        GUI.Label(new Rect(box.x + 20f * guiScale, box.y + 55f * guiScale, box.width - 40f * guiScale, 22f * guiScale), "Stage " + stage, CenterLabel(15, Color.yellow));

        if (GUI.Button(new Rect(box.x + 55f * guiScale, box.y + 88f * guiScale, 190f * guiScale, 30f * guiScale), "Resume"))
        {
            isPaused = false;
        }

        if (GUI.Button(new Rect(box.x + 55f * guiScale, box.y + 126f * guiScale, 190f * guiScale, 30f * guiScale), "Restart"))
        {
            isPaused = false;
            ResetBattle();
        }

        if (GUI.Button(new Rect(box.x + 55f * guiScale, box.y + 164f * guiScale, 190f * guiScale, 30f * guiScale), "Stage Select"))
        {
            isPaused = false;
            ShowStageSelect();
        }
    }

    private void DrawDarkOverlay(float alpha)
    {
        var previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), whitePixel);
        GUI.color = previousColor;
    }

    private void DrawOriginalScreen(Texture2D texture)
    {
        if (texture == null)
        {
            DrawDarkOverlay(0.35f);
            return;
        }

        GUI.DrawTexture(OriginalRect(new Rect(0f, 0f, 800f, 400f)), texture, ScaleMode.StretchToFill);
    }

    private Rect OriginalRect(Rect original)
    {
        var scale = OriginalScale();
        var width = 800f * scale;
        var height = 400f * scale;
        var left = (Screen.width - width) * 0.5f;
        var top = (Screen.height - height) * 0.5f;
        return new Rect(left + original.x * scale, top + original.y * scale, original.width * scale, original.height * scale);
    }

    private Rect BottomOriginalRect(Rect original)
    {
        var scale = OriginalScale();
        var width = 800f * scale;
        var height = 400f * scale;
        var left = (Screen.width - width) * 0.5f;
        var top = Screen.height - height;
        return new Rect(left + original.x * scale, top + original.y * scale, original.width * scale, original.height * scale);
    }

    private float OriginalScale()
    {
        return Mathf.Min(Screen.width / 800f, Screen.height / 400f);
    }

    private float UiScale()
    {
        return Mathf.Clamp(OriginalScale(), 1f, 2.2f);
    }

    private bool DrawOriginalMenuButton(string buttonId, Rect originalRect)
    {
        var style = GetOriginalButtonStyle(buttonId);
        return GUI.Button(OriginalRect(originalRect), GUIContent.none, style);
    }

    private bool DrawOriginalMenuButtonOverlay(string buttonId, Rect originalRect)
    {
        var style = GetOriginalMenuButtonOverlayStyle(buttonId);
        return GUI.Button(OriginalRect(originalRect), GUIContent.none, style);
    }

    private bool DrawOriginalHitArea(Rect originalRect)
    {
        return GUI.Button(OriginalRect(originalRect), GUIContent.none, GUIStyle.none);
    }

    private GUIStyle GetOriginalButtonStyle(string buttonId)
    {
        if (uiButtonStyles.TryGetValue(buttonId, out var style))
        {
            return style;
        }

        style = new GUIStyle(GUI.skin.button)
        {
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
            alignment = TextAnchor.MiddleCenter,
            normal = { background = GetUiTexture(buttonId + "/1_up"), textColor = Color.clear },
            hover = { background = GetUiTexture(buttonId + "/2_over"), textColor = Color.clear },
            active = { background = GetUiTexture(buttonId + "/3_down"), textColor = Color.clear },
            focused = { background = GetUiTexture(buttonId + "/1_up"), textColor = Color.clear }
        };

        uiButtonStyles[buttonId] = style;
        return style;
    }

    private GUIStyle GetOriginalMenuButtonOverlayStyle(string buttonId)
    {
        var styleKey = "menu_overlay_" + buttonId;
        if (uiButtonStyles.TryGetValue(styleKey, out var style))
        {
            return style;
        }

        style = new GUIStyle(GUIStyle.none)
        {
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0),
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.clear },
            hover = { background = GetUiTexture(buttonId + "/2_over"), textColor = Color.clear },
            active = { background = GetUiTexture(buttonId + "/3_down"), textColor = Color.clear },
            focused = { textColor = Color.clear }
        };

        uiButtonStyles[styleKey] = style;
        return style;
    }

    private void DrawMainMenuPopup()
    {
        if (mainMenuPopup == MainMenuPopup.Options)
        {
            DrawOptionsPopup();
            return;
        }

        DrawCreditsPopup();
    }

    private void DrawCreditsPopup()
    {
        DrawDarkOverlay(0.38f);
        var panel = OriginalRect(new Rect(204f, 104f, 392f, 184f));
        DrawGuiRect(panel, new Color(0.035f, 0.04f, 0.04f, 0.96f));
        DrawGuiBorder(panel, new Color(0.74f, 0.52f, 0.22f, 0.95f), 2f);
        DrawGuiRect(new Rect(panel.x + 16f, panel.y + 16f, panel.width - 32f, 2f), new Color(0.74f, 0.52f, 0.22f, 0.7f));

        DrawShadowLabel(new Rect(panel.x + 24f, panel.y + 28f, panel.width - 48f, 30f), "Credits", 24, new Color(1f, 0.78f, 0.28f), TextAnchor.MiddleCenter);
        GUI.Label(new Rect(panel.x + 36f, panel.y + 70f, panel.width - 72f, 54f),
            "Prince of War prototype\nBattle UI and gameplay restoration build\nLocal assets and code-driven menu screen",
            WrappedLabel(13, new Color(0.88f, 0.84f, 0.76f)));

        if (DrawMainMenuButton(new Rect(panel.x + panel.width * 0.5f - 66f, panel.y + 134f, 132f, 34f), "Back", "Return", false))
        {
            mainMenuPopup = MainMenuPopup.None;
        }
    }

    private void DrawOptionsPopup()
    {
        DrawDarkOverlay(0.38f);
        var panel = OriginalRect(new Rect(192f, 82f, 416f, 236f));
        DrawGuiRect(panel, new Color(0.035f, 0.04f, 0.04f, 0.96f));
        DrawGuiBorder(panel, new Color(0.74f, 0.52f, 0.22f, 0.95f), 2f);
        DrawGuiRect(new Rect(panel.x + 16f, panel.y + 16f, panel.width - 32f, 2f), new Color(0.74f, 0.52f, 0.22f, 0.7f));

        DrawShadowLabel(new Rect(panel.x + 24f, panel.y + 28f, panel.width - 48f, 30f), "Options", 24, new Color(1f, 0.78f, 0.28f), TextAnchor.MiddleCenter);
        DrawVolumeControl(new Rect(panel.x + 44f, panel.y + 82f, panel.width - 88f, 34f), "Music", ref musicVolume);
        DrawVolumeControl(new Rect(panel.x + 44f, panel.y + 132f, panel.width - 88f, 34f), "SFX", ref sfxVolume);

        if (DrawMainMenuButton(new Rect(panel.x + panel.width * 0.5f - 66f, panel.y + 184f, 132f, 34f), "Back", "Save", false))
        {
            SaveAudioSettings();
            mainMenuPopup = MainMenuPopup.None;
        }
    }

    private void DrawVolumeControl(Rect rect, string label, ref float value)
    {
        GUI.Label(new Rect(rect.x, rect.y, 86f, 22f), label, Label(15, new Color(0.96f, 0.9f, 0.74f)));
        GUI.Label(new Rect(rect.xMax - 48f, rect.y, 48f, 22f), Mathf.RoundToInt(value * 100f) + "%", Label(13, new Color(0.78f, 0.72f, 0.6f)));
        var sliderRect = new Rect(rect.x + 88f, rect.y + 7f, rect.width - 148f, 18f);
        var nextValue = GUI.HorizontalSlider(sliderRect, value, 0f, 1f);
        if (!Mathf.Approximately(nextValue, value))
        {
            value = Mathf.Clamp01(nextValue);
            ApplyAudioSettings();
            SaveAudioSettings();
        }
    }

    private void DrawMainMenuBackdrop()
    {
        DrawDarkOverlay(0.24f);
        DrawGuiRect(OriginalRect(new Rect(0f, 0f, 800f, 54f)), new Color(0f, 0f, 0f, 0.34f));
        DrawGuiRect(OriginalRect(new Rect(0f, 330f, 800f, 70f)), new Color(0f, 0f, 0f, 0.42f));

        var leftPanel = OriginalRect(new Rect(38f, 38f, 392f, 304f));
        DrawGuiRect(leftPanel, new Color(0.03f, 0.035f, 0.035f, 0.48f));
        DrawGuiRect(new Rect(leftPanel.x, leftPanel.y, 4f, leftPanel.height), new Color(0.64f, 0.18f, 0.13f, 0.86f));
        DrawGuiRect(new Rect(leftPanel.x + 14f, leftPanel.y + 104f, leftPanel.width * 0.84f, 2f), new Color(0.78f, 0.55f, 0.22f, 0.68f));

        var rightPanel = OriginalRect(new Rect(486f, 88f, 260f, 284f));
        DrawGuiRect(rightPanel, new Color(0.025f, 0.03f, 0.032f, 0.62f));
        DrawGuiBorder(rightPanel, new Color(0.72f, 0.52f, 0.24f, 0.56f), 1f);
    }

    private bool DrawMainMenuButton(Rect rect, string title, string caption, bool primary)
    {
        var hovered = rect.Contains(Event.current.mousePosition);
        var pressed = hovered && Mouse.current != null && Mouse.current.leftButton.isPressed;
        var fill = primary
            ? new Color(0.33f, 0.12f, 0.08f, 0.94f)
            : new Color(0.055f, 0.065f, 0.065f, 0.94f);

        if (hovered)
        {
            fill = primary ? new Color(0.47f, 0.18f, 0.1f, 0.98f) : new Color(0.12f, 0.13f, 0.12f, 0.98f);
        }

        if (pressed)
        {
            fill = primary ? new Color(0.22f, 0.075f, 0.045f, 1f) : new Color(0.035f, 0.04f, 0.04f, 1f);
        }

        DrawGuiRect(rect, fill);
        DrawGuiRect(new Rect(rect.x, rect.y, 5f, rect.height), primary ? new Color(0.94f, 0.6f, 0.16f, 0.96f) : new Color(0.55f, 0.42f, 0.2f, 0.88f));
        DrawGuiBorder(rect, hovered ? new Color(1f, 0.72f, 0.28f, 0.95f) : new Color(0.5f, 0.38f, 0.18f, 0.72f), pressed ? 1f : 2f);

        var titleOffset = pressed ? 1f : 0f;
        var titleStyle = Label(primary ? 18 : 16, hovered ? new Color(1f, 0.92f, 0.76f) : Color.white);
        titleStyle.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(rect.x + 18f, rect.y + 4f + titleOffset, rect.width - 32f, rect.height * 0.55f), title, titleStyle);
        GUI.Label(new Rect(rect.x + 18f, rect.y + rect.height * 0.54f + titleOffset, rect.width - 32f, rect.height * 0.34f), caption, Label(10, new Color(0.78f, 0.72f, 0.6f)));

        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    private void DrawMainMenuProgress(Rect rect)
    {
        var progress = Mathf.Clamp01(highestUnlockedStage / (float)MaxStage);
        DrawGuiRect(rect, new Color(0.02f, 0.025f, 0.025f, 0.76f));
        DrawGuiBorder(rect, new Color(0.55f, 0.42f, 0.2f, 0.7f), 1f);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, 14f), "Campaign Progress", Label(10, new Color(0.78f, 0.72f, 0.6f)));
        var bar = new Rect(rect.x + 12f, rect.y + 21f, rect.width - 24f, 5f);
        DrawGuiRect(bar, new Color(0f, 0f, 0f, 0.56f));
        DrawGuiRect(new Rect(bar.x, bar.y, bar.width * progress, bar.height), new Color(0.9f, 0.54f, 0.16f, 0.96f));
        GUI.Label(new Rect(rect.x + rect.width - 76f, rect.y + 4f, 64f, 14f), highestUnlockedStage + "/" + MaxStage, Label(10, new Color(0.95f, 0.9f, 0.78f)));
    }

    private void DrawGuiRect(Rect rect, Color color)
    {
        var previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, whitePixel);
        GUI.color = previousColor;
    }

    private void DrawGuiBorder(Rect rect, Color color, float thickness)
    {
        DrawGuiRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawGuiRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawGuiRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawGuiRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private void DrawShadowLabel(Rect rect, string text, int size, Color color, TextAnchor alignment)
    {
        var shadowStyle = Label(size, new Color(0f, 0f, 0f, 0.72f));
        shadowStyle.alignment = alignment;
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, shadowStyle);

        var textStyle = Label(size, color);
        textStyle.alignment = alignment;
        GUI.Label(rect, text, textStyle);
    }

    private GUIStyle OriginalSmallButtonStyle(bool enabled)
    {
        var style = GetOriginalButtonStyle("DefineButton2_1157");
        var copy = new GUIStyle(style)
        {
            fontSize = Mathf.RoundToInt(18f * UiScale()),
            alignment = TextAnchor.MiddleCenter
        };
        var textColor = enabled ? Color.white : new Color(0.35f, 0.35f, 0.35f);
        copy.normal.textColor = textColor;
        copy.hover.textColor = textColor;
        copy.active.textColor = textColor;
        copy.focused.textColor = textColor;
        return copy;
    }

    private void DrawScrollPanel(Rect originalRect)
    {
        var rect = OriginalRect(originalRect);
        var previousColor = GUI.color;
        GUI.color = new Color(0.44f, 0.37f, 0.3f, 0.92f);
        GUI.DrawTexture(rect, whitePixel);
        GUI.color = new Color(0.08f, 0.18f, 0.08f, 0.85f);
        GUI.DrawTexture(new Rect(rect.x - rect.width * 0.035f, rect.y + rect.height * 0.03f, rect.width * 0.035f, rect.height * 0.94f), whitePixel);
        GUI.DrawTexture(new Rect(rect.xMax, rect.y + rect.height * 0.03f, rect.width * 0.035f, rect.height * 0.94f), whitePixel);
        GUI.color = previousColor;
    }

    private void DrawStatusPanel()
    {
        var previousColor = GUI.color;
        uiPanel = BottomOriginalRect(new Rect(0f, 326f, 800f, 74f));
        GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.96f);
        GUI.DrawTexture(uiPanel, whitePixel);
        GUI.color = new Color(0.28f, 0.28f, 0.28f, 0.9f);
        GUI.DrawTexture(new Rect(uiPanel.x, uiPanel.y, uiPanel.width, Mathf.Max(1f, uiPanel.height * 0.025f)), whitePixel);
        GUI.color = previousColor;

        GUI.Label(OriginalRect(new Rect(8f, 12f, 560f, 20f)), "Escort any allied unit into the enemy gate to win.", Label(16, Color.yellow));
        GUI.Label(BottomOriginalRect(new Rect(60f, 350f, 90f, 13f)), "gold +" + GoldPerSecond + "/s", Label(10, Color.white));
        GUI.Label(BottomOriginalRect(new Rect(60f, 364f, 86f, 24f)), playerGold.ToString(), Label(22, new Color(1f, 0.82f, 0f)));
        GUI.Label(BottomOriginalRect(new Rect(150f, 350f, 230f, 20f)), "Level " + stage + "   Morale " + morale, Label(14, Color.white));
        GUI.Label(BottomOriginalRect(new Rect(150f, 370f, 260f, 18f)), "Enemies " + enemiesKilled + "/" + enemiesToSpawn + "   Reserve " + enemyReserveGold, Label(12, Color.white));
    }

    private void DrawRecruitButtons()
    {
        var roster = UnitStats.AllyRoster();
        for (var i = 0; i < roster.Length; i++)
        {
            DrawBuyButton(336f + i * 50f, 348f, roster[i], (i + 1).ToString());
        }
    }

    private void DrawMeteorTestControls()
    {
        if (stage != 0)
        {
            return;
        }

        var canUse = !gameOver && !isPaused && !forceWizardLightningOnce;
        var previousEnabled = GUI.enabled;
        GUI.enabled = canUse;
        if (GUI.Button(OriginalRect(new Rect(604f, 12f, 160f, 30f)), forceWizardLightningOnce ? "Lightning Ready" : "Force Lightning", OriginalSmallButtonStyle(canUse)))
        {
            forceWizardLightningOnce = true;
            AddFloatingText(new Vector3(0f, HeroMaxY + 0.75f, 0f), "Wizard Lightning Ready", new Color(0.45f, 0.85f, 1f));
        }

        GUI.enabled = previousEnabled;
    }

    private void DrawBuyButton(float x, float y, UnitStats stats, string hotkey)
    {
        var cooldown = GetTrainingCooldown(stats);
        var available = IsUnitAvailableThisStage(stats);
        var canBuy = available && playerGold >= stats.cost && cooldown <= 0f && !gameOver && !isPaused;
        var label = available ? hotkey : "-";
        if (available && cooldown > 0f)
        {
            label = cooldown.ToString("0");
        }

        var buttonRect = BottomOriginalRect(new Rect(x, y, 44f, 44f));
        DrawRecruitSlot(buttonRect, available, canBuy, cooldown > 0f);

        var previousEnabled = GUI.enabled;
        GUI.enabled = canBuy;
        if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
        {
            TryBuy(stats);
        }
        GUI.enabled = previousEnabled;

        DrawRecruitIcon(buttonRect, stats, available, canBuy);

        GUI.Label(new Rect(buttonRect.x + 3f, buttonRect.y + 2f, buttonRect.width - 6f, buttonRect.height * 0.28f), label, Label(10, canBuy ? Color.white : new Color(0.55f, 0.55f, 0.55f)));
        GUI.Label(new Rect(buttonRect.x + 2f, buttonRect.yMax - buttonRect.height * 0.28f, buttonRect.width - 4f, buttonRect.height * 0.25f), available ? stats.cost.ToString() : "LOCK", CenterLabel(10, canBuy ? new Color(1f, 0.82f, 0f) : new Color(0.5f, 0.42f, 0.12f)));
    }

    private void DrawRecruitSlot(Rect rect, bool available, bool canBuy, bool coolingDown)
    {
        var previousColor = GUI.color;
        var hovered = rect.Contains(Event.current.mousePosition);
        GUI.color = !available
            ? new Color(0.025f, 0.025f, 0.028f, 0.9f)
            : canBuy
            ? hovered ? new Color(0.18f, 0.2f, 0.2f, 0.96f) : new Color(0.1f, 0.11f, 0.11f, 0.94f)
            : coolingDown ? new Color(0.08f, 0.08f, 0.09f, 0.9f) : new Color(0.04f, 0.04f, 0.045f, 0.88f);
        GUI.DrawTexture(rect, whitePixel);

        GUI.color = canBuy ? new Color(0.78f, 0.68f, 0.38f, 0.95f) : new Color(0.24f, 0.24f, 0.24f, 0.85f);
        var line = Mathf.Max(1f, rect.width * 0.035f);
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, line), whitePixel);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - line, rect.width, line), whitePixel);
        GUI.DrawTexture(new Rect(rect.x, rect.y, line, rect.height), whitePixel);
        GUI.DrawTexture(new Rect(rect.xMax - line, rect.y, line, rect.height), whitePixel);
        GUI.color = previousColor;
    }

    private void DrawRecruitIcon(Rect buttonRect, UnitStats stats, bool available, bool canBuy)
    {
        if (!recruitIcons.TryGetValue(stats.spriteKey, out var sprite) || sprite == null || sprite.texture == null)
        {
            return;
        }

        var iconRect = FitRectPreserveAspect(sprite.rect, new Rect(buttonRect.x + buttonRect.width * 0.2f, buttonRect.y + buttonRect.height * 0.24f, buttonRect.width * 0.6f, buttonRect.height * 0.48f));
        var texture = sprite.texture;
        var textureRect = sprite.textureRect;
        var texCoords = new Rect(textureRect.x / texture.width, textureRect.y / texture.height, textureRect.width / texture.width, textureRect.height / texture.height);
        var previousColor = GUI.color;
        GUI.color = canBuy ? Color.white : available ? new Color(0.55f, 0.55f, 0.55f, 0.72f) : new Color(0.22f, 0.22f, 0.22f, 0.72f);
        GUI.DrawTextureWithTexCoords(iconRect, texture, texCoords, true);
        GUI.color = previousColor;
    }

    private Rect FitRectPreserveAspect(Rect sourceRect, Rect targetRect)
    {
        if (sourceRect.width <= 0f || sourceRect.height <= 0f)
        {
            return targetRect;
        }

        var sourceAspect = sourceRect.width / sourceRect.height;
        var targetAspect = targetRect.width / targetRect.height;
        if (sourceAspect > targetAspect)
        {
            var height = targetRect.width / sourceAspect;
            return new Rect(targetRect.x, targetRect.y + (targetRect.height - height) * 0.5f, targetRect.width, height);
        }

        var width = targetRect.height * sourceAspect;
        return new Rect(targetRect.x + (targetRect.width - width) * 0.5f, targetRect.y, width, targetRect.height);
    }

    private void TryBuy(UnitStats stats)
    {
        if (!IsUnitAvailableThisStage(stats) || playerGold < stats.cost || gameOver || GetTrainingCooldown(stats) > 0f)
        {
            return;
        }

        playerGold -= stats.cost;
        SpawnUnit(stats, true);
        trainingCooldowns[stats.displayName] = GetScaledTrainingCooldown(stats);
    }

    private bool IsUnitAvailableThisStage(UnitStats stats)
    {
        return IsUnitAvailableOnStage(stage, stats.spriteKey);
    }

    private bool IsUnitAvailableOnStage(int stageNumber, string spriteKey)
    {
        switch (Mathf.Clamp(stageNumber, 1, MaxStage))
        {
            case 9:
            case 14:
            case 15:
            case 19:
                return IsAdvancedRosterUnit(spriteKey);
            case 10:
            case 11:
            case 12:
            case 13:
            case 17:
                return IsAdvancedRosterUnit(spriteKey) || spriteKey == "player_swordsman" || spriteKey == "player_knight";
            case 16:
                return IsAdvancedRosterUnit(spriteKey) || spriteKey == "player_knight";
            default:
                return true;
        }
    }

    private bool IsAdvancedRosterUnit(string spriteKey)
    {
        return spriteKey == "player_axeman"
            || spriteKey == "player_elven_archer"
            || spriteKey == "player_cavalry"
            || spriteKey == "player_healer"
            || spriteKey == "player_wizard"
            || spriteKey == "player_griffin";
    }

    private void SpawnEnemyWave()
    {
        var extraSpawns = stage >= 2 ? 3 : 2;
        for (var i = 0; i < extraSpawns; i++)
        {
            if (!TrySpawnNextEnemy())
            {
                return;
            }
        }
    }

    private bool TrySpawnNextEnemy()
    {
        if (!TryCreateNextInitialEnemyStats(out var stats))
        {
            return false;
        }

        SpawnUnit(stats, false);
        enemiesSpawned++;
        return true;
    }

    private bool TryCreateNextInitialEnemyStats(out UnitStats stats)
    {
        var roster = UnitStats.EnemyOpeningRoster(stage);
        if (enemiesSpawned < enemyInitialSlotsToSpawn)
        {
            stats = roster[enemiesSpawned % roster.Length].Scaled(stage);
            return true;
        }

        stats = default;
        return false;
    }

    private bool HasInitialEnemiesLeftToSpawn()
    {
        return enemiesSpawned < enemyInitialSlotsToSpawn;
    }

    private void QueueEnemyRespawn(UnitStats stats)
    {
        var cost = UnitStats.EnemyRespawnCost(stats.spriteKey);
        if (cost > enemyReserveGold)
        {
            return;
        }

        enemyReserveGold -= cost;
        pendingEnemySpawns.Add(new PendingEnemySpawn
        {
            stats = stats,
            timeLeft = EnemyRespawnDelay
        });
    }

    private void TickPendingEnemyRespawns(float deltaTime)
    {
        for (var i = pendingEnemySpawns.Count - 1; i >= 0; i--)
        {
            var pending = pendingEnemySpawns[i];
            pending.timeLeft -= deltaTime;
            if (pending.timeLeft > 0f)
            {
                pendingEnemySpawns[i] = pending;
                continue;
            }

            SpawnUnit(pending.stats.Scaled(stage), false);
            enemiesSpawned++;
            pendingEnemySpawns.RemoveAt(i);
        }
    }

    private void SpawnUnit(UnitStats stats, bool isAlly)
    {
        var unitObject = new GameObject((isAlly ? "Ally " : "Enemy ") + stats.displayName);
        unitObject.transform.position = new Vector3(GetSpawnX(stats, isAlly), GetSpawnY(stats, isAlly), 0f);
        var unit = unitObject.AddComponent<LaneUnit>();
        var animations = GetCharacterAnimations(stats.spriteKey, squareSprite);
        var tint = animations.UsesFallback ? isAlly ? AllyColor : EnemyColor : Color.white;
        unit.Initialize(this, stats, isAlly ? 1 : -1, animations, squareSprite, tint);
        units.Add(unit);
    }

    private float GetSpawnX(UnitStats stats, bool isAlly)
    {
        if (isAlly)
        {
            return PlayerBaseX + 0.75f;
        }

        if (stage == 0 && stats.spriteKey == "enemy_stone_golem")
        {
            return MeteorTestGolemX;
        }

        return EnemySpawnX;
    }

    private float GetSpawnY(UnitStats stats, bool isAlly)
    {
        if (isAlly || IsEnemyRangedUnit(stats.spriteKey))
        {
            return LaneY;
        }

        if (stats.spriteKey == "enemy_roaming_orc")
        {
            return Random.Range(HeroMinY, HeroMaxY);
        }

        if (IsCenteredBossUnit(stats.spriteKey))
        {
            return GetCenteredBossSpawnY(stats.spriteKey);
        }

        return LaneY + (OriginalEnemyLaneReferenceY - GetOriginalEnemySpawnY(stats.spriteKey)) / OriginalPixelsPerWorldUnit;
    }

    private bool IsEnemyRangedUnit(string spriteKey)
    {
        return spriteKey == "enemy_dark_ranger"
            || spriteKey == "enemy_shaman"
            || spriteKey == "enemy_shadow_mage";
    }

    private bool IsCenteredBossUnit(string spriteKey)
    {
        return spriteKey == "enemy_red_dragon"
            || spriteKey == "enemy_green_dragon"
            || spriteKey == "enemy_black_dragon"
            || spriteKey == "enemy_stone_golem"
            || spriteKey == "enemy_fire_golem";
    }

    private float GetCenteredBossSpawnY(string spriteKey)
    {
        return spriteKey switch
        {
            "enemy_stone_golem" => LaneY - 0.15f,
            "enemy_fire_golem" => LaneY - 0.25f,
            _ => LaneY
        };
    }

    private float GetOriginalEnemySpawnY(string spriteKey)
    {
        return spriteKey switch
        {
            "enemy_red_dragon" => 184.7f,
            "enemy_green_dragon" => 188.25f,
            "enemy_black_dragon" => 188.25f,
            "enemy_dark_ranger" => 205.35f,
            "enemy_shaman" => 207f,
            "enemy_shadow_mage" => 211.25f,
            "enemy_stone_golem" => 211.4f,
            "enemy_fire_golem" => 211.4f,
            "enemy_orc_war_rider" => 228.15f,
            "enemy_dark_knight" => 227.8f,
            _ => 225.2f
        };
    }

    private void SpawnHero()
    {
        if (hero != null)
        {
            Destroy(hero.gameObject);
        }

        var heroObject = new GameObject("Player King");
        heroObject.transform.position = new Vector3(PlayerBaseX + 1.4f, LaneY + 0.25f, 0f);
        hero = heroObject.AddComponent<HeroUnit>();
        hero.Initialize(this, GetCharacterAnimations("player_king", squareSprite), squareSprite);
    }

    private void TickUnits()
    {
        for (var i = units.Count - 1; i >= 0; i--)
        {
            var unit = units[i];
            if (unit == null || unit.IsDead)
            {
                units.RemoveAt(i);
                continue;
            }

            unit.ManualUpdate(Time.deltaTime);
        }
    }

    private void TickBossMeteors(float deltaTime)
    {
        if (!BossMeteorEnabled)
        {
            pendingMeteors.Clear();
            meteorCooldownTimer = 0f;
            meteorClusterWarningTimer = 0f;
            return;
        }

        meteorCooldownTimer = Mathf.Max(0f, meteorCooldownTimer - deltaTime);
        meteorClusterWarningTimer = Mathf.Max(0f, meteorClusterWarningTimer - deltaTime);

        for (var i = pendingMeteors.Count - 1; i >= 0; i--)
        {
            var meteor = pendingMeteors[i];
            meteor.timeLeft -= deltaTime;
            if (meteor.timeLeft > 0f)
            {
                pendingMeteors[i] = meteor;
                continue;
            }

            ResolveMeteorStrike(meteor.position);
            pendingMeteors.RemoveAt(i);
        }

        if (pendingMeteors.Count > 0 || !HasActiveBossEnemy())
        {
            return;
        }

        var clusterCount = FindRangedCluster(out var clusterCenter);
        if (clusterCount >= MeteorRangedClusterCount && meteorCooldownTimer <= 0f)
        {
            pendingMeteors.Add(new PendingMeteor
            {
                position = clusterCenter,
                timeLeft = MeteorWarningDelay
            });
            SpawnMeteorEffect(clusterCenter);
            meteorCooldownTimer = MeteorCooldown;
        }
        else if (clusterCount >= MeteorWarningClusterCount && meteorClusterWarningTimer <= 0f)
        {
            AddFloatingText(clusterCenter + Vector3.up * 0.9f, "Spread out", new Color(1f, 0.74f, 0.18f));
            meteorClusterWarningTimer = MeteorClusterWarningCooldown;
        }
    }

    private bool HasActiveBossEnemy()
    {
        foreach (var unit in units)
        {
            if (unit != null && unit.IsCombatActive && unit.IsEnemyBoss)
            {
                return true;
            }
        }

        return false;
    }

    private int FindRangedCluster(out Vector3 center)
    {
        center = default;
        var bestCount = 0;
        var bestSum = Vector3.zero;

        foreach (var anchor in units)
        {
            if (anchor == null || !anchor.IsCombatActive || !anchor.IsAlliedMeteorTarget)
            {
                continue;
            }

            var count = 0;
            var sum = Vector3.zero;
            foreach (var candidate in units)
            {
                if (candidate == null || !candidate.IsCombatActive || !candidate.IsAlliedMeteorTarget)
                {
                    continue;
                }

                if (Vector2.Distance(candidate.transform.position, anchor.transform.position) > MeteorClusterRadius)
                {
                    continue;
                }

                count++;
                sum += candidate.transform.position;
            }

            if (count > bestCount)
            {
                bestCount = count;
                bestSum = sum;
            }
        }

        if (bestCount <= 0)
        {
            return 0;
        }

        center = bestSum / bestCount;
        return bestCount;
    }

    private void ResolveMeteorStrike(Vector3 position)
    {
        var hitCount = 0;
        foreach (var unit in units)
        {
            if (unit == null || !unit.IsCombatActive || !unit.IsAlliedMeteorTarget)
            {
                continue;
            }

            if (Vector2.Distance(unit.transform.position, position) > MeteorStrikeRadius)
            {
                continue;
            }

            unit.KillByMeteor();
            hitCount++;
        }

        if (hitCount == 0)
        {
            AddFloatingText(position + Vector3.up * 0.9f, "Miss", Color.white);
        }
    }

    private void SpawnMeteorEffect(Vector3 position)
    {
        var effectObject = new GameObject("Meteor Warning");
        effectObject.transform.position = position;
        var effect = effectObject.AddComponent<MeteorEffect>();
        effect.Initialize(meteorCoreSprite, meteorGlowSprite, meteorRingSprite, MeteorWarningDelay, MeteorStrikeRadius);
    }

    private void SpawnRoamingOrcs()
    {
        var count = UnitStats.RoamingOrcCount(stage);
        for (var i = 0; i < count; i++)
        {
            var unitObject = new GameObject("Enemy Roaming Orc");
            unitObject.transform.position = GetRoamingOrcSpawnPosition();
            var roamer = unitObject.AddComponent<RoamingOrcUnit>();
            roamer.Initialize(this, GetCharacterAnimations("enemy_roaming_orc", squareSprite), squareSprite, RoamingOrcMoveSpeed);
            roamingOrcs.Add(roamer);
        }
    }

    private void TickRoamingOrcs(float deltaTime)
    {
        for (var i = roamingOrcs.Count - 1; i >= 0; i--)
        {
            var roamer = roamingOrcs[i];
            if (roamer == null)
            {
                roamingOrcs.RemoveAt(i);
                continue;
            }

            roamer.ManualUpdate(deltaTime);
        }
    }

    public Vector3 GetRoamingOrcSpawnPosition()
    {
        return new Vector3(EnemySpawnX, Random.Range(HeroMinY, HeroMaxY), 0f);
    }

    private void TickFloatingTexts()
    {
        for (var i = floatingTexts.Count - 1; i >= 0; i--)
        {
            var entry = floatingTexts[i];
            entry.timeLeft -= Time.deltaTime;
            entry.position += Vector3.up * Time.deltaTime * 0.7f;
            floatingTexts[i] = entry;

            if (entry.timeLeft <= 0f)
            {
                floatingTexts.RemoveAt(i);
            }
        }
    }

    private void TickTrainingCooldowns(float deltaTime)
    {
        var keys = new List<string>(trainingCooldowns.Keys);
        var roster = UnitStats.AllyRoster();
        foreach (var key in keys)
        {
            var cooldown = Mathf.Max(0f, trainingCooldowns[key] - deltaTime);
            if (TryFindAllyStatsByDisplayName(roster, key, out var stats))
            {
                cooldown = Mathf.Min(cooldown, GetScaledTrainingCooldown(stats));
            }

            trainingCooldowns[key] = cooldown;
        }
    }

    private bool TryFindAllyStatsByDisplayName(UnitStats[] roster, string displayName, out UnitStats stats)
    {
        for (var i = 0; i < roster.Length; i++)
        {
            if (roster[i].displayName == displayName)
            {
                stats = roster[i];
                return true;
            }
        }

        stats = default;
        return false;
    }

    private void TickPassiveGoldIncome(float deltaTime)
    {
        passiveGoldTimer += deltaTime;
        if (passiveGoldTimer < 1f)
        {
            return;
        }

        var seconds = Mathf.FloorToInt(passiveGoldTimer);
        passiveGoldTimer -= seconds;
        playerGold += seconds * GoldPerSecond;
    }

    private float GetTrainingCooldown(UnitStats stats)
    {
        return trainingCooldowns.TryGetValue(stats.displayName, out var cooldown) ? cooldown : 0f;
    }

    private float GetScaledTrainingCooldown(UnitStats stats)
    {
        var perExtraUnit = stats.spriteKey switch
        {
            "player_archer" => ArcherCooldownPerExtraUnit,
            "player_healer" => HealerCooldownPerExtraUnit,
            _ => 0f
        };

        if (perExtraUnit <= 0f)
        {
            return stats.trainCooldown;
        }

        var activeCount = CountActiveAlliedUnits(stats.spriteKey);
        return stats.trainCooldown + Mathf.Max(0, activeCount - 1) * perExtraUnit;
    }

    private int CountActiveAlliedUnits(string spriteKey)
    {
        var count = 0;
        foreach (var unit in units)
        {
            if (unit != null && unit.IsCombatActive && unit.Direction > 0 && unit.SpriteKey == spriteKey)
            {
                count++;
            }
        }

        return count;
    }

    private void CheckMissionState()
    {
        if (morale <= 0)
        {
            EndGame("Defeat");
            return;
        }
    }

    private void CompleteStage()
    {
        if (gameOver)
        {
            return;
        }

        if (stage == 0)
        {
            stageCleared = true;
            EndGame("Meteor Test Complete");
            return;
        }

        stageCleared = true;
        highestUnlockedStage = Mathf.Min(MaxStage, Mathf.Max(highestUnlockedStage, stage + 1));
        PlayerPrefs.SetInt("PrinceOfWar.HighestUnlockedStage", highestUnlockedStage);
        PlayerPrefs.Save();
        EndGame(stage >= MaxStage ? "Campaign Cleared" : "Stage " + stage + " Cleared");
    }

    private void EndGame(string text)
    {
        gameOver = true;
        resultText = text;
    }

    private void ShowMainMenu()
    {
        screen = GameScreen.MainMenu;
        mainMenuPopup = MainMenuPopup.None;
        ClearBattleObjects();
        floatingTexts.Clear();
        trainingCooldowns.Clear();
        gameOver = false;
        stageCleared = false;
        isPaused = false;
        resultText = string.Empty;
        BuildWorld();
    }

    private void ShowStageSelect()
    {
        screen = GameScreen.StageSelect;
        mainMenuPopup = MainMenuPopup.None;
        ClearBattleObjects();
        floatingTexts.Clear();
        trainingCooldowns.Clear();
        gameOver = false;
        stageCleared = false;
        isPaused = false;
        resultText = string.Empty;
        BuildWorld();
    }

    private void StartBattle(int selectedStage)
    {
        screen = GameScreen.Battle;
        mainMenuPopup = MainMenuPopup.None;
        ConfigureStage(selectedStage);
        ClearBattleObjects();
        floatingTexts.Clear();
        trainingCooldowns.Clear();
        gameOver = false;
        isPaused = false;
        resultText = string.Empty;
        BuildWorld();
        SpawnHero();
        SpawnRoamingOrcs();
        AddFloatingText(new Vector3(0f, HeroMaxY + 0.4f, 0f), "Stage " + stage, Color.yellow);
    }

    private void ConfigureStage(int selectedStage)
    {
        stage = Mathf.Clamp(selectedStage, 0, MaxStage);
        playerGold = UnitStats.StartingGold(stage);
        morale = 6;
        enemyInitialSlotsToSpawn = UnitStats.EnemyOpeningRoster(stage).Length;
        enemyReserveGold = UnitStats.EnemyReinforcementRoster(stage).Length > 0 ? UnitStats.EnemyGoldBudget(stage) : 0;
        enemiesToSpawn = enemyInitialSlotsToSpawn + UnitStats.EstimatedEnemyReinforcementCount(stage);
        enemiesSpawned = 0;
        enemiesKilled = 0;
        soldiersEscorted = 0;
        pendingEnemySpawns.Clear();
        enemySpawnTimer = stage == 0 ? 999f : 0f;
        passiveGoldTimer = 0f;
        forceWizardLightningOnce = false;
        stageCleared = false;
    }

    private void ClearBattleObjects()
    {
        for (var i = units.Count - 1; i >= 0; i--)
        {
            if (units[i] != null)
            {
                Destroy(units[i].gameObject);
            }
        }

        units.Clear();
        pendingMeteors.Clear();
        meteorCooldownTimer = 0f;
        meteorClusterWarningTimer = 0f;
        foreach (var effect in FindObjectsByType<MeteorEffect>(FindObjectsSortMode.None))
        {
            Destroy(effect.gameObject);
        }

        foreach (var effect in FindObjectsByType<LightningEffect>(FindObjectsSortMode.None))
        {
            Destroy(effect.gameObject);
        }

        for (var i = roamingOrcs.Count - 1; i >= 0; i--)
        {
            if (roamingOrcs[i] != null)
            {
                Destroy(roamingOrcs[i].gameObject);
            }
        }

        roamingOrcs.Clear();
        pendingEnemySpawns.Clear();

        if (hero != null)
        {
            Destroy(hero.gameObject);
            hero = null;
        }
    }

    private void ResetBattle()
    {
        StartBattle(stage);
    }

    private void StartNextStage()
    {
        if (stage >= MaxStage)
        {
            ShowStageSelect();
            return;
        }

        StartBattle(stage + 1);
    }

    public LaneUnit FindTarget(LaneUnit seeker)
    {
        LaneUnit closest = null;
        var bestDistance = float.MaxValue;

        foreach (var candidate in units)
        {
            if (candidate == null || !candidate.IsCombatActive || candidate.Direction == seeker.Direction)
            {
                continue;
            }

            var distance = Mathf.Abs(candidate.transform.position.x - seeker.transform.position.x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    public LaneUnit FindNearestEnemy(Vector3 position, float maxDistance)
    {
        LaneUnit closest = null;
        var bestDistance = maxDistance;

        foreach (var candidate in units)
        {
            if (candidate == null || !candidate.IsCombatActive || candidate.Direction > 0)
            {
                continue;
            }

            var distance = Vector2.Distance(candidate.transform.position, position);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    public LaneUnit FindWoundedAlly(LaneUnit healer, float maxDistance)
    {
        LaneUnit mostWounded = null;
        var highestMissingHp = 0f;

        foreach (var candidate in units)
        {
            if (candidate == null || !candidate.IsCombatActive || candidate.Direction != healer.Direction || !candidate.IsWounded)
            {
                continue;
            }

            if (Vector2.Distance(candidate.transform.position, healer.transform.position) > maxDistance)
            {
                continue;
            }

            if (candidate.MissingHp > highestMissingHp)
            {
                highestMissingHp = candidate.MissingHp;
                mostWounded = candidate;
            }
        }

        return mostWounded;
    }

    public int KnockBackAlliedUnitsInRange(Vector3 center, float maxDistance, float distance, float lockDuration)
    {
        var hitCount = 0;

        foreach (var candidate in units)
        {
            if (candidate == null || !candidate.IsCombatActive || candidate.Direction <= 0)
            {
                continue;
            }

            if (Vector2.Distance(candidate.transform.position, center) > maxDistance)
            {
                continue;
            }

            candidate.ReceiveKnockback(distance, lockDuration);
            hitCount++;
        }

        return hitCount;
    }

    public int DamageAlliedUnitsInRange(Vector3 center, float maxDistance, float damage, int attackerDirection)
    {
        var hitCount = 0;

        foreach (var candidate in units)
        {
            if (candidate == null || !candidate.IsCombatActive || candidate.Direction <= 0)
            {
                continue;
            }

            if (Vector2.Distance(candidate.transform.position, center) > maxDistance)
            {
                continue;
            }

            candidate.TakeDamage(damage, attackerDirection);
            hitCount++;
        }

        return hitCount;
    }

    public int DamageEnemyUnitsInRange(Vector3 center, float maxDistance, float damage, int attackerDirection, LaneUnit excluded)
    {
        var hitCount = 0;

        foreach (var candidate in units)
        {
            if (candidate == null || candidate == excluded || !candidate.IsCombatActive || candidate.Direction >= 0)
            {
                continue;
            }

            if (Vector2.Distance(candidate.transform.position, center) > maxDistance)
            {
                continue;
            }

            candidate.TakeDamage(damage, attackerDirection);
            hitCount++;
        }

        return hitCount;
    }

    public bool TryFindEnemyBacklineOrNearestPosition(Vector3 frontPosition, float minBacklineOffset, out Vector3 position)
    {
        position = default;
        LaneUnit backline = null;
        var bestX = float.MinValue;
        LaneUnit nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var candidate in units)
        {
            if (candidate == null || !candidate.IsCombatActive || candidate.Direction >= 0)
            {
                continue;
            }

            var distance = Vector2.Distance(candidate.transform.position, frontPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }

            if (candidate.transform.position.x < frontPosition.x + minBacklineOffset)
            {
                continue;
            }

            if (candidate.transform.position.x > bestX)
            {
                bestX = candidate.transform.position.x;
                backline = candidate;
            }
        }

        if (backline == null)
        {
            if (nearest == null)
            {
                return false;
            }

            position = nearest.transform.position;
            return true;
        }

        position = backline.transform.position;
        return true;
    }

    public void SpawnLightningEffect(Vector3 position)
    {
        var effectObject = new GameObject("Wizard Lightning");
        effectObject.transform.position = position;
        var effect = effectObject.AddComponent<LightningEffect>();
        effect.Initialize(lightningBoltSprite, lightningGlowSprite);
    }

    public bool IsForcedWizardLightningReady()
    {
        return stage == 0 && forceWizardLightningOnce;
    }

    public void ConsumeForcedWizardLightning()
    {
        forceWizardLightningOnce = false;
    }

    public int DamageEnemiesInRange(Vector3 position, float maxDistance, float damage)
    {
        var hitCount = 0;
        foreach (var candidate in units)
        {
            if (candidate == null || !candidate.IsCombatActive || candidate.Direction > 0)
            {
                continue;
            }

            if (Vector2.Distance(candidate.transform.position, position) > maxDistance)
            {
                continue;
            }

            candidate.TakeDamage(damage, 1);
            hitCount++;
        }

        for (var i = 0; i < roamingOrcs.Count; i++)
        {
            var roamer = roamingOrcs[i];
            if (roamer != null && roamer.TryKillFromHero(position, maxDistance))
            {
                hitCount++;
            }
        }

        return hitCount;
    }

    public bool IsHeroInRange(Vector3 attackerPosition, float range)
    {
        return hero != null && hero.IsAlive && Vector2.Distance(hero.transform.position, attackerPosition) <= range;
    }

    public void DamageHero(float damage)
    {
        if (hero != null)
        {
            hero.TakeDamage(damage);
        }
    }

    public void PlayAttackSound(string spriteKey)
    {
        PlaySound(GetAttackSoundId(spriteKey), GetAttackSoundVolume(spriteKey));
        PlaySound(GetAttackRoarSoundId(spriteKey), GetAttackRoarSoundVolume(spriteKey));
    }

    public void PlayDeathSound(string spriteKey)
    {
        PlaySound(GetDeathSoundId(spriteKey), 0.82f);
    }

    public bool HasReachedBattlefieldEdge(LaneUnit unit)
    {
        return unit.Direction > 0
            ? unit.transform.position.x >= EnemyBaseX - 0.18f
            : unit.transform.position.x <= PlayerBaseX + 0.4f;
    }

    public bool IsInsideBattlefieldEntry(LaneUnit unit)
    {
        return unit.Direction > 0
            ? unit.transform.position.x >= PlayerBaseX + 0.75f
            : unit.transform.position.x <= EnemyBaseX - 0.75f;
    }

    public bool ShouldHoldForOriginalRangedAttack(LaneUnit unit, string spriteKey)
    {
        return unit.Direction < 0
            && spriteKey == "enemy_dark_ranger"
            && unit.transform.position.x <= EnemyRangedHoldX;
    }

    public void ResolveBattlefieldEdge(LaneUnit unit)
    {
        if (unit.Direction > 0)
        {
            soldiersEscorted++;
            playerGold += 8;
            AddFloatingText(unit.transform.position + Vector3.up * 0.9f, "Gate captured", Color.green);
            unit.MarkDead();
            CompleteStage();
            return;
        }
        else
        {
            morale--;
            enemiesKilled++;
            AddFloatingText(new Vector3(PlayerBaseX + 0.8f, LaneY + 1.1f, 0f), "Breach", Color.red);
        }

        unit.MarkDead();
    }

    public void RewardRoamingOrcKill(Vector3 position)
    {
        enemiesKilled++;
        playerGold += 20;
        AddFloatingText(position + Vector3.up * 0.7f, "+20", Color.yellow);
    }

    public void ResolveRoamingOrcBreach(Vector3 position)
    {
        AddFloatingText(new Vector3(PlayerBaseX + 0.8f, position.y + 0.7f, 0f), "Breach", Color.red);
        EndGame("Defeat");
    }

    public bool HasRoamingOrcBreached(Vector3 position)
    {
        return position.x <= PlayerBaseX - 0.45f;
    }

    public void RewardForKill(bool killedEnemy, UnitStats killedStats)
    {
        if (killedEnemy)
        {
            enemiesKilled++;
            playerGold += killedStats.killReward;
            QueueEnemyRespawn(killedStats);
        }
    }

    public bool TrySpendGold(int amount)
    {
        if (playerGold < amount)
        {
            return false;
        }

        playerGold -= amount;
        return true;
    }

    public float ClampHeroX(float x)
    {
        return Mathf.Clamp(x, PlayerBaseX + 0.95f, EnemyBaseX - 0.45f);
    }

    public float ClampLaneUnitX(float x)
    {
        return Mathf.Clamp(x, PlayerBaseX + 0.75f, EnemyBaseX - 0.18f);
    }

    public float ClampHeroY(float y)
    {
        return Mathf.Clamp(y, HeroMinY, HeroMaxY);
    }

    private string GetHeroStatusText()
    {
        if (hero == null)
        {
            return "-";
        }

        return hero.IsAlive ? Mathf.CeilToInt(hero.Hp).ToString() : "Respawning";
    }

    public void AddFloatingText(Vector3 position, string text, Color color)
    {
        floatingTexts.Add(new FloatingText
        {
            position = position,
            text = text,
            color = color,
            timeLeft = 1.1f
        });
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.45f, 0.34f, 0.18f);
        Gizmos.DrawCube(new Vector3(0f, LaneY - 0.55f, 0f), new Vector3(14f, 0.35f, 0.1f));

        Gizmos.color = AllyColor;
        Gizmos.DrawCube(new Vector3(PlayerBaseX, LaneY + 0.65f, 0f), new Vector3(1.1f, 2.4f, 0.1f));
        Gizmos.color = EnemyColor;
        Gizmos.DrawCube(new Vector3(EnemyBaseX, LaneY + 0.65f, 0f), new Vector3(1.1f, 2.4f, 0.1f));
    }

    private void BuildWorld()
    {
        foreach (var old in FindObjectsByType<WorldPiece>(FindObjectsSortMode.None))
        {
            Destroy(old.gameObject);
        }

        CreateOriginalBackground();
        CreateWorldPiece("Western Gate", new Vector3(PlayerBaseX, LaneY + 0.65f, 1.1f), new Vector3(0.18f, 2.6f, 1f), AllyColor * 0.82f);
        CreateWorldPiece("Enemy Entry", new Vector3(EnemyBaseX, LaneY + 0.65f, 1.1f), new Vector3(0.18f, 2.6f, 1f), EnemyColor * 0.82f);
    }

    private void CreateOriginalBackground()
    {
        var piece = new GameObject("Original Battlefield");
        piece.transform.position = new Vector3(0f, 0f, 2f);
        piece.AddComponent<WorldPiece>();

        var renderer = piece.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSpriteFromResource("OriginalPrince/Backgrounds/battlefield_clean", new Vector2(0.5f, 0.5f), 96f)
            ?? LoadSpriteFromResource("OriginalPrince/Backgrounds/battlefield", new Vector2(0.5f, 0.5f), 96f);
        renderer.color = Color.white;
        renderer.sortingOrder = -30;

        var spriteSize = renderer.sprite.bounds.size;
        var targetWidth = 14.8f;
        var targetHeight = 7.3f;
        var scale = Mathf.Max(targetWidth / spriteSize.x, targetHeight / spriteSize.y);
        piece.transform.localScale = Vector3.one * scale;
        piece.transform.position = new Vector3(0f, -0.3f, 2f);
    }

    private void CreateWorldPiece(string pieceName, Vector3 position, Vector3 scale, Color color)
    {
        var piece = new GameObject(pieceName);
        piece.transform.position = position;
        piece.transform.localScale = scale;
        piece.AddComponent<WorldPiece>();
        var renderer = piece.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = color;
        renderer.sortingOrder = -20 + Mathf.RoundToInt(-position.z);
    }

    private void SetupCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 4.05f;
        mainCamera.transform.position = new Vector3(0f, -1.15f, -10f);
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.08f, 0.12f, 0.18f);
    }

    private Sprite CreateSquareSprite()
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
    }

    private void CreateMeteorSprites()
    {
        meteorCoreSprite = CreateMeteorCoreSprite();
        meteorGlowSprite = CreateRadialSprite(96, new Color(1f, 0.5f, 0.08f, 0.7f), new Color(0.45f, 0.04f, 0f, 0f));
        meteorRingSprite = CreateRingSprite(128, new Color(1f, 0.44f, 0.08f, 0.78f));
        lightningBoltSprite = CreateLightningBoltSprite();
        lightningGlowSprite = CreateRadialSprite(96, new Color(0.45f, 0.9f, 1f, 0.72f), new Color(0.08f, 0.2f, 0.6f, 0f));
    }

    private Sprite CreateLightningBoltSprite()
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        var points = new[]
        {
            new Vector2(52f, 94f),
            new Vector2(38f, 68f),
            new Vector2(50f, 68f),
            new Vector2(34f, 32f),
            new Vector2(48f, 35f),
            new Vector2(40f, 2f),
            new Vector2(66f, 43f),
            new Vector2(53f, 40f),
            new Vector2(66f, 74f),
            new Vector2(54f, 73f)
        };

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var inside = IsPointInPolygon(new Vector2(x, y), points);
                var color = inside ? new Color(0.8f, 0.96f, 1f, 1f) : Color.clear;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.02f), 64f);
    }

    private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y)
                && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private Sprite CreateMeteorCoreSprite()
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        var center = new Vector2(size * 0.52f, size * 0.46f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var point = new Vector2(x, y);
                var coreDistance = Vector2.Distance(point, center) / (size * 0.3f);
                var tailDistance = Mathf.Abs((x - y * 0.34f) - size * 0.22f) / (size * 0.13f);
                var tailLength = Mathf.InverseLerp(size * 0.92f, size * 0.18f, y);
                var alpha = Mathf.Clamp01((1f - coreDistance) * 1.25f);
                alpha = Mathf.Max(alpha, Mathf.Clamp01((1f - tailDistance) * tailLength * 0.82f));

                var heat = Mathf.Clamp01(1f - coreDistance * 0.7f);
                var color = Color.Lerp(new Color(0.72f, 0.06f, 0.02f, 1f), new Color(1f, 0.88f, 0.26f, 1f), heat);
                color.a = alpha;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 64f);
    }

    private Sprite CreateRadialSprite(int size, Color inner, Color outer)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        var center = Vector2.one * ((size - 1) * 0.5f);
        var radius = size * 0.5f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                var color = Color.Lerp(inner, outer, Mathf.Clamp01(distance));
                color.a *= Mathf.Clamp01(1f - distance);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 64f);
    }

    private Sprite CreateRingSprite(int size, Color color)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        var center = Vector2.one * ((size - 1) * 0.5f);
        var radius = size * 0.42f;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), center);
                var ring = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) / 4.8f);
                var fill = Mathf.Clamp01(1f - distance / radius) * 0.12f;
                var pixel = color;
                pixel.a *= Mathf.Max(ring, fill);
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 64f);
    }

    private void LoadCharacterSprites()
    {
        characterAnimations.Clear();
        recruitIcons.Clear();
        LoadExtractedCharacterAnimation("player_king");
        LoadExtractedCharacterAnimation("player_swordsman");
        LoadExtractedCharacterAnimation("player_spearman");
        LoadExtractedCharacterAnimation("player_archer");
        LoadExtractedCharacterAnimation("player_knight");
        LoadExtractedCharacterAnimation("player_axeman");
        LoadExtractedCharacterAnimation("player_elven_archer");
        LoadExtractedCharacterAnimation("player_cavalry");
        LoadExtractedCharacterAnimation("player_healer");
        LoadExtractedCharacterAnimation("player_wizard");
        LoadExtractedCharacterAnimation("player_griffin");
        LoadExtractedCharacterAnimation("enemy_orc_raider");
        LoadExtractedCharacterAnimation("enemy_roaming_orc");
        LoadExtractedCharacterAnimation("enemy_orc_war_rider");
        LoadExtractedCharacterAnimation("enemy_dark_ranger");
        LoadExtractedCharacterAnimation("enemy_shadow_mage");
        LoadExtractedCharacterAnimation("enemy_shaman");
        LoadExtractedCharacterAnimation("enemy_dark_knight");
        LoadExtractedCharacterAnimation("enemy_stone_golem");
        LoadExtractedCharacterAnimation("enemy_fire_golem");
        LoadExtractedCharacterAnimation("enemy_red_dragon");
        LoadExtractedCharacterAnimation("enemy_green_dragon");
        LoadExtractedCharacterAnimation("enemy_black_dragon");
    }

    private void LoadOriginalUi()
    {
        uiTextures.Clear();
        uiButtonStyles.Clear();
        stageIntroTexture = GetUiTexture("stage_intro");
    }

    private Texture2D GetUiTexture(string resourceName)
    {
        if (uiTextures.TryGetValue(resourceName, out var cached))
        {
            return cached;
        }

        var texture = Resources.Load<Texture2D>("OriginalPrince/UI/" + resourceName);
        if (texture != null)
        {
            texture.filterMode = FilterMode.Bilinear;
        }

        uiTextures[resourceName] = texture;
        return texture;
    }

    private void LoadOriginalSounds()
    {
        soundClips.Clear();
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = sfxVolume;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;

        LoadSound(733);
        LoadSound(760);
        LoadSound(785);
        LoadSound(806);
        LoadSound(1121);
        LoadSound(1231);
        LoadSound(1276);
        LoadSound(1325);
        LoadSound(1339);
        LoadSound(1487);
        LoadSound(1582);
        LoadSound(1643);
        LoadSound(1829);
        LoadSound(1866);
        LoadSound(1868);
        LoadSound(1917);
        LoadSound(1989);
    }

    private void LoadAudioSettings()
    {
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefsKey, musicVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefsKey, sfxVolume));
    }

    private void ApplyAudioSettings()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }

        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
    }

    private void SaveAudioSettings()
    {
        PlayerPrefs.SetFloat(MusicVolumePrefsKey, musicVolume);
        PlayerPrefs.SetFloat(SfxVolumePrefsKey, sfxVolume);
        PlayerPrefs.Save();
    }

    private void LoadSound(int soundId)
    {
        var clip = Resources.Load<AudioClip>("OriginalPrince/Sounds/sound_" + soundId);
        if (clip != null)
        {
            soundClips[soundId] = clip;
        }
    }

    private void StartBackgroundMusic()
    {
        if (musicSource == null || !soundClips.TryGetValue(1276, out var musicClip))
        {
            return;
        }

        musicSource.clip = musicClip;
        musicSource.Play();
    }

    private void PlaySound(int soundId, float volumeScale)
    {
        if (soundId == 0 || sfxSource == null || !soundClips.TryGetValue(soundId, out var clip))
        {
            return;
        }

        sfxSource.PlayOneShot(clip, volumeScale);
    }

    private int GetAttackSoundId(string spriteKey)
    {
        return spriteKey switch
        {
            "player_king" => 733,
            "player_swordsman" => 1231,
            "player_spearman" => 1231,
            "player_knight" => 785,
            "player_archer" => 1325,
            "player_axeman" => 1121,
            "player_elven_archer" => 733,
            "player_cavalry" => 1231,
            "player_healer" => 1917,
            "player_wizard" => 1917,
            "player_griffin" => 1989,
            "enemy_orc_raider" => 1121,
            "enemy_orc_war_rider" => 1121,
            "enemy_dark_ranger" => 1325,
            "enemy_shadow_mage" => 1487,
            "enemy_shaman" => 1582,
            "enemy_dark_knight" => 733,
            "enemy_stone_golem" => 1643,
            "enemy_fire_golem" => 1643,
            "enemy_red_dragon" => 1866,
            "enemy_green_dragon" => 1866,
            "enemy_black_dragon" => 1866,
            _ => 0
        };
    }

    private float GetAttackSoundVolume(string spriteKey)
    {
        return spriteKey switch
        {
            "enemy_red_dragon" => 1.18f,
            "enemy_green_dragon" => 1.18f,
            "enemy_black_dragon" => 1.18f,
            _ => 0.72f
        };
    }

    private int GetAttackRoarSoundId(string spriteKey)
    {
        return spriteKey switch
        {
            "enemy_red_dragon" => 1989,
            "enemy_green_dragon" => 1989,
            "enemy_black_dragon" => 1989,
            _ => 0
        };
    }

    private float GetAttackRoarSoundVolume(string spriteKey)
    {
        return spriteKey switch
        {
            "enemy_red_dragon" => 0.7f,
            "enemy_green_dragon" => 0.7f,
            "enemy_black_dragon" => 0.7f,
            _ => 0f
        };
    }

    private int GetDeathSoundId(string spriteKey)
    {
        return spriteKey switch
        {
            "player_archer" => 1339,
            "player_healer" => 1829,
            "enemy_orc_war_rider" => 806,
            "enemy_orc_raider" => 760,
            "enemy_dark_ranger" => 760,
            "enemy_shadow_mage" => 760,
            "enemy_shaman" => 806,
            "enemy_dark_knight" => 760,
            "enemy_stone_golem" => 1643,
            "enemy_fire_golem" => 1643,
            "enemy_red_dragon" => 1868,
            "enemy_green_dragon" => 1868,
            "enemy_black_dragon" => 1868,
            _ => 760
        };
    }

    private void LoadExtractedCharacterAnimation(string spriteKey)
    {
        var idle = LoadExtractedFrames(spriteKey, "idle");
        var move = LoadExtractedFrames(spriteKey, "move");
        var attack = LoadExtractedFrames(spriteKey, "attack");
        var death = LoadExtractedFrames(spriteKey, "death");
        var defaultFrames = FirstAvailable(idle, move, attack, death);

        if (defaultFrames.Length == 0)
        {
            Debug.LogWarning("Missing extracted character animation: " + spriteKey);
            return;
        }

        characterAnimations[spriteKey] = new CharacterAnimationSet
        {
            Idle = idle.Length > 0 ? idle : StaticFirstFrame(FirstAvailable(attack, move, death)),
            Move = move.Length > 0 ? move : defaultFrames,
            Attack = attack.Length > 0 ? attack : defaultFrames,
            Death = death.Length > 0 ? death : defaultFrames,
            AttackHitFrame = GetAttackHitFrame(spriteKey),
            AttackSoundFrame = GetAttackSoundFrame(spriteKey),
            FrameDuration = 1f / 24f
        };
        recruitIcons[spriteKey] = defaultFrames[0];
    }

    private static Sprite[] StaticFirstFrame(Sprite[] frames)
    {
        return frames != null && frames.Length > 0
            ? new[] { frames[0] }
            : System.Array.Empty<Sprite>();
    }

    private int GetAttackSoundFrame(string spriteKey)
    {
        return spriteKey switch
        {
            "player_king" => 9,
            "enemy_orc_war_rider" => 20,
            "enemy_orc_raider" => 20,
            "player_swordsman" => 19,
            "player_knight" => 23,
            "enemy_dark_ranger" => 6,
            "player_archer" => 6,
            "player_axeman" => 21,
            "player_elven_archer" => 13,
            "enemy_shadow_mage" => 10,
            "player_cavalry" => 15,
            "enemy_stone_golem" => 25,
            "enemy_dark_knight" => 7,
            "enemy_red_dragon" => 42,
            "player_wizard" => 16,
            "enemy_green_dragon" => 42,
            "player_griffin" => 13,
            "enemy_black_dragon" => 42,
            "enemy_fire_golem" => 25,
            _ => GetAttackHitFrame(spriteKey)
        };
    }

    private int GetAttackHitFrame(string spriteKey)
    {
        return spriteKey switch
        {
            "player_king" => 12,
            "enemy_orc_war_rider" => 23,
            "enemy_orc_raider" => 23,
            "player_swordsman" => 19,
            "player_spearman" => 20,
            "player_knight" => 23,
            "enemy_dark_ranger" => 19,
            "player_archer" => 18,
            "player_axeman" => 25,
            "player_elven_archer" => 16,
            "enemy_shadow_mage" => 23,
            "enemy_shaman" => 42,
            "player_cavalry" => 23,
            "enemy_stone_golem" => 42,
            "enemy_dark_knight" => 12,
            "player_healer" => 77,
            "enemy_red_dragon" => 42,
            "player_wizard" => 45,
            "enemy_green_dragon" => 42,
            "player_griffin" => 16,
            "enemy_black_dragon" => 42,
            "enemy_fire_golem" => 25,
            _ => 1
        };
    }

    private Sprite[] LoadExtractedFrames(string spriteKey, string stateName)
    {
        var textures = Resources.LoadAll<Texture2D>("OriginalPrince/ExtractedAnimations/Units/" + spriteKey + "/" + stateName);
        if (textures == null || textures.Length == 0)
        {
            return System.Array.Empty<Sprite>();
        }

        var frames = new List<Sprite>();
        for (var i = 0; i < textures.Length; i++)
        {
            var texture = textures[i];
            texture.filterMode = FilterMode.Bilinear;
            var rect = new Rect(0f, 0f, texture.width, texture.height);
            frames.Add(Sprite.Create(texture, rect, new Vector2(0.5f, 0.12f), 96f));
        }

        frames.Sort((left, right) => ExtractFrameNumber(left.texture.name).CompareTo(ExtractFrameNumber(right.texture.name)));
        return frames.ToArray();
    }

    private Sprite[] FirstAvailable(params Sprite[][] frameSets)
    {
        for (var i = 0; i < frameSets.Length; i++)
        {
            if (frameSets[i] != null && frameSets[i].Length > 0)
            {
                return frameSets[i];
            }
        }

        return System.Array.Empty<Sprite>();
    }

    private int ExtractFrameNumber(string frameName)
    {
        return int.TryParse(frameName, out var number) ? number : 0;
    }

    private CharacterAnimationSet GetCharacterAnimations(string spriteKey, Sprite fallback)
    {
        if (!string.IsNullOrEmpty(spriteKey) && characterAnimations.TryGetValue(spriteKey, out var animations))
        {
            return animations;
        }

        return CharacterAnimationSet.Fallback(fallback);
    }

    private Sprite LoadSpriteFromResource(string resourcePath, Vector2 pivot, float pixelsPerUnit)
    {
        var texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning("Missing sprite resource: " + resourcePath);
            return squareSprite;
        }

        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, pixelsPerUnit);
    }

    private void EnsureGuiSkin()
    {
        GUI.color = Color.white;
        GUI.backgroundColor = new Color(0.22f, 0.24f, 0.27f);
    }

    private GUIStyle Label(int size, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(size, Mathf.RoundToInt(size * UiScale())),
            normal = { textColor = color }
        };
    }

    private GUIStyle CenterLabel(int size, Color color)
    {
        var style = Label(size, color);
        style.alignment = TextAnchor.MiddleCenter;
        return style;
    }

    private GUIStyle WrappedLabel(int size, Color color)
    {
        var style = Label(size, color);
        style.wordWrap = true;
        return style;
    }

    private void DrawFloatingText()
    {
        foreach (var entry in floatingTexts)
        {
            var screen = mainCamera.WorldToScreenPoint(entry.position);
            var rect = new Rect(screen.x - 50f, Screen.height - screen.y - 14f, 100f, 28f);
            GUI.Label(rect, entry.text, CenterLabel(16, entry.color));
        }
    }

    private void OnDisable()
    {
        units.Clear();
    }

    private struct FloatingText
    {
        public Vector3 position;
        public string text;
        public Color color;
        public float timeLeft;
    }

    private struct PendingEnemySpawn
    {
        public UnitStats stats;
        public float timeLeft;
    }

    private struct PendingMeteor
    {
        public Vector3 position;
        public float timeLeft;
    }

    private sealed class WorldPiece : MonoBehaviour
    {
    }

    public struct UnitStats
    {
        public string displayName;
        public string tooltip;
        public int cost;
        public float maxHp;
        public int hpRandomExclusive;
        public float attackDamage;
        public float attackRange;
        public float attackCooldown;
        public float moveSpeed;
        public int speedRandomTenthsExclusive;
        public float bodyHeight;
        public float bodyWidth;
        public float trainCooldown;
        public string spriteKey;
        public float visualScale;
        public int killReward;
        public bool invulnerable;

        public static UnitStats Swordsman(bool ally)
        {
            return new UnitStats
            {
                displayName = ally ? "Swordsman" : "Orc Raider",
                tooltip = "cheap melee",
                cost = ally ? 20 : 0,
                maxHp = 5f,
                hpRandomExclusive = 5,
                attackDamage = 1f,
                attackRange = 0.72f,
                attackCooldown = ally ? 1.25f : 1.17f,
                moveSpeed = 1f,
                speedRandomTenthsExclusive = 20,
                bodyHeight = 0.82f,
                bodyWidth = 0.42f,
                trainCooldown = 2.2f,
                spriteKey = ally ? "player_swordsman" : "enemy_orc_raider",
                visualScale = ally ? 1.1f : 1.25f,
                killReward = ally ? 0 : 10
            };
        }

        public static UnitStats Archer(bool ally)
        {
            return new UnitStats
            {
                displayName = ally ? "Archer" : "Dark Ranger",
                tooltip = "ranged support",
                cost = ally ? 20 : 0,
                maxHp = ally ? 5f : 3f,
                hpRandomExclusive = ally ? 5 : 3,
                attackDamage = 1f,
                attackRange = 5f,
                attackCooldown = 1.33f,
                moveSpeed = 1f,
                speedRandomTenthsExclusive = ally ? 10 : 20,
                bodyHeight = 0.72f,
                bodyWidth = 0.36f,
                trainCooldown = ally ? 4.8f : 3.4f,
                spriteKey = ally ? "player_archer" : "enemy_dark_ranger",
                visualScale = ally ? 1.2f : 1.25f,
                killReward = ally ? 0 : 20
            };
        }

        public static UnitStats Knight(bool ally)
        {
            return new UnitStats
            {
                displayName = ally ? "Knight" : "Stone Golem",
                tooltip = "durable melee",
                cost = ally ? 50 : 0,
                maxHp = ally ? 10f : 100f,
                hpRandomExclusive = ally ? 5 : 50,
                attackDamage = ally ? 1f : 3f,
                attackRange = ally ? 0.82f : 1f,
                attackCooldown = ally ? 1.17f : 1.83f,
                moveSpeed = ally ? 2f : 0.25f,
                speedRandomTenthsExclusive = 10,
                bodyHeight = 1.02f,
                bodyWidth = 0.58f,
                trainCooldown = 5f,
                spriteKey = ally ? "player_knight" : "enemy_stone_golem",
                visualScale = ally ? 1f : 1.2f,
                killReward = ally ? 0 : 300
            };
        }

        public static UnitStats[] AllyRoster()
        {
            return new[]
            {
                Swordsman(true),
                Knight(true),
                Archer(true),
                Create("Axeman", "heavy melee", 100, 30f, 10, 2f, 0.86f, 1.25f, 2f, 15, 1.02f, 0.58f, 4.4f, "player_axeman", 1.1f, 0),
                Create("Elven Swordsman", "swift melee", 125, 10f, 10, 3f, 1.05f, 1.08f, 2f, 25, 0.78f, 0.38f, 4.2f, "player_elven_archer", 1.14f, 0),
            Create("Cavalry", "fast charge", 150, 20f, 30, 2f, 0.92f, 1.58f, 2.25f, 23, 1.02f, 0.72f, 5.4f, "player_cavalry", 1.05f, 0),
                Create("Healer", "light magic", 200, 20f, 5, 1f, 3.8f, 3.75f, 1f, 10, 0.78f, 0.38f, 4.6f, "player_healer", 1.14f, 0),
                Create("Wizard", "burst magic", 300, 20f, 10, 3f, 4.2f, 3.13f, 1f, 5, 0.9f, 0.42f, 6.2f, "player_wizard", 1.12f, 0),
                Create("Griffin", "elite beast", 500, 50f, 20, 4f, 1.05f, 1.25f, 2f, 25, 1.22f, 0.95f, 7.2f, "player_griffin", 1.05f, 0)
            };
        }

        public static int StartingGold(int stage)
        {
            if (stage == 0)
            {
                return 1000;
            }

            return Mathf.Clamp(stage, 1, MaxStage) switch
            {
                10 => 0,
                11 or 12 => 500,
                19 or 20 => 1000,
                _ => 100
            };
        }

        public static UnitStats[] EnemyRoster(int stage)
        {
            if (stage == 0)
            {
                return OriginalEnemyRoster("enemy_stone_golem");
            }

            return Mathf.Clamp(stage, 1, MaxStage) switch
            {
                1 => OriginalEnemyRoster("enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider"),
                2 => OriginalEnemyRoster("enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                3 => OriginalEnemyRoster("enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                4 => OriginalEnemyRoster("enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                5 => OriginalEnemyRoster("enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                6 => OriginalEnemyRoster("enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_shadow_mage"),
                7 => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                8 => OriginalEnemyRoster("enemy_stone_golem", "enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                9 => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                10 => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_orc_war_rider"),
                11 => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_dark_knight", "enemy_orc_war_rider"),
                12 => OriginalEnemyRoster("enemy_shadow_mage", "enemy_dark_knight"),
                13 => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_shadow_mage", "enemy_orc_war_rider"),
                14 => OriginalEnemyRoster("enemy_red_dragon"),
                15 => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_shadow_mage", "enemy_orc_war_rider"),
                16 => OriginalEnemyRoster("enemy_green_dragon"),
                17 => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_shadow_mage", "enemy_orc_war_rider"),
                18 => OriginalEnemyRoster("enemy_stone_golem"),
                19 => OriginalEnemyRoster("enemy_black_dragon"),
                _ => OriginalEnemyRoster("enemy_shaman", "enemy_dark_ranger", "enemy_orc_war_rider", "enemy_orc_raider", "enemy_orc_raider", "enemy_dark_ranger", "enemy_shadow_mage", "enemy_dark_knight", "enemy_fire_golem", "enemy_orc_war_rider")
            };
        }

        public static UnitStats[] EnemyOpeningRoster(int stage)
        {
            return EnemyRoster(stage);
        }

        public static int RoamingOrcCount(int stage)
        {
            if (stage == 0)
            {
                return 0;
            }

            return Mathf.Clamp(stage, 1, MaxStage) switch
            {
                1 or 2 or 3 => 1,
                4 or 5 or 6 or 7 or 8 or 9 => 2,
                10 => 4,
                11 or 12 => 3,
                13 or 14 or 15 => 2,
                16 or 17 => 3,
                18 => 2,
                19 => 4,
                _ => 0
            };
        }

        public static UnitStats[] EnemyReinforcementRoster(int stage)
        {
            var roster = EnemyOpeningRoster(stage);
            var reinforcements = new List<UnitStats>();
            for (var i = 0; i < roster.Length; i++)
            {
                if (EnemyRespawnCost(roster[i].spriteKey) < int.MaxValue)
                {
                    reinforcements.Add(roster[i]);
                }
            }

            return reinforcements.ToArray();
        }

        private static bool HasRespawnableUnit(UnitStats[] roster)
        {
            for (var i = 0; i < roster.Length; i++)
            {
                if (EnemyRespawnCost(roster[i].spriteKey) < int.MaxValue)
                {
                    return true;
                }
            }

            return false;
        }

        public static int EnemyGoldBudget(int stage)
        {
            if (stage == 0)
            {
                return 0;
            }

            return Mathf.Clamp(stage, 1, MaxStage) switch
            {
                1 => 30,
                2 => 35,
                3 => 40,
                4 or 5 or 6 => 50,
                7 or 8 => 70,
                9 or 10 => 80,
                11 or 12 or 13 or 14 or 15 or 16 => 50,
                17 or 18 or 19 => 200,
                _ => 300
            };
        }

        public static int EstimatedEnemyReinforcementCount(int stage)
        {
            var roster = EnemyReinforcementRoster(stage);
            var budget = EnemyGoldBudget(stage);
            var count = 0;
            var index = 0;
            while (roster.Length > 0)
            {
                var spawned = false;
                for (var i = 0; i < roster.Length; i++)
                {
                    var candidateIndex = (index + i) % roster.Length;
                    var cost = EnemyRespawnCost(roster[candidateIndex].spriteKey);
                    if (cost > budget)
                    {
                        continue;
                    }

                    budget -= cost;
                    count++;
                    index = (candidateIndex + 1) % roster.Length;
                    spawned = true;
                    break;
                }

                if (!spawned)
                {
                    break;
                }
            }

            return count;
        }

        public static int EnemyRespawnCost(string spriteKey)
        {
            return spriteKey switch
            {
                "enemy_orc_raider" => 1,
                "enemy_dark_ranger" => 1,
                "enemy_orc_war_rider" => 2,
                "enemy_shaman" => 3,
                _ => int.MaxValue
            };
        }

        private static UnitStats[] OriginalEnemyRoster(params string[] unitIds)
        {
            var roster = new UnitStats[unitIds.Length];
            for (var i = 0; i < unitIds.Length; i++)
            {
                roster[i] = EnemyById(unitIds[i]);
            }

            return roster;
        }

        private static UnitStats EnemyById(string unitId)
        {
            return unitId switch
            {
                "enemy_orc_raider" => Swordsman(false),
                "enemy_roaming_orc" => Create("Roaming Orc", "upper ambusher", 0, 5f, 5, 0f, 0.2f, 1.17f, 2f, 0, 0.82f, 0.42f, 0f, "enemy_roaming_orc", 1.25f, 20),
                "enemy_orc_war_rider" => Create("Orc War Rider", "mounted axe", 0, 10f, 5, 1f, 0.8f, 1.17f, 2f, 10, 0.9f, 0.56f, 0f, "enemy_orc_war_rider", 1.16f, 20),
                "enemy_dark_ranger" => Archer(false),
                "enemy_shadow_mage" => Create("Shadow Mage", "ranged magic", 0, 40f, 20, 2f, 4f, 1.88f, 3f, 20, 0.82f, 0.38f, 0f, "enemy_shadow_mage", 1.14f, 300),
                "enemy_shaman" => Create("Shaman", "ranged magic", 0, 16f, 8, 1f, 4f, 3.13f, 1f, 10, 0.95f, 0.48f, 0f, "enemy_shaman", 1.1f, 30),
                "enemy_stone_golem" => Knight(false),
                "enemy_dark_knight" => Create("Dark Knight", "elite melee", 0, 40f, 40, 2f, 0.92f, 0.83f, 4f, 10, 0.98f, 0.54f, 0f, "enemy_dark_knight", 1.13f, 300),
                "enemy_red_dragon" => Create("Red Dragon", "huge beast", 0, 70f, 50, 3f, 1.18f, 2.92f, 1f, 10, 1.12f, 1.1f, 0f, "enemy_red_dragon", 1.05f, 300),
                "enemy_green_dragon" => Create("Green Dragon", "huge beast", 0, 110f, 50, 4f, 1.18f, 2.92f, 1f, 10, 1.12f, 1.1f, 0f, "enemy_green_dragon", 1.05f, 400),
                "enemy_black_dragon" => Create("Black Dragon", "late elite", 0, 220f, 100, 6f, 1.18f, 2.92f, 1f, 10, 1.12f, 1.1f, 0f, "enemy_black_dragon", 1.05f, 1000),
                "enemy_fire_golem" => Create("Fire Golem", "heavy elemental", 0, 200f, 200, 5f, 1.05f, 1.83f, 0.55f, 10, 1.18f, 0.72f, 0f, "enemy_fire_golem", 1.1f, 0),
                _ => Swordsman(false)
            };
        }

        private static UnitStats Create(string displayName, string tooltip, int cost, float maxHp, int hpRandomExclusive, float attackDamage, float attackRange, float attackCooldown, float moveSpeed, int speedRandomTenthsExclusive, float bodyHeight, float bodyWidth, float trainCooldown, string spriteKey, float visualScale, int killReward)
        {
            return new UnitStats
            {
                displayName = displayName,
                tooltip = tooltip,
                cost = cost,
                maxHp = maxHp,
                hpRandomExclusive = hpRandomExclusive,
                attackDamage = attackDamage,
                attackRange = attackRange,
                attackCooldown = attackCooldown,
                moveSpeed = moveSpeed,
                speedRandomTenthsExclusive = speedRandomTenthsExclusive,
                bodyHeight = bodyHeight,
                bodyWidth = bodyWidth,
                trainCooldown = trainCooldown,
                spriteKey = spriteKey,
                visualScale = visualScale,
                killReward = killReward
            };
        }

        public UnitStats Scaled(int waveNumber)
        {
            var scaled = this;
            switch (waveNumber)
            {
                case 0 when spriteKey == "enemy_stone_golem":
                    scaled.maxHp = 9999f;
                    scaled.hpRandomExclusive = 0;
                    scaled.moveSpeed = 0f;
                    scaled.speedRandomTenthsExclusive = 0;
                    scaled.attackDamage = 0f;
                    scaled.attackRange = 0f;
                    scaled.invulnerable = true;
                    break;
                case 14 when spriteKey == "enemy_red_dragon":
                    scaled.maxHp = 120f;
                    scaled.hpRandomExclusive = 60;
                    scaled.attackDamage = 4f;
                    break;
                case 16 when spriteKey == "enemy_green_dragon":
                    scaled.maxHp = 180f;
                    scaled.hpRandomExclusive = 70;
                    scaled.attackDamage = 5f;
                    break;
                case 18 when spriteKey == "enemy_stone_golem":
                    scaled.maxHp = 190f;
                    scaled.hpRandomExclusive = 80;
                    scaled.attackDamage = 5f;
                    break;
                case 19 when spriteKey == "enemy_black_dragon":
                    scaled.maxHp = 340f;
                    scaled.hpRandomExclusive = 130;
                    scaled.attackDamage = 7f;
                    break;
            }

            return scaled;
        }
    }
}

public enum GameScreen
{
    MainMenu,
    StageSelect,
    Battle
}

public enum MainMenuPopup
{
    None,
    Credits,
    Options
}

public enum CharacterAnimationState
{
    Idle,
    Move,
    Attack,
    Death
}

public sealed class CharacterAnimationSet
{
    public Sprite[] Idle = System.Array.Empty<Sprite>();
    public Sprite[] Move = System.Array.Empty<Sprite>();
    public Sprite[] Attack = System.Array.Empty<Sprite>();
    public Sprite[] Death = System.Array.Empty<Sprite>();
    public int AttackHitFrame = 1;
    public int AttackSoundFrame = 1;
    public float FrameDuration = 1f / 24f;
    public bool UsesFallback;

    public Sprite[] GetFrames(CharacterAnimationState state)
    {
        return state switch
        {
            CharacterAnimationState.Idle => Idle,
            CharacterAnimationState.Move => Move,
            CharacterAnimationState.Attack => Attack,
            CharacterAnimationState.Death => Death,
            _ => Move
        };
    }

    public float GetDuration(CharacterAnimationState state)
    {
        var frames = GetFrames(state);
        return Mathf.Max(FrameDuration, (frames == null ? 0 : frames.Length) * FrameDuration);
    }

    public float GetAttackHitDelay()
    {
        var attackFrameCount = Attack == null ? 0 : Attack.Length;
        var hitFrame = Mathf.Clamp(AttackHitFrame, 1, Mathf.Max(1, attackFrameCount));
        return (hitFrame - 1) * FrameDuration;
    }

    public float GetAttackSoundDelay()
    {
        var attackFrameCount = Attack == null ? 0 : Attack.Length;
        var soundFrame = Mathf.Clamp(AttackSoundFrame, 1, Mathf.Max(1, attackFrameCount));
        return (soundFrame - 1) * FrameDuration;
    }

    public static CharacterAnimationSet Fallback(Sprite fallback)
    {
        var frames = new[] { fallback };
        return new CharacterAnimationSet
        {
            Idle = frames,
            Move = frames,
            Attack = frames,
            Death = frames,
            UsesFallback = true
        };
    }
}

public sealed class MeteorEffect : MonoBehaviour
{
    private const int EffectSortingOrder = 1450;
    private static readonly Vector3 MeteorStartOffset = new Vector3(1.25f, 3.1f, 0f);
    private SpriteRenderer ringRenderer;
    private SpriteRenderer glowRenderer;
    private SpriteRenderer coreRenderer;
    private Vector3 targetPosition;
    private float warningDelay;
    private float strikeRadius;
    private float elapsed;
    private bool impacted;

    public void Initialize(Sprite coreSprite, Sprite glowSprite, Sprite ringSprite, float delay, float radius)
    {
        targetPosition = transform.position;
        warningDelay = Mathf.Max(0.1f, delay);
        strikeRadius = radius;

        ringRenderer = CreateRenderer("Warning Ring", ringSprite, EffectSortingOrder);
        glowRenderer = CreateRenderer("Meteor Glow", glowSprite, EffectSortingOrder + 1);
        coreRenderer = CreateRenderer("Meteor Core", coreSprite, EffectSortingOrder + 2);

        ringRenderer.transform.localScale = Vector3.one * (strikeRadius * 0.92f);
        glowRenderer.transform.localScale = Vector3.one * 0.74f;
        coreRenderer.transform.localScale = Vector3.one * 0.66f;
        coreRenderer.transform.localPosition = MeteorStartOffset;
        glowRenderer.transform.localPosition = coreRenderer.transform.localPosition;
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int sortingOrder)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        var progress = Mathf.Clamp01(elapsed / warningDelay);

        if (!impacted)
        {
            var pulse = 1f + Mathf.Sin(elapsed * 18f) * 0.05f;
            ringRenderer.transform.localScale = Vector3.one * (strikeRadius * 0.92f * pulse);
            var fallOffset = Vector3.Lerp(MeteorStartOffset, Vector3.zero, progress);
            coreRenderer.transform.localPosition = fallOffset;
            glowRenderer.transform.localPosition = fallOffset;
            coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.58f, 0.82f, progress);
            glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.62f, 1.05f, progress);

            if (progress >= 1f)
            {
                impacted = true;
                elapsed = 0f;
                coreRenderer.enabled = false;
                glowRenderer.transform.localPosition = Vector3.zero;
                ringRenderer.transform.localScale = Vector3.one * (strikeRadius * 0.82f);
            }

            return;
        }

        var blast = Mathf.Clamp01(elapsed / 0.38f);
        ringRenderer.transform.localScale = Vector3.one * Mathf.Lerp(strikeRadius * 0.82f, strikeRadius * 1.55f, blast);
        glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.05f, strikeRadius * 1.7f, blast);
        var fade = 1f - blast;
        ringRenderer.color = new Color(1f, 0.38f, 0.08f, fade * 0.85f);
        glowRenderer.color = new Color(1f, 0.58f, 0.1f, fade * 0.9f);

        if (blast >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

public sealed class LightningEffect : MonoBehaviour
{
    private const int EffectSortingOrder = 1460;
    private SpriteRenderer boltRenderer;
    private SpriteRenderer glowRenderer;
    private float elapsed;

    public void Initialize(Sprite boltSprite, Sprite glowSprite)
    {
        glowRenderer = CreateRenderer("Lightning Glow", glowSprite, EffectSortingOrder);
        boltRenderer = CreateRenderer("Lightning Bolt", boltSprite, EffectSortingOrder + 1);

        glowRenderer.transform.localScale = Vector3.one * 0.42f;
        glowRenderer.color = new Color(0.45f, 0.9f, 1f, 0.42f);
        boltRenderer.transform.localPosition = new Vector3(0.18f, 2.45f, 0f);
        boltRenderer.transform.localScale = new Vector3(0.62f, 1.45f, 1f);
        boltRenderer.color = new Color(0.86f, 0.98f, 1f, 1f);
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int sortingOrder)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        var strikeProgress = Mathf.Clamp01(elapsed / 0.18f);
        boltRenderer.transform.localPosition = Vector3.Lerp(new Vector3(0.18f, 2.45f, 0f), Vector3.zero, strikeProgress);

        if (strikeProgress < 1f)
        {
            glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.42f, 0.72f, strikeProgress);
            return;
        }

        var fadeProgress = Mathf.Clamp01((elapsed - 0.18f) / 0.32f);
        var fade = 1f - fadeProgress;
        boltRenderer.color = new Color(0.86f, 0.98f, 1f, fade);
        glowRenderer.color = new Color(0.45f, 0.9f, 1f, fade * 0.62f);
        glowRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.25f, fadeProgress);

        if (fadeProgress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}

public sealed class LaneUnit : MonoBehaviour
{
    private const int UnitSortingBase = 1000;
    private const int UnitSortingScale = 100;
    private const int UnitSortingStride = 10;
    private const int HealthBarSortingOffset = 6;
    private const float HealerAllyHealAmount = 3f;
    private const float GolemKnockbackChance = 0.18f;
    private const float GolemKnockbackDistance = 0.45f;
    private const float GolemKnockbackRadius = 0.95f;
    private const float GolemKnockbackLockDuration = 3f;
    private const float DragonAttackRadius = 1.05f;
    private const float WizardLightningChance = 0.25f;
    private const float WizardLightningRadius = 1.1f;
    private const float WizardLightningDamage = 2f;
    private const float WizardLightningBacklineOffset = 0.65f;

    private PrinceOfWarPrototype game;
    private PrinceOfWarPrototype.UnitStats stats;
    private Transform visualRoot;
    private SpriteRenderer spriteRenderer;
    private CharacterAnimationSet animations;
    private Sprite[] activeFrames;
    private Transform hpBar;
    private float maxHp;
    private float hp;
    private float moveSpeed;
    private float attackTimer;
    private float frameTimer;
    private int frameIndex;
    private Vector3 baseVisualScale;
    private float anchorFrameWidth;
    private CharacterAnimationState currentState;
    private bool loopAnimation;
    private bool isDying;
    private bool pendingHeroHit;
    private float pendingHitTimer;
    private LaneUnit pendingTarget;
    private bool pendingAttackSound;
    private float pendingAttackSoundTimer;
    private float deathTimer;
    private float movementLockTimer;

    public int Direction { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsCombatActive => !IsDead && !isDying;
    public bool IsWounded => IsCombatActive && hp < maxHp - 0.01f;
    public bool IsAlliedMeteorTarget => Direction > 0 && IsMeteorTargetSprite(stats.spriteKey);
    public bool IsEnemyBoss => Direction < 0 && IsBossSprite(stats.spriteKey);
    public string SpriteKey => stats.spriteKey;
    public float MissingHp => Mathf.Max(0f, maxHp - hp);
    public float AttackRange => stats.attackRange;

    public void Initialize(PrinceOfWarPrototype owner, PrinceOfWarPrototype.UnitStats unitStats, int direction, CharacterAnimationSet animationSet, Sprite barSprite, Color color)
    {
        game = owner;
        stats = unitStats;
        Direction = direction;
        maxHp = stats.maxHp + Random.Range(0, Mathf.Max(1, stats.hpRandomExclusive));
        hp = maxHp;
        moveSpeed = stats.moveSpeed + Random.Range(0, Mathf.Max(1, stats.speedRandomTenthsExclusive)) / 10f;
        animations = animationSet;
        activeFrames = animations.GetFrames(CharacterAnimationState.Move);
        var firstFrame = activeFrames.Length > 0 ? activeFrames[0] : barSprite;

        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);

        spriteRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = firstFrame;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = 4;
        visualRoot.localScale = firstFrame == barSprite
            ? new Vector3(stats.bodyWidth, stats.bodyHeight, 1f)
            : Vector3.one * stats.visualScale;
        if (Direction < 0 && ShouldFlipForEnemyDirection(stats.spriteKey))
        {
            visualRoot.localScale = new Vector3(-visualRoot.localScale.x, visualRoot.localScale.y, visualRoot.localScale.z);
        }

        baseVisualScale = visualRoot.localScale;
        anchorFrameWidth = firstFrame.bounds.size.x;
        PlayAnimation(moveSpeed <= 0.01f ? CharacterAnimationState.Idle : CharacterAnimationState.Move, true);

        var hpBackground = CreateBar("Hp Back", barSprite, Color.black, new Vector3(0f, 0.94f, -0.02f), new Vector3(0.72f, 0.06f, 1f));
        hpBackground.SetParent(transform, false);
        hpBar = CreateBar("Hp", barSprite, Color.green, new Vector3(0f, 0.94f, -0.03f), new Vector3(0.68f, 0.04f, 1f));
        hpBar.SetParent(transform, false);
        RefreshSortingOrder();
    }

    public void ManualUpdate(float deltaTime)
    {
        if (IsDead)
        {
            return;
        }

        RefreshSortingOrder();

        if (isDying)
        {
            TickFrameAnimation(deltaTime);
            deathTimer -= deltaTime;
            if (deathTimer <= 0f)
            {
                MarkDead();
            }

            return;
        }

        attackTimer -= deltaTime;

        if (movementLockTimer > 0f)
        {
            movementLockTimer -= deltaTime;
            PlayAnimation(CharacterAnimationState.Idle, false);
            TickFrameAnimation(deltaTime);
            return;
        }

        TickFrameAnimation(deltaTime);
        TickPendingAttackSound(deltaTime);
        TickPendingAttack(deltaTime);

        var canStartAttack = game.IsInsideBattlefieldEntry(this);
        if (canStartAttack && game.ShouldHoldForOriginalRangedAttack(this, stats.spriteKey))
        {
            var rangedTarget = game.FindTarget(this);
            if (rangedTarget != null)
            {
                AttackUnit(rangedTarget);
            }
            else if (attackTimer <= 0f)
            {
                PlayAnimation(CharacterAnimationState.Attack, false);
            }

            return;
        }

        var target = canStartAttack ? game.FindTarget(this) : null;

        if (target != null && Mathf.Abs(target.transform.position.x - transform.position.x) <= stats.attackRange)
        {
            AttackUnit(target);
            return;
        }

        if (canStartAttack && Direction < 0 && game.IsHeroInRange(transform.position, stats.attackRange))
        {
            AttackHero();
            return;
        }

        if (attackTimer > 0f && currentState == CharacterAnimationState.Attack)
        {
            return;
        }

        if (game.HasReachedBattlefieldEdge(this))
        {
            game.ResolveBattlefieldEdge(this);
            return;
        }

        if (moveSpeed <= 0.01f)
        {
            PlayAnimation(CharacterAnimationState.Idle, false);
            return;
        }

        PlayAnimation(CharacterAnimationState.Move, false);
        transform.position += Vector3.right * Direction * moveSpeed * PrinceOfWarPrototype.CharacterMoveSpeedMultiplier * deltaTime;
        RefreshSortingOrder();
    }

    public void TakeDamage(float amount, int attackerDirection)
    {
        if (isDying || IsDead)
        {
            return;
        }

        if (stats.invulnerable)
        {
            game.AddFloatingText(transform.position + Vector3.up * 0.8f, "Immune", Color.gray);
            return;
        }

        hp -= amount;
        game.AddFloatingText(transform.position + Vector3.up * 0.8f, "-" + Mathf.CeilToInt(amount), Color.white);
        RefreshHpBar();

        if (hp <= 0f)
        {
            BeginDeath();
            game.RewardForKill(attackerDirection > 0, stats);
        }
    }

    public void ReceiveHeal(float amount)
    {
        if (!IsWounded)
        {
            return;
        }

        hp = Mathf.Min(maxHp, hp + amount);
        RefreshHpBar();
        game.AddFloatingText(transform.position + Vector3.up * 0.8f, "+" + Mathf.CeilToInt(amount), Color.green);
    }

    public void KillByMeteor()
    {
        if (!IsCombatActive)
        {
            return;
        }

        hp = 0f;
        RefreshHpBar();
        game.AddFloatingText(transform.position + Vector3.up * 0.8f, "Meteor", new Color(1f, 0.35f, 0.1f));
        BeginDeath();
    }

    private void BeginDeath()
    {
        if (isDying)
        {
            return;
        }

        isDying = true;
        ClearPendingAttack();
        game.PlayDeathSound(stats.spriteKey);
        PlayAnimation(CharacterAnimationState.Death, true);
        deathTimer = Mathf.Max(0.25f, activeFrames.Length * animations.FrameDuration + 0.1f);

        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer.transform == visualRoot || renderer.transform.IsChildOf(visualRoot))
            {
                continue;
            }

            renderer.enabled = false;
        }
    }

    public void MarkDead()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        Destroy(gameObject);
    }

    private void AttackUnit(LaneUnit target)
    {
        if (attackTimer > 0f)
        {
            return;
        }

        attackTimer = stats.attackCooldown;
        attackTimer = Mathf.Max(attackTimer, animations.GetDuration(CharacterAnimationState.Attack));
        PlayAnimation(CharacterAnimationState.Attack, true);
        pendingTarget = target;
        pendingHeroHit = false;
        pendingHitTimer = animations.GetAttackHitDelay();
        ScheduleAttackSound();
    }

    private void AttackHero()
    {
        if (attackTimer > 0f)
        {
            return;
        }

        attackTimer = stats.attackCooldown;
        attackTimer = Mathf.Max(attackTimer, animations.GetDuration(CharacterAnimationState.Attack));
        PlayAnimation(CharacterAnimationState.Attack, true);
        pendingTarget = null;
        pendingHeroHit = true;
        pendingHitTimer = animations.GetAttackHitDelay();
        ScheduleAttackSound();
    }

    private void ScheduleAttackSound()
    {
        pendingAttackSound = true;
        pendingAttackSoundTimer = animations.GetAttackSoundDelay();
    }

    private void TickPendingAttackSound(float deltaTime)
    {
        if (!pendingAttackSound)
        {
            return;
        }

        pendingAttackSoundTimer -= deltaTime;
        if (pendingAttackSoundTimer > 0f)
        {
            return;
        }

        game.PlayAttackSound(stats.spriteKey);
        pendingAttackSound = false;
    }

    private void TickPendingAttack(float deltaTime)
    {
        if (pendingTarget == null && !pendingHeroHit)
        {
            return;
        }

        pendingHitTimer -= deltaTime;
        if (pendingHitTimer > 0f)
        {
            return;
        }

        if (pendingHeroHit)
        {
            if (game.IsHeroInRange(transform.position, stats.attackRange + 0.25f))
            {
                game.DamageHero(Mathf.Max(PrinceOfWarPrototype.EnemyDamageToHero, stats.attackDamage));
            }

            ClearPendingAttack();
            return;
        }

        var target = pendingTarget != null && pendingTarget.IsCombatActive
            ? pendingTarget
            : game.FindTarget(this);

        if (target != null && target.IsCombatActive && Mathf.Abs(target.transform.position.x - transform.position.x) <= stats.attackRange + 0.25f)
        {
            var hitCenter = target.transform.position;
            if (IsDragonAttacker() && Direction < 0 && target.Direction > 0)
            {
                game.DamageAlliedUnitsInRange(hitCenter, DragonAttackRadius, stats.attackDamage, Direction);
            }
            else
            {
                target.TakeDamage(stats.attackDamage, Direction);
                TryKnockBackAllies(target, hitCenter);
                TryWizardLightning(target, hitCenter);
            }
        }

        TryHealAllyOnAttack();
        ClearPendingAttack();
    }

    private void TryWizardLightning(LaneUnit target, Vector3 hitCenter)
    {
        if (Direction <= 0 || target.Direction >= 0 || stats.spriteKey != "player_wizard")
        {
            return;
        }

        var forced = game.IsForcedWizardLightningReady();
        if (!forced && Random.value > WizardLightningChance)
        {
            return;
        }

        if (!game.TryFindEnemyBacklineOrNearestPosition(hitCenter, WizardLightningBacklineOffset, out var lightningCenter))
        {
            return;
        }

        if (forced)
        {
            game.ConsumeForcedWizardLightning();
        }

        game.SpawnLightningEffect(lightningCenter);
        var hitCount = game.DamageEnemyUnitsInRange(lightningCenter, WizardLightningRadius, WizardLightningDamage, Direction, null);
        if (hitCount > 0)
        {
            game.AddFloatingText(lightningCenter + Vector3.up * 0.9f, "Lightning", new Color(0.45f, 0.85f, 1f));
        }
    }

    private void TryKnockBackAllies(LaneUnit target, Vector3 hitCenter)
    {
        if (Direction >= 0 || target.Direction <= 0 || !IsGolemAttacker() || Random.value > GolemKnockbackChance)
        {
            return;
        }

        game.KnockBackAlliedUnitsInRange(hitCenter, GolemKnockbackRadius, GolemKnockbackDistance, GolemKnockbackLockDuration);
    }

    private bool IsGolemAttacker()
    {
        return stats.spriteKey == "enemy_stone_golem"
            || stats.spriteKey == "enemy_fire_golem";
    }

    private bool IsDragonAttacker()
    {
        return stats.spriteKey == "enemy_red_dragon"
            || stats.spriteKey == "enemy_green_dragon"
            || stats.spriteKey == "enemy_black_dragon";
    }

    private static bool IsMeteorTargetSprite(string spriteKey)
    {
        return spriteKey == "player_archer"
            || spriteKey == "player_healer"
            || spriteKey == "player_wizard";
    }

    private static bool IsBossSprite(string spriteKey)
    {
        return spriteKey == "enemy_stone_golem"
            || spriteKey == "enemy_dark_knight"
            || spriteKey == "enemy_shadow_mage"
            || spriteKey == "enemy_red_dragon"
            || spriteKey == "enemy_green_dragon"
            || spriteKey == "enemy_black_dragon"
            || spriteKey == "enemy_fire_golem";
    }

    public void ReceiveKnockback(float distance, float lockDuration)
    {
        if (!IsCombatActive)
        {
            return;
        }

        ClearPendingAttack();
        movementLockTimer = Mathf.Max(movementLockTimer, lockDuration);
        transform.position = new Vector3(game.ClampLaneUnitX(transform.position.x - Direction * distance), transform.position.y, transform.position.z);
        PlayAnimation(CharacterAnimationState.Idle, true);
        RefreshSortingOrder();
        game.AddFloatingText(transform.position + Vector3.up * 0.8f, "Push", Color.gray);
    }

    private void TryHealAllyOnAttack()
    {
        if (Direction <= 0 || stats.spriteKey != "player_healer")
        {
            return;
        }

        var ally = game.FindWoundedAlly(this, stats.attackRange);
        ally?.ReceiveHeal(HealerAllyHealAmount);
    }

    private void RefreshHpBar()
    {
        if (hpBar == null)
        {
            return;
        }

        var ratio = Mathf.Clamp01(hp / maxHp);
        hpBar.localScale = new Vector3(0.68f * ratio, 0.04f, 1f);
        hpBar.localPosition = new Vector3((ratio - 1f) * 0.34f, 0.94f, -0.03f);
    }

    private void ClearPendingAttack()
    {
        pendingTarget = null;
        pendingHeroHit = false;
        pendingHitTimer = 0f;
        pendingAttackSound = false;
        pendingAttackSoundTimer = 0f;
    }

    private void TickFrameAnimation(float deltaTime)
    {
        if (activeFrames == null || activeFrames.Length <= 1 || spriteRenderer == null)
        {
            return;
        }

        frameTimer += deltaTime;
        if (frameTimer < animations.FrameDuration)
        {
            return;
        }

        frameTimer = 0f;
        if (frameIndex >= activeFrames.Length - 1)
        {
            if (!loopAnimation)
            {
                return;
            }

            frameIndex = 0;
        }
        else
        {
            frameIndex++;
        }

        spriteRenderer.sprite = activeFrames[frameIndex];
        ApplyFrameOffset();
    }

    private void PlayAnimation(CharacterAnimationState state, bool restart)
    {
        if (!restart && currentState == state)
        {
            return;
        }

        var frames = animations.GetFrames(state);
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        currentState = state;
        activeFrames = frames;
        loopAnimation = state == CharacterAnimationState.Idle || state == CharacterAnimationState.Move;
        frameIndex = 0;
        frameTimer = 0f;
        spriteRenderer.sprite = activeFrames[frameIndex];
        ApplyFrameOffset();
    }

    private void ApplyFrameOffset()
    {
        if (visualRoot == null || spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        var offsetX = 0f;
        if (currentState == CharacterAnimationState.Attack && anchorFrameWidth > 0f)
        {
            var extraWidth = spriteRenderer.sprite.bounds.size.x - anchorFrameWidth;
            if (extraWidth > anchorFrameWidth * 3f)
            {
                offsetX = Direction * extraWidth * 0.5f * Mathf.Abs(baseVisualScale.x);
            }
        }

        visualRoot.localPosition = new Vector3(offsetX, 0f, 0f);
    }

    private Transform CreateBar(string objectName, Sprite sprite, Color color, Vector3 localPosition, Vector3 localScale)
    {
        var bar = new GameObject(objectName).transform;
        bar.localPosition = localPosition;
        bar.localScale = localScale;
        var renderer = bar.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 8;
        return bar;
    }

    private void RefreshSortingOrder()
    {
        var baseOrder = UnitSortingBase
            + Mathf.RoundToInt(-transform.position.y * UnitSortingScale) * UnitSortingStride
            + Mathf.Abs(GetInstanceID()) % UnitSortingStride;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = baseOrder;
        }

        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer == spriteRenderer)
            {
                continue;
            }

            renderer.sortingOrder = baseOrder + HealthBarSortingOffset;
        }
    }

    private bool ShouldFlipForEnemyDirection(string spriteKey)
    {
        return spriteKey != "enemy_stone_golem"
            && spriteKey != "enemy_fire_golem";
    }
}

public sealed class RoamingOrcUnit : MonoBehaviour
{
    private const int UnitSortingBase = 1000;
    private const int UnitSortingScale = 100;
    private const int UnitSortingStride = 10;
    private const float RearHitRangeBonus = 0.32f;
    private const float RearHitUpperYLimit = 0.46f;

    private PrinceOfWarPrototype game;
    private SpriteRenderer spriteRenderer;
    private CharacterAnimationSet animations;
    private Sprite[] activeFrames;
    private float moveSpeed;
    private float frameTimer;
    private float deathTimer;
    private int frameIndex;
    private CharacterAnimationState currentState;
    private bool loopAnimation;
    private bool isDying;
    private bool breached;

    public void Initialize(PrinceOfWarPrototype owner, CharacterAnimationSet animationSet, Sprite fallbackSprite, float speed)
    {
        game = owner;
        animations = animationSet;
        moveSpeed = speed;
        activeFrames = animations.GetFrames(CharacterAnimationState.Move);
        var firstFrame = activeFrames.Length > 0 ? activeFrames[0] : fallbackSprite;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = firstFrame;
        spriteRenderer.color = firstFrame == fallbackSprite ? new Color(0.32f, 0.85f, 0.28f) : Color.white;
        spriteRenderer.sortingOrder = 5;
        transform.localScale = firstFrame == fallbackSprite ? new Vector3(0.42f, 0.82f, 1f) : Vector3.one * 1.25f;
        if (firstFrame != fallbackSprite)
        {
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
        }

        PlayAnimation(CharacterAnimationState.Move, true);
        RefreshSortingOrder();
    }

    public void ManualUpdate(float deltaTime)
    {
        if (breached)
        {
            return;
        }

        RefreshSortingOrder();
        TickFrameAnimation(deltaTime);

        if (isDying)
        {
            deathTimer -= deltaTime;
            if (deathTimer <= 0f)
            {
                ResetToSpawn();
            }

            return;
        }

        transform.position += Vector3.left * moveSpeed * deltaTime;
        RefreshSortingOrder();
        if (game.HasRoamingOrcBreached(transform.position))
        {
            breached = true;
            game.ResolveRoamingOrcBreach(transform.position);
        }
    }

    public bool TryKillFromHero(Vector3 heroPosition, float attackRange)
    {
        if (isDying || breached)
        {
            return false;
        }

        var isBehindHero = transform.position.x < heroPosition.x;
        if (isBehindHero && transform.position.y > heroPosition.y + RearHitUpperYLimit)
        {
            return false;
        }

        var effectiveRange = isBehindHero ? attackRange + RearHitRangeBonus : attackRange;
        if (Vector2.Distance(heroPosition, transform.position) > effectiveRange)
        {
            return false;
        }

        isDying = true;
        deathTimer = Mathf.Max(21f / 24f, animations.GetDuration(CharacterAnimationState.Death));
        game.PlayDeathSound("enemy_roaming_orc");
        game.RewardRoamingOrcKill(transform.position);
        PlayAnimation(CharacterAnimationState.Death, true);
        return true;
    }

    private void ResetToSpawn()
    {
        isDying = false;
        transform.position = game.GetRoamingOrcSpawnPosition();
        PlayAnimation(CharacterAnimationState.Move, true);
        RefreshSortingOrder();
    }

    private void TickFrameAnimation(float deltaTime)
    {
        if (activeFrames == null || activeFrames.Length <= 1 || spriteRenderer == null)
        {
            return;
        }

        frameTimer += deltaTime;
        if (frameTimer < animations.FrameDuration)
        {
            return;
        }

        frameTimer = 0f;
        if (frameIndex >= activeFrames.Length - 1)
        {
            if (!loopAnimation)
            {
                return;
            }

            frameIndex = 0;
        }
        else
        {
            frameIndex++;
        }

        spriteRenderer.sprite = activeFrames[frameIndex];
    }

    private void PlayAnimation(CharacterAnimationState state, bool restart)
    {
        if (!restart && currentState == state)
        {
            return;
        }

        var frames = animations.GetFrames(state);
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        currentState = state;
        activeFrames = frames;
        loopAnimation = state == CharacterAnimationState.Idle || state == CharacterAnimationState.Move;
        frameIndex = 0;
        frameTimer = 0f;
        spriteRenderer.sprite = activeFrames[frameIndex];
    }

    private void RefreshSortingOrder()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingOrder = UnitSortingBase
            + Mathf.RoundToInt(-transform.position.y * UnitSortingScale) * UnitSortingStride
            + Mathf.Abs(GetInstanceID()) % UnitSortingStride;
    }
}

public sealed class HeroUnit : MonoBehaviour
{
    private const float MaxHp = 100f;
    private const float MoveSpeed = 3f;
    private const float AttackDamage = 1.25f;
    private const float AttackRange = 0.72f;
    private const float AttackCooldown = 0.83f;
    private const float RespawnDelay = 4f;
    private const int UnitSortingBase = 1000;
    private const int UnitSortingScale = 100;
    private const int UnitSortingStride = 10;

    private PrinceOfWarPrototype game;
    private Transform visualRoot;
    private SpriteRenderer bodyRenderer;
    private CharacterAnimationSet animations;
    private Sprite[] activeFrames;
    private Transform hpBar;
    private float hp;
    private float attackTimer;
    private float frameTimer;
    private int frameIndex;
    private float respawnTimer;
    private int facingDirection = 1;
    private Vector3 spawnPosition;
    private Vector3 baseVisualScale;
    private CharacterAnimationState currentState;
    private bool loopAnimation;
    private bool pendingAttackHit;
    private float pendingAttackTimer;
    private bool pendingAttackSound;
    private float pendingAttackSoundTimer;

    public bool IsAlive => hp > 0f;
    public float Hp => Mathf.Max(0f, hp);

    public void Initialize(PrinceOfWarPrototype owner, CharacterAnimationSet animationSet, Sprite barSprite)
    {
        game = owner;
        spawnPosition = transform.position;
        hp = MaxHp;
        animations = animationSet;
        activeFrames = animations.GetFrames(CharacterAnimationState.Idle);
        var firstFrame = activeFrames.Length > 0 ? activeFrames[0] : barSprite;

        visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(transform, false);

        bodyRenderer = visualRoot.gameObject.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = firstFrame;
        bodyRenderer.color = firstFrame == barSprite ? new Color(1f, 0.86f, 0.18f) : Color.white;
        bodyRenderer.sortingOrder = 6;
        visualRoot.localScale = firstFrame == barSprite ? new Vector3(0.72f, 1.22f, 1f) : Vector3.one * 1.15f;
        baseVisualScale = visualRoot.localScale;

        if (firstFrame == barSprite)
        {
            var crown = CreateChildSprite("Crown", barSprite, new Color(1f, 0.96f, 0.35f), new Vector3(0f, 0.68f, -0.02f), new Vector3(0.82f, 0.2f, 1f), 9);
            crown.SetParent(visualRoot, false);

            var cape = CreateChildSprite("Cape", barSprite, new Color(0.45f, 0.12f, 0.18f), new Vector3(-0.13f, -0.04f, 0.02f), new Vector3(0.82f, 1.08f, 1f), 5);
            cape.SetParent(visualRoot, false);
        }

        var hpBack = CreateChildSprite("King Hp Back", barSprite, Color.black, new Vector3(0f, 1.16f, -0.03f), new Vector3(0.92f, 0.07f, 1f), 10);
        hpBack.SetParent(transform, false);
        hpBar = CreateChildSprite("King Hp", barSprite, Color.green, new Vector3(0f, 1.16f, -0.04f), new Vector3(0.88f, 0.045f, 1f), 11);
        hpBar.SetParent(transform, false);
        PlayAnimation(CharacterAnimationState.Idle, true);
        RefreshSortingOrder();
    }

    public void ManualUpdate(float deltaTime)
    {
        RefreshSortingOrder();
        attackTimer -= deltaTime;
        TickFrameAnimation(deltaTime);
        TickPendingAttackSound(deltaTime);
        TickPendingAttack(deltaTime);

        if (!IsAlive)
        {
            TickRespawn(deltaTime);
            return;
        }

        if (game.IsPrimaryAttackHeld())
        {
            Attack();
        }

        var move = Vector2.zero;
        var isAttacking = attackTimer > 0f || pendingAttackHit;

        if (!isAttacking)
        {
            var moveX = 0f;
            var moveY = 0f;
            if (game.IsKeyHeld(Key.A) || game.IsKeyHeld(Key.LeftArrow))
            {
                moveX -= 1f;
            }

            if (game.IsKeyHeld(Key.D) || game.IsKeyHeld(Key.RightArrow))
            {
                moveX += 1f;
            }

            if (game.IsKeyHeld(Key.W) || game.IsKeyHeld(Key.UpArrow))
            {
                moveY += 1f;
            }

            if (game.IsKeyHeld(Key.S) || game.IsKeyHeld(Key.DownArrow))
            {
                moveY -= 1f;
            }

            move = new Vector2(moveX, moveY);
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }
        }

        var moved = move.sqrMagnitude > 0f;
        if (moved)
        {
            if (!Mathf.Approximately(move.x, 0f))
            {
                facingDirection = move.x > 0f ? 1 : -1;
                visualRoot.localScale = new Vector3(baseVisualScale.x * facingDirection, baseVisualScale.y, baseVisualScale.z);
            }

            var moveDistance = MoveSpeed * PrinceOfWarPrototype.CharacterMoveSpeedMultiplier * deltaTime;
            var nextX = game.ClampHeroX(transform.position.x + move.x * moveDistance);
            var nextY = game.ClampHeroY(transform.position.y + move.y * moveDistance);
            transform.position = new Vector3(nextX, nextY, transform.position.z);
            RefreshSortingOrder();
        }

        if (attackTimer <= 0f)
        {
            PlayAnimation(moved ? CharacterAnimationState.Move : CharacterAnimationState.Idle, false);
        }

        if (game.WasKeyPressed(Key.LeftShift) || game.WasKeyPressed(Key.RightShift))
        {
            Heal();
        }
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive)
        {
            return;
        }

        hp -= amount;
        game.AddFloatingText(transform.position + Vector3.up * 1f, "-" + Mathf.CeilToInt(amount), Color.yellow);
        RefreshHpBar();

        if (hp <= 0f)
        {
            hp = 0f;
            pendingAttackHit = false;
            pendingAttackSound = false;
            respawnTimer = RespawnDelay;
            game.PlayDeathSound("player_king");
            PlayAnimation(CharacterAnimationState.Death, true);
            game.AddFloatingText(transform.position + Vector3.up * 1.25f, "King down", Color.red);
        }
    }

    private void Attack()
    {
        if (attackTimer > 0f)
        {
            return;
        }

        attackTimer = Mathf.Max(AttackCooldown, animations.GetDuration(CharacterAnimationState.Attack));
        PlayAnimation(CharacterAnimationState.Attack, true);
        pendingAttackHit = true;
        pendingAttackTimer = animations.GetAttackHitDelay();
        pendingAttackSound = true;
        pendingAttackSoundTimer = animations.GetAttackSoundDelay();
    }

    private void TickPendingAttackSound(float deltaTime)
    {
        if (!pendingAttackSound)
        {
            return;
        }

        pendingAttackSoundTimer -= deltaTime;
        if (pendingAttackSoundTimer > 0f)
        {
            return;
        }

        game.PlayAttackSound("player_king");
        pendingAttackSound = false;
    }

    private void TickPendingAttack(float deltaTime)
    {
        if (!pendingAttackHit)
        {
            return;
        }

        pendingAttackTimer -= deltaTime;
        if (pendingAttackTimer > 0f)
        {
            return;
        }

        var hitCount = game.DamageEnemiesInRange(transform.position, AttackRange, AttackDamage);
        if (hitCount == 0)
        {
            game.AddFloatingText(transform.position + Vector3.up * 1f, "Miss", Color.white);
        }
        pendingAttackHit = false;
    }

    private void TickFrameAnimation(float deltaTime)
    {
        if (activeFrames == null || activeFrames.Length <= 1 || bodyRenderer == null)
        {
            return;
        }

        frameTimer += deltaTime;
        if (frameTimer < animations.FrameDuration)
        {
            return;
        }

        frameTimer = 0f;
        if (frameIndex >= activeFrames.Length - 1)
        {
            if (!loopAnimation)
            {
                return;
            }

            frameIndex = 0;
        }
        else
        {
            frameIndex++;
        }

        bodyRenderer.sprite = activeFrames[frameIndex];
    }

    private void Heal()
    {
        if (hp >= MaxHp)
        {
            game.AddFloatingText(transform.position + Vector3.up * 1f, "Full", Color.white);
            return;
        }

        if (!game.TrySpendGold(25))
        {
            game.AddFloatingText(transform.position + Vector3.up * 1f, "No gold", Color.red);
            return;
        }

        hp = Mathf.Min(MaxHp, hp + 42f);
        RefreshHpBar();
        game.AddFloatingText(transform.position + Vector3.up * 1f, "Heal", Color.green);
    }

    private void TickRespawn(float deltaTime)
    {
        respawnTimer -= deltaTime;
        if (respawnTimer > 0f)
        {
            return;
        }

        hp = MaxHp;
        transform.position = spawnPosition;
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.enabled = true;
        }

        PlayAnimation(CharacterAnimationState.Idle, true);
        RefreshHpBar();
        RefreshSortingOrder();
        game.AddFloatingText(transform.position + Vector3.up * 1.25f, "King returns", Color.green);
    }

    private void PlayAnimation(CharacterAnimationState state, bool restart)
    {
        if (!restart && currentState == state)
        {
            return;
        }

        var frames = animations.GetFrames(state);
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        currentState = state;
        activeFrames = frames;
        loopAnimation = state == CharacterAnimationState.Idle || state == CharacterAnimationState.Move;
        frameIndex = 0;
        frameTimer = 0f;
        bodyRenderer.sprite = activeFrames[frameIndex];
    }

    private void RefreshHpBar()
    {
        if (hpBar == null)
        {
            return;
        }

        var ratio = Mathf.Clamp01(hp / MaxHp);
        hpBar.localScale = new Vector3(0.88f * ratio, 0.045f, 1f);
        hpBar.localPosition = new Vector3((ratio - 1f) * 0.44f, 1.16f, -0.04f);
    }

    private Transform CreateChildSprite(string objectName, Sprite sprite, Color color, Vector3 localPosition, Vector3 localScale, int sortingOrder)
    {
        var child = new GameObject(objectName).transform;
        child.localPosition = localPosition;
        child.localScale = localScale;
        var renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return child;
    }

    private void RefreshSortingOrder()
    {
        var baseOrder = UnitSortingBase
            + Mathf.RoundToInt(-transform.position.y * UnitSortingScale) * UnitSortingStride
            + Mathf.Abs(GetInstanceID()) % UnitSortingStride;

        if (bodyRenderer != null)
        {
            bodyRenderer.sortingOrder = baseOrder;
        }

        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            if (renderer == bodyRenderer)
            {
                continue;
            }

            if (renderer.transform.IsChildOf(visualRoot))
            {
                renderer.sortingOrder = renderer.gameObject.name == "Cape" ? baseOrder - 1 : baseOrder + 1;
                continue;
            }

            renderer.sortingOrder = baseOrder + 6;
        }
    }
}
