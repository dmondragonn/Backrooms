using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Backrooms "Level Fun" generator: white walls with doodles, red spotted carpet,
// wide pillared halls and enclosed rooms, party tables with balloons, bright
// ambient. Standalone (does not touch MazeGenerator). Drop on an empty object.
public class Level2Generator : MonoBehaviour
{
    [Header("Grid")]
    public int width = 42;
    public int height = 42;
    public float cellSize = 4f;
    public float wallHeight = 3f;
    public int seed = 0;
    public bool buildCeiling = true;
    public int buildChunkCols = 2;
    public bool spawnPlayer = true;

    [Header("Colors")]
    public Color wallColor    = new Color(0.92f, 0.90f, 0.84f); // off-white walls
    public Color ceilingColor = new Color(0.95f, 0.95f, 0.95f); // white ceiling
    public Color carpetColor  = new Color(0.55f, 0.07f, 0.07f); // red carpet base
    public Color lightColor   = new Color(1f, 0.98f, 0.92f);
    public Color ambient      = new Color(0.32f, 0.31f, 0.30f); // bright, airy level

    [Header("Lights")]
    public float lightSpacing = 1.5f;
    public float realLightSpacing = 3f;
    public float lightIntensity = 1.6f;
    public float lightRange = 12f;

    [Header("Rooms")]
    public int roomCount = 14;                 // lots of wide areas
    public int roomMaxSize = 6;
    [Range(0f, 1f)] public float enclosedChance = 0.35f; // mostly open halls vs closed rooms
    public bool addPillars = true;
    public bool addTables = true;

    [Header("Wall doodles")]
    [Range(0f, 1f)] public float doodleChance = 0.06f; // chance a wall gets a colored drawing

    Material wallMat, floorMat, ceilMat, lightMat, legMat, clothMat;
    Material[] partyMats;                       // balloons / doodles
    Transform geo;
    bool[,] wallN, wallE, wallS, wallW, visited, isRoom, isOpen;
    List<Vector2Int> tableCells = new List<Vector2Int>();
    System.Random rng;

    void Start() => Generate();

    public void Generate()
    {
        rng = seed == 0 ? new System.Random() : new System.Random(seed);
        MakeMaterials();
        SetupPostFX();
        Carve();
        StartCoroutine(BuildRoutine());
    }

    // ---------------- materials ----------------
    void MakeMaterials()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");

        wallMat = Lit(lit, wallColor);
        ceilMat = Lit(lit, ceilingColor);
        legMat  = Lit(lit, new Color(0.1f, 0.1f, 0.1f));   // black table legs
        clothMat = Lit(lit, new Color(0.95f, 0.95f, 0.97f)); // white tablecloth

        floorMat = Lit(lit, Color.white);
        floorMat.mainTexture = CarpetTexture();
        floorMat.mainTextureScale = Vector2.one;           // one carpet tile per cell

        lightMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lightMat.SetColor("_BaseColor", lightColor * 2.2f); // HDR for bloom

