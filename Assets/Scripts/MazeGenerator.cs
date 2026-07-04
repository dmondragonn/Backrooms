using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

public class MazeGenerator : MonoBehaviour
{
    // ── Flag estático: enemySpawner y horrorDirector esperan esto ────────────
    public static bool NavMeshReady = false;

    [Header("Grid")]
    public int width = 42;
    public int height = 42;
    public float cellSize = 4f;
    public float wallHeight = 3f;
    public int seed = 0;
    public bool buildCeiling = true;
    public int ceilingTilesPerCell = 2;
    public int buildChunkCols = 4;
    public bool spawnPlayer = true;
    public bool elevatorIntro = true;

    [Header("Wall/beam texture (optional)")]
    public Texture2D wallTexture;
    public Vector2 wallTextureTiling = new Vector2(2f, 2f);

    [Header("Backrooms colors")]
    public Color wallColor    = new Color(0.76f, 0.70f, 0.50f);
    public Color floorColor   = new Color(0.42f, 0.36f, 0.24f);
    public Color ceilingColor = new Color(0.85f, 0.82f, 0.70f);
    public Color lightColor   = new Color(1f, 0.97f, 0.85f);

    [Header("Lights")]
    public float lightSpacing = 1.5f;
    public float realLightSpacing = 3f;
    public float lightIntensity = 1.8f;
    public float lightRange = 12f;

    [Header("Rooms")]
    public int roomCount = 6;
    public int roomMaxSize = 4;
    public bool addPillars = true;

    [Header("Void")]
    [Range(0f, 1f)] public float voidRoomChance = 0.4f;
    public float beamWidth = 0.3f;
    public float beamHeight = 0.5f;
    public float voidDepth = 8f;

    [Header("Player Audio")]
    public AudioClip[] playerFootstepClips;

    Material wallMat, floorMat, ceilMat, lightMat, darkMat, metalMat, buttonMat, panelMat, labelMat;
    Transform geo;
    bool[,] wallN, wallE, wallS, wallW;
    bool[,] visited;
    bool[,] isRoom, isVoid;
    System.Random rng;

    void Start()
    {
        NavMeshReady = false; // Resetear al iniciar
        Generate();
    }

    public void Generate()
    {
        rng = seed == 0 ? new System.Random() : new System.Random(seed);
        MakeMaterials();
        SetupPostFX();
        CarveMaze();
        StartCoroutine(BuildRoutine());
    }

