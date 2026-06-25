using System.Collections.Generic;
using UnityEngine;

// Generates a random 3D maze (recursive backtracker) and builds it from cube
// primitives with backrooms-colored materials made in code. Zero setup: drop on
// an empty GameObject and press Play. Swap colors/materials later for real art.
public class MazeGenerator : MonoBehaviour
{
    [Header("Grid")]
    public int width = 42;
    public int height = 42;
    public float cellSize = 4f;     // wide cells = backrooms-style corridors
    public float wallHeight = 3f;
    public int seed = 0;            // 0 = random each run
    public bool buildCeiling = true; // uncheck to see the maze from above
    public int ceilingTilesPerCell = 2; // drywall tile grid density per cell
    public bool spawnPlayer = true;  // auto-create the player capsule at the start cell
    public bool elevatorIntro = true; // start inside a failing elevator that drops you in

    [Header("Wall/beam texture (optional)")]
    public Texture2D wallTexture;                 // assign your image; set Wrap Mode = Repeat
    public Vector2 wallTextureTiling = new Vector2(2f, 2f);

    [Header("Backrooms colors")]
    public Color wallColor    = new Color(0.76f, 0.70f, 0.50f); // mustard wallpaper
    public Color floorColor   = new Color(0.42f, 0.36f, 0.24f); // damp carpet
    public Color ceilingColor = new Color(0.85f, 0.82f, 0.70f); // pale tiles
    public Color lightColor   = new Color(1f, 0.97f, 0.85f);    // warm fluorescent

    [Header("Lights")]
    public float lightSpacing = 1.5f; // lamp+light spacing in cells (0 = none)
    public float lightIntensity = 1.4f;
    public float lightRange = 13f;

    [Header("Rooms (open areas with pillars)")]
    public int roomCount = 6;
    public int roomMaxSize = 4;     // up to N x N cells, internal walls removed
    public bool addPillars = true;

    [Header("Void (thin beam floors you can fall off)")]
    [Range(0f, 1f)] public float voidRoomChance = 0.4f; // chance a room is beams-over-void instead of pillars
    public float beamWidth = 0.3f;
    public float beamHeight = 0.5f;
    public float voidDepth = 8f;        // how far below the floor the catch floor sits

    Material wallMat, floorMat, ceilMat, lightMat, darkMat, metalMat, buttonMat, panelMat, labelMat, silverMat;
    bool[,] wallN, wallE, wallS, wallW;
    bool[,] visited;
    bool[,] isRoom, isVoid;
    System.Random rng;

    void Start() => Generate();

    public void Generate()
    {
        rng = seed == 0 ? new System.Random() : new System.Random(seed);
        MakeMaterials();
        CarveMaze();
        Build();
    }

    // --- materials (URP Lit) made in code so it works with no asset setup ---
    void MakeMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");

        // walls: new texture (BackroomsWall2); floor + beams: old texture (BackroomsWall)
        if (wallTexture == null) wallTexture = Resources.Load<Texture2D>("BackroomsWall2");
        var floorTex = Resources.Load<Texture2D>("BackroomsWall");

        wallMat  = Lit(shader, wallColor);
        Apply(wallMat, wallTexture);

        floorMat = Lit(shader, floorColor);
        Apply(floorMat, floorTex);

