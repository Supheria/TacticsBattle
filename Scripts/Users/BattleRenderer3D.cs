using System;
using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User — procedurally builds the 3D world and handles all mouse input.
///
/// Changes in this version:
///  • No-overlap rule: GetReachableTiles already enforces it in MapService;
///    renderer also guards before calling TryMoveSelected.
///  • BUG FIX (can't move to highlighted tile): collision shape is now
///    FULL TileSize wide — previously gaps between shapes made raycasts
///    miss tiles at the edges of highlighted zones.
///  • Unit type top-marker removed; team ring + slim body silhouette kept.
///  • Enemy unit selection: left-clicking an enemy tile selects it and
///    highlights its attack range outline in orange; BattleUI shows its info.
///  • OnSelectionChanged fires for enemies too, so BattleUI.ShowUnitInfo works.
///  • Highlight state machine strictly clears on every transition.
/// </summary>
[User]
public sealed partial class BattleRenderer3D : Node3D, IDependenciesResolved
{
    [Inject] private IGameStateService?   _gameState;
    [Inject] private IMapService?         _mapService;
    [Inject] private IBattleService?      _battleService;
    [Inject] private ILevelConfigService? _levelConfig;

    public override partial void _Notification(int what);

    // ── Sizing ────────────────────────────────────────────────────────────────
    private const float TileSize      = 1.20f;
    private const float TileH         = 0.16f;
    private const float DamageShowSec = 1.8f;

    // ── Tile colours ──────────────────────────────────────────────────────────
    private static readonly Color ColGrass    = new(0.30f, 0.60f, 0.25f);
    private static readonly Color ColForest   = new(0.12f, 0.40f, 0.14f);
    private static readonly Color ColMountain = new(0.54f, 0.50f, 0.44f);
    private static readonly Color ColWater    = new(0.16f, 0.38f, 0.76f);

    // ── Highlight colours ─────────────────────────────────────────────────────
    private static readonly Color ColSelPlayer  = new(1.00f, 0.92f, 0.08f);   // yellow – own unit selected
    private static readonly Color ColSelEnemy   = new(1.00f, 0.55f, 0.05f);   // orange – enemy unit selected
    private static readonly Color ColMoveable   = new(0.20f, 0.88f, 1.00f, 0.80f);
    private static readonly Color ColAttackable = new(1.00f, 0.18f, 0.12f, 0.85f);
    private static readonly Color ColEnemyRange = new(1.00f, 0.45f, 0.10f, 0.60f); // enemy attack range

    // ── Unit body colours ─────────────────────────────────────────────────────
    private static Color BodyColor(Team t, UnitType u) => (t, u) switch
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

    // ── Scene nodes ───────────────────────────────────────────────────────────
    private Camera3D? _camera;
    private readonly Dictionary<Vector2I, TileData>  _tiles   = new();
    private readonly Dictionary<int,      UnitVisual> _unitVis = new();

    // ── Input state ───────────────────────────────────────────────────────────
    private enum SelMode { None, PlayerUnit, EnemyUnit }
    private SelMode        _selMode     = SelMode.None;
    private List<Vector2I> _moveTiles   = new();
    private List<Unit>     _attackUnits = new();
    private List<Vector2I> _enemyRange  = new(); // highlighted when enemy is selected