        partyMats = new[]
        {
            Lit(lit, new Color(0.9f, 0.2f, 0.2f)),  // red
            Lit(lit, new Color(0.95f, 0.8f, 0.2f)), // yellow
            Lit(lit, new Color(0.2f, 0.4f, 0.9f)),  // blue
            Lit(lit, new Color(0.2f, 0.7f, 0.3f)),  // green
            Lit(lit, new Color(0.7f, 0.3f, 0.8f)),  // purple
            Lit(lit, new Color(0.95f, 0.5f, 0.15f)) // orange
        };

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;            // brighter than level 1
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.5f, 0.48f, 0.45f);
        RenderSettings.fogStartDistance = 20f;
        RenderSettings.fogEndDistance = 45f;

        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.enabled = false;
    }

    Material Lit(Shader s, Color c)
    {
        var m = new Material(s);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.05f);
        return m;
    }

    // red carpet with scattered colored blobs
    Texture2D CarpetTexture()
    {
        const int S = 128;
        var t = new Texture2D(S, S);
        var px = new Color[S * S];
        for (int i = 0; i < px.Length; i++) px[i] = carpetColor;

        Color[] blobCols =
        {
            new Color(0.2f, 0.45f, 0.85f), new Color(0.9f, 0.75f, 0.2f),
            new Color(0.85f, 0.5f, 0.2f),  new Color(0.3f, 0.6f, 0.75f),
            new Color(0.7f, 0.2f, 0.2f)
        };
        for (int b = 0; b < 110; b++)
        {
            int cx = rng.Next(S), cy = rng.Next(S), r = rng.Next(3, 9);
            Color col = blobCols[rng.Next(blobCols.Length)];
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (dx * dx + dy * dy <= r * r)
                        px[((cy + dy + S) % S) * S + ((cx + dx + S) % S)] = col;
        }
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.Apply();
        return t;
    }

    // ---------------- post fx ----------------
    void SetupPostFX()
    {
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = p.Add<Bloom>(true);
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(0.95f);
        var tone = p.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
        var grade = p.Add<ColorAdjustments>(true);
        grade.saturation.Override(12f);          // pop the party colors
        grade.contrast.Override(6f);
        var vig = p.Add<Vignette>(true);
        vig.intensity.Override(0.2f);

        var go = new GameObject("PostFX");
        go.transform.SetParent(transform);
        var v = go.AddComponent<Volume>();
        v.isGlobal = true; v.priority = 10; v.sharedProfile = p;
    }

    // ---------------- maze + rooms ----------------
    void Carve()
    {
        wallN = Filled(); wallE = Filled(); wallS = Filled(); wallW = Filled();
        visited = new bool[width, height];
        isRoom = new bool[width, height];
        isOpen = new bool[width, height];

        var stack = new Stack<Vector2Int>();
        var cur = new Vector2Int(0, 0);
        visited[cur.x, cur.y] = true;
        stack.Push(cur);
        while (stack.Count > 0)
        {
            cur = stack.Peek();
            var nx = Unvisited(cur);
            if (nx.x < 0) { stack.Pop(); continue; }
            SetWall(cur, nx, false);
            visited[nx.x, nx.y] = true;
            stack.Push(nx);
        }
        CarveRooms();
    }

    void CarveRooms()
    {
        for (int i = 0; i < roomCount; i++)
        {
            int rw = rng.Next(3, roomMaxSize + 1);
            int rh = rng.Next(3, roomMaxSize + 1);
            int rx = rng.Next(0, Mathf.Max(1, width - rw));
            int ry = rng.Next(0, Mathf.Max(1, height - rh));
            bool open = rng.NextDouble() > enclosedChance; // open hall vs enclosed room

            for (int x = rx; x < rx + rw && x < width; x++)
                for (int y = ry; y < ry + rh && y < height; y++)
                {
                    isRoom[x, y] = true;
                    isOpen[x, y] = open;
                    if (x + 1 < rx + rw && x + 1 < width) SetWall(new Vector2Int(x, y), new Vector2Int(x + 1, y), false);
                    if (y + 1 < ry + rh && y + 1 < height) SetWall(new Vector2Int(x, y), new Vector2Int(x, y + 1), false);
                }

            if (!open) EncloseRoom(rx, ry, rw, rh); // walls all around + one doorway
            if (addTables && !(rx == 0 && ry == 0))
                tableCells.Add(new Vector2Int(Mathf.Min(rx + rw / 2, width - 1), Mathf.Min(ry + rh / 2, height - 1)));
        }
    }

    // close the room perimeter, then knock a single doorway into a carved neighbour
    void EncloseRoom(int rx, int ry, int rw, int rh)
    {
        int x1 = Mathf.Min(rx + rw - 1, width - 1), y1 = Mathf.Min(ry + rh - 1, height - 1);
        for (int x = rx; x <= x1; x++)
        {
            if (ry > 0) SetWall(new Vector2Int(x, ry), new Vector2Int(x, ry - 1), true);
            if (y1 + 1 < height) SetWall(new Vector2Int(x, y1), new Vector2Int(x, y1 + 1), true);
        }
        for (int y = ry; y <= y1; y++)
        {
            if (rx > 0) SetWall(new Vector2Int(rx, y), new Vector2Int(rx - 1, y), true);
            if (x1 + 1 < width) SetWall(new Vector2Int(x1, y), new Vector2Int(x1 + 1, y), true);
        }
        // doorway on a random side that has a valid outside neighbour
        for (int tries = 0; tries < 8; tries++)
        {
            int side = rng.Next(4);
            if (side == 0 && ry > 0)        { int x = rng.Next(rx, x1 + 1); SetWall(new Vector2Int(x, ry), new Vector2Int(x, ry - 1), false); return; }
            if (side == 1 && y1 + 1 < height) { int x = rng.Next(rx, x1 + 1); SetWall(new Vector2Int(x, y1), new Vector2Int(x, y1 + 1), false); return; }
            if (side == 2 && rx > 0)        { int y = rng.Next(ry, y1 + 1); SetWall(new Vector2Int(rx, y), new Vector2Int(rx - 1, y), false); return; }
            if (side == 3 && x1 + 1 < width) { int y = rng.Next(ry, y1 + 1); SetWall(new Vector2Int(x1, y), new Vector2Int(x1 + 1, y), false); return; }
        }
    }

    bool[,] Filled()
    {
        var a = new bool[width, height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) a[x, y] = true;
        return a;
    }

    Vector2Int Unvisited(Vector2Int c)
    {
        var o = new List<Vector2Int>();
        if (c.y + 1 < height && !visited[c.x, c.y + 1]) o.Add(new Vector2Int(c.x, c.y + 1));
        if (c.x + 1 < width  && !visited[c.x + 1, c.y]) o.Add(new Vector2Int(c.x + 1, c.y));
        if (c.y - 1 >= 0     && !visited[c.x, c.y - 1]) o.Add(new Vector2Int(c.x, c.y - 1));
        if (c.x - 1 >= 0     && !visited[c.x - 1, c.y]) o.Add(new Vector2Int(c.x - 1, c.y));
        return o.Count == 0 ? new Vector2Int(-1, -1) : o[rng.Next(o.Count)];
    }

    // closed=true raises a wall between a,b; false removes it
    void SetWall(Vector2Int a, Vector2Int b, bool closed)
    {
        if (b.y > a.y) { wallN[a.x, a.y] = closed; wallS[b.x, b.y] = closed; }
        else if (b.y < a.y) { wallS[a.x, a.y] = closed; wallN[b.x, b.y] = closed; }
        else if (b.x > a.x) { wallE[a.x, a.y] = closed; wallW[b.x, b.y] = closed; }
        else { wallW[a.x, a.y] = closed; wallE[b.x, b.y] = closed; }
    }

    // ---------------- build ----------------
    IEnumerator BuildRoutine()
    {
        geo = new GameObject("Geometry").transform;
        geo.SetParent(transform); geo.localPosition = Vector3.zero;

        var postNodes = new HashSet<Vector2Int>();
        var lampX = AxisMarks(width, lightSpacing);
        var lampY = AxisMarks(height, lightSpacing);
        var litX = AxisMarks(width, realLightSpacing);
        var litY = AxisMarks(height, realLightSpacing);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 c = Cell(x, y);
                Slab(c + Vector3.down * 0.05f, new Vector3(cellSize, 0.1f, cellSize), floorMat);
                if (buildCeiling) Slab(c + Vector3.up * wallHeight, new Vector3(cellSize, 0.1f, cellSize), ceilMat, false);

                if (addPillars && isRoom[x, y] && isOpen[x, y] && x % 2 == 0 && y % 2 == 0)
                    Slab(c + Vector3.up * wallHeight / 2f, new Vector3(cellSize * 0.3f, wallHeight, cellSize * 0.3f), wallMat);

                if (wallN[x, y]) { WallSeg(c + Vector3.forward * cellSize / 2f, false); Node(postNodes, x, y + 1); Node(postNodes, x + 1, y + 1); }
                if (wallW[x, y]) { WallSeg(c + Vector3.left * cellSize / 2f, true); Node(postNodes, x, y); Node(postNodes, x, y + 1); }
                if (y == 0 && wallS[x, y]) { WallSeg(c + Vector3.back * cellSize / 2f, false); Node(postNodes, x, y); Node(postNodes, x + 1, y); }
                if (x == width - 1 && wallE[x, y]) { WallSeg(c + Vector3.right * cellSize / 2f, true); Node(postNodes, x + 1, y); Node(postNodes, x + 1, y + 1); }

                if (buildCeiling && lampX.Contains(x) && lampY.Contains(y))
                    Lamp(c, litX.Contains(x) && litY.Contains(y));
            }
            if (buildChunkCols > 0 && x % buildChunkCols == 0) yield return null;
        }

        foreach (var n in postNodes) Slab(NodePos(n), new Vector3(0.2f, wallHeight, 0.2f), wallMat);
        foreach (var t in tableCells) PartyTable(Cell(t.x, t.y));

        StaticBatchingUtility.Combine(geo.gameObject);
        if (spawnPlayer) SpawnPlayer();
    }

    HashSet<int> AxisMarks(int n, float spacing)
    {
        var s = new HashSet<int>();
        if (spacing <= 0) return s;
        for (float k = 0; ; k++) { int i = Mathf.RoundToInt(k * spacing); if (i >= n) break; s.Add(i); }
        return s;
    }

    void Node(HashSet<Vector2Int> set, int i, int j) => set.Add(new Vector2Int(i, j));
    Vector3 Cell(int x, int y) => transform.position + new Vector3(x * cellSize, 0f, y * cellSize);
    Vector3 NodePos(Vector2Int n) => transform.position + new Vector3(n.x * cellSize - cellSize / 2f, wallHeight / 2f, n.y * cellSize - cellSize / 2f);

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

    void WallSeg(Vector3 pos, bool alongZ)
    {
        Vector3 s = alongZ ? new Vector3(0.2f, wallHeight, cellSize) : new Vector3(cellSize, wallHeight, 0.2f);
        Slab(pos + Vector3.up * wallHeight / 2f, s, wallMat);

        // occasional colored doodle on the wall face
        if (rng.NextDouble() < doodleChance)
        {
            var col = partyMats[rng.Next(partyMats.Length)];
            Vector3 ds = alongZ ? new Vector3(0.05f, 0.9f, 0.9f) : new Vector3(0.9f, 0.9f, 0.05f);
            Slab(pos + Vector3.up * (1.4f) + (alongZ ? Vector3.left : Vector3.back) * 0.13f, ds, col, false);
        }
    }

    void Lamp(Vector3 c, bool realLight)
    {
        Slab(c + Vector3.up * (wallHeight - 0.05f), new Vector3(cellSize * 0.16f, 0.05f, cellSize * 0.16f), lightMat, false);
        if (!realLight) return;
        var go = new GameObject("Light");
        go.transform.SetParent(transform);
        go.transform.position = c + Vector3.up * (wallHeight - 0.3f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = lightColor;
        l.intensity = lightIntensity; l.range = lightRange; l.shadows = LightShadows.None;
    }

    // party table: white top + 4 legs + a few balloons on strings
    void PartyTable(Vector3 c)
    {
        float topY = 0.75f;
        Slab(c + Vector3.up * topY, new Vector3(2.2f, 0.08f, 1.0f), clothMat);          // top
        float lx = 0.95f, lz = 0.4f;
        foreach (var s in new[] { new Vector2(-lx, -lz), new Vector2(lx, -lz), new Vector2(-lx, lz), new Vector2(lx, lz) })
            Slab(c + new Vector3(s.x, topY / 2f, s.y), new Vector3(0.06f, topY, 0.06f), legMat);

        int n = 3;
        for (int i = 0; i < n; i++)
        {
            Vector3 anchor = c + new Vector3((i - 1) * 0.6f, topY + 0.05f, 0);
            float h = 1.1f + (float)rng.NextDouble() * 0.4f;
            var mat = partyMats[rng.Next(partyMats.Length)];

            var str = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // string
            str.transform.SetParent(geo);
            str.transform.position = anchor + Vector3.up * h / 2f;
            str.transform.localScale = new Vector3(0.01f, h / 2f, 0.01f);
            str.GetComponent<Renderer>().sharedMaterial = legMat;
            Destroy(str.GetComponent<Collider>());

            var bal = GameObject.CreatePrimitive(PrimitiveType.Sphere); // balloon
            bal.transform.SetParent(geo);
            bal.transform.position = anchor + Vector3.up * h;
            bal.transform.localScale = new Vector3(0.32f, 0.4f, 0.32f);
            bal.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(bal.GetComponent<Collider>());
        }
    }

    void SpawnPlayer()
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p.name = "Player";
        Destroy(p.GetComponent<Collider>());
        p.transform.position = Cell(0, 0) + Vector3.up * 1.1f;
        p.AddComponent<SimplePlayer>();
    }
}
