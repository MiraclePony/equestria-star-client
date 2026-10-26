using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

public partial class RunEnvironmentArtTests : SceneTree
{
    private readonly List<string> _failures=[];
    private void Check(bool condition,string message){if(!condition)_failures.Add(message);}
    public override void _Initialize()=>Callable.From(Run).CallDeferred();
    private void Run()
    {
        try
        {
            CheckClearanceGeometry();
            Check(EnvironmentArtCatalog.Entries.Count>=51,"Current stable environment deliveries are missing");
            var root=new Node3D();Root.AddChild(root);
            foreach(var id in EnvironmentArtCatalog.Entries.Keys)
            {
                var instance=EnvironmentArtCatalog.Place(root,id,Vector3.Zero,.55f,0);
                Check(instance!=null,id+": cannot load actual split model");
                if(instance==null)continue;
                Check(instance.FindChildren("*","CollisionObject3D",true,false).Count==0,id+": environment must not intercept picking");
                instance.Free();
            }
            using var document=JsonDocument.Parse(FileAccess.GetFileAsString("res://tests/fixtures/main-map-20261024.json"));
            var map=JsonSerializer.Deserialize<GameMapDto>(document.RootElement.GetProperty("data").GetRawText(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
            var cellScene=GD.Load<PackedScene>("res://scenes/game/map/HexCell3D.tscn");
            var builder=new GameMapBuilder();
            var baseline=builder.Build(map,root,cellScene,.55f,.11f,.025f,false,true,false);
            var basePositions=builder.CellsById.ToDictionary(p=>p.Key,p=>p.Value.Position);
            var baselineShape=builder.CellsByCode["A13"].InteractionShape;
            var enhanced=builder.Build(map,root,cellScene,.55f,.11f,.025f,false);
            Check(enhanced.Ok && enhanced.CellCount==138 && enhanced.EdgeCount==map.Edges!.Count,"Art changed gameplay topology");
            Check(enhanced.Bounds.Size.X>baseline.Bounds.Size.X,"Scenery excluded from camera fitting");
            var newDecorated=0;
            foreach(var pair in builder.CellsById)
            {
                var cell=pair.Value;
                Check(cell.Position.IsEqualApprox(basePositions[pair.Key]),cell.Name+": art moved logical coordinate");
                Check(cell.Rotation.IsEqualApprox(Vector3.Zero),cell.Name+": art rotated interaction/collision root");
                var vegetation=cell.GetNodeOrNull<Node3D>("VisualRoot/DecorationRoot/LocalVegetation");
                if(vegetation==null)continue;
                newDecorated++;
                var r=cell.Radius*.30f;
                var reserve=new Aabb(cell.Position+new Vector3(-r,.045f,-r),new Vector3(r*2,cell.Radius*.75f,r*2));
                Check(!TokenClearancePreview.Overlaps(vegetation,reserve),cell.Name+": new foliage intrudes into centre clearance");
            }
            Check(newDecorated>80,"Ordinary map remains undecorated");
            var forest=builder.CellsById.Values.Where(c=>c.Data!.BoardCode=="B" && OrigamiArtCatalog.Resolve(c.Data)?.Id=="everfree").ToArray();
            Check(forest.Select(c=>c.CustomVisualYawDegrees).Distinct().Count()>2,"Forest orientations repeat uniformly");
            Check(forest.Select(c=>c.GetNode("VisualRoot/DecorationRoot/LocalVegetation").GetChildCount()).Distinct().Count()>1,"Forest density repeats uniformly");
            var trees=forest.SelectMany(c=>c.GetNode("VisualRoot/DecorationRoot/LocalVegetation").GetChildren().OfType<Node3D>()).Where(n=>n.HasMeta("zoom_main_tree")).ToArray();
            Check(trees.Length>=forest.Length*2,"Everfree has fewer than two fixed main trees per ordinary forest cell");
            Check(trees.All(n=>n.GetMeta("environment_asset").AsString() is "VEG006" or "VEG007" or "VEG008" or "VEG009"),"Generic pines used in Everfree instead of dedicated trees");
            Check(trees.Select(n=>n.GetMeta("environment_asset").AsString()).Distinct().Count()==4,"Everfree dedicated tree variants missing");
            GD.Print($"FOREST_TEST cells={forest.Length} dedicatedMainTrees={trees.Length} variants=4 singleCompleteMesh=true");
            Check(ReferenceEquals(builder.CellsByCode["A13"].InteractionShape,baselineShape),"Visual rotation changed picking geometry");
            foreach(var station in new[]{"A15","E8","F4"})Check(builder.CellsByCode[station].CustomVisualYawDegrees==0,station+": station port orientation guessed");
            var picked=false;
            builder.CellsByCode["A13"].CellClicked+=id=>picked=id==builder.CellsByCode["A13"].Data!.CellId;
            builder.CellsByCode["A13"].TryActivate();
            Check(picked,"Scenery broke click identity");
            var scenery=root.GetNode<Node3D>("GeneratedCells/ContinentalScenery");
            Check(scenery.FindChildren("*","CollisionObject3D",true,false).Count==0,"Background has gameplay collision");
            Check(scenery.HasNode("LakePlacementDraft"),"Lake proposal missing");
            Check(!root.HasNode("RailStyleSample_NotRoute"),"Unverified rail route enabled in live layout");
            WaterModulePreview.Add(root);
            var water=root.GetNode<Node3D>("WaterPortSample_NotWorldRiver");
            var curve=water.GetNode<Node3D>("WAT006");var straight=water.GetNode<Node3D>("WAT005");
            var from=EnvironmentArtCatalog.Entries["WAT006"].Ports.Single(p=>p.Id=="B");
            var to=EnvironmentArtCatalog.Entries["WAT005"].Ports.Single(p=>p.Id=="A");
            Check((curve.Transform*from.PositionGodot).DistanceTo(straight.Transform*to.PositionGodot)<.0001f,"Water module endpoints or surface heights do not meet");
            Check((curve.Basis*from.DirectionGodot).Normalized().Dot((straight.Basis*to.DirectionGodot).Normalized())<-.9999f,"Water port normals are not opposed");
            var firstVegetation=builder.CellsByCode["A1"].GetNode<Node3D>("VisualRoot/DecorationRoot/LocalVegetation").GetChild<Node3D>(0).Transform;
            builder.Build(map,root,cellScene,.55f,.11f,.025f,false);
            Check(builder.CellsByCode["A1"].GetNode<Node3D>("VisualRoot/DecorationRoot/LocalVegetation").GetChild<Node3D>(0).Transform.IsEqualApprox(firstVegetation),"Rebuild changes random placement");
            var proxy=new TokenClearancePreview();root.AddChild(proxy);proxy.Build(builder.CellsById,builder.Edges);
            Check(proxy.FlaggedCells==0,"Fitted token anchors intersect current map geometry");
            Check(proxy.PathPoints.Count>=2 && proxy.SweepWarnings.Count==0,"Preview movement still intersects actual map geometry");
            proxy.SaveReport("res://artifacts/token-clearance.json");
            GD.Print($"ENVIRONMENT_TEST assets={EnvironmentArtCatalog.Entries.Count} decoratedCells={newDecorated} clearanceWarnings={proxy.FlaggedCells} sweepWarnings={proxy.SweepWarnings.Count}");
            root.Free();
            var gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<OrigamiGallery>();
            Root.AddChild(gallery);
            var stage=gallery.GetChildren().OfType<GameMapScene>().Single();
            var chosen=stage.GetNode<HexCell3D>("MapRoot/GeneratedCells/A2");
            var initialYaw=chosen.CustomVisualYawDegrees;
            var identity=chosen.Data!.CellId;
            chosen.TryActivate();
            var rotate=gallery.FindChildren("*","Button",true,false).OfType<Button>().Single(b=>b.Text=="右转60°");
            rotate.EmitSignal(Button.SignalName.Pressed);
            Check(chosen.CustomVisualYawDegrees==initialYaw+60,"Gallery rotation control does not update selected landmark");
            Check(Math.Abs(chosen.GetNode<Node3D>("VisualRoot/CustomVisualRoot").GetChild<Node3D>(0).RotationDegrees.Y-30-initialYaw-60)<.01f,"Rotation control did not turn actual visual");
            Check(chosen.Data.CellId==identity && chosen.Rotation.IsEqualApprox(Vector3.Zero),"Rotation control changed gameplay identity or root");
            gallery.Free();
        }
        catch(Exception exception){_failures.Add(exception.ToString());}
        foreach(var failure in _failures)GD.PushError(failure);
        GD.Print(_failures.Count==0?"Environment art tests passed.":$"Environment art tests failed: {_failures.Count}");
        Quit(_failures.Count==0?0:1);
    }
    private void CheckClearanceGeometry()
    {
        var root=new Node3D();Root.AddChild(root);
        var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach(var p in new[]{new Vector3(-1,.5f,0),new Vector3(1,.5f,0),new Vector3(0,.5f,-1),new Vector3(0,.5f,1)})
            surface.AppendFrom(new BoxMesh {Size=p.X==0?new Vector3(2,1,.15f):new Vector3(.15f,1,2)},0,new Transform3D(Basis.Identity,p));
        root.AddChild(new MeshInstance3D {Mesh=surface.Commit()});
        var query=new MeshClearanceQuery(root);
        Check(query.IsFree(Vector3.Zero,1),"Triangle clearance incorrectly fills an open courtyard's AABB");
        Check(!query.IsFree(new Vector3(.95f,0,0),1),"Triangle clearance misses an actual wall");
        root.AddChild(new MeshInstance3D {Position=new Vector3(0,1.2f,0),Mesh=new BoxMesh {Size=new Vector3(2,.1f,2)}});
        Check(!new MeshClearanceQuery(root).IsFree(Vector3.Zero,1),"Roof ray permits landing inside a closed roof");
        root.Free();
        root=new Node3D();Root.AddChild(root);
        root.AddChild(new MeshInstance3D {Position=Vector3.Up*.5f,Mesh=new BoxMesh {Size=new Vector3(.35f,1,.35f)}});
        query=new MeshClearanceQuery(root);
        var path=TokenPreviewPath.Plan(query,new Vector3(-.8f,0,0),new Vector3(.8f,0,0),1);
        Check(path.Count>2,"Movement does not route around the obstructing wall");
        Check(path.Count>0 && Enumerable.Range(0,101).All(i=>query.IsFree(TokenPreviewPath.Sample(path,i/100f),1)),"Movement detour clips actual obstacle surfaces");
        root.Free();
    }
}
