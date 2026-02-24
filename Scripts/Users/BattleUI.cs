using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User — 2D HUD overlay.
///
/// Navigation calls go to ISceneRouterService — no scene paths here.
/// Movement-blocked indicator uses IMapService.GetReachableTiles.
/// Pause menu keeps ProcessMode=Always so ESC works while tree is paused.
/// </summary>
[User]
public sealed partial class BattleUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private IGameStateService?    _gameState;
    [Inject] private IBattleService?       _battleService;
    [Inject] private IMapService?          _mapService;
    [Inject] private ISceneRouterService?  _router;
    [Inject] private ILevelRegistryService? _registry;

    // ── HUD ───────────────────────────────────────────────────────────────────
    private Label?           _turnLabel;
    private Label?           _phaseLabel;
    private Button?          _endTurnButton;
    private ScrollContainer? _logScroll;
    private Label?           _logLabel;
    private bool             _needsScroll;

    // ── Unit info panel ───────────────────────────────────────────────────────
    private Panel? _unitPanel;
    private Label? _unitHeader;
    private Label? _unitName;
    private Label? _unitHp;
    private Label? _unitStats;
    private Label? _unitActions;
    private Label? _unitBlocked;

    // ── Overlays ──────────────────────────────────────────────────────────────
    private Panel? _gameOverPanel;
    private Label? _gameOverLabel;
    private Panel? _pausePanel;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildHud();
        BuildUnitInfoPanel();
        BuildGameOverPanel();
        BuildPausePanel();
    }

    public override void _Process(double _)
    {
        if (!_needsScroll || _logScroll == null) return;
        _logScroll.ScrollVertical = (int)(_logScroll.GetVScrollBar()?.MaxValue ?? 999999f);
        _needsScroll = false;
    }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[BattleUI] DI failed."); return; }

        // Populate level name in HUD title
        var lvl = _registry?.ActiveLevel;
        if (lvl != null) AppendLog($"═ {lvl.Name} — {lvl.Difficulty} ═");

        _gameState!.OnPhaseChanged += phase =>
        {
            _phaseLabel!.Text        = $"Phase: {phase}";
            _endTurnButton!.Disabled = phase != GamePhase.PlayerTurn;
            AppendLog($"• Phase → {phase}");
            if (phase is GamePhase.Victory or GamePhase.Defeat)
                ShowGameOver(phase == GamePhase.Victory);
        };

        _gameState.OnTurnStarted += turn =>
        {
            _turnLabel!.Text = $"Turn  {turn}";
            AppendLog($"─── Turn {turn} ───");
        };

        _gameState.OnSelectionChanged += ShowUnitInfo;

        _battleService!.OnAttackExecuted += (atk, def, dmg) =>
            AppendLog($"{atk.Name} → {def.Name}  -{dmg} HP");

        _battleService.OnUnitDefeated += u =>
            AppendLog($"☠ {u.Name} defeated!");

        // Sync immediately — BeginPlayerTurn may have already fired
        _endTurnButton!.Disabled = _gameState.Phase != GamePhase.PlayerTurn;
        _phaseLabel!.Text        = $"Phase: {_gameState.Phase}";
        _turnLabel!.Text         = $"Turn  {_gameState.CurrentTurn}";
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true } key)
        {
            if (key.Keycode == Key.Escape)
            {
                TogglePause();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Enter && !GetTree().Paused)
                _gameState?.EndTurn();
        }
    }

    // ── Pause ─────────────────────────────────────────────────────────────────
    private void TogglePause()
    {
        bool p = !GetTree().Paused;
        GetTree().Paused = p;
        _pausePanel!.Visible = p;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildHud()
    {
        var bg = new Panel { CustomMinimumSize = new Vector2(214, 0) };
        bg.AnchorLeft = 0; bg.AnchorTop = 0; bg.AnchorRight = 0; bg.AnchorBottom = 0;
        bg.OffsetLeft = 8; bg.OffsetTop = 8; bg.OffsetRight = 222; bg.OffsetBottom = 298;
        AddChild(bg);

        var vb = new VBoxContainer();
        vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation", 4);
        bg.AddChild(vb);

        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _turnLabel  = Lbl("Turn  —",  14, true);
        _phaseLabel = Lbl("Phase: —", 12, false);
        vb.AddChild(_turnLabel);
        vb.AddChild(_phaseLabel);
        vb.AddChild(new HSeparator());

        _endTurnButton = new Button { Text = "End Turn  [Enter]" };
        _endTurnButton.Pressed += () => _gameState?.EndTurn();
        vb.AddChild(_endTurnButton);
        vb.AddChild(new HSeparator());
        vb.AddChild(Lbl("Battle Log", 12, true));

        _logScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _logScroll.CustomMinimumSize = new Vector2(0, 150);
        _logScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vb.AddChild(_logScroll);

        _logLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _logLabel.AddThemeFontSizeOverride("font_size", 11);
        _logLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _logLabel.CustomMinimumSize   = new Vector2(198, 0);
        _logScroll.AddChild(_logLabel);
    }

    private void BuildUnitInfoPanel()
    {
        _unitPanel = new Panel { Visible = false };
        _unitPanel.AnchorLeft  = 0; _unitPanel.AnchorTop    = 1;
        _unitPanel.AnchorRight = 0; _unitPanel.AnchorBottom = 1;
        _unitPanel.OffsetLeft  = 8; _unitPanel.OffsetTop    = -124;
        _unitPanel.OffsetRight = 340; _unitPanel.OffsetBottom = -8;
        AddChild(_unitPanel);

        var vb = new VBoxContainer();
        vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation", 3);
        _unitPanel.AddChild(vb);

        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _unitHeader  = Lbl("",  11, false);
        _unitName    = Lbl("",  15, true);
        _unitHp      = Lbl("",  12, false);
        _unitStats   = Lbl("",  11, false);
        _unitActions = Lbl("",  11, false);
        _unitBlocked = Lbl("",  11, false);
        _unitBlocked.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.2f));
        vb.AddChild(_unitHeader); vb.AddChild(_unitName); vb.AddChild(_unitHp);
        vb.AddChild(_unitStats);  vb.AddChild(_unitActions); vb.AddChild(_unitBlocked);
    }

    private void BuildGameOverPanel()
    {
        _gameOverPanel = new Panel { Visible = false };
        _gameOverPanel.AnchorLeft  = 0.5f; _gameOverPanel.AnchorRight  = 0.5f;
        _gameOverPanel.AnchorTop   = 0.5f; _gameOverPanel.AnchorBottom = 0.5f;
        _gameOverPanel.OffsetLeft  = -190; _gameOverPanel.OffsetRight  =  190;
        _gameOverPanel.OffsetTop   = -110; _gameOverPanel.OffsetBottom =  110;
        AddChild(_gameOverPanel);

        var vb = new VBoxContainer();
        vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation", 12);
        _gameOverPanel.AddChild(vb);

        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        _gameOverLabel = Lbl("", 26, true);
        _gameOverLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vb.AddChild(_gameOverLabel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12);
        vb.AddChild(row);

        var again = MenuBtn("Play Again");
        again.Pressed += () => _router!.RestartBattle();
        row.AddChild(again);

        var menu = MenuBtn("Level Select");
        menu.Pressed += () => _router!.GoToMenu();
        row.AddChild(menu);
    }

    private void BuildPausePanel()
    {
        _pausePanel = new Panel { Visible = false };
        _pausePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pausePanel.ProcessMode = ProcessModeEnum.Always;

        var dimStyle = new StyleBoxFlat();
        dimStyle.BgColor = new Color(0f, 0f, 0f, 0.55f);
        _pausePanel.AddThemeStyleboxOverride("panel", dimStyle);
        AddChild(_pausePanel);

        var card = new PanelContainer { ProcessMode = ProcessModeEnum.Always };
        card.AnchorLeft  = 0.5f; card.AnchorRight  = 0.5f;
        card.AnchorTop   = 0.5f; card.AnchorBottom = 0.5f;
        card.OffsetLeft  = -160; card.OffsetRight  =  160;
        card.OffsetTop   = -150; card.OffsetBottom =  150;
        _pausePanel.AddChild(card);

        var cardStyle = new StyleBoxFlat();
        cardStyle.BgColor = new Color(0.10f, 0.12f, 0.18f, 0.97f);
        cardStyle.SetBorderWidthAll(2);
        cardStyle.BorderColor = new Color(0.5f, 0.6f, 0.8f, 0.7f);
        cardStyle.SetCornerRadiusAll(10);
        cardStyle.SetContentMarginAll(20);
        card.AddThemeStyleboxOverride("panel", cardStyle);

        var vb = new VBoxContainer { ProcessMode = ProcessModeEnum.Always };
        vb.AddThemeConstantOverride("separation", 14);
        card.AddChild(vb);

        var title = Lbl("⏸  PAUSED", 22, true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vb.AddChild(title);

        // Show which level is paused
        var lvlName = _registry?.ActiveLevel.Name ?? "";
        var sub = Lbl(lvlName, 13, false);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.AddThemeColorOverride("font_color", new Color(0.6f, 0.65f, 0.75f));
        vb.AddChild(sub);

        var hint = Lbl("ESC to resume", 11, false);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeColorOverride("font_color", new Color(0.45f, 0.50f, 0.60f));
        vb.AddChild(hint);

        vb.AddChild(new HSeparator());

        var resume  = MenuBtn("▶  Resume");
        resume.Pressed += TogglePause;
        vb.AddChild(resume);

        var restart = MenuBtn("↺  Restart Level");
        restart.Pressed += () => _router!.RestartBattle();
        vb.AddChild(restart);

        var toMenu  = MenuBtn("☰  Level Select");
        toMenu.Pressed += () => _router!.GoToMenu();
        vb.AddChild(toMenu);
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void ShowUnitInfo(Unit? unit)
    {
        if (unit == null) { _unitPanel!.Visible = false; return; }
        _unitPanel!.Visible = true;
        bool isPlayer = unit.Team == Team.Player;

        _unitHeader!.Text = isPlayer ? "[ YOUR UNIT ]" : "[ ENEMY INFO ]";
        _unitHeader.AddThemeColorOverride("font_color",
            isPlayer ? new Color(0.4f, 0.9f, 1f) : new Color(1f, 0.5f, 0.4f));

        _unitName!.Text    = $"{unit.Name}  ({unit.Type})";
        _unitHp!.Text      = $"HP  {unit.Hp} / {unit.MaxHp}";
        _unitStats!.Text   = $"ATK {unit.Attack}   DEF {unit.Defense}   MOVE {unit.MoveRange}   RNG {unit.AttackRange}";
        _unitActions!.Text = isPlayer
            ? $"Move {(unit.HasMoved ? "✓" : "●")}    Attack {(unit.HasAttacked ? "✓" : "●")}"
            : "";

        if (isPlayer && !unit.HasMoved && _mapService != null)
            _unitBlocked!.Text = _mapService.GetReachableTiles(unit).Count == 0
                ? "⚠ Path blocked — no move tiles available!"
                : "";
        else
            _unitBlocked!.Text = "";
    }

    private void ShowGameOver(bool victory)
    {
        _gameOverPanel!.Visible = true;
        _gameOverLabel!.Text    = victory
            ? "⚔  VICTORY!\nAll enemies defeated!"
            : "💀  DEFEAT!\nAll your units were lost.";
    }

    // ── Log ───────────────────────────────────────────────────────────────────

    private const int MaxLog = 60;
    private readonly System.Collections.Generic.List<string> _logLines = new();

    private void AppendLog(string msg)
    {
        _logLines.Add(msg);
        if (_logLines.Count > MaxLog) _logLines.RemoveAt(0);
        _logLabel!.Text = string.Join("\n", _logLines);
        _needsScroll = true;
    }

    // ── Factories ─────────────────────────────────────────────────────────────

    private static Label Lbl(string text, int size, bool bold)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (bold) l.AddThemeColorOverride("font_color", Colors.White);
        return l;
    }

    private static Button MenuBtn(string text)
    {
        var b = new Button { Text = text, ProcessMode = ProcessModeEnum.Always };
        b.AddThemeFontSizeOverride("font_size", 16);
        return b;
    }
}
