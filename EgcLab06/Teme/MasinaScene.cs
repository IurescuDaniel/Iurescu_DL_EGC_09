// Cerinta suplimentara - masinuta condusa cu sagetile (EaseInOut, miscare cu dt)
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

// Sus/jos = accelereaza inainte/inapoi, stanga/dreapta = volanul, Space = frana, R = inapoi la start.
// Acceleratia si volanul nu sar direct la maxim: un progres 0..1 creste cu dt / timp,
// iar viteza si unghiul rotilor se iau prin EaseInOut din progresul asta (pornire si oprire line).
// Pozitia se schimba doar cu viteza * dt, deci pauza si scara timpului merg si aici.
public sealed class MasinaScene : Scene
{
    private const string Inainte = "masina: inainte";
    private const string Inapoi = "masina: inapoi";
    private const string Stanga = "masina: volan stanga";
    private const string Dreapta = "masina: volan dreapta";
    private const string Frana = "masina: frana";
    private const string LaStart = "masina: inapoi la start";

    private const float VitezaMax = 7.0f;        // unitati/s inainte
    private const float VitezaMaxInapoi = 3.5f;
    private const float TimpAcceleratie = 1.2f;  // secunde pana la viteza maxima
    private const float TimpFrana = 0.5f;
    private const float TimpVolan = 0.35f;
    private const float VolanMax = 30.0f;        // grade
    private const float Ampatament = 1.9f;       // distanta dintre axe
    private const float RazaRoata = 0.35f;
    private const float Limita = 9.0f;           // cat de departe poate merge pe podea

    private static readonly Vector3 Start = new(0.0f, 0.0f, 5.0f);

    private SceneObject _masina = null!;
    private SceneObject _rotiStangaFata = null!, _rotiDreaptaFata = null!;
    private readonly List<SceneObject> _rotiSpin = new();

    private float _acceleratie;   // -1..1 (negativ = mers inapoi)
    private float _volan;         // -1..1 (pozitiv = stanga)
    private float _viteza;        // unitati/s
    private float _unghiVolan;    // grade
    private float _directie;      // grade, rotatia masinii in jurul lui Y
    private float _rotireRoti;    // grade
    private Vector3 _pozitie = Start;

    public override string Name => "masinuta";

    public override string Status => $"v = {_viteza:F1} u/s, volan {_unghiVolan:F0} grd";