        ceilMat  = Lit(shader, ceilingColor);
        darkMat  = Lit(shader, new Color(0.02f, 0.02f, 0.02f)); // void enclosure
        silverMat = Lit(shader, new Color(0.7f, 0.7f, 0.72f));  // ceiling T-bar grid lines
        silverMat.SetFloat("_Smoothness", 0.7f);
        metalMat = Lit(shader, new Color(0.34f, 0.34f, 0.36f)); // elevator car (brushed steel)
        metalMat.SetFloat("_Smoothness", 0.55f);
        buttonMat = Lit(shader, new Color(0.62f, 0.62f, 0.64f)); // light steel buttons
        buttonMat.SetFloat("_Smoothness", 0.4f);
        panelMat = Lit(shader, new Color(0.48f, 0.48f, 0.50f));  // panel plate (mid steel)
        panelMat.SetFloat("_Smoothness", 0.6f);
        // unlit so every lamp panel reads identical, regardless of nearby point lights
        lightMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lightMat.SetColor("_BaseColor", lightColor);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.035f);

        // turn off the scene's sun so no exterior light leaks through wall seams
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.enabled = false;
    }

    void Apply(Material m, Texture2D tex)
    {
        if (tex == null) return;
        m.mainTexture = tex;                       // URP Lit _BaseMap
        m.mainTextureScale = wallTextureTiling;
        m.SetColor("_BaseColor", Color.white);     // don't tint the texture
    }

    Material Lit(Shader s, Color c)
    {
        var m = new Material(s);
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.05f); // matte, not plastic
        return m;
    }

    // --- maze carving: recursive backtracker over the grid ---
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
            if (next.x < 0) { stack.Pop(); continue; }   // dead end, backtrack

            RemoveWallBetween(cur, next);
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        CarveRooms();

        if (elevatorIntro && height > 1)                       // guarantee a north exit
            RemoveWallBetween(new Vector2Int(0, 0), new Vector2Int(0, 1));
    }

    // open rectangular rooms: clear internal walls. Each room is either a pillar
    // room (solid floor) or a void room (beams over the void). Corridors stay solid.
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
                    if (voidRoom && !(x == 0 && y == 0)) isVoid[x, y] = true; // never void the spawn
                    if (x + 1 < rx + rw && x + 1 < width) RemoveWallBetween(new Vector2Int(x, y), new Vector2Int(x + 1, y));
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
        if (b.y > a.y) { wallN[a.x, a.y] = false; wallS[b.x, b.y] = false; }
        else if (b.y < a.y) { wallS[a.x, a.y] = false; wallN[b.x, b.y] = false; }
        else if (b.x > a.x) { wallE[a.x, a.y] = false; wallW[b.x, b.y] = false; }
        else { wallW[a.x, a.y] = false; wallE[b.x, b.y] = false; }
    }

    // --- build geometry from the carved grid ---
    void Build()
    {
        var postNodes = new HashSet<Vector2Int>(); // grid corners that need a post
        var litX = AxisMarks(width, lightSpacing);  // which cell columns get lamps
        var litY = AxisMarks(height, lightSpacing);  // which cell rows get lamps

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 c = CellCenter(x, y);

                if (isVoid[x, y]) BeamFloor(c);        // thin beams over the void
                else Tile(c, 0f, floorMat);            // solid floor
                if (buildCeiling) DrywallCeiling(c);   // suspended-tile ceiling

                if (addPillars && isRoom[x, y] && !isVoid[x, y] && x % 2 == 0 && y % 2 == 0)
                    Pillar(c);

                // place N and W walls per cell, plus outer S (bottom row) and E (right col).
                // register each wall's two end nodes so we can cap corners/seams with posts.
                if (wallN[x, y]) { Wall(c + Vector3.forward * cellSize / 2f, false); Node(postNodes, x, y + 1); Node(postNodes, x + 1, y + 1); }
                if (wallW[x, y]) { Wall(c + Vector3.left    * cellSize / 2f, true);  Node(postNodes, x, y);     Node(postNodes, x, y + 1); }
                if (y == 0 && wallS[x, y]) { Wall(c + Vector3.back  * cellSize / 2f, false); Node(postNodes, x, y); Node(postNodes, x + 1, y); }
                if (x == width - 1 && wallE[x, y]) { Wall(c + Vector3.right * cellSize / 2f, true); Node(postNodes, x + 1, y); Node(postNodes, x + 1, y + 1); }

                // lamp only on the spacing grid, always with a real light
                if (buildCeiling && litX.Contains(x) && litY.Contains(y))
                    LampFixture(c, true);
            }
        }

        foreach (var n in postNodes) CornerPost(n);

        CatchFloor();
        if (spawnPlayer) SpawnPlayer();
    }

    void Node(HashSet<Vector2Int> set, int i, int j) => set.Add(new Vector2Int(i, j));

    // cell indices along an axis spaced ~`spacing` cells apart (e.g. 1.5 -> 0,2,3,5,6,8,...)
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

    // post at a grid corner: caps the gap where wall segments meet at corners and seams
    void CornerPost(Vector2Int n)
    {
        Vector3 pos = transform.position + new Vector3(n.x * cellSize - cellSize / 2f, wallHeight / 2f, n.y * cellSize - cellSize / 2f);
        Slab(pos, new Vector3(0.2f, wallHeight, 0.2f), wallMat);
    }

    // dark, enclosed pit below the maze: black floor + 4 walls, no lights, so
    // falling drops into near-total darkness. Touching the floor respawns you.
    void CatchFloor()
    {
        // true maze center/size: cells span [-cellSize/2 .. (n-1)*cellSize + cellSize/2]
        Vector3 mid = transform.position + new Vector3((width - 1) * cellSize / 2f, 0f, (height - 1) * cellSize / 2f);
        float w = width * cellSize, d = height * cellSize;
        float t = 0.2f; // match outer maze wall thickness so the pit looks like its continuation

        var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
        f.name = "CatchFloor";
        f.transform.SetParent(transform);
        f.transform.position = mid + Vector3.down * voidDepth;
        f.transform.localScale = new Vector3(w, 0.2f, d);
        f.GetComponent<Renderer>().material = darkMat;

        // 4 perimeter walls, aligned to the outer wall planes, from floor down to the pit
        Pit(mid + new Vector3(0, -voidDepth / 2f,  d / 2f), new Vector3(w, voidDepth, t));
        Pit(mid + new Vector3(0, -voidDepth / 2f, -d / 2f), new Vector3(w, voidDepth, t));
        Pit(mid + new Vector3( w / 2f, -voidDepth / 2f, 0), new Vector3(t, voidDepth, d));
        Pit(mid + new Vector3(-w / 2f, -voidDepth / 2f, 0), new Vector3(t, voidDepth, d));
    }

    void Pit(Vector3 pos, Vector3 scale)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.transform.SetParent(transform);
        p.transform.position = pos;
        p.transform.localScale = scale;
        p.GetComponent<Renderer>().material = darkMat;
    }

    // spawn the player capsule at the start cell so you don't add it by hand
    void SpawnPlayer()
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        p.name = "Player";
        Destroy(p.GetComponent<Collider>());        // SimplePlayer's CharacterController is the collider
        p.transform.position = CellCenter(0, 0) + Vector3.up * 1.1f;
        var player = p.AddComponent<SimplePlayer>();

        if (elevatorIntro) BuildElevator(CellCenter(0, 0), player);
    }

    // metal car inset inside the start cell (inset avoids z-fighting with the maze
    // walls/floor) with center-parting north doors; runs the intro
    void BuildElevator(Vector3 center, SimplePlayer player)
    {
        float inset = 0.2f;             // keep faces off the maze planes
        float cs = cellSize, h = wallHeight - 0.12f;
        float half = cs / 2f - inset;   // car half-width

        var ele = new GameObject("Elevator");
        ele.transform.SetParent(transform);
        ele.transform.position = center;

        Box(ele.transform, new Vector3(0, 0.06f, 0),  new Vector3(half * 2, 0.1f, half * 2), metalMat); // floor (raised)
        Box(ele.transform, new Vector3(0, h, 0),       new Vector3(half * 2, 0.1f, half * 2), metalMat); // ceiling
        Box(ele.transform, new Vector3(0, h / 2f, -half), new Vector3(half * 2, h, 0.15f), metalMat);    // south
        Box(ele.transform, new Vector3(-half, h / 2f, 0), new Vector3(0.15f, h, half * 2), metalMat);    // west
        Box(ele.transform, new Vector3( half, h / 2f, 0), new Vector3(0.15f, h, half * 2), metalMat);    // east

        // telescoping center-opening doors: 2 panels per side, staggered in depth
        // so they nest without z-fighting; inner panel retracts farther than outer
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
            panelSlides[i] = (nPanels - 1 - i) * pw;   // outer panel barely moves, inner slides full
        }

        // control panel on the east wall, near the doors, styled after a real panel
        float px = half - 0.082f;                // frame back flush against the east wall face
        float cz = half * 0.62f;                 // closer to the doors (north)
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelMat = new Material(Shader.Find("Custom/TextDepth")); // colored text, depth-tested
        Color red  = new Color(1f, 0.15f, 0.05f);
        Color dark = new Color(0.08f, 0.08f, 0.08f);
        float zL = cz - 0.09f, zR = cz + 0.09f;  // left / right button columns

        // recessed frame + raised plate so it reads as a mounted panel
        Box(ele.transform, new Vector3(px,         1.40f, cz), new Vector3(0.012f, 1.22f, 0.46f), darkMat);  // frame
        Box(ele.transform, new Vector3(px - 0.008f, 1.40f, cz), new Vector3(0.01f, 1.14f, 0.40f), panelMat); // plate

        // flat black LED box + red floor digit at the top, facing the player (-X)
        Box(ele.transform, new Vector3(px - 0.015f, 1.86f, cz), new Vector3(0.008f, 0.16f, 0.26f), darkMat);
        var tm = Label(ele.transform, new Vector3(px - 0.022f, 1.86f, cz), "2", red, 0.01f, font);

        // door open / close buttons
        PanelButton(ele.transform, px, 1.62f, zL, "▶◀",  dark, font, 0.006f);
        PanelButton(ele.transform, px, 1.62f, zR, "◀ ▶", dark, font, 0.006f);

        // numbered floor buttons: left even/B, right odd, rising upward
        string[] leftLabels  = { "B", "2", "4", "6" };
        string[] rightLabels = { "1", "3", "5", "7" };
        for (int r = 0; r < 4; r++)
        {
            float y = 0.95f + r * 0.16f;
            PanelButton(ele.transform, px, y, zL, leftLabels[r],  dark, font);
            PanelButton(ele.transform, px, y, zR, rightLabels[r], dark, font);
        }

        var lgt = new GameObject("ElevatorLight");
        lgt.transform.SetParent(ele.transform);
        lgt.transform.localPosition = new Vector3(0, h - 0.3f, 0);
        var l = lgt.AddComponent<Light>();
        l.type = LightType.Point; l.color = lightColor; l.intensity = 1.6f; l.range = cs * 2f;

        var intro = ele.AddComponent<ElevatorIntro>();
        intro.player = player;
        intro.leftPanels = leftPanels;
        intro.rightPanels = rightPanels;
        intro.panelSlides = panelSlides;
        intro.display = tm;
    }

    GameObject Box(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.transform.SetParent(parent, false);
        b.transform.localPosition = localPos;
        b.transform.localScale = scale;
        b.GetComponent<Renderer>().material = mat;
        return b;
    }

    // 3D text facing -X (toward a player looking at the east wall)
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

    // square button + its engraved label on the panel face
    void PanelButton(Transform parent, float px, float y, float z, string label, Color labelCol, Font font, float labelSize = 0.011f)
    {
        Box(parent, new Vector3(px - 0.015f, y, z), new Vector3(0.008f, 0.12f, 0.13f), buttonMat);
        if (!string.IsNullOrEmpty(label))
            Label(parent, new Vector3(px - 0.022f, y, z), label, labelCol, labelSize, font);
    }

    Vector3 CellCenter(int x, int y) =>
        transform.position + new Vector3(x * cellSize, 0f, y * cellSize);

    void Tile(Vector3 center, float yOffset, Material mat)
    {
        var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
        t.transform.SetParent(transform);
        t.transform.position = center + Vector3.up * (yOffset + (yOffset == 0 ? -0.05f : 0.05f));
        t.transform.localScale = new Vector3(cellSize, 0.1f, cellSize);
        t.GetComponent<Renderer>().material = mat;
    }

    // cross of two beams through the cell center: walkable spine, edges fall.
    // top flush with floor level (y=0), extends down by beamHeight.
    void BeamFloor(Vector3 center)
    {
        Vector3 p = center + Vector3.down * beamHeight / 2f;

        var bx = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bx.transform.SetParent(transform);
        bx.transform.position = p;
        bx.transform.localScale = new Vector3(cellSize, beamHeight, beamWidth);
        bx.GetComponent<Renderer>().material = floorMat;

        var bz = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bz.transform.SetParent(transform);
        bz.transform.position = p;
        bz.transform.localScale = new Vector3(beamWidth, beamHeight, cellSize);
        bz.GetComponent<Renderer>().material = floorMat;
    }

    void Pillar(Vector3 center)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
        p.transform.SetParent(transform);
        p.transform.position = center + Vector3.up * wallHeight / 2f;
        p.transform.localScale = new Vector3(cellSize * 0.3f, wallHeight, cellSize * 0.3f);
        p.GetComponent<Renderer>().material = wallMat;
    }

    // alongZ = wall runs along the Z axis (a left/right wall); else along X
    void Wall(Vector3 pos, bool alongZ)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.transform.SetParent(transform);
        w.transform.position = pos + Vector3.up * wallHeight / 2f;
        w.transform.rotation = alongZ ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
        w.transform.localScale = new Vector3(cellSize, wallHeight, 0.2f);
        w.GetComponent<Renderer>().material = wallMat;
    }

    // a world-space cube of any size/material, parented to the maze
    GameObject Slab(Vector3 pos, Vector3 scale, Material mat)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.transform.SetParent(transform);
        b.transform.position = pos;
        b.transform.localScale = scale;
        b.GetComponent<Renderer>().material = mat;
        return b;
    }

    // suspended drywall ceiling: a silver T-bar backing with white tiles inset
    // a little, so thin silver lines show in the gaps between tiles
    void DrywallCeiling(Vector3 c)
    {
        Slab(c + Vector3.up * (wallHeight + 0.04f), new Vector3(cellSize, 0.06f, cellSize), silverMat); // grid backing

        int n = Mathf.Max(1, ceilingTilesPerCell);
        float s = cellSize / n;
        float gap = 0.06f;                       // thickness of the silver lines
        Vector3 corner = c + new Vector3(-cellSize / 2f + s / 2f, wallHeight, -cellSize / 2f + s / 2f);
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                Slab(corner + new Vector3(i * s, 0, j * s), new Vector3(s - gap, 0.1f, s - gap), ceilMat);
    }

    // ceiling lamp fixture (emissive panel) in every cell; optionally a real light
    void LampFixture(Vector3 c, bool realLight)
    {
        var panel = Slab(c + Vector3.up * (wallHeight - 0.05f),
                         new Vector3(cellSize * 0.15f, 0.05f, cellSize * 0.15f), lightMat);
        Destroy(panel.GetComponent<BoxCollider>());

        if (!realLight) return;
        var go = new GameObject("Light");
        go.transform.SetParent(transform);
        go.transform.position = c + Vector3.up * (wallHeight - 0.3f);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = lightColor;
        l.intensity = lightIntensity;
        l.range = lightRange;
    }
}

// ponytail: maze + geometry + placeholder art in code, so Play works with no
// setup. Swap to real prefabs/textures once the layout feels right. Enemies and
// win/lose are separate scripts; bake a NavMesh after Generate() if enemies need it.
