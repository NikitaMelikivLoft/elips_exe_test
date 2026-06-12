using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows.Forms;

namespace EllipsometrySolver;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "outputs");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "error.log"), ex.ToString(), Encoding.UTF8);
            }
            catch
            {
                // ignored
            }

            MessageBox.Show(
                "Программа не смогла запуститься.\n\n" + ex.Message + "\n\nПодробности сохранены в outputs/error.log",
                "Ошибка запуска",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}

public sealed class Material
{
    public string Key { get; }
    public string Label { get; }
    public double N { get; }
    public double K { get; }

    public Material(string key, string label, double n, double k)
    {
        Key = key;
        Label = label;
        N = n;
        K = k;
    }

    public override string ToString() => $"{Key} — {Label}";
}

public sealed class CalcRow
{
    public int D { get; init; }
    public Complex Beta { get; init; }
    public Complex Q { get; init; }
    public Complex Rho { get; init; }
    public double Psi { get; init; }
    public double Delta { get; init; }
    public double DeltaDelta { get; init; }
    public double F { get; init; }
}

public sealed class CalcResult
{
    public List<CalcRow> Rows { get; init; } = new();
    public CalcRow Best { get; init; } = new();
    public Dictionary<string, Complex> Constants { get; init; } = new();
    public Dictionary<string, string> Notes { get; init; } = new();
}

public sealed class MainForm : Form
{
    private readonly Dictionary<string, Material> _materials = new()
    {
        ["Air"] = new("Air", "Air / воздух", 1.0, 0.0),
        ["Acrylic"] = new("Acrylic", "Acrylic / PMMA", 1.48899, 0.0),
        ["Al"] = new("Al", "Al / Aluminium", 1.37289, 7.617691),
        ["Al2O3"] = new("Al2O3", "Al2O3 / Aluminium oxide", 1.77, 0.0),
        ["AlN"] = new("AlN", "AlN / Aluminium nitride", 2.16468, 0.0),
        ["Ag"] = new("Ag", "Ag / Silver", 0.13511, 3.985137),
        ["Au"] = new("Au", "Au / Gold", 0.18104, 3.068099),
        ["Cr"] = new("Cr", "Cr / Chromium", 3.13628, 3.31186),
        ["Cu"] = new("Cu", "Cu / Copper", 0.23883, 3.415658),
        ["GaN"] = new("GaN", "GaN / Gallium nitride", 2.37966, 0.0),
        ["InP"] = new("InP", "InP / Indium phosphide", 3.53635, 0.3075118),
        ["ITO"] = new("ITO", "ITO / Indium tin oxide", 1.85844, 0.0580774),
        ["Pt"] = new("Pt", "Pt / Platinum", 2.32694, 4.148077),
        ["Quartz"] = new("Quartz", "Quartz", 1.54259, 0.0),
        ["Si"] = new("Si", "Si / Silicon", 3.88163, 0.01896923),
        ["SiO2"] = new("SiO2", "SiO2 / Silicon dioxide", 1.45704, 0.0),
        ["Si3N4"] = new("Si3N4", "Si3N4 / Silicon nitride", 2.02252, 0.0),
        ["Ta"] = new("Ta", "Ta / Tantalum", 1.72416, 2.075176),
        ["Ti"] = new("Ti", "Ti / Titanium", 2.15349, 2.923488),
        ["W"] = new("W", "W / Tungsten", 3.63739, 2.916877),
    };

    private readonly TextBox _psi = Entry();
    private readonly TextBox _delta = Entry();
    private readonly TextBox _lambda = Entry();
    private readonly TextBox _theta = Entry();
    private readonly TextBox _dMin = Entry();
    private readonly TextBox _dMax = Entry();
    private readonly TextBox _step = Entry();

    private readonly CheckBox _useDb = new() { Text = "Использовать встроенную базу материалов", Checked = true, AutoSize = true };

