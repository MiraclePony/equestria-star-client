using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Api;

namespace EquestriaStar.Game.Map;

/// <summary>Only cosmetic meshes are controlled here. Main silhouettes, interaction and anchors stay resident.</summary>
public partial class ZoomArtDetailController : Node
{
    private sealed class Group
    {
        public readonly List<(MeshInstance3D Mesh,ShaderMaterial[] Fade)> Meshes=[];
        public float Amount;
        public void Add(MeshInstance3D mesh)
        {
            var fades=new List<ShaderMaterial>();
            for(var i=0;i<mesh.Mesh.GetSurfaceCount();i++)
                if(mesh.GetActiveMaterial(i) is ShaderMaterial source)
                {var copy=(ShaderMaterial)source.Duplicate();PaperMaterialLifetime.Remember(copy);copy.SetShaderParameter("detail_fade",0f);mesh.SetSurfaceOverrideMaterial(i,copy);fades.Add(copy);}
            Meshes.Add((mesh,fades.ToArray()));mesh.Visible=false;
        }
        public void Update(float target,float step)
        {
            var next=Mathf.MoveToward(Amount,target,step);if(next==Amount)return;Amount=next;
            foreach(var (mesh,materials) in Meshes)
            {
                if(!GodotObject.IsInstanceValid(mesh))continue;
                mesh.Visible=Amount>.001f;
                foreach(var material in materials)material.SetShaderParameter("detail_fade",Amount);
            }
        }
        public void Release()
        {
            Meshes.Clear();
        }
    }
    private sealed class Slot(HexCell3D cell)
    {
        public HexCell3D Cell=cell;
        public Group Existing=new();public Group Streamed=new();
        public OrigamiMapPresentation.NearProp[] Specs=[];
        public Node3D? Root;
        public Node3D? Diorama;   // P9 close-zoom themed set dressing (PaperCellDiorama)
        public int Next;public double LastNeeded;public bool InView;public int Target;public int Shown;
    }
    private readonly List<Slot> _slots=[];
    private Camera3D _camera=null!;
    private ScreenDetailPolicy _policy=null!;
    private Node3D _mapRoot=null!;
    private MapInformationOverlay _information=null!;
    private readonly HashSet<(long,long)> _edges=[];
    private SceneryDrift[] _motions=[];private bool _animationsApplied=true;
    private PaperEnvironmentMotion? _environmentMotion;
    private readonly List<(Node3D Root,Aabb LocalBounds)> _aerial=[];
    private readonly Dictionary<Node3D,Group> _skyFine=[];
    private readonly HashSet<Node3D> _skyFineTargets=[];
    private double _lastFrameTime;
    private float _overview;
    public ZoomDetailSettings Settings {get;set;}=ZoomDetailSettings.Load();
    public DetailAssetCache Cache {get;set;}=DetailAssetCache.Shared;
    public Vector4 ViewInsets {get;set;}
    public bool DetailsEnabled {get;set;}=true;
    public bool AnimationsEnabled {get;set;}=true;
    public bool AutomaticUpdates {get;set;}=true;
    public int Stage=>_policy?.Stage??0;
    public float ScreenRatio {get;private set;}
    public int InstancesCreated {get;private set;}
    public int MaxInstantiatedInFrame {get;private set;}
    public double MaxInstantiationMs {get;private set;}
    public double LastTickMs {get;private set;}
    public double MaxTickMs {get;private set;}
    public int ResidentCellCount=>_slots.Count(s=>s.Root!=null);
    public int DioramaCount=>_slots.Count(s=>s.Diorama!=null);
    public int VisibleDioramas=>_slots.Count(s=>s.Diorama is {Visible:true});
    public int VisibleNearCells=>_slots.Count(s=>s.Existing.Amount>.99f || s.Streamed.Amount>.99f);
    public int NearMeshCount=>_slots.Sum(s=>s.Existing.Meshes.Count+s.Streamed.Meshes.Count);
    public int CriticalMarkerCount=>_information?.MarkerCount??0;
    public Rect2 EffectiveViewport
    {
        get
        {
            var size=_camera.GetViewport().GetVisibleRect().Size;
            return new Rect2(new Vector2(ViewInsets.X,ViewInsets.Y),new Vector2(Math.Max(1,size.X-ViewInsets.X-ViewInsets.Z),Math.Max(1,size.Y-ViewInsets.Y-ViewInsets.W)));
        }
    }
    public IReadOnlyList<HexCell3D> Cells=>_slots.Select(s=>s.Cell).ToArray();
    public void Configure(Camera3D camera,IEnumerable<HexCell3D> cells,Node3D mapRoot,IEnumerable<MapEdgeDto>? edges=null)
    {
        _camera=camera;_mapRoot=mapRoot;_policy=new ScreenDetailPolicy(Settings);
        _environmentMotion=mapRoot.GetNodeOrNull<PaperEnvironmentMotion>("GeneratedCells/PaperEnvironmentMotion");
        foreach(var cell in cells)
        {
            var slot=new Slot(cell){Specs=OrigamiMapPresentation.NearProps(cell)};
            var deco=cell.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset/Decoration");
            if(deco!=null)CollectSmall(deco,slot);
            _slots.Add(slot);
            foreach(var main in cell.FindChildren("*","Node3D",true,false).OfType<Node3D>().Where(n=>n.HasMeta("zoom_main_tree")))
                foreach(var mesh in main.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>())mesh.LodBias=1000;
        }
        var layer=new CanvasLayer {Layer=8};AddChild(layer);
        _information=new MapInformationOverlay();layer.AddChild(_information);
        _information.Configure(camera,Cells,()=>EffectiveViewport);
        _motions=_mapRoot.FindChildren("*","Node",true,false).OfType<SceneryDrift>().ToArray();
        foreach(var item in _mapRoot.FindChildren("*","Node3D",true,false).OfType<Node3D>())
            if(item.HasMeta("environment_asset") && item.GetMeta("environment_asset").AsString() is "BG003" or "BG004" or "BG006" or "BG101" or "BG102" or "BG103" or "BG104" or "BG105" or "BG108" or "BG109")
            {
                var meshes=item.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>m.Mesh!=null).ToArray();
                if(meshes.Length==0)continue;
                var box=item.GlobalTransform.AffineInverse()*meshes[0].GlobalTransform*meshes[0].GetAabb();
                foreach(var mesh in meshes.Skip(1))box=box.Merge(item.GlobalTransform.AffineInverse()*mesh.GlobalTransform*mesh.GetAabb());
                _aerial.Add((item,box));
                if(item.GetMeta("environment_asset").AsString() is "BG101" or "BG102")
                {
                    var group=new Group();
                    foreach(var part in new[]{"part03","part04","part05","part06","part08","part09"})
                    {
                        var branch=item.GetNodeOrNull<Node3D>("MotionPivot/"+part)??item.GetNodeOrNull<Node3D>(part);if(branch==null)continue;
                        if(branch is MeshInstance3D own && own.Mesh!=null)group.Add(own);
                        foreach(var mesh in branch.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>())group.Add(mesh);
                    }
                    _skyFine[item]=group;
                }
            }
        foreach(var edge in edges??[]) {_edges.Add((edge.FromCellId,edge.ToCellId));_edges.Add((edge.ToCellId,edge.FromCellId));}
    }
    private static void CollectSmall(Node node,Slot slot)
    {
        if(node is Node3D spatial && !spatial.Visible)return;
        if(node is MeshInstance3D mesh && mesh.Mesh!=null)
        {
            var size=(mesh.GlobalTransform*mesh.GetAabb()).Size;
            // Conservative: never classify full trees, supports, towers, entrances or whole assemblies.
            var explicitLow=node.Name.ToString() is "Forest_Stones_And_Mushrooms" or "Forest_Folded_Plants" or "18_R3A_Canterlot_PaperLeafGardens";
            if(explicitLow || size.Y<slot.Cell.Radius*.24f && new Vector2(size.X,size.Z).Length()<slot.Cell.Radius*.85f)
                slot.Existing.Add(mesh);
        }
        foreach(var child in node.GetChildren())CollectSmall(child,slot);
    }
    public override void _Process(double delta){if(AutomaticUpdates && _camera!=null)Tick(delta);}
    public void Tick(double delta,double? clock=null)
    {
        var totalWatch=Stopwatch.StartNew();
        var now=clock??Time.GetTicksMsec()/1000.0;_lastFrameTime=now;
        var rect=EffectiveViewport;var margin=rect.Size*Settings.PrefetchMargin;
        var expanded=new Rect2(rect.Position-margin,rect.Size+margin*2);
        var centre=rect.GetCenter();
        var sample=_slots.OrderBy(s=>_camera.UnprojectPosition(s.Cell.GlobalPosition).DistanceSquaredTo(centre)).First();
        ScreenRatio=ScreenDetailPolicy.Ratio(_camera,sample.Cell.GlobalPosition,sample.Cell.Radius,rect);
        var stage=_policy.Update(ScreenRatio);
        var candidates=new List<Slot>();
        foreach(var slot in _slots)
        {
            slot.InView=!_camera.IsPositionBehind(slot.Cell.GlobalPosition) && expanded.Intersects(ScreenDetailPolicy.GroundRect(_camera,slot.Cell.GlobalPosition,slot.Cell.Radius));
            slot.Target=slot.InView?stage:0;
            if(DetailsEnabled && slot.InView && ScreenRatio>=Settings.PrefetchRatio)candidates.Add(slot);
        }
        candidates=candidates.OrderBy(s=>s.Cell.VisualState==HexCellVisualState.Selected?0:1)
            .ThenBy(s=>_camera.UnprojectPosition(s.Cell.GlobalPosition).DistanceSquaredTo(centre)).Take(Settings.MaxNearCells).ToList();
        var wanted=new HashSet<Slot>(candidates);
        string[] Paths(Slot slot)=>slot.Specs.SelectMany(s=>s.Parts.Select(p=>EnvironmentArtCatalog.Entries[s.Id].Components[p].Path)).Distinct().ToArray();
        Cache.Publish(GetInstanceId(),candidates.SelectMany(Paths));Cache.Tick(now,Settings);
        var count=0;var watch=Stopwatch.StartNew();
        foreach(var slot in candidates)
        {
            slot.LastNeeded=now;
            if(slot.Next>=slot.Specs.Length || count>=Settings.MaxInstancesPerFrame || watch.Elapsed.TotalMilliseconds>=Settings.InstantiateBudgetMs)continue;
            var spec=slot.Specs[slot.Next];
            var paths=spec.Parts.Select(p=>EnvironmentArtCatalog.Entries[spec.Id].Components[p].Path).ToArray();
            if(paths.Any(p=>Cache.Peek(p)==null))continue;
            if(slot.Root==null)
            {slot.Root=new Node3D {Name="StreamedNearDetails"};slot.Cell.GetNode("VisualRoot/DecorationRoot").AddChild(slot.Root);}
            var prop=EnvironmentArtCatalog.PlaceParts(slot.Root,spec.Id,spec.Parts,spec.Position,spec.Scale,spec.Yaw,Cache.Peek);
            slot.Next++;count++;InstancesCreated++;
            if(prop==null)continue;
            var r=slot.Cell.Radius;
            var reserve=new Aabb(slot.Cell.GlobalPosition+new Vector3(-r*.30f,.045f,-r*.30f),new Vector3(r*.60f,r*.75f,r*.60f));
            for(var i=0;i<12 && (VisualClearance.Intersects(prop,reserve) || !new MeshClearanceQuery(prop).IsFree(PawnArtLayout.WorldAnchor(slot.Cell),r));i++)prop.Scale*=.88f;
            if(!new MeshClearanceQuery(prop).IsFree(PawnArtLayout.WorldAnchor(slot.Cell),r)){prop.Free();continue;}
            var rails=_mapRoot.GetNodeOrNull<Node3D>("ReferenceRailDraft");
            if(rails!=null && rails.HasMeta("reserved_points") && rails.GetMeta("reserved_points").AsVector3Array().Any(p=>new Vector2(p.X-prop.GlobalPosition.X,p.Z-prop.GlobalPosition.Z).Length()<r*.40f))
            {prop.Free();continue;}
            ConceptPaperStyle.Apply(prop);SceneryShadowBudget.Apply(prop,r);
            foreach(var mesh in prop.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>())slot.Streamed.Add(mesh);
            Cache.CountUse(paths);
        }
        // Close zoom: build each in-view cell's themed diorama, one cell per frame within budget.
        if(PaperCellDiorama.Enabled && DetailsEnabled && stage>=2)
        {
            var built=0;
            foreach(var slot in candidates)
            {
                if(slot.Diorama!=null)continue;
                if(built>=1 || watch.Elapsed.TotalMilliseconds>=Settings.InstantiateBudgetMs*3)break;
                slot.Diorama=PaperCellDiorama.Build(slot.Cell);slot.LastNeeded=now;built++;
            }
        }
        foreach(var slot in _slots)
            if(slot.Diorama!=null)slot.Diorama.Visible=DetailsEnabled && stage>=2 && wanted.Contains(slot);
        if(PaperCellDiorama.Enabled)
        {
            // Widest zoom reads like a strategy map: pawn beacons up, exterior scenery steps back.
            _overview=Mathf.MoveToward(_overview,stage==0?1f:stage==1?.4f:0f,(float)delta/Settings.FadeSeconds);
            RenderingServer.GlobalShaderParameterSet("paper_overview",_overview);
            foreach(var slot in _slots)
            {
                var beacon=PaperPawnBeacon.Sync(slot.Cell);
                if(beacon==null)continue;
                beacon.Visible=stage<2;
                if(beacon.Visible)PaperPawnBeacon.Scale(beacon,_camera.Size,slot.Cell.Radius);
            }
        }
        if(count>0)MaxInstantiationMs=Math.Max(MaxInstantiationMs,watch.Elapsed.TotalMilliseconds);
        MaxInstantiatedInFrame=Math.Max(MaxInstantiatedInFrame,count);
        foreach(var slot in _slots)
        {
            var show=DetailsEnabled && stage>=2 && wanted.Contains(slot);
            var step=(float)delta/Settings.FadeSeconds;
            slot.Existing.Update(show?1:0,step);slot.Streamed.Update(show?1:0,step);
            var faded=(slot.Existing.Meshes.Count==0 || slot.Existing.Amount>.99f) && (slot.Streamed.Meshes.Count==0 || slot.Streamed.Amount>.99f);
            slot.Shown=show && faded && (slot.Specs.Length==0 || slot.Next==slot.Specs.Length)?stage:Math.Min(slot.Target,1);
        }
        var released=0;
        foreach(var slot in _slots.Where(s=>s.Root!=null && !wanted.Contains(s) && s.Streamed.Amount==0).OrderBy(s=>s.LastNeeded).ToArray())
            if(released<Settings.MaxReleasesPerFrame && (now-slot.LastNeeded>Settings.CacheGraceSeconds || ResidentCellCount>Settings.MaxRetainedCells))
            {slot.Root!.Free();slot.Root=null;slot.Streamed=new Group();slot.Next=0;released++;}
        foreach(var slot in _slots.Where(s=>s.Diorama!=null && !s.Diorama.Visible && now-s.LastNeeded>Settings.CacheGraceSeconds).Take(Settings.MaxReleasesPerFrame).ToArray())
        {slot.Diorama!.QueueFree();slot.Diorama=null;}
        // One complete mesh at every distance; zoom only controls independent cosmetic details.
        if(_camera.GetViewport().MeshLodThreshold!=0)_camera.GetViewport().MeshLodThreshold=0;
        var ties=_mapRoot.GetNodeOrNull<Node3D>("ReferenceRailDraft/PaperSleepers");if(ties!=null)ties.Visible=stage>=1;
        if(_environmentMotion!=null)_environmentMotion.Enabled=AnimationsEnabled;
        else if(AnimationsEnabled!=_animationsApplied)
        {foreach(var motion in _motions)if(GodotObject.IsInstanceValid(motion))motion.SetProcess(AnimationsEnabled);_animationsApplied=AnimationsEnabled;}
        _information.Refresh();
        foreach(var (root,bounds) in _aerial)
        {
            if(!GodotObject.IsInstanceValid(root))continue;
            var world=root.GlobalTransform*bounds;var low=new Vector2(float.MaxValue,float.MaxValue);var high=-low;
            for(var i=0;i<8;i++){var p=_camera.UnprojectPosition(world.GetEndpoint(i));low=low.Min(p);high=high.Max(p);}
            var projected=new Rect2(low,high-low);root.Visible=!_information.ProtectedRegions.Any(r=>r.Intersects(projected));
            if(_skyFine.TryGetValue(root,out var fine))
            {
                var ratio=projected.Size.Y/Math.Max(1,rect.Size.Y);
                if(ratio>.165f)_skyFineTargets.Add(root);else if(ratio<.135f)_skyFineTargets.Remove(root);
                fine.Update(DetailsEnabled && root.Visible && _skyFineTargets.Contains(root)?1:0,(float)delta/Settings.FadeSeconds);
            }
        }
        LastTickMs=totalWatch.Elapsed.TotalMilliseconds;MaxTickMs=Math.Max(MaxTickMs,LastTickMs);
    }
    public bool SetCriticalRoute(IEnumerable<string> codes)
    {
        var route=codes.ToArray();var cells=Cells.ToDictionary(c=>c.Data!.CellCode);
        if(route.Any(c=>!cells.ContainsKey(c)))return false;
        for(var i=1;i<route.Length;i++)if(!_edges.Contains((cells[route[i-1]].Data!.CellId,cells[route[i]].Data!.CellId)))return false;
        _information.Route=route;return true;
    }
    public object Snapshot()=>new {stage=Stage,screenRatio=ScreenRatio,detailsEnabled=DetailsEnabled,nearCells=ResidentCellCount,visibleNearCells=VisibleNearCells,
        nearMeshes=NearMeshCount,dioramas=DioramaCount,visibleDioramas=VisibleDioramas,requests=Cache.Requests,cacheHits=Cache.CacheHits,resources=Cache.ResidentCount,loading=Cache.LoadingCount,failed=Cache.FailedCount,
        instances=InstancesCreated,maxPerFrame=MaxInstantiatedInFrame,maxInstantiationMs=MaxInstantiationMs,tickMs=LastTickMs,maxTickMs=MaxTickMs,criticalMarkers=CriticalMarkerCount,
        cells=_slots.Select(s=>new {id=s.Cell.Data!.CellCode,target=s.Target,shown=s.Shown,state=s.Root!=null?"resident":s.Specs.Length>0?string.Join(",",s.Specs.SelectMany(p=>p.Parts.Select(k=>Cache.State(EnvironmentArtCatalog.Entries[p.Id].Components[k].Path)))):"base-only",fade=s.Existing.Amount}).ToArray()};
    public override void _ExitTree()
    {Cache.Release(GetInstanceId());foreach(var slot in _slots){slot.Existing.Release();slot.Streamed.Release();}foreach(var group in _skyFine.Values)group.Release();_slots.Clear();_skyFine.Clear();_aerial.Clear();_motions=[];_environmentMotion=null;}
}