    // ── Deferred build ────────────────────────────────────────────────────────
    private bool _diReady = false, _worldBuilt = false;

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[Renderer3D] DI failed."); return; }
        _diReady = true;
    }

    public override void _Process(double _delta)
    {
        if (_diReady && !_worldBuilt) { BuildWorld(); _worldBuilt = true; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  World construction
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildWorld()
    {
        SetupCamera();
        SetupLighting();
        SetupEnvironment();
        BuildTileGrid();
        foreach (var u in _gameState!.AllUnits) EnsureUnitVisual(u);
        SubscribeEvents();
    }

    private void SetupCamera()
    {
        var w = _mapService!.MapWidth;
        var h = _mapService.MapHeight;
        _camera = new Camera3D { Fov = 45f };
        _camera.Position        = new Vector3((w - 1) * TileSize * 0.5f - 0.5f, w * 1.4f, (h - 1) * TileSize * 0.5f + h * 1.0f);
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

            // BUG FIX: collision box covers FULL TileSize so every pixel of a tile is clickable.
            var col = new CollisionShape3D();
            col.Shape = new BoxShape3D { Size = new Vector3(TileSize, TileH, TileSize) };
            body.AddChild(col);

            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh { Size = new Vector3(TileSize * 0.96f, TileH, TileSize * 0.96f) };
            var baseCol = TileColor(tile.Type);
            var mat     = MakeMat(baseCol, rougher: true);
            mesh.MaterialOverride = mat;
            body.AddChild(mesh);

            AddChild(body);
            _tiles[gp] = new TileData(body, mesh, mat, baseCol);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Event subscriptions
    // ─────────────────────────────────────────────────────────────────────────

    private void SubscribeEvents()
    {
        _gameState!.OnTurnStarted += _ =>
        {
            foreach (var u in _gameState.AllUnits) EnsureUnitVisual(u);
        };

        _gameState.OnSelectionChanged += unit =>
        {
            ClearHighlights();
            if (unit == null) { _selMode = SelMode.None; return; }

            if (unit.Team == Team.Player)
            {
                _selMode = SelMode.PlayerUnit;
                HighlightTile(unit.Position, ColSelPlayer);
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
            }
            else // enemy selected for info
            {
                _selMode = SelMode.EnemyUnit;
                HighlightTile(unit.Position, ColSelEnemy);
                // Show the enemy's attack range tiles in soft orange
                _enemyRange = _mapService!.GetReachableTiles(unit);
                foreach (var t in _enemyRange) HighlightTile(t, ColEnemyRange);
                // Also show cells the enemy can attack from current position
                var atk = _mapService.GetAttackableTargets(unit);
                foreach (var a in atk) HighlightTile(a.Position, ColAttackable);
            }
        };

        _gameState.OnUnitMoved += unit =>
        {
            SyncUnitWorldPos(unit);
            if (_gameState.SelectedUnit != unit) return;

            if (unit.HasMoved && unit.HasAttacked)
            {
                _gameState.SelectedUnit = null; // triggers ClearHighlights via OnSelectionChanged
                _selMode = SelMode.None;
                return;
            }
            ClearHighlights();
            HighlightTile(unit.Position, ColSelPlayer);
            _moveTiles   = new();
            _attackUnits = unit.HasAttacked ? new() : _mapService!.GetAttackableTargets(unit);
            foreach (var e in _attackUnits) HighlightTile(e.Position, ColAttackable);
            if (_attackUnits.Count == 0) { _gameState.SelectedUnit = null; _selMode = SelMode.None; }
        };

        _gameState.OnPhaseChanged += phase =>
        {
            if (phase is GamePhase.EnemyTurn or GamePhase.PlayerTurn)
            {
                _gameState.SelectedUnit = null;
                _selMode = SelMode.None;
            }
            if (phase == GamePhase.PlayerTurn)
                foreach (var u in _gameState.AllUnits) SyncUnitWorldPos(u);
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
            _selMode = SelMode.None;
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mouse input
    // ─────────────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent ev)
    {
        if (!_worldBuilt) return;
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        if (_gameState!.Phase is GamePhase.Victory or GamePhase.Defeat) return;

        var gp = RaycastTile(mb.Position);
        if (gp.HasValue) HandleClick(gp.Value);
    }

    private Vector2I? RaycastTile(Vector2 mousePos)
    {
        if (_camera == null) return null;
        var space  = GetWorld3D().DirectSpaceState;
        var origin = _camera.ProjectRayOrigin(mousePos);
        var dest   = origin + _camera.ProjectRayNormal(mousePos) * 250f;
        var result = space.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, dest));
        if (result.Count == 0) return null;
        var col = result["collider"].As<StaticBody3D>();
        if (col == null) return null;
        return new Vector2I(col.GetMeta("gx").AsInt32(), col.GetMeta("gz").AsInt32());
    }

    private void HandleClick(Vector2I gp)
    {
        // Ignore clicks during enemy AI turn
        if (_gameState!.Phase == GamePhase.EnemyTurn) return;

        var unitMgr  = GetParent().GetNodeOrNull<UnitManager>("UnitManager");
        var clickedUnit = _mapService!.GetUnitAt(gp);

        switch (_selMode)
        {
            // ── Nothing selected ──────────────────────────────────────────────
            case SelMode.None:
                if (clickedUnit != null)
                {
                    _gameState.SelectedUnit = clickedUnit; // works for both teams
                    _selMode = clickedUnit.Team == Team.Player ? SelMode.PlayerUnit : SelMode.EnemyUnit;
                }
                break;

            // ── Player unit selected ──────────────────────────────────────────
            case SelMode.PlayerUnit:
                var sel = _gameState.SelectedUnit;
                if (sel == null) { _selMode = SelMode.None; return; }

                // Attack enemy
                var victim = _attackUnits.Find(u => u.Position == gp);
                if (victim != null)
                {
                    unitMgr?.TryAttackTarget(victim);
                    _gameState.SelectedUnit = null;
                    _selMode = SelMode.None;
                    return;
                }

                // Move to empty reachable tile
                if (_moveTiles.Contains(gp) && _mapService.GetUnitAt(gp) == null)
                {
                    unitMgr?.TryMoveSelected(gp);
                    // OnUnitMoved fires → re-evaluates remaining actions
                    return;
                }

                // Click another own unit → switch
                if (clickedUnit != null && clickedUnit.Team == Team.Player && clickedUnit != sel)
                {
                    _gameState.SelectedUnit = clickedUnit;
                    _selMode = SelMode.PlayerUnit;
                    return;
                }

                // Click an enemy when NOT in attack list → show enemy info
                if (clickedUnit != null && clickedUnit.Team == Team.Enemy)
                {
                    _gameState.SelectedUnit = clickedUnit;
                    _selMode = SelMode.EnemyUnit;
                    return;
                }

                // Click same unit or empty non-highlighted → deselect
                _gameState.SelectedUnit = null;
                _selMode = SelMode.None;
                break;

            // ── Enemy unit selected (info view) ───────────────────────────────
            case SelMode.EnemyUnit:
                if (clickedUnit != null && clickedUnit.Team == Team.Player)
                {
                    _gameState.SelectedUnit = clickedUnit;
                    _selMode = SelMode.PlayerUnit;
                }
                else if (clickedUnit != null && clickedUnit.Team == Team.Enemy && clickedUnit != _gameState.SelectedUnit)
                {
                    _gameState.SelectedUnit = clickedUnit; // switch enemy selection
                }
                else
                {
                    _gameState.SelectedUnit = null;
                    _selMode = SelMode.None;
                }
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Unit visuals (no top-marker, slim body, team ring)
    // ─────────────────────────────────────────────────────────────────────────

    private static (float w, float h, float d) BodyDims(UnitType t) => t switch
    {
        UnitType.Warrior => (0.30f, 0.66f, 0.30f),
        UnitType.Archer  => (0.22f, 0.62f, 0.22f),
        UnitType.Mage    => (0.18f, 0.80f, 0.18f),
        _                => (0.26f, 0.66f, 0.26f),
    };

    private void EnsureUnitVisual(Unit unit)
    {
        if (_unitVis.ContainsKey(unit.Id)) return;

        var root = new Node3D { Name = $"Unit_{unit.Id}" };

        // Team ring
        var ring = new MeshInstance3D
        {
            Mesh             = new CylinderMesh { TopRadius = 0.40f, BottomRadius = 0.40f, Height = 0.05f, RadialSegments = 20 },
            MaterialOverride = MakeMat(TeamRingColor(unit.Team)),
            Position         = new Vector3(0, 0.025f, 0),
        };
        root.AddChild(ring);

        // Body
        var (bw, bh, bd) = BodyDims(unit.Type);
        var body = new MeshInstance3D
        {
            Mesh             = new BoxMesh { Size = new Vector3(bw, bh, bd) },
            MaterialOverride = MakeMat(BodyColor(unit.Team, unit.Type)),
            Position         = new Vector3(0, bh * 0.5f + 0.05f, 0),
        };
        root.AddChild(body);

        // HP bar (background)
        float barY = bh + 0.05f + 0.22f;
        var barBg = MakeBarNode(new Color(0.15f, 0.15f, 0.15f), new Vector3(0.70f, 0.10f, 0.07f), barY, "HpBarBg");
        root.AddChild(barBg);
        var barFill = MakeBarNode(Colors.LimeGreen, new Vector3(0.70f, 0.10f, 0.08f), barY, "HpBarFill");
        root.AddChild(barFill);

        // Label — 30pt
        var lbl = new Label3D
        {
            Text      = unit.Name,
            FontSize  = 30,
            Modulate  = unit.Team == Team.Player ? Colors.Cyan : Colors.OrangeRed,
            Position  = new Vector3(0, barY + 0.28f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Name      = "NameLabel",
        };
        root.AddChild(lbl);

        AddChild(root);
        _unitVis[unit.Id] = new UnitVisual(root, body, barFill, lbl, unit);
        SyncUnitWorldPos(unit);
        RefreshHpBar(unit);
    }

    private static MeshInstance3D MakeBarNode(Color c, Vector3 size, float y, string name)
    {
        var inst = new MeshInstance3D
        {
            Mesh             = new BoxMesh { Size = size },
            MaterialOverride = MakeMat(c),
            Position         = new Vector3(0, y, 0),
            Name             = name,
        };
        return inst;
    }

    private void SyncUnitWorldPos(Unit unit)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        float wy = TileWorldY(unit.Position);
        vis.Root.Position = new Vector3(unit.Position.X * TileSize, wy + TileH * 0.5f, unit.Position.Y * TileSize);
    }

    private void RefreshHpBar(Unit unit)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        float ratio = Mathf.Clamp((float)unit.Hp / unit.MaxHp, 0f, 1f);
        vis.HpBarFill.Scale    = new Vector3(ratio, 1f, 1f);
        vis.HpBarFill.Position = vis.HpBarFill.Position with { X = (ratio - 1f) * 0.35f };
        ((StandardMaterial3D)vis.HpBarFill.MaterialOverride).AlbedoColor =
            ratio > 0.5f ? new Color(1f - (ratio - 0.5f) * 2f, 0.88f, 0.12f)
                         : new Color(0.88f, ratio * 2f * 0.88f, 0.05f);
        vis.Label.Text = $"{unit.Name}\n{unit.Hp}/{unit.MaxHp}";
    }

    private void ShowFloatingDamage(Unit unit, int dmg)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        var fl = new Label3D { Text = $"-{dmg}", FontSize = 36, Modulate = Colors.Yellow,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position  = vis.Root.Position + new Vector3(0, 1.4f, 0) };
        AddChild(fl);
        var t = new Timer { WaitTime = DamageShowSec, OneShot = true };
        t.Timeout += () => { fl.QueueFree(); t.QueueFree(); };
        AddChild(t); t.Start();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tile highlight helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void HighlightTile(Vector2I pos, Color col)
    {
        if (_tiles.TryGetValue(pos, out var td))
            ((StandardMaterial3D)td.Mesh.MaterialOverride).AlbedoColor = col;
    }

    private void ClearHighlights()
    {
        foreach (var (_, td) in _tiles)
            ((StandardMaterial3D)td.Mesh.MaterialOverride).AlbedoColor = td.BaseColor;
        _moveTiles = new(); _attackUnits = new(); _enemyRange = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Utilities
    // ─────────────────────────────────────────────────────────────────────────

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

    private static StandardMaterial3D MakeMat(Color c, bool rougher = false) =>
        new() { AlbedoColor = c, Roughness = rougher ? 0.85f : 0.55f, Metallic = rougher ? 0f : 0.08f };

    // ─────────────────────────────────────────────────────────────────────────
    //  Inner records
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record TileData(StaticBody3D Body, MeshInstance3D Mesh, StandardMaterial3D Mat, Color BaseColor);
    private sealed record UnitVisual(Node3D Root, MeshInstance3D Mesh, MeshInstance3D HpBarFill, Label3D Label, Unit Unit);
}
