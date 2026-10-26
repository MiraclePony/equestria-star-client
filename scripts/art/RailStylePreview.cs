using System;
using Godot;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

/// <summary>A standalone paper rail sample, deliberately not a map station connection.</summary>
public static class RailStylePreview
{
    public static void Add(Node3D parent,Vector3 position,float scale)
    {
        var root=new Node3D {Name="RailStyleSample_NotRoute",Position=position,Scale=Vector3.One*scale};
        root.SetMeta("route_status","style_sample_no_verified_station_path");parent.AddChild(root);
        root.AddChild(new MeshInstance3D {Name="SamplePaperBoard",Position=new Vector3(0,-.05f,.6f),
            Mesh=new BoxMesh {Size=new Vector3(3.9f,.08f,3.1f),Material=OrigamiMapPresentation.PaperMaterial(new Color("d0dcb6"))}});
        // Quarter arc plus straight approach, consistent rail gauge and sleeper spacing.
        var railMaterial=OrigamiMapPresentation.PaperMaterial(new Color("fff1d0"));
        var sleeperMaterial=OrigamiMapPresentation.PaperMaterial(new Color("c89880"));
        Vector3 Point(float t) => t<1 ? new Vector3(-1.6f+t*1.6f,0,0) : new Vector3(Mathf.Sin((t-1)*Mathf.Pi/2)*1.2f,0,1.2f-Mathf.Cos((t-1)*Mathf.Pi/2)*1.2f);
        for(var side=-1;side<=1;side+=2)
        {
            for(var i=0;i<40;i++)
            {
                var a=Point(i/20f);var b=Point((i+1)/20f);var tangent=(b-a).Normalized();
                var lateral=new Vector3(-tangent.Z,0,tangent.X)*(.14f*side);
                var rail=new MeshInstance3D {Name="FoldedRail",Position=(a+b)*.5f+lateral+Vector3.Up*.06f,
                    Mesh=new BoxMesh {Size=new Vector3(.038f,.05f,a.DistanceTo(b)+.012f),Material=railMaterial}};
                rail.Rotation=new Vector3(0,Mathf.Atan2(tangent.X,tangent.Z),0);root.AddChild(rail);
            }
        }
        for(var i=0;i<=20;i++)
        {
            var a=Point(i/10f);var tangent=(Point(Math.Min(2,i/10f+.01f))-Point(Math.Max(0,i/10f-.01f))).Normalized();
            var sleeper=new MeshInstance3D {Name="Sleeper",Position=a+Vector3.Up*.025f,
                Mesh=new BoxMesh {Size=new Vector3(.44f,.045f,.065f),Material=sleeperMaterial}};
            sleeper.Rotation=new Vector3(0,Mathf.Atan2(tangent.X,tangent.Z),0);root.AddChild(sleeper);
        }
        root.AddChild(new Label3D {Text="铁路样件 · 非正式路线",Position=new Vector3(0,.04f,1.8f),
            FontSize=28,PixelSize=.004f,Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate=new Color("4a635a"),OutlineModulate=new Color("fff5df"),OutlineSize=5});
    }
}
