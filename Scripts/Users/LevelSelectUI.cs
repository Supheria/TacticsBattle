using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: renders the level-select menu and navigates to battle scenes.
/// Demonstrates that even a menu benefits from GodotSharpDI — ILevelMenuService
/// is injected so level data is decoupled from the UI.
/// </summary>
[User]
public sealed partial class LevelSelectUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private ILevelMenuService? _menuService;

    public override partial void _Notification(int what);

    public override void _Ready() { /* UI built after DI */ }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[LevelSelectUI] DI failed."); return; }
        BuildMenu();
    }

    private void BuildMenu()
    {
        // ── Full-screen background ─────────────────────────────────────────
        var bg = new ColorRect { Color = new Color(0.08f, 0.10f, 0.16f) };
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 24);
        AddChild(root);

        // ── Top spacer ─────────────────────────────────────────────────────
        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 48) });

        // ── Title ──────────────────────────────────────────────────────────
        var title = new Label { Text = "⚔  TacticsBattle", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", new Color(1.0f, 0.88f, 0.28f));
        root.AddChild(title);

        var subtitle = new Label { Text = "GodotSharpDI Level Demo", HorizontalAlignment = HorizontalAlignment.Center };
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.65f, 0.75f));
        root.AddChild(subtitle);

        root.AddChild(new HSeparator());

        // ── Level cards ────────────────────────────────────────────────────
        var hbox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        hbox.AddThemeConstantOverride("separation", 32);
        root.AddChild(hbox);

        var levels = _menuService!.Levels;
        var cardColors = new[] {
            new Color(0.14f, 0.30f, 0.18f), // level 1 – forest green
            new Color(0.12f, 0.22f, 0.40f), // level 2 – river blue
            new Color(0.28f, 0.18f, 0.12f), // level 3 – mountain brown
        };

        for (int i = 0; i < levels.Count; i++)
        {
            var lvl   = levels[i];
            var color = i < cardColors.Length ? cardColors[i] : new Color(0.2f, 0.2f, 0.2f);
            hbox.AddChild(BuildCard(lvl, color));
        }

        // ── Bottom DI note ─────────────────────────────────────────────────
        root.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        var note = new Label
        {
            Text = "Each level has its own GodotSharpDI Scope — swap a single [Host] to run a different level.",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        note.AddThemeFontSizeOverride("font_size", 12);
        note.AddThemeColorOverride("font_color", new Color(0.45f, 0.50f, 0.60f));
        root.AddChild(note);
    }

    private Control BuildCard(LevelMenuItem lvl, Color bgColor)
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(280, 280) };

        var style = new StyleBoxFlat();
        style.BgColor        = bgColor;
        style.BorderColor    = new Color(0.6f, 0.65f, 0.75f, 0.6f);
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(10);
        style.SetContentMarginAll(20);
        card.AddThemeStyleboxOverride("panel", style);

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 10);
        card.AddChild(vb);

        // Name
        var name = new Label { Text = lvl.Name, HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 22);
        name.AddThemeColorOverride("font_color", Colors.White);
        vb.AddChild(name);

        // Difficulty
        var diff = new Label { Text = lvl.Difficulty, HorizontalAlignment = HorizontalAlignment.Center };
        diff.AddThemeFontSizeOverride("font_size", 14);
        diff.AddThemeColorOverride("font_color", new Color(1.0f, 0.85f, 0.4f));
        vb.AddChild(diff);

        vb.AddChild(new HSeparator());

        // Description
        var desc = new Label
        {
            Text             = lvl.Description,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode     = TextServer.AutowrapMode.Word,
        };
        desc.AddThemeFontSizeOverride("font_size", 13);
        desc.AddThemeColorOverride("font_color", new Color(0.80f, 0.82f, 0.86f));
        desc.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vb.AddChild(desc);

        // Play button
        var btn = new Button { Text = "▶  Play" };
        btn.AddThemeFontSizeOverride("font_size", 18);
        var scenePath = lvl.ScenePath;
        btn.Pressed += () => GetTree().ChangeSceneToFile(scenePath);
        vb.AddChild(btn);

        return card;
    }
}