    public override IEnumerable<string> ActionNames => new[] { Inainte, Inapoi, Stanga, Dreapta, Frana, LaStart };

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 14.0f, 16.0f);
        CameraTarget = new Vector3(0.0f, 0.0f, 0.5f);

        input.Bind(Inainte, Keys.Up);
        input.Bind(Inapoi, Keys.Down);
        input.Bind(Stanga, Keys.Left);
        input.Bind(Dreapta, Keys.Right);
        input.Bind(Frana, Keys.Space);
        input.Bind(LaStart, Keys.R);

        Mesh grid = AddMesh(Shapes.Grid(10, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh cub = AddMesh(Shapes.ShadedCube());
        Mesh cilindru = AddMesh(Shapes.ShadedCylinder(0.5f, 1.0f, 24));

        AddObject("podea", grid);
        PuneConuri(cilindru);
        ConstruiesteMasina(cub, cilindru);
    }

    private void PuneConuri(Mesh cilindru)
    {
        // cateva "conuri" ca sa se vada ca masina se misca
        Vector2[] pozitii =
        {
            new(-5.0f, -5.0f), new(5.0f, -5.0f), new(-5.0f, 3.0f), new(5.0f, 3.0f), new(0.0f, -1.0f),
        };
        foreach (Vector2 p in pozitii)
        {
            SceneObject con = AddObject("con", cilindru);
            con.Transform.Position = new Vector3(p.X, 0.35f, p.Y);
            con.Transform.Scale = new Vector3(0.5f, 0.7f, 0.5f);
            con.Tint = new Color4(1.0f, 0.5f, 0.1f, 1.0f);
        }
    }

    private void ConstruiesteMasina(Mesh cub, Mesh cilindru)
    {
        // pivotul masinii: pozitia si directia. Fata masinii este spre -Z.
        _masina = AddObject("masina", null);
        _masina.Transform.Position = _pozitie;

        SceneObject caroserie = AddObject("caroserie", cub, _masina);
        caroserie.Transform.Position = new Vector3(0.0f, 0.6f, 0.0f);
        caroserie.Transform.Scale = new Vector3(1.5f, 0.5f, 3.0f);
        caroserie.Tint = new Color4(0.9f, 0.15f, 0.15f, 1.0f);

        SceneObject cabina = AddObject("cabina", cub, _masina);
        cabina.Transform.Position = new Vector3(0.0f, 1.1f, 0.25f);
        cabina.Transform.Scale = new Vector3(1.3f, 0.5f, 1.5f);
        cabina.Tint = new Color4(0.55f, 0.75f, 0.95f, 1.0f);

        foreach (float x in new[] { -0.5f, 0.5f })
        {
            SceneObject far = AddObject("far", cub, _masina);
            far.Transform.Position = new Vector3(x, 0.65f, -1.52f);
            far.Transform.Scale = new Vector3(0.3f, 0.15f, 0.05f);
            far.Tint = new Color4(1.0f, 1.0f, 0.7f, 1.0f);

            SceneObject stop = AddObject("stop", cub, _masina);
            stop.Transform.Position = new Vector3(x, 0.65f, 1.52f);
            stop.Transform.Scale = new Vector3(0.3f, 0.15f, 0.05f);
            stop.Tint = new Color4(0.8f, 0.0f, 0.0f, 1.0f);
        }

        // rotile din fata au un pivot in plus pentru volan
        _rotiStangaFata = AddObject("volan stanga", null, _masina);
        _rotiStangaFata.Transform.Position = new Vector3(-0.8f, RazaRoata, -1.0f);
        _rotiDreaptaFata = AddObject("volan dreapta", null, _masina);
        _rotiDreaptaFata.Transform.Position = new Vector3(0.8f, RazaRoata, -1.0f);

        AdaugaRoata(_rotiStangaFata, Vector3.Zero, cub, cilindru);
        AdaugaRoata(_rotiDreaptaFata, Vector3.Zero, cub, cilindru);
        AdaugaRoata(_masina, new Vector3(-0.8f, RazaRoata, 1.0f), cub, cilindru);
        AdaugaRoata(_masina, new Vector3(0.8f, RazaRoata, 1.0f), cub, cilindru);
    }

    private void AdaugaRoata(SceneObject parinte, Vector3 pozitie, Mesh cub, Mesh cilindru)
    {
        // pivotul se invarte in jurul lui X (axul rotii)
        SceneObject spin = AddObject("roata", null, parinte);
        spin.Transform.Position = pozitie;
        _rotiSpin.Add(spin);

        SceneObject anvelopa = AddObject("anvelopa", cilindru, spin);
        anvelopa.Transform.Rotation = new Vector3(0.0f, 0.0f, 90.0f);   // axa cilindrului pe X
        anvelopa.Transform.Scale = new Vector3(2 * RazaRoata, 0.3f, 2 * RazaRoata);
        anvelopa.Tint = new Color4(0.25f, 0.25f, 0.28f, 1.0f);

        // o dunga pe janta, ca sa se vada cum se invarte roata
        SceneObject dunga = AddObject("dunga", cub, spin);
        dunga.Transform.Scale = new Vector3(0.32f, 2 * RazaRoata * 0.8f, 0.1f);
        dunga.Tint = new Color4(0.85f, 0.85f, 0.85f, 1.0f);
    }

    // muta valoarea spre tinta cu cel mult pas
    private static float SpreTinta(float valoare, float tinta, float pas)
    {
        if (MathF.Abs(tinta - valoare) <= pas) return tinta;
        return valoare + MathF.Sign(tinta - valoare) * pas;
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        if (input.WasPressed(LaStart))
        {
            _pozitie = Start;
            _directie = 0.0f;
            _acceleratie = 0.0f;
            _volan = 0.0f;
            ConsolePanel.PrintMessage("masina e inapoi la start");
        }

        // 1. pedala: progresul merge spre -1, 0 sau 1 cu dt / TimpAcceleratie
        float pedala = input.Axis(Inapoi, Inainte);
        if (input.IsDown(Frana))
        {
            _acceleratie = SpreTinta(_acceleratie, 0.0f, dt / TimpFrana);
        }
        else
        {
            // fara taste masina se opreste singura, tot lin
            _acceleratie = SpreTinta(_acceleratie, pedala, dt / TimpAcceleratie);
        }

        // viteza din EaseInOut: porneste incet, accelereaza, apoi se stabilizeaza
        if (_acceleratie >= 0.0f)
            _viteza = VitezaMax * Easing.EaseInOut(_acceleratie);
        else
            _viteza = -VitezaMaxInapoi * Easing.EaseInOut(-_acceleratie);

        // 2. volanul, la fel
        float volan = input.Axis(Dreapta, Stanga);
        _volan = SpreTinta(_volan, volan, dt / TimpVolan);
        _unghiVolan = MathF.Sign(_volan) * VolanMax * Easing.EaseInOut(MathF.Abs(_volan));

        // 3. virajul depinde de viteza (la viteza 0 masina nu se roteste pe loc)
        float virajRad = _viteza / Ampatament * MathF.Tan(MathHelper.DegreesToRadians(_unghiVolan));
        _directie += MathHelper.RadiansToDegrees(virajRad) * dt;

        // 4. deplasarea: viteza * dt pe directia in care e intoarsa masina
        float rad = MathHelper.DegreesToRadians(_directie);
        var inainte = new Vector3(-MathF.Sin(rad), 0.0f, -MathF.Cos(rad));
        _pozitie += inainte * _viteza * dt;

        if (MathF.Abs(_pozitie.X) > Limita || MathF.Abs(_pozitie.Z) > Limita)
        {
            _pozitie.X = Math.Clamp(_pozitie.X, -Limita, Limita);
            _pozitie.Z = Math.Clamp(_pozitie.Z, -Limita, Limita);
            _acceleratie = 0.0f;   // a lovit marginea
        }

        // rotile: unghiul parcurs = distanta / raza (spre -Z rotatia in jurul lui X e negativa)
        _rotireRoti -= MathHelper.RadiansToDegrees(_viteza / RazaRoata) * dt;
        _rotireRoti %= 360.0f;

        _masina.Transform.Position = _pozitie;
        _masina.Transform.Rotation = new Vector3(0.0f, _directie, 0.0f);
        _rotiStangaFata.Transform.Rotation = new Vector3(0.0f, _unghiVolan, 0.0f);
        _rotiDreaptaFata.Transform.Rotation = new Vector3(0.0f, _unghiVolan, 0.0f);
        foreach (SceneObject roata in _rotiSpin)
        {
            roata.Transform.Rotation = new Vector3(_rotireRoti, 0.0f, 0.0f);
        }
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Pozitie        : ({_pozitie.X:F2}, {_pozitie.Z:F2})   directie: {_directie % 360.0f:F1} grd";
        yield return $"Acceleratie    : {_acceleratie:F2}   viteza: {_viteza:F2} u/s";
        yield return $"Volan          : {_volan:F2}   unghi roti: {_unghiVolan:F1} grd";
    }
}
