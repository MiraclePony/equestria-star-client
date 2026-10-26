using System;
using System.Linq;
using Godot;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

/// <summary>Port-matched sample assembly. Does not claim a world map hydrology layout.</summary>
public static class WaterModulePreview
{
    public static Node3D Join(Node3D parent,Node3D previous,string previousPort,string id,string nextPort,float scale)
    {
        var fromId=previous.GetMeta("environment_asset").AsString();
        var from=EnvironmentArtCatalog.Entries[fromId].Ports.Single(p=>p.Id==previousPort);
        var to=EnvironmentArtCatalog.Entries[id].Ports.Single(p=>p.Id==nextPort);
        if(Math.Abs(from.Width*previous.Scale.X-to.Width*scale)>.0001f)
            throw new InvalidOperationException("Water port width mismatch");
        var target=previous.Transform*from.PositionGodot;
        var direction=-(previous.Basis*from.DirectionGodot).Normalized();
        var yaw=Mathf.Atan2(direction.X,direction.Z)-Mathf.Atan2(to.DirectionGodot.X,to.DirectionGodot.Z);
        var offset=new Basis(Vector3.Up,yaw)*(to.PositionGodot*scale);
        return EnvironmentArtCatalog.Place(parent,id,target-offset,scale,Mathf.RadToDeg(yaw))!;
    }
    public static void Add(Node3D parent)
    {
        var root=new Node3D {Name="WaterPortSample_NotWorldRiver"};parent.AddChild(root);
        root.AddChild(new MeshInstance3D {Name="SamplePaperBoard",Position=new Vector3(.8f,-.055f,-.9f),
            Mesh=new BoxMesh {Size=new Vector3(8.9f,.08f,9.0f),Material=OrigamiMapPresentation.PaperMaterial(new Color("d4ddbb"))}});
        var curve=EnvironmentArtCatalog.Place(root,"WAT006",new Vector3(0,.0f,1.1f),.8f,0)!;
        var straight=Join(root,curve,"B","WAT005","A",.8f);
        Join(root,straight,"B","WAT010","上游河口",.8f);
        EnvironmentArtCatalog.Place(root,"WAT007",new Vector3(3.7f,0,.3f),.75f,0);
        EnvironmentArtCatalog.Place(root,"WAT008",new Vector3(-2.7f,0,-1.1f),.75f,-20);
        EnvironmentArtCatalog.Place(root,"WAT009",new Vector3(3.6f,0,-2.4f),.85f,0);
        parent.AddChild(new Label3D {Text="水域接口试拼 · 正式水系位置待审阅",Position=new Vector3(0,.05f,3.1f),FontSize=30,PixelSize=.0045f,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new Color("4a635a"),OutlineModulate=new Color("fff5df"),OutlineSize=5});
    }
}
