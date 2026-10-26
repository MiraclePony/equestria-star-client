using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

/// <summary>
/// Claude lookdev harness for the P9 sunlit papercraft preset.
/// Same three fixed cameras as the V5/P7/P8 evidence (artifacts/paper-v5-before-diagnostic.json),
/// same assets, frozen environment motion. Variants come from
/// res://artifacts/claude-sunlit/variants.json so looks can be tuned without recompiling:
///   [{"name":"p9","preset":9,"grade":true,"views":["region"],"set":{"SunEnergy":1.4,"SunColor":"ffe2b0"}}]
/// Writes PNGs + report.json (luma / chroma / warmth statistics) next to the variants file.
/// Diagnostic only: it never changes the shipped default preset.
/// </summary>
public partial class RunClaudeSunlitLookDev : SceneTree
{
    private OrigamiGallery _gallery = null!;
    private GameMapScene _stage = null!;
    private Camera3D _camera = null!;
    private Control _panel = null!;
    private readonly Dictionary<string, (Transform3D Transform, float Size)> _cases = new();
    private const string OutDir = "res://artifacts/claude-sunlit";
    private readonly List<object> _report = [];

    private async Task Frames(int n) { for (var i = 0; i < n; i++) await ToSignal(this, SignalName.ProcessFrame); }
    public override void _Initialize() => Callable.From(() => { _ = Run(); }).CallDeferred();

    private sealed class Variant
    {
        public string Name { get; set; } = "p9";
        public int Preset { get; set; } = 9;
        public bool Grade { get; set; } = true;
        public string[]? Views { get; set; }
        public bool Ui { get; set; }
        public Dictionary<string, JsonElement>? Set { get; set; }
    }

