using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

/// <summary>Actual Gallery update chain; no manual motion stepping in animation checks or film.</summary>
public partial class RunPaperV5Acceptance : SceneTree
{
    private OrigamiGallery _gallery=null!;private GameMapScene _stage=null!;private Camera3D _camera=null!;private Control _panel=null!;
    private PaperEnvironmentMotion Motion=>_gallery.EnvironmentMotion!;
    private ZoomArtDetailController Detail=>_stage.DetailController!;
    private readonly List<string> _fail=[];private readonly List<object> _evidence=[];
    private string _method="";private string[] _args=[];
    private string Arg(string key,string fallback){var p=Array.IndexOf(_args,key);return p>=0 && p+1<_args.Length?_args[p+1]:fallback;}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(this,SignalName.ProcessFrame);}
    private void Check(bool ok,string error){if(!ok)_fail.Add(error);}
    private static float[] V(Vector3 v)=>[v.X,v.Y,v.Z];
    private CheckButton Toggle(string text)=>_gallery.FindChildren("*","CheckButton",true,false).OfType<CheckButton>().Single(b=>b.Text==text);
    private void Set(string text,bool value)=>Toggle(text).ButtonPressed=value;
    private SceneryDrift[] Balloons()=>_gallery.FindChildren("*","Node",true,false).OfType<SceneryDrift>().Where(d=>d.IsBalloon).ToArray();
    private void CameraCase(string name)
    {
        using var d=JsonDocument.Parse(FileAccess.GetFileAsString("res://artifacts/paper-v5-before-diagnostic.json"));
        var c=d.RootElement.GetProperty("cameras").EnumerateArray().Single(c=>c.GetProperty("view").GetString()==name);
        Vector3 P(string key){var a=c.GetProperty(key).EnumerateArray().Select(v=>v.GetSingle()).ToArray();return new Vector3(a[0],a[1],a[2]);}
        _camera.GlobalTransform=new Transform3D(new Basis(P("x"),P("y"),P("z")),P("origin"));_camera.Size=c.GetProperty("size").GetSingle();
        _stage.CameraController.SetProcess(false);
    }
    private async Task<Image> Shot(string name,bool ui=false)
    {_panel.Visible=ui;_gallery.FindChild("ArtSettingsButton",true,false)!.Set("visible",ui);await Frames(4);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);var im=Root.GetTexture().GetImage();im.SavePng($"res://artifacts/paper-v5-{_method}-{name}.png");return im;}
    private static double Difference(Image a,Image b,Rect2I region)
    {using var x=a.GetRegion(region);using var y=b.GetRegion(region);x.Convert(Image.Format.Rgb8);y.Convert(Image.Format.Rgb8);return x.GetData().Zip(y.GetData(),(p,q)=>Math.Abs(p-q)).Average();}
    private Vector3 Centre(SceneryDrift drift)
    {
        var meshes=drift.VisualPivot.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>m.Mesh!=null && m.IsVisibleInTree()).ToArray();
        if(meshes.Length==0)return drift.VisualPivot.GlobalPosition;
        var box=meshes[0].GlobalTransform*meshes[0].GetAabb();foreach(var m in meshes.Skip(1))box=box.Merge(m.GlobalTransform*m.GetAabb());return box.GetCenter();
    }
    private async Task MotionCycle(string label,bool requireRange)
    {
        var all=Balloons();var anchor=all.ToDictionary(b=>b,b=>b.Anchor.GlobalTransform);var camera=_camera.GlobalTransform;var size=_camera.Size;
        var samples=new List<object>();var screens=all.ToDictionary(b=>b,_=>new List<Vector2>());var clockStart=Motion.Clock;
        for(int f=0;f<=240;f++)
        {
            if(label=="normal-main-gallery-cycle" && f is 0 or 90){using var frame=await Shot("automatic-balloon-"+f);}
            if(f%4==0)foreach(var b in all)
            {
                var world=Centre(b);var screen=_camera.UnprojectPosition(world);screens[b].Add(screen);
                samples.Add(new {frame=f,clock=Motion.Clock,path=b.Anchor.GetPath().ToString(),anchorId=b.Anchor.GetInstanceId(),pivotPath=b.VisualPivot.GetPath().ToString(),pivotId=b.VisualPivot.GetInstanceId(),viewport=b.Anchor.GetViewport().GetPath().ToString(),b.Updates,b.LastFrame,visible=b.VisualPivot.IsVisibleInTree(),centre=V(world),screen=new[]{screen.X,screen.Y},anchor=V(b.Anchor.GlobalPosition),pivot=V(b.VisualPivot.Position),managerProcessing=Motion.IsProcessing()});
            }
            await Frames(1);
        }
        var ranges=all.Select(b=>new {name=b.Anchor.Name.ToString(),peakToPeakX=screens[b].Max(p=>p.X)-screens[b].Min(p=>p.X),peakToPeakY=screens[b].Max(p=>p.Y)-screens[b].Min(p=>p.Y),inViewport=screens[b].Any(p=>new Rect2(Vector2.Zero,Root.GetVisibleRect().Size).HasPoint(p)),anchorStable=b.Anchor.GlobalTransform.IsEqualApprox(anchor[b])}).ToArray();
        Check(Motion.Clock-clockStart>=7.9,"Actual automatic clock failed full cycle");Check(Motion.AerialCount>=11 && all.Length==3,"Visible assets not registered in manager");
        Check(_camera.GlobalTransform.IsEqualApprox(camera) && _camera.Size==size,"Camera moved during balloon measurements");Check(ranges.All(r=>r.anchorStable),"Balloon static anchor animated");
        if(requireRange)Check(ranges.All(r=>r.peakToPeakY>=8 && r.peakToPeakY<=16),"Balloon screen bob outside 8–16px: "+JsonSerializer.Serialize(ranges));
        _evidence.Add(new {name=label,galleryProcessing=_gallery.IsProcessing(),Motion.TickCount,Motion.AerialCount,ranges,samples});
    }
    private async Task Acceptance()
    {
        Set("冻结环境（拍照）",true);Motion.SetClock(2.25);
        foreach(var preset in new[]{5,6})
        {
            ArtRenderProfile.Preset(_stage,preset);
            foreach(var view in new[]{"overview","region","close"})
            {CameraCase(view);await Frames(90);using var clean=await Shot($"{preset}-{view}-clean");using var uiShot=await Shot($"{preset}-{view}-ui",true);}
        }
        ArtRenderProfile.Preset(_stage,5);CameraCase("overview");
        foreach(var c in Detail.Cells)c.Visible=false;
        var place=_stage.GetNode<Node3D>("MapRoot/GeneratedCells/PaperPlaceComposition");place.Visible=false;
        var rail=_stage.GetNodeOrNull<Node3D>("MapRoot/ReferenceRailDraft");if(rail!=null)rail.Visible=false;
        var info=Detail.GetChildren().OfType<CanvasLayer>().Single();info.Visible=false;
        Motion.Visible=false;using(var bg=await Shot("background-only")){}Motion.Visible=true;
        foreach(var c in Detail.Cells)c.Visible=true;place.Visible=true;if(rail!=null)rail.Visible=true;info.Visible=true;
        var baseTransform=_camera.GlobalTransform;var target=Detail.Cells.Aggregate(Vector3.Zero,(v,c)=>v+c.GlobalPosition)/Detail.Cells.Count;
        foreach(var degrees in new[]{90,180,270})
        {var rotate=new Basis(Vector3.Up,Mathf.DegToRad(degrees));_camera.GlobalPosition=target+rotate*(baseTransform.Origin-target);_camera.LookAt(target);using var a=await Shot("angle-"+degrees);}
        _camera.GlobalTransform=baseTransform;_camera.Size*=1.25f;using(var world=await Shot("world-overview")){}
        CameraCase("region");_panel.Visible=false;Set("冻结环境（拍照）",false);await Frames(90);
        var roots=Detail.Cells.SelectMany(c=>c.FindChildren("*","Node3D",true,false).OfType<Node3D>()).Where(n=>n.HasMeta("zoom_main_tree")).ToDictionary(n=>n,n=>n.GlobalTransform);
        var cells=Detail.Cells.ToDictionary(c=>c.Data!.CellCode,c=>(c.GlobalTransform,PawnArtLayout.WorldAnchor(c),c.InteractionShape));
        await MotionCycle("normal-main-gallery-cycle",true);
        foreach(var name in new[]{"BalloonEast","BalloonDistant"})
        {
            var balloon=Balloons().Single(b=>b.Anchor.Name==name);
            var focus=Centre(balloon);_camera.GlobalPosition=focus+_camera.GlobalBasis.Z*40;_camera.Size=7.101977f;
            await Frames(30);await MotionCycle("visible-camera-"+name,true);using var image=await Shot("visible-"+name);
            Check(new Rect2(Vector2.Zero,Root.GetVisibleRect().Size).HasPoint(_camera.UnprojectPosition(Centre(balloon))) && balloon.VisualPivot.IsVisibleInTree(),"Target balloon not really visible: "+name);
        }
        CameraCase("region");await Frames(30);
        // Freeze and resume through the real UI signal path.
        Set("冻结环境（拍照）",true);await Frames(3);var frozenClock=Motion.Clock;var pivot=Balloons().Select(b=>b.VisualPivot.Transform).ToArray();
        using var first=await Shot("freeze-start");await Frames(60);using var later=await Shot("freeze-after-two-seconds");
        var delta=Difference(first,later,new Rect2I(0,0,1760,1000));Check(delta<.01 && Motion.Clock==frozenClock,"Freeze changed actual world");
        Check(Balloons().Select((b,i)=>b.VisualPivot.Transform.IsEqualApprox(pivot[i])).All(v=>v),"Frozen pivot changed");
        Set("冻结环境（拍照）",false);await Frames(10);Check(Motion.Clock>frozenClock,"Resume failed");
        Set("环境动画",false);await Frames(20);Check(Motion.MotionWeight<.001 && Balloons().All(b=>b.VisualPivot.Position.Length()<.0001f),"Disable did not smoothly return to rest");
        Set("环境动画",true);await Frames(20);Check(Motion.MotionWeight>.99,"Enable fade failed");
        Set("减少动态",true);await MotionCycle("reduced-main-gallery-cycle",false);Set("减少动态",false);
        Check(roots.All(x=>x.Key.GlobalTransform.IsEqualApprox(x.Value)),"Main tree root moved");
        Check(Detail.Cells.All(c=>c.GlobalTransform.IsEqualApprox(cells[c.Data!.CellCode].GlobalTransform) && PawnArtLayout.WorldAnchor(c).IsEqualApprox(cells[c.Data.CellCode].Item2) && ReferenceEquals(c.InteractionShape,cells[c.Data.CellCode].Item3)),"Board/pawn/picking changed");
        var before=Motion.Clock;_gallery.RebuildForInspection();Check(Motion.Clock>=before && Motion.Clock-before<.05,"Rebuild restarted world time");CameraCase("region");await Frames(30);
        Check(Detail.Cells.All(c=>c.GlobalTransform.IsEqualApprox(cells[c.Data!.CellCode].GlobalTransform)),"Rebuild moved cells");
        var sea=_gallery.FindChildren("Sea","MeshInstance3D",true,false).OfType<MeshInstance3D>().Single();Check(sea.MaterialOverride is ShaderMaterial sm && sm.Shader.ResourcePath.EndsWith("paper_ocean.gdshader"),"Visible sea still uses plain material");
        Check(_camera.GetViewport().MeshLodThreshold==0,"Mesh LOD enabled");
        // Actual HUD colour/real card are above the only grade, with no world-effect tint.
        var ui=new CanvasLayer {Layer=0};Root.AddChild(ui);
        ui.AddChild(new ColorRect {Position=new Vector2(500,940),Size=new Vector2(100,45),Color=new Color("cfb2e4"),MouseFilter=Control.MouseFilterEnum.Ignore});
        ui.AddChild(new TextureRect {Position=new Vector2(1600,810),Size=new Vector2(120,170),Texture=GD.Load<Texture2D>("res://assets/characters/cards/hf.jpg"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered});
        Set("冻结环境（拍照）",true);using var on=await Shot("ui-world-on",true);_stage.PaperEffect.Visible=false;Motion.Visible=false;using var off=await Shot("ui-world-off",true);
        Check(Difference(on,off,new Rect2I(500,940,100,45))==0 && Difference(on,off,new Rect2I(1600,810,120,170))==0,"HUD/card tinted");
        _stage.PaperEffect.Visible=true;Motion.Visible=true;
        if(_method!="gl_compatibility"){Root.UseHdr2D=true;await Frames(4);Check(!_stage.PaperEffect.IsActive,"HDR2D grade bypass failed");Root.UseHdr2D=false;}
        ui.QueueFree();
        _evidence.Add(new {name="invariants",roots=roots.Count,cells=cells.Count,frozenPixelDelta=delta,uiColorDelta=0,geometry="one complete mesh; unchanged pawn, picking and logical graph",background=JsonDocument.Parse(_stage.GetNode<Node3D>("MapRoot/GeneratedCells/ContinentalScenery/V5WorldBackground").GetMeta("groups_json").AsString()).RootElement.Clone(),motion=Motion.Snapshot()});
    }
    private async Task Film()
    {
        CameraCase("region");_panel.Visible=false;_gallery.FindChild("ArtSettingsButton",true,false)!.Set("visible",false);Set("冻结环境（拍照）",false);var start=Motion.Clock;var movieStartFrame=Engine.GetProcessFrames();
        var layer=new CanvasLayer {Layer=35};Root.AddChild(layer);var label=new Label {Position=new Vector2(25,20),Text="V5 · 主预览自动更新 · 固定镜头观察动态",Modulate=new Color("fff4dc")};layer.AddChild(label);
        var originalSize=_camera.Size;var originalPosition=_camera.GlobalPosition;var cell=Detail.Cells.First(c=>c.Data!.CellCode=="A16");var originalCentre=Detail.Cells.First(c=>c.Data!.CellCode=="B3").GlobalPosition;
        for(int frame=0;frame<600;frame++)
        {
            if(frame==300){Set("冻结环境（拍照）",true);label.Text="10–12秒 · 冻结（实际UI开关）";}
            if(frame==360){Set("冻结环境（拍照）",false);label.Text="恢复动态 · 同一实例连续运行";}
            if(frame>=420){label.Text="缓慢拉近 · 锚点/铁路/棋子不动";var u=Mathf.SmoothStep(0,1,(frame-420)/179f);_camera.GlobalPosition=originalPosition+(cell.GlobalPosition-originalCentre)*u;_camera.Size=Mathf.Lerp(originalSize,3.0878f,u);}
            await Frames(1);
        }
        _evidence.Add(new {name="film",frames=600,fps=30,movieStartFrame,simulationSeconds=20,automaticUpdates=Motion.AutomaticUpdates,actualClockAdvance=Motion.Clock-start,sequence="0–10 fixed;10–12 UI freeze;12–14 resume;14–20 slow zoom",note="native MovieWriter, real Gallery automatic _Process; no manual Tick/clock injection; fixed-step film is not realtime FPS"});layer.QueueFree();
    }
    private static double P(List<double> a,double p){var s=a.OrderBy(v=>v).ToArray();return s[Math.Min(s.Length-1,(int)(s.Length*p))];}
    private async Task Benchmark()
    {
        // Windows can restore/maximize an inspection window independently of content
        // scale. Lock only timed runs; the normal Gallery remains freely resizable.
        Root.Mode=Window.ModeEnum.Windowed;Root.Unresizable=true;Root.Size=new Vector2I(1760,1000);await Frames(10);
        _panel.Visible=false;_gallery.FindChild("ArtSettingsButton",true,false)!.Set("visible",false);Set("冻结环境（拍照）",false);RenderingServer.ViewportSetMeasureRenderTime(Root.GetViewportRid(),true);
        foreach(var view in new[]{"overview","region","close"})
        {
            CameraCase(view);await Frames(240);if(Root.GetTexture().GetWidth()!=1760 || Root.GetTexture().GetHeight()!=1000)throw new InvalidOperationException("Benchmark window size changed before timed interval");var cpu=new List<double>();var gpu=new List<double>();var setup=new List<double>();var wall=new List<double>();var motion=new List<double>();var start=Time.GetTicksUsec();var last=start;double draw=0,memory=0;
            while((Time.GetTicksUsec()-start)/1000000.0<30)
            {await Frames(1);var now=Time.GetTicksUsec();wall.Add((now-last)/1000.0);last=now;cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(Root.GetViewportRid()));gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(Root.GetViewportRid()));setup.Add(RenderingServer.GetFrameSetupTimeCpu());motion.Add(Motion.LastTickMs);draw=Math.Max(draw,Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));memory=Math.Max(memory,Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed));}
            if(Root.GetTexture().GetWidth()!=1760 || Root.GetTexture().GetHeight()!=1000)throw new InvalidOperationException("Benchmark window size changed during timed interval");
            _evidence.Add(new {view,baseline=!PaperWorldBackground.Enabled,reduced=Motion.Reduced,durationSeconds=(Time.GetTicksUsec()-start)/1000000.0,samples=wall.Count,width=Root.GetTexture().GetWidth(),height=Root.GetTexture().GetHeight(),cpuP95=P(cpu,.95),cpuP99=P(cpu,.99),gpuP95=P(gpu,.95),gpuP99=P(gpu,.99),setupP95=P(setup,.95),setupP99=P(setup,.99),wallP95=P(wall,.95),wallP99=P(wall,.99),motionP95=P(motion,.95),draws=draw,memoryBytes=memory});
        }
    }
    private async Task BaselineAngles()
    {
        Set("冻结环境（拍照）",true);Motion.SetClock(2.25);CameraCase("overview");
        var original=_camera.GlobalTransform;var target=Detail.Cells.Aggregate(Vector3.Zero,(v,c)=>v+c.GlobalPosition)/Detail.Cells.Count;
        foreach(var angle in new[]{0,90,180,270})
        {var rotate=new Basis(Vector3.Up,Mathf.DegToRad(angle));_camera.GlobalPosition=target+rotate*(original.Origin-target);_camera.LookAt(target);using var shot=await Shot("v4-fallback-angle-"+angle);}
        _evidence.Add(new {name="v4-fallback-angles",note="Same current executable, V4 geometry/colour fallback; original V4 three-camera PNGs are separately preserved from pre-edit run."});
    }
    public override void _Initialize()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        _args=OS.GetCmdlineUserArgs();_method=RenderingServer.GetCurrentRenderingMethod();var mode=Arg("--v5-mode","acceptance");var suffix=mode=="benchmark"?mode+(_args.Contains("--art-v4-baseline")?"-v4":"-v5")+(_args.Contains("--paper-reduced")?"-reduced":"-full"):mode;
        try
        {
            Root.ContentScaleSize=new Vector2I(1760,1000);_gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<OrigamiGallery>();Root.AddChild(_gallery);
            _stage=_gallery.GetChildren().OfType<GameMapScene>().Single();_camera=_stage.CameraController.Camera;_panel=_gallery.GetChildren().OfType<CanvasLayer>().Single(l=>l.Layer==30).GetChild<Control>(0);await Frames(90);
            if(mode=="film")await Film();else if(mode=="benchmark")await Benchmark();else if(mode=="baseline-angles")await BaselineAngles();else await Acceptance();
        }
        catch(Exception e){_fail.Add(e.ToString());}
        using(var f=FileAccess.Open($"res://artifacts/paper-v5-{_method}-{suffix}.json",FileAccess.ModeFlags.Write))f.StoreString(JsonSerializer.Serialize(new {status=_fail.Count==0?"passed":"failed",renderer=_method,failures=_fail,evidence=_evidence},new JsonSerializerOptions{WriteIndented=true}));
        foreach(var f in _fail)GD.PushError(f);GD.Print(_fail.Count==0?"PAPER_V5_PASS":"PAPER_V5_FAILED");var gallery=_gallery;_gallery=null!;_stage=null!;_camera=null!;_panel=null!;gallery.FinishInspection(_fail.Count==0?0:1);
    }
}
