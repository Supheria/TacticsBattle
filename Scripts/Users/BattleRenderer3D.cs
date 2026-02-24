using System;
using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: builds the entire 3D world (camera, lighting, tile grid, unit meshes)
/// and handles all player mouse input via PhysicsRaycast.
///
/// Interaction flow:
///   Idle         → click player unit   → UnitSelected (show move + attack highlights)
///   UnitSelected → click move tile     → unit moves,  re-highlight attackable targets
///   UnitSelected → click enemy unit    → unit attacks, return to Idle
///   UnitSelected → click elsewhere     → deselect, return to Idle
/// </summary>
[User]
public sealed partial class BattleRenderer3D : Node3D, IDependenciesResolved
{
    // ── DI injections ────────────────────────────────────────────────────────
    [Inject] private IGameStateService? _gameState;
    [Inject] private IMapService?       _mapService;
    [Inject] private IBattleService?    _battleService;

    public override partial void _Notification(int what);

    // ── Constants ────────────────────────────────────────────────────────────
    private const float TileSize   = 1.15f;   // world-space distance between tile centres
    private const float TileH      = 0.18f;   // tile mesh height
    private const float UnitRadius = 0.22f;   // unit mesh base radius
    private const float DamageShowSec = 1.5f; // seconds to show floating damage label

    // ── Tile colours ─────────────────────────────────────────────────────────
    private static readonly Color ColGrass    = new(0.32f, 0.62f, 0.28f);
    private static readonly Color ColForest   = new(0.14f, 0.42f, 0.16f);
    private static readonly Color ColMountain = new(0.56f, 0.52f, 0.46f);
    private static readonly Color ColWater    = new(0.18f, 0.40f, 0.78f);

    // ── Highlight colours ────────────────────────────────────────────────────
    private static readonly Color ColSelected   = new(1.00f, 0.90f, 0.10f);
    private static readonly Color ColMoveable   = new(0.20f, 0.85f, 1.00f, 0.80f);
    private static readonly Color ColAttackable = new(1.00f, 0.20f, 0.15f, 0.85f);

    // ── Unit colours (Player / Enemy × type) ─────────────────────────────────
    private static Color UnitColor(Team team, UnitType type) => (team, type) switch
    {
        (Team.Player, UnitType.Warrior) => new Color(0.20f, 0.45f, 0.92f),
        (Team.Player, UnitType.Archer)  => new Color(0.10f, 0.72f, 0.82f),
        (Team.Player, UnitType.Mage)    => new Color(0.70f, 0.20f, 0.92f),
        (Team.Enemy,  UnitType.Warrior) => new Color(0.90f, 0.18f, 0.18f),
        (Team.Enemy,  UnitType.Archer)  => new Color(0.92f, 0.52f, 0.08f),
        (Team.Enemy,  UnitType.Mage)    => new Color(0.80f, 0.10f, 0.50f),
        _                               => Colors.White,
    };

    // ── Scene nodes ──────────────────────────────────────────────────────────
    private Camera3D?        _camera;
    private DirectionalLight3D? _sun;
    private readonly Dictionary<Vector2I, TileData>  _tiles   = new();
    private readonly Dictionary<int,      UnitVisual> _unitVis = new();  // unit.Id → visual

    // ── Input state ──────────────────────────────────────────────────────────
    private enum InputState { Idle, UnitSelected }
    private InputState _inputState = InputState.Idle;

    private List<Vector2I> _moveTiles    = new();
    private List<Unit>     _attackUnits  = new();