    void SetupPostFX()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.3f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.75f);
        bloom.tint.Override(lightColor);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);

        var grade = profile.Add<ColorAdjustments>(true);
        grade.postExposure.Override(0.2f);
        grade.contrast.Override(12f);
        grade.saturation.Override(-8f);
        grade.colorFilter.Override(new Color(1f, 0.95f, 0.78f));

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.38f);
        vig.smoothness.Override(0.45f);

        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium1);
        grain.intensity.Override(0.22f);

        var go = new GameObject("PostFX");
        go.transform.SetParent(transform);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.priority = 10;
        v.sharedProfile = profile;
    }

    void MakeMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");

        if (wallTexture == null) wallTexture = Resources.Load<Texture2D>("BackroomsWall2");
        var floorTex = Resources.Load<Texture2D>("BackroomsWall");

        wallMat = Lit(shader, wallColor);
        Apply(wallMat, wallTexture);

        floorMat = Lit(shader, floorColor);
        Apply(floorMat, floorTex);

        ceilMat = Lit(shader, Color.white);
        ceilMat.mainTexture = GridTexture(ceilingColor, new Color(0.5f, 0.5f, 0.52f));
        ceilMat.mainTextureScale = new Vector2(ceilingTilesPerCell, ceilingTilesPerCell);
        darkMat = Lit(shader, new Color(0.02f, 0.02f, 0.02f));
        metalMat = Lit(shader, new Color(0.34f, 0.34f, 0.36f));
        metalMat.SetFloat("_Smoothness", 0.55f);
        buttonMat = Lit(shader, new Color(0.62f, 0.62f, 0.64f));
        buttonMat.SetFloat("_Smoothness", 0.4f);
        panelMat = Lit(shader, new Color(0.48f, 0.48f, 0.50f));
        panelMat.SetFloat("_Smoothness", 0.6f);

        lightMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lightMat.SetColor("_BaseColor", lightColor * 2.5f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.035f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.04f, 0.04f, 0.035f);
        RenderSettings.fogStartDistance = 14f;
        RenderSettings.fogEndDistance = 38f;

        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.enabled = false;
    }

    void Apply(Material m, Texture2D tex)
    {
        if (tex == null) return;
        m.mainTexture = tex;
        m.mainTextureScale = wallTextureTiling;
        m.SetColor("_BaseColor", Color.white);
    }

    Texture2D GridTexture(Color fill, Color line)
    {
        const int S = 64, b = 2;
        var t = new Texture2D(S, S);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = (x < b || y < b || x >= S - b || y >= S - b) ? line : fill;
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.Apply();
        return t;
    }

    Material Lit(Shader s, Color c)
    {
        var m = new Material(s);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.05f);
        return m;
    }

    void CarveMaze()
    {
        wallN = Filled(); wallE = Filled(); wallS = Filled(); wallW = Filled();
        visited = new bool[width, height];

        var stack = new Stack<Vector2Int>();
        var cur = new Vector2Int(0, 0);
        visited[cur.x, cur.y] = true;
        stack.Push(cur);

        while (stack.Count > 0)
        {
            cur = stack.Peek();
            var next = UnvisitedNeighbour(cur);
            if (next.x < 0) { stack.Pop(); continue; }

            RemoveWallBetween(cur, next);
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        CarveRooms();

        if (elevatorIntro && height > 1)
            RemoveWallBetween(new Vector2Int(0, 0), new Vector2Int(0, 1));
    }

    void CarveRooms()
    {
        isRoom = new bool[width, height];
        isVoid = new bool[width, height];
        for (int i = 0; i < roomCount; i++)
        {
            int rw = rng.Next(2, roomMaxSize + 1);
            int rh = rng.Next(2, roomMaxSize + 1);
            int rx = rng.Next(0, Mathf.Max(1, width - rw));
            int ry = rng.Next(0, Mathf.Max(1, height - rh));
            bool voidRoom = rng.NextDouble() < voidRoomChance;

            for (int x = rx; x < rx + rw && x < width; x++)
                for (int y = ry; y < ry + rh && y < height; y++)
                {
                    isRoom[x, y] = true;
                    if (voidRoom && !(x == 0 && y == 0)) isVoid[x, y] = true;
                    if (x + 1 < rx + rw && x + 1 < width)  RemoveWallBetween(new Vector2Int(x, y), new Vector2Int(x + 1, y));
                    if (y + 1 < ry + rh && y + 1 < height) RemoveWallBetween(new Vector2Int(x, y), new Vector2Int(x, y + 1));
                }
        }
    }

    bool[,] Filled()
    {
        var a = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++) a[x, y] = true;
        return a;
    }

    Vector2Int UnvisitedNeighbour(Vector2Int c)
    {
        var opts = new List<Vector2Int>();
        if (c.y + 1 < height && !visited[c.x, c.y + 1]) opts.Add(new Vector2Int(c.x, c.y + 1));
        if (c.x + 1 < width  && !visited[c.x + 1, c.y]) opts.Add(new Vector2Int(c.x + 1, c.y));
        if (c.y - 1 >= 0     && !visited[c.x, c.y - 1]) opts.Add(new Vector2Int(c.x, c.y - 1));
        if (c.x - 1 >= 0     && !visited[c.x - 1, c.y]) opts.Add(new Vector2Int(c.x - 1, c.y));
        if (opts.Count == 0) return new Vector2Int(-1, -1);
        return opts[rng.Next(opts.Count)];
    }

    void RemoveWallBetween(Vector2Int a, Vector2Int b)
    {
        if (b.y > a.y)      { wallN[a.x, a.y] = false; wallS[b.x, b.y] = false; }
        else if (b.y < a.y) { wallS[a.x, a.y] = false; wallN[b.x, b.y] = false; }
        else if (b.x > a.x) { wallE[a.x, a.y] = false; wallW[b.x, b.y] = false; }
        else                 { wallW[a.x, a.y] = false; wallE[b.x, b.y] = false; }
    }

    IEnumerator BuildRoutine()
    {
        geo = new GameObject("Geometry").transform;
        geo.SetParent(transform);
        geo.localPosition = Vector3.zero;

        var postNodes = new HashSet<Vector2Int>();
        var lampX = AxisMarks(width,  lightSpacing);
        var lampY = AxisMarks(height, lightSpacing);
        var litX  = AxisMarks(width,  realLightSpacing);
        var litY  = AxisMarks(height, realLightSpacing);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 c = CellCenter(x, y);

                if (isVoid[x, y]) BeamFloor(c);
                else Tile(c, 0f, floorMat);
                if (buildCeiling) DrywallCeiling(c);

                if (addPillars && isRoom[x, y] && !isVoid[x, y] && x % 2 == 0 && y % 2 == 0)
                    Pillar(c);

                if (wallN[x, y]) { Wall(c + Vector3.forward * cellSize / 2f, false); Node(postNodes, x, y + 1); Node(postNodes, x + 1, y + 1); }
                if (wallW[x, y]) { Wall(c + Vector3.left    * cellSize / 2f, true);  Node(postNodes, x, y);     Node(postNodes, x, y + 1); }
                if (y == 0 && wallS[x, y])         { Wall(c + Vector3.back  * cellSize / 2f, false); Node(postNodes, x, y);     Node(postNodes, x + 1, y); }
                if (x == width - 1 && wallE[x, y]) { Wall(c + Vector3.right * cellSize / 2f, true);  Node(postNodes, x + 1, y); Node(postNodes, x + 1, y + 1); }

                if (buildCeiling && lampX.Contains(x) && lampY.Contains(y))
                    LampFixture(c, litX.Contains(x) && litY.Contains(y));
            }

            if (buildChunkCols > 0 && x % buildChunkCols == 0)
                yield return null;
        }

        foreach (var n in postNodes) CornerPost(n);
        CatchFloor();

        // Bake NavMesh
        var surface = GetComponent<NavMeshSurface>();
        if (surface != null)
        {
            surface.BuildNavMesh();
            NavMeshReady = true;
            Debug.Log("[MazeGenerator] NavMesh bakeado. Enemigo puede activarse.");
        }
        else
        {
            Debug.LogWarning("[MazeGenerator] No hay NavMeshSurface en este GameObject.");
        }

        if (spawnPlayer) SpawnPlayer();
    }

    void Node(HashSet<Vector2Int> set, int i, int j) => set.Add(new Vector2Int(i, j));

    HashSet<int> AxisMarks(int n, float spacing)
    {
        var s = new HashSet<int>();
        if (spacing <= 0) return s;
        for (float k = 0; ; k++)
        {
            int idx = Mathf.RoundToInt(k * spacing);
            if (idx >= n) break;
            s.Add(idx);
        }
        return s;
    }

    void CornerPost(Vector2Int n)
    {
        Vector3 pos = transform.position + new Vector3(n.x * cellSize - cellSize / 2f, wallHeight / 2f, n.y * cellSize - cellSize / 2f);
        Slab(pos, new Vector3(0.2f, wallHeight, 0.2f), wallMat);
    }

    void CatchFloor()
    {
        Vector3 mid = transform.position + new Vector3((width - 1) * cellSize / 2f, 0f, (height - 1) * cellSize / 2f);
        float w = width * cellSize, d = height * cellSize;
        float t = 0.2f;

        Slab(mid + Vector3.down * voidDepth, new Vector3(w, 0.2f, d), darkMat);
        Slab(mid + new Vector3(0,      -voidDepth / 2f,  d / 2f), new Vector3(w, voidDepth, t), darkMat);
        Slab(mid + new Vector3(0,      -voidDepth / 2f, -d / 2f), new Vector3(w, voidDepth, t), darkMat);
        Slab(mid + new Vector3( w / 2f,-voidDepth / 2f, 0),       new Vector3(t, voidDepth, d), darkMat);
        Slab(mid + new Vector3(-w / 2f,-voidDepth / 2f, 0),       new Vector3(t, voidDepth, d), darkMat);
    }

    void SpawnPlayer()
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p.name = "Player";
        Destroy(p.GetComponent<Collider>());
        p.transform.position = CellCenter(0, 0) + Vector3.up * 1.1f;
        var playerComp = p.AddComponent<SimplePlayer>();
        playerComp.footstepClips = playerFootstepClips;

        // Asignar jugador al spawner automáticamente
        var spawner = FindFirstObjectByType<enemySpawner>();
        if (spawner != null)
        {
            spawner.player       = p.transform;
            spawner.playerCamera = p.GetComponentInChildren<Camera>();
            Debug.Log("[MazeGenerator] Jugador asignado al enemySpawner.");
        }

        // Asignar jugador a enemyLogic también por si acaso
        var logic = FindFirstObjectByType<enemyLogic>();
        if (logic != null)
        {
            logic.player = p.transform;
            Debug.Log("[MazeGenerator] Jugador asignado al enemyLogic.");
        }

        if (elevatorIntro) BuildElevator(CellCenter(0, 0), playerComp);
    }

    void BuildElevator(Vector3 center, SimplePlayer player)
    {
        float inset = 0.2f;
        float cs = cellSize, h = wallHeight - 0.12f;
        float half = cs / 2f - inset;

        var ele = new GameObject("Elevator");
        ele.transform.SetParent(transform);
        ele.transform.position = center;

        Box(ele.transform, new Vector3(0, 0.06f, 0),      new Vector3(half * 2, 0.1f, half * 2), metalMat);
        Box(ele.transform, new Vector3(0, h, 0),           new Vector3(half * 2, 0.1f, half * 2), metalMat);
        Box(ele.transform, new Vector3(0, h / 2f, -half),  new Vector3(half * 2, h, 0.15f), metalMat);
        Box(ele.transform, new Vector3(-half, h / 2f, 0),  new Vector3(0.15f, h, half * 2), metalMat);
        Box(ele.transform, new Vector3( half, h / 2f, 0),  new Vector3(0.15f, h, half * 2), metalMat);

        int nPanels = 2;
        float pw = half / nPanels;
        var leftPanels  = new Transform[nPanels];
        var rightPanels = new Transform[nPanels];
        var panelSlides = new float[nPanels];
        for (int i = 0; i < nPanels; i++)
        {
            float pz = half + i * 0.13f;
            leftPanels[i]  = Box(ele.transform, new Vector3(-(i + 0.5f) * pw, h / 2f, pz), new Vector3(pw, h, 0.1f), metalMat).transform;
            rightPanels[i] = Box(ele.transform, new Vector3( (i + 0.5f) * pw, h / 2f, pz), new Vector3(pw, h, 0.1f), metalMat).transform;
            panelSlides[i] = (nPanels - 1 - i) * pw;
        }

        float px = half - 0.082f;
        float cz = half * 0.62f;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        Color red  = new Color(1f, 0.15f, 0.05f);
        Color dark = new Color(0.08f, 0.08f, 0.08f);
        float zL = cz - 0.09f, zR = cz + 0.09f;

        Box(ele.transform, new Vector3(px,          1.40f, cz), new Vector3(0.012f, 1.22f, 0.46f), darkMat,  false);
        Box(ele.transform, new Vector3(px - 0.008f, 1.40f, cz), new Vector3(0.01f,  1.14f, 0.40f), panelMat, false);
        Box(ele.transform, new Vector3(px - 0.015f, 1.86f, cz), new Vector3(0.008f, 0.16f, 0.26f), darkMat,  false);

        var tm = Label(ele.transform, new Vector3(px - 0.022f, 1.86f, cz), "2", red, 0.01f, font);

        PanelButton(ele.transform, px, 1.62f, zL, "▶◀",  dark, font, 0.006f);
        PanelButton(ele.transform, px, 1.62f, zR, "◀ ▶", dark, font, 0.006f);

        string[] leftLabels  = { "B", "2", "4", "6" };
        string[] rightLabels = { "1", "3", "5", "7" };
        for (int r = 0; r < 4; r++)
        {
            float y = 0.95f + r * 0.16f;
            FloorButton(ele.transform, px, y, zL, leftLabels[r],  dark, font, tm);
            FloorButton(ele.transform, px, y, zR, rightLabels[r], dark, font, tm);
        }

        var lgt = new GameObject("ElevatorLight");
        lgt.transform.SetParent(ele.transform);
        lgt.transform.localPosition = new Vector3(0, h - 0.3f, 0);
        var l = lgt.AddComponent<Light>();
        l.type = LightType.Point; l.color = lightColor; l.intensity = 1.6f; l.range = cs * 2f; l.shadows = LightShadows.None;

        var intro = ele.AddComponent<ElevatorIntro>();
        intro.player      = player;
        intro.leftPanels  = leftPanels;
        intro.rightPanels = rightPanels;
        intro.panelSlides = panelSlides;
        intro.display     = tm;
    }

    GameObject Box(Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collide = true)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.transform.SetParent(parent, false);
        b.transform.localPosition = localPos;
        b.transform.localScale = scale;
        b.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collide) Destroy(b.GetComponent<Collider>());
        return b;
    }

    TextMesh Label(Transform parent, Vector3 lp, string txt, Color col, float size, Font font)
    {
        var g = new GameObject("Label");
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localRotation = Quaternion.Euler(0, -90, 0);
        var t = g.AddComponent<TextMesh>();
        t.text = txt; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
        t.characterSize = size; t.fontSize = 90; t.color = col;
        if (font != null)
        {
            t.font = font;
            var rend = g.GetComponent<MeshRenderer>();
            if (labelMat != null) { labelMat.mainTexture = font.material.mainTexture; rend.material = labelMat; }
            else rend.material = font.material;
        }
        return t;
    }

    void PanelButton(Transform parent, float px, float y, float z, string label, Color labelCol, Font font, float labelSize = 0.011f)
    {
        Box(parent, new Vector3(px - 0.015f, y, z), new Vector3(0.008f, 0.12f, 0.13f), buttonMat, false);
        if (!string.IsNullOrEmpty(label))
            Label(parent, new Vector3(px - 0.022f, y, z), label, labelCol, labelSize, font);
    }

    void FloorButton(Transform parent, float px, float y, float z, string label, Color labelCol, Font font, TextMesh display, float labelSize = 0.011f)
    {
        var button = Box(parent, new Vector3(px - 0.015f, y, z), new Vector3(0.008f, 0.12f, 0.13f), buttonMat, true);
        if (!string.IsNullOrEmpty(label))
            Label(parent, new Vector3(px - 0.022f, y, z), label, labelCol, labelSize, font);

        var elevatorButton = button.AddComponent<ElevatorButton>();
        elevatorButton.display = display;
        elevatorButton.value = label;
    }

    Vector3 CellCenter(int x, int y) =>
        transform.position + new Vector3(x * cellSize, 0f, y * cellSize);

    void Tile(Vector3 center, float yOffset, Material mat)
    {
        Slab(center + Vector3.up * (yOffset + (yOffset == 0 ? -0.05f : 0.05f)),
             new Vector3(cellSize, 0.1f, cellSize), mat);
    }

    void BeamFloor(Vector3 center)
    {
        Vector3 p = center + Vector3.down * beamHeight / 2f;
        Slab(p, new Vector3(cellSize, beamHeight, beamWidth), floorMat);
        Slab(p, new Vector3(beamWidth, beamHeight, cellSize), floorMat);
    }

    void Pillar(Vector3 center)
    {
        Slab(center + Vector3.up * wallHeight / 2f,
             new Vector3(cellSize * 0.3f, wallHeight, cellSize * 0.3f), wallMat);
    }

    void Wall(Vector3 pos, bool alongZ)
    {
        Vector3 scale = alongZ ? new Vector3(0.2f, wallHeight, cellSize)
                               : new Vector3(cellSize, wallHeight, 0.2f);
        Slab(pos + Vector3.up * wallHeight / 2f, scale, wallMat);
    }

    GameObject Slab(Vector3 pos, Vector3 scale, Material mat, bool collide = true)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.transform.SetParent(geo);
        b.transform.position = pos;
        b.transform.localScale = scale;
        b.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collide) Destroy(b.GetComponent<Collider>());
        return b;
    }

    void DrywallCeiling(Vector3 c)
    {
        Slab(c + Vector3.up * wallHeight, new Vector3(cellSize, 0.1f, cellSize), ceilMat, false);
    }

    void LampFixture(Vector3 c, bool realLight)
    {
        Slab(c + Vector3.up * (wallHeight - 0.05f),
             new Vector3(cellSize * 0.15f, 0.05f, cellSize * 0.15f), lightMat, false);

        if (!realLight) return;
        var go = new GameObject("Light");
        go.transform.SetParent(transform);
        go.transform.position = c + Vector3.up * (wallHeight - 0.3f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = lightColor;
        l.intensity = lightIntensity;
        l.range = lightRange;
        l.shadows = LightShadows.None;
    }
}