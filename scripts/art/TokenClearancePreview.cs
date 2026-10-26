using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

/// <summary>Inspection proxy, not a gameplay token. Tests actual triangle surfaces and overhead roofs.</summary>
public partial class TokenClearancePreview : Node3D
{
    public int FlaggedCells { get; private set; }
    public List<string> FlaggedCodes { get; } = [];
    public List<float> SweepWarnings { get; } = [];
    private string _previewEdge = "";
    private Node3D? _probe;
    private Vector3 _from, _to;
    public List<Vector3> PathPoints {get;private set;}=[];
    private float _radius;
    private double _time;
    public void Build(IReadOnlyDictionary<long,HexCell3D> cells,IReadOnlyList<MapEdgeDto> edges)
    {
        var query=new MeshClearanceQuery(cells.Values.First().GetParent());
        foreach(var cell in cells.Values)
        {
            var halfWidth=cell.Radius*.30f;
            // Body clearance excludes low decorative ground cover; feet require mesh-level review.
            var feet=PawnArtLayout.WorldAnchor(cell);
            var blocked=!query.IsFree(feet,cell.Radius);
            if(blocked){FlaggedCells++;FlaggedCodes.Add(cell.Data!.CellCode);}
            var color=blocked?new Color("d76e65"):new Color("419a88");
            var marker=new MeshInstance3D {Name="Clearance_"+cell.Data!.CellCode,Position=feet+Vector3.Up*.04f,
                Mesh=new TorusMesh {InnerRadius=halfWidth*.9f,OuterRadius=halfWidth, Rings=24,RingSegments=6},
                MaterialOverride=new StandardMaterial3D {AlbedoColor=color,ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,
                    NoDepthTest=true},CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            AddChild(marker);
        }
        // Only animate over an actual NORMAL edge in the supplied map snapshot.
        var edge=edges.FirstOrDefault(e=>e.EdgeType=="NORMAL" && cells.ContainsKey(e.FromCellId) && cells.ContainsKey(e.ToCellId)
            && cells[e.FromCellId].Data!.BoardCode=="A" && cells[e.ToCellId].Data!.BoardCode=="A");
        if(edge==null)return;
        _from=PawnArtLayout.WorldAnchor(cells[edge.FromCellId]);_to=PawnArtLayout.WorldAnchor(cells[edge.ToCellId]);
        _previewEdge=cells[edge.FromCellId].Data!.CellCode+" → "+cells[edge.ToCellId].Data!.CellCode;
        var radius=cells[edge.FromCellId].Radius;
        _radius=radius;
        var width=radius*.22f;
        PathPoints=TokenPreviewPath.Plan(query,_from,_to,radius);
        if(PathPoints.Count==0){SweepWarnings.Add(-1);return;}
        for(var i=0;i<=24;i++)
        {
            var point=TokenPreviewPath.Sample(PathPoints,i/24f);
            if(!query.IsFree(point,radius))SweepWarnings.Add(i/24f);
        }
        _probe=new MeshInstance3D {Name="MovementSizeProbe",Mesh=new CapsuleMesh {Radius=radius*.22f,Height=radius*.72f},
            MaterialOverride=OrigamiMapPresentation.PaperMaterial(new Color("f8d48b"))};
        AddChild(_probe);
    }
    public static bool Overlaps(Node node,Aabb volume)=>VisualClearance.Intersects(node,volume);
    public void SaveReport(string path)
    {
        using var file=FileAccess.Open(path,FileAccess.ModeFlags.Write);
        file.StoreString(JsonSerializer.Serialize(new {status="triangle_box_and_roof_ray_provisional_envelope",bodyHalfWidthRatio=.22,
            heightRatio=.72,bodyFloorRatio=.12,flaggedCells=FlaggedCodes,previewEdge=_previewEdge,sweepWarnings=SweepWarnings,
            path=PathPoints.Select(p=>new[]{p.X,p.Y,p.Z}),
            note="Exact surface triangles and upward centre roof ray for a provisional body box. Not final capsule physics, multiplayer parking or ground/bridge validation."},new JsonSerializerOptions{WriteIndented=true}));
    }
    public override void _Process(double delta)
    {
        if(_probe==null)return;
        _time+=delta;
        var t=(1-Mathf.Cos((float)_time*1.2f))*.5f;
        _probe.Position=TokenPreviewPath.Sample(PathPoints,t)+Vector3.Up*(_radius*.48f);
    }
}
