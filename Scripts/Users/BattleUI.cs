using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User — 2D HUD overlay (CanvasLayer).
/// Fixes applied:
///   • End Turn button enabled immediately on DI resolve when phase is PlayerTurn
///   • Battle Log uses ScrollContainer with auto-scroll
///   • Game Over panel has a "Play Again" restart button
///   • Bottom unit-info panel with readable font sizes
/// </summary>
[User]
public sealed partial class BattleUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IBattleService?    _battleService;

    // ── Top-left HUD ──────────────────────────────────────────────────────
    private Label?           _turnLabel;
    private Label?           _phaseLabel;
    private Button?          _endTurnButton;
    private Label?           _logLabel;
    private ScrollContainer? _logScroll;

    // ── Bottom unit-info panel ────────────────────────────────────────────
    private Panel? _unitInfoPanel;
    private Label? _unitNameLabel;
    private Label? _unitHpLabel;
    private Label? _unitStatsLabel;
    private Label? _unitActionsLabel;

    // ── Game-over overlay ─────────────────────────────────────────────────
    private Panel? _gameOverPanel;
    private Label? _gameOverLabel;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        BuildHudPanel();
        BuildUnitInfoPanel();
        BuildGameOverPanel();
    }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[BattleUI] DI failed."); return; }

        // ── Subscribe events ──────────────────────────────────────────────
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

        _gameState.OnSelectionChanged += UpdateUnitInfoPanel;

        _battleService!.OnAttackExecuted += (atk, def, dmg) =>
            AppendLog($"{atk.Name} → {def.Name}  -{dmg} HP");

        _battleService.OnUnitDefeated += unit =>
            AppendLog($"☠ {unit.Name} defeated!");

        // FIX #3: Sync button & labels immediately — don't wait for next event.
        // BeginPlayerTurn() may have already fired before we subscribed.
        _endTurnButton!.Disabled = _gameState.Phase != GamePhase.PlayerTurn;
        _phaseLabel!.Text        = $"Phase: {_gameState.Phase}";
        _turnLabel!.Text         = $"Turn  {_gameState.CurrentTurn}";
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Keyboard shortcut
    // ─────────────────────────────────────────────────────────────────────
    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Enter })
            _gameState?.EndTurn();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  UI construction helpers
    // ─────────────────────────────────────────────────────────────────────

    private void BuildHudPanel()
    {
        var bg = new Panel();
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        bg.CustomMinimumSize = new Vector2(220, 0);
        bg.Position          = new Vector2(8, 8);
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 4);
        bg.AddChild(vbox);

        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) }); // top padding

        _turnLabel  = MakeLabel("Turn  —",  14, bold: true);
        _phaseLabel = MakeLabel("Phase: —", 12, bold: false);
        vbox.AddChild(_turnLabel);
        vbox.AddChild(_phaseLabel);
        vbox.AddChild(new HSeparator());

        // FIX #3: Button starts enabled (will be corrected to phase state in OnDependenciesResolved)
        _endTurnButton = new Button { Text = "End Turn  [Enter]", Disabled = false };
        _endTurnButton.Pressed += () => _gameState?.EndTurn();
        vbox.AddChild(_endTurnButton);
        vbox.AddChild(new HSeparator());
        vbox.AddChild(MakeLabel("Battle Log", 12, bold: true));

        // FIX #2: wrap log in a ScrollContainer
        _logScroll = new ScrollContainer();
        _logScroll.CustomMinimumSize         = new Vector2(0, 160);
        _logScroll.HorizontalScrollMode      = ScrollContainer.ScrollMode.Disabled;
        _logScroll.SizeFlagsVertical         = Control.SizeFlags.ExpandFill;
        vbox.AddChild(_logScroll);

        _logLabel = new Label
        {
            AutowrapMode              = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize         = new Vector2(200, 0),
            SizeFlagsHorizontal       = Control.SizeFlags.ExpandFill,
        };
        _logLabel.AddThemeFontSizeOverride("font_size", 11);
        _logScroll.AddChild(_logLabel);
    }

    private void BuildUnitInfoPanel()
    {
        _unitInfoPanel = new Panel { Visible = false };
        _unitInfoPanel.CustomMinimumSize = new Vector2(300, 88);
        _unitInfoPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _unitInfoPanel.OffsetBottom = -8;
        _unitInfoPanel.OffsetLeft   = 8;
        AddChild(_unitInfoPanel);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 14);
        _unitInfoPanel.AddChild(hbox);

        var left  = new VBoxContainer { CustomMinimumSize = new Vector2(140, 0) };
        _unitNameLabel  = MakeLabel("", 14, bold: true);
        _unitHpLabel    = MakeLabel("", 12, bold: false);
        left.AddChild(_unitNameLabel);
        left.AddChild(_unitHpLabel);
        hbox.AddChild(left);

        var right = new VBoxContainer();
        _unitStatsLabel   = MakeLabel("", 11, bold: false);
        _unitActionsLabel = MakeLabel("", 11, bold: false);
        right.AddChild(_unitStatsLabel);
        right.AddChild(_unitActionsLabel);
        hbox.AddChild(right);
    }

    private void BuildGameOverPanel()
    {
        _gameOverPanel = new Panel { Visible = false };
        _gameOverPanel.CustomMinimumSize = new Vector2(340, 160);
        _gameOverPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddChild(_gameOverPanel);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 12);
        _gameOverPanel.AddChild(vbox);

        _gameOverLabel = MakeLabel("", 22, bold: true);
        _gameOverLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _gameOverLabel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        vbox.AddChild(_gameOverLabel);

        // FIX #1: Restart button
        var restartBtn = new Button { Text = "Play Again" };
        restartBtn.Pressed += () => GetTree().ReloadCurrentScene();
        vbox.AddChild(restartBtn);

        var margin = new Control { CustomMinimumSize = new Vector2(0, 8) };
        vbox.AddChild(margin);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Event handlers
    // ─────────────────────────────────────────────────────────────────────

    private void UpdateUnitInfoPanel(Unit? unit)
    {
        if (unit == null) { _unitInfoPanel!.Visible = false; return; }
        _unitInfoPanel!.Visible  = true;
        _unitNameLabel!.Text     = $"{unit.Name}  [{unit.Team} · {unit.Type}]";
        _unitHpLabel!.Text       = $"HP  {unit.Hp} / {unit.MaxHp}";
        _unitStatsLabel!.Text    = $"ATK {unit.Attack}   DEF {unit.Defense}   MOVE {unit.MoveRange}   RNG {unit.AttackRange}";
        string mv = unit.HasMoved    ? "Move ✓" : "Move ●";
        string at = unit.HasAttacked ? "  Attack ✓" : "  Attack ●";
        _unitActionsLabel!.Text  = mv + at;
    }

    private void ShowGameOver(bool victory)
    {
        _gameOverPanel!.Visible = true;
        _gameOverLabel!.Text    = victory ? "⚔  VICTORY!\nAll enemies defeated!" : "💀  DEFEAT!\nAll your units were lost.";
    }

    // ─────────────────────────────────────────────────────────────────────
    //  FIX #2: auto-scroll log to bottom after every append
    // ─────────────────────────────────────────────────────────────────────
    private const int MaxLog = 60;   // keep last 60 lines; scroll shows all
    private readonly System.Collections.Generic.List<string> _logLines = new();

    private void AppendLog(string msg)
    {
        _logLines.Add(msg);
        if (_logLines.Count > MaxLog) _logLines.RemoveAt(0);
        _logLabel!.Text = string.Join("\n", _logLines);
        // deferred so layout has recalculated before we scroll
        _logScroll!.CallDeferred(ScrollContainer.MethodName.EnsureControlVisible, _logLabel);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Shared factory
    // ─────────────────────────────────────────────────────────────────────
    private static Label MakeLabel(string text, int size, bool bold)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (bold) l.AddThemeColorOverride("font_color", Colors.White);
        return l;
    }
}
