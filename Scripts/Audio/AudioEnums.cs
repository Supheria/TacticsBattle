namespace TacticsBattle.Audio;

public enum BgmTrack
{
    Menu,       // calm title theme
    Battle,     // tense combat loop
    Victory,    // short fanfare → loop
    Defeat,     // somber sting
}

public enum SfxEvent
{
    UiClick,        // button press / menu navigation
    UnitSelect,     // click own unit
    UnitMove,       // unit placed on tile
    AttackHit,      // melee/ranged impact
    AttackMiss,     // miss (unused currently but wired)
    UnitDeath,      // unit defeated
    StatusApplied,  // poison / slow applied
    StatusTick,     // poison tick damage
    HealAura,       // aura heal pulse
    PushBack,       // unit knocked back
    Victory,        // game-over victory
    Defeat,         // game-over defeat
}
