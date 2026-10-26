using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

/// <summary>Original gallery integration. Still capture, clean timing and fixed-step film are separate runs.</summary>
public partial class RunPaperV4Acceptance : SceneTree
{
    private OrigamiGallery _gallery=null!;private GameMapScene _stage=null!;private PaperEnvironmentMotion _motion=null!;
    private ZoomArtDetailController _detail=null!;private Dictionary<string,HexCell3D> _cells=[];
    private Control _inspector=null!;private CanvasLayer _ui=null!;
    private readonly List<string> _fail=[];private readonly List<object> _evidence=[];
    private string _method="";private string[] _args=[];
    public override void _Initialize()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private string Arg(string key,string fallback){var p=Array.IndexOf(_args,key);return p>=0 && p+1<_args.Length?_args[p+1]:fallback;}
    private async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(this,SignalName.ProcessFrame);}
    private void Check(bool pass,string message){if(!pass)_fail.Add(message);}
    private static double P(List<double> a,double p){if(a.Count==0)return 0;var s=a.OrderBy(v=>v).ToArray();return s[Math.Min(s.Length-1,(int)(s.Length*p))];}
    private void Focus(string cell,float ratio)
    {
        var c=_cells[cell];_detail.ViewInsets=Vector4.Zero;
        var camera=_stage.CameraController.Camera;
        var current=ScreenDetailPolicy.Ratio(camera,c.GlobalPosition,c.Radius,_detail.EffectiveViewport);
        _stage.CameraController.FocusGround(c.GlobalPosition,camera.Size*current/ratio,true);
    }
    private void Rebind()
    {_motion=_gallery.EnvironmentMotion!;_detail=_stage.DetailController!;_cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);}
    private void Look(int preset,bool frozen=true)
    {
        ArtRenderProfile.Preset(_stage,preset);_gallery.RebuildForInspection();Rebind();
        _motion.Frozen=frozen;_motion.SetClock(2.25);
    }
    private void Fixtures()
    {
        _ui=new CanvasLayer {Layer=0};Root.AddChild(_ui);
        var colors=new[]{Colors.White,new Color("808080"),new Color("3a2969"),new Color("75a75e")};
        for(var i=0;i<4;i++)_ui.AddChild(new ColorRect {Position=new Vector2(20+i*55,940),Size=new Vector2(45,45),Color=colors[i],MouseFilter=Control.MouseFilterEnum.Ignore});
        _ui.AddChild(new TextureRect {Position=new Vector2(1600,810),Size=new Vector2(120,170),Texture=GD.Load<Texture2D>("res://assets/characters/cards/hf.jpg"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=Control.MouseFilterEnum.Ignore});
    }
    private async Task<Image> Shot(string name,bool ui=false)
    {
        _inspector.Visible=ui;_ui.Visible=ui;await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=Root.GetTexture().GetImage();image.SavePng($"res://artifacts/paper-v4-{_method}-{name}.png");return image;
    }
    private static byte[] Region(Image source,Rect2I rect)
    {using var region=source.GetRegion(rect);region.Convert(Image.Format.Rgb8);return region.GetData();}
    private static double Difference(Image a,Image b,Rect2I rect)
    {var x=Region(a,rect);var y=Region(b,rect);return x.Zip(y,(l,r)=>Math.Abs(l-r)).Average();}
    private async Task UiChecks()
    {
        _stage.PaperEffect.Visible=true;_motion.HaloEnabled=true;_motion.Visible=true;_motion.SetLook(3);using var on=await Shot("world-effects-ui-on",true);
        _stage.PaperEffect.Visible=false;_motion.HaloEnabled=false;_motion.Visible=false;_motion.SetLook(0);using var off=await Shot("world-effects-ui-off",true);
        var areas=Enumerable.Range(0,4).Select(i=>("swatch-"+i,new Rect2I(20+i*55,940,45,45))).Append(("actual-character-card",new Rect2I(1600,810,120,170)));
        foreach(var (name,rect) in areas)
        {var x=Region(on,rect);var y=Region(off,rect);var changed=x.Zip(y,(a,b)=>a!=b).Count(v=>v);Check(changed==0,"World effects changed HUD "+name);_evidence.Add(new {name,differentBytes=changed,totalBytes=x.Length});}
        Check(_stage.PaperEffect.Layer<0 && _stage.PaperEffect.GetNode<ColorRect>("PaperGrade").MouseFilter==Control.MouseFilterEnum.Ignore,"Grade layer or mouse protection");
        if(_method!="gl_compatibility")
        {_stage.PaperEffect.Visible=true;Root.UseHdr2D=true;await Frames(3);Check(!_stage.PaperEffect.IsActive && !_stage.PaperEffect.GetNode<ColorRect>("PaperGrade").Visible,"HDR2D failed bypass");Root.UseHdr2D=false;await Frames(3);Check(_stage.PaperEffect.IsActive,"Grade did not resume after HDR2D");}
        _stage.PaperEffect.Visible=true;_motion.HaloEnabled=true;_motion.Visible=true;_motion.SetLook(3);_ui.Visible=false;_inspector.Visible=false;
    }
    private async Task Acceptance()
    {
        var camera=_stage.CameraController.Camera;
        foreach(var preset in new[]{0,3,4})
        {
            Look(preset);var label=preset==0?"v3":preset==3?"D":"D-vivid";
            foreach(var (name,cell,ratio) in new[]{("overview","A1",.04f),("region","B3",.10f),("close","A16",.23f)})
            {
                Focus(cell,ratio);await Frames(100);using var clean=await Shot(label+"-"+name+"-clean");using var ui=await Shot(label+"-"+name+"-ui",true);
                _evidence.Add(new {look=label,view=name,cell,ratio,cameraSize=camera.Size,cameraTransform=camera.GlobalTransform.ToString(),motion=_motion.Snapshot()});
            }
        }
        Look(3);Focus("B3",.10f);await Frames(90);
        var roots=_gallery.FindChildren("*","Node3D",true,false).OfType<Node3D>().Where(n=>n.HasMeta("zoom_main_tree")).ToDictionary(n=>n,n=>n.GlobalTransform);
        var cells=_cells.Values.ToDictionary(c=>c,c=>(c.GlobalTransform,PawnArtLayout.WorldAnchor(c),c.InteractionShape));
        var buildings=_cells.Values.Select(c=>c.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset/Building")).Where(n=>n!=null).ToDictionary(n=>n!,n=>n!.GlobalTransform);
        var sun=_stage.GetNode<DirectionalLight3D>("DirectionalLight3D");var sunTransform=sun.GlobalTransform;var exposure=_stage.GetNode<WorldEnvironment>("Environment").Environment.TonemapExposure;
        using(var d=await Shot("lighting-D-only")){}
        ArtRenderProfile.Preset(_stage,0,false);await Frames(40);using(var old=await Shot("lighting-v3-only")){}
        ArtRenderProfile.Preset(_stage,3,false);await Frames(40);
        foreach(var energy in new[]{1.30f,1.40f,1.50f})
        {sun.LightEnergy=energy*(_method=="gl_compatibility"?.43f:1);await Frames(25);using var s=await Shot("sun-"+energy.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));}
        ArtRenderProfile.Preset(_stage,3,false);
        _stage.PaperEffect.Palette(0);using(var grade=await Shot("grade-v3-only")){}
        _stage.PaperEffect.Palette(3);using(var grade=await Shot("grade-D-only")){}
        _motion.HaloEnabled=false;_motion.Tick(0);using(var halo=await Shot("halo-off")){}
        _motion.HaloEnabled=true;_motion.Tick(0);using(var halo=await Shot("halo-on")){}
        _stage.PaperEffect.Visible=false;using(var filter=await Shot("diagnostic-no-grade")){}_stage.PaperEffect.Visible=true;
        sun.ShadowEnabled=false;using(var shade=await Shot("diagnostic-no-shadow")){}sun.ShadowEnabled=true;
        var materials=_gallery.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>m.Mesh!=null)
            .SelectMany(m=>Enumerable.Range(0,m.Mesh.GetSurfaceCount()).Select(m.GetActiveMaterial)).OfType<ShaderMaterial>().Where(m=>m.Shader?.ResourcePath=="res://assets/shaders/physical_paper.gdshader").DistinctBy(m=>m.GetInstanceId()).ToArray();
        var textureFlags=materials.Select(m=>(m,m.GetShaderParameter("use_albedo_texture"))).ToArray();
        foreach(var (m,_) in textureFlags)m.SetShaderParameter("use_albedo_texture",false);
        using(var texture=await Shot("diagnostic-no-albedo-texture")){}
        foreach(var (m,value) in textureFlags)m.SetShaderParameter("use_albedo_texture",value);
        await UiChecks();
        Focus("B3",.10f);await Frames(40);_motion.SetClock(2.25);using var frozen=await Shot("motion-frozen");
        await Frames(30);using var still=await Shot("motion-frozen-later");
        var frozenDelta=Difference(frozen,still,new Rect2I(0,0,1760,1000));Check(frozenDelta<.01,"Frozen CPU/shader scene changes");
        _motion.SetClock(4.25);using var moved=await Shot("motion-after-two-seconds");
        var moveDelta=Difference(frozen,moved,new Rect2I(150,100,1500,850));Check(moveDelta>.015,"No visible motion in region");
        _motion.Reduced=true;using(var reduced=await Shot("reduced-motion")){}_evidence.Add(new {name="reduced-mode",motion=_motion.Snapshot()});_motion.Reduced=false;
        _motion.Frozen=false;_detail.AnimationsEnabled=false;await Frames(5);var stoppedClock=_motion.Clock;await Frames(25);
        Check(Math.Abs(_motion.Clock-stoppedClock)<.000001,"Environment off did not stop the shared shader/CPU clock");
        _detail.AnimationsEnabled=true;_motion.Frozen=true;
        foreach(var ratio in new[]{.035f,.10f,.23f,.40f,.16f,.17f,.15f})
        {Focus("B3",ratio);_motion.SetClock(4.25+ratio*7);await Frames(8);Check(camera.GetViewport().MeshLodThreshold==0,"Mesh LOD was enabled");}
        Check(roots.All(x=>x.Key.GlobalTransform.IsEqualApprox(x.Value)),"Tree roots moved");
        Check(buildings.All(x=>x.Key.GlobalTransform.IsEqualApprox(x.Value)),"Whole building animated");
        Check(cells.All(x=>x.Key.GlobalTransform.IsEqualApprox(x.Value.GlobalTransform) && PawnArtLayout.WorldAnchor(x.Key).IsEqualApprox(x.Value.Item2) && ReferenceEquals(x.Key.InteractionShape,x.Value.InteractionShape)),"Gameplay anchor/picking changed");
        Check(sun.GlobalTransform.IsEqualApprox(sunTransform) && Math.Abs(_stage.GetNode<WorldEnvironment>("Environment").Environment.TonemapExposure-exposure)<.00001f,"Sun/exposure changed on zoom");
        Check(_motion.CrownHingeCount==74 && _motion.FlagHingeCount>0,"Dedicated crown or authored flag hinges missing");
        Check(_motion.SmokeSourceCount==2,"Verified factory chimney sources missing");
        Check(_motion.ParticleBudget<=40 && _motion.BatchCount<=18,"Effect budget exceeded");
        _motion.UpdateAllHingesForInspection=true;var windChecks=0;
        foreach(var time in new[]{0f,1.1f,2.2f,3.3f,4.4f,5.5f,6.6f,7.7f})
        {
            _motion.SetClock(time);
            foreach(var cell in _cells.Values.Where(c=>c.Data!.BoardCode=="B"))
            {Check(new MeshClearanceQuery(cell).IsFree(PawnArtLayout.WorldAnchor(cell),cell.Radius),"Wind envelope blocks "+cell.Data!.CellCode);windChecks++;}
        }
        _motion.UpdateAllHingesForInspection=false;_evidence.Add(new {name="wind-envelope",cases=windChecks});
        using var mapDocument=JsonDocument.Parse(FileAccess.GetFileAsString("res://tests/fixtures/main-map-20261024.json"));
        var fixture=JsonSerializer.Deserialize<EquestriaStar.Api.GameMapDto>(mapDocument.RootElement.GetProperty("data").GetRawText(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
        var contact=new TokenClearancePreview();_stage.AddChild(contact);contact.Build(_cells.ToDictionary(p=>p.Value.Data!.CellId,p=>p.Value),fixture.Edges!);
        Check(contact.FlaggedCells==0 && contact.SweepWarnings.Count==0,"V4 placement/pivot blocks pawn envelope");contact.SaveReport($"res://artifacts/paper-v4-{_method}-clearance.json");contact.Free();
        Focus("L7",.23f);_motion.SetClock(2.25);await Frames(45);using(var smoke=await Shot("factory-smoke-close")){}
        Focus("A2",.30f);await Frames(45);using(var flags=await Shot("twilight-flags-close")){}
        Focus("B3",.10f);_motion.SetClock(2.25);
        var siteJson=_stage.GetNode<Node3D>("MapRoot/GeneratedCells/PaperPlaceComposition").GetMeta("sites_json").AsString();
        var sites=JsonSerializer.Deserialize<PaperPlaceComposition.Site[]>(siteJson)!;
        Check(sites.All(s=>s.Asset!="rainbow-house"),"Floating cloud house received a ground approach");
        Check(sites.Where(s=>s.Asset is "dragon-lands" or "tartarus" or "baltimare" or "appleloosa" or "griffonstone" or "yakyakistan" or "zecora" or "old-castle" or "harmony-tree").All(s=>s.Flowers==0),"Wrong biome received generic bright flower gardens");
        _evidence.Add(new {name="dynamic-invariants",roots=roots.Count,cells=cells.Count,buildings=buildings.Count,frozenMeanByteDelta=frozenDelta,motionMeanByteDelta=moveDelta,motion=_motion.Snapshot(),siteJson});
        _motion.Frozen=false;Focus("B3",.035f);await Frames(90);
        Check(_motion.AreDistantDetailBatchesCulled && _motion.VisibleBatchCount<_motion.BatchCount,"Transparent distant detail batches still submit draw calls");
        _evidence.Add(new {name="distant-effect-culling",motion=_motion.Snapshot()});
    }
    private async Task Benchmark()
    {
        var look=int.Parse(Arg("--v4-look","3"));var reduced=_args.Contains("--paper-reduced");var duration=double.Parse(Arg("--v4-seconds","30"),System.Globalization.CultureInfo.InvariantCulture);
        Look(look,false);_motion.Reduced=reduced;_inspector.Visible=false;_ui.Visible=false;
        RenderingServer.ViewportSetMeasureRenderTime(Root.GetViewportRid(),true);
        foreach(var (name,cell,ratio) in new[]{("overview","A1",.04f),("region","B3",.10f),("close","A16",.23f)})
        {
            if(Arg("--v4-view","all")!="all" && Arg("--v4-view","all")!=name)continue;
            Focus(cell,ratio);await Frames(180);
            var cpu=new List<double>(20000);var gpu=new List<double>(20000);var wall=new List<double>(20000);var process=new List<double>(20000);var motion=new List<double>(20000);
            var start=Time.GetTicksUsec();var last=start;var memory=0.0;var draws=0.0;
            // No screenshot, file IO, assertion or logging in the timed section.
            while((Time.GetTicksUsec()-start)/1000000.0<duration)
            {
                await ToSignal(this,SignalName.ProcessFrame);var now=Time.GetTicksUsec();wall.Add((now-last)/1000.0);last=now;
                cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(Root.GetViewportRid()));gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(Root.GetViewportRid()));
                process.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000);motion.Add(_motion.LastTickMs);
                memory=Math.Max(memory,Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed));draws=Math.Max(draws,Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
            }
            _evidence.Add(new {view=name,look,reduced,durationSeconds=(Time.GetTicksUsec()-start)/1000000.0,samples=wall.Count,renderTargetWidth=Root.GetTexture().GetWidth(),renderTargetHeight=Root.GetTexture().GetHeight(),cpuP95=P(cpu,.95),cpuP99=P(cpu,.99),gpuP95=P(gpu,.95),gpuP99=P(gpu,.99),frameP95=P(wall,.95),frameP99=P(wall,.99),processP95=P(process,.95),processP99=P(process,.99),motionP95=P(motion,.95),motionP99=P(motion,.99),memoryPeakBytes=memory,drawsPeak=draws,cameraSize=_stage.CameraController.Camera.Size,animation="on; deterministic seeds; no captures/assertions/IO during timed interval"});
        }
    }
    private async Task Film()
    {
        Look(3);_motion.Frozen=false;_motion.AutomaticUpdates=false;_motion.SetClock(0);_detail.AutomaticUpdates=false;_inspector.Visible=false;_ui.Visible=false;
        var textLayer=new CanvasLayer {Layer=35};Root.AddChild(textLayer);var label=new Label {Position=new Vector2(24,22),Text="D纸艺童话 · 原工程固定步长录制 · 主树根/铁路/地块不动",Modulate=new Color("fff0d8")};textLayer.AddChild(label);
        for(var frame=0;frame<540;frame++)
        {
            var t=frame/30f;var u=Mathf.SmoothStep(0,1,Mathf.Clamp((t-9)/7,0,1));
            var centre=_cells["B3"].GlobalPosition.Lerp(_cells["A16"].GlobalPosition,u);
            Focus("B3",Mathf.Lerp(.10f,.23f,u));_stage.CameraController.FocusGround(centre,_stage.CameraController.Camera.Size,true);
            _motion.Tick(1/30.0);_detail.Tick(1/30.0,t);await Frames(1);
        }
        _evidence.Add(new {durationSeconds=18,frames=540,fps=30,source="actual original gallery with deterministic fixed step; not realtime FPS evidence",timeline="0–9s region motion;9–16s slow focus/zoom;16–18s close"});textLayer.Free();
    }
    private async Task Run()
    {
        _args=OS.GetCmdlineUserArgs();_method=RenderingServer.GetCurrentRenderingMethod();var mode=Arg("--v4-mode","acceptance");var tag=mode=="benchmark"?$"benchmark-{Arg("--v4-look","3")}-{(_args.Contains("--paper-reduced")?"reduced":"full")}":mode;
        try
        {
            Root.ContentScaleSize=new Vector2I(1760,1000);
            _gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<OrigamiGallery>();Root.AddChild(_gallery);_gallery.SetProcess(false);
            _stage=_gallery.GetChildren().OfType<GameMapScene>().Single();Rebind();
            _inspector=_gallery.GetChildren().OfType<CanvasLayer>().Single(l=>l.Layer==30).GetChild<Control>(0);Fixtures();
            if(mode=="benchmark")await Benchmark();else if(mode=="film")await Film();else await Acceptance();
        }
        catch(Exception e){_fail.Add(e.ToString());}
        using(var f=FileAccess.Open($"res://artifacts/paper-v4-{_method}-{tag}.json",FileAccess.ModeFlags.Write))
            f.StoreString(JsonSerializer.Serialize(new {status=_fail.Count==0?"passed":"failed",mode,renderer=_method,device=RenderingServer.GetVideoAdapterName(),engine=Engine.GetVersionInfo().ToString(),failures=_fail,evidence=_evidence},new JsonSerializerOptions{WriteIndented=true}));
        foreach(var f in _fail)GD.PushError(f);GD.Print(_fail.Count==0?"PAPER_V4_PASS":"PAPER_V4_FAILED");
        _ui?.QueueFree();var gallery=_gallery;_ui=null!;_gallery=null!;_stage=null!;_motion=null!;_detail=null!;_cells.Clear();_inspector=null!;
        if(gallery!=null)gallery.FinishInspection(_fail.Count==0?0:1);else Quit(1);
    }
}
