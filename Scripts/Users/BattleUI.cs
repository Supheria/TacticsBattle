using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;
using TacticsBattle.Models;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: heads-up display overlay (CanvasLayer).
/// - Top-left panel: Turn, Phase, End Turn button, battle log
/// - Bottom panel:   Selected unit stats (name / HP / ATK / DEF / actions left)
/// </summary>
[User]
public sealed partial class BattleUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IBattleService?    _battleService;

    // ── Top-left HUD ──────────────────────────────────────────────────────────
    private Label?  _turnLabel;
    private Label?  _phaseLabel;
    private Label?  _logLabel;
    private Button? _endTurnButton;

    // ── Bottom unit-info panel ─────────────────────────────────────────────────
    private Panel?  _unitInfoPanel;
    private Label?  _unitNameLabel;
    private Label?  _unitHpLabel;
    private Label?  _unitStatsLabel;
    private Label?  _unitActionsLabel;

    // ── Game-over overlay ─────────────────────────────────────────────────────
    private Panel?  _gameOverPanel;
    private Label?  _gameOverLabel;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        GD.Print("[BattleUI] _Ready — building UI.");
        BuildUI();
    }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[BattleUI] DI failed."); return; }

        _gameState!.OnPhaseChanged += phase =>
        {
            _phaseLabel!.Text     = $"Phase: {phase}";
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

        _gameState.OnSelectionChanged += unit => UpdateUnitInfoPanel(unit);

        _battleService!.OnAttackExecuted += (atk, def, dmg) =>
            AppendLog($"{atk.Name} → {def.Name}  -{dmg} HP");

        _battleService.OnUnitDefeated += unit =>
            AppendLog($"☠ {unit.Name} defeated!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UI construction
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        BuildHudPanel();
        BuildUnitInfoPanel();
        BuildGameOverPanel();
    }

    private void BuildHudPanel()
    {
        var bg = new Panel();
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        bg.CustomMinimumSize = new Vector2(210, 260);
        bg.Position = new Vector2(8, 8);
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 4);
        bg.AddChild(vbox);

        // Padding
        var pad = new Control { CustomMinimumSize = new Vector2(0, 4) };
        vbox.AddChild(pad);

        _turnLabel  = new Label { Text = "Turn  —" };
        _phaseLabel = new Label { Text = "Phase: —" };
        StyleLabel(_turnLabel,  16, true);
        StyleLabel(_phaseLabel, 13, false);
        vbox.AddChild(_turnLabel);
        vbox.AddChild(_phaseLabel);

        vbox.AddChild(new HSeparator());

        _endTurnButton = new Button { Text = "End Turn  [Enter]", Disabled = true };
        _endTurnButton.Pressed += () => _gameState?.EndTurn();
        vbox.AddChild(_endTurnButton);

        vbox.AddChild(new HSeparator());
        vbox.AddChild(new Label { Text = "Battle Log" });

        _logLabel = new Label
        {
            AutowrapMode      = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 120),
        };
        StyleLabel(_logLabel, 11, false);
        vbox.AddChild(_logLabel);
    }

    private void BuildUnitInfoPanel()
    {
        _unitInfoPanel = new Panel { Visible = false };
        _unitInfoPanel.CustomMinimumSize = new Vector2(280, 90);
        _unitInfoPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        _unitInfoPanel.Position = new Vector2(8, -100);
        AddChild(_unitInfoPanel);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 12);
        _unitInfoPanel.AddChild(hbox);

        // Left: name + HP
        var leftBox = new VBoxContainer();
        leftBox.CustomMinimumSize = new Vector2(130, 0);
        _unitNameLabel  = new Label();
        _unitHpLabel    = new Label();
        StyleLabel(_unitNameLabel,  14, true);
        StyleLabel(_unitHpLabel,    12, false);
        leftBox.AddChild(_unitNameLabel);
        leftBox.AddChild(_unitHpLabel);
        hbox.AddChild(leftBox);

        // Right: stats
        var rightBox = new VBoxContainer();
        _unitStatsLabel   = new Label();
        _unitActionsLabel = new Label();
        StyleLabel(_unitStatsLabel,   11, false);
        StyleLabel(_unitActionsLabel, 11, false);
        rightBox.AddChild(_unitStatsLabel);
        rightBox.AddChild(_unitActionsLabel);
        hbox.AddChild(rightBox);
    }

    private void BuildGameOverPanel()
    {
        _gameOverPanel = new Panel { Visible = false };
        _gameOverPanel.CustomMinimumSize = new Vector2(360, 130);
        _gameOverPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddChild(_gameOverPanel);

        _gameOverLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        StyleLabel(_gameOverLabel, 24, true);
        _gameOverLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _gameOverPanel.AddChild(_gameOverLabel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Updates
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateUnitInfoPanel(Unit? unit)
    {
        if (unit == null)
        {
            _unitInfoPanel!.Visible = false;
            return;
        }

        _unitInfoPanel!.Visible = true;
        _unitNameLabel!.Text  = $"{unit.Name}  [{unit.Team}·{unit.Type}]";
        _unitHpLabel!.Text    = $"HP  {unit.Hp} / {unit.MaxHp}";
        _unitStatsLabel!.Text = $"ATK {unit.Attack}   DEF {unit.Defense}\nMOVE {unit.MoveRange}   RNG {unit.AttackRange}";
        var moved  = unit.HasMoved    ? "✓" : "●";
        var atked  = unit.HasAttacked ? "✓" : "●";
        _unitActionsLabel!.Text = $"Move {moved}   Attack {atked}";
    }

    private void ShowGameOver(bool victory)
    {
        _gameOverPanel!.Visible = true;
        _gameOverLabel!.Text    = victory
            ? "VICTORY!\nAll enemies defeated!"
            : "DEFEAT!\nAll your units were lost.";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Keyboard shortcut: Enter = End Turn
    // ─────────────────────────────────────────────────────────────────────────

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey k && k.Pressed && k.Keycode == Key.Enter)
            _gameState?.EndTurn();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void StyleLabel(Label l, int size, bool bold)
    {
        l.AddThemeFontSizeOverride("font_size", size);
        if (bold) l.AddThemeColorOverride("font_color", Colors.White);
    }

    private readonly System.Collections.Generic.Queue<string> _logLines = new();
    private const int MaxLog = 9;

    private void AppendLog(string msg)
    {
        _logLines.Enqueue(msg);
        if (_logLines.Count > MaxLog) _logLines.Dequeue();
        _logLabel!.Text = string.Join("\n", _logLines);
    }
}
