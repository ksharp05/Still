using UnityEngine;
using UnityEngine.Rendering;

public enum VfxKind
{
    Hit, Deflect, Dash, DashEnd, EnemyDeath, PlayerHurt, PlayerDeath,
    Shard, Heal, Arrival, Descent, Muzzle, WallHit, Charge
}

/// <summary>Shared, bounded particle banks and reusable slash meshes. No burst instantiates objects.</summary>
[DefaultExecutionOrder(50)]
public class GameVfx : MonoBehaviour
{
    public enum Clock { World, Player, Death }
    private enum Shape { Glow, Shard, Ring }
    public const int ArcCapacity = 12;
    public const int ParticleCapacity = 2832;
    public static GameVfx Instance { get; private set; }
    public VfxTuning Tuning { get; private set; }
    public int ActiveParticles { get { int n = 0; foreach (var p in _banks) n += p.particleCount; return n; } }
    public int ActiveArcs { get { int n = 0; foreach (var a in _arcs) if (a.Active) n++; return n; } }
    public int EmittedParticles { get; private set; }
    public float WorldSimulationSpeed => _banks[0].main.simulationSpeed;
    public Material PlayerTrail => _playerTrail;
    public Material EnemyTrail => _enemyTrail;

    private readonly ParticleSystem[] _banks = new ParticleSystem[9];
    private readonly Arc[] _arcs = new Arc[ArcCapacity];
    private readonly Material[] _materials = new Material[4];
    private readonly System.Random _rng = new System.Random(53109);
    private Material _playerTrail, _enemyTrail;
    private int _nextArc;
    private bool _ownsTuning, _reduced, _flowing;
    private float _dust, _clockCooldown;

    private void Awake()
    {
        Instance = this;
        Tuning = Resources.Load<VfxTuning>("VfxTuning");
        if (Tuning == null) { Tuning = ScriptableObject.CreateInstance<VfxTuning>(); _ownsTuning = true; }
        var shader = Resources.Load<Shader>("StillVfx");
        if (shader == null) { Debug.LogError("STILL particle shader is missing."); enabled = false; return; }
        for (int i = 0; i < 4; i++)
        {
            _materials[i] = new Material(shader) { name = "VFX shared " + i };
            _materials[i].SetFloat("_Shape", i);
        }
        _playerTrail = new Material(_materials[3]) { name = "VFX player trail" };
        _enemyTrail = new Material(_materials[3]) { name = "VFX hostile trail" };
        SetTrailIntensity();
        for (int clock = 0; clock < 3; clock++)
            for (int shape = 0; shape < 3; shape++) BuildBank((Clock)clock, (Shape)shape);
        for (int i = 0; i < _arcs.Length; i++) _arcs[i] = new Arc(transform, _materials[3]);
        _reduced = HUD.ReducedEffects;
    }

