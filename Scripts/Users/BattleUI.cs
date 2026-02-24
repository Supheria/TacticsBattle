using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: provides the in-game HUD.
/// Injects IGameStateService and IBattleService to react to game events.
/// </summary>
[User]
public sealed partial class BattleUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IBattleService?    _battleService;

    // ── UI nodes (wired in scene) ──
    private Label?  _turnLabel;
    private Label?  _phaseLabel;
    private Label?  _logLabel;
    private Button? _endTurnButton;
    private Panel?  _gameOverPanel;
    private Label?  _gameOverLabel;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        GD.Print("[BattleUI] _Ready — building UI nodes programmatically.");
        BuildUI();
    }

    void IDependenciesResolved.OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (!isAllDependenciesReady)
        {
            GD.PrintErr("[BattleUI] DI failed — UI will not update.");
            return;
        }

        GD.Print("[BattleUI] DI ready — subscribing to game events.");

        _gameState!.OnPhaseChanged += phase =>
        {
            _phaseLabel!.Text = $"Phase: {phase}";
            _endTurnButton!.Disabled = phase != Services.GamePhase.PlayerTurn;
            AppendLog($"Phase changed → {phase}");

            if (phase is Services.GamePhase.Victory or Services.GamePhase.Defeat)
                ShowGameOver(phase == Services.GamePhase.Victory);
        };

        _gameState.OnTurnStarted += turn =>
        {
            _turnLabel!.Text = $"Turn: {turn}";
            AppendLog($"--- Turn {turn} begins ---");
        };

        _battleService!.OnAttackExecuted += (attacker, defender, dmg) =>
            AppendLog($"{attacker.Name} hit {defender.Name} for {dmg} dmg");

        _battleService.OnUnitDefeated += unit =>
            AppendLog($"★ {unit.Name} was defeated!");
    }

    // ──────────────────────────────────────────────────────
    //  UI Construction
    // ──────────────────────────────────────────────────────
    private void BuildUI()
    {
        var root = new VBoxContainer { AnchorLeft = 0, AnchorTop = 0 };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        root.CustomMinimumSize = new Vector2(220, 0);
        AddChild(root);

        _turnLabel  = new Label { Text = "Turn: —" };
        _phaseLabel = new Label { Text = "Phase: —" };
        root.AddChild(_turnLabel);
        root.AddChild(_phaseLabel);

        root.AddChild(new HSeparator());

        _endTurnButton = new Button { Text = "End Turn", Disabled = true };
        _endTurnButton.Pressed += OnEndTurnPressed;
        root.AddChild(_endTurnButton);

        root.AddChild(new HSeparator());

        var logTitle = new Label { Text = "Battle Log:" };
        root.AddChild(logTitle);

        _logLabel = new Label
        {
            AutowrapMode    = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 160),
        };
        root.AddChild(_logLabel);

        // Game-over overlay
        _gameOverPanel = new Panel { Visible = false };
        _gameOverPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _gameOverPanel.CustomMinimumSize = new Vector2(300, 120);
        AddChild(_gameOverPanel);

        _gameOverLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        _gameOverLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _gameOverPanel.AddChild(_gameOverLabel);
    }

    private void OnEndTurnPressed()
    {
        GD.Print("[BattleUI] End Turn pressed.");
        _gameState?.EndTurn();
    }

    private void ShowGameOver(bool victory)
    {
        _gameOverPanel!.Visible  = true;
        _gameOverLabel!.Text = victory ? "VICTORY!\nAll enemies defeated!" : "DEFEAT!\nAll your units were lost.";
    }

    private static readonly int MaxLogLines = 8;
    private readonly System.Collections.Generic.Queue<string> _logLines = new();

    private void AppendLog(string msg)
    {
        _logLines.Enqueue(msg);
        if (_logLines.Count > MaxLogLines) _logLines.Dequeue();
        _logLabel!.Text = string.Join("\n", _logLines);
    }
}
