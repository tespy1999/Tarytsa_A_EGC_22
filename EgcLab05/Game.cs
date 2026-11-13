using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra principală și ciclul de randare al aplicației 3D.
/// Gestionează scena cu obiecte construite procedural, bufferul de adâncime,
/// controlul camerelor (FPS și orbită) și afișarea informațiilor în consolă.
/// </summary>
public class Game : GameWindow
{
    // Instanțele camerelor 3D (nu necesită context OpenGL la inițializare).
    private readonly FpsCamera _fps = new();
    private readonly OrbitCamera _orbit = new();
    // Configurare cameră orbită pentru tema de laborator.
    private Camera _active;                        // camera selectată pentru randare

    // Resursele grafice OpenGL sunt alocate în OnLoad, după inițializarea contextului.
    private Shader _shader = null!;
    private readonly List<Mesh> _meshes = new();          // colecția de mesh-uri eliberate la închidere
    private readonly List<SceneObject> _scene = new();

    // Parametrii de randare modificați din tastatură.
    private bool _depthTest;           // tasta Z (după TODO 5.2 îl puteți porni de la început: = true)
    private bool _faceCulling;         // tasta C: ascunderea fețelor orientate invers
    private bool _wireframe;           // tasta F: afișare doar pe muchii (contur)

    // Ignorăm primul delta al mouse-ului la capturarea cursorului sau la schimbarea camerei
    // pentru a evita rotirea bruscă a imaginii.
    private bool _ignoreMouseOnce = true;

