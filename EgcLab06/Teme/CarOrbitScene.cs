using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public sealed class CarOrbitScene : Scene
{
    private const string Restart = "repornește mașina";

    // Conducere
    private const float MoveSpeed = 6.0f;          // unități / s
    private const float TurnSpeedDeg = 140.0f;     // grade / s (rotație când virezi)
    private const float CarY = 0.55f;
    private static readonly Vector2 PlayMin = new(-10.0f, -8.0f);
    private static readonly Vector2 PlayMax = new(10.0f, 8.0f);

    // Orbită (kinematică, urmează mașina prin ierarhie)
    private const float OrbitRadius = 2.4f;
    private const float OrbitSpeedDeg = 100.0f;    // grade / s

    private SceneObject _carRoot = null!;
    private SceneObject _orbitPivot = null!;
    private SceneObject _sphere = null!;
    private readonly List<SceneObject> _wheels = new();

    private float _headingDeg;   // orientarea mașinii (0 = +X, 90 = +Z ...)
    private float _speed;        // viteza curentă (pentru roți + status)

    public override string Name => "Conduci mașina + sferă pe orbită";

    public override string Status =>
        $"viteză {_speed:F1}  |  direcție {_headingDeg:F0}°  |  orbită {_orbitPivot.Transform.Rotation.Y:F0}°";

    public override IEnumerable<string> ActionNames => new[]
    {
        Actions.Forward, Actions.Back, Actions.Left, Actions.Right, Restart
    };

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 9.2f, 13.8f);
        CameraTarget = new Vector3(0.0f, 0.5f, 0.0f);

        // Controale de conducere (aceleași acțiuni ca în laborator)
        input.Bind(Actions.Forward, Keys.W, Keys.Up);
        input.Bind(Actions.Back, Keys.S, Keys.Down);
        input.Bind(Actions.Left, Keys.A, Keys.Left);
        input.Bind(Actions.Right, Keys.D, Keys.Right);
        input.Bind(Restart, Keys.R);

        Mesh grid = AddMesh(Shapes.Grid(14, 1.0f, new Color4(0.20f, 0.30f, 0.40f, 1.0f)));
        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh sphere = AddMesh(Shapes.ShadedSphere(0.45f, 28, 16));
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.35f, 0.22f, 20));

        AddObject("podea", grid);

        BuildArena(cube, cylinder, sphere);
        BuildCar(cube, cylinder);
        BuildOrbitingSphere(cube, sphere, cylinder);
        ResetCar();
    }

    private void BuildArena(Mesh cube, Mesh cylinder, Mesh sphere)
    {
        // Platforma centrală (inel de start/pit-stop)
        SceneObject centerPad = AddObject("centru_arena", cylinder);
        centerPad.Transform.Position = new Vector3(0.0f, 0.005f, 0.0f);
        centerPad.Transform.Scale = new Vector3(4.2f, 0.02f, 4.2f);
        centerPad.Tint = new Color4(0.14f, 0.18f, 0.24f, 1.0f);

        SceneObject centerRing = AddObject("inel_centru", cylinder);
        centerRing.Transform.Position = new Vector3(0.0f, 0.01f, 0.0f);
        centerRing.Transform.Scale = new Vector3(2.0f, 0.02f, 2.0f);
        centerRing.Tint = new Color4(0.08f, 0.55f, 0.85f, 1.0f);

        // Borduri perimetru de delimitare a arenei de joc
        SceneObject borderN = AddObject("bordură_nord", cube);
        borderN.Transform.Position = new Vector3(0.0f, 0.12f, PlayMax.Y + 0.25f);
        borderN.Transform.Scale = new Vector3(20.5f, 0.24f, 0.25f);
        borderN.Tint = new Color4(0.22f, 0.28f, 0.36f, 1.0f);

        SceneObject borderS = AddObject("bordură_sud", cube);
        borderS.Transform.Position = new Vector3(0.0f, 0.12f, PlayMin.Y - 0.25f);
        borderS.Transform.Scale = new Vector3(20.5f, 0.24f, 0.25f);
        borderS.Tint = new Color4(0.22f, 0.28f, 0.36f, 1.0f);

        SceneObject borderE = AddObject("bordură_est", cube);
        borderE.Transform.Position = new Vector3(PlayMax.X + 0.25f, 0.12f, 0.0f);
        borderE.Transform.Scale = new Vector3(0.25f, 0.24f, 16.5f);
        borderE.Tint = new Color4(0.22f, 0.28f, 0.36f, 1.0f);

        SceneObject borderW = AddObject("bordură_vest", cube);
        borderW.Transform.Position = new Vector3(PlayMin.X - 0.25f, 0.12f, 0.0f);
        borderW.Transform.Scale = new Vector3(0.25f, 0.24f, 16.5f);
        borderW.Tint = new Color4(0.22f, 0.28f, 0.36f, 1.0f);

        // Piloni luminoși la cele 4 colțuri
        Vector2[] corners = { PlayMin, new(PlayMin.X, PlayMax.Y), new(PlayMax.X, PlayMin.Y), PlayMax };
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 c = corners[i];
            SceneObject post = AddObject($"pilon_{i}", cylinder);
            post.Transform.Position = new Vector3(c.X, 0.45f, c.Y);
            post.Transform.Scale = new Vector3(0.45f, 4.2f, 0.45f);
            post.Tint = new Color4(0.18f, 0.20f, 0.26f, 1.0f);

            SceneObject beacon = AddObject($"baliză_colț_{i}", sphere);
            beacon.Transform.Position = new Vector3(c.X, 1.0f, c.Y);
            beacon.Transform.Scale = new Vector3(0.5f);
            beacon.Tint = new Color4(0.10f, 0.90f, 1.0f, 1.0f);
            beacon.Spin = new Vector3(0.0f, 60.0f, 0.0f);
        }
    }

    private void BuildCar(Mesh cube, Mesh cylinder)
    {
        _carRoot = AddObject("mașină", null);
        _carRoot.Transform.Position = new Vector3(0.0f, CarY, 0.0f);

        // Șasiu inferior / praguri (dark carbon)
        SceneObject underbody = AddObject("șasiu", cube, _carRoot);
        underbody.Transform.Position = new Vector3(0.0f, 0.02f, 0.0f);
        underbody.Transform.Scale = new Vector3(2.5f, 0.20f, 1.25f);
        underbody.Tint = new Color4(0.13f, 0.14f, 0.17f, 1.0f);

        // Caroserie principală aerodinamică (electric sapphire blue)
        SceneObject body = AddObject("caroserie", cube, _carRoot);
        body.Transform.Position = new Vector3(-0.05f, 0.22f, 0.0f);
        body.Transform.Scale = new Vector3(2.15f, 0.40f, 1.15f);
        body.Tint = new Color4(0.08f, 0.50f, 0.88f, 1.0f);

        // Splitter față (+X local)
        SceneObject splitter = AddObject("splitter_față", cube, _carRoot);
        splitter.Transform.Position = new Vector3(1.15f, 0.08f, 0.0f);
        splitter.Transform.Scale = new Vector3(0.40f, 0.12f, 1.10f);
        splitter.Tint = new Color4(0.16f, 0.17f, 0.20f, 1.0f);

        // Cabină / cupolă aerodinamică (sticlă fumurie cyan)
        SceneObject cabin = AddObject("cabină", cube, _carRoot);
        cabin.Transform.Position = new Vector3(-0.15f, 0.52f, 0.0f);
        cabin.Transform.Scale = new Vector3(1.15f, 0.38f, 0.90f);
        cabin.Tint = new Color4(0.06f, 0.14f, 0.24f, 1.0f);

        // Priză aer / aripioară plafon
        SceneObject roofTrim = AddObject("plafon", cube, _carRoot);
        roofTrim.Transform.Position = new Vector3(-0.15f, 0.74f, 0.0f);
        roofTrim.Transform.Scale = new Vector3(0.65f, 0.08f, 0.40f);
        roofTrim.Tint = new Color4(0.08f, 0.50f, 0.88f, 1.0f);

        // Eleron sport spate (-X local)
        SceneObject wingPostL = AddObject("suport_eleron_stânga", cube, _carRoot);
        wingPostL.Transform.Position = new Vector3(-1.0f, 0.45f, 0.35f);
        wingPostL.Transform.Scale = new Vector3(0.08f, 0.35f, 0.06f);
        wingPostL.Tint = new Color4(0.13f, 0.14f, 0.17f, 1.0f);

        SceneObject wingPostR = AddObject("suport_eleron_dreapta", cube, _carRoot);
        wingPostR.Transform.Position = new Vector3(-1.0f, 0.45f, -0.35f);
        wingPostR.Transform.Scale = new Vector3(0.08f, 0.35f, 0.06f);
        wingPostR.Tint = new Color4(0.13f, 0.14f, 0.17f, 1.0f);

        SceneObject spoiler = AddObject("eleron", cube, _carRoot);
        spoiler.Transform.Position = new Vector3(-1.0f, 0.63f, 0.0f);
        spoiler.Transform.Scale = new Vector3(0.35f, 0.06f, 1.30f);
        spoiler.Tint = new Color4(0.08f, 0.58f, 0.95f, 1.0f);

        // Faruri LED față (+X local)
        SceneObject headL = AddObject("far stânga", cube, _carRoot);
        headL.Transform.Position = new Vector3(1.18f, 0.22f, 0.36f);
        headL.Transform.Scale = new Vector3(0.12f, 0.10f, 0.28f);
        headL.Tint = new Color4(0.85f, 0.96f, 1.0f, 1.0f);

        SceneObject headR = AddObject("far dreapta", cube, _carRoot);
        headR.Transform.Position = new Vector3(1.18f, 0.22f, -0.36f);
        headR.Transform.Scale = new Vector3(0.12f, 0.10f, 0.28f);
        headR.Tint = new Color4(0.85f, 0.96f, 1.0f, 1.0f);

        // Bandă stop LED spate (-X local)
        SceneObject tailBar = AddObject("stop_spate", cube, _carRoot);
        tailBar.Transform.Position = new Vector3(-1.18f, 0.24f, 0.0f);
        tailBar.Transform.Scale = new Vector3(0.08f, 0.10f, 0.95f);
        tailBar.Tint = new Color4(1.0f, 0.15f, 0.15f, 1.0f);

        // Evacuări duble spate
        SceneObject exhaustL = AddObject("evacuare_stânga", cylinder, _carRoot);
        exhaustL.Transform.Position = new Vector3(-1.18f, 0.08f, 0.25f);
        exhaustL.Transform.Rotation = new Vector3(0.0f, 0.0f, 90.0f);
        exhaustL.Transform.Scale = new Vector3(0.20f, 0.15f, 0.20f);
        exhaustL.Tint = new Color4(0.75f, 0.78f, 0.82f, 1.0f);

        SceneObject exhaustR = AddObject("evacuare_dreapta", cylinder, _carRoot);
        exhaustR.Transform.Position = new Vector3(-1.18f, 0.08f, -0.25f);
        exhaustR.Transform.Rotation = new Vector3(0.0f, 0.0f, 90.0f);
        exhaustR.Transform.Scale = new Vector3(0.20f, 0.15f, 0.20f);
        exhaustR.Tint = new Color4(0.75f, 0.78f, 0.82f, 1.0f);

        // Roți cu jante argintii (copil al roții -> se rotește odată cu roata)
        float[] wheelX = { 0.75f, -0.75f };
        float[] wheelZ = { 0.65f, -0.65f };
        foreach (float wx in wheelX)
        {
            foreach (float wz in wheelZ)
            {
                SceneObject wheel = AddObject("roată", cylinder, _carRoot);
                wheel.Transform.Position = new Vector3(wx, -0.25f, wz);
                wheel.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f); // culcat pe Z
                wheel.Transform.Scale = new Vector3(0.92f);
                wheel.Tint = new Color4(0.12f, 0.12f, 0.14f, 1.0f);
                _wheels.Add(wheel);

                // Jantă argintie în interiorul roții
                SceneObject rim = AddObject("jantă", cylinder, wheel);
                rim.Transform.Position = Vector3.Zero;
                rim.Transform.Scale = new Vector3(0.58f, 1.04f, 0.58f);
                rim.Tint = new Color4(0.85f, 0.88f, 0.92f, 1.0f);
            }
        }
    }

    private void BuildOrbitingSphere(Mesh cubeMesh, Mesh sphereMesh, Mesh cylinderMesh)
    {
        // Pivot copil al mașinii → satelitul urmează automat mașina
        _orbitPivot = AddObject("pivot orbită", null, _carRoot);
        _orbitPivot.Spin = new Vector3(0.0f, OrbitSpeedDeg, 0.0f);

        // Miezul sferic al satelitului
        _sphere = AddObject("sferă orbită", sphereMesh, _orbitPivot);
        _sphere.Transform.Position = new Vector3(OrbitRadius, 0.75f, 0.0f);
        _sphere.Transform.Scale = new Vector3(0.65f);
        _sphere.Tint = new Color4(1.0f, 0.72f, 0.15f, 1.0f);
        _sphere.Spin = new Vector3(0.0f, 200.0f, 0.0f); // rotație proprie a satelitului

        // Panouri solare atașate sferei (se rotesc odată cu ea)
        SceneObject wingL = AddObject("panou_solar_1", cubeMesh, _sphere);
        wingL.Transform.Position = new Vector3(0.0f, 0.0f, 0.65f);
        wingL.Transform.Scale = new Vector3(0.40f, 0.06f, 0.65f);
        wingL.Tint = new Color4(0.14f, 0.38f, 0.78f, 1.0f);

        SceneObject wingR = AddObject("panou_solar_2", cubeMesh, _sphere);
        wingR.Transform.Position = new Vector3(0.0f, 0.0f, -0.65f);
        wingR.Transform.Scale = new Vector3(0.40f, 0.06f, 0.65f);
        wingR.Tint = new Color4(0.14f, 0.38f, 0.78f, 1.0f);

        // Antenă / sondă frontală
        SceneObject antenna = AddObject("antenă_satelit", cylinderMesh, _sphere);
        antenna.Transform.Position = new Vector3(0.38f, 0.0f, 0.0f);
        antenna.Transform.Rotation = new Vector3(0.0f, 0.0f, 90.0f);
        antenna.Transform.Scale = new Vector3(0.15f, 0.35f, 0.15f);
        antenna.Tint = new Color4(0.20f, 0.90f, 1.0f, 1.0f);
    }

    private void ResetCar()
    {
        _carRoot.Transform.Position = new Vector3(0.0f, CarY, 0.0f);
        _carRoot.Transform.Rotation = Vector3.Zero;
        _headingDeg = 0.0f;
        _speed = 0.0f;
        _orbitPivot.Transform.Rotation = Vector3.Zero;
        foreach (var w in _wheels)
            w.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
    }

    public override void Update(float dt, Input input)
    {
        if (input.WasPressed(Restart))
        {
            ResetCar();
            ConsolePanel.PrintMessage("mașina a fost resetată");
            return;
        }

        // 1. Input de conducere
        float throttle = input.Axis(Actions.Back, Actions.Forward); // W = +1 (înainte)
        float steer = input.Axis(Actions.Left, Actions.Right);      // D = +1 (dreapta)

        // 2. Viraj doar când te miști (ca o mașină reală)
        if (MathF.Abs(throttle) > 0.01f)
        {
            _headingDeg += steer * TurnSpeedDeg * dt * MathF.Sign(throttle);
        }
        // Normalizare unghi
        while (_headingDeg > 180f) _headingDeg -= 360f;
        while (_headingDeg < -180f) _headingDeg += 360f;

        // 3. Direcția de mers din heading (0° = +X)
        float rad = MathHelper.DegreesToRadians(_headingDeg);
        Vector3 forward = new(MathF.Cos(rad), 0.0f, MathF.Sin(rad));

        // 4. Deplasare
        _speed = throttle * MoveSpeed;
        Vector3 pos = _carRoot.Transform.Position;
        pos += forward * _speed * dt;

        // Limite teren
        pos.X = Math.Clamp(pos.X, PlayMin.X, PlayMax.X);
        pos.Z = Math.Clamp(pos.Z, PlayMin.Y, PlayMax.Y);
        pos.Y = CarY;

        _carRoot.Transform.Position = pos;
        // Orientăm mașina după heading (rotație pe Y)
        _carRoot.Transform.Rotation = new Vector3(0.0f, -_headingDeg, 0.0f);

        // 5. Roțile se rotesc în funcție de viteză
        float wheelSpin = -_speed * 90.0f; // grade / s
        foreach (var w in _wheels)
            w.Spin = new Vector3(0.0f, 0.0f, wheelSpin);

        // 6. Spin-uri (orbită + roți + satelit)
        base.Update(dt, input);
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe())
            yield return line;

        yield return $"Mașină         : pos {Fmt(_carRoot.Transform.Position)}  heading {_headingDeg:F1}°  viteză {_speed:F2}";
        yield return $"Sferă pe orbită: rază {OrbitRadius:F1}  unghi {_orbitPivot.Transform.Rotation.Y:F1}°";
        yield return $"Poziție sferă  : {Fmt(_sphere.WorldPosition)}";
    }

    private static string Fmt(Vector3 v) => $"({v.X:F2}, {v.Y:F2}, {v.Z:F2})";
}
