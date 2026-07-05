using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

// Level 3: rebuilds the REAL Rec Room "backstage" map from baked gltf box data
// (one cube per mesh node, real position/scale). Clean materials, NOT a maze.
//   Level3Data.json = corridor map (built at origin)
//   HintsData.json  = goal room, built under a movable "HintsRoot" so you can
//                     drag/rotate it in the Scene to dock it onto the corridor exit.
// Right-click the component -> "Generate" to build in edit mode (no Play needed).
[ExecuteAlways]
public class Level3Generator : MonoBehaviour
{
    [Header("Data")]
    public string levelData = "Level3Data";
    public string hintsData = "HintsData";
    public bool includeHints = true;
    public float worldScale = 1f;

    [Header("Player Audio")]
    public AudioClip[] playerFootstepClips;

    [Header("Music")]
    public bool playMusic = true;
    public string musicResource = "Level3Music"; // Resources/Level3Music.mp3
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("Hints placement (drag HintsRoot in Scene to fit)")]
    // model has openings only on its long sides + closed X ends. To make the hall run
    // IN LINE with the corridor, rotate 90 and cut open the +X end so you enter lengthwise.
    public bool openHintsEnd = true;
    public bool openPlusXEnd = false;      // false = open the -X end (connect the other end)
    public float hintsOpenXMax = 64f;      // if +X: delete boxes x >= this
    public float hintsOpenXMin = -12f;     // if -X: delete boxes x <= this
    public Vector3 hintsDock = new Vector3(63.458f, 45.5f, -217.5f);  // world spot for the opened end
    public bool bridgeGap = true;          // floor patch over the seam so player can't fall
    public bool autoRegen = true;          // rebuild live when you tweak values in edit mode

    [Header("Open the corridor exit")]
    public bool removeCorridorWall = true;
    public Vector3 corridorAnchor = new Vector3(63.458f, 48.181f, -217.792f);
    public float removeRadius = 1f;

    [Header("Build / spawn")]
    public bool spawnPlayer = true;
    public float playerEyeUp = 1.1f;

    [Header("Look (clean, ignores model textures)")]
    public bool fog = true;
    public Color ambient = new Color(0.16f, 0.17f, 0.20f);
    public Color floorColor = new Color(0.50f, 0.50f, 0.52f);
    public Color wallColor  = new Color(0.70f, 0.70f, 0.72f);
    public Color ceilColor  = new Color(0.28f, 0.28f, 0.30f);

    [Header("Pipes (replace model rails with cylinders)")]
    public bool pipesAsCylinders = true;
    public float railThick = 0.8f;         // a box with its 2 smallest dims <= this = a beam
    public float pipeMinLen = 5f;          // beam at least this long -> becomes a cylinder
    public float pipeMinRadius = 0.09f;
    public bool pipeColliders = true;      // solid pipes (off = fewer colliders, better perf)
    public Color pipeColor = new Color(0.30f, 0.31f, 0.33f);

    [Header("Ceiling cap (model has no solid ceiling)")]
    public bool ceilingCap = true;
    public float ceilingY = 52f;

    [Header("Lights (from model's White_Neon fixtures)")]
    public bool addLights = true;
    public float lightGrid = 8f;           // one point light per this grid cell of neon
    public float lightRange = 10f;         // tighter range = less overlapping fill cost
    public float lightIntensity = 2.2f;
    public int maxLights = 70;

    [Header("Doors")]
    public bool removeDoors = true;        // delete the level door block (Brown_Particle_Board)
    public bool removeHintsText = true;    // delete hints wall text (Black_Cardboard glyphs)
    public bool hintsDoorPassable = true;  // keep the hints door visible but walk-through (no hole)

    [System.Serializable]
    public struct CutVol { public Vector3 center; public Vector3 size; }

    [Header("Cut boxes (delete ANYTHING inside - move the red gizmos onto obstructions)")]
    public bool useCutBox = true;
    public CutVol[] cutBoxes = DefaultCuts();

    static CutVol[] DefaultCuts() => new CutVol[0]; // empty; add volumes only to carve specific obstructions

    Material floorMat, wallMat, ceilMat, pipeMat, lampMat;
    Vector3 spawnPos;
    bool haveSpawn;
    float bestFloorArea;
    int neonMat = -1, doorMat = -1, hintsTextMat = -1;
    readonly HashSet<Vector3Int> lightCells = new HashSet<Vector3Int>();
    int lightCount;

    [System.Serializable] public class BMat { public string name; public float[] col; public string tex; }
    [System.Serializable] public class BBox { public float[] c; public float[] s; public int m; }
    [System.Serializable] public class BData { public BMat[] mats; public BBox[] boxes; }

    void Start() { if (Application.isPlaying) Generate(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying || !autoRegen) return;
        UnityEditor.EditorApplication.delayCall += () => { if (this != null && !Application.isPlaying) Generate(); };
    }
