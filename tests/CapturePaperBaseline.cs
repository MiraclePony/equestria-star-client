using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Art;
using EquestriaStar.Game.Map;

public partial class CapturePaperBaseline : SceneTree
{
    public override void _Initialize()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Frames(int count){for(var i=0;i<count;i++)await ToSignal(this,SignalName.ProcessFrame);}
    private async Task Run()
    {
        var exit=0;Node3D? gallery=null;
        try
        {
            Root.ContentScaleSize=new Vector2I(1760,1000);
            gallery=GD.Load<PackedScene>("res://scenes/art/OrigamiGallery.tscn").Instantiate<Node3D>();Root.AddChild(gallery);gallery.SetProcess(false);
            var stage=gallery.GetChildren().OfType<GameMapScene>().Single();var detail=stage.DetailController!;
            foreach(var motion in gallery.FindChildren("*","Node",true,false).OfType<SceneryDrift>())motion.SetProcess(false);
            var inspector=gallery.GetChildren().OfType<CanvasLayer>().Single(c=>c.Layer==30).GetChild<Control>(0);
            var camera=stage.CameraController.Camera;
            RenderingServer.ViewportSetMeasureRenderTime(Root.GetViewportRid(),true);
            var cases=new List<object>();var ids=detail.Cells.ToDictionary(c=>c.Data!.CellCode);
            foreach(var (name,cell,ratio) in new[]{("overview",ids["A1"],.04f),("region",ids["B3"],.10f),("close",detail.Cells.Single(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id=="fashion-shop"),.23f)})
            {
                inspector.Visible=false;detail.ViewInsets=Vector4.Zero;
                var actual=ScreenDetailPolicy.Ratio(camera,cell.GlobalPosition,cell.Radius,detail.EffectiveViewport);
                stage.CameraController.FocusGround(cell.GlobalPosition,camera.Size*actual/ratio,true);await Frames(90);
                var cpu=new List<double>();var gpu=new List<double>();var wall=new List<double>();
                for(var i=0;i<90;i++)
                {var before=Time.GetTicksUsec();await Frames(1);wall.Add((Time.GetTicksUsec()-before)/1000.0);cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(Root.GetViewportRid()));var g=RenderingServer.ViewportGetMeasuredRenderTimeGpu(Root.GetViewportRid());if(g>0)gpu.Add(g);}
                foreach(var ui in new[]{false,true})
                {inspector.Visible=ui;await Frames(2);await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var shot=Root.GetTexture().GetImage();shot.SavePng($"res://artifacts/paper-v3-baseline-{name}-{(ui?"ui":"clean")}.png");}
                double P(List<double> xs,double p)=>xs.Count==0?0:xs.OrderBy(x=>x).ElementAt(Math.Min(xs.Count-1,(int)(xs.Count*p)));
                cases.Add(new {name,cell=cell.Data!.CellCode,ratio,cameraSize=camera.Size,cameraTransform=camera.GlobalTransform.ToString(),cpuP95=P(cpu,.95),cpuP99=P(cpu,.99),gpuP95=P(gpu,.95),gpuP99=P(gpu,.99),frameP95=P(wall,.95),frameP99=P(wall,.99),draws=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),memory=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed)});
            }
            PaperSceneAudit.Save(gallery,"res://artifacts/paper-v3-baseline-audit.json");
            using(var report=FileAccess.Open("res://artifacts/paper-v3-baseline.json",FileAccess.ModeFlags.Write))report.StoreString(JsonSerializer.Serialize(new {engine=Engine.GetVersionInfo().ToString(),renderer=RenderingServer.GetCurrentRenderingMethod(),device=RenderingServer.GetVideoAdapterName(),cases},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("PAPER_BASELINE_PASS 3 cameras / 6 captures");
        }
        catch(Exception e){GD.PushError(e.ToString());exit=1;}
        gallery?.Free();await Frames(2);ConceptPaperStyle.ReleaseAtShutdown();GC.Collect();GC.WaitForPendingFinalizers();Quit(exit);
    }
}