    // Variabile pentru măsurarea FPS-ului și actualizarea titlului ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;


    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // versiunea OpenGL 3.3
                   Profile = ContextProfile.Core,           // profil Core (doar OpenGL modern)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        _active = _fps;
        VSync = VSyncMode.On;   // sincronizează cadrele cu rata de refresh a ecranului
    }

    /// <summary>Metodă apelată la pornire, imediat după crearea ferestrei și a contextului grafic.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();
        ConsolePanel.Init();

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _fps.Resize(ClientSize.X, ClientSize.Y);
        // Actualizăm dimensiunile și raportul de aspect pentru ambele camere.
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _fps.Resize(ClientSize.X, ClientSize.Y);
        _orbit.Resize(ClientSize.X, ClientSize.Y);



        CursorState = CursorState.Grabbed;
        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        BuildScene();

        // Blocăm cursorul în interiorul ferestrei pentru a permite rotirea continuă în modul FPS.
        CursorState = CursorState.Grabbed;

        ConsolePanel.PrintHelp();
        PrintSceneInfo();
    }

    /// <summary>Inițializează obiectele 3D din scenă și le poziționează folosind Transform3D.</summary>
    private void BuildScene()
    {
        // Creăm geometriile de bază (un singur VAO/VBO în GPU poate fi partajat de mai multe obiecte).
        Mesh grid = AddMesh(Shapes.Grid(10, 1.0f, new Color4(0.35f, 0.38f, 0.42f, 1.0f)));
        Mesh axes = AddMesh(Shapes.Axes(2.0f));
        Mesh pyramid = AddMesh(Shapes.Pyramid());
        Mesh cube = AddMesh(Shapes.Cube());
        Mesh sphere = AddMesh(Shapes.Sphere(1.0f, 32, 16));
        Mesh smallSphere = AddMesh(Shapes.Sphere(0.35f, 16, 8));
        Mesh cylinder = AddMesh(Shapes.Cylinder(0.5f, 1.5f, 32));

        // Planul de bază și axele de coordonate (ridicate ușor pe Y pentru vizibilitate clară).
        AddObject("podea", grid);
        AddObject("axe", axes).Transform.Position = new Vector3(0.0f, 0.01f, 0.0f);

        // Corpurile geometrice principale din centrul scenei.
        AddObject("piramidă", pyramid).Transform.Position = new Vector3(-3.0f, 0.5f, 0.0f);

        SceneObject bigCube = AddObject("cub", cube);
        bigCube.Transform.Position = new Vector3(0.0f, 1.2f, -2.0f);
        bigCube.Transform.Scale = new Vector3(1.2f);
        bigCube.Spin = new Vector3(20.0f, 35.0f, 0.0f);

        SceneObject globe = AddObject("sferă", sphere);
        globe.Transform.Position = new Vector3(3.0f, 1.2f, -1.0f);
        globe.Spin = new Vector3(0.0f, 30.0f, 0.0f);

        // TODO 5.8 (facultativ): un cilindru generat procedural, adăugat în scenă
        //           (de exemplu în punctul (-1.5, 0.75, -6)).
        SceneObject cylObj = AddObject("cilindru", cylinder);
        cylObj.Transform.Position = new Vector3(-1.5f, 0.75f, -6.0f);
        cylObj.Spin = new Vector3(0.0f, 45.0f, 0.0f);

        // Alee de coloane simetrice colorate distinct, fiecare având o sferă în partea superioară.
        for (int k = 0; k < 6; k++)
        {
            foreach (float side in new[] { -4.0f, 4.0f })
            {
                float z = -4.0f - 3.0f * k;
                SceneObject pillar = AddObject($"stâlp {k}", cube);
                pillar.Transform.Position = new Vector3(side, 1.5f, z);
                pillar.Transform.Scale = new Vector3(0.6f, 3.0f, 0.6f);
                pillar.Tint = ColorUtil.FromHsv(k / 6.0f, 0.25f, 0.95f);

                AddObject($"bilă {k}", smallSphere).Transform.Position = new Vector3(side, 3.35f, z);
            }
        }


    }

    private Mesh AddMesh(Mesh mesh)
    {
        _meshes.Add(mesh);
        return mesh;
    }

    private SceneObject AddObject(string name, Mesh mesh)
    {
        var obj = new SceneObject(name, mesh);
        _scene.Add(obj);
        return obj;
    }

    /// <summary>Recalculează zona de randare și aspect ratio-ul la redimensionarea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);
        _fps.Resize(e.Width, e.Height);
        _orbit.Resize(e.Width, e.Height);
        _titleDirty = true;
    }

    /// <summary>La recăpătarea focusului ferestrei, ignorăm prima citire a deplasării mouse-ului.</summary>
    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        base.OnFocusedChanged(e);
        _ignoreMouseOnce = true;
    }

    /// <summary>Actualizează starea logică a scenei la fiecare cadru (taste, mouse, mișcare).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        float dt = (float)args.Time;

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
            return;
        }

        HandleKeys();

        // --- Procesare input pentru camera activă --------------------------------------

        Vector2 mouseDelta = MouseState.Delta;
        bool fpsWithoutCursor = _active == _fps && CursorState != CursorState.Grabbed;
        if (_ignoreMouseOnce || fpsWithoutCursor)
        {
            mouseDelta = Vector2.Zero;   // în modul FPS camera se orientează doar dacă cursorul e blocat
            _ignoreMouseOnce = false;
        }

        float fovBefore = _active.FieldOfView;
        _active.Update(KeyboardState, MouseState, mouseDelta, dt);
        if (_active.FieldOfView != fovBefore)
        {
            _titleDirty = true;
        }

        // --- Actualizare animații obiecte ----------------------------------------------

        foreach (SceneObject obj in _scene)
        {
            obj.Update(dt);
        }

        UpdateTitle(args.Time);
    }

    /// <summary>Gestionează comenzile declanșate la apăsarea punctuală a tastelor.</summary>
    private void HandleKeys()
    {
        KeyboardState kb = KeyboardState;

        if (kb.IsKeyPressed(Keys.H)) ConsolePanel.PrintHelp();
        if (kb.IsKeyPressed(Keys.I)) PrintSceneInfo();

        if (kb.IsKeyPressed(Keys.Z)) Toggle(ref _depthTest, "test de adâncime");
        if (kb.IsKeyPressed(Keys.C)) Toggle(ref _faceCulling, "eliminarea fețelor din spate");
        if (kb.IsKeyPressed(Keys.F)) Toggle(ref _wireframe, "contur (wireframe)");

        // Tasta L comută sincronizarea verticală (VSync); viteza camerei rămâne constantă prin dt.
        if (kb.IsKeyPressed(Keys.L))
        {
            VSync = VSync == VSyncMode.On ? VSyncMode.Off : VSyncMode.On;
            ConsolePanel.PrintMessage($"VSync (limitarea cadrelor): {(VSync == VSyncMode.On ? "pornit" : "oprit")}");
        }

        if (kb.IsKeyPressed(Keys.R))
        {
            _active.Reset();
            ConsolePanel.PrintMessage("camera a revenit în poziția inițială");
            _titleDirty = true;
        }

        if (kb.IsKeyPressed(Keys.G) && _active == _fps)
        {
            _fps.FreeFly = !_fps.FreeFly;
            ConsolePanel.PrintMessage($"mod cameră: {_fps.Name}");
            _titleDirty = true;
        }

        if (kb.IsKeyPressed(Keys.M))
        {
            CursorState = CursorState == CursorState.Grabbed ? CursorState.Normal : CursorState.Grabbed;
            _ignoreMouseOnce = true;
            ConsolePanel.PrintMessage(CursorState == CursorState.Grabbed ? "cursor capturat" : "cursor eliberat");
        }

        // Tasta V alternează între camera FPS și camera orbită, păstrând continuitatea privirii.
        if (kb.IsKeyPressed(Keys.V))
        {
            if (_active == _fps)
            {
                _orbit.SetFromFps(_fps.Position, _fps.Front, 5.0f);
                _active = _orbit;
                CursorState = CursorState.Normal;
            }
            else
            {
                _fps.SetFromOrbit(_orbit.Position, _orbit.Target);
                _active = _fps;
                CursorState = CursorState.Grabbed;
            }

            _ignoreMouseOnce = true;
            _titleDirty = true;
            ConsolePanel.PrintMessage($"cameră activă: {_active.Name}");
        }
    }

    private void Toggle(ref bool flag, string name)
    {
        flag = !flag;
        ConsolePanel.PrintMessage($"{name}: {(flag ? "pornit" : "oprit")}");
    }

    /// <summary>Execută desenarea tuturor obiectelor din scenă pentru cadrul curent.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Resetăm bufferele de culoare și adâncime înainte de desenare.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        // TODO 5.2: ștergerea bufferului de adâncime la fiecare cadru
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // 2. Aplicăm setările de rasterizare (depth test, culling, wireframe).
        SetCapability(EnableCap.DepthTest, _depthTest);   // activează compararea adâncimii fragmentelor
        SetCapability(EnableCap.CullFace, _faceCulling);  // elimină poligoanele orientate invers
        SetWireframe(_wireframe);

        // 3. Activăm shader-ul și încărcăm matricile globale ale camerei.
        _shader.Use();
        _shader.SetMatrix4("uView", _active.ViewMatrix);
        _shader.SetMatrix4("uProjection", _active.ProjectionMatrix);

        // 4. Trimitem transformarea și culoarea fiecărui obiect, apoi apelăm desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        // 5. Afișăm cadrul randat pe ecran prin schimbarea bufferelor.
        SwapBuffers();
    }

    /// <summary>Eliberează resursele video (VAO, VBO, EBO, shadere) la închiderea ferestrei.</summary>
    protected override void OnUnload()
    {
        foreach (Mesh mesh in _meshes)
        {
            mesh.Dispose();
        }
        _shader.Dispose();

        base.OnUnload();
    }

    // --- Gestionare titlu fereastră și raportare în consolă -------------------------

    private void UpdateTitle(double elapsed)
    {
        _secondTimer += elapsed;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            Title = $"{_baseTitle} | cameră: {_active.Name} | FOV {_active.FieldOfView:F0}° | FPS: {_lastFrames}";
            _titleDirty = false;
        }
    }

    private void PrintSceneInfo()
    {
        ConsolePanel.PrintInfo(SceneInfo());
    }

    private IEnumerable<string> SceneInfo()
    {
        yield return $"Camera activă  : {_active.Name}";
        foreach (string line in _active.Describe())
        {
            yield return line;
        }
        yield return $"FOV vertical   : {_active.FieldOfView,6:F1}°   orizontal: {_active.HorizontalFov,6:F1}°";
        yield return $"Aspect         : {_active.AspectRatio,6:F3}   ({ClientSize.X} × {ClientSize.Y} pixeli)";
        yield return $"Near / far     : {_active.Near} / {_active.Far}";

        int triangles = 0;
        foreach (SceneObject obj in _scene)
        {
            triangles += obj.Mesh.TriangleCount;
        }
        yield return $"Obiecte        : {_scene.Count}   triunghiuri desenate pe cadru: {triangles}";
        yield return $"Depth test     : {OnOff(_depthTest)}   culling: {OnOff(_faceCulling)}   wireframe: {OnOff(_wireframe)}";
        yield return $"VSync          : {OnOff(VSync == VSyncMode.On)}";
        yield return $"Cadre/secundă  : {_lastFrames}";
    }

    private static string OnOff(bool value) => value ? "pornit" : "oprit";

    // --- Funcții utilitare pentru configurarea stării OpenGL ------------------------

    private static void SetCapability(EnableCap capability, bool enabled)
    {
        if (enabled) GL.Enable(capability);
        else GL.Disable(capability);
    }

    /// <summary>
    /// Setează modul de poligonare între randare plină (Fill) și randare pe contur (Line).
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }
}