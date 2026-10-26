using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

/// <summary>
/// DeepSeek lookdev harness: same camera, same assets, one variable per capture.
/// Writes PNGs + machine-readable image statistics into res://artifacts/npr-lookdev/.
/// It is a diagnostic tool only; it does not change the shipped default preset.
/// </summary>
public partial class RunPaperNprLookDev : SceneTree
{
    private OrigamiGallery _gallery=null!;
    private GameMapScene _stage=null!;
    private Camera3D _camera=null!;
    private Control _panel=null!;
    private string[] _args=[];
    private readonly Dictionary<string,(Transform3D Transform,float Size)> _cases=new();
    private string _outDir="res://artifacts/npr-lookdev";
    private readonly List<object> _report=[];

    private async Task Frames(int n){for(var i=0;i<n;i++)await ToSignal(this,SignalName.ProcessFrame);}
    private string Arg(string key,string fallback){var p=Array.IndexOf(_args,key);return p>=0 && p+1<_args.Length?_args[p+1]:fallback;}

    public override void _Initialize()=>Callable.From(()=>{_=Run();}).CallDeferred();

    private async Task Run()
    {
        _args=OS.GetCmdlineUserArgs();
        _outDir=Arg("--npr-out","res://artifacts/npr-lookdev");
        try
        {
            Root.ContentScaleSize=new Vector2I(1760,1000);
            using(var doc=JsonDocument.Parse(FileAccess.GetFileAsString("res://artifacts/paper-v5-before-diagnostic.json")))
                foreach(var c in doc.RootElement.GetProperty("cameras").EnumerateArray())
                {
                    Vector3 P(string k){var a=c.GetProperty(k).EnumerateArray().Select(v=>v.GetSingle()).ToArray();return new Vector3(a[0],a[1],a[2]);}
                    _cases[c.GetProperty("view").GetString()!]=(new Transform3D(new Basis(P("x"),P("y"),P("z")),P("origin")),c.GetProperty("size").GetSingle());
                }
            System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath(_outDir));
            _gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<OrigamiGallery>();
            Root.AddChild(_gallery);
            _stage=_gallery.GetChildren().OfType<GameMapScene>().Single();
            _camera=_stage.CameraController.Camera;
            _panel=_gallery.GetChildren().OfType<CanvasLayer>().Single(l=>l.Layer==30).GetChild<Control>(0);
            await Frames(90);
            _panel.Visible=false;
            _gallery.FindChild("ArtSettingsButton",true,false)!.Set("visible",false);
            _stage.CameraController.SetProcess(false);
            _gallery.EnvironmentMotion!.Frozen=true;

            var only=Arg("--npr-only","").Split(',',StringSplitOptions.RemoveEmptyEntries);
            var gradeToggle=_gallery.FindChildren("*","CheckButton",true,false).OfType<CheckButton>().Single(b=>b.Text=="纸艺调色（保护白纸）");
            foreach(var v in Variants())
            {
                if(only.Length>0 && !only.Contains(v.Name))continue;
                PaperNprStyle.Use(v.Settings ?? PaperNprStyle.Current);
                ArtRenderProfile.Preset(_stage,v.Preset,true,true);
                gradeToggle.ButtonPressed=v.Grade;
                _gallery.RebuildForInspection();
                ConceptPaperStyle.RefreshNprParameters();
                _stage.CameraController.SetProcess(false);
                _gallery.EnvironmentMotion!.Frozen=true;
                _gallery.EnvironmentMotion!.SetClock(2.25);
                await Frames(30);
                foreach(var view in new[]{"overview","region","close"})
                {
                    if(!_cases.TryGetValue(view,out var c))continue;
                    _camera.GlobalTransform=c.Transform;_camera.Size=c.Size;
                    await Frames(24);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var im=Root.GetTexture().GetImage();
                    var name=$"{v.Name}-{view}";
                    var path=$"{_outDir}/{name}.png";
                    im.SavePng(ProjectSettings.GlobalizePath(path));
                    var st=Stats(name,v,im,path);
                    _report.Add(st);
                    GD.Print("NPR_SHOT "+name);
                }
                if(v.Name is "v5" or "p7" or "p8" or "p7-nograde")
                {
                    var c=_cases["region"];
                    _panel.Visible=true;_gallery.FindChild("ArtSettingsButton",true,false)!.Set("visible",true);
                    _camera.GlobalTransform=c.Transform;_camera.Size=c.Size;
                    await Frames(16);
                    await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                    using var im=Root.GetTexture().GetImage();
                    var name=$"{v.Name}-region-ui";
                    var path=$"{_outDir}/{name}.png";
                    im.SavePng(ProjectSettings.GlobalizePath(path));
                    _report.Add(Stats(name,v,im,path));
                    GD.Print("NPR_SHOT "+name);
                    _panel.Visible=false;_gallery.FindChild("ArtSettingsButton",true,false)!.Set("visible",false);
                }
            }
        }
        catch(Exception e){GD.PushError("NPR_LOOKDEV_FAILED "+e);_report.Add(new{error=e.ToString()});}

