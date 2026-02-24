using System;
using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User — builds the entire 3D world and handles mouse input.
///
/// Bugs fixed vs first version:
///   • Highlight logic: respects HasMoved / HasAttacked; auto-deselects when
///     no actions remain after a move; clears on every transition.
///   • Unit visuals: smaller footprint (0.26 f), per-team base ring, per-type
///     silhouette shape → far less visual overlap on the isometric camera.
///   • Label3D HP text now 30 pt; HP bar doubled in height.
/// </summary>
[User]
public sealed partial class BattleRenderer3D : Node3D, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IMapService?       _mapService;
    [Inject] private IBattleService?    _battleService;

    public override partial void _Notification(int what);

    // ── Layout constants ──────────────────────────────────────────────────
    private const float TileSize      = 1.20f;
    private const float TileH         = 0.16f;
    private const float DamageShowSec = 1.8f;

    // ── Tile colours ──────────────────────────────────────────────────────
    private static readonly Color ColGrass    = new(0.30f, 0.60f, 0.25f);
    private static readonly Color ColForest   = new(0.12f, 0.40f, 0.14f);
    private static readonly Color ColMountain = new(0.54f, 0.50f, 0.44f);
    private static readonly Color ColWater    = new(0.16f, 0.38f, 0.76f);

    // ── Highlight colours ─────────────────────────────────────────────────
    private static readonly Color ColSelected   = new(1.00f, 0.92f, 0.08f);
    private static readonly Color ColMoveable   = new(0.20f, 0.88f, 1.00f, 0.80f);
    private static readonly Color ColAttackable = new(1.00f, 0.18f, 0.12f, 0.85f);

    // ── Unit body colours ─────────────────────────────────────────────────
    private static Color BodyColor(Team team, UnitType type) => (team, type) switch
    {
        (Team.Player, UnitType.Warrior) => new Color(0.18f, 0.42f, 0.90f),
        (Team.Player, UnitType.Archer)  => new Color(0.08f, 0.72f, 0.82f),
        (Team.Player, UnitType.Mage)    => new Color(0.68f, 0.18f, 0.90f),
        (Team.Enemy,  UnitType.Warrior) => new Color(0.88f, 0.16f, 0.16f),
        (Team.Enemy,  UnitType.Archer)  => new Color(0.90f, 0.50f, 0.08f),
        (Team.Enemy,  UnitType.Mage)    => new Color(0.80f, 0.08f, 0.50f),
        _                               => Colors.White,
    };

    private static Color TeamRingColor(Team t) =>
        t == Team.Player ? new Color(0.25f, 0.55f, 1.00f) : new Color(1.00f, 0.30f, 0.20f);

    // ── Scene nodes ───────────────────────────────────────────────────────
    private Camera3D? _camera;
    private readonly Dictionary<Vector2I, TileData>   _tiles   = new();
    private readonly Dictionary<int,      UnitVisual>  _unitVis = new();

    // ── Input state machine ───────────────────────────────────────────────
    private enum InputState { Idle, UnitSelected }
    private InputState     _inputState  = InputState.Idle;
    private List<Vector2I> _moveTiles   = new();
    private List<Unit>     _attackUnits = new();

    // ── Deferred build ────────────────────────────────────────────────────
    private bool _diReady = false, _worldBuilt = false;

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[Renderer3D] DI failed."); return; }
        _diReady = true;
    }

    public override void _Process(double _delta)
    {
        if (!_diReady || _worldBuilt) return;
        BuildWorld();
        _worldBuilt = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  World construction
    // ─────────────────────────────────────────────────────────────────────

    private void BuildWorld()
    {
        SetupCamera();
        SetupLighting();
        SetupEnvironment();
        BuildTileGrid();
        BuildExistingUnits();
        SubscribeEvents();
    }

    private void SetupCamera()
    {
        _camera = new Camera3D { Fov = 45f };
        float cx = (_mapService!.MapWidth  - 1) * TileSize * 0.5f;
        float cz = (_mapService.MapHeight  - 1) * TileSize * 0.5f;
        _camera.Position       = new Vector3(cx - 0.5f, 12f, cz + 9f);
        _camera.RotationDegrees = new Vector3(-54f, 0f, 0f);
        AddChild(_camera);
    }

    private void SetupLighting()
    {
        var sun = new DirectionalLight3D { LightEnergy = 1.35f, ShadowEnabled = true };
        sun.RotationDegrees = new Vector3(-55f, 42f, 0f);
        AddChild(sun);
    }

    private void SetupEnvironment()
    {
        var env = new Godot.Environment();
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor  = new Color(0.55f, 0.65f, 0.80f);
        env.AmbientLightEnergy = 0.55f;
        env.BackgroundMode     = Godot.Environment.BGMode.Color;
        env.BackgroundColor    = new Color(0.35f, 0.52f, 0.80f);
        AddChild(new WorldEnvironment { Environment = env });
    }

    private void BuildTileGrid()
    {
        for (int gx = 0; gx < _mapService!.MapWidth;  gx++)
        for (int gz = 0; gz < _mapService.MapHeight; gz++)
        {
            var gp   = new Vector2I(gx, gz);
            var tile = _mapService.GetTile(gx, gz);
            float wy = TileWorldY(gp);

            var body = new StaticBody3D { Position = new Vector3(gx * TileSize, wy, gz * TileSize) };
            body.SetMeta("gx", gx);
            body.SetMeta("gz", gz);

            var col = new CollisionShape3D();
            var box = new BoxShape3D { Size = new Vector3(TileSize * 0.96f, TileH, TileSize * 0.96f) };
            col.Shape = box;
            body.AddChild(col);

            var mesh = new MeshInstance3D();
            var bm   = new BoxMesh { Size = new Vector3(TileSize * 0.96f, TileH, TileSize * 0.96f) };
            mesh.Mesh = bm;
            var baseCol = TileColor(tile.Type);
            var mat     = MakeMat(baseCol, rougher: true);
            mesh.MaterialOverride = mat;
            body.AddChild(mesh);

            AddChild(body);
            _tiles[gp] = new TileData(body, mesh, mat, baseCol);
        }
    }

    private void BuildExistingUnits()
    {
        foreach (var u in _gameState!.AllUnits)
            EnsureUnitVisual(u);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Event subscriptions
    // ─────────────────────────────────────────────────────────────────────

    private void SubscribeEvents()
    {
        // Spawn unit visuals once the first turn fires (units spawn just before)
        _gameState!.OnTurnStarted += _ =>
        {
            foreach (var u in _gameState.AllUnits) EnsureUnitVisual(u);
        };

        // FIX #5: Clear + recompute highlights respecting HasMoved / HasAttacked
        _gameState.OnSelectionChanged += unit =>
        {
            ClearHighlights();
            if (unit == null) return;

            HighlightTile(unit.Position, ColSelected);

            if (!unit.HasMoved)
            {
                _moveTiles = _mapService!.GetReachableTiles(unit);
                foreach (var t in _moveTiles) HighlightTile(t, ColMoveable);
            }
            else _moveTiles = new();

            if (!unit.HasAttacked)
            {
                _attackUnits = _mapService!.GetAttackableTargets(unit);
                foreach (var e in _attackUnits) HighlightTile(e.Position, ColAttackable);
            }
            else _attackUnits = new();
        };

        // FIX #5: After move, re-evaluate what actions remain
        _gameState.OnUnitMoved += unit =>
        {
            SyncUnitWorldPos(unit);

            if (_gameState.SelectedUnit != unit) return;

            // No actions left? Auto-deselect.
            if (unit.HasMoved && unit.HasAttacked)
            {
                _gameState.SelectedUnit = null;   // fires OnSelectionChanged(null) → ClearHighlights
                _inputState = InputState.Idle;
                return;
            }

            // Show only remaining possible actions
            ClearHighlights();
            HighlightTile(unit.Position, ColSelected);
            _moveTiles = new();  // already moved

            if (!unit.HasAttacked)
            {
                _attackUnits = _mapService!.GetAttackableTargets(unit);
                foreach (var e in _attackUnits) HighlightTile(e.Position, ColAttackable);
            }
            else _attackUnits = new();

            // No attack targets AND already moved → auto-deselect
            if (_attackUnits.Count == 0)
            {
                _gameState.SelectedUnit = null;
                _inputState = InputState.Idle;
            }
        };

        // FIX #5: Always clear on phase changes
        _gameState.OnPhaseChanged += phase =>
        {
            if (phase is GamePhase.EnemyTurn or GamePhase.PlayerTurn)
            {
                _gameState.SelectedUnit = null;   // fires ClearHighlights via OnSelectionChanged
                _inputState = InputState.Idle;
            }
            if (phase == GamePhase.PlayerTurn)
            {
                // Sync all unit positions after enemy AI has moved things
                foreach (var u in _gameState.AllUnits) SyncUnitWorldPos(u);
            }
        };

        _battleService!.OnAttackExecuted += (atk, def, dmg) =>
        {
            SyncUnitWorldPos(atk);
            RefreshHpBar(atk);
            RefreshHpBar(def);
            ShowFloatingDamage(def, dmg);
        };

        _battleService.OnUnitDefeated += unit =>
        {
            if (_unitVis.TryGetValue(unit.Id, out var vis)) { vis.Root.QueueFree(); _unitVis.Remove(unit.Id); }
            _gameState.SelectedUnit = null;
            _inputState = InputState.Idle;
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Mouse input
    // ─────────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent ev)
    {
        if (!_worldBuilt) return;
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        if (_gameState!.Phase is GamePhase.EnemyTurn or GamePhase.Victory or GamePhase.Defeat) return;

        var hit = RaycastTile(mb.Position);
        if (hit.HasValue) HandleClick(hit.Value);
    }

    private Vector2I? RaycastTile(Vector2 mousePos)
    {
        if (_camera == null) return null;
        var space  = GetWorld3D().DirectSpaceState;
        var origin = _camera.ProjectRayOrigin(mousePos);
        var dest   = origin + _camera.ProjectRayNormal(mousePos) * 220f;
        var query  = PhysicsRayQueryParameters3D.Create(origin, dest);
        var result = space.IntersectRay(query);
        if (result.Count == 0) return null;
        var col = result["collider"].As<StaticBody3D>();
        if (col == null) return null;
        return new Vector2I(col.GetMeta("gx").AsInt32(), col.GetMeta("gz").AsInt32());
    }

    private void HandleClick(Vector2I gp)
    {
        var unitMgr = GetParent().GetNodeOrNull<UnitManager>("UnitManager");

        switch (_inputState)
        {
            case InputState.Idle:
                TrySelect(gp);
                break;

            case InputState.UnitSelected:
                var sel = _gameState!.SelectedUnit;
                if (sel == null) { _inputState = InputState.Idle; return; }

                // ── Attack? ───────────────────────────────────────────────
                var victim = _attackUnits.Find(u => u.Position == gp);
                if (victim != null)
                {
                    unitMgr?.TryAttackTarget(victim);
                    // FIX #5: always clear after attack — OnSelectionChanged handles it
                    _gameState.SelectedUnit = null;
                    _inputState = InputState.Idle;
                    return;
                }

                // ── Move? ─────────────────────────────────────────────────
                if (_moveTiles.Contains(gp) && _mapService!.GetUnitAt(gp) == null)
                {
                    unitMgr?.TryMoveSelected(gp);
                    // OnUnitMoved fires → recalculates state
                    return;
                }

                // ── Tap own unit again → deselect ─────────────────────────
                if (gp == sel.Position)
                {
                    _gameState.SelectedUnit = null;
                    _inputState = InputState.Idle;
                    return;
                }

                // ── Switch to another own unit ────────────────────────────
                var other = _mapService!.GetUnitAt(gp);
                if (other != null && other.Team == Team.Player && other.IsAlive)
                {
                    _gameState.SelectedUnit = other;
                    _inputState = InputState.UnitSelected;
                    return;
                }

                // ── Click elsewhere → deselect ────────────────────────────
                _gameState.SelectedUnit = null;
                _inputState = InputState.Idle;
                break;
        }
    }

    private void TrySelect(Vector2I gp)
    {
        var unit = _mapService!.GetUnitAt(gp);
        if (unit == null || unit.Team != Team.Player || !unit.IsAlive) return;
        _gameState!.SelectedUnit = unit;
        _inputState = InputState.UnitSelected;
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Unit visuals (FIX #4 + #6)
    // ─────────────────────────────────────────────────────────────────────

    // FIX #6: per-type body dimensions — narrow footprint reduces visual overlap
    private static (float w, float h, float d) BodyDims(UnitType type) => type switch
    {
        UnitType.Warrior => (0.34f, 0.68f, 0.34f),
        UnitType.Archer  => (0.24f, 0.64f, 0.24f),
        UnitType.Mage    => (0.20f, 0.84f, 0.20f),
        _                => (0.28f, 0.68f, 0.28f),
    };

    private void EnsureUnitVisual(Unit unit)
    {
        if (_unitVis.ContainsKey(unit.Id)) return;

        var root = new Node3D { Name = $"Unit_{unit.Id}" };

        // FIX #6: Team-colour base ring (flat cylinder) — instantly shows team
        var ringMesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.06f, RadialSegments = 20 };
        var ringInst = new MeshInstance3D { Mesh = ringMesh };
        ringInst.MaterialOverride = MakeMat(TeamRingColor(unit.Team));
        ringInst.Position = new Vector3(0, 0.03f, 0);
        root.AddChild(ringInst);

        // FIX #6: per-type body shape (BoxMesh) with narrow dimensions
        var (bw, bh, bd) = BodyDims(unit.Type);
        var bodyMesh = new BoxMesh { Size = new Vector3(bw, bh, bd) };
        var bodyInst = new MeshInstance3D { Mesh = bodyMesh };
        bodyInst.MaterialOverride = MakeMat(BodyColor(unit.Team, unit.Type));
        bodyInst.Position = new Vector3(0, bh * 0.5f + 0.06f, 0);
        root.AddChild(bodyInst);

        // FIX #6: unit type indicator on top (small diamond / sphere / spike)
        var topY = bh + 0.06f + 0.12f;
        var topInst = MakeTypeTop(unit.Type, unit.Team, topY);
        root.AddChild(topInst);

        // FIX #4: HP bar — doubled height (0.10f), label 30 pt
        float barY = bh + 0.06f + 0.28f;
        var barBg = MakeBarMesh(new Color(0.15f, 0.15f, 0.15f), new Vector3(0.72f, 0.10f, 0.07f));
        barBg.Position = new Vector3(0, barY, 0);
        barBg.Name = "HpBarBg";
        root.AddChild(barBg);

        var barFill = MakeBarMesh(new Color(0.15f, 0.90f, 0.15f), new Vector3(0.72f, 0.10f, 0.08f));
        barFill.Position = new Vector3(0, barY, 0);
        barFill.Name = "HpBarFill";
        root.AddChild(barFill);

        // FIX #4: 30pt font, offset above HP bar
        var lbl = new Label3D
        {
            Text      = unit.Name,
            FontSize  = 30,
            Modulate  = unit.Team == Team.Player ? Colors.Cyan : Colors.OrangeRed,
            Position  = new Vector3(0, barY + 0.30f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Name      = "NameLabel",
        };
        root.AddChild(lbl);

        AddChild(root);
        _unitVis[unit.Id] = new UnitVisual(root, bodyInst, barFill, lbl, unit);
        SyncUnitWorldPos(unit);
        RefreshHpBar(unit);
    }

    /// <summary>Tiny type indicator placed on top of the body mesh.</summary>
    private static MeshInstance3D MakeTypeTop(UnitType type, Team team, float y)
    {
        // Warrior: flat square cap; Archer: small sphere; Mage: tall spike
        Mesh mesh = type switch
        {
            UnitType.Warrior => new BoxMesh       { Size = new Vector3(0.22f, 0.10f, 0.22f) },
            UnitType.Archer  => new SphereMesh    { Radius = 0.12f, Height = 0.24f },
            UnitType.Mage    => new CylinderMesh  { TopRadius = 0.02f, BottomRadius = 0.10f, Height = 0.24f, RadialSegments = 8 },
            _                => new SphereMesh    { Radius = 0.10f },
        };

        var inst = new MeshInstance3D { Mesh = mesh };
        // Muted contrasting accent colour
        inst.MaterialOverride = MakeMat(team == Team.Player ? new Color(0.9f, 0.95f, 1.0f) : new Color(1.0f, 0.9f, 0.6f));
        inst.Position = new Vector3(0, y, 0);
        return inst;
    }

    private static MeshInstance3D MakeBarMesh(Color c, Vector3 size)
    {
        var m = new BoxMesh { Size = size };
        var inst = new MeshInstance3D { Mesh = m };
        inst.MaterialOverride = MakeMat(c);
        return inst;
    }

    private void SyncUnitWorldPos(Unit unit)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        float wy = TileWorldY(unit.Position);
        vis.Root.Position = new Vector3(
            unit.Position.X * TileSize,
            wy + TileH * 0.5f,
            unit.Position.Y * TileSize
        );
    }

    private void RefreshHpBar(Unit unit)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        float ratio = Mathf.Clamp((float)unit.Hp / unit.MaxHp, 0f, 1f);

        vis.HpBarFill.Scale    = new Vector3(ratio, 1f, 1f);
        vis.HpBarFill.Position = vis.HpBarFill.Position with { X = (ratio - 1f) * 0.36f };

        var mat = (StandardMaterial3D)vis.HpBarFill.MaterialOverride;
        mat.AlbedoColor = ratio > 0.5f
            ? new Color(1f - (ratio - 0.5f) * 2f, 0.88f, 0.12f)
            : new Color(0.88f, ratio * 2f * 0.88f, 0.05f);

        // FIX #4: show HP numbers in the label
        vis.Label.Text = $"{unit.Name}\n{unit.Hp}/{unit.MaxHp}";
    }

    private void ShowFloatingDamage(Unit unit, int dmg)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        var fl = new Label3D
        {
            Text      = $"-{dmg}",
            FontSize  = 36,
            Modulate  = Colors.Yellow,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position  = vis.Root.Position + new Vector3(0, 1.4f, 0),
        };
        AddChild(fl);
        var t = new Timer { WaitTime = DamageShowSec, OneShot = true };
        t.Timeout += () => { fl.QueueFree(); t.QueueFree(); };
        AddChild(t);
        t.Start();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Tile highlight helpers
    // ─────────────────────────────────────────────────────────────────────

    private void HighlightTile(Vector2I pos, Color col)
    {
        if (_tiles.TryGetValue(pos, out var td))
            ((StandardMaterial3D)td.Mesh.MaterialOverride).AlbedoColor = col;
    }

    /// <summary>Reset every tile to its base colour and clear lists.</summary>
    private void ClearHighlights()
    {
        foreach (var (_, td) in _tiles)
            ((StandardMaterial3D)td.Mesh.MaterialOverride).AlbedoColor = td.BaseColor;
        _moveTiles   = new();
        _attackUnits = new();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Utilities
    // ─────────────────────────────────────────────────────────────────────

    private float TileWorldY(Vector2I gp)
    {
        if (!_mapService!.IsValidPosition(gp)) return 0f;
        return _mapService.GetTile(gp).Type switch
        {
            TileType.Water    => -0.12f,
            TileType.Mountain =>  0.10f,
            TileType.Forest   =>  0.04f,
            _                 =>  0.00f,
        };
    }

    private static Color TileColor(TileType t) => t switch
    {
        TileType.Grass    => ColGrass,
        TileType.Forest   => ColForest,
        TileType.Mountain => ColMountain,
        TileType.Water    => ColWater,
        _                 => ColGrass,
    };

    private static StandardMaterial3D MakeMat(Color c, bool rougher = false)
    {
        return new StandardMaterial3D { AlbedoColor = c, Roughness = rougher ? 0.85f : 0.55f, Metallic = rougher ? 0f : 0.08f };
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Inner record types
    // ─────────────────────────────────────────────────────────────────────

    private sealed record TileData(StaticBody3D Body, MeshInstance3D Mesh, StandardMaterial3D Mat, Color BaseColor);
    private sealed record UnitVisual(Node3D Root, MeshInstance3D Mesh, MeshInstance3D HpBarFill, Label3D Label, Unit Unit);
}
