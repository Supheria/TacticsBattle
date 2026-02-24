using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User — 2D HUD overlay (CanvasLayer).
///
/// Fixes in this version:
///   1. End Turn button state synced immediately after DI resolves (no missed first event).
///   2. Battle Log uses ScrollContainer; auto-scrolls to bottom on each entry.
///   3. Game-over panel has "Play Again" (reload) and "Menu" (back to level select) buttons.
///   4. Unit info panel: fixed anchors so it actually appears; shows info for any selected unit
///      (player OR enemy), with clear team/role label.
/// </summary>
[User]
public sealed partial class BattleUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IBattleService?    _battleService;

    // ── HUD ───────────────────────────────────────────────────────────────────
    private Label?           _turnLabel;
    private Label?           _phaseLabel;
    private Button?          _endTurnButton;
    private ScrollContainer? _logScroll;
    private Label?           _logLabel;
    private bool             _needsScroll;

    // ── Unit info panel ───────────────────────────────────────────────────────
    private Panel? _unitPanel;
    private Label? _unitHeader;   // "[YOUR UNIT]" or "[ENEMY INFO]"
    private Label? _unitName;
    private Label? _unitHp;
    private Label? _unitStats;
    private Label? _unitActions;

    // ── Game-over overlay ─────────────────────────────────────────────────────
    private Panel? _gameOverPanel;
    private Label? _gameOverLabel;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        BuildHud();
        BuildUnitInfoPanel();
        BuildGameOverPanel();
    }

    // ── _Process handles deferred log scroll ──────────────────────────────────
    public override void _Process(double _)
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

        // FIX #4: subscribe to selection → show info for ANY unit (player or enemy)
        _gameState.OnSelectionChanged += ShowUnitInfo;

        _battleService!.OnAttackExecuted += (atk, def, dmg) =>
            AppendLog($"{atk.Name} → {def.Name}  -{dmg} HP");

        _battleService.OnUnitDefeated += u =>
            AppendLog($"☠ {u.Name} defeated!");

        // FIX #3: sync state immediately — BeginPlayerTurn may have already fired
        _endTurnButton!.Disabled = _gameState.Phase != GamePhase.PlayerTurn;
        _phaseLabel!.Text        = $"Phase: {_gameState.Phase}";
        _turnLabel!.Text         = $"Turn  {_gameState.CurrentTurn}";
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Enter })
            _gameState?.EndTurn();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI construction
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildHud()
    {
        var bg = new Panel { CustomMinimumSize = new Vector2(214, 0) };
        bg.AnchorLeft   = 0; bg.AnchorTop    = 0;
        bg.AnchorRight  = 0; bg.AnchorBottom = 0;
        bg.OffsetLeft   = 8; bg.OffsetTop    = 8;
        bg.OffsetRight  = 222; bg.OffsetBottom = 290;
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 4);
        bg.AddChild(vbox);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        _turnLabel  = Lbl("Turn  —",  14, true);
        _phaseLabel = Lbl("Phase: —", 12, false);
        vbox.AddChild(_turnLabel);
        vbox.AddChild(_phaseLabel);
        vbox.AddChild(new HSeparator());

        // FIX #3: starts enabled; corrected in OnDependenciesResolved
        _endTurnButton = new Button { Text = "End Turn  [Enter]", Disabled = false };
        _endTurnButton.Pressed += () => _gameState?.EndTurn();
        vbox.AddChild(_endTurnButton);
        vbox.AddChild(new HSeparator());
        vbox.AddChild(Lbl("Battle Log", 12, true));

        // FIX #2: ScrollContainer for log
        _logScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _logScroll.CustomMinimumSize   = new Vector2(0, 150);
        _logScroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        vbox.AddChild(_logScroll);

        _logLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _logLabel.AddThemeFontSizeOverride("font_size", 11);
        _logLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _logLabel.CustomMinimumSize   = new Vector2(198, 0);
        _logScroll.AddChild(_logLabel);
    }

    private void BuildUnitInfoPanel()
    {
        // FIX #4: explicit anchor placement at bottom-left
        _unitPanel = new Panel { Visible = false };
        _unitPanel.AnchorLeft   = 0f;  _unitPanel.AnchorTop    = 1f;
        _unitPanel.AnchorRight  = 0f;  _unitPanel.AnchorBottom = 1f;
        _unitPanel.OffsetLeft   = 8f;  _unitPanel.OffsetTop    = -112f;
        _unitPanel.OffsetRight  = 330f; _unitPanel.OffsetBottom = -8f;
        AddChild(_unitPanel);

        var vb = new VBoxContainer();
        vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation", 3);
        _unitPanel.AddChild(vb);

        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _unitHeader  = Lbl("",  11, false); _unitHeader.AddThemeColorOverride("font_color", new Color(0.6f,0.8f,1f));
        _unitName    = Lbl("",  15, true);
        _unitHp      = Lbl("",  12, false);
        _unitStats   = Lbl("",  11, false);
        _unitActions = Lbl("",  11, false);
        vb.AddChild(_unitHeader);
        vb.AddChild(_unitName);
        vb.AddChild(_unitHp);
        vb.AddChild(_unitStats);
        vb.AddChild(_unitActions);
    }

    private void BuildGameOverPanel()
    {
        _gameOverPanel = new Panel { Visible = false };
        _gameOverPanel.AnchorLeft   = 0.5f; _gameOverPanel.AnchorTop    = 0.5f;
        _gameOverPanel.AnchorRight  = 0.5f; _gameOverPanel.AnchorBottom = 0.5f;
        _gameOverPanel.OffsetLeft   = -180f; _gameOverPanel.OffsetTop   = -100f;
        _gameOverPanel.OffsetRight  =  180f; _gameOverPanel.OffsetBottom =  100f;
        AddChild(_gameOverPanel);

        var vb = new VBoxContainer();
        vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation", 12);
        _gameOverPanel.AddChild(vb);

        vb.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _gameOverLabel = Lbl("", 26, true);
        _gameOverLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vb.AddChild(_gameOverLabel);

        // FIX #1: Restart button
        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 12);
        vb.AddChild(btnRow);

        var btnAgain = new Button { Text = "Play Again" };
        btnAgain.AddThemeFontSizeOverride("font_size", 16);
        btnAgain.Pressed += () => GetTree().ReloadCurrentScene();
        btnRow.AddChild(btnAgain);

        var btnMenu = new Button { Text = "Level Select" };
        btnMenu.AddThemeFontSizeOverride("font_size", 16);
        btnMenu.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/LevelSelectScene.tscn");
        btnRow.AddChild(btnMenu);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
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
    }

    private void ShowGameOver(bool victory)
    {
        _gameOverPanel!.Visible = true;
        _gameOverLabel!.Text    = victory
            ? "⚔  VICTORY!\nAll enemies defeated!"
            : "💀  DEFEAT!\nAll your units were lost.";
    }

    private const int MaxLog = 60;
    private readonly System.Collections.Generic.List<string> _logLines = new();

    private void AppendLog(string msg)
    {
        _logLines.Add(msg);
        if (_logLines.Count > MaxLog) _logLines.RemoveAt(0);
        _logLabel!.Text = string.Join("\n", _logLines);
        _needsScroll = true;
    }

    private static Label Lbl(string text, int size, bool bold)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (bold) l.AddThemeColorOverride("font_color", Colors.White);
        return l;
    }
}
