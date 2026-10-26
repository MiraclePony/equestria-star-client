using Godot;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

public static class SkyAssetPreview
{
    public static void Add(Node3D parent)
    {
        var root=new Node3D {Name="NewSkyDeliveryReview"};parent.AddChild(root);
        for(var i=0;i<9;i++)
        {
            var id="BG"+(101+i);if(!EnvironmentArtCatalog.Entries.TryGetValue(id,out var entry))continue;
            var position=new Vector3((i%3-1)*3.6f,0,(i/3-1)*3.6f);
            EnvironmentArtCatalog.Place(root,id,position,.72f,0);
            root.AddChild(new Label3D {Text=id+" · "+entry.Name,Position=position+new Vector3(0,.06f,1.45f),FontSize=28,PixelSize=.0035f,
                Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,NoDepthTest=true,Modulate=new Color("3b514c"),OutlineModulate=new Color("fff5dd"),OutlineSize=5});
        }
        root.AddChild(new MeshInstance3D {Name="PaperReviewDesk",Position=new Vector3(0,-.10f,0),Mesh=new BoxMesh {Size=new Vector3(11,.15f,11),Material=OrigamiMapPresentation.PaperMaterial(new Color("e7dfbf"))}});
        ConceptPaperStyle.Apply(root);
    }
}
