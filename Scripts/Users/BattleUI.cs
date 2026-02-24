using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User — 2D HUD (CanvasLayer).
///
/// Changes:
///  • Pause menu: ESC toggles; uses GetTree().Paused so all other nodes freeze.
///    ProcessMode = Always keeps this layer responsive while paused.
///  • Movement-blocked indicator: unit info panel shows "⚠ No move tiles!" when
///    a player unit has not yet moved but GetReachableTiles returns empty
///    (surrounded or blocked by enemies at a choke-point).
///  • End Turn button sync fixed: state read immediately after DI resolves.
///  • Battle Log uses ScrollContainer with auto-scroll.
///  • Game-over overlay has Play Again + Level Select buttons.
/// </summary>
[User]
public sealed partial class BattleUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IBattleService?    _battleService;
    [Inject] private IMapService?       _mapService;

    // ── HUD ──────────────────────────────────────────────────────────────────
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
    private Label? _unitBlocked;   // "⚠ No move tiles!" when surrounded

    // ── Game-over overlay ─────────────────────────────────────────────────────
    private Panel? _gameOverPanel;
    private Label? _gameOverLabel;

    // ── Pause overlay ─────────────────────────────────────────────────────────
    private Panel? _pausePanel;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        // This layer must keep processing while the scene tree is paused
        // so the ESC key can resume and buttons still work.
        ProcessMode = ProcessModeEnum.Always;

        BuildHud();
        BuildUnitInfoPanel();
        BuildGameOverPanel();
        BuildPausePanel();
    }

    // ── Process: deferred log auto-scroll ─────────────────────────────────────
    public override void _Process(double _delta)
    {
        if (!_needsScroll || _logScroll == null) return;
        _logScroll.ScrollVertical = (int)(_logScroll.GetVScrollBar()?.MaxValue ?? 999999f);
        _needsScroll = false;
    }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[BattleUI] DI failed."); return; }

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

        // Sync state immediately — BeginPlayerTurn may have fired before we subscribed
        _endTurnButton!.Disabled = _gameState.Phase != GamePhase.PlayerTurn;
        _phaseLabel!.Text        = $"Phase: {_gameState.Phase}";
        _turnLabel!.Text         = $"Turn  {_gameState.CurrentTurn}";
    }

    // ── Input: ESC = pause, Enter = end turn ──────────────────────────────────
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
            {
                _gameState?.EndTurn();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pause logic
    // ─────────────────────────────────────────────────────────────────────────

    private void TogglePause()
    {
        bool nowPaused        = !GetTree().Paused;
        GetTree().Paused      = nowPaused;
        _pausePanel!.Visible  = nowPaused;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI construction
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildHud()
    {
        var bg = new Panel { CustomMinimumSize = new Vector2(214, 0) };
        bg.AnchorLeft  = 0f; bg.AnchorRight  = 0f;
        bg.AnchorTop   = 0f; bg.AnchorBottom = 0f;
        bg.OffsetLeft  = 8f; bg.OffsetTop    = 8f;
        bg.OffsetRight = 222f; bg.OffsetBottom = 298f;
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 4);
        bg.AddChild(vbox);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _turnLabel  = Lbl("Turn  —",  14, bold: true);
        _phaseLabel = Lbl("Phase: —", 12, bold: false);
        vbox.AddChild(_turnLabel);
        vbox.AddChild(_phaseLabel);
        vbox.AddChild(new HSeparator());

        _endTurnButton = new Button { Text = "End Turn  [Enter]", Disabled = false };
        _endTurnButton.Pressed += () => _gameState?.EndTurn();
        vbox.AddChild(_endTurnButton);
        vbox.AddChild(new HSeparator());
        vbox.AddChild(Lbl("Battle Log", 12, bold: true));

        _logScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _logScroll.CustomMinimumSize  = new Vector2(0, 150);
        _logScroll.SizeFlagsVertical  = Control.SizeFlags.ExpandFill;
        vbox.AddChild(_logScroll);

        _logLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _logLabel.AddThemeFontSizeOverride("font_size", 11);
        _logLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _logLabel.CustomMinimumSize   = new Vector2(198, 0);
        _logScroll.AddChild(_logLabel);
    }

    private void BuildUnitInfoPanel()
    {
        _unitPanel = new Panel { Visible = false };
        _unitPanel.AnchorLeft   = 0f;  _unitPanel.AnchorTop    = 1f;
        _unitPanel.AnchorRight  = 0f;  _unitPanel.AnchorBottom = 1f;
        _unitPanel.OffsetLeft   = 8f;  _unitPanel.OffsetTop    = -120f;
        _unitPanel.OffsetRight  = 336f; _unitPanel.OffsetBottom = -8f;
        AddChild(_unitPanel);

        var vb = new VBoxContainer();
        vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation", 3);
        _unitPanel.AddChild(vb);

        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _unitHeader  = Lbl("", 11, bold: false);
        _unitName    = Lbl("", 15, bold: true);
        _unitHp      = Lbl("", 12, bold: false);
        _unitStats   = Lbl("", 11, bold: false);
        _unitActions = Lbl("", 11, bold: false);
        _unitBlocked = Lbl("", 11, bold: false);
        _unitBlocked.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.2f));
        vb.AddChild(_unitHeader);
        vb.AddChild(_unitName);
        vb.AddChild(_unitHp);
        vb.AddChild(_unitStats);
        vb.AddChild(_unitActions);
        vb.AddChild(_unitBlocked);
    }

    private void BuildGameOverPanel()
    {
        _gameOverPanel = new Panel { Visible = false };
        _gameOverPanel.AnchorLeft  = 0.5f; _gameOverPanel.AnchorRight  = 0.5f;
        _gameOverPanel.AnchorTop   = 0.5f; _gameOverPanel.AnchorBottom = 0.5f;
        _gameOverPanel.OffsetLeft  = -190f; _gameOverPanel.OffsetRight  =  190f;
        _gameOverPanel.OffsetTop   = -110f; _gameOverPanel.OffsetBottom =  110f;
        AddChild(_gameOverPanel);

        var vb = new VBoxContainer();
        vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation", 12);
        _gameOverPanel.AddChild(vb);

        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        _gameOverLabel = Lbl("", 26, bold: true);
        _gameOverLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vb.AddChild(_gameOverLabel);

        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 12);
        vb.AddChild(btnRow);

        var again = new Button { Text = "Play Again" };
        again.AddThemeFontSizeOverride("font_size", 16);
        again.Pressed += () => { GetTree().Paused = false; GetTree().ReloadCurrentScene(); };
        btnRow.AddChild(again);

        var menu = new Button { Text = "Level Select" };
        menu.AddThemeFontSizeOverride("font_size", 16);
        menu.Pressed += () => { GetTree().Paused = false; GetTree().ChangeSceneToFile("res://Scenes/LevelSelectScene.tscn"); };
        btnRow.AddChild(menu);
    }

    private void BuildPausePanel()
    {
        // Full-screen semi-transparent dim layer
        _pausePanel = new Panel { Visible = false };
        _pausePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pausePanel.ProcessMode = ProcessModeEnum.Always;

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0f, 0f, 0f, 0.55f);
        _pausePanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_pausePanel);

        // Centred settings card
        var card = new PanelContainer();
        card.ProcessMode = ProcessModeEnum.Always;
        card.AnchorLeft  = 0.5f; card.AnchorRight  = 0.5f;
        card.AnchorTop   = 0.5f; card.AnchorBottom = 0.5f;
        card.OffsetLeft  = -160f; card.OffsetRight  =  160f;
        card.OffsetTop   = -140f; card.OffsetBottom =  140f;
        _pausePanel.AddChild(card);

        var cardStyle = new StyleBoxFlat();
        cardStyle.BgColor = new Color(0.10f, 0.12f, 0.18f, 0.97f);
        cardStyle.SetBorderWidthAll(2);
        cardStyle.BorderColor = new Color(0.5f, 0.6f, 0.8f, 0.7f);
        cardStyle.SetCornerRadiusAll(10);
        cardStyle.SetContentMarginAll(20);
        card.AddThemeStyleboxOverride("panel", cardStyle);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        card.AddChild(vb);

        // Title
        var title = Lbl("⏸  PAUSED", 22, bold: true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vb.AddChild(title);

        var hint = Lbl("Press ESC to resume", 12, bold: false);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.65f, 0.75f));
        vb.AddChild(hint);

        vb.AddChild(new HSeparator());

        var resume = MakeMenuBtn("▶  Resume");
        resume.Pressed += TogglePause;
        vb.AddChild(resume);

        var restart = MakeMenuBtn("↺  Restart Level");
        restart.Pressed += () => { GetTree().Paused = false; GetTree().ReloadCurrentScene(); };
        vb.AddChild(restart);

        var lvlSelect = MakeMenuBtn("☰  Level Select");
        lvlSelect.Pressed += () =>
        {
            GetTree().Paused = false;
            GetTree().ChangeSceneToFile("res://Scenes/LevelSelectScene.tscn");
        };
        vb.AddChild(lvlSelect);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unit info / selection display
    // ─────────────────────────────────────────────────────────────────────────

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

        // Movement-blocked indicator (only meaningful for own units that haven't moved)
        if (isPlayer && !unit.HasMoved && _mapService != null)
        {
            var reach = _mapService.GetReachableTiles(unit);
            _unitBlocked!.Text = reach.Count == 0
                ? "⚠ No move tiles — path blocked by enemies!"
                : "";
        }
        else
        {
            _unitBlocked!.Text = "";
        }
    }

    private void ShowGameOver(bool victory)
    {
        _gameOverPanel!.Visible = true;
        _gameOverLabel!.Text    = victory
            ? "⚔  VICTORY!\nAll enemies defeated!"
            : "💀  DEFEAT!\nAll your units were lost.";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Log helpers
    // ─────────────────────────────────────────────────────────────────────────

    private const int MaxLog = 60;
    private readonly System.Collections.Generic.List<string> _logLines = new();

    private void AppendLog(string msg)
    {
        _logLines.Add(msg);
        if (_logLines.Count > MaxLog) _logLines.RemoveAt(0);
        _logLabel!.Text = string.Join("\n", _logLines);
        _needsScroll = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shared factory helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Label Lbl(string text, int size, bool bold)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (bold) l.AddThemeColorOverride("font_color", Colors.White);
        return l;
    }

    private static Button MakeMenuBtn(string text)
    {
        var b = new Button { Text = text };
        b.AddThemeFontSizeOverride("font_size", 16);
        b.ProcessMode = ProcessModeEnum.Always;
        return b;
    }
}
