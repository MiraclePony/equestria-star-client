using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

/// <summary>Actual map inspection with cold/late resource requests and invariant checks.</summary>
public partial class RunZoomDetailAcceptance : SceneTree
{
    private readonly List<string> _failures=[];
    private readonly List<object> _cases=[];
    private readonly List<double> _cpu=[],_gpu=[],_wall=[],_ticks=[];
    private double _bootstrapMs;private double _videoMemoryPeak;
    private GameMapScene _stage=null!;private ZoomArtDetailController _detail=null!;
    private Dictionary<string,HexCell3D> _cells=null!;private double _clock;
    private bool _capture;
    private Label _annotation=null!;
    private readonly GateLoader _loader=new();
    private sealed class GateLoader : IDetailResourceLoader
    {
        public bool Closed;public readonly Dictionary<string,int> Requests=[];private readonly ThreadedDetailLoader _engine=new();
        public Error Request(string path){Requests[path]=Requests.GetValueOrDefault(path)+1;return _engine.Request(path);}
        public ResourceLoader.ThreadLoadStatus Status(string path)=>Closed?ResourceLoader.ThreadLoadStatus.InProgress:_engine.Status(path);
        public PackedScene? Get(string path)=>_engine.Get(path);
    }
    private sealed class FakeLoader : IDetailResourceLoader
    {
        public readonly Dictionary<string,ResourceLoader.ThreadLoadStatus> States=[];public int Gets;
        public Error Request(string path){if(path=="bad")return Error.FileNotFound;States[path]=ResourceLoader.ThreadLoadStatus.InProgress;return Error.Ok;}
        public ResourceLoader.ThreadLoadStatus Status(string path)=>States[path];
        public PackedScene Get(string path){Gets++;var scene=new PackedScene();var node=new Node3D();scene.Pack(node);node.Free();return scene;}
    }
    public override void _Initialize()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private void Check(bool ok,string text){if(!ok)_failures.Add(text);}
    private async Task Frames(int n)
    {
        for(var i=0;i<n;i++)
        {
            var before=Time.GetTicksUsec();await ToSignal(this,SignalName.ProcessFrame);_wall.Add((Time.GetTicksUsec()-before)/1000.0);_clock+=1/60.0;_detail.Tick(1/60.0,_clock);
            _ticks.Add(_detail.LastTickMs);
            _cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(Root.GetViewportRid()));
            _videoMemoryPeak=Math.Max(_videoMemoryPeak,Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed));
            var gpu=RenderingServer.ViewportGetMeasuredRenderTimeGpu(Root.GetViewportRid());if(gpu>0)_gpu.Add(gpu);
        }
    }
    private void Focus(HexCell3D cell,float ratio)
    {
        var camera=_stage.CameraController.Camera;
        var actual=ScreenDetailPolicy.Ratio(camera,cell.GlobalPosition,cell.Radius,_detail.EffectiveViewport);
        _stage.CameraController.FocusGround(cell.GlobalPosition,camera.Size*actual/ratio,true);
    }
    private async Task Capture(string name,HexCell3D cell,float ratio)
    {
        Focus(cell,ratio);await Frames(65);
        _annotation.Text=$"缩放验收 · {cell.Data!.CellCode} · Z{_detail.Stage} · 六角占屏{_detail.ScreenRatio:P1} | 胶囊为测试体，铁路为离线草稿";
        await Frames(1);
        if(_capture)
        {
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            using var screenshot=Root.GetTexture().GetImage();screenshot.SavePng("res://artifacts/zoom-"+name+".png");
        }
        _cases.Add(new {name,cell=cell.Data!.CellCode,snapshot=_detail.Snapshot()});
        GD.Print($"ZOOM_CASE {name} Z{_detail.Stage} s={_detail.ScreenRatio:F3} residentCells={_detail.ResidentCellCount}");
    }
    private void PureTests()
    {
        var settings=new ZoomDetailSettings();var policy=new ScreenDetailPolicy(settings);
        Check(policy.Update(.04f)==0,"Overview policy");Check(policy.Update(.1f)==1,"Region policy");Check(policy.Update(.2f)==2,"Near policy");
        for(var i=0;i<100;i++)Check(policy.Update(i%2==0?.15f:.17f)==2,"Hysteresis flickered near boundary");
        Check(policy.Update(.14f)==1,"Near exit threshold");Check(policy.Update(.4f)==3,"Showcase threshold");
        var loader=new FakeLoader();var cache=new DetailAssetCache(loader);
        settings.MaxConcurrentLoads=1;settings.CacheGraceSeconds=.2;
        cache.Publish(1,["a","b"]);cache.Tick(0,settings);
        Check(cache.Requests==1 && cache.LoadingCount==1 && cache.Peek("a")==null && loader.Gets==0,"Cold loader blocks or exceeds concurrency");
        cache.Tick(.05,settings);Check(cache.Requests==1 && loader.Gets==0,"Pending resource was fetched synchronously");
        loader.States["a"]=ResourceLoader.ThreadLoadStatus.Loaded;cache.Tick(.1,settings);
        Check(cache.Peek("a")!=null && cache.Requests==2,"Ready resource / queue advance");
        cache.Publish(2,["a"]);cache.Release(1);cache.Tick(1,settings);
        Check(cache.Peek("a")!=null,"Shared scene released while another owner needs it");
        loader.States["b"]=ResourceLoader.ThreadLoadStatus.Loaded;cache.Tick(1.1,settings);
        Check(cache.State("b")=="unloaded","Late unused result was retained beyond grace");
        cache.Release(2);cache.Tick(2,settings);Check(cache.ResidentCount==0,"Cold resources not reclaimed");
        cache.Publish(3,["bad"]);cache.Tick(3,settings);var count=cache.Requests;cache.Tick(4,settings);
        Check(cache.FailedCount==1 && cache.Requests==count && cache.Peek("bad")==null,"Failure fallback or repeated failed requests");
    }
    private async Task Run()
    {
        Node3D? gallery=null;
        try
        {
            PureTests();_capture=!OS.GetCmdlineUserArgs().Contains("--zoom-no-capture");
            var boot=System.Diagnostics.Stopwatch.StartNew();
            gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<Node3D>();Root.AddChild(gallery);gallery.SetProcess(false);_bootstrapMs=boot.Elapsed.TotalMilliseconds;
            _stage=gallery.GetChildren().OfType<GameMapScene>().Single();_detail=_stage.DetailController??throw new InvalidOperationException("Live map detail controller was not installed");
            _detail.AutomaticUpdates=false;_detail.Cache.Release(_detail.GetInstanceId());_detail.Cache=new DetailAssetCache(_loader);
            _cells=_detail.Cells.ToDictionary(c=>c.Data!.CellCode);
            ReferenceRailDraft.Add(_stage.GetNode<Node3D>("MapRoot"),_cells);
            var rail=_stage.GetNode<MeshInstance3D>("MapRoot/ReferenceRailDraft/CreamDoubleRails");var railBounds=rail.GetAabb();
            var labelLayer=new CanvasLayer {Layer=30};Root.AddChild(labelLayer);_annotation=new Label {Position=new Vector2(18,14),Text="缩放加载验收",Modulate=new Color("fff6d7")};labelLayer.AddChild(_annotation);
            var forest=_cells["B3"];var palace=_cells.Values.Single(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id=="canterlot");
            var coast=_cells.Values.Single(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id=="hippogriff");var school=_cells["A6"];
            var main=_cells.Values.SelectMany(c=>c.FindChildren("*","Node3D",true,false).OfType<Node3D>())
                .Where(n=>n.HasMeta("zoom_main_tree")).ToDictionary(n=>n,n=>(n.GlobalTransform,n.Visible));
            var anchors=_cells.Values.ToDictionary(c=>c,c=>(c.GlobalTransform,PawnArtLayout.WorldAnchor(c),c.InteractionShape));
            var sun=_stage.GetNode<DirectionalLight3D>("DirectionalLight3D");var sunTransform=sun.GlobalTransform;
            var environment=_stage.GetNode<WorldEnvironment>("Environment").Environment;var exposure=environment.TonemapExposure;
            forest.SetSelected(true);_cells["A1"].SetCurrent(true);_cells["A2"].SetMovable(true);
            var pawn=new Node3D {Name="InspectionPawn_NotServerPlayer"};forest.GetNode("TokenRoot").AddChild(pawn);
            pawn.AddChild(new MeshInstance3D {Position=Vector3.Up*.19f,Mesh=new CapsuleMesh {Radius=.10f,Height=.38f},MaterialOverride=new StandardMaterial3D {AlbedoColor=new Color("f5be69")}});
            Check(_detail.SetCriticalRoute(["A1","A2"]),"Actual map edge rejected");Check(!_detail.SetCriticalRoute(["A1","B33"]),"Invented map route accepted");
            RenderingServer.ViewportSetMeasureRenderTime(Root.GetViewportRid(),true);
            _loader.Closed=true;Focus(forest,.23f);await Frames(12);
            Check(_detail.Cache.LoadingCount<=2 && _detail.ResidentCellCount==0,"Cold detail exceeds load budget or instantiates before readiness");
            Check(_cells.Values.All(c=>c.IsInsideTree() && c.GetNode("InteractionArea")!=null),"Base board vanished during loading");
            Focus(palace,.4f);_loader.Closed=false;await Frames(40);
            Check(forest.GetNodeOrNull("VisualRoot/DecorationRoot/StreamedNearDetails")==null,"Late forest request instantiated into an inactive region");
            await Capture("overview",forest,.035f);Check(_detail.Stage==0 && _detail.VisibleNearCells==0,"Overview did not hide small detail");
            await Capture("forest-region",forest,.10f);Check(_detail.Stage==1,"Region did not reach Z1");
            await Capture("forest-near",forest,.23f);Check(_detail.Stage==2 && _detail.VisibleNearCells>0 && _detail.Cache.ResidentCount>0,"Near details failed to become resident");
            var prior=_detail.Cache.Requests;
            for(var i=0;i<30;i++){Focus(forest,i%2==0?.15f:.17f);await Frames(1);Check(_detail.Stage==2,"Actual camera boundary flicker");}
            Check(_loader.Requests.All(p=>p.Value==1),"Cache reloaded resources during boundary jitter");
            await Capture("forest-showcase",forest,.40f);Check(_detail.Stage==3,"Showcase not reachable through normal camera limits");
            await Capture("palace-near",palace,.23f);await Capture("school-near",school,.23f);await Capture("coast-near",coast,.23f);
            for(var i=0;i<70;i++)
            {
                var ratio=.025f+(.40f-.025f)*(1-Math.Abs(i/35f-1));Focus(forest,ratio);await Frames(1);
                Check(_detail.CriticalMarkerCount>=3,"Critical information vanished on zoom");
                Check(rail.Visible && rail.GetAabb()==railBounds,"Rail centreline or visibility changed on zoom");
                Check(_stage.CameraController.Camera.GetViewport().MeshLodThreshold==0,"Zoom substituted simplified meshes against single-mesh art policy");
            }
            Check(main.All(p=>GodotObject.IsInstanceValid(p.Key) && p.Key.GlobalTransform.IsEqualApprox(p.Value.GlobalTransform) && p.Key.Visible==p.Value.Visible),"Main trees moved, resized or vanished on zoom");
            Check(anchors.All(p=>p.Key.GlobalTransform.IsEqualApprox(p.Value.GlobalTransform) && PawnArtLayout.WorldAnchor(p.Key).IsEqualApprox(p.Value.Item2) && ReferenceEquals(p.Key.InteractionShape,p.Value.InteractionShape)),"Tile, pawn anchor or picking identity changed");
            Check(sun.GlobalTransform.IsEqualApprox(sunTransform) && Math.Abs(environment.TonemapExposure-exposure)<.00001f,"Lighting/exposure changed on zoom");
            Focus(forest,.23f);await Frames(35);var requests=_detail.Cache.Requests;_detail.DetailsEnabled=false;await Frames(25);
            Check(_detail.VisibleNearCells==0 && _detail.CriticalMarkerCount>=3,"Disabling decoration hides gameplay or leaves near detail visible");
            _detail.DetailsEnabled=true;await Frames(35);Check(_detail.Cache.Requests==requests,"Toggle unnecessarily reloads cached resources");
            _detail.ViewInsets=new Vector4(420,0,0,220);Focus(forest,.23f);await Frames(5);Check(Math.Abs(_detail.ScreenRatio-.23f)<.015f,"Effective map viewport ignored UI insets");_detail.ViewInsets=Vector4.Zero;
            var oldSize=Root.Size;
            foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1720,720)})
            {Root.Size=size;await Frames(3);Focus(forest,.23f);await Frames(3);Check(Math.Abs(_detail.ScreenRatio-.23f)<.015f,"Screen metric failed after aspect change");}
            Root.Size=oldSize;await Frames(3);Focus(forest,.035f);await Frames(25);_clock+=10;_detail.Tick(.1,_clock);await Frames(20);
            Check(_detail.ResidentCellCount==0 && _detail.Cache.ResidentCount==0,"Out-of-view detail cache not reclaimed after grace");
            _detail.Settings.MaxNearCells=3;_detail.Settings.MaxRetainedCells=4;Focus(forest,.23f);await Frames(40);Focus(school,.23f);await Frames(40);
            Check(_detail.ResidentCellCount<=4,"Low-memory retained cell limit exceeded");
            _detail.Settings.MaxNearCells=12;_detail.Settings.MaxRetainedCells=32;
            Check(_detail.MaxInstantiatedInFrame<=_detail.Settings.MaxInstancesPerFrame,"Instance frame budget exceeded");
            var clicked=false;forest.CellClicked+=_=>clicked=true;forest.TryActivate();Check(clicked,"Click no longer works after cache eviction");
            double P(List<double> values,float percentile)=>values.Count==0?0:values.OrderBy(x=>x).ToArray()[Math.Min(values.Count-1,(int)(values.Count*percentile))];
            using var report=FileAccess.Open("res://artifacts/zoom-acceptance.json",FileAccess.ModeFlags.Write);
            report.StoreString(JsonSerializer.Serialize(new {date="2026-10-25",status=_failures.Count==0?"passed":"failed",failures=_failures,mainTreesChecked=main.Count,cellsChecked=anchors.Count,
                renderer=RenderingServer.GetCurrentRenderingMethod(),device=RenderingServer.GetVideoAdapterName(),initialResidentMapBuildMs=_bootstrapMs,cpuRenderP95=P(_cpu,.95f),cpuRenderP99=P(_cpu,.99f),gpuP95=P(_gpu,.95f),gpuP99=P(_gpu,.99f),
                wallFrameP95=P(_wall,.95f),wallFrameP99=P(_wall,.99f),detailTickP95=P(_ticks,.95f),detailTickP99=P(_ticks,.99f),
                gpuSamples=_gpu.Count,videoMemoryPeakBytes=_videoMemoryPeak,videoMemoryBytes=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),cases=_cases,final=_detail.Snapshot(),
                meshPolicy="single-complete-mesh",
                note="Local inspection includes assertion/capture overhead; timings are diagnostics, not release FPS or mobile certification. One complete mesh is retained at every distance; independent cosmetic detail visibility only."},new JsonSerializerOptions {WriteIndented=true}));
        }
        catch(Exception exception){_failures.Add(exception.ToString());}
        foreach(var failure in _failures)GD.PushError(failure);
        GD.Print(_failures.Count==0?"Zoom detail acceptance passed.":$"Zoom detail acceptance failed: {_failures.Count}");
        _cells?.Clear();_cells=null!;_stage=null!;_detail=null!;_annotation=null!;
        if(gallery is OrigamiGallery preview)preview.FinishInspection(_failures.Count==0?0:1);
        else {gallery?.Free();Quit(_failures.Count==0?0:1);}
    }
}
