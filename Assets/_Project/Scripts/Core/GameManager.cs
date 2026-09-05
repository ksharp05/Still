using UnityEngine;

/// <summary>
/// Owns the run: which floor you're on, how many shards you've got, and what happens
/// when you die.
///
/// The player object survives between floors (health and shards carry over, which is what
/// makes descending a real decision), while everything else lives under a floor root that
/// gets destroyed and rebuilt each descent.
/// </summary>
[DefaultExecutionOrder(-50)]
public class GameManager : MonoBehaviour
{
    /// <summary>
    /// <see cref="Outfitting"/> is the threshold: the player has chosen the stairs but has not
    /// committed yet, and can spend shards before going down. It gets a frozen world from
    /// SetState like every other non-Playing state, and CanPlay already excludes it, so
    /// movement, pickups and the stairs prompt all fall silent without any extra work.
    /// </summary>
    public enum RunState { Title, Playing, Paused, Dead, Outfitting }
    public RunState State { get; private set; } = RunState.Title;
    public bool CanPlay => State == RunState.Playing && Time.frameCount > _inputFrame;
    public float RunSeconds { get; private set; }
    public bool CanRestart => !IsGameOver || Time.unscaledTime > _diedAt + 0.6f;
    private int _inputFrame;
    private float _diedAt;
    public static GameManager Instance { get; private set; }

    /// <summary>Enemies and projectiles read these instead of searching for the player.</summary>
    public static Transform PlayerTransform { get; private set; }
    public static Health PlayerHealth { get; private set; }

    /// <summary>Everything belonging to the current floor. Destroyed on descent.</summary>
    public static Transform FloorRoot { get; private set; }

    /// <summary>Layout of the floor currently being played, for anything that needs to reason about it.</summary>
    public static DungeonMap CurrentMap { get; private set; }

    /// <summary>What the player has bought this run. Null until the player exists.</summary>
    public static PlayerUpgrades Upgrades { get; private set; }

    public int Floor { get; private set; }
    public int Shards { get; private set; }
    public bool IsGameOver { get; private set; }

    /// <summary>Best depth reached this session — survives death, so there's something to beat.</summary>
    public int BestFloor { get; private set; }

    private GameObject _player;
    private GameObject _floorObject;
    private int _runSeed;

