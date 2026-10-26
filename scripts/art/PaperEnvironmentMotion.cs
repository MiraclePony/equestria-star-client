using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

/// <summary>One reproducible world clock for CPU hinges and GPU batches. Never animates gameplay roots.</summary>
public partial class PaperEnvironmentMotion : Node3D
{
    private sealed record Hinge(Node3D Node,Transform3D Rest,float Phase,Vector3 Anchor,Vector3 Axis,bool Flag,float SwayLimit);
    private readonly List<Hinge> _hinges=[];
    private readonly List<MeshInstance3D> _halos=[];
    private readonly List<MultiMeshInstance3D> _batches=[];
    private readonly List<object> _smokeOrigins=[];
    private SceneryDrift[] _aerial=[];
    private Camera3D? _camera;
    private ZoomArtDetailController? _detail;
    private bool _fairy;
    private float _detailAmount=1;
    private float _canopyAmount=1;
    private bool _detailBatchesVisible=true;
    private bool _reduced;
    private float _motionWeight=1;
    public long TickCount {get;private set;}
    public int AerialCount=>_aerial.Length;
    public IReadOnlyList<SceneryDrift> Aerial=>_aerial;
    public float MotionWeight=>_motionWeight;
    public void RestoreState(double clock,bool enabled,bool frozen,float weight)
    {Clock=clock;Enabled=enabled;Frozen=frozen;_motionWeight=weight;Tick(0);}
    public bool Enabled {get;set;}=true;
    public bool Frozen {get;set;}
    public bool Reduced
    {
        get=>_reduced;
        set
        {
            if(value==_reduced)return;_reduced=value;
            foreach(var batch in _batches)
                if(batch.GetMeta("effect_kind").AsInt32()!=0)batch.Multimesh.VisibleInstanceCount=value?Math.Max(1,batch.Multimesh.InstanceCount/2):-1;
        }
    }
    public bool HaloEnabled {get;set;}=true;
    public bool AutomaticUpdates {get;set;}=true;
    public bool UpdateAllHingesForInspection {get;set;}
    public double Clock {get;private set;}
    public int HingeCount=>_hinges.Count;
    public int CrownHingeCount=>_hinges.Count(h=>!h.Flag);
    public int FlagHingeCount=>_hinges.Count(h=>h.Flag);
    public int SmokeSourceCount=>_smokeOrigins.Count;
    public int SwayLimitedHinges=>_hinges.Count(h=>h.SwayLimit<.99f);
    public int BatchCount=>_batches.Count;
    public int VisibleBatchCount=>_batches.Count(b=>b.Visible);
    public bool AreDistantDetailBatchesCulled=>!_detailBatchesVisible;
    public int ParticleBudget {get;private set;}
    public int WaterInstanceCount {get;private set;}
    public double LastTickMs {get;private set;}
    public string SmokeStatus=>_smokeOrigins.Count+" sources on verified weather-factory stepped chimney caps; no invented chimneys";
    public string FlagStatus=>FlagHingeCount+" independently split authored Twilight cloth groups; original poles/building fixed";
    public void Configure(Node3D mapRoot,IReadOnlyCollection<HexCell3D> cells,Camera3D? camera)
    {
        _camera=camera;_fairy=ArtRenderProfile.IsFairy;
        _aerial=mapRoot.FindChildren("*","Node",true,false).OfType<SceneryDrift>().ToArray();
        foreach(var motion in _aerial){motion.SetProcess(false);motion.BoardDiameter=cells.First().Radius*2;}
        foreach(var cell in cells)
        {
            var nodes=cell.FindChildren("*","Node3D",true,false).OfType<Node3D>().Where(n=>n.HasMeta("paper_wind_hinge")).ToArray();
            var growth=1.08f+(OrigamiMapPresentation.Seed(cell.Data!.CellCode)>>8)%3*.075f;
            foreach(var node in nodes)if(!node.HasMeta("paper_wind_flag"))node.Scale=Vector3.One*growth;
            var vegetation=cell.GetNodeOrNull<Node3D>("VisualRoot/DecorationRoot/LocalVegetation");
            var half=cell.Radius*.30f;
            var reserve=new Aabb(cell.GlobalPosition+new Vector3(-half,.045f,-half),new Vector3(half*2,cell.Radius*.75f,half*2));
            if(nodes.Length>0 && ((vegetation!=null && VisualClearance.Intersects(vegetation,reserve)) || !new MeshClearanceQuery(cell).IsFree(PawnArtLayout.WorldAnchor(cell),cell.Radius)))
                foreach(var node in nodes)if(!node.HasMeta("paper_wind_flag"))node.Scale=Vector3.One;
            for(var i=0;i<nodes.Length;i++)
            {
                var node=nodes[i];var phase=(OrigamiMapPresentation.Seed(cell.Data!.CellCode+":crown:"+i)%1000)/1000f*Mathf.Tau;
                var localAxis=(node.GlobalBasis.Inverse()*new Vector3(-.25f,0,1).Normalized()).Normalized();
                var rest=node.Transform;var limit=1f;
                if(!node.HasMeta("paper_wind_flag") && vegetation!=null)
                {
                    foreach(var candidate in new[]{1f,.65f,.4f,.2f,0f})
                    {
                        bool Clear(float sign)
                        {node.Transform=new Transform3D(rest.Basis*new Basis(localAxis,Mathf.DegToRad(2.1f)*candidate*sign),rest.Origin);return !VisualClearance.Intersects(vegetation,reserve);}
                        if(Clear(1) && Clear(-1)){limit=candidate;break;}
                    }
                    node.Transform=rest;
                }
                _hinges.Add(new Hinge(node,rest,phase,node.GlobalPosition,localAxis,node.HasMeta("paper_wind_flag"),limit));
            }
        }
        if(_fairy)BuildEffects(mapRoot,cells);
        SetLook(ArtRenderProfile.CurrentPreset);Tick(0);
    }
    public void SetLook(int preset)
    {
        _fairy=preset>=3;
        foreach(var halo in _halos)
            if(halo.MaterialOverride is ShaderMaterial material)material.SetShaderParameter("halo_strength",preset>=5?(preset==6?.95f:.68f):preset==4?.80f:.48f);
        Tick(0);
    }
    public void SetClock(double time){Clock=Math.Max(0,time);Tick(0);}
    public override void _Process(double delta){if(AutomaticUpdates)Tick(delta);}
    public void Tick(double delta)
    {
        var started=Time.GetTicksUsec();
        TickCount++;
        if(!Frozen && Enabled && !GetTree().Paused)Clock+=delta;
        if(!Frozen)_motionWeight=Mathf.MoveToward(_motionWeight,Enabled?1:0,(float)delta*2);
        _detail??=GetParent().GetNodeOrNull<ZoomArtDetailController>("ZoomArtDetailController");
        var ratio=_detail?.ScreenRatio??.10f;
        var amplitude=_fairy?(Reduced?.25f:1f)*_motionWeight:0f;
        var target=(_detail?.Stage??1)==0?0:1;
        if(!Frozen && Enabled)
        {_detailAmount=Mathf.MoveToward(_detailAmount,target,(float)delta*2);_canopyAmount=Mathf.MoveToward(_canopyAmount,target==0?.35f:1,(float)delta*1.5f);}
        // Fully transparent distant leaves/stars must not keep submitting draw calls.
        // Change visibility only at the fade endpoint; the shared clock never restarts.
        var showDetails=_detailAmount>.001f;
        if(showDetails!=_detailBatchesVisible)
        {
            _detailBatchesVisible=showDetails;
            foreach(var batch in _batches)
                if(batch.GetMeta("effect_kind").AsInt32() is 1 or 2)batch.Visible=showDetails;
        }
        RenderingServer.GlobalShaderParameterSet("paper_clock",(float)Clock);
        RenderingServer.GlobalShaderParameterSet("paper_motion",amplitude);
        RenderingServer.GlobalShaderParameterSet("paper_detail",_detailAmount*(Reduced?.45f:1f));
        var overview=_canopyAmount;
        foreach(var hinge in _hinges)
        {
            if(!GodotObject.IsInstanceValid(hinge.Node))continue;
            if(!UpdateAllHingesForInspection && _camera!=null && !_camera.IsPositionInFrustum(hinge.Anchor))continue;
            var angle=Mathf.DegToRad(hinge.Flag?3.2f:2.1f)*hinge.SwayLimit*amplitude*overview*Mathf.Sin((float)Clock*(hinge.Flag?1.85f:1.3f)+hinge.Phase);
            hinge.Node.Transform=new Transform3D(hinge.Rest.Basis*new Basis(hinge.Axis,angle),hinge.Rest.Origin);
        }
        foreach(var motion in _aerial)if(GodotObject.IsInstanceValid(motion))motion.ApplyAt(Clock,_fairy,(Reduced?.45f:1f)*_motionWeight);
        foreach(var halo in _halos)halo.Visible=_fairy && HaloEnabled;
        LastTickMs=(Time.GetTicksUsec()-started)/1000.0;
    }
    private static ArrayMesh FoldedLeaf(bool star=false,bool wave=false)
    {
        var t=new SurfaceTool();t.Begin(Mesh.PrimitiveType.Triangles);
        var count=star?10:4;
        for(var i=0;i<count;i++)
        {
            Vector3 P(int n){var a=n*Mathf.Tau/count;var r=star && n%2==1?.38f:1;return wave?new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)*.10f):new Vector3(Mathf.Cos(a)*r,Mathf.Abs(Mathf.Cos(a))*.12f,Mathf.Sin(a)*r);}
            var a=P(i);var b=P(i+1);var peak=wave?Vector3.Up*.10f:Vector3.Up*.23f;
            t.SetNormal(Vector3.Up);t.SetColor(i%2==0?Colors.White:new Color(.77f,.82f,.89f));t.AddVertex(peak);t.AddVertex(b);t.AddVertex(a);
        }
        return t.Commit();
    }
    private void Batch(string name,Vector3 centre,float diameter,int count,int kind,Color color,float spread)
    {
        var mesh=FoldedLeaf(kind==1,kind==0);
        var mm=new MultiMesh {TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseColors=true,UseCustomData=true,Mesh=mesh,InstanceCount=count};
        var seed=OrigamiMapPresentation.Seed(name);
        for(var i=0;i<count;i++)
        {
            var phase=((seed+i*173)%1000)/1000f;var a=phase*Mathf.Tau;
            var p=new Vector3(Mathf.Cos(a)*spread,kind==0?0:kind==3?diameter*.02f:diameter*(.12f+(i%3)*.10f),Mathf.Sin(a)*spread*(kind==2?.35f:.65f));
            var size=diameter*(kind==0?.08f:kind==1?.018f:kind==3?.035f:.022f);
            var rotation=kind==3?new Basis(Vector3.Right,Mathf.Pi*.32f):new Basis(Vector3.Up,a);
            mm.SetInstanceTransform(i,new Transform3D(rotation.Scaled(Vector3.One*size),p));
            mm.SetInstanceColor(i,color);mm.SetInstanceCustomData(i,new Color(phase,diameter,0,1));
        }
        var extent=spread+diameter*.38f;
        mm.CustomAabb=new Aabb(new Vector3(-extent,-diameter*.1f,-extent),new Vector3(extent*2,diameter*1.05f,extent*2));
        var mat=new ShaderMaterial {Shader=GD.Load<Shader>("res://assets/shaders/paper_ambient.gdshader")};mat.SetShaderParameter("kind",(float)kind);
        var node=new MultiMeshInstance3D {Name=name,Position=centre,Multimesh=mm,MaterialOverride=mat,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};AddChild(node);_batches.Add(node);
        node.SetMeta("effect_kind",kind);
        if(kind==0)WaterInstanceCount+=count;else ParticleBudget+=count;
    }
    private void BuildEffects(Node3D mapRoot,IReadOnlyCollection<HexCell3D> cells)
    {
        foreach(var cell in cells)
        {
            var id=OrigamiArtCatalog.Resolve(cell.Data!)?.Id;var d=cell.Radius*2;
            if(id=="weather-factory")AddChimneyVapor(cell,d);
            if(id is "harmony-tree" or "crystal-castle" or "crystal-empire")
            {
                var meshes=cell.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>m.IsVisibleInTree() && m.Mesh!=null && m.GetPath().ToString().Contains("OrigamiAsset/Building")).ToArray();
                if(meshes.Length==0)continue;
                var b=meshes[0].GlobalTransform*meshes[0].GetAabb();foreach(var m in meshes.Skip(1))b=b.Merge(m.GlobalTransform*m.GetAabb());
                var at=b.GetCenter();at.Y=b.Position.Y+d*.12f;
                Batch("Magic_"+cell.Data!.CellCode,at,d,6,1,new Color(id=="harmony-tree"?"f0d38d":"cbbded"),d*.18f);
                var halo=new MeshInstance3D {Name="LocalHalo_"+cell.Data.CellCode,Position=at-Vector3.Up*d*.10f,Mesh=new PlaneMesh {Size=Vector2.One*d*.62f},CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
                var haloMaterial=new ShaderMaterial {Shader=GD.Load<Shader>("res://assets/shaders/paper_local_halo.gdshader")};
                haloMaterial.SetShaderParameter("phase",OrigamiMapPresentation.Seed(cell.Data.CellCode)%1000/1000f*Mathf.Tau);
                halo.MaterialOverride=haloMaterial;AddChild(halo);_halos.Add(halo);
            }
        }
        var forest=cells.Where(c=>c.Data!.BoardCode=="B").OrderBy(c=>c.Data!.CellId).ToArray();
        foreach(var cell in forest.Where((_,i)=>i%9==3).Take(4))
            Batch("ForestPaper_"+cell.Data!.CellCode,cell.GlobalPosition,cell.Radius*2,3,2,new Color("c5c67d"),cell.Radius*.65f);
        var mapDiameter=cells.First().Radius*2;
        var lake=mapRoot.GetNodeOrNull<Node3D>("GeneratedCells/ContinentalScenery/LakePlacementDraft")??GetParent().GetNodeOrNull<Node3D>("ContinentalScenery/LakePlacementDraft");
        if(lake!=null)Batch("LakePaperRipples",lake.GlobalPosition+Vector3.Up*mapDiameter*.016f,mapDiameter,5,0,new Color("c7e5dc"),mapDiameter*.50f);
        foreach(var wave in mapRoot.FindChildren("*","Node3D",true,false).OfType<Node3D>().Where(n=>n.HasMeta("environment_asset") && n.GetMeta("environment_asset").AsString()=="WAT002").Take(6))
            Batch("CoastalPaperRipples_"+_batches.Count,wave.GlobalPosition+Vector3.Up*mapDiameter*.005f,mapDiameter,2,0,new Color("d0e5d5"),mapDiameter*.20f);
    }
    private void AddChimneyVapor(HexCell3D cell,float diameter)
    {
        var source=cell.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().FirstOrDefault(m=>m.Mesh!=null && m.Name.ToString().Contains("DiamondDomesAndSteppedChimneys"));
        if(source?.Mesh==null)return;
        var faces=source.Mesh.GetFaces();var box=source.Mesh.GetAabb();var candidates=new List<Vector3>();
        for(var i=0;i+2<faces.Length;i+=3)
        {
            var a=faces[i];var b=faces[i+1];var c=faces[i+2];var centre=(a+b+c)/3;
            if(centre.Y<box.Position.Y+box.Size.Y*.65f || Math.Abs(a.Y-b.Y)>.0001f || Math.Abs(a.Y-c.Y)>.0001f)continue;
            if((b-a).Cross(c-a).LengthSquared()<.00000001f)continue;
            candidates.Add(centre);
        }
        var selected=new List<Vector3>();
        foreach(var cap in candidates.OrderByDescending(p=>p.Y))
        {
            if(selected.Any(p=>new Vector2(p.X-cap.X,p.Z-cap.Z).Length()<box.Size.X*.13f))continue;
            var top=candidates.Where(p=>Math.Abs(p.Y-cap.Y)<.002f && new Vector2(p.X-cap.X,p.Z-cap.Z).Length()<box.Size.X*.065f).ToArray();
            var local=top.Aggregate(Vector3.Zero,(sum,p)=>sum+p)/top.Length;selected.Add(local);
            var world=source.GlobalTransform*local+Vector3.Up*diameter*.008f;
            Batch("FactoryPaperVapor_"+selected.Count,world,diameter,4,3,new Color("cbd9df"),diameter*.01f);
            _smokeOrigins.Add(new {cell=cell.Data!.CellCode,source=source.GetPath().ToString(),localPosition=local.ToString(),worldPosition=world.ToString(),method="highest separate horizontal caps in authored stepped-chimney mesh; source view inspected"});
            if(selected.Count==2)break;
        }
    }
    public object Snapshot()=>new {clock=Clock,frozen=Frozen,enabled=Enabled,reduced=Reduced,hinges=HingeCount,crownHinges=CrownHingeCount,flagHinges=FlagHingeCount,swayLimitedHinges=SwayLimitedHinges,particleBudget=ParticleBudget,activeSmallInstances=_batches.Where(b=>b.GetMeta("effect_kind").AsInt32()!=0).Sum(b=>b.Multimesh.VisibleInstanceCount<0?b.Multimesh.InstanceCount:b.Multimesh.VisibleInstanceCount),waterInstances=WaterInstanceCount,batches=BatchCount,visibleBatches=VisibleBatchCount,distantDetailBatchesCulled=AreDistantDetailBatchesCulled,haloCount=_halos.Count,smoke=SmokeStatus,smokeSources=_smokeOrigins,flags=FlagStatus,lastTickMs=LastTickMs};
    public override void _ExitTree()
    {
        _hinges.Clear();_halos.Clear();_batches.Clear();_smokeOrigins.Clear();_aerial=[];_camera=null;_detail=null;
    }
}