#endif

    [ContextMenu("Generate")]
    public void Generate()
    {
        MazeGenerator.NavMeshReady = false;
        ClearOld();
        SetupLook();
        SetupPostFX();
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        floorMat = MakeMat(lit, floorColor);
        wallMat  = MakeMat(lit, wallColor);
        ceilMat  = MakeMat(lit, ceilColor);
        pipeMat  = MakeMat(lit, pipeColor); pipeMat.SetFloat("_Smoothness", 0.45f);
        lampMat  = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lampMat.SetColor("_BaseColor", new Color(1f, 0.98f, 0.92f) * 2.6f); // HDR glow for bloom
        haveSpawn = false; bestFloorArea = 0f;
        lightCells.Clear(); lightCount = 0;

        var lvl = LoadData(levelData);
        if (lvl == null) return;
        neonMat = System.Array.FindIndex(lvl.mats, m => m.name == "White_Neon");
        doorMat = System.Array.FindIndex(lvl.mats, m => m.name == "Brown_Particle_Board");

        var geo = new GameObject("Geometry").transform;
        geo.SetParent(transform); geo.localPosition = Vector3.zero;
        // skip the wall plugging the corridor exit
        BuildData(lvl, geo, Vector3.zero, true,
            raw => removeCorridorWall && Vector3.Distance(raw, corridorAnchor) <= removeRadius);
        if (ceilingCap) BuildCeiling(geo, lvl.boxes);
        StaticBatchingUtility.Combine(geo.gameObject);

        if (includeHints)
        {
            var hints = LoadData(hintsData);
            if (hints != null)
            {
                hintsTextMat = System.Array.FindIndex(hints.mats, m => m.name == "Black_Cardboard");
                // open one X end and dock it to the corridor; the opened end faces +Z
                float rotY = openPlusXEnd ? 270f : 90f;
                Vector3 pivot = new Vector3(openPlusXEnd ? 65.7f : -13.7f, 3.69f, -0.45f) * worldScale;
                float cut = (openPlusXEnd ? hintsOpenXMax : hintsOpenXMin) * worldScale;
                var root = new GameObject("HintsRoot").transform;
                root.SetParent(transform);
                root.localPosition = hintsDock;
                root.localRotation = Quaternion.Euler(0f, rotY, 0f);
                BuildData(hints, root, pivot, false,
                    raw => openHintsEnd && (openPlusXEnd ? raw.x >= cut : raw.x <= cut));
                StaticBatchingUtility.Combine(root.gameObject);
            }
        }

        if (bridgeGap) BuildBridge();
        AddFill(spawnPos + Vector3.up * 4f, 1.4f, 30f);

        // bake NavMesh so enemies can path here (play mode only; edit regen stays fast)
        if (Application.isPlaying)
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.2f;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.BuildNavMesh();
        }

        MazeGenerator.NavMeshReady = true;
        if (playMusic && Application.isPlaying) PlayMusic();
        if (spawnPlayer && haveSpawn && Application.isPlaying) SpawnPlayer();
    }

    [ContextMenu("Reset Hints To Recommended")]
    void ResetHintsRecommended()
    {
        openHintsEnd = true;
        openPlusXEnd = false;   // open -X end
        hintsOpenXMax = 64f;
        hintsOpenXMin = -12f;
        hintsDock = new Vector3(63.458f, 45.5f, -217.5f);
        bridgeGap = true;
        Generate();
    }

    [ContextMenu("Clear")]
    void ClearOld()
    {
        var kill = new List<GameObject>();
        foreach (Transform t in transform)
            if (t.name == "Geometry" || t.name == "HintsRoot" || t.name == "PostFX" ||
                t.name == "Fill" || t.name == "Player" || t.name == "Bridge" || t.name == "Neon" || t.name == "Music")
                kill.Add(t.gameObject);
        foreach (var g in kill) { if (Application.isPlaying) Destroy(g); else DestroyImmediate(g); }
    }

    Material MakeMat(Shader s, Color c)
    {
        var m = new Material(s);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.08f);
        return m;
    }

    void SetupLook()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        RenderSettings.fog = fog;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.11f);
        RenderSettings.fogStartDistance = 12f;
        RenderSettings.fogEndDistance = 50f;
    }

    void SetupPostFX()
    {
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = p.Add<Bloom>(true); bloom.intensity.Override(0.7f); bloom.threshold.Override(1.0f);
        var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
        var grade = p.Add<ColorAdjustments>(true); grade.contrast.Override(8f); grade.saturation.Override(-6f);
        var vig = p.Add<Vignette>(true); vig.intensity.Override(0.3f);
        var grain = p.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.15f);
        var go = new GameObject("PostFX"); go.transform.SetParent(transform);
        var v = go.AddComponent<Volume>(); v.isGlobal = true; v.priority = 10; v.sharedProfile = p;
    }

    BData LoadData(string resName)
    {
        var ta = Resources.Load<TextAsset>(resName);
        if (ta == null) { Debug.LogError("Level3Generator: missing Resources/" + resName + ".json"); return null; }
        var data = JsonUtility.FromJson<BData>(ta.text);
        if (data == null || data.boxes == null) { Debug.LogError("Level3Generator: bad data " + resName); return null; }
        return data;
    }

    void Extents(BBox[] boxes, out Vector3 lo, out Vector3 hi)
    {
        lo = Vector3.one * float.MaxValue; hi = Vector3.one * float.MinValue;
        foreach (var b in boxes)
        {
            Vector3 c = new Vector3(b.c[0], b.c[1], b.c[2]) * worldScale;
            Vector3 h = new Vector3(b.s[0], b.s[1], b.s[2]) * worldScale * 0.5f;
            lo = Vector3.Min(lo, c - h); hi = Vector3.Max(hi, c + h);
        }
    }

    // cubes parented to 'parent' at (raw - pivot) local; parent transform places/rotates the set
    void BuildData(BData data, Transform parent, Vector3 pivot, bool isLevel, System.Func<Vector3, bool> skip)
    {
        if (isLevel) PickSpawn(data.boxes);

        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var b in data.boxes) { float y = b.c[1]; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        float midY = minY + (maxY - minY) * 0.5f;

        foreach (var bx in data.boxes)
        {
            Vector3 raw = new Vector3(bx.c[0], bx.c[1], bx.c[2]) * worldScale;
            if (skip != null && skip(raw)) continue;
            if (isLevel && removeDoors && doorMat >= 0 && bx.m == doorMat) continue; // door block
            if (!isLevel && removeHintsText && hintsTextMat >= 0 && bx.m == hintsTextMat) continue; // wall text
            Vector3 scl = new Vector3(bx.s[0], bx.s[1], bx.s[2]) * worldScale;
            Vector3 local = raw - pivot;

            if (useCutBox && cutBoxes != null && cutBoxes.Length > 0 &&
                InCut(parent.TransformPoint(local))) continue; // manual delete volume

            // model neon fixture -> emissive lamp box + a sparse real point light
            if (isLevel && neonMat >= 0 && bx.m == neonMat)
            {
                MakeBox(parent, local, scl, lampMat, false); // lamps: no collider
                if (addLights) TryLight(raw);
                continue;
            }

            // model rail beam (2 thin dims, 1 long) -> cylinder pipe, only in the lower
            // half (wall pipes); leave upper beams as boxes so the ceiling stays solid
            if (isLevel && pipesAsCylinders && bx.c[1] < midY &&
                IsBeam(scl, out int axis, out float len, out float rad))
            {
                RailCylinder(parent, local, axis, len, rad);
                continue;
            }

            bool flat = scl.y < scl.x && scl.y < scl.z;
            Material mat = !flat ? wallMat : (bx.c[1] > midY ? ceilMat : floorMat);
            // collider only where the player can actually hit: walls + low floors,
            // and skip tiny decorative bits. Ceiling + clutter render without colliders.
            bool collide = (!flat || bx.c[1] <= midY) && Mathf.Max(scl.x, Mathf.Max(scl.y, scl.z)) > 0.3f;
            if (!isLevel && hintsDoorPassable && InHintsDoor(raw)) collide = false; // walk through door
            MakeBox(parent, local, scl, mat, collide);
        }
    }

    // hints doorway block in raw model space (independent of dock/rotation)
    bool InHintsDoor(Vector3 raw)
    {
        Vector3 r = raw / Mathf.Max(0.0001f, worldScale);
        return r.x >= 31f && r.x <= 35.5f && r.y >= 3f && r.y <= 7.5f && r.z >= -4f && r.z <= 1f;
    }

    bool InCut(Vector3 w)
    {
        if (cutBoxes == null) return false;
        foreach (var v in cutBoxes)
        {
            Vector3 d = w - v.center;
            if (Mathf.Abs(d.x) <= v.size.x * 0.5f && Mathf.Abs(d.y) <= v.size.y * 0.5f && Mathf.Abs(d.z) <= v.size.z * 0.5f)
                return true;
        }
        return false;
    }

    [ContextMenu("Reset Cut Boxes")]
    void ResetCutBoxes() { cutBoxes = DefaultCuts(); Generate(); }

    void OnDrawGizmosSelected()
    {
        if (!useCutBox || cutBoxes == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        foreach (var v in cutBoxes) Gizmos.DrawWireCube(v.center, v.size);
    }

    void Kill(Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    // model has no solid ceiling -> duplicate the floor tiles and raise them to ceilingY
    void BuildCeiling(Transform parent, BBox[] boxes)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var b in boxes) { float y = b.c[1]; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        float midY = minY + (maxY - minY) * 0.5f;

        foreach (var b in boxes)
        {
            Vector3 scl = new Vector3(b.s[0], b.s[1], b.s[2]) * worldScale;
            bool flat = scl.y < scl.x && scl.y < scl.z;
            if (!flat || b.c[1] > midY) continue;   // floor tiles only
            Vector3 local = new Vector3(b.c[0] * worldScale, ceilingY, b.c[2] * worldScale);
            MakeBox(parent, local, scl, floorMat, false); // ceiling = floor texture, no collider
        }
    }

    void MakeBox(Transform parent, Vector3 local, Vector3 scl, Material mat, bool collide = true)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!collide) Kill(c.GetComponent<Collider>());   // drop colliders player never touches
        c.transform.SetParent(parent, false);
        c.transform.localPosition = local;
        c.transform.localScale = scl;
        c.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // beam = the two smallest dims are thin and the longest is a pipe run
    bool IsBeam(Vector3 s, out int axis, out float len, out float rad)
    {
        float sx = s.x, sy = s.y, sz = s.z;
        len = Mathf.Max(sx, Mathf.Max(sy, sz));
        axis = len == sx ? 0 : (len == sy ? 1 : 2);
        float a = Mathf.Min(sx, Mathf.Min(sy, sz));
        float mid = sx + sy + sz - len - a;
        rad = Mathf.Max(pipeMinRadius, (a + mid) * 0.25f);
        return mid <= railThick && len >= pipeMinLen;
    }

    void RailCylinder(Transform parent, Vector3 pos, int axis, float len, float rad)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        if (!pipeColliders) Kill(c.GetComponent<Collider>());
        c.transform.SetParent(parent, false);
        c.transform.localPosition = pos;
        c.transform.localRotation = axis == 0 ? Quaternion.Euler(0, 0, 90)
                                  : axis == 2 ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        c.transform.localScale = new Vector3(rad * 2f, len * 0.5f, rad * 2f);
        c.GetComponent<Renderer>().sharedMaterial = pipeMat;
    }

    // one point light per grid cell of neon fixtures, capped
    void TryLight(Vector3 raw)
    {
        Vector3 wp = raw + transform.position;
        var cell = new Vector3Int(Mathf.FloorToInt(wp.x / lightGrid),
            Mathf.FloorToInt(wp.y / lightGrid), Mathf.FloorToInt(wp.z / lightGrid));
        if (lightCount >= maxLights || !lightCells.Add(cell)) return;
        lightCount++;
        var go = new GameObject("Neon"); go.transform.SetParent(transform); go.transform.position = wp;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.97f, 0.9f);
        l.intensity = lightIntensity; l.range = lightRange; l.shadows = LightShadows.None;
    }

    // spawn = biggest flat slab in bottom third (a real floor, not a ceiling)
    void PickSpawn(BBox[] boxes)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var b in boxes) { float y = b.c[1]; if (y < minY) minY = y; if (y > maxY) maxY = y; }
        float lowCut = minY + (maxY - minY) * 0.34f;

        foreach (var b in boxes)
        {
            float sx = b.s[0] * worldScale, sy = b.s[1] * worldScale, sz = b.s[2] * worldScale;
            if (sy >= sx || sy >= sz) continue;
            if (b.c[1] > lowCut) continue;
            float foot = sx * sz;
            if (foot <= bestFloorArea) continue;
            bestFloorArea = foot;
            Vector3 pos = new Vector3(b.c[0], b.c[1], b.c[2]) * worldScale + transform.position;
            spawnPos = pos + Vector3.up * (sy * 0.5f + playerEyeUp);
            haveSpawn = true;
        }
    }

    // floor slab bridging corridor exit -> hints opened end, so no fall-through gap
    void BuildBridge()
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "Bridge";
        b.transform.SetParent(transform);
        b.transform.position = new Vector3(corridorAnchor.x, hintsDock.y - 0.1f,
            (corridorAnchor.z + hintsDock.z) * 0.5f);
        float zlen = Mathf.Abs(corridorAnchor.z - hintsDock.z) + 6f;
        b.transform.localScale = new Vector3(7f, 0.2f, zlen);
        b.GetComponent<Renderer>().sharedMaterial = floorMat;
    }

    void AddFill(Vector3 pos, float intensity, float range)
    {
        var go = new GameObject("Fill"); go.transform.SetParent(transform);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(0.9f, 0.93f, 1f);
        l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
    }

    // looping 2D background music for the level
    void PlayMusic()
    {
        var clip = Resources.Load<AudioClip>(musicResource);
        if (clip == null) { Debug.LogWarning("Level3Generator: missing Resources/" + musicResource); return; }
        var go = new GameObject("Music"); go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.loop = true; src.volume = musicVolume;
        src.spatialBlend = 0f; // 2D, plays everywhere
        src.playOnAwake = false;
        src.Play();
    }

    void SpawnPlayer()
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p.name = "Player";
        Destroy(p.GetComponent<Collider>());
        p.transform.position = spawnPos;
        var playerComp = p.AddComponent<SimplePlayer>();
        playerComp.footstepClips = playerFootstepClips;
    }
}
