using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] that exposes ILevelRegistryService to the DI scope.
///
/// Previously this required:
///   LevelMenuHost + Level1ConfigHost + Level2ConfigHost + Level3ConfigHost
/// Now it is a single host backed by the static LevelRegistry.
///
/// To add a new level: edit LevelRegistry.cs only.
/// </summary>
[Host]
public sealed partial class LevelRegistryHost : Node
{
    [Provide(ExposedTypes = [typeof(ILevelRegistryService)])]
    public LevelRegistryService Registry => new LevelRegistryService();

    public override partial void _Notification(int what);

    public override void _Ready() =>
        GD.Print($"[LevelRegistryHost] {LevelRegistry.All.Count} levels available. " +
                 $"Active: index {SelectedLevel.Index} ({LevelRegistry.Get(SelectedLevel.Index)?.Name ?? "?"})");
}