    private async Task Run()
    {
        try
        {
            Root.ContentScaleSize = new Vector2I(1760, 1000);
            using (var doc = JsonDocument.Parse(FileAccess.GetFileAsString("res://artifacts/paper-v5-before-diagnostic.json")))
                foreach (var c in doc.RootElement.GetProperty("cameras").EnumerateArray())
                {
                    Vector3 P(string k) { var a = c.GetProperty(k).EnumerateArray().Select(v => v.GetSingle()).ToArray(); return new Vector3(a[0], a[1], a[2]); }
                    _cases[c.GetProperty("view").GetString()!] = (new Transform3D(new Basis(P("x"), P("y"), P("z")), P("origin")), c.GetProperty("size").GetSingle());
                }
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(OutDir));
            var variantsPath = OutDir + "/variants.json";
            var variants = FileAccess.FileExists(variantsPath)
                ? JsonSerializer.Deserialize<List<Variant>>(FileAccess.GetFileAsString(variantsPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
                : new List<Variant> { new Variant { Name = "v5", Preset = 5 }, new Variant { Name = "p9", Preset = 9 } };

            _gallery = GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<OrigamiGallery>();
            Root.AddChild(_gallery);
            _stage = _gallery.GetChildren().OfType<GameMapScene>().Single();
            _camera = _stage.CameraController.Camera;
            _panel = _gallery.GetChildren().OfType<CanvasLayer>().Single(l => l.Layer == 30).GetChild<Control>(0);
            await Frames(90);
            var gradeToggle = _gallery.FindChildren("*", "CheckButton", true, false).OfType<CheckButton>().Single(b => b.Text == "纸艺调色（保护白纸）");

            foreach (var v in variants)
            {
                var sunlit = new PaperSunlitStyle.Settings();
                if (v.Set != null) Override(sunlit, v.Set);
                PaperSunlitStyle.Use(sunlit);
                ArtRenderProfile.Preset(_stage, v.Preset, true, true);
                gradeToggle.ButtonPressed = v.Grade;
                _gallery.RebuildForInspection();
                ConceptPaperStyle.RefreshNprParameters();
                _stage.PaperEffect.Palette(v.Preset);
                _stage.PaperEffect.Visible = v.Grade;
                _stage.CameraController.SetProcess(false);
                _gallery.EnvironmentMotion!.Frozen = true;
                _gallery.EnvironmentMotion!.SetClock(2.25);
                await Frames(30);
                foreach (var view in v.Views ?? new[] { "overview", "region", "close" })
                {
                    if (!_cases.TryGetValue(view, out var c)) continue;
                    _panel.Visible = v.Ui;
                    _gallery.FindChild("ArtSettingsButton", true, false)!.Set("visible", v.Ui);
                    _camera.GlobalTransform = c.Transform; _camera.Size = c.Size;
                    PaperSunlitStyle.UpdateHaze(_stage);
                    await Frames(24);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var im = Root.GetTexture().GetImage();
                    var name = $"{v.Name}-{view}";
                    var path = $"{OutDir}/{name}.png";
                    im.SavePng(ProjectSettings.GlobalizePath(path));
                    _report.Add(Stats(name, v, im, path));
                    GD.Print("SUN_SHOT " + name);
                }
            }
        }
        catch (Exception e) { GD.PushError("SUN_LOOKDEV_FAILED " + e); _report.Add(new { error = e.ToString() }); }

        using (var file = FileAccess.Open($"{OutDir}/report.json", FileAccess.ModeFlags.Write))
            file.StoreString(JsonSerializer.Serialize(new { renderer = RenderingServer.GetCurrentRenderingMethod(), device = RenderingServer.GetVideoAdapterName(), time = DateTime.Now.ToString("O"), shots = _report }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("SUN_LOOKDEV_DONE");
        _gallery.FinishInspection(0);
    }

    /// <summary>Apply {"Field": value} overrides onto Settings by reflection (float, bool, Color hex, Vector2/3 arrays, enum name).</summary>
    private static void Override(PaperSunlitStyle.Settings s, Dictionary<string, JsonElement> set)
    {
        foreach (var (key, value) in set)
        {
            var f = typeof(PaperSunlitStyle.Settings).GetField(key, BindingFlags.Public | BindingFlags.Instance);
            if (f == null) { GD.PushWarning("SUN_UNKNOWN_FIELD " + key); continue; }
            object? v = null;
            if (f.FieldType == typeof(float)) v = value.GetSingle();
            else if (f.FieldType == typeof(bool)) v = value.GetBoolean();
            else if (f.FieldType == typeof(Color)) v = new Color(value.GetString()!);
            else if (f.FieldType == typeof(Vector3)) { var a = value.EnumerateArray().Select(x => x.GetSingle()).ToArray(); v = new Vector3(a[0], a[1], a[2]); }
            else if (f.FieldType == typeof(Vector2)) { var a = value.EnumerateArray().Select(x => x.GetSingle()).ToArray(); v = new Vector2(a[0], a[1]); }
            else if (f.FieldType.IsEnum) v = Enum.Parse(f.FieldType, value.GetString()!, true);
            if (v != null) f.SetValue(s, v);
        }
    }

    private static object Stats(string name, Variant v, Image im, string path)
    {
        using var rgb = (Image)im.Duplicate();
        rgb.Convert(Image.Format.Rgb8);
        var bytes = rgb.GetData();
        var luma = new List<float>(bytes.Length / 3);
        double chroma = 0, rs = 0, gs = 0, bs = 0;
        for (var i = 0; i + 2 < bytes.Length; i += 3)
        {
            var r = bytes[i] / 255f; var g = bytes[i + 1] / 255f; var b = bytes[i + 2] / 255f;
            luma.Add(.2126f * r + .7152f * g + .0722f * b);
            chroma += Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            rs += r; gs += g; bs += b;
        }
        var n = luma.Count;
        luma.Sort();
        float P(double q) => luma[Math.Clamp((int)(q * (n - 1)), 0, n - 1)];
        return new
        {
            name, preset = v.Preset, grade = v.Grade, path,
            settings = v.Preset == 9 ? PaperSunlitStyle.Describe(PaperSunlitStyle.Current) : "control",
            mean = Math.Round(luma.Average(), 4), p05 = Math.Round(P(.05), 4), p50 = Math.Round(P(.5), 4), p95 = Math.Round(P(.95), 4),
            chroma = Math.Round(chroma / n, 4), warmth = Math.Round((rs - bs) / n, 4), green = Math.Round(gs / n, 4)
        };
    }
}
