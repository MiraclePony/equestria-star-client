using System;
using System.Linq;
using System.Text.Json;
using Godot;

namespace EquestriaStar.Game.Map;

public sealed class ZoomDetailSettings
{
    public float[] Thresholds {get;set;}=[.06f,.16f,.35f];
    public float Hysteresis {get;set;}=.10f;
    public float PrefetchRatio {get;set;}=.12f;
    public float PrefetchMargin {get;set;}=.15f;
    public float FadeSeconds {get;set;}=.30f;
    public double CacheGraceSeconds {get;set;}=8;
    public int MaxConcurrentLoads {get;set;}=2;
    public int MaxCachedResources {get;set;}=32;
    public int MaxNearCells {get;set;}=12;
    public int MaxRetainedCells {get;set;}=32;
    public int MaxInstancesPerFrame {get;set;}=1;
    public int MaxReleasesPerFrame {get;set;}=2;
    public double InstantiateBudgetMs {get;set;}=2;
    public static ZoomDetailSettings Load()
    {
        const string path="res://assets/art/zoom-detail.json";
        var data=FileAccess.FileExists(path)?JsonSerializer.Deserialize<ZoomDetailSettings>(FileAccess.GetFileAsString(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}):null;
        data??=new();
        if(data.Thresholds.Length!=3 || data.Thresholds.Any(x=>x<=0) || !data.Thresholds.SequenceEqual(data.Thresholds.OrderBy(x=>x)))throw new InvalidOperationException("Invalid zoom thresholds");
        data.Hysteresis=Math.Clamp(data.Hysteresis,0,.3f);data.FadeSeconds=Math.Max(.01f,data.FadeSeconds);
        data.MaxInstancesPerFrame=Math.Max(1,data.MaxInstancesPerFrame);data.MaxConcurrentLoads=Math.Max(1,data.MaxConcurrentLoads);
        data.MaxReleasesPerFrame=Math.Max(1,data.MaxReleasesPerFrame);data.MaxNearCells=Math.Max(1,data.MaxNearCells);data.MaxRetainedCells=Math.Max(1,data.MaxRetainedCells);
        return data;
    }
}

public sealed class ScreenDetailPolicy(ZoomDetailSettings settings)
{
    public int Stage {get;private set;}
    public int Update(float ratio)
    {
        while(Stage<3 && ratio>settings.Thresholds[Stage]*(1+settings.Hysteresis))Stage++;
        while(Stage>0 && ratio<settings.Thresholds[Stage-1]*(1-settings.Hysteresis))Stage--;
        return Stage;
    }
    public static Rect2 GroundRect(Camera3D camera,Vector3 centre,float radius)
    {
        var low=new Vector2(float.MaxValue,float.MaxValue);var high=-low;
        for(var i=0;i<6;i++)
        {
            var a=Mathf.DegToRad(i*60+30);var world=centre+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
            var p=camera.UnprojectPosition(world);low=low.Min(p);high=high.Max(p);
        }
        return new Rect2(low,high-low);
    }
    public static float Ratio(Camera3D camera,Vector3 centre,float radius,Rect2 effectiveViewport)=>
        camera.IsPositionBehind(centre)?0:GroundRect(camera,centre,radius).Size.Y/Math.Max(1,effectiveViewport.Size.Y);
}