    private void Awake()
    {
        Instance = this;
        BestFloor = PlayerPrefs.GetInt("Still.BestFloor", 0);
        AudioListener.volume = PlayerPrefs.GetFloat("Still.Volume", 0.75f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            PlayerTransform = null;
            PlayerHealth = null;
            FloorRoot = null;
            CurrentMap = null;
            Upgrades = null;
            Juice.TimeLocked = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    private void Start()
    {
        StartRun();
        SetState(RunState.Title);
    }

    private void SetState(RunState state)
    {
        State = state;
        _inputFrame = Time.frameCount;
        Juice.TimeLocked = state != RunState.Playing;
        Time.timeScale = Juice.TimeLocked ? 0f : 1f;
        AudioListener.pause = state == RunState.Paused || state == RunState.Title;
    }

    public void Resume() { if (State == RunState.Paused) SetState(RunState.Playing); }
    public void Pause() { if (State == RunState.Playing) SetState(RunState.Paused); }
    private void OnApplicationFocus(bool focused) { if (!focused) Pause(); }

    private void Update()
    {
        if (State == RunState.Playing) RunSeconds += Time.deltaTime;
        if (State == RunState.Outfitting) { UpdateOutfitting(); return; }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (State == RunState.Playing) Pause();
            else if (State == RunState.Paused) Resume();
        }
        if (State == RunState.Title && Input.GetKeyDown(KeyCode.Return)) StartRun();
        if (IsGameOver && Time.unscaledTime > _diedAt + 0.6f && Input.GetKeyDown(KeyCode.R))
            StartRun();
    }

    public void StartRun()
    {
        if (!CanRestart) return;
        IsGameOver = false;
        RunSeconds = 0f;
        SetState(RunState.Playing);
        Juice.TimeLocked = false;
        Time.timeScale = 1f;
        WorldTime.ResetToFrozen();

        Floor = 0;
        Shards = 0;
        _runSeed = Random.Range(1, int.MaxValue);

        if (_player == null) CreatePlayer();

        // Before RestorePlayer: resetting Vitality lowers max health back to its authored value,
        // and RestorePlayer's Configure(Max) then fills to that rather than to an upgraded max.
        // The player object survives restarts, so without this every upgrade would carry over.
        if (Upgrades != null) Upgrades.ResetAll();

        RestorePlayer();

        BuildFloor(1);

        GameEvents.RaiseShards(Shards);
        GameEvents.RaisePlayerHealth(PlayerHealth.Current, PlayerHealth.Max);
    }

    /// <summary>Step onto the threshold: spend shards before committing to the next floor.</summary>
    public void BeginDescent()
    {
        if (!CanPlay) return;
        SetState(RunState.Outfitting);
    }

    /// <summary>Leave the threshold without descending, so the player can go find more shards.</summary>
    public void CancelDescent()
    {
        if (State == RunState.Outfitting) SetState(RunState.Playing);
    }

    /// <summary>
    /// Threshold input. Raw key polling in Update with the keys printed on the screen is the
    /// house convention here — the project has no EventSystem navigation and should not grow any.
    /// </summary>
    private void UpdateOutfitting()
    {
        if (Time.frameCount <= _inputFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { CancelDescent(); return; }
        if (Input.GetKeyDown(KeyCode.Return)) { Descend(); return; }

        if (Upgrades == null) return;
        for (int i = 0; i < PlayerUpgrades.LineCount; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
            if (Upgrades.TryPurchase((UpgradeKind)i)) Sound.Play(Sfx.Pickup, 0.9f);
            return;
        }
    }

    public void Descend()
    {
        // Deliberately not CanPlay: descending is confirmed from the threshold, where the run
        // state is Outfitting rather than Playing.
        if (State != RunState.Playing && State != RunState.Outfitting) return;

        SetState(RunState.Playing);
        BuildFloor(Floor + 1);
        _inputFrame = Time.frameCount;
    }

    public void AddShard()
    {
        Shards++;
        GameEvents.RaiseShards(Shards);
    }

    /// <summary>Spend shards if there are enough. Mirrors AddShard, including the event.</summary>
    public bool TrySpendShards(int cost)
    {
        if (cost < 0 || Shards < cost) return false;

        Shards -= cost;
        GameEvents.RaiseShards(Shards);
        return true;
    }

    private void CreatePlayer()
    {
        _player = ActorFactory.CreatePlayer(Vector3.zero);
        PlayerTransform = _player.transform;
        PlayerHealth = _player.GetComponent<Health>();
        Upgrades = _player.GetComponent<PlayerUpgrades>();

        PlayerHealth.Changed += (current, max) => GameEvents.RaisePlayerHealth(current, max);
        PlayerHealth.Damaged += point => GameVfx.Emit(VfxKind.PlayerHurt, _player.transform.position + Vector3.up * .7f);
        PlayerHealth.Died += HandlePlayerDeath;

        var controller = _player.GetComponent<PlayerController>();
        controller.Dashed += () => Sound.Play(Sfx.Dash, 0.6f);

        if (CameraRig.Instance != null) CameraRig.Instance.SetTarget(_player.transform);
    }

    private void RestorePlayer()
    {
        _player.SetActive(true);
        PlayerHealth.Invulnerable = false;
        PlayerHealth.Configure(PlayerHealth.Max); // back to full
    }

    private void BuildFloor(int floorNumber)
    {
        if (_floorObject != null)
        {
            _floorObject.SetActive(false);
            Destroy(_floorObject);
        }

        Floor = floorNumber;
        if (Floor > BestFloor)
        {
            BestFloor = Floor;
            PlayerPrefs.SetInt("Still.BestFloor", BestFloor);
            PlayerPrefs.Save();
        }

        _floorObject = new GameObject($"Floor {floorNumber}");
        FloorRoot = _floorObject.transform;

        // Each floor gets its own deterministic seed derived from the run seed.
        int seed = _runSeed ^ (floorNumber * 486187739);

        DungeonMap map = DungeonGenerator.Generate(floorNumber, seed);
        CurrentMap = map;
        DungeonBuilder.Build(map, FloorRoot);
        Vector3 spawn = FloorPopulator.Populate(map, floorNumber, seed, FloorRoot);

        MovePlayerTo(spawn);
        GameEvents.RaiseFloor(Floor);
    }

    /// <summary>
    /// A CharacterController overrides direct transform writes, so it has to be switched
    /// off for the teleport or the player snaps straight back to where they were.
    /// </summary>
    private void MovePlayerTo(Vector3 position)
    {
        _player.GetComponent<PlayerController>().ResetMotion();
        _player.GetComponent<PlayerCombat>().ResetCombat();
        if (TimeDirector.Instance != null) TimeDirector.Instance.ResetClock();
        var cc = _player.GetComponent<CharacterController>();
        cc.enabled = false;
        _player.transform.position = position;
        cc.enabled = true;

        if (CameraRig.Instance != null) CameraRig.Instance.SetTarget(_player.transform);
    }

    private void HandlePlayerDeath()
    {
        if (IsGameOver) return;

        IsGameOver = true;
        _diedAt = Time.unscaledTime;
        SetState(RunState.Dead);
        Sound.Play(Sfx.PlayerDie, 1f);
        GameVfx.Emit(VfxKind.PlayerDeath, _player.transform.position + Vector3.up);
        Juice.Shake(1.5f);

        _player.SetActive(false);

        // TimeLocked stops a pending hit-stop from quietly un-pausing us a frame later.
        Juice.TimeLocked = true;
        Time.timeScale = 0f;

        GameEvents.RaisePlayerDied();
    }
}

