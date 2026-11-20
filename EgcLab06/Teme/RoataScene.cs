// Varianta C - roata panoramica
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

// Butucul este un pivot care se roteste in jurul lui Z si poarta spitele, janta si cabinele.
// Fiecare cabina se roteste invers fata de roata ca sa ramana verticala.
// Sageti sus/jos = viteza tinta, Space = oprire/pornire lina, 1-8 = cabina aleasa vine la peron.
public sealed class RoataScene : Scene
{
    private const string VitezaPlus = "roata: viteza tinta +";
    private const string VitezaMinus = "roata: viteza tinta -";
    private const string Stop = "roata: oprire / pornire";

    private const int NrCabine = 8;
    private static readonly string[] CabinaLaPeron =
        Enumerable.Range(1, NrCabine).Select(i => $"cabina {i} la peron").ToArray();

    private const float Raza = 4.0f;
    private const float InaltimeButuc = 6.5f;
    private const float PasViteza = 5.0f;     // grade/s la o apasare
    private const float VitezaMax = 45.0f;
    private const float K = 1.2f;             // cat de repede ajunge viteza la tinta

    // bonus: leganarea cabinelor (resort amortizat)
    private const float Rigiditate = 25.0f;
    private const float Amortizare = 3.0f;
    private const float Leganare = 2.0f;
    private const float LeganareMax = 30.0f;

    private SceneObject _butuc = null!;
    private readonly List<SceneObject> _cabine = new();

    private float _unghi;                     // unghiul rotii, in grade
    private float _viteza;                    // grade/s
    private float _vitezaTinta = 15.0f;
    private float _vitezaSalvata;
    private bool _oprita;

    private Tween? _tween;                    // doar cand aducem o cabina la peron
    private int _cabinaAleasa = -1;

    private float _omegaAnterior;
    private float _leganare;
    private float _vitezaLeganare;

    public override string Name => "tema C - roata panoramica";

    public override string Status
    {
        get
        {
            if (_tween != null)
            {
                return $"cabina {_cabinaAleasa + 1} vine la peron";
            }
            string oprita = _oprita ? " (oprita)" : "";
            return $"v = {_viteza:F1} grd/s, tinta {_vitezaTinta:F0}{oprita}";
        }
    }

