/// <summary>
/// Game-wide ability switches, deliberately configured in C# rather than the Inspector.
/// Change an initializer to false and recompile to remove that ability from your fangame.
/// Numeric tuning belongs to the serialized settings on the player's components.
/// </summary>
public static class PlayerAbilityFeatures
{
    public static bool Jump = true;
    public static bool ManualRolling = true;
    public static bool SpinDash = true;
    public static bool HomingAttack = true;
    public static bool AirDash = true;
    public static bool DropDash = true;

    // Optional extensions are disabled: the default moveset uses the ground-launching Drop Dash.
    public static bool DropDashAirDash = false;
    public static bool DropDashHomingAttack = false;
    public static bool BoostedHomingRebound = false;
    public static bool ReboundRestoresAirAbilities = false;
}
