using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

public partial class RunPaperLookAcceptance : SceneTree
{
    private readonly List<string> _fail=[];private readonly List<object> _cases=[];
    private OrigamiGallery _gallery=null!;private GameMapScene _stage=null!;private ZoomArtDetailController _detail=null!;
    private Control _inspector=null!;private CanvasLayer _fixtures=null!;private readonly List<object> _pixels=[];
    private string _method="";private bool _short;
    public override void _Initialize()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private void Check(bool ok,string why){if(!ok)_fail.Add(why);}
    private async Task Frames(int n){for(var i=0;i<n;i++)await ToSignal(this,SignalName.ProcessFrame);}
    private void Freeze(){foreach(var m in _gallery.FindChildren("*","Node",true,false).OfType<SceneryDrift>())m.SetProcess(false);if(_gallery.EnvironmentMotion is {} motion){motion.Frozen=true;motion.SetClock(0);}}
    private void Focus(HexCell3D cell,float ratio)
    {
        var c=_stage.CameraController.Camera;_detail.ViewInsets=Vector4.Zero;
        var actual=ScreenDetailPolicy.Ratio(c,cell.GlobalPosition,cell.Radius,_detail.EffectiveViewport);
        _stage.CameraController.FocusGround(cell.GlobalPosition,c.Size*actual/ratio,true);
    }
    private async Task<Image> Capture(string name,bool ui=false)
    {
        _inspector.Visible=ui;_fixtures.Visible=ui;await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        var image=Root.GetTexture().GetImage();image.SavePng($"res://artifacts/paper-v3-{_method}-{name}.png");return image;
    }
    private void MakeFixtures()
    {
        _fixtures=new CanvasLayer {Layer=0};Root.AddChild(_fixtures);
        var colors=new[]{Colors.White,new Color("808080"),new Color("3a2969"),new Color("75a75e")};
        for(var i=0;i<4;i++)_fixtures.AddChild(new ColorRect {Position=new Vector2(20+i*55,940),Size=new Vector2(45,45),Color=colors[i],MouseFilter=Control.MouseFilterEnum.Ignore});
        _fixtures.AddChild(new TextureRect {Position=new Vector2(1600,810),Size=new Vector2(120,170),Texture=GD.Load<Texture2D>("res://assets/characters/cards/hf.jpg"),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCovered,MouseFilter=Control.MouseFilterEnum.Ignore});
    }
    private async Task Metrics(string name,HexCell3D cell,float ratio)
    {
        Focus(cell,ratio);_inspector.Visible=false;_fixtures.Visible=false;await Frames(90);
        var cpu=new List<double>();var gpu=new List<double>();var wall=new List<double>();
        for(var i=0;i<120;i++)
        {var t=Time.GetTicksUsec();await Frames(1);wall.Add((Time.GetTicksUsec()-t)/1000.0);cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(Root.GetViewportRid()));var g=RenderingServer.ViewportGetMeasuredRenderTimeGpu(Root.GetViewportRid());if(g>0)gpu.Add(g);}
        double P(List<double> xs,double p)=>xs.Count==0?0:xs.OrderBy(x=>x).ElementAt(Math.Min(xs.Count-1,(int)(xs.Count*p)));
        var draws=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);var memory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed);
        using(var clean=await Capture(name+"-clean")){}using(var ui=await Capture(name+"-ui",true)){}
        _cases.Add(new {name,cell=cell.Data!.CellCode,ratio,cameraSize=_stage.CameraController.Camera.Size,cameraTransform=_stage.CameraController.Camera.GlobalTransform.ToString(),cpuP95=P(cpu,.95),cpuP99=P(cpu,.99),gpuP95=P(gpu,.95),gpuP99=P(gpu,.99),frameP95=P(wall,.95),frameP99=P(wall,.99),drawsClean=draws,memory});
    }
    private static byte[] Region(Image image,Rect2I rect){using var crop=image.GetRegion(rect);return crop.GetData();}
    private async Task PixelChecks()
    {
        _stage.PaperEffect.Visible=true;using var on=await Capture("grade-on",true);
        _stage.PaperEffect.Visible=false;using var off=await Capture("grade-off",true);
        var regions=Enumerable.Range(0,4).Select(i=>("hud-swatch-"+i,new Rect2I(20+i*55,940,45,45))).Append(("actual-card-texture",new Rect2I(1600,810,120,170)));
        foreach(var (name,rect) in regions)
        {var a=Region(on,rect);var b=Region(off,rect);var different=a.Zip(b,(x,y)=>x!=y).Count(x=>x);Check(different==0,"Grade changed "+name);_pixels.Add(new {name,differentBytes=different,total=a.Length});}
        var aa=Region(on,new Rect2I(700,350,400,300));var bb=Region(off,new Rect2I(700,350,400,300));
        var delta=aa.Zip(bb,(x,y)=>Math.Abs(x-y)).Average();Check(delta>0 && delta<15,"Grade missing or too strong");_pixels.Add(new {name="map-grade-delta",meanAbsoluteByteDelta=delta});
        Check(_stage.PaperEffect.GetNode<ColorRect>("PaperGrade").MouseFilter==Control.MouseFilterEnum.Ignore,"Grade intercepts map clicks");
        Check(_stage.PaperEffect.Layer<0,"Default Canvas HUD is below grade");
        if(_method!="gl_compatibility")
        {Root.UseHdr2D=true;_stage.PaperEffect.Visible=true;await Frames(3);Check(!_stage.PaperEffect.IsActive && !_stage.PaperEffect.GetNode<ColorRect>("PaperGrade").Visible,"HDR2D did not bypass LDR grade");Root.UseHdr2D=false;await Frames(3);}
        _stage.PaperEffect.Visible=true;
    }
    private async Task ReferenceRig()
    {
        var viewport=new SubViewport {Size=new Vector2I(64,64),OwnWorld3D=true};Root.AddChild(viewport);
        var rig=GD.Load<GDScript>("res://tests/art/lookdev_reference/paper_look_rig.gd").New().As<Node3D>();viewport.AddChild(rig);
        for(var i=0;i<3;i++){rig.Call("apply_preset",i);Check(rig.Get("sun").As<DirectionalLight3D>().ShadowEnabled,"Reference rig sun disabled");}
        viewport.Free();await Frames(2);
    }
    private object[] Proportions()=>_detail.Cells.Where(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id is "fashion-shop" or "crystal-castle" or "everfree")
        .Where(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id!="everfree" || c.Data!.CellCode=="B3").Select(c=>
        {
            var entry=OrigamiArtCatalog.Resolve(c.Data!)!;var node=c.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset/Building")??c.GetNode<Node3D>("VisualRoot/DecorationRoot/LocalVegetation");
            var meshes=node.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>m.Mesh!=null && m.IsVisibleInTree()).ToArray();
            var box=meshes.Length>0?meshes[0].GlobalTransform*meshes[0].GetAabb():new Aabb(c.GlobalPosition,Vector3.Zero);
            foreach(var m in meshes.Skip(1))box=box.Merge(m.GlobalTransform*m.GetAabb());
            return new {id=entry.Id,cell=c.Data!.CellCode,widthOverDiameter=box.Size.X/(c.Radius*2),depthOverDiameter=box.Size.Z/(c.Radius*2),heightOverDiameter=box.Size.Y/(c.Radius*2),bottom=box.Position.Y,fitScale=PawnArtLayout.Data.Assets.GetValueOrDefault(entry.Id)?.ContentScale??1} as object;
        }).ToArray();
    private async Task Calibration()
    {
        _stage.GetNode<Node3D>("MapRoot").Visible=false;_stage.GetNode<MeshInstance3D>("Floor").Visible=false;
        var stand=new Node3D {Name="ColorCalibration_NotGameplay"};_stage.AddChild(stand);
        var colors=new[]{new Color("ecebe6"),new Color("808080"),new Color("233f58"),new Color("7951a8")};
        for(var i=0;i<4;i++)
        {
            var material=new ShaderMaterial {Shader=GD.Load<Shader>("res://assets/shaders/physical_paper.gdshader")};material.SetShaderParameter("base_color",colors[i]);
            stand.AddChild(new MeshInstance3D {Position=new Vector3(i*1.5f,.4f,0),Mesh=new BoxMesh {Size=Vector3.One*.8f},MaterialOverride=material});
        }
        stand.AddChild(new MeshInstance3D {Position=Vector3.Down*.02f,Mesh=new PlaneMesh {Size=new Vector2(12,8)},MaterialOverride=OrigamiMapPresentation.PaperMaterial(new Color("b9c7bb"))});
        _stage.CameraController.InitializeMap(new Aabb(new Vector3(-1,-.1f,-2),new Vector3(7,.9f,4)));
        _stage.CameraController.FocusGround(new Vector3(2.25f,0,0),6.5f,true);
        for(var i=0;i<3;i++)
        {
            ArtRenderProfile.Preset(_stage,i);await Frames(60);using var shot=await Capture("calibration-"+i);
            var samples=new List<string>();
            foreach(var p in new[]{new Vector3(0,.801f,0),new Vector3(.401f,.4f,0),new Vector3(0,.4f,.401f)})
            {var screen=_stage.CameraController.Camera.UnprojectPosition(p);samples.Add(shot.GetPixel(Math.Clamp((int)screen.X,0,shot.GetWidth()-1),Math.Clamp((int)screen.Y,0,shot.GetHeight()-1)).ToHtml());}
            _pixels.Add(new {name="white-paper-face-samples-"+i,samples});
        }
        stand.Free();_stage.GetNode<Node3D>("MapRoot").Visible=true;ArtRenderProfile.Preset(_stage,0);
    }
    private async Task Run()
    {
        try
        {
            _method=RenderingServer.GetCurrentRenderingMethod();_short=OS.GetCmdlineUserArgs().Contains("--paper-short");Root.ContentScaleSize=new Vector2I(1760,1000);
            await ReferenceRig();
            _gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<OrigamiGallery>();Root.AddChild(_gallery);_gallery.SetProcess(false);
            _stage=_gallery.GetChildren().OfType<GameMapScene>().Single();_detail=_stage.DetailController!;
            _inspector=_gallery.GetChildren().OfType<CanvasLayer>().Single(c=>c.Layer==30).GetChild<Control>(0);MakeFixtures();Freeze();
            var cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);var camera=_stage.CameraController.Camera;
            var trees=_gallery.FindChildren("*","Node3D",true,false).OfType<Node3D>().Where(n=>n.HasMeta("zoom_main_tree")).ToDictionary(n=>n,n=>n.GlobalTransform);
            var anchors=cells.Values.ToDictionary(c=>c,c=>(c.GlobalTransform,PawnArtLayout.WorldAnchor(c),c.InteractionShape));
            RenderingServer.ViewportSetMeasureRenderTime(Root.GetViewportRid(),true);
            await Metrics("overview",cells["A1"],.04f);await Metrics("region",cells["B3"],.10f);
            var shop=cells.Values.Single(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id=="fashion-shop");await Metrics("close",shop,.23f);
            await PixelChecks();
            var sun=_stage.GetNode<DirectionalLight3D>("DirectionalLight3D");var transform=sun.GlobalTransform;var exposure=_stage.GetNode<WorldEnvironment>("Environment").Environment.TonemapExposure;
            foreach(var ratio in new[]{.04f,.1f,.23f,.4f,.16f,.17f,.15f}){Focus(cells["B3"],ratio);await Frames(3);Check(sun.GlobalTransform.IsEqualApprox(transform),"Sun follows zoom");Check(camera.GetViewport().MeshLodThreshold==0,"Automatic mesh LOD enabled");}
            Check(trees.All(x=>x.Key.GlobalTransform.IsEqualApprox(x.Value)),"Primary tree anchor changed");
            Check(anchors.All(x=>x.Key.GlobalTransform.IsEqualApprox(x.Value.GlobalTransform) && PawnArtLayout.WorldAnchor(x.Key).IsEqualApprox(x.Value.Item2) && ReferenceEquals(x.Key.InteractionShape,x.Value.InteractionShape)),"Logical anchor or picking changed");
            Check(Math.Abs(_stage.GetNode<WorldEnvironment>("Environment").Environment.TonemapExposure-exposure)<.0001f,"Exposure changed on zoom");
            Focus(cells["B3"],.10f);await Frames(30);
            var rail=_stage.GetNodeOrNull<Node3D>("MapRoot/ReferenceRailDraft");var seams=_stage.GetNode<Node3D>("MapRoot/GeneratedCells/ContinentalScenery/VerifiedLandSeams");
            var inventory=new {proportions=Proportions(),rootSeats=_gallery.FindChildren("PaperRootSeat","MeshInstance3D",true,false).Count,foldAdaptations=_gallery.FindChildren("*","Node3D",true,false).Count(n=>n.HasMeta("paper_fold_adapted")),filledEdges=seams.GetMeta("filled_existing_edges").AsStringArray(),reservedEdges=seams.GetMeta("reserved_edges").AsStringArray(),railDeckSpans=rail?.GetMeta("bridge_deck_spans").AsInt32()??0,railCentreline=rail?.GetMeta("centerline_segments").AsVector3Array().Select(p=>p.ToString()).ToArray()??[],stationPorts="not-final; retain source trace and list manual survey"};
            if(!_short)
            {
                using(var final=await Capture("lighting-new")){}
                ArtRenderProfile.LegacyLighting(_stage);await Frames(70);using(var old=await Capture("lighting-v2-only")){}
                ArtRenderProfile.Preset(_stage,0);await Frames(70);using(var restored=await Capture("lighting-new-restored")){}
                sun.ShadowEnabled=false;await Frames(20);using(var shadows=await Capture("sun-shadow-off")){}sun.ShadowEnabled=true;
                foreach(var mode in new[]{DirectionalLight3D.ShadowMode.Orthogonal,DirectionalLight3D.ShadowMode.Parallel2Splits,DirectionalLight3D.ShadowMode.Parallel4Splits})
                {sun.DirectionalShadowMode=mode;await Frames(45);using var img=await Capture("shadow-"+mode);}
                sun.DirectionalShadowMode=DirectionalLight3D.ShadowMode.Parallel2Splits;
                sun.LightAngularDistance=.7f;await Frames(60);using(var pcss=await Capture("pcss-test")){}sun.LightAngularDistance=0;
                for(var i=0;i<3;i++){ArtRenderProfile.Preset(_stage,i);await Frames(60);using var preset=await Capture("preset-"+i);}
                ArtRenderProfile.Preset(_stage,0);
                Focus(shop,.23f);await Frames(50);
                using(var matNew=await Capture("material-new")){}
                ConceptPaperStyle.LegacyMaterialForInspection=true;_gallery.RebuildForInspection();_detail=_stage.DetailController!;Freeze();cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);Focus(cells.Values.Single(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id=="fashion-shop"),.23f);await Frames(90);using(var matOld=await Capture("material-v2-only")){}
                ConceptPaperStyle.LegacyMaterialForInspection=false;_gallery.RebuildForInspection();_detail=_stage.DetailController!;Freeze();cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);Focus(cells.Values.Single(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id=="fashion-shop"),.23f);await Frames(90);
                PaperFoldGeometry.Enabled=false;_gallery.RebuildForInspection();_detail=_stage.DetailController!;Freeze();cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);Focus(cells["B3"],.10f);await Frames(90);using(var oldGeo=await Capture("geometry-source-only")){}
                PaperFoldGeometry.Enabled=true;_gallery.RebuildForInspection();_detail=_stage.DetailController!;Freeze();cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);Focus(cells["B3"],.10f);await Frames(90);using(var newGeo=await Capture("geometry-folded-only")){}
                ReferenceRailDraft.QuietStyle=false;_gallery.RebuildForInspection();_detail=_stage.DetailController!;Freeze();cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);Focus(cells["B3"],.10f);await Frames(90);using(var loud=await Capture("rail-v2-weight-only")){}
                ReferenceRailDraft.QuietStyle=true;_gallery.RebuildForInspection();_detail=_stage.DetailController!;Freeze();cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);Focus(cells["B3"],.10f);await Frames(90);using(var quiet=await Capture("rail-quiet-only")){}
            }
            await Calibration();
            PaperSceneAudit.Save(_gallery,$"res://artifacts/paper-v3-{_method}-audit.json");
            var environment=_stage.GetNode<WorldEnvironment>("Environment").Environment;
            Check(_gallery.FindChildren("*","WorldEnvironment",true,false).Count==1 && camera.Environment==null,"Environment override or duplication");
            Check(_gallery.FindChildren("*","DirectionalLight3D",true,false).OfType<DirectionalLight3D>().Count(l=>l.ShadowEnabled)==1,"Duplicate shadowed sun");
            Check(!environment.SsilEnabled && !environment.GlowEnabled && !environment.SdfgiEnabled && !environment.VolumetricFogEnabled,"Unrequested heavy effect");
            Check(_method=="forward_plus" || !environment.SsaoEnabled,"Mobile/Compatibility depends on SSAO");
            using var f=FileAccess.Open($"res://artifacts/paper-v3-{_method}-acceptance.json",FileAccess.ModeFlags.Write);
            f.StoreString(JsonSerializer.Serialize(new {status=_fail.Count==0?"passed":"failed",failures=_fail,renderer=_method,userArgs=OS.GetCmdlineUserArgs(),engine=Engine.GetVersionInfo().ToString(),device=RenderingServer.GetVideoAdapterName(),cases=_cases,pixelChecks=_pixels,inventory,note="Local original-project test with capture/assertion overhead. Desktop device only; no mobile/web/FPS certification. Ports remain an offline draft."},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception e){_fail.Add(e.ToString());}
        foreach(var e in _fail)GD.PushError(e);GD.Print(_fail.Count==0?"PAPER_LOOK_ACCEPTANCE_PASS":"PAPER_LOOK_ACCEPTANCE_FAILED");
        _fixtures?.Free();_gallery?.Free();_gallery=null!;_stage=null!;_detail=null!;_inspector=null!;
        _=Finish(_fail.Count==0?0:1);
    }
    private async Task Finish(int code)
    {
        // Execute after Run's async frame (and its mesh/material locals) has returned.
        await Frames(3);ConceptPaperStyle.ReleaseAtShutdown();OrigamiArtCatalog.ReleaseSceneCache();EnvironmentArtCatalog.ReleaseSceneCache();DetailAssetCache.Shared.ClearAtShutdown();
        GC.Collect();GC.WaitForPendingFinalizers();await Frames(2);GC.Collect();GC.WaitForPendingFinalizers();Quit(code);
    }
}