    private void OnEnable() => GameEvents.FloorChanged += NewFloor;
    private void OnDisable() => GameEvents.FloorChanged -= NewFloor;
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var a in _arcs) a?.Dispose();
        foreach (var m in _materials) if (m != null) Destroy(m);
        if (_playerTrail != null) Destroy(_playerTrail);
        if (_enemyTrail != null) Destroy(_enemyTrail);
        if (_ownsTuning) Destroy(Tuning);
    }
    private void NewFloor(int floor)
    {
        Clear(); _flowing = false; _clockCooldown = Time.unscaledTime + 1f;
        if (GameManager.PlayerTransform != null)
            Emit(floor > 1 ? VfxKind.Descent : VfxKind.Arrival, GameManager.PlayerTransform.position);
    }
    public void Clear()
    {
        foreach (var p in _banks) if (p != null) p.Clear();
        foreach (var a in _arcs) a?.Clear();
        _dust = 0;
    }
    public static float Speed(Clock clock)
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.State != GameManager.RunState.Playing)
            return gm.State == GameManager.RunState.Dead && clock == Clock.Death ? 1f : 0f;
        return Time.timeScale * (clock == Clock.World ? WorldTime.Scale : 1f);
    }
    private void Update()
    {
        if (_reduced != HUD.ReducedEffects) { _reduced = HUD.ReducedEffects; Clear(); SetTrailIntensity(); }
        for (int i = 0; i < _banks.Length; i++)
        { var main = _banks[i].main; main.simulationSpeed = Speed((Clock)(i / 3)); }
        foreach (var a in _arcs) a.Tick(Time.unscaledDeltaTime * Speed(a.TimeDomain));
        var gm = GameManager.Instance; var player = GameManager.PlayerTransform;
        if (gm == null || !gm.CanPlay || player == null) return;
        _dust += WorldTime.DeltaTime * Tuning.DustPerSecond;
        if (_dust >= 1f)
        {
            _dust = 0f;
            if (!_reduced) Particle(Clock.World, Shape.Glow, player.position + new Vector3(Range(-8,8),Range(.3f,2.2f),Range(-8,8)),
                new Vector3(.04f,.08f,0), new Color(.34f,.54f,.65f,.3f), Range(.04f,.10f), 2f);
        }
        bool next = _flowing ? WorldTime.Flow > .12f : WorldTime.Flow > .7f;
        if (next != _flowing && Time.unscaledTime > _clockCooldown)
        {
            _flowing = next; _clockCooldown = Time.unscaledTime + .8f;
            Ring(player.position, Palette.PlayerGlow, next ? 2.4f : 3.1f, .45f, Clock.Player, .22f);
        }
    }
    private void SetTrailIntensity()
    {
        float alpha = HUD.ReducedEffects ? .35f : 1f;
        Color player = Palette.Blade * 1.4f; player.a = alpha;
        Color enemy = Palette.Arrow * 1.2f; enemy.a = alpha;
        _playerTrail.SetColor("_Tint", player);
        _enemyTrail.SetColor("_Tint", enemy);
    }
    private void BuildBank(Clock clock, Shape shape)
    {
        var go = new GameObject("VFX " + clock + " " + shape); go.transform.SetParent(transform, false);
        var p = go.AddComponent<ParticleSystem>(); p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = p.main; m.loop = true; m.playOnAwake = false; m.duration = 5; m.useUnscaledTime = true;
        m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = shape == Shape.Ring ? 48 : shape == Shape.Shard ? 512 : 384;
        m.startSpeed = 0; m.startLifetime = 1; m.gravityModifier = shape == Shape.Shard ? .45f : 0;
        m.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = p.emission; emission.enabled = false;
        var sh = p.shape; sh.enabled = false;
        var color = p.colorOverLifetime; color.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1,0), new GradientAlphaKey(.8f,.2f), new GradientAlphaKey(0,1) }); color.color = g;
        var size = p.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, shape == Shape.Ring ? AnimationCurve.EaseInOut(0,.22f,1,1) : AnimationCurve.EaseInOut(0,1,1,.05f));
        var r = p.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = _materials[(int)shape];
        r.renderMode = shape == Shape.Ring ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.maxParticleSize = 2;
        p.useAutoRandomSeed = false; p.randomSeed = (uint)(100 + (int)clock * 3 + (int)shape);
        p.Play(); _banks[(int)clock * 3 + (int)shape] = p;
    }
    private float Range(float a, float b) => Mathf.Lerp(a,b,(float)_rng.NextDouble());
    private Vector3 Radial()
    { float a = Range(0,Mathf.PI*2); return new Vector3(Mathf.Cos(a),Range(.1f,.6f),Mathf.Sin(a)); }
    private int Count(int n) => Mathf.Max(1,Mathf.RoundToInt(n * Tuning.Density * (HUD.ReducedEffects ? Tuning.ReducedDensity : 1f)));
    private void Particle(Clock clock, Shape shape, Vector3 p, Vector3 velocity, Color color, float size, float life)
    {
        var bank = _banks[(int)clock * 3 + (int)shape]; if (bank == null) return;
        color = new Color(color.r*Tuning.Brightness,color.g*Tuning.Brightness,color.b*Tuning.Brightness,color.a);
        bank.Emit(new ParticleSystem.EmitParams { position=p, velocity=velocity, startColor=color, startSize=size,
            startLifetime=life, rotation=Range(0,360) },1); EmittedParticles++;
    }
    private void Ring(Vector3 p, Color color, float diameter, float life, Clock clock, float alpha=.65f)
    {
        color.a = alpha * (HUD.ReducedEffects ? .45f : 1);
        p.y = Mathf.Max(.06f,p.y+.04f);
        Particle(clock,Shape.Ring,p,Vector3.zero,color,diameter,life);
    }
    private void Burst(Vector3 p, Vector3 direction, Color color, int count, float speed, Clock clock)
    {
        int n = Count(count);
        for (int i=0;i<n;i++) Particle(clock,Shape.Shard,p, (Radial()+direction*.65f)*Range(speed*.4f,speed),
            color,Range(.06f,.16f),Range(.25f,.6f));
    }
    public static void LegacyBurst(Vector3 p, Color c, int n, float speed)
        => Instance?.Burst(p,Vector3.zero,c,n,speed,Clock.World);
    public static void Emit(VfxKind kind, Vector3 p, Vector3 direction=default, Color? tint=null)
    { if (Instance != null && Instance.enabled) Instance.PlayEffect(kind,p,direction,tint); }
    private void PlayEffect(VfxKind kind, Vector3 p, Vector3 d, Color? tint)
    {
        Color c = tint ?? Palette.Blade;
        switch(kind)
        {
            case VfxKind.Hit:
                Burst(p,d,c,12,5,Clock.World); Particle(Clock.Player,Shape.Glow,p,Vector3.zero,Color.white,.65f,.12f); break;
            case VfxKind.Deflect:
                Burst(p,d,Palette.Blade,24,7,Clock.Player);
                Particle(Clock.Player,Shape.Glow,p,Vector3.zero,Color.white,1.6f,.16f);
                Ring(p,Palette.Blade,2.8f,.28f,Clock.Player); break;
            case VfxKind.Dash:
                Burst(p+Vector3.up*.25f,-d,Palette.PlayerGlow,14,3,Clock.Player);
                Ring(p,Palette.PlayerGlow,2f,.28f,Clock.Player,.35f); break;
            case VfxKind.DashEnd:
                Burst(p+Vector3.up*.15f,-d,Palette.PlayerGlow,6,1.7f,Clock.Player); break;
            case VfxKind.EnemyDeath:
                Burst(p,Vector3.up*.4f,c,32,5.5f,Clock.World);
                Particle(Clock.World,Shape.Glow,p,Vector3.up*.2f,c,1.5f,.42f);
                Ring(new Vector3(p.x,.07f,p.z),c,2.5f,.55f,Clock.World,.35f); break;
            case VfxKind.PlayerHurt:
                Burst(p,d,Palette.Heart,16,4,Clock.Player);
                Particle(Clock.Player,Shape.Glow,p,Vector3.zero,Palette.Heart,1.1f,.18f); break;
            case VfxKind.PlayerDeath:
                Burst(p,Vector3.up,Palette.PlayerGlow,48,5.5f,Clock.Death);
                Ring(new Vector3(p.x,.08f,p.z),Palette.PlayerGlow,6f,.85f,Clock.Death,.7f);
                Particle(Clock.Death,Shape.Glow,p,Vector3.zero,Palette.PlayerGlow,2.4f,.65f); break;
            case VfxKind.Shard:
            case VfxKind.Heal:
                c = kind==VfxKind.Heal ? Palette.Heart : Palette.Shard;
                for(int i=0;i<Count(12);i++) Particle(Clock.Player,Shape.Glow,p,Radial()*.6f+Vector3.up*1.8f,c,Range(.12f,.25f),.55f);
                if(kind==VfxKind.Heal) Ring(new Vector3(p.x,.06f,p.z),c,2.4f,.55f,Clock.Player,.45f);
                break;
            case VfxKind.Arrival:
            case VfxKind.Descent:
                c = kind==VfxKind.Arrival ? Palette.PlayerGlow : Palette.Exit;
                Ring(p,c,4.5f,.7f,Clock.Player,.55f);
                for(int i=0;i<Count(24);i++) { Vector3 radial=Radial(); Particle(Clock.Player,Shape.Shard,p+radial*.65f+Vector3.up*.3f,Vector3.up*Range(1,3),c,.1f,.7f); }
                break;
            case VfxKind.Muzzle:
                Burst(p,d,Palette.Arrow,8,2.5f,Clock.World);
                Particle(Clock.World,Shape.Glow,p,Vector3.zero,Palette.Arrow,.65f,.18f); break;
            case VfxKind.WallHit: Burst(p,-d,Palette.Arrow,7,2.8f,Clock.World); break;
            case VfxKind.Charge:
                for(int i=0;i<Count(6);i++) { var radial=Radial()*.6f; Particle(Clock.World,Shape.Glow,p+radial,-radial*2,c,.17f,.25f); }
                break;
        }
    }
    public static void Slash(Vector3 p, Vector3 direction, float radius, float degrees, bool hostile=false)
    {
        var v = Instance; if(v==null || !v.enabled) return;
        var c = hostile ? Palette.Melee : Palette.Blade; c.a = HUD.ReducedEffects ? .22f : .6f;
        v._arcs[v._nextArc++ % ArcCapacity].Begin(p+Vector3.up*.65f,direction,radius,degrees,
            v.Tuning.SlashWidth,v.Tuning.SlashLifetime,c,hostile?Clock.World:Clock.Player);
    }
    public static void Ambient(Vector3 p, VfxEmitter.Kind kind, float phase)
    {
        var v=Instance; var player=GameManager.PlayerTransform;
        if(v==null || player==null || (p-player.position).sqrMagnitude>v.Tuning.AmbientDistance*v.Tuning.AmbientDistance) return;
        if(HUD.ReducedEffects && kind!=VfxEmitter.Kind.Exit) return;
        switch(kind)
        {
            case VfxEmitter.Kind.Brazier:
                v.Particle(Clock.World,Shape.Glow,p+Vector3.up*.2f,new Vector3(v.Range(-.08f,.08f),v.Range(.4f,.8f),v.Range(-.08f,.08f)),Palette.Sconce,.35f,.9f);
                v.Particle(Clock.World,Shape.Shard,p+Vector3.up*.15f,Vector3.up*.8f,Palette.Sconce,.055f,1.1f); break;
            case VfxEmitter.Kind.Exit:
                var offset=new Vector3(Mathf.Cos(phase)*.85f,.13f,Mathf.Sin(phase)*.85f);
                v.Particle(Clock.Player,Shape.Glow,p+offset,Vector3.up*.5f,Palette.Exit,.2f,1.3f); break;
            default:
                v.Particle(Clock.World,Shape.Glow,p+new Vector3(v.Range(-.3f,.3f),.05f,v.Range(-.3f,.3f)),Vector3.up*.3f,
                    kind==VfxEmitter.Kind.Shard?Palette.Shard:Palette.Heart,.13f,.65f); break;
        }
    }
    private sealed class Arc
    {
        public Clock TimeDomain;
        public bool Active => _renderer.enabled;
        private readonly Mesh _mesh; private readonly MeshRenderer _renderer;
        private readonly Vector3[] _vertices = new Vector3[66]; private readonly Color[] _colors = new Color[66];
        private float _age,_duration; private Color _color;
        public Arc(Transform parent,Material material)
        {
            var go=new GameObject("Pooled slash",typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(parent,false);
            _mesh=new Mesh {name="Slash ribbon"}; _mesh.MarkDynamic(); go.GetComponent<MeshFilter>().sharedMesh=_mesh;
            _renderer=go.GetComponent<MeshRenderer>(); _renderer.sharedMaterial=material; _renderer.shadowCastingMode=ShadowCastingMode.Off; _renderer.receiveShadows=false;
            var uv=new Vector2[66]; var triangles=new int[32*6];
            for(int i=0;i<=32;i++) { uv[i*2]=new Vector2(i/32f,0); uv[i*2+1]=new Vector2(i/32f,1); }
            for(int i=0;i<32;i++) { int k=i*6,j=i*2; triangles[k]=j;triangles[k+1]=j+1;triangles[k+2]=j+2;triangles[k+3]=j+1;triangles[k+4]=j+3;triangles[k+5]=j+2; }
            _mesh.vertices=_vertices; _mesh.uv=uv; _mesh.triangles=triangles; Clear();
        }
        public void Begin(Vector3 p,Vector3 direction,float radius,float degrees,float width,float lifetime,Color color,Clock clock)
        {
            float angle=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
            for(int i=0;i<=32;i++)
            {
                float a=(angle-degrees*.5f+degrees*i/32f)*Mathf.Deg2Rad; var d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                float r=radius;
                if(Physics.Raycast(p,d,out var hit,radius,Layers.WallMask,QueryTriggerInteraction.Ignore)) r=Mathf.Max(0,hit.distance-.05f);
                _vertices[i*2]=p+d*Mathf.Max(0,r-width); _vertices[i*2+1]=p+d*r;
            }
            _mesh.vertices=_vertices; _mesh.RecalculateBounds(); _color=color; TimeDomain=clock; _age=0; _duration=lifetime; _renderer.enabled=true; Tick(0);
        }
        public void Tick(float dt)
        {
            if(!Active)return; _age+=dt; if(_age>=_duration){Clear();return;}
            float t=_age/_duration;
            for(int i=0;i<=32;i++)
            {
                float u=i/32f; float reveal=Mathf.Clamp01((t*2.5f+.15f-u)*8);
                var c=_color; c.a*=Mathf.Sin(u*Mathf.PI)*reveal*(1-t);
                _colors[i*2]=_colors[i*2+1]=c;
            }
            _mesh.colors=_colors;
        }
        public void Clear()=>_renderer.enabled=false;
        public void Dispose()=>Object.Destroy(_mesh);
    }
}
