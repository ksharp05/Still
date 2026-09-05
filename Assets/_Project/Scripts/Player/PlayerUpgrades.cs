using UnityEngine;

/// <summary>Which line an upgrade belongs to. Order matches the keys 1-4 on the threshold screen.</summary>
public enum UpgradeKind
{
    Edge = 0,      // swing damage
    Reach = 1,     // swing and deflect range
    Vitality = 2,  // max health
    Momentum = 3,  // dash cooldown
}

/// <summary>
/// What the player has bought this run, and the only thing that writes their stats.
///
/// The player has always scaled in exactly one direction — down. Enemy health rises about 11%
/// per floor while every player number was a compile-time constant, so deep floors got longer
/// rather than more dangerous. This is the other half of that curve, paid for with shards, which
/// until now were incremented, displayed, and never spent on anything.
///
/// Stats are DERIVED, never incremented. <see cref="Apply"/> recomputes every affected value from
/// the components' authored defaults plus the current levels, so <see cref="ResetAll"/> is just
/// "levels to zero, recompute". That matters more than it looks: the player GameObject is created
/// once and never destroyed — it survives every descent AND every restart — so a stat nudged in
/// place would quietly persist into the next run with nothing anywhere to undo it.
/// </summary>
public class PlayerUpgrades : MonoBehaviour
{
    public const int LineCount = 4;

    /// <summary>One purchasable line: what it does, what it costs, how far it goes.</summary>
    public readonly struct Line
    {
        public readonly UpgradeKind Kind;
        public readonly string Name;
        public readonly string Effect;
        public readonly int MaxLevel;
        public readonly int BaseCost;
        public readonly int CostStep;

        public Line(UpgradeKind kind, string name, string effect, int maxLevel, int baseCost, int costStep)
        {
            Kind = kind;
            Name = name;
            Effect = effect;
            MaxLevel = maxLevel;
            BaseCost = baseCost;
            CostStep = costStep;
        }

        public int CostAt(int level) => BaseCost + CostStep * level;
    }

    /// <summary>
    /// The catalogue.
    ///
    /// Neither swing duration nor dash duration is purchasable, deliberately: both are handed to
    /// TimeDirector.Impulse, so shortening them would quietly change how long the world runs at
    /// full speed after every attack — a hidden edit to the core mechanic dressed up as a stat.
    /// </summary>
    public static readonly Line[] Lines =
    {
        new Line(UpgradeKind.Edge, "EDGE", "+1 swing damage", 3, 6, 5),
        new Line(UpgradeKind.Reach, "REACH", "+0.45m swing and deflect reach", 3, 6, 5),

        // Capped at 4 because the HUD allocates exactly 12 health pips and the player starts on
        // 6; anything past that would silently stop rendering.
        new Line(UpgradeKind.Vitality, "VITALITY", "+1 max health", 4, 8, 6),
        new Line(UpgradeKind.Momentum, "MOMENTUM", "-0.04s dash cooldown", 3, 7, 6),
    };

    private readonly int[] _levels = new int[LineCount];

    private PlayerCombat _combat;
    private PlayerController _controller;
    private Health _health;
    private int _baseMaxHealth;

    public int LevelOf(UpgradeKind kind) => _levels[(int)kind];

    public bool IsMaxed(UpgradeKind kind) => LevelOf(kind) >= Lines[(int)kind].MaxLevel;

    /// <summary>Shards the next level of this line costs, or 0 when it is already maxed.</summary>
    public int CostOf(UpgradeKind kind) =>
        IsMaxed(kind) ? 0 : Lines[(int)kind].CostAt(LevelOf(kind));

    private void Awake()
    {
        _combat = GetComponent<PlayerCombat>();
        _controller = GetComponent<PlayerController>();
        _health = GetComponent<Health>();

        // Captured before any upgrade can touch it, for the same reason the other components
        // capture theirs: this is the value a reset has to return to.
        if (_health != null) _baseMaxHealth = _health.Max;
    }

    /// <summary>Buy one level, if it is affordable and not already maxed.</summary>
    public bool TryPurchase(UpgradeKind kind)
    {
        if (IsMaxed(kind)) return false;

        var gm = GameManager.Instance;
        if (gm == null || !gm.TrySpendShards(CostOf(kind))) return false;

        _levels[(int)kind]++;
        Apply();
        return true;
    }

    /// <summary>Back to an unupgraded run. Called by GameManager when a run starts.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < _levels.Length; i++) _levels[i] = 0;
        Apply();
    }

    /// <summary>Recompute every affected stat from the authored defaults plus current levels.</summary>
    public void Apply()
    {
        if (_combat != null)
            _combat.ApplyTuning(LevelOf(UpgradeKind.Edge), LevelOf(UpgradeKind.Reach) * 0.45f);

        if (_controller != null)
            _controller.ApplyTuning(LevelOf(UpgradeKind.Momentum) * 0.04f);

        if (_health != null)
            _health.SetMax(_baseMaxHealth + LevelOf(UpgradeKind.Vitality));
    }

    /// <summary>One line of the threshold screen, already formatted.</summary>
    public string Describe(UpgradeKind kind)
    {
        Line line = Lines[(int)kind];
        int level = LevelOf(kind);

        // Spelled out rather than drawn as pips: a run of dots reads as an ellipsis at this
        // font size, not as a meter.
        string rank = $"{level}/{line.MaxLevel}";
        return IsMaxed(kind)
            ? $"[ {(int)kind + 1} ]   {line.Name}   {rank}   MAX"
            : $"[ {(int)kind + 1} ]   {line.Name}   {rank}   {line.Effect}   -   {CostOf(kind)} shards";
    }
}
