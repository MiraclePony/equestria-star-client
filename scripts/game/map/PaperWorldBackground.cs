using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using EquestriaStar.Art;

namespace EquestriaStar.Game.Map;

/// <summary>Exterior-only, full 3D scenic proposal. No gameplay topology, physics or mesh LOD.</summary>
public static class PaperWorldBackground
{
    public static bool Enabled=true;
    public static void Add(Node3D scenery,IReadOnlyCollection<HexCell3D> cells,Aabb bounds,float r,Vector2[] outline,Vector2 centre)
    {
        if(!Enabled)return;
        var root=new Node3D {Name="V5WorldBackground"};scenery.AddChild(root);
        root.SetMeta("art_status","provisional_exterior_scenography_not_canonical_geography");
        var tool=new SurfaceTool();tool.Begin(Mesh.PrimitiveType.Triangles);
        tool.SetMaterial(new StandardMaterial3D {VertexColorUseAsAlbedo=true,Roughness=1});
        var groups=new List<object>();
        void Tri(Vector3 a,Vector3 b,Vector3 c,Color color)
        {var normal=(b-a).Cross(c-a).Normalized();if(normal.Y<0){(b,c)=(c,b);normal=-normal;}tool.SetNormal(normal);tool.SetColor(color.SrgbToLinear());tool.AddVertex(a);tool.AddVertex(c);tool.AddVertex(b);}
        void Ridge(Vector3 p,Vector3 dir,float width,float depth,float height,Color color,bool snow=false)
        {
            var side=new Vector3(dir.Z,0,-dir.X);var peak=p+dir*depth*.12f+Vector3.Up*height;
            var ring=Enumerable.Range(0,7).Select(i=>{var a=i*Mathf.Tau/7;return p+side*Mathf.Cos(a)*width+dir*Mathf.Sin(a)*depth;}).ToArray();
            for(var i=0;i<ring.Length;i++)
            {var tint=i%3==0?color.Lightened(.12f):i%3==1?color:color.Darkened(.07f);var j=(i+1)%ring.Length;
             if(snow){var a=peak.Lerp(ring[i],.36f);var b=peak.Lerp(ring[j],.36f);Tri(peak,a,b,new Color("e1e9e8"));Tri(a,ring[i],ring[j],tint);Tri(a,ring[j],b,tint);}
             else Tri(peak,ring[i],ring[j],tint);}
        }
        void LandShelf(Vector3 p,Vector3 dir,float width,float depth,Color color)
        {
            var side=new Vector3(dir.Z,0,-dir.X);var inner=new Vector3[10];var outer=new Vector3[10];
            for(var i=0;i<10;i++)
            {var a=i*Mathf.Tau/10;var offset=side*Mathf.Cos(a)*width+dir*Mathf.Sin(a)*depth;inner[i]=p+offset*.82f;inner[i].Y=-r*.24f;outer[i]=p+offset;outer[i].Y=-r*.94f;}
            var middle=p;middle.Y=-r*.24f;
            for(var i=0;i<10;i++){var j=(i+1)%10;Tri(middle,inner[i],inner[j],color);Tri(inner[i],outer[i],outer[j],color.Darkened(.08f));Tri(inner[i],outer[j],inner[j],color);}
        }
        Vector3 Exterior(Vector3 p,Vector3 dir,float footprint)
        {for(var i=0;i<32 && cells.Any(c=>new Vector2(c.Position.X-p.X,c.Position.Z-p.Z).Length()<r+footprint);i++)p+=dir*r*.55f;return p;}
        // A low folded apron outside the existing outline joins paper core, beaches and scenery.
        for(var i=0;i<outline.Length;i++)
        {
            var j=(i+1)%outline.Length;var a=outline[i];var b=outline[j];var da=(a-centre).Normalized();var db=(b-centre).Normalized();
            Vector3 P(Vector2 p,float y)=>new(p.X,y*r,p.Y);
            var col=new Color(i/8%3==0?"8eac79":i/8%3==1?"a8ba84":"98b694");
            Tri(P(a,-.24f),P(b,-.24f),P(b+db*r*.6f,-.67f),col);Tri(P(a,-.24f),P(b+db*r*.6f,-.67f),P(a+da*r*.6f,-.67f),col);
        }
        foreach(var region in cells.GroupBy(c=>c.Data!.BoardCode))
        {
            var ids=region.Select(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id??"").ToHashSet();
            var kind=ids.Contains("dragon-lands")?"volcanic":ids.Contains("appleloosa")||ids.Contains("baltimare")?"dry":ids.Contains("yakyakistan")||ids.Contains("crystal-castle")?"snow":ids.Contains("canterlot")?"city-cliff":ids.Contains("manehattan-district")?"city-coast":ids.Contains("hippogriff")?"island":region.Key=="B"?"everfree":"meadow";
            if(kind=="meadow" && region.Key!="A")continue;
            var cluster=region.Aggregate(Vector3.Zero,(sum,c)=>sum+c.Position)/region.Count();
            var dir=new Vector3(cluster.X-centre.X,0,cluster.Z-centre.Y).Normalized();if(dir.LengthSquared()<.1f)dir=Vector3.Back;
            var side=new Vector3(dir.Z,0,-dir.X);var angle=Mathf.Atan2(dir.Z,dir.X);if(angle<0)angle+=Mathf.Tau;
            var point=outline[((int)Mathf.Round(angle/Mathf.Tau*outline.Length))%outline.Length];
            var near=Exterior(new Vector3(point.X,-r*.24f,point.Y)+dir*r*.65f,dir,r*1.45f);
            var color=new Color(kind switch {"snow"=>"a5bad4","dry"=>"c7a071","volcanic"=>"82718d","city-cliff"=>"ada7c6","city-coast"=>"a5bcb5","everfree"=>"638f8f",_=>"96b48b"});
            var high=dir.Z<.1f?1.8f:.65f;
            if(kind=="city-cliff")high*=1.25f;
            LandShelf(near,dir,r*3.0f,r*2.2f,color.Darkened(.10f));
            Ridge(near,dir,r*1.45f,r*1.2f,r*high,color,kind=="snow");
            Ridge(near+side*r*1.20f+dir*r*.50f,dir,r*.90f,r,r*high*.62f,color.Darkened(.06f),kind=="snow");
            Ridge(near-side*r*1.45f+dir*r*.40f,dir,r*.88f,r,r*high*.48f,color.Lightened(.04f),false);
            // A separate desaturated far ridge is real geometry from every camera azimuth.
            var far=near+dir*r*3.3f;far.Y=-r*.94f;var farColor=color.Lerp(new Color("9cb8c6"),.40f);
            Ridge(far,dir,r*2.15f,r*1.15f,r*high*.85f,farColor,kind=="snow");
            Ridge(far+side*r*2.4f+dir*r*.5f,dir,r*1.8f,r,r*high*.59f,farColor.Lightened(.06f));
            groups.Add(new {board=region.Key,kind,near=near.ToString(),far=far.ToString(),high,policy="outside cells; artistic shoreline/ridge proposal; no shared river claim"});
            if(kind is "everfree" or "meadow" or "snow")
            {
                var tree=kind=="everfree"?"VEG006":kind=="snow"?"VEG103":"VEG003";
                if(!EnvironmentArtCatalog.Entries.ContainsKey(tree))tree="VEG002";
                for(var i=0;i<(kind=="everfree"?9:5);i++)
                {
                    var at=near-dir*r*.75f+side*r*(i-(kind=="everfree"?4:2))*.55f;
                    at=Exterior(at,dir,r*.40f);at.Y=-r*.24f;
                    var treeRoot=EnvironmentArtCatalog.Place(root,tree,at,r*(.28f+(i%3)*.055f),i*59);
                    if(treeRoot!=null)PaperFoldGeometry.Apply(treeRoot);
                }
            }
        }
        root.AddChild(new MeshInstance3D {Name="RegionalNearAndFarFoldedLandforms",Mesh=tool.Commit(),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
        // Ocean is the actual visible lower plane, not the Environment clear colour or skybox.
        var sea=scenery.GetNode<MeshInstance3D>("Sea");
        var ocean=new ShaderMaterial {Shader=GD.Load<Shader>("res://assets/shaders/paper_ocean.gdshader")};
        ocean.SetShaderParameter("map_centre",centre);ocean.SetShaderParameter("hex_diameter",r*2);
        if(ArtRenderProfile.IsSunlit)ocean.SetShaderParameter("sun_mix",PaperSunlitStyle.Current.OceanSun);
        sea.MaterialOverride=ocean;sea.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        // Deliberate back-side cloud banks; all assets are authored full 3D folds.
        for(var i=0;i<4;i++)
        {
            var p=new Vector3(centre.X+(i-1.5f)*r*7,r*(1.15f+i%2*.65f),bounds.Position.Z-r*(4.0f+i%2));
            var cloud=EnvironmentArtCatalog.Place(root,"BG"+(103+i%3),p,r*(.5f+i%3*.16f),i*23);
            if(cloud==null)continue;
            cloud.AddChild(new SceneryDrift {Amplitude=r*.05f,Speed=.2f,Phase=i*1.7f});
            foreach(var m in cloud.FindChildren("*","GeometryInstance3D",true,false).OfType<GeometryInstance3D>())m.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
        root.SetMeta("groups_json",JsonSerializer.Serialize(groups));root.SetMeta("region_count",groups.Count);
        root.SetMeta("new_landform_draws",1);root.SetMeta("ocean_transparent_layers",0);
    }
}
