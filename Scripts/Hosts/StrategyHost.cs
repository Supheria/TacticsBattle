using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

[Host]
public sealed partial class StrategyHost : Node
{
    private StandardTileRuleProvider? _tileRules;
    private StandardUnitDataProvider? _unitData;
    private UnitFactory?              _unitFactory;

    [Inject] private IUnitDataProvider? _unitDataInj;

    [Provide(ExposedTypes = [typeof(ITileRuleProvider)])]
    public StandardTileRuleProvider TileRules => _tileRules ??= new StandardTileRuleProvider();

    [Provide(ExposedTypes = [typeof(IUnitDataProvider)])]
    public StandardUnitDataProvider UnitData => _unitData ??= new StandardUnitDataProvider();

    [Provide(
        ExposedTypes = [typeof(IUnitFactory)],
        WaitFor      = [nameof(_unitDataInj)]
    )]
    public UnitFactory UnitFactorySvc => _unitFactory ??= new UnitFactory(_unitDataInj!);

    public override partial void _Notification(int what);
}
