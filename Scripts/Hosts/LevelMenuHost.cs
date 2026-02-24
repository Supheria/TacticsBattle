using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>[Host] that provides ILevelMenuService to the level-select screen.</summary>
[Host]
public sealed partial class LevelMenuHost : Node
{
    [Provide(ExposedTypes = [typeof(ILevelMenuService)])]
    public LevelMenuService MenuSvc => new LevelMenuService();

    public override partial void _Notification(int what);
}