    public override IEnumerable<string> ActionNames =>
        new[] { VitezaPlus, VitezaMinus, Stop }.Concat(CabinaLaPeron);

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 5.0f, 17.0f);
        CameraTarget = new Vector3(0.0f, 4.5f, 0.0f);

        input.Bind(VitezaPlus, Keys.Up);
        input.Bind(VitezaMinus, Keys.Down);
        input.Bind(Stop, Keys.Space);
        for (int i = 0; i < NrCabine; i++)
        {
            input.Bind(CabinaLaPeron[i], Keys.D1 + i);
        }

        Mesh grid = AddMesh(Shapes.Grid(10, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh cub = AddMesh(Shapes.ShadedCube());
        Mesh cilindru = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));

        AddObject("podea", grid);

        ConstruiesteSuport(cub, cilindru);
        ConstruiesteRoata(cub);
        ConstruiestePeron(cub);
    }

    private void ConstruiesteSuport(Mesh cub, Mesh cilindru)
    {
        var gri = new Color4(0.6f, 0.6f, 0.65f, 1.0f);
        float dx = 3.0f;   // cat de departe sunt picioarele de centru, jos
        float lungime = MathF.Sqrt(dx * dx + InaltimeButuc * InaltimeButuc);
        float inclinare = MathHelper.RadiansToDegrees(MathF.Atan2(dx, InaltimeButuc));

        // doua perechi de picioare in forma de A, in fata si in spatele rotii
        foreach (float z in new[] { -1.0f, 1.0f })
        {
            foreach (float semn in new[] { -1.0f, 1.0f })
            {
                SceneObject picior = AddObject("picior", cub);
                picior.Transform.Position = new Vector3(semn * dx / 2, InaltimeButuc / 2, z);
                picior.Transform.Rotation = new Vector3(0.0f, 0.0f, semn * inclinare);
                picior.Transform.Scale = new Vector3(0.25f, lungime, 0.25f);
                picior.Tint = gri;
            }
        }

        SceneObject ax = AddObject("ax", cilindru);
        ax.Transform.Position = new Vector3(0.0f, InaltimeButuc, 0.0f);
        ax.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);   // axa cilindrului pe Z
        ax.Transform.Scale = new Vector3(0.6f, 2.3f, 0.6f);
        ax.Tint = new Color4(0.4f, 0.4f, 0.45f, 1.0f);
    }

    private void ConstruiesteRoata(Mesh cub)
    {
        _butuc = AddObject("butuc", null);
        _butuc.Transform.Position = new Vector3(0.0f, InaltimeButuc, 0.0f);

        var alb = new Color4(0.9f, 0.9f, 0.95f, 1.0f);

        // spitele si janta, o data in fata (z = 0.6) si o data in spate (z = -0.6)
        foreach (float z in new[] { -0.6f, 0.6f })
        {
            for (int i = 0; i < NrCabine; i++)
            {
                float a = UnghiCabina(i);
                float rad = MathHelper.DegreesToRadians(a);
                SceneObject spita = AddObject("spita", cub, _butuc);
                spita.Transform.Position = new Vector3(MathF.Cos(rad) * Raza / 2, MathF.Sin(rad) * Raza / 2, z);
                spita.Transform.Rotation = new Vector3(0.0f, 0.0f, a - 90.0f);   // bara e pe Y, o intorc spre unghiul a
                spita.Transform.Scale = new Vector3(0.08f, Raza, 0.08f);
                spita.Tint = alb;
            }

            int segmente = 24;
            float lungSeg = 2.0f * Raza * MathF.Sin(MathF.PI / segmente) * 1.05f;
            for (int i = 0; i < segmente; i++)
            {
                float a = (i + 0.5f) * 360.0f / segmente;
                float rad = MathHelper.DegreesToRadians(a);
                SceneObject seg = AddObject("janta", cub, _butuc);
                seg.Transform.Position = new Vector3(Raza * MathF.Cos(rad), Raza * MathF.Sin(rad), z);
                seg.Transform.Rotation = new Vector3(0.0f, 0.0f, a + 90.0f);     // tangent la cerc
                seg.Transform.Scale = new Vector3(lungSeg, 0.12f, 0.12f);
                seg.Tint = alb;
            }
        }

        // cabinele: un pivot pe janta (copil al butucului), forma atarna sub el
        for (int i = 0; i < NrCabine; i++)
        {
            float rad = MathHelper.DegreesToRadians(UnghiCabina(i));
            SceneObject pivot = AddObject($"cabina {i + 1}", null, _butuc);
            pivot.Transform.Position = new Vector3(Raza * MathF.Cos(rad), Raza * MathF.Sin(rad), 0.0f);
            _cabine.Add(pivot);

            Color4 culoare = ColorUtil.FromHsv(i / (float)NrCabine, 0.6f, 1.0f);

            SceneObject bolt = AddObject("bolt", cub, pivot);
            bolt.Transform.Scale = new Vector3(0.1f, 0.1f, 1.3f);
            bolt.Tint = new Color4(0.5f, 0.5f, 0.55f, 1.0f);

            SceneObject tija = AddObject("tija", cub, pivot);
            tija.Transform.Position = new Vector3(0.0f, -0.22f, 0.0f);
            tija.Transform.Scale = new Vector3(0.06f, 0.45f, 0.06f);
            tija.Tint = new Color4(0.5f, 0.5f, 0.55f, 1.0f);

            SceneObject acoperis = AddObject("acoperis", cub, pivot);
            acoperis.Transform.Position = new Vector3(0.0f, -0.45f, 0.0f);
            acoperis.Transform.Scale = new Vector3(1.05f, 0.1f, 1.05f);
            acoperis.Tint = culoare;

            SceneObject corp = AddObject("corp cabina", cub, pivot);
            corp.Transform.Position = new Vector3(0.0f, -0.88f, 0.0f);
            corp.Transform.Scale = new Vector3(0.9f, 0.75f, 0.9f);
            corp.Tint = culoare;
        }
    }

    private void ConstruiestePeron(Mesh cub)
    {
        // putin sub cabina care e jos
        float h = InaltimeButuc - Raza - 0.88f - 0.375f - 0.05f;
        SceneObject peron = AddObject("peron", cub);
        peron.Transform.Position = new Vector3(0.0f, h / 2, 0.0f);
        peron.Transform.Scale = new Vector3(3.0f, h, 2.6f);
        peron.Tint = new Color4(0.55f, 0.4f, 0.3f, 1.0f);
    }

    private static float UnghiCabina(int i) => i * 360.0f / NrCabine;

    // aduce unghiul in (-180, 180]
    private static float Normalizeaza(float a)
    {
        a %= 360.0f;
        if (a > 180.0f) a -= 360.0f;
        if (a <= -180.0f) a += 360.0f;
        return a;
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        if (input.WasPressed(VitezaPlus)) SchimbaViteza(PasViteza);
        if (input.WasPressed(VitezaMinus)) SchimbaViteza(-PasViteza);

        if (input.WasPressed(Stop))
        {
            if (_oprita)
            {
                AnuleazaTween();
                _oprita = false;
                _vitezaTinta = _vitezaSalvata;
                ConsolePanel.PrintMessage("roata porneste");
            }
            else
            {
                _oprita = true;
                _vitezaSalvata = _vitezaTinta;
                _vitezaTinta = 0.0f;
                ConsolePanel.PrintMessage("roata se opreste");
            }
        }

        for (int i = 0; i < NrCabine; i++)
        {
            if (input.WasPressed(CabinaLaPeron[i]))
            {
                AduLaPeron(i);
            }
        }

        if (dt > 0.0f)
        {
            float omega;
            if (_tween != null)
            {
                float vechi = _unghi;
                _tween.Update(dt);
                _unghi = _tween.Value.X;
                omega = (_unghi - vechi) / dt;

                if (_tween.IsFinished)
                {
                    ConsolePanel.PrintMessage($"cabina {_cabinaAleasa + 1} a ajuns la peron");
                    _tween = null;
                    _cabinaAleasa = -1;
                }
            }
            else
            {
                // se apropie lin de tinta, la fel la orice FPS
                _viteza += (_vitezaTinta - _viteza) * (1.0f - MathF.Exp(-K * dt));
                if (_vitezaTinta == 0.0f && MathF.Abs(_viteza) < 0.01f) _viteza = 0.0f;
                _unghi = (_unghi + _viteza * dt) % 360.0f;
                omega = _viteza;
            }

            ActualizeazaLeganare(dt, omega);
        }

        _butuc.Transform.Rotation = new Vector3(0.0f, 0.0f, _unghi);
        foreach (SceneObject cabina in _cabine)
        {
            // invers fata de roata => ramane verticala
            cabina.Transform.Rotation = new Vector3(0.0f, 0.0f, -_unghi + _leganare);
        }
    }

    private void SchimbaViteza(float delta)
    {
        AnuleazaTween();
        if (_oprita)
        {
            _oprita = false;
            _vitezaTinta = _vitezaSalvata;
        }
        _vitezaTinta = Math.Clamp(_vitezaTinta + delta, -VitezaMax, VitezaMax);
    }

    private void AduLaPeron(int i)
    {
        // peronul e jos, la -90 de grade; aleg drumul cel mai scurt (max 180 grade)
        float unghiAcum = UnghiCabina(i) + _unghi;
        float diferenta = Normalizeaza(-90.0f - unghiAcum);

        if (!_oprita)
        {
            _vitezaSalvata = _vitezaTinta;
            _oprita = true;
        }
        _vitezaTinta = 0.0f;
        _viteza = 0.0f;

        float durata = 1.0f + MathF.Abs(diferenta) / 90.0f;
        _tween = new Tween(new Vector3(_unghi, 0.0f, 0.0f), new Vector3(_unghi + diferenta, 0.0f, 0.0f),
                           durata, Easing.EaseInOut);
        _cabinaAleasa = i;
        ConsolePanel.PrintMessage($"cabina {i + 1} -> peron ({diferenta:F0} grd, {durata:F1} s)");
    }

    private void AnuleazaTween()
    {
        if (_tween == null) return;
        _tween = null;
        _cabinaAleasa = -1;
        _viteza = _omegaAnterior;   // continua de unde era, fara salt
    }

    private void ActualizeazaLeganare(float dt, float omega)
    {
        float alfa = (omega - _omegaAnterior) / dt;   // acceleratia unghiulara a rotii
        _omegaAnterior = omega;

        // pasi mici ca sa nu explodeze resortul cand dt e mare (B sau timp x4)
        int pasi = (int)MathF.Ceiling(dt / 0.02f);
        float h = dt / pasi;
        for (int k = 0; k < pasi; k++)
        {
            float acc = -Rigiditate * _leganare - Amortizare * _vitezaLeganare - Leganare * alfa;
            _vitezaLeganare += acc * h;
            _leganare = Math.Clamp(_leganare + _vitezaLeganare * h, -LeganareMax, LeganareMax);
        }
    }

    private int CabinaJos()
    {
        int best = 0;
        float min = float.MaxValue;
        for (int i = 0; i < NrCabine; i++)
        {
            float d = MathF.Abs(Normalizeaza(UnghiCabina(i) + _unghi + 90.0f));
            if (d < min)
            {
                min = d;
                best = i;
            }
        }
        return best;
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;

        string tween = _tween == null ? "-" : $"cabina {_cabinaAleasa + 1}, progres {_tween.Progress:P0}";
        yield return $"Unghi roata    : {_unghi:F1} grd   cabina de jos: {CabinaJos() + 1}";
        yield return $"Viteza         : {_viteza:F2} grd/s   tinta: {_vitezaTinta:F1}   oprita: {(_oprita ? "da" : "nu")}";
        yield return $"Tween peron    : {tween}";
        yield return $"Leganare       : {_leganare:F1} grd";
    }
}