    // ── DI lifecycle ─────────────────────────────────────────────────────────
    private bool _diReady    = false;
    private bool _worldBuilt = false;

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[BattleRenderer3D] DI failed."); return; }
        _diReady = true;
        GD.Print("[BattleRenderer3D] DI ready.");
    }

    public override void _Process(double _delta)
    {
        if (!_diReady || _worldBuilt) return;
        BuildWorld();
        _worldBuilt = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  World construction
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildWorld()
    {
        SetupCamera();
        SetupLighting();
        SetupSkyAmbient();
        BuildTileGrid();
        BuildUnitMeshes();
        SubscribeEvents();
        GD.Print("[BattleRenderer3D] 3D world built.");
    }

    private void SetupCamera()
    {
        _camera = new Camera3D();
        float cx = (_mapService!.MapWidth  - 1) * TileSize * 0.5f;
        float cz = (_mapService.MapHeight - 1) * TileSize * 0.5f;
        _camera.Position  = new Vector3(cx - 1f, 11f, cz + 8f);
        _camera.RotationDegrees = new Vector3(-52f, 0f, 0f);
        _camera.Fov = 45f;
        AddChild(_camera);
    }

    private void SetupLighting()
    {
        _sun = new DirectionalLight3D();
        _sun.RotationDegrees = new Vector3(-55f, 45f, 0f);
        _sun.LightEnergy     = 1.4f;
        _sun.ShadowEnabled   = true;
        AddChild(_sun);
    }

    private void SetupSkyAmbient()
    {
        // World environment for ambient light
        var env  = new Godot.Environment();
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightColor  = new Color(0.55f, 0.65f, 0.80f);
        env.AmbientLightEnergy = 0.55f;
        env.BackgroundMode     = Godot.Environment.BGMode.Color;
        env.BackgroundColor    = new Color(0.35f, 0.55f, 0.80f);

        var we = new WorldEnvironment { Environment = env };
        AddChild(we);
    }

    private void BuildTileGrid()
    {
        for (int gx = 0; gx < _mapService!.MapWidth;  gx++)
        for (int gz = 0; gz < _mapService.MapHeight; gz++)
        {
            var gridPos = new Vector2I(gx, gz);
            var tile    = _mapService.GetTile(gx, gz);
            float wy    = tile.Type switch
            {
                TileType.Water    => -0.12f,
                TileType.Mountain =>  0.10f,
                TileType.Forest   =>  0.04f,
                _                 =>  0.00f,
            };

            // StaticBody3D = needed for raycasting
            var body = new StaticBody3D();
            body.Position = new Vector3(gx * TileSize, wy, gz * TileSize);
            body.SetMeta("gx", gx);
            body.SetMeta("gz", gz);

            // Collision shape
            var shape = new CollisionShape3D();
            var box   = new BoxShape3D();
            box.Size  = new Vector3(TileSize * 0.96f, TileH, TileSize * 0.96f);
            shape.Shape = box;
            body.AddChild(shape);

            // Mesh
            var mesh   = new MeshInstance3D();
            var bMesh  = new BoxMesh();
            bMesh.Size = new Vector3(TileSize * 0.96f, TileH, TileSize * 0.96f);
            mesh.Mesh  = bMesh;
            var mat    = MakeMat(TileColor(tile.Type), true);
            mesh.MaterialOverride = mat;
            body.AddChild(mesh);

            AddChild(body);
            _tiles[gridPos] = new TileData(body, mesh, mat, TileColor(tile.Type));
        }
    }

    private void BuildUnitMeshes()
    {
        // Called once after DI fires — at this point AllUnits may already be populated
        // (if UnitManager's OnDependenciesResolved fired first). Either way,
        // we also subscribe OnTurnStarted to catch the first population.
        foreach (var u in _gameState!.AllUnits)
            EnsureUnitVisual(u);
    }

    private void SubscribeEvents()
    {
        _gameState!.OnTurnStarted += _ =>
        {
            // Rebuild unit visuals for any newly spawned units
            foreach (var u in _gameState.AllUnits)
                EnsureUnitVisual(u);
            RefreshHighlights();
        };

        _gameState.OnPhaseChanged += phase =>
        {
            if (phase is GamePhase.EnemyTurn)
                ClearHighlights(clearSelection: true);
            else if (phase is GamePhase.PlayerTurn)
            {
                // Sync positions after enemy AI moved things
                foreach (var u in _gameState.AllUnits)
                    SyncUnitVisualPosition(u);
            }
        };

        _gameState.OnSelectionChanged += unit =>
        {
            ClearHighlights(clearSelection: false);
            if (unit != null)
            {
                HighlightTile(unit.Position, ColSelected);
                foreach (var t in _mapService!.GetReachableTiles(unit))
                    HighlightTile(t, ColMoveable);
                foreach (var e in _mapService.GetAttackableTargets(unit))
                    HighlightTile(e.Position, ColAttackable);
                _moveTiles   = _mapService.GetReachableTiles(unit);
                _attackUnits = _mapService.GetAttackableTargets(unit);
            }
        };

        _gameState.OnUnitMoved += unit =>
        {
            SyncUnitVisualPosition(unit);
            // Re-show attack highlights if this unit is still selected
            if (_gameState.SelectedUnit == unit)
            {
                ClearHighlights(clearSelection: false);
                HighlightTile(unit.Position, ColSelected);
                _attackUnits = _mapService!.GetAttackableTargets(unit);
                foreach (var e in _attackUnits) HighlightTile(e.Position, ColAttackable);
                _moveTiles = new List<Vector2I>(); // already moved
            }
        };

        _battleService!.OnAttackExecuted += (attacker, defender, dmg) =>
        {
            SyncUnitVisualPosition(attacker);
            UpdateUnitLabel(attacker);
            UpdateUnitLabel(defender);
            ShowFloatingDamage(defender, dmg);
        };

        _battleService.OnUnitDefeated += unit =>
        {
            if (_unitVis.TryGetValue(unit.Id, out var vis))
            {
                vis.Root.QueueFree();
                _unitVis.Remove(unit.Id);
            }
            ClearHighlights(clearSelection: true);
            _inputState = InputState.Idle;
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mouse input
    // ─────────────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent ev)
    {
        if (!_worldBuilt) return;
        if (ev is not InputEventMouseButton mb) return;
        if (!mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;

        // Ignore clicks when it's not player turn or game is over
        if (_gameState!.Phase is GamePhase.EnemyTurn or GamePhase.Victory or GamePhase.Defeat)
            return;

        var hit = RaycastFromMouse(mb.Position);
        if (hit == null) return;

        HandleGridClick(hit.Value);
    }

    private Vector2I? RaycastFromMouse(Vector2 mousePos)
    {
        if (_camera == null) return null;
        var viewport = GetViewport();
        var space    = GetWorld3D().DirectSpaceState;

        var from = _camera.ProjectRayOrigin(mousePos);
        var to   = from + _camera.ProjectRayNormal(mousePos) * 200f;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithBodies = true;
        var result = space.IntersectRay(query);
        if (result.Count == 0) return null;

        var collider = result["collider"].As<StaticBody3D>();
        if (collider == null) return null;

        int gx = collider.GetMeta("gx").AsInt32();
        int gz = collider.GetMeta("gz").AsInt32();
        return new Vector2I(gx, gz);
    }

    private void HandleGridClick(Vector2I gridPos)
    {
        var unitMgr = GetParent().GetNodeOrNull<UnitManager>("UnitManager");

        switch (_inputState)
        {
            case InputState.Idle:
                TrySelectUnit(gridPos);
                break;

            case InputState.UnitSelected:
                var sel = _gameState!.SelectedUnit;
                if (sel == null) { _inputState = InputState.Idle; return; }

                // Click on an attackable enemy
                var enemy = _attackUnits.Find(u => u.Position == gridPos);
                if (enemy != null)
                {
                    unitMgr?.TryAttackTarget(enemy);
                    _gameState.SelectedUnit = null;
                    _inputState = InputState.Idle;
                    ClearHighlights(clearSelection: true);
                    return;
                }

                // Click on a reachable empty tile
                if (_moveTiles.Contains(gridPos) && _mapService!.GetUnitAt(gridPos) == null)
                {
                    unitMgr?.TryMoveSelected(gridPos);
                    // Remain in UnitSelected state so player can still attack
                    // _gameState.OnUnitMoved event will refresh highlights
                    return;
                }

                // Click on same unit → deselect
                if (gridPos == sel.Position)
                {
                    _gameState.SelectedUnit = null;
                    _inputState = InputState.Idle;
                    ClearHighlights(clearSelection: true);
                    return;
                }

                // Click on another own player unit → switch selection
                var other = _mapService!.GetUnitAt(gridPos);
                if (other != null && other.Team == Team.Player)
                {
                    _gameState.SelectedUnit = other;
                    _inputState = InputState.UnitSelected;
                    return;
                }

                // Click elsewhere → deselect
                _gameState.SelectedUnit = null;
                _inputState = InputState.Idle;
                ClearHighlights(clearSelection: true);
                break;
        }
    }

    private void TrySelectUnit(Vector2I gridPos)
    {
        var unit = _mapService!.GetUnitAt(gridPos);
        if (unit == null || unit.Team != Team.Player || !unit.IsAlive) return;

        _gameState!.SelectedUnit = unit;
        _inputState = InputState.UnitSelected;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Visual helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void EnsureUnitVisual(Unit unit)
    {
        if (_unitVis.ContainsKey(unit.Id)) return;

        var root = new Node3D();
        root.Name = $"Unit_{unit.Id}";

        // Body mesh
        var meshInst = new MeshInstance3D();
        var (w, h, d) = unit.Type switch
        {
            UnitType.Warrior => (0.48f, 0.78f, 0.48f),
            UnitType.Archer  => (0.32f, 0.68f, 0.32f),
            UnitType.Mage    => (0.28f, 0.92f, 0.28f),
            _                => (0.40f, 0.70f, 0.40f),
        };
        var bm = new BoxMesh(); bm.Size = new Vector3(w, h, d);
        meshInst.Mesh = bm;
        meshInst.MaterialOverride = MakeMat(UnitColor(unit.Team, unit.Type));
        meshInst.Position = new Vector3(0, h * 0.5f, 0);
        root.AddChild(meshInst);

        // HP bar background
        var barBg = new MeshInstance3D();
        var barBgM = new BoxMesh(); barBgM.Size = new Vector3(0.8f, 0.06f, 0.06f);
        barBg.Mesh = barBgM;
        barBg.MaterialOverride = MakeMat(new Color(0.2f, 0.2f, 0.2f));
        barBg.Position = new Vector3(0, h + 0.15f, 0);
        barBg.Name = "HpBarBg";
        root.AddChild(barBg);

        // HP bar fill
        var barFill = new MeshInstance3D();
        var barFillM = new BoxMesh(); barFillM.Size = new Vector3(0.8f, 0.06f, 0.065f);
        barFill.Mesh = barFillM;
        barFill.MaterialOverride = MakeMat(new Color(0.15f, 0.9f, 0.15f));
        barFill.Position = new Vector3(0, h + 0.15f, 0);
        barFill.Name = "HpBarFill";
        root.AddChild(barFill);

        // Name label
        var label = new Label3D();
        label.Text     = unit.Name;
        label.FontSize = 18;
        label.Modulate = unit.Team == Team.Player ? Colors.Cyan : Colors.OrangeRed;
        label.Position = new Vector3(0, h + 0.40f, 0);
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        label.Name = "NameLabel";
        root.AddChild(label);

        AddChild(root);
        _unitVis[unit.Id] = new UnitVisual(root, meshInst, barFill, label, unit);
        SyncUnitVisualPosition(unit);
        UpdateUnitLabel(unit);
    }

    private void SyncUnitVisualPosition(Unit unit)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        float wy = TileWorldY(unit.Position);
        float h  = ((BoxMesh)vis.Mesh.Mesh).Size.Y;
        vis.Root.Position = new Vector3(
            unit.Position.X * TileSize,
            wy + TileH * 0.5f,
            unit.Position.Y * TileSize
        );
    }

    private void UpdateUnitLabel(Unit unit)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;
        float ratio = (float)unit.Hp / unit.MaxHp;

        // Update HP bar fill scale (scale X by ratio, offset so it grows from left)
        vis.HpBarFill.Scale    = new Vector3(ratio, 1f, 1f);
        vis.HpBarFill.Position = vis.HpBarFill.Position with { X = (ratio - 1f) * 0.4f };

        // Colour: green → yellow → red
        var barMat = (StandardMaterial3D)vis.HpBarFill.MaterialOverride;
        barMat.AlbedoColor = ratio > 0.5f
            ? new Color(1f - (ratio - 0.5f) * 2f, 0.9f, 0.15f)
            : new Color(0.9f, ratio * 2f * 0.9f, 0.05f);

        vis.Label.Text = $"{unit.Name}\n{unit.Hp}/{unit.MaxHp}";
    }

    private void ShowFloatingDamage(Unit unit, int damage)
    {
        if (!_unitVis.TryGetValue(unit.Id, out var vis)) return;

        var fl = new Label3D();
        fl.Text      = $"-{damage}";
        fl.FontSize  = 24;
        fl.Modulate  = Colors.Yellow;
        fl.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        fl.Position  = vis.Root.Position + new Vector3(0, 1.2f, 0);
        AddChild(fl);

        // Remove after a short delay
        var timer = new Timer { WaitTime = DamageShowSec, OneShot = true };
        timer.Timeout += () => { fl.QueueFree(); timer.QueueFree(); };
        AddChild(timer);
        timer.Start();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tile highlight helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void HighlightTile(Vector2I pos, Color col)
    {
        if (!_tiles.TryGetValue(pos, out var td)) return;
        ((StandardMaterial3D)td.Mesh.MaterialOverride).AlbedoColor = col;
    }

    private void ClearHighlights(bool clearSelection)
    {
        foreach (var (pos, td) in _tiles)
            ((StandardMaterial3D)td.Mesh.MaterialOverride).AlbedoColor = td.BaseColor;
        _moveTiles   = new();
        _attackUnits = new();
        if (clearSelection) _inputState = InputState.Idle;
    }

    private void RefreshHighlights()
    {
        if (_gameState?.SelectedUnit is { } sel)
        {
            ClearHighlights(clearSelection: false);
            HighlightTile(sel.Position, ColSelected);
            _moveTiles   = _mapService!.GetReachableTiles(sel);
            _attackUnits = _mapService.GetAttackableTargets(sel);
            foreach (var t in _moveTiles)   HighlightTile(t, ColMoveable);
            foreach (var e in _attackUnits) HighlightTile(e.Position, ColAttackable);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Utility
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

    private static StandardMaterial3D MakeMat(Color color, bool rougher = false)
    {
        var mat = new StandardMaterial3D();
        mat.AlbedoColor  = color;
        mat.Roughness    = rougher ? 0.85f : 0.55f;
        mat.Metallic     = rougher ? 0.00f : 0.10f;
        return mat;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Inner data types
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record TileData(
        StaticBody3D  Body,
        MeshInstance3D Mesh,
        StandardMaterial3D Mat,
        Color BaseColor);

    private sealed record UnitVisual(
        Node3D Root,
        MeshInstance3D Mesh,
        MeshInstance3D HpBarFill,
        Label3D Label,
        Unit Unit);
}
