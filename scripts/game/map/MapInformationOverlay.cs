using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace EquestriaStar.Game.Map;

/// <summary>Screen-sized gameplay information. Never a child of an art detail group.</summary>
public partial class MapInformationOverlay : Control
{
    private Camera3D _camera=null!;private IReadOnlyList<HexCell3D> _cells=[];private Func<Rect2> _viewport=null!;
    public string[] Route {get;set;}=[];
    public int MarkerCount {get;private set;}
    public List<Rect2> ProtectedRegions {get;}=[];
    public void Configure(Camera3D camera,IReadOnlyList<HexCell3D> cells,Func<Rect2> viewport)
    {_camera=camera;_cells=cells;_viewport=viewport;MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);}
    public void Refresh()
    {
        var critical=_cells.Where(Critical).ToArray();MarkerCount=critical.Length;ProtectedRegions.Clear();
        foreach(var cell in critical)
        {var p=_camera.UnprojectPosition(PawnArtLayout.WorldAnchor(cell));if(_viewport().HasPoint(p))ProtectedRegions.Add(new Rect2(p-new Vector2(24,24),new Vector2(48,48)));}
        QueueRedraw();
    }
    private bool Critical(HexCell3D c)=>c.VisualState is HexCellVisualState.Selected or HexCellVisualState.Current or HexCellVisualState.Movable || c.GetNode("TokenRoot").GetChildCount()>0 || Route.Contains(c.Data!.CellCode);
    public override void _Draw()
    {
        if(_camera==null)return;
        var area=_viewport().Grow(-16);var font=ThemeDB.FallbackFont;
        Vector2 Screen(HexCell3D c)
        {var p=_camera.UnprojectPosition(PawnArtLayout.WorldAnchor(c));return p.Clamp(area.Position,area.End);}
        var used=new List<Vector2>();
        foreach(var cell in _cells.Where(Critical))
        {
            var p=Screen(cell);var color=cell.VisualState==HexCellVisualState.Movable?new Color("4dba8d"):new Color("eab956");
            var raw=_camera.UnprojectPosition(PawnArtLayout.WorldAnchor(cell));var outside=!area.HasPoint(raw);
            if(outside)
            {
                for(var i=0;i<12 && used.Any(v=>v.DistanceTo(p)<27);i++)
                    p=(p+(Math.Abs(p.X-area.Position.X)<1 || Math.Abs(p.X-area.End.X)<1?new Vector2(0,28):new Vector2(-28,0))).Clamp(area.Position,area.End);
                var direction=(raw-p).Normalized();var side=direction.Orthogonal();
                DrawColoredPolygon([p+direction*12,p-direction*5+side*5,p-direction*5-side*5],color);
            }
            used.Add(p);
            DrawCircle(p,10,new Color("233d46"));DrawArc(p,9,0,Mathf.Tau,24,color,3,true);
            if(cell.GetNode("TokenRoot").GetChildCount()>0)DrawCircle(p,3,new Color("fff7d7"));
            var text=cell.Data!.CellCode;
            var textPoint=p+new Vector2(p.X>area.GetCenter().X?-36:12,Math.Max(16,p.Y-8)-p.Y);
            DrawStringOutline(font,textPoint,text,HorizontalAlignment.Left,-1,16,5,new Color("223e42"));
            DrawString(font,textPoint,text,HorizontalAlignment.Left,-1,16,new Color("fff7db"));
        }
        var byCode=_cells.ToDictionary(c=>c.Data!.CellCode);
        for(var i=1;i<Route.Length;i++)
            if(byCode.TryGetValue(Route[i-1],out var a) && byCode.TryGetValue(Route[i],out var b))
            {
                var from=_camera.UnprojectPosition(PawnArtLayout.WorldAnchor(a));var to=_camera.UnprojectPosition(PawnArtLayout.WorldAnchor(b));
                if(Clip(ref from,ref to,area))
                {DrawLine(from,to,new Color("284c50"),7,true);DrawLine(from,to,new Color("fff2b5"),3,true);}
            }
    }
    private static bool Clip(ref Vector2 a,ref Vector2 b,Rect2 area)
    {
        var d=b-a;var t0=0f;var t1=1f;
        bool Edge(float p,float q)
        {if(Math.Abs(p)<.00001f)return q>=0;var t=q/p;if(p<0){if(t>t1)return false;t0=Math.Max(t0,t);}else {if(t<t0)return false;t1=Math.Min(t1,t);}return true;}
        if(!Edge(-d.X,a.X-area.Position.X) || !Edge(d.X,area.End.X-a.X) || !Edge(-d.Y,a.Y-area.Position.Y) || !Edge(d.Y,area.End.Y-a.Y))return false;
        b=a+d*t1;a+=d*t0;return true;
    }
}