        await Calibration();
        if(_args.Contains("--npr-angles"))await Angles();
        if(_args.Contains("--npr-bench"))await Benchmark();

        using(var file=FileAccess.Open($"{_outDir}/report.json",FileAccess.ModeFlags.Write))
            file.StoreString(JsonSerializer.Serialize(new{renderer=RenderingServer.GetCurrentRenderingMethod(),device=RenderingServer.GetVideoAdapterName(),shots=_report},new JsonSerializerOptions{WriteIndented=true}));
        GD.Print("NPR_LOOKDEV_DONE");
        _gallery.FinishInspection(0);
    }

    /// <summary>Grey-box lit/shade measurement on a fixed rig, same camera and sun for every preset.</summary>
    private async Task Calibration()
    {
        var results=new List<object>();
        var shade28=new PaperNprStyle.Settings{Shade=0.28f,ShadeTintMix=0.70f};
        var shade35=new PaperNprStyle.Settings{Shade=0.35f,ShadeTintMix=0.70f};
        var amb40=new PaperNprStyle.Settings{AmbientEnergy=0.40f};
        foreach(var (name,preset,settings) in new (string,int,PaperNprStyle.Settings?)[]{
            ("v5",5,null),("p7",7,new PaperNprStyle.Settings()),("p8",8,PaperNprStyle.Cinema()),
            ("p7-shade28",7,shade28),("p7-shade35",7,shade35),("p7-ambient40",7,amb40)})
        {
            PaperNprStyle.Use(settings ?? new PaperNprStyle.Settings());
            ArtRenderProfile.Preset(_stage,preset,true,true);
            var rig=new Node3D{Name="NprCalibrationRig",Position=new Vector3(0,0,-30)};
            var shader=GD.Load<Shader>(preset==7?"res://assets/shaders/physical_paper_npr.gdshader":"res://assets/shaders/physical_paper.gdshader");
            var material=new ShaderMaterial{Shader=shader};
            material.SetShaderParameter("base_color",new Color(0.58f,0.58f,0.58f));
            material.SetShaderParameter("terrain_palette",false);
            material.SetShaderParameter("use_albedo_texture",false);
            if(preset==7)PaperNprStyle.ApplyMaterial(material,false);
            var box=new MeshInstance3D{Name="CalBox",Mesh=new BoxMesh{Size=new Vector3(1.4f,1.4f,1.4f)},Position=new Vector3(0,0.7f,0),MaterialOverride=material,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            var floor=new MeshInstance3D{Name="CalFloor",Mesh=new PlaneMesh{Size=new Vector2(8,8)},Position=new Vector3(0,0.001f,0),MaterialOverride=material,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            rig.AddChild(box);rig.AddChild(floor);_gallery.AddChild(rig);
            var centre=rig.Position+new Vector3(0,0.7f,0);
            var eye=rig.Position+new Vector3(2.7f,2.7f,5.4f);
            _camera.GlobalTransform=new Transform3D(Basis.Identity,eye);
            _camera.LookAt(centre);
            _camera.Size=3.6f;
            await Frames(20);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var im=Root.GetTexture().GetImage();
            im.SavePng(ProjectSettings.GlobalizePath($"{_outDir}/calibration-{name}.png"));
            var probes=new Dictionary<string,Vector3>{
                {"top",centre+new Vector3(0,0.71f,0)},
                {"sun_face_pz",centre+new Vector3(0,0,0.71f)},
                {"shade_face_px",centre+new Vector3(0.71f,0,0)}};
            var values=new Dictionary<string,double>();
            foreach(var p in probes)
            {
                var sp=_camera.UnprojectPosition(p.Value);
                var x0=Mathf.Clamp((int)sp.X-8,0,im.GetWidth()-17);
                var y0=Mathf.Clamp((int)sp.Y-8,0,im.GetHeight()-17);
                double sum=0;var n=0;
                for(var y=y0;y<y0+17;y++)
                    for(var x=x0;x<x0+17;x++){var c=im.GetPixel(x,y);sum+=.2126*c.R+.7152*c.G+.0722*c.B;n++;}
                values[p.Key]=Math.Round(sum/n,4);
            }
            var lit=Math.Max(values["sun_face_pz"],values["top"]);
            results.Add(new{name,preset,samples=values,lit,shade=values["shade_face_px"],
                ratio=Math.Round(lit/Math.Max(values["shade_face_px"],0.001),3),
                note="grey 0.58 paper box on the same rig/camera; cast shadows off; display-referred luminance"});
            GD.Print($"NPR_CAL {name} top={values["top"]} pz={values["sun_face_pz"]} px={values["shade_face_px"]} ratio={Math.Round(lit/Math.Max(values["shade_face_px"],0.001),2)}");
            rig.QueueFree();
            await Frames(4);
        }
        _report.Add(new{calibration=results});
    }

    /// <summary>Orbit the overview camera to confirm the new shading has no back-side artefacts.</summary>
    private async Task Angles()
    {
        PaperNprStyle.Use(new PaperNprStyle.Settings());
        ArtRenderProfile.Preset(_stage,7,true,true);
        _gallery.RebuildForInspection();
        ConceptPaperStyle.RefreshNprParameters();
        _stage.CameraController.SetProcess(false);
        _gallery.EnvironmentMotion!.Frozen=true;
        await Frames(40);
        if(!_cases.TryGetValue("overview",out var overview))return;
        _camera.GlobalTransform=overview.Transform;_camera.Size=overview.Size;
        var cells=_stage.DetailController!.Cells;
        var target=cells.Aggregate(Vector3.Zero,(v,c)=>v+c.GlobalPosition)/cells.Count;
        var origin=_camera.GlobalPosition;
        var angles=new List<object>();
        foreach(var deg in new[]{0,90,180,270})
        {
            var rotate=new Basis(Vector3.Up,Mathf.DegToRad(deg));
            _camera.GlobalPosition=target+rotate*(origin-target);
            _camera.LookAt(target);
            await Frames(20);
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var im=Root.GetTexture().GetImage();
            var name=$"p7-angle-{deg}";
            im.SavePng(ProjectSettings.GlobalizePath($"{_outDir}/{name}.png"));
            angles.Add(Stats(name,(name,7,new PaperNprStyle.Settings(),true),im,name));
        }
        _report.Add(new{angles});
    }

    /// <summary>Short, contained GPU/CPU comparison of preset 5 vs preset 7 at the three fixed cameras.</summary>
    private async Task Benchmark()
    {
        var results=new List<object>();
        RenderingServer.ViewportSetMeasureRenderTime(Root.GetViewportRid(),true);
        foreach(var (name,preset,settings) in new (string,int,PaperNprStyle.Settings?)[]{
            ("v5",5,null),("p7",7,new PaperNprStyle.Settings()),("p8",8,PaperNprStyle.Cinema())})
        {
            PaperNprStyle.Use(settings ?? new PaperNprStyle.Settings());
            ArtRenderProfile.Preset(_stage,preset,true,true);
            _gallery.RebuildForInspection();
            ConceptPaperStyle.RefreshNprParameters();
            _stage.CameraController.SetProcess(false);
            _gallery.EnvironmentMotion!.Frozen=true;
            await Frames(40);
            foreach(var view in new[]{"overview","region","close"})
            {
                if(!_cases.TryGetValue(view,out var c))continue;
                _camera.GlobalTransform=c.Transform;_camera.Size=c.Size;
                await Frames(120);
                var cpu=new List<double>();var gpu=new List<double>();var setup=new List<double>();
                double draws=0,mem=0;
                var start=Time.GetTicksUsec();
                while(Time.GetTicksUsec()-start<12_000_000)
                {
                    await Frames(1);
                    cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(Root.GetViewportRid()));
                    gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(Root.GetViewportRid()));
                    setup.Add(RenderingServer.GetFrameSetupTimeCpu());
                    draws=Math.Max(draws,Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
                    mem=Math.Max(mem,Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed));
                }
                results.Add(new{name,preset,view,frames=cpu.Count,
                    cpuP95=Math.Round(P(cpu,.95),3),cpuP99=Math.Round(P(cpu,.99),3),
                    gpuP95=Math.Round(P(gpu,.95),3),gpuP99=Math.Round(P(gpu,.99),3),
                    setupP95=Math.Round(P(setup,.95),3),maxDraws=draws,videoMemMiB=Math.Round(mem/1048576.0,1)});
                GD.Print($"NPR_BENCH {name}/{view} cpu95={Math.Round(P(cpu,.95),2)} gpu95={Math.Round(P(gpu,.95),2)} draws={draws}");
            }
        }
        _report.Add(new{benchmark=results,note="12 s per condition after 120 warmup frames, 1760x1000, animation on, no capture inside the window"});
        static double P(List<double> values,double q){var a=values.OrderBy(x=>x).ToArray();return a[Math.Clamp((int)(q*(a.Length-1)),0,a.Length-1)];}
    }

    private object Stats(string name,(string Name,int Preset,PaperNprStyle.Settings? Settings,bool Grade) v,Image im,string path)    {
        using var rgb=(Image)im.Duplicate();
        rgb.Convert(Image.Format.Rgb8);
        var bytes=rgb.GetData();
        var luma=new List<float>(bytes.Length/3);
        double chroma=0;
        for(var i=0;i+2<bytes.Length;i+=3)
        {
            var r=bytes[i]/255f;var g=bytes[i+1]/255f;var b=bytes[i+2]/255f;
            luma.Add(.2126f*r+.7152f*g+.0722f*b);
            chroma+=Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b));
        }
        luma.Sort();
        float P(double q)=>luma[Math.Clamp((int)(q*(luma.Count-1)),0,luma.Count-1)];
        double mean=luma.Average();
        return new{name,preset=v.Preset,grade=v.Grade,settings=v.Settings==null?"V5 control":PaperNprStyle.Describe(v.Settings),
            path,size=new[]{im.GetWidth(),im.GetHeight()},mean=Math.Round(mean,4),p05=Math.Round(P(.05),4),p25=Math.Round(P(.25),4),
            p50=Math.Round(P(.50),4),p75=Math.Round(P(.75),4),p95=Math.Round(P(.95),4),
            contrast=Math.Round(P(.95)-P(.05),4),chroma=Math.Round(chroma/(luma.Count),4),
            shadowFraction=Math.Round(luma.Count(x=>x<.22)/ (double)luma.Count,4),
            highlightFraction=Math.Round(luma.Count(x=>x>.86)/(double)luma.Count,4)};
    }

    /// <summary>Each entry is one isolated change against the V5 control.</summary>
    private static IEnumerable<(string Name,int Preset,PaperNprStyle.Settings? Settings,bool Grade)> Variants()
    {
        PaperNprStyle.Settings D()=>new();
        yield return ("v5",5,null,true);                                   // current shipped look
        yield return ("v5-nograde",5,null,false);                          // final filter off
        var p7=D();  yield return ("p7",7,p7,true);                        // full NPR
        yield return ("p7-nograde",7,p7.Clone(),false);                    // material+light only
        yield return ("p8",8,PaperNprStyle.Cinema(),true);                 // cinematic intensity
        yield return ("p8-nograde",8,PaperNprStyle.Cinema(),false);
        var p8noFill=PaperNprStyle.Cinema(); p8noFill.BounceEnergy=0f; yield return ("p8-nofill",8,p8noFill,true);
        var p8fill=PaperNprStyle.Cinema(); p8fill.BounceEnergy=0.70f; yield return ("p8-fill70",8,p8fill,true);
        var p8hard=PaperNprStyle.Cinema(); p8hard.SunPitch=-26f; p8hard.GradeContrast=0.38f; yield return ("p8-hard",8,p8hard,true);
        var p8soft=PaperNprStyle.Cinema(); p8soft.SunPitch=-34f; p8soft.GradeSaturation=1.26f; yield return ("p8-soft",8,p8soft,true);
        var p8b=PaperNprStyle.Cinema(); p8b.AmbientEnergy=0.27f; p8b.BounceEnergy=0.50f; p8b.Exposure=1.10f; yield return ("p8b",8,p8b,true);
        var p8c=PaperNprStyle.Cinema(); p8c.AmbientEnergy=0.31f; p8c.BounceEnergy=0.55f; p8c.Exposure=1.12f; p8c.GradeSaturation=1.30f; yield return ("p8c",8,p8c,true);
        var p8d=PaperNprStyle.Cinema(); p8d.AmbientEnergy=0.27f; p8d.BounceEnergy=0.50f; p8d.Exposure=1.10f; p8d.SunPitch=-33f; yield return ("p8d",8,p8d,true);
        var p8e=PaperNprStyle.Cinema(); p8e.SunColor=new Color("ffd79a"); p8e.SunEnergy=1.75f; p8e.AmbientColor=new Color("8aa0ff"); p8e.AmbientEnergy=0.30f;
        p8e.BounceEnergy=0.55f; p8e.BounceColor=new Color("9db4ff"); p8e.GradeSaturation=1.40f; p8e.GradeContrast=0.28f; p8e.Exposure=1.08f; p8e.SunPitch=-33f;
        yield return ("p8e",8,p8e,true);
        var p8f=PaperNprStyle.Cinema(); p8f.Tonemap=Godot.Environment.ToneMapper.Filmic; p8f.SunColor=new Color("ffd79a"); p8f.SunEnergy=1.60f;
        p8f.AmbientColor=new Color("8aa0ff"); p8f.AmbientEnergy=0.30f; p8f.BounceEnergy=0.55f; p8f.BounceColor=new Color("9db4ff");
        p8f.GradeSaturation=1.34f; p8f.GradeContrast=0.24f; p8f.Exposure=1.18f; p8f.SunPitch=-33f; yield return ("p8f",8,p8f,true);
        var p8g=PaperNprStyle.Cinema(); p8g.SunColor=new Color("ffd79a"); p8g.SunEnergy=1.75f; p8g.AmbientColor=new Color("8aa0ff"); p8g.AmbientEnergy=0.30f;
        p8g.BounceEnergy=0.55f; p8g.BounceColor=new Color("9db4ff"); p8g.GradeSaturation=1.38f; p8g.GradeContrast=0.28f; p8g.Exposure=1.16f; p8g.SunPitch=-33f;
        yield return ("p8g",8,p8g,true);
        var noGlow=PaperNprStyle.Cinema(); noGlow.Glow=false; noGlow.GradeVignette=0f; yield return ("p8-noglow",8,noGlow,true);
        var strongGlow=PaperNprStyle.Cinema(); strongGlow.GlowIntensity=0.60f; strongGlow.GradeVignette=0.26f; yield return ("p8-glow2",8,strongGlow,true);
        var fold25=PaperNprStyle.Cinema(); fold25.Fold=0.25f; fold25.FoldScale=1.15f; yield return ("p8-fold25",8,fold25,true);
        var fold35=PaperNprStyle.Cinema(); fold35.Fold=0.35f; fold35.FoldScale=1.0f; yield return ("p8-fold35",8,fold35,true);
        var s118=D(); s118.GradeSaturation=1.18f; yield return ("p7-sat118",7,s118,true);
        var plain=D(); plain.PlainMaterial=true; yield return ("p7-plain-mat",7,plain,true);
        var ambLo=D(); ambLo.AmbientEnergy=0.18f; yield return ("p7-ambient-lo",7,ambLo,true);
        var ambHi=D(); ambHi.AmbientEnergy=0.42f; yield return ("p7-ambient-hi",7,ambHi,true);
        var ssao=D(); ssao.SsaoRadius=0.60f; ssao.SsaoIntensity=1.40f; ssao.SsaoLightAffect=0.22f; yield return ("p7-ssao-strong",7,ssao,true);
        var crisp=D(); crisp.ShadowBlur=0.85f; crisp.ShadowMaxDistance=26f; yield return ("p7-shadow-crisp",7,crisp,true);
        var four=D(); four.ShadowMode=DirectionalLight3D.ShadowMode.Parallel4Splits; yield return ("p7-4splits",7,four,true);
        var fold80=D(); fold80.Fold=0.80f; yield return ("p7-fold80",7,fold80,true);
        var noshadow=D(); noshadow.ShadowOpacity=0.0f; noshadow.Fold=0.0f; yield return ("p7-no-shadow-nofold",7,noshadow,false);
    }
}
