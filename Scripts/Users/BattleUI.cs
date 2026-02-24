using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Audio;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

[User]
public sealed partial class BattleUI : CanvasLayer, IDependenciesResolved
{
    [Inject] private IGameStateService?     _gs;
    [Inject] private IBattleService?        _battle;
    [Inject] private IMapService?           _map;
    [Inject] private ISceneRouterService?   _router;
    [Inject] private ILevelRegistryService? _registry;
    [Inject] private IAudioService?         _audio;

    private Label?         _turnLabel, _phaseLabel;
    private Button?        _endTurnButton;
    private ScrollContainer? _logScroll;
    private Label?         _logLabel;
    private bool           _needsScroll;

    private Panel?        _unitPanel;
    private Label?        _unitHeader, _unitName, _unitHp, _unitStats, _unitActions, _unitBlocked;
    private VBoxContainer? _componentList;

    private Panel? _gameOverPanel;
    private Label? _gameOverLabel;
    private Panel? _pausePanel;
    private Label? _pauseLevelName;

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
        if (_needsScroll && _logScroll != null)
        {
            _logScroll.ScrollVertical = (int)(_logScroll.GetVScrollBar()?.MaxValue ?? 999999f);
            _needsScroll = false;
        }
    }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[BattleUI] DI failed."); return; }

        var lvl = _registry?.ActiveLevel;
        if (lvl != null) AppendLog($"═ {lvl.Name} — {lvl.Difficulty} ═");

        _gs!.OnPhaseChanged += phase =>
        {
            _phaseLabel!.Text        = $"Phase: {phase}";
            _endTurnButton!.Disabled = phase != GamePhase.PlayerTurn;
            AppendLog($"• Phase → {phase}");
            if (phase == GamePhase.Victory)  { ShowGameOver(true);  _audio?.PlaySfx(SfxEvent.Victory); _audio?.PlayBgm(BgmTrack.Victory); }
            if (phase == GamePhase.Defeat)   { ShowGameOver(false); _audio?.PlaySfx(SfxEvent.Defeat);  _audio?.PlayBgm(BgmTrack.Defeat); }
        };
        _gs.OnTurnStarted += turn =>
        {
            _turnLabel!.Text = $"Turn  {turn}";
            AppendLog($"─── Turn {turn} ───");
        };
        _gs.OnSelectionChanged += u =>
        {
            ShowUnitInfo(u);
            if (u != null) _audio?.PlaySfx(SfxEvent.UnitSelect);
        };

        _battle!.OnAttackExecuted += (a, d, dmg) =>
        {
            AppendLog($"{a.Name} → {d.Name}  -{dmg} HP");
            _audio?.PlaySfx(SfxEvent.AttackHit);
        };
        _battle.OnUnitDefeated += u =>
        {
            AppendLog($"☠ {u.Name} defeated!");
            _audio?.PlaySfx(SfxEvent.UnitDeath);
        };
        _battle.OnCounterDamage += (u, dmg) =>
        {
            AppendLog($"  ↩ Counter! {u.Name} -{dmg} HP");
            _audio?.PlaySfx(SfxEvent.AttackHit);
        };
        _battle.OnStatusApplied += (u, s) =>
        {
            AppendLog(s.TurnsRemaining <= 0
                ? $"  {u.Name}: {s.DisplayName} expired"
                : $"  {u.Name}: {s.DisplayEmoji}{s.DisplayName} ({s.TurnsRemaining}t)");
            if (s.TurnsRemaining > 0) _audio?.PlaySfx(SfxEvent.StatusApplied);
            if (_gs.SelectedUnit == u) ShowUnitInfo(u);
        };
        _battle.OnStatusTick += (u, s, dmg) =>
        {
            AppendLog($"  {s.DisplayEmoji}{u.Name} {s.DisplayName} -{dmg} HP ({u.Hp}/{u.MaxHp})");
            _audio?.PlaySfx(SfxEvent.StatusTick);
            if (_gs.SelectedUnit == u) ShowUnitInfo(u);
        };
        _battle.OnAuraHeal += (h, ally, hp) =>
        {
            AppendLog($"  ✚ {h.Name} aura → {ally.Name} +{hp} HP");
            _audio?.PlaySfx(SfxEvent.HealAura);
            if (_gs.SelectedUnit == ally) ShowUnitInfo(ally);
        };
        _battle.OnUnitPushed += (u, _) =>
        {
            _audio?.PlaySfx(SfxEvent.PushBack);
            if (_gs.SelectedUnit == u) ShowUnitInfo(u);
        };

        _endTurnButton!.Disabled = _gs.Phase != GamePhase.PlayerTurn;
        _phaseLabel!.Text        = $"Phase: {_gs.Phase}";
        _turnLabel!.Text         = $"Turn  {_gs.CurrentTurn}";
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode == Key.Escape)
            {
                _audio?.PlaySfx(SfxEvent.UiClick);
                TogglePause();
                GetViewport().SetInputAsHandled();
            }
            else if (k.Keycode == Key.Enter && !GetTree().Paused)
            {
                _audio?.PlaySfx(SfxEvent.UiClick);
                _gs?.EndTurn();
            }
        }
    }

    private void TogglePause()
    {
        bool p = !GetTree().Paused;
        GetTree().Paused = p;
        _pausePanel!.Visible = p;
    }

    // ── Unit info ─────────────────────────────────────────────────────────────

    private void ShowUnitInfo(Unit? unit)
    {
        if (unit == null) { _unitPanel!.Visible = false; return; }
        _unitPanel!.Visible = true;
        bool ip = unit.Team == Team.Player;
        _unitHeader!.Text = ip ? "[ YOUR UNIT ]" : "[ ENEMY INFO ]";
        _unitHeader.AddThemeColorOverride("font_color", ip ? new Color(0.4f,0.9f,1f) : new Color(1f,0.5f,0.4f));
        _unitName!.Text    = $"{unit.Name}  ({unit.Type})";
        _unitHp!.Text      = $"HP  {unit.Hp} / {unit.MaxHp}";
        var moveTxt = unit.EffectiveMoveRange != unit.MoveRange
            ? $"MOVE {unit.EffectiveMoveRange} (base {unit.MoveRange})"
            : $"MOVE {unit.MoveRange}";
        _unitStats!.Text   = $"ATK {unit.Attack}   DEF {unit.Defense}   {moveTxt}   RNG {unit.AttackRange}";
        _unitActions!.Text = ip ? $"Move {(unit.HasMoved?"✓":"●")}    Attack {(unit.HasAttacked?"✓":"●")}" : "";
        _unitBlocked!.Text = ip && !unit.HasMoved && (_map?.GetReachableTiles(unit).Count == 0)
            ? "⚠ Path blocked!" : "";

        foreach (Node c in _componentList!.GetChildren()) c.QueueFree();
        foreach (var comp in unit.Components)
        {
            var (icon, label, col) = CompDisplay(comp);
            if (label == "") continue;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            var ico = new Label { Text = icon }; ico.AddThemeFontSizeOverride("font_size",12); row.AddChild(ico);
            var lbl = new Label { Text = label }; lbl.AddThemeFontSizeOverride("font_size",11);
            lbl.AddThemeColorOverride("font_color", col); lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(lbl);
            _componentList.AddChild(row);
        }
    }

    private static (string,string,Color) CompDisplay(IUnitComponent c) => c switch
    {
        ArmorComponent         a => ("🛡", $"Armor  -{a.FlatReduction} dmg/hit",                  new Color(.7f,.7f,.9f)),
        MovementBonusComponent m => ("👟", $"Move +{m.BonusRange}",                               new Color(.4f,.9f,.5f)),
        PoisonOnHitComponent   p => ("☠",  $"Poison on hit  {p.DamagePerTurn}dmg×{p.Duration}t", new Color(.3f,.9f,.3f)),
        SlowOnHitComponent     s => ("❄",  $"Slow on hit  -{s.MoveReduction}mv×{s.Duration}t",   new Color(.4f,.7f,1f)),
        PushBackOnHitComponent b => ("↩",  $"Push back {b.Distance} tile(s) on hit",              new Color(1f,.7f,.3f)),
        CounterAttackComponent a => ("⚡", $"Counter  {(int)(a.DamageRatio*100)}% dmg reflected", new Color(1f,.9f,.2f)),
        ThornComponent         t => ("🌵", $"Thorn  +{t.ReflectDamage} reflect/hit",              new Color(.6f,.9f,.3f)),
        HealAuraComponent      h => ("✚",  $"Heal aura  +{h.AmountPerTurn}HP/turn r={h.Radius}", new Color(.3f,1f,.5f)),
        PoisonedComponent      p => ("☠",  $"POISONED  -{p.DamagePerTurn}/turn  {p.TurnsRemaining}t",new Color(.3f,.9f,.3f)),
        SlowedComponent        s => ("❄",  $"SLOWED  -{s.MoveReduction}mv  {s.TurnsRemaining}t",  new Color(.4f,.7f,1f)),
        _ => ("","",Colors.White),
    };

    private void ShowGameOver(bool v)
    {
        _gameOverPanel!.Visible = true;
        _gameOverLabel!.Text    = v ? "⚔  VICTORY!\nAll enemies defeated!" : "💀  DEFEAT!\nAll units lost.";
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildHud()
    {
        var bg = MkPanel(8,8,222,298); AddChild(bg);
        var vb = Vbox(bg); vb.AddThemeConstantOverride("separation",4);
        vb.AddChild(new Control{CustomMinimumSize=new Vector2(0,4)});
        _turnLabel=Lbl("Turn  —",14,true); _phaseLabel=Lbl("Phase: —",12,false);
        vb.AddChild(_turnLabel); vb.AddChild(_phaseLabel); vb.AddChild(new HSeparator());
        _endTurnButton = new Button{Text="End Turn  [Enter]"};
        _endTurnButton.Pressed += () => { _audio?.PlaySfx(SfxEvent.UiClick); _gs?.EndTurn(); };
        vb.AddChild(_endTurnButton); vb.AddChild(new HSeparator());
        vb.AddChild(Lbl("Battle Log",12,true));
        _logScroll = new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled};
        _logScroll.CustomMinimumSize=new Vector2(0,150); _logScroll.SizeFlagsVertical=Control.SizeFlags.ExpandFill;
        vb.AddChild(_logScroll);
        _logLabel=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};
        _logLabel.AddThemeFontSizeOverride("font_size",11); _logLabel.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        _logLabel.CustomMinimumSize=new Vector2(198,0); _logScroll.AddChild(_logLabel);
    }

    private void BuildUnitInfoPanel()
    {
        _unitPanel=new Panel{Visible=false};
        _unitPanel.AnchorLeft=0; _unitPanel.AnchorTop=1; _unitPanel.AnchorRight=0; _unitPanel.AnchorBottom=1;
        _unitPanel.OffsetLeft=8; _unitPanel.OffsetTop=-204; _unitPanel.OffsetRight=350; _unitPanel.OffsetBottom=-8;
        AddChild(_unitPanel);
        var vb=new VBoxContainer(); vb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        vb.AddThemeConstantOverride("separation",3); _unitPanel.AddChild(vb);
        vb.AddChild(new Control{CustomMinimumSize=new Vector2(0,4)});
        _unitHeader=Lbl("",11,false); _unitName=Lbl("",15,true); _unitHp=Lbl("",12,false);
        _unitStats=Lbl("",11,false); _unitActions=Lbl("",11,false); _unitBlocked=Lbl("",11,false);
        _unitBlocked.AddThemeColorOverride("font_color",new Color(1f,.6f,.2f));
        vb.AddChild(_unitHeader); vb.AddChild(_unitName); vb.AddChild(_unitHp);
        vb.AddChild(_unitStats); vb.AddChild(_unitActions); vb.AddChild(_unitBlocked);
        vb.AddChild(new HSeparator());
        _componentList=new VBoxContainer(); _componentList.AddThemeConstantOverride("separation",2);
        vb.AddChild(_componentList);
    }

    private void BuildGameOverPanel()
    {
        _gameOverPanel=new Panel{Visible=false};
        _gameOverPanel.AnchorLeft=.5f; _gameOverPanel.AnchorRight=.5f;
        _gameOverPanel.AnchorTop=.5f; _gameOverPanel.AnchorBottom=.5f;
        _gameOverPanel.OffsetLeft=-190; _gameOverPanel.OffsetRight=190;
        _gameOverPanel.OffsetTop=-100; _gameOverPanel.OffsetBottom=100;
        AddChild(_gameOverPanel);
        var vb=Vbox(_gameOverPanel); vb.AddThemeConstantOverride("separation",12);
        vb.AddChild(new Control{CustomMinimumSize=new Vector2(0,8)});
        _gameOverLabel=Lbl("",26,true); _gameOverLabel.HorizontalAlignment=HorizontalAlignment.Center;
        vb.AddChild(_gameOverLabel);
        var row=new HBoxContainer{Alignment=BoxContainer.AlignmentMode.Center};
        row.AddThemeConstantOverride("separation",12); vb.AddChild(row);
        var again=MBtn("Play Again"); again.Pressed+=()=>{_audio?.PlaySfx(SfxEvent.UiClick);_router!.RestartBattle();};
        var menu=MBtn("Level Select"); menu.Pressed+=()=>{_audio?.PlaySfx(SfxEvent.UiClick);_router!.GoToMenu();};
        row.AddChild(again); row.AddChild(menu);
    }

    private void BuildPausePanel()
    {
        _pausePanel=new Panel{Visible=false,ProcessMode=ProcessModeEnum.Always};
        _pausePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim=new StyleBoxFlat{BgColor=new Color(0,0,0,.55f)};
        _pausePanel.AddThemeStyleboxOverride("panel",dim); AddChild(_pausePanel);
        var card=new PanelContainer{ProcessMode=ProcessModeEnum.Always};
        card.AnchorLeft=.5f;card.AnchorRight=.5f;card.AnchorTop=.5f;card.AnchorBottom=.5f;
        card.OffsetLeft=-160;card.OffsetRight=160;card.OffsetTop=-170;card.OffsetBottom=170;
        var cs=new StyleBoxFlat{BgColor=new Color(.10f,.12f,.18f,.97f)};
        cs.SetBorderWidthAll(2);cs.BorderColor=new Color(.5f,.6f,.8f,.7f);cs.SetCornerRadiusAll(10);cs.SetContentMarginAll(20);
        card.AddThemeStyleboxOverride("panel",cs); _pausePanel.AddChild(card);
        var vb=new VBoxContainer{ProcessMode=ProcessModeEnum.Always}; vb.AddThemeConstantOverride("separation",12); card.AddChild(vb);
        var title=Lbl("⏸  PAUSED",22,true); title.HorizontalAlignment=HorizontalAlignment.Center; vb.AddChild(title);
        _pauseLevelName=Lbl("",13,false); _pauseLevelName.HorizontalAlignment=HorizontalAlignment.Center;
        _pauseLevelName.AddThemeColorOverride("font_color",new Color(.6f,.65f,.75f)); vb.AddChild(_pauseLevelName);
        vb.AddChild(new HSeparator());
        // Volume sliders
        vb.AddChild(Lbl("BGM Volume",11,false));
        var bgmSlider=new HSlider{MinValue=0,MaxValue=1,Step=0.05f,Value=0.55f};
        bgmSlider.ValueChanged+=v=>_audio?.SetBgmVolume((float)v); vb.AddChild(bgmSlider);
        vb.AddChild(Lbl("SFX Volume",11,false));
        var sfxSlider=new HSlider{MinValue=0,MaxValue=1,Step=0.05f,Value=0.80f};
        sfxSlider.ValueChanged+=v=>_audio?.SetSfxVolume((float)v); vb.AddChild(sfxSlider);
        vb.AddChild(new HSeparator());
        var resume=MBtn("▶  Resume"); resume.Pressed+=()=>{_audio?.PlaySfx(SfxEvent.UiClick);TogglePause();};
        var restart=MBtn("↺  Restart"); restart.Pressed+=()=>{_audio?.PlaySfx(SfxEvent.UiClick);_router!.RestartBattle();};
        var toMenu=MBtn("☰  Level Select"); toMenu.Pressed+=()=>{_audio?.PlaySfx(SfxEvent.UiClick);_router!.GoToMenu();};
        vb.AddChild(resume); vb.AddChild(restart); vb.AddChild(toMenu);
        // Populate level name once DI resolves (_pauseLevelName set in OnDependenciesResolved)
    }

    // ── Log ───────────────────────────────────────────────────────────────────
    private const int MaxLog=60;
    private readonly List<string> _logLines=new();
    private void AppendLog(string msg)
    {
        _logLines.Add(msg); if(_logLines.Count>MaxLog)_logLines.RemoveAt(0);
        _logLabel!.Text=string.Join("\n",_logLines); _needsScroll=true;
    }

    // ── Factories ─────────────────────────────────────────────────────────────
    private static Panel MkPanel(float l,float t,float r,float b)
    { var p=new Panel(); p.AnchorLeft=0;p.AnchorTop=0;p.AnchorRight=0;p.AnchorBottom=0;
      p.OffsetLeft=l;p.OffsetTop=t;p.OffsetRight=r;p.OffsetBottom=b; return p; }
    private static VBoxContainer Vbox(Control parent)
    { var v=new VBoxContainer(); v.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); parent.AddChild(v); return v; }
    private static Label Lbl(string txt,int sz,bool bold)
    { var l=new Label{Text=txt}; l.AddThemeFontSizeOverride("font_size",sz);
      if(bold) l.AddThemeColorOverride("font_color",Colors.White); return l; }
    private static Button MBtn(string txt)
    { var b=new Button{Text=txt,ProcessMode=ProcessModeEnum.Always}; b.AddThemeFontSizeOverride("font_size",16); return b; }
}
