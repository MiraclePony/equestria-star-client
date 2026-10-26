using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.Game.Map;

/// <summary>Explicit offline fitting tool. Writes local art config; does not edit source GLBs.</summary>
public partial class GeneratePawnArtLayout : SceneTree
{
    public override void _Initialize()=>Callable.From(Run).CallDeferred();
    private static IEnumerable<Vector3> Candidates(float radius)
    {
        yield return Vector3.Zero;
        foreach(var ring in new[]{.22f,.36f,.48f,.54f})for(var i=0;i<16;i++)
        {
            var angle=i*Mathf.Tau/16;var p=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*radius*ring;
            var inside=true;
            for(var k=0;k<6;k++)
            {
                var a=k*Mathf.Tau/6;var n=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                if(n.Dot(p)+radius*.22f*(Math.Abs(n.X)+Math.Abs(n.Z))>radius*(Mathf.Sqrt(3)/2-.012f)){inside=false;break;}
            }
            if(inside)yield return p;
        }
    }
    private void Run()
    {
        try
        {
            using var doc=JsonDocument.Parse(FileAccess.GetFileAsString("res://tests/fixtures/main-map-20261024.json"));
            var map=JsonSerializer.Deserialize<GameMapDto>(doc.RootElement.GetProperty("data").GetRawText(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            var root=new Node3D();Root.AddChild(root);var builder=new GameMapBuilder();
            var result=builder.Build(map,root,GD.Load<PackedScene>("res://scenes/game/map/HexCell3D.tscn"),.55f,.11f,.025f,false,true,true,false);
            if(!result.Ok)throw new InvalidOperationException(result.Error);
            var catalog=new PawnArtLayout.Catalog();var failed=new List<string>();var beforeBlocked=0;var fits=new List<object>();
            foreach(var cell in builder.CellsById.Values)
            {
                var entry=OrigamiArtCatalog.Resolve(cell.Data!);var r=cell.Radius;
                var query=new MeshClearanceQuery(cell.GetNode("VisualRoot"));
                if(!query.IsFree(cell.GlobalPosition,r))beforeBlocked++;
                var chosen=Vector3.Zero;var found=false;var chosenScale=1f;var chosenOffset=Vector3.Zero;
                var landmark=entry is {Id:not "everfree"};
                var scales=landmark?(entry!.Id=="sugarcube"?new[]{.72f,.66f,.60f}:new[]{1f,.94f,.88f,.82f,.76f,.70f,.64f,.58f,.52f,.46f}):new[]{1f};
                foreach(var scale in scales)
                {
                    foreach(var shift in landmark?new[]{0f,-.10f}:new[]{0f})
                    {
                        var visual=cell.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset");
                        var offset=visual==null?Vector3.Zero:visual.Basis.Inverse()*(new Vector3(0,0,shift)*r);
                        if(landmark)PawnArtLayout.TransformContent(cell,scale,offset);
                        query=new MeshClearanceQuery(cell.GetNode("VisualRoot"));
                        foreach(var point in Candidates(r))
                            if(query.IsFree(cell.ToGlobal(point),r)){chosen=point;found=true;chosenScale=scale;chosenOffset=offset;break;}
                        if(found)break;
                    }
                    if(found)break;
                }
                if(!found){failed.Add(cell.Data!.CellCode);continue;}
                if(landmark)
                {
                    var visual=cell.GetNode<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset");
                    catalog.Assets[entry!.Id]=new PawnArtLayout.Profile {Fingerprint=PawnArtLayout.Fingerprint(entry),ContentScale=chosenScale,
                        ContentOffsetArt=PawnArtLayout.Array(chosenOffset),AnchorArt=PawnArtLayout.Array(visual.Basis.Inverse()*chosen)};
                }
                else catalog.CellAnchors[cell.Data!.CellCode]=PawnArtLayout.Array(chosen/r);
                cell.GetNode<Node3D>("TokenRoot").Position=chosen;
                fits.Add(new {cell=cell.Data!.CellCode,asset=entry?.Id,contentScale=chosenScale,anchor=PawnArtLayout.Array(chosen/r)});
                GD.Print($"TOKEN_FIT {cell.Data!.CellCode} scale={chosenScale} anchor={chosen/r}");
            }
            var options=new JsonSerializerOptions {WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
            using(var file=FileAccess.Open("res://artifacts/token-layout-fit.json",FileAccess.ModeFlags.Write))
                file.StoreString(JsonSerializer.Serialize(new {beforeBlocked,failed,fits},options));
            if(failed.Count>0)throw new InvalidOperationException("No clear landing anchor: "+string.Join(",",failed));
            using(var file=FileAccess.Open("res://assets/art/token-layout.json",FileAccess.ModeFlags.Write))file.StoreString(JsonSerializer.Serialize(catalog,options));
            GD.Print($"TOKEN_LAYOUT anchors={fits.Count} landmarks={catalog.Assets.Count} beforeBlocked={beforeBlocked} failed={failed.Count}");
            root.Free();Quit();
        }
        catch(Exception exception){GD.PushError(exception.ToString());Quit(1);}
    }
}
