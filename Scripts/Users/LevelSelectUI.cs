using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: renders the level-select menu and delegates navigation to
/// ISceneRouterService — no scene path strings anywhere in this file.
///
/// Data comes from ILevelRegistryService → LevelRegistry (static, pure).
/// UI layout comes from LevelDefinition fields (pure records).
/// Navigation is ISceneRouterService.GoToBattle(index) — one call.
/// </summary>
[User]
public sealed partial class LevelSelectUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private ILevelRegistryService? _registry;
    [Inject] private ISceneRouterService?   _router;
    [Inject] private TacticsBattle.Audio.IAudioService? _audio;

    public override partial void _Notification(int what);
    public override void _Ready() { /* built after DI */ }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[LevelSelectUI] DI failed."); return; }
        BuildMenu();
    }

    private void BuildMenu()
    {
        var bg = new ColorRect { Color = new Color(0.08f, 0.10f, 0.16f) };
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 24);
        AddChild(root);

        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 48) });

        var title = new Label { Text = "⚔  TacticsBattle", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.28f));
        root.AddChild(title);

        var sub = new Label { Text = "GodotSharpDI — Data / Service / System Demo", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 15);
        sub.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.72f));
        root.AddChild(sub);

        root.AddChild(new HSeparator());

        var cards = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        cards.AddThemeConstantOverride("separation", 28);
        root.AddChild(cards);

        // Card colours cycle by level index
        Color[] palette = {
            new(0.14f, 0.30f, 0.18f),  // forest green
            new(0.12f, 0.20f, 0.38f),  // river blue
            new(0.28f, 0.18f, 0.12f),  // mountain brown
        };

        foreach (var level in _registry!.AllLevels)
        {
            var bg2 = palette[level.Index % palette.Length];
            cards.AddChild(BuildCard(level, bg2));
        }

        // Architecture note at bottom
        var note = new Label
        {
            Text = "One BattleScene.tscn · One BattleScope · LevelRegistry.cs is the only file to edit when adding levels",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        note.AddThemeFontSizeOverride("font_size", 12);
        note.AddThemeColorOverride("font_color", new Color(0.40f, 0.45f, 0.55f));
        root.AddChild(note);
    }

    private Control BuildCard(LevelDefinition lvl, Color bgColor)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(280, 290) };
        var style = new StyleBoxFlat();
        style.BgColor = bgColor;
        style.BorderColor = new Color(0.6f, 0.65f, 0.75f, 0.6f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(20);
        card.AddThemeStyleboxOverride("panel", style);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 10);
        card.AddChild(vb);

        var name = new Label { Text = lvl.Name, HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 22);
        name.AddThemeColorOverride("font_color", Colors.White);
        vb.AddChild(name);

        var diff = new Label { Text = lvl.Difficulty, HorizontalAlignment = HorizontalAlignment.Center };
        diff.AddThemeFontSizeOverride("font_size", 14);
        diff.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
        vb.AddChild(diff);

        vb.AddChild(new HSeparator());

        var desc = new Label
        {
            Text = lvl.Description,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Word,
        };
        desc.AddThemeFontSizeOverride("font_size", 13);
        desc.AddThemeColorOverride("font_color", new Color(0.80f, 0.82f, 0.86f));
        desc.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vb.AddChild(desc);

        // Map size badge
        var badge = new Label
        {
            Text = $"Map {lvl.MapWidth}×{lvl.MapHeight}  ·  {CountTeam(lvl, Models.Team.Player)}v{CountTeam(lvl, Models.Team.Enemy)}",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        badge.AddThemeFontSizeOverride("font_size", 12);
        badge.AddThemeColorOverride("font_color", new Color(0.55f, 0.80f, 0.95f));
        vb.AddChild(badge);

        // Play button — calls router, not GetTree() directly
        var idx = lvl.Index;
        var btn = new Button { Text = "▶  Play" };
        btn.AddThemeFontSizeOverride("font_size", 18);
        btn.Pressed += () => { _audio?.PlaySfx(TacticsBattle.Audio.SfxEvent.UiClick); _router!.GoToBattle(idx); };
        vb.AddChild(btn);

        return card;
    }

    private static int CountTeam(LevelDefinition lvl, Models.Team team)
    {
        int n = 0;
        foreach (var u in lvl.Units) if (u.Team == team) n++;
        return n;
    }
}
