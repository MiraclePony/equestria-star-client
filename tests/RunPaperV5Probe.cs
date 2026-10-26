using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

/// <summary>V4 real Gallery automatic-update diagnostic and fixed-camera baseline.</summary>
public partial class RunPaperV5Probe : SceneTree
{
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(this,SignalName.ProcessFrame);}
    private static float[] V(Vector3 p)=>[p.X,p.Y,p.Z];
    private async Task Shot(string name){await Frames(3);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var im=Root.GetTexture().GetImage();im.SavePng("res://artifacts/paper-v5-before-"+name+".png");}
    public override void _Initialize()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        Root.ContentScaleSize=new Vector2I(1760,1000);
        var gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<OrigamiGallery>();Root.AddChild(gallery);
        var stage=gallery.GetChildren().OfType<GameMapScene>().Single();var camera=stage.CameraController.Camera;
        var detail=stage.DetailController!;var motion=gallery.EnvironmentMotion!;
        var panel=gallery.GetChildren().OfType<CanvasLayer>().Single(x=>x.Layer==30).GetChild<Control>(0);
        await Frames(60);stage.CameraController.SetProcess(false);
        var cameraCases=new List<object>();
        foreach(var (view,cellCode,ratio) in new[]{("overview","A1",.04f),("region","B3",.10f),("close","A16",.23f)})
        {
            var cell=detail.Cells.First(c=>c.Data!.CellCode==cellCode);panel.Visible=false;detail.ViewInsets=Vector4.Zero;
            var s=ScreenDetailPolicy.Ratio(camera,cell.GlobalPosition,cell.Radius,detail.EffectiveViewport);
            stage.CameraController.FocusGround(cell.GlobalPosition,camera.Size*s/ratio,true);motion.Frozen=true;motion.SetClock(2.25);
            await Frames(90);await Shot(view+"-clean");panel.Visible=true;await Shot(view+"-ui");panel.Visible=false;
            var t=camera.GlobalTransform;cameraCases.Add(new {view,size=camera.Size,origin=V(t.Origin),x=V(t.Basis.X),y=V(t.Basis.Y),z=V(t.Basis.Z)});
            if(view=="overview")
            {
                foreach(var c in detail.Cells)c.Visible=false;
                var place=stage.GetNodeOrNull<Node3D>("MapRoot/GeneratedCells/PaperPlaceComposition");if(place!=null)place.Visible=false;
                var rail=stage.GetNodeOrNull<Node3D>("MapRoot/ReferenceRailDraft");if(rail!=null)rail.Visible=false;
                await Shot("background-only");foreach(var c in detail.Cells)c.Visible=true;if(place!=null)place.Visible=true;if(rail!=null)rail.Visible=true;
            }
        }
        // Restore normal Gallery defaults and use the actual process chain, never manual Tick/SetClock.
        stage.CameraController.SetProcess(true);panel.Visible=true;
        var centre=detail.Cells.First(c=>c.Data!.CellCode=="B3");
        stage.CameraController.FocusGround(centre.GlobalPosition,7.101977f,true);stage.CameraController.SetProcess(false);
        motion.Frozen=false;motion.Enabled=true;await Frames(30);
        var drifts=gallery.FindChildren("*","Node",true,false).OfType<SceneryDrift>().ToArray();
        var aerial=(Array?)typeof(PaperEnvironmentMotion).GetField("_aerial",BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(motion);
        var balloons=drifts.Where(d=>d.GetParent().Name.ToString().Contains("Balloon")).ToArray();
        var samples=new List<object>();var cameraTransform=camera.GlobalTransform;
        for(var f=0;f<=240;f++)
        {
            if(f%4==0)foreach(var d in balloons)
            {
                var node=(Node3D)d.GetParent();var meshes=node.FindChildren("*","MeshInstance3D",true,false).OfType<MeshInstance3D>().Where(m=>m.Mesh!=null && m.IsVisibleInTree()).ToArray();
                var bounds=meshes.Length==0?new Aabb(node.GlobalPosition,Vector3.Zero):meshes[0].GlobalTransform*meshes[0].GetAabb();foreach(var m in meshes.Skip(1))bounds=bounds.Merge(m.GlobalTransform*m.GetAabb());
                var screen=camera.UnprojectPosition(bounds.GetCenter());samples.Add(new {frame=f,clock=motion.Clock,path=node.GetPath().ToString(),id=node.GetInstanceId(),viewport=node.GetViewport().GetPath().ToString(),visible=node.IsVisibleInTree(),visibleMeshes=meshes.Length,position=V(node.GlobalPosition),centre=V(bounds.GetCenter()),screen=new[]{screen.X,screen.Y},driftProcessing=d.IsProcessing(),managerProcessing=motion.IsProcessing(),motion.Frozen,motion.Enabled});
            }
            await Frames(1);
        }
        var sea=gallery.FindChildren("Sea","MeshInstance3D",true,false).OfType<MeshInstance3D>().Single();
        var report=new {cameras=cameraCases,automaticGallery=gallery.IsProcessing(),paused=Paused,registeredDrifts=aerial?.Length,foundDrifts=drifts.Length,cameraStable=camera.GlobalTransform.IsEqualApprox(cameraTransform),motion=motion.Snapshot(),samples,seaPath=sea.GetPath().ToString(),seaY=sea.GlobalPosition.Y,floorY=stage.GetNode<Node3D>("Floor").GlobalPosition.Y};
        using(var file=FileAccess.Open("res://artifacts/paper-v5-before-diagnostic.json",FileAccess.ModeFlags.Write))file.StoreString(JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        GD.Print("V5_BASELINE_REAL_GALLERY_PROBE_DONE");balloons=[];drifts=[];aerial=null;sea=null!;stage=null!;motion=null!;detail=null!;panel=null!;camera=null!;
        gallery.FinishInspection();
    }
}