    private readonly ComboBox _m0 = Combo();
    private readonly ComboBox _m1 = Combo();
    private readonly ComboBox _m2 = Combo();

    private readonly TextBox _n0 = Entry();
    private readonly TextBox _k0 = Entry();
    private readonly TextBox _n1 = Entry();
    private readonly TextBox _k1 = Entry();
    private readonly TextBox _n2 = Entry();
    private readonly TextBox _k2 = Entry();

    private readonly TextBox _resultBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 11, FontStyle.Bold) };
    private readonly TextBox _constantsBox = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 11) };
    private readonly DataGridView _grid = new();

    private CalcResult? _lastResult;

    public MainForm()
    {
        Text = "Эллиптический метод расчёта d";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1050, 720);
        Size = new Size(1180, 780);

        BuildUi();
        ClearFields();
    }

    private static TextBox Entry() => new() { Dock = DockStyle.Fill };
    private static ComboBox Combo() => new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Эллиптический метод расчёта d",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            AutoSize = true
        }, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(0, 8, 0, 8) };
        root.Controls.Add(buttons, 0, 1);

        AddButton(buttons, "Пример 1", (_, _) => LoadExample(1));
        AddButton(buttons, "Пример 2", (_, _) => LoadExample(2));
        AddButton(buttons, "Пример 3", (_, _) => LoadExample(3));
        AddButton(buttons, "Очистить", (_, _) => ClearFields());
        AddButton(buttons, "Рассчитать", (_, _) => CalculateAndRender());
        AddButton(buttons, "Сохранить CSV в outputs", (_, _) => SaveCsv());

        buttons.Controls.Add(new Label
        {
            Text = $"Папка сохранения: {OutputsDir().FullName}",
            AutoSize = true,
            Padding = new Padding(12, 9, 0, 0)
        });

        // Вместо SplitContainer используется TableLayoutPanel: он не падает на старте из-за SplitterDistance.
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410));
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(main, 0, 2);

        var left = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, 10, 0) };
        var right = new Panel { Dock = DockStyle.Fill };
        main.Controls.Add(left, 0, 0);
        main.Controls.Add(right, 1, 0);

        BuildLeft(left);
        BuildRight(right);
    }

    private static void AddButton(Control parent, string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 34, Margin = new Padding(0, 0, 6, 6) };
        b.Click += onClick;
        parent.Controls.Add(b);
    }

    private void BuildLeft(Control parent)
    {
        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        parent.Controls.Add(stack);

        var data = Group("Дано", 380, 230);
        stack.Controls.Add(data);

        var dataGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(8) };
        dataGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        dataGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        data.Controls.Add(dataGrid);

        AddField(dataGrid, "Ψ exp, град", _psi, 0, 0);
        AddField(dataGrid, "Δ exp, град", _delta, 0, 1);
        AddField(dataGrid, "λ, нм", _lambda, 2, 0);
        AddField(dataGrid, "θ₀, град", _theta, 2, 1);
        AddField(dataGrid, "d min, нм", _dMin, 4, 0);
        AddField(dataGrid, "d max, нм", _dMax, 4, 1);
        AddField(dataGrid, "шаг, нм", _step, 6, 0);

        var mats = Group("Материалы", 380, 255);
        stack.Controls.Add(mats);

        var matPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(8) };
        mats.Controls.Add(matPanel);
        matPanel.Controls.Add(_useDb, 0, 0);
        AddCombo(matPanel, "Среда 0", _m0, 1);
        AddCombo(matPanel, "Плёнка 1", _m1, 3);
        AddCombo(matPanel, "Подложка 2", _m2, 5);

        var manual = Group("Ручной ввод n,k", 380, 250);
        stack.Controls.Add(manual);

        var manGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, Padding = new Padding(8) };
        manGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        manGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        manual.Controls.Add(manGrid);

        AddField(manGrid, "n0", _n0, 0, 0);
        AddField(manGrid, "k0", _k0, 0, 1);
        AddField(manGrid, "n1", _n1, 2, 0);
        AddField(manGrid, "k1", _k1, 2, 1);
        AddField(manGrid, "n2", _n2, 4, 0);
        AddField(manGrid, "k2", _k2, 4, 1);
    }

    private void BuildRight(Control parent)
    {
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        parent.Controls.Add(right);

        var resultGroup = Group("Результат", 0, 0);
        resultGroup.Dock = DockStyle.Fill;
        resultGroup.Controls.Add(_resultBox);
        right.Controls.Add(resultGroup, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        right.Controls.Add(tabs, 0, 1);

        var tabTable = new TabPage("Массив расчётов");
        var tabConst = new TabPage("Промежуточные");
        tabs.TabPages.Add(tabTable);
        tabs.TabPages.Add(tabConst);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        _grid.RowHeadersVisible = false;
        tabTable.Controls.Add(_grid);

        foreach (var name in new[] { "d", "β", "q", "ρ", "Ψ", "Δ", "δΔ", "F" })
            _grid.Columns.Add(name, name);

        tabConst.Controls.Add(_constantsBox);
    }

    private static GroupBox Group(string title, int width, int height) => new()
    {
        Text = title,
        Width = width > 0 ? width : 360,
        Height = height > 0 ? height : 120,
        Margin = new Padding(0, 0, 0, 10)
    };

    private static void AddField(TableLayoutPanel panel, string label, TextBox textBox, int row, int col)
    {
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, AutoSize = true }, col, row);
        panel.Controls.Add(textBox, col, row + 1);
    }

    private void AddCombo(TableLayoutPanel panel, string label, ComboBox combo, int row)
    {
        combo.Items.Clear();
        foreach (var key in _materials.Keys.OrderBy(k => _materials[k].Label))
            combo.Items.Add(_materials[key]);

        panel.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Fill }, 0, row);
        panel.Controls.Add(combo, 0, row + 1);
    }

    private void SetMaterial(ComboBox combo, string key)
    {
        foreach (var item in combo.Items)
        {
            if (item is Material m && m.Key == key)
            {
                combo.SelectedItem = m;
                return;
            }
        }
    }

    private string SelectedKey(ComboBox combo) => combo.SelectedItem is Material m ? m.Key : "Air";

    private void ClearFields()
    {
        foreach (var tb in new[] { _psi, _delta, _lambda, _theta, _dMin, _dMax, _step, _n0, _k0, _n1, _k1, _n2, _k2 })
            tb.Text = "";

        _useDb.Checked = true;
        SetMaterial(_m0, "Air");
        SetMaterial(_m1, "SiO2");
        SetMaterial(_m2, "Si");

        _resultBox.Text = "Введите данные и нажмите «Рассчитать».";
        _constantsBox.Clear();
        _grid.Rows.Clear();
        _lastResult = null;
    }

    private void LoadExample(int n)
    {
        var ex = n switch { 1 => Example1(), 2 => Example2(), _ => Example3() };
        _useDb.Checked = false;

        _psi.Text = ex["psi"];
        _delta.Text = ex["delta"];
        _lambda.Text = ex["lam"];
        _theta.Text = ex["theta"];
        _dMin.Text = ex["dmin"];
        _dMax.Text = ex["dmax"];
        _step.Text = ex["step"];

        SetMaterial(_m0, ex["m0"]);
        SetMaterial(_m1, ex["m1"]);
        SetMaterial(_m2, ex["m2"]);

        _n0.Text = ex["n0"];
        _k0.Text = ex["k0"];
        _n1.Text = ex["n1"];
        _k1.Text = ex["k1"];
        _n2.Text = ex["n2"];
        _k2.Text = ex["k2"];

        CalculateAndRender();
    }

    private static Dictionary<string, string> Example1() => new()
    {
        ["psi"] = "22.854", ["delta"] = "264.158", ["lam"] = "632.8", ["theta"] = "70",
        ["dmin"] = "0", ["dmax"] = "150", ["step"] = "1",
        ["m0"] = "Air", ["m1"] = "SiO2", ["m2"] = "Si",
        ["n0"] = "1", ["k0"] = "0", ["n1"] = "1.457", ["k1"] = "0", ["n2"] = "3.881", ["k2"] = "0.019"
    };

    private static Dictionary<string, string> Example2() => new()
    {
        ["psi"] = "30.779", ["delta"] = "276.857", ["lam"] = "632.8", ["theta"] = "70",
        ["dmin"] = "0", ["dmax"] = "150", ["step"] = "1",
        ["m0"] = "Air", ["m1"] = "SiO2", ["m2"] = "Si",
        ["n0"] = "1", ["k0"] = "0", ["n1"] = "1.457", ["k1"] = "0", ["n2"] = "3.881", ["k2"] = "0.019"
    };

    private static Dictionary<string, string> Example3() => new()
    {
        ["psi"] = "40.180", ["delta"] = "334.160", ["lam"] = "632.8", ["theta"] = "70",
        ["dmin"] = "0", ["dmax"] = "150", ["step"] = "1",
        ["m0"] = "Air", ["m1"] = "Si3N4", ["m2"] = "Si",
        ["n0"] = "1", ["k0"] = "0", ["n1"] = "2.00", ["k1"] = "0", ["n2"] = "3.881", ["k2"] = "0.019"
    };

    private Dictionary<string, string> Params() => new()
    {
        ["psi"] = Clean(_psi.Text),
        ["delta"] = Clean(_delta.Text),
        ["lam"] = Clean(_lambda.Text),
        ["theta"] = Clean(_theta.Text),
        ["dmin"] = Clean(_dMin.Text),
        ["dmax"] = Clean(_dMax.Text),
        ["step"] = Clean(_step.Text),
        ["m0"] = SelectedKey(_m0),
        ["m1"] = SelectedKey(_m1),
        ["m2"] = SelectedKey(_m2),
        ["n0"] = Clean(_n0.Text),
        ["k0"] = Clean(_k0.Text),
        ["n1"] = Clean(_n1.Text),
        ["k1"] = Clean(_k1.Text),
        ["n2"] = Clean(_n2.Text),
        ["k2"] = Clean(_k2.Text),
    };

    private static string Clean(string s) => s.Trim().Replace(",", ".");

    private void CalculateAndRender()
    {
        try
        {
            _lastResult = CalculateCore(Params(), _useDb.Checked);
            Render(_lastResult);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка расчёта", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private CalcResult CalculateCore(Dictionary<string, string> p, bool useDb)
    {
        double Parse(string key)
        {
            if (!double.TryParse(p[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException($"Заполните поле: {key}");
            return value;
        }

        double lam = Parse("lam");
        double theta0 = Math.PI / 180.0 * Parse("theta");
        double psiExp = Parse("psi");
        double deltaExp = Parse("delta");
        int dmin = (int)Parse("dmin");
        int dmax = (int)Parse("dmax");
        int step = Math.Max(1, Math.Abs((int)Parse("step")));

        if (dmin > dmax) (dmin, dmax) = (dmax, dmin);

        Complex N0, N1, N2;
        var notes = new Dictionary<string, string>();

        if (useDb)
        {
            Material GetM(string key) => _materials[p[key]];
            var m0 = GetM("m0");
            var m1 = GetM("m1");
            var m2 = GetM("m2");

            N0 = new Complex(m0.N, m0.K);
            N1 = new Complex(m1.N, m1.K);
            N2 = new Complex(m2.N, m2.K);

            notes["N0"] = $"{m0.Label} / KLA 632.8 nm";
            notes["N1"] = $"{m1.Label} / KLA 632.8 nm";
            notes["N2"] = $"{m2.Label} / KLA 632.8 nm";
        }
        else
        {
            N0 = new Complex(Parse("n0"), Parse("k0"));
            N1 = new Complex(Parse("n1"), Parse("k1"));
            N2 = new Complex(Parse("n2"), Parse("k2"));
            notes["N0"] = "ручной ввод";
            notes["N1"] = "ручной ввод";
            notes["N2"] = "ручной ввод";
        }

        double sin0 = Math.Sin(theta0);
        Complex cos0 = new(Math.Cos(theta0), 0);

        Complex sin1 = (N0 / N1) * sin0;
        Complex cos1 = Complex.Sqrt(1 - sin1 * sin1);
        Complex sin2 = (N0 / N2) * sin0;
        Complex cos2 = Complex.Sqrt(1 - sin2 * sin2);

        Complex r01s = FresnelS(N0, N1, cos0, cos1);
        Complex r01p = FresnelP(N0, N1, cos0, cos1);
        Complex r12s = FresnelS(N1, N2, cos1, cos2);
        Complex r12p = FresnelP(N1, N2, cos1, cos2);

        var rows = new List<CalcRow>();

        for (int d = dmin; d <= dmax; d += step)
        {
            Complex beta = (2 * Math.PI / lam) * N1 * d * cos1;
            Complex q = Complex.Exp(2 * Complex.ImaginaryOne * beta);

            Complex rs = (r01s + r12s * q) / (1 + r01s * r12s * q);
            Complex rp = (r01p + r12p * q) / (1 + r01p * r12p * q);
            Complex rho = rp / rs;

            double psi = 180.0 / Math.PI * Math.Atan(rho.Magnitude);
            double delta = PhaseDeg(rho);
            double dDelta = CyclicDelta(delta, deltaExp);
            double f = Math.Pow(psi - psiExp, 2) + Math.Pow(dDelta, 2);

            rows.Add(new CalcRow { D = d, Beta = beta, Q = q, Rho = rho, Psi = psi, Delta = delta, DeltaDelta = dDelta, F = f });
        }

        if (rows.Count == 0) throw new InvalidOperationException("Пустой диапазон d.");

        return new CalcResult
        {
            Rows = rows,
            Best = rows.MinBy(r => r.F)!,
            Constants = new Dictionary<string, Complex>
            {
                ["N0"] = N0, ["N1"] = N1, ["N2"] = N2,
                ["sin1"] = sin1, ["cos1"] = cos1,
                ["sin2"] = sin2, ["cos2"] = cos2,
                ["r01s"] = r01s, ["r01p"] = r01p,
                ["r12s"] = r12s, ["r12p"] = r12p
            },
            Notes = notes
        };
    }

    private static Complex FresnelS(Complex na, Complex nb, Complex cosA, Complex cosB)
        => (na * cosA - nb * cosB) / (na * cosA + nb * cosB);

    private static Complex FresnelP(Complex na, Complex nb, Complex cosA, Complex cosB)
        => (nb * cosA - na * cosB) / (nb * cosA + na * cosB);

    private static double PhaseDeg(Complex z)
    {
        var deg = 180.0 / Math.PI * Math.Atan2(z.Imaginary, z.Real);
        return deg < 0 ? deg + 360 : deg;
    }

    private static double CyclicDelta(double model, double exp)
    {
        var x = Math.PI / 180.0 * (model - exp);
        return 180.0 / Math.PI * Math.Atan2(Math.Sin(x), Math.Cos(x));
    }

    private void Render(CalcResult result)
    {
        var b = result.Best;

        _resultBox.Text =
            $"Минимум ошибки: d = {Fmt(b.D, 0)} нм{Environment.NewLine}" +
            $"Ψ model = {Fmt(b.Psi)} град{Environment.NewLine}" +
            $"Δ model = {Fmt(b.Delta)} град{Environment.NewLine}" +
            $"δΔ = {Fmt(b.DeltaDelta)} град{Environment.NewLine}" +
            $"F min = {Fmt(b.F, 9)}{Environment.NewLine}" +
            $"ρ = {FmtC(b.Rho, 5)}{Environment.NewLine}" +
            $"q = exp(2iβ) = {FmtC(b.Q, 5)}";

        _grid.Rows.Clear();
        foreach (var r in result.Rows)
        {
            _grid.Rows.Add(r.D, FmtC(r.Beta, 5), FmtC(r.Q, 5), FmtC(r.Rho, 5), Fmt(r.Psi), Fmt(r.Delta), Fmt(r.DeltaDelta), Fmt(r.F, 8));
        }

        var c = result.Constants;
        _constantsBox.Text =
            $"N₀ = {FmtC(c["N0"])} ({result.Notes["N0"]}){Environment.NewLine}" +
            $"N₁ = {FmtC(c["N1"])} ({result.Notes["N1"]}){Environment.NewLine}" +
            $"N₂ = {FmtC(c["N2"])} ({result.Notes["N2"]}){Environment.NewLine}{Environment.NewLine}" +
            $"sin θ₁ = {FmtC(c["sin1"])}{Environment.NewLine}" +
            $"cos θ₁ = {FmtC(c["cos1"])}{Environment.NewLine}" +
            $"sin θ₂ = {FmtC(c["sin2"])}{Environment.NewLine}" +
            $"cos θ₂ = {FmtC(c["cos2"])}{Environment.NewLine}{Environment.NewLine}" +
            $"r₀₁,ₛ = {FmtC(c["r01s"])}{Environment.NewLine}" +
            $"r₀₁,ₚ = {FmtC(c["r01p"])}{Environment.NewLine}" +
            $"r₁₂,ₛ = {FmtC(c["r12s"])}{Environment.NewLine}" +
            $"r₁₂,ₚ = {FmtC(c["r12p"])}";
    }

    private static string Fmt(double x, int digits = 6)
    {
        if (Math.Abs(x) < 1e-12) return "0";
        var s = x.ToString("F" + digits, CultureInfo.InvariantCulture);
        return digits == 0 ? s : s.TrimEnd('0').TrimEnd('.');
    }

    private static string FmtC(Complex z, int digits = 6)
    {
        var sign = z.Imaginary >= 0 ? "+" : "-";
        return $"{Fmt(z.Real, digits)} {sign} {Fmt(Math.Abs(z.Imaginary), digits)}i";
    }

    private static DirectoryInfo OutputsDir()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "outputs");
        Directory.CreateDirectory(path);
        return new DirectoryInfo(path);
    }

    private void SaveCsv()
    {
        if (_lastResult is null || _lastResult.Rows.Count == 0)
        {
            MessageBox.Show("Сначала выполните расчёт.", "Нет данных", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var file = Path.Combine(OutputsDir().FullName, $"ellipsometry_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        using var writer = new StreamWriter(file, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.WriteLine("sep=;");
        writer.WriteLine("d_nm;beta_re;beta_im;q_re;q_im;rho_re;rho_im;Psi_deg;Delta_deg;deltaDelta_deg;F");

        foreach (var r in _lastResult.Rows)
        {
            writer.WriteLine(string.Join(";",
                Inv(r.D), Inv(r.Beta.Real), Inv(r.Beta.Imaginary),
                Inv(r.Q.Real), Inv(r.Q.Imaginary),
                Inv(r.Rho.Real), Inv(r.Rho.Imaginary),
                Inv(r.Psi), Inv(r.Delta), Inv(r.DeltaDelta), Inv(r.F)
            ));
        }

        MessageBox.Show($"CSV сохранён:{Environment.NewLine}{file}", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string Inv(double x) => x.ToString("G17", CultureInfo.InvariantCulture);
    private static string Inv(int x) => x.ToString(CultureInfo.InvariantCulture);
}
