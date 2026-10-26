using System;
using System.Collections.Generic;
using Godot;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

/// <summary>Material adaptation for the supplied concept. Source GLBs and decal UVs stay intact.</summary>
public static class ConceptPaperStyle
{
    public static readonly IReadOnlyDictionary<string,Color> Regions=new Dictionary<string,Color>
    {
        ["A"]=new("6c883e"),["B"]=new("315b2a"),["C"]=new("8975a8"),["D"]=new("b09551"),
        ["E"]=new("a2aaa0"),["F"]=new("b38da8"),["G"]=new("cf8579"),["H"]=new("b89060"),
        ["I"]=new("a6b4ba"),["J"]=new("c3a65f"),["K"]=new("4c9684"),["L"]=new("71a3ad")
    };
    private static Shader? _shader;
    private static Shader? _nprShader;
    private static Shader? _sunShader;
    private static Shader? _legacyShader;
    private static Texture2D? _legacyFibre;
    public static bool LegacyMaterialForInspection;
    /// <summary>Presets 7/8 use the NPR lighting branch; presets 0..6 keep the original shader.</summary>
    private static bool NprActive=>ArtRenderProfile.CurrentPreset is 7 or 8 && !PaperNprStyle.Current.PlainMaterial;
    /// <summary>Preset 9 uses the sunlit papercraft shader (crumpled cards, rims, warm response).</summary>
    private static bool SunActive=>ArtRenderProfile.IsSunlit;
    private static string StyleKey=>SunActive?"sun:":NprActive?"npr:":"";
    private static readonly Dictionary<string,Material> Materials=new();
    private static readonly Dictionary<int,ArrayMesh> Borders=new();
    private static readonly HashSet<BaseMaterial3D> TexturedSources=new();
    public static void ReleaseAtShutdown()
    {
        // Resources may be shared with live native mesh surfaces. Drop owned references,
        // then let RefCounted/managed collection release each resource exactly once.
        foreach(var source in TexturedSources)if(GodotObject.IsInstanceValid(source))source.AlbedoTexture=null;
        foreach(var material in Materials.Values)
            if(GodotObject.IsInstanceValid(material) && material is ShaderMaterial paper)
            paper.SetShaderParameter("albedo_texture",default(Variant));
        TexturedSources.Clear();Materials.Clear();Borders.Clear();_shader=null;_nprShader=null;_sunShader=null;PaperSunlitStyle.ReleaseAtShutdown();_legacyShader=null;_legacyFibre=null;PaperFoldGeometry.Clear();PaperFlagMotion.Clear();
    }
    private static ShaderMaterial Create(Color color,bool terrain=false,string board="",Texture2D? albedo=null,bool vertex=false,bool vertexSrgb=false)
    {
        var sun=SunActive && !LegacyMaterialForInspection;
        var npr=!sun && NprActive && !LegacyMaterialForInspection;
        if(sun)_sunShader??=GD.Load<Shader>("res://assets/shaders/sunlit_paper.gdshader");
        else if(npr)_nprShader??=GD.Load<Shader>("res://assets/shaders/physical_paper_npr.gdshader");
        else _shader??=GD.Load<Shader>("res://assets/shaders/physical_paper.gdshader");
        if(LegacyMaterialForInspection && _legacyShader==null)_legacyShader=GD.Load<Shader>("res://assets/shaders/legacy_physical_paper.gdshader");
        var material=new ShaderMaterial {Shader=LegacyMaterialForInspection?_legacyShader:sun?_sunShader:npr?_nprShader:_shader};
        material.SetShaderParameter("base_color",color);
        // Default has no grain: do not allocate or upload a noise texture for an inactive effect.
        if(LegacyMaterialForInspection)
        {
            if(_legacyFibre==null)
            {using var noise=new FastNoiseLite {NoiseType=FastNoiseLite.NoiseTypeEnum.SimplexSmooth,Frequency=.085f,FractalOctaves=3,Seed=1483};using var image=noise.GetImage(256,256);image.GenerateMipmaps();_legacyFibre=ImageTexture.CreateFromImage(image);}
            material.SetShaderParameter("fibre_texture",_legacyFibre);
        }
        material.SetShaderParameter("terrain_palette",terrain);
        material.SetShaderParameter("world_paper_fields",EquestriaStar.Game.Map.PaperWorldBackground.Enabled);
        material.SetShaderParameter("use_vertex_color",vertex);
        material.SetShaderParameter("vertex_color_srgb",vertexSrgb);
        material.SetShaderParameter("grain_strength",LegacyMaterialForInspection?(terrain?.22f:.12f):0f);
        if(terrain && Regions.TryGetValue(board,out var region))material.SetShaderParameter("region_color",region);
        if(albedo!=null){material.SetShaderParameter("albedo_texture",albedo);material.SetShaderParameter("use_albedo_texture",true);}
        if(npr)PaperNprStyle.ApplyMaterial(material,terrain);
        if(sun){PaperSunlitStyle.ApplyMaterial(material,terrain,board);material.SetMeta("paper_board",board);}
        return material;
    }
    /// <summary>Re-push the NPR material parameters on the cached paper materials (lookdev tuning / live profile switch).</summary>
    public static void RefreshNprParameters()
    {
        if(!NprActive && !SunActive)return;
        foreach(var material in Materials.Values)
            if(GodotObject.IsInstanceValid(material) && material is ShaderMaterial paper)
            {
                var terrain=paper.GetShaderParameter("terrain_palette").AsBool();
                if(SunActive)PaperSunlitStyle.ApplyMaterial(paper,terrain,paper.GetMeta("paper_board","").AsString());
                else PaperNprStyle.ApplyMaterial(paper,terrain);
            }
    }
    public static Material Ground(string board)
    {
        var key=(EquestriaStar.Game.Map.PaperWorldBackground.Enabled?"world5:":"world4:")+(LegacyMaterialForInspection?"legacy:":"")+StyleKey+"ground:"+board;
        if(!Materials.TryGetValue(key,out var material)){material=Create(Regions[board],true,board);Materials[key]=material;}
        return material;
    }
    public static void Apply(Node node,bool terrain=false,string board="")
    {
        if(node is MeshInstance3D mesh && mesh.Mesh!=null)
            for(var i=0;i<mesh.Mesh.GetSurfaceCount();i++)
            {
                if(mesh.GetActiveMaterial(i) is not StandardMaterial3D source)continue;
                PaperMaterialLifetime.Remember(source);
                if(source.AlbedoTexture!=null)TexturedSources.Add(source);
                // Transparent printed inks retain the original blend/scissor path and UVs.
                if(source.Transparency!=BaseMaterial3D.TransparencyEnum.Disabled)continue;
                var flowing=source.ResourceName.ToString() is "Water" or "Papercard_Pond"?1:mesh.Name.ToString().Contains("ContinuousRainbowAndPool")?2:0;
                var key=(EquestriaStar.Game.Map.PaperWorldBackground.Enabled?"world5:":"world4:")+(LegacyMaterialForInspection?"legacy:":"")+StyleKey+(terrain?"ground:"+board:$"paper:{source.GetInstanceId()}:{flowing}");
                if(!Materials.TryGetValue(key,out var material))
                {
                    material=terrain?Ground(board):Create(source.AlbedoColor,false,"",source.AlbedoTexture,source.VertexColorUseAsAlbedo,source.VertexColorIsSrgb);
                    if(!LegacyMaterialForInspection && material is ShaderMaterial moving)moving.SetShaderParameter("water_flow",(float)flowing);
                    Materials[key]=material;
                }
                mesh.SetSurfaceOverrideMaterial(i,material);
            }
        foreach(var child in node.GetChildren())Apply(child,terrain,board);
    }
    public static void ApplyCell(HexCell3D cell)
    {
        var imported=cell.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset");
        if(imported!=null)
        {
            foreach(var child in imported.GetChildren())Apply(child,child.Name=="Terrain",cell.Data!.BoardCode);
        }
        var landmark=OrigamiArtCatalog.Resolve(cell.Data!) is {Id:not "everfree"};
        var radius=cell.Radius;
        var key=((int)Mathf.Round(radius*10000)*2+(landmark?1:0))*2+(SunActive?1:0);
        if(!Borders.TryGetValue(key,out var border))
        {
            var tool=new SurfaceTool();tool.Begin(Mesh.PrimitiveType.Triangles);
            var width=radius*(landmark?.025f:SunActive?PaperSunlitStyle.Current.SeamWidth:.010f);
            Vector3 Point(int i,float r){var a=Mathf.DegToRad(i*60+30);return new Vector3(Mathf.Cos(a)*r,.008f,Mathf.Sin(a)*r);}
            for(var i=0;i<6;i++)
            {
                var a=Point(i,radius*.988f);var b=Point(i+1,radius*.988f);var c=Point(i,radius*.988f-width);var d=Point(i+1,radius*.988f-width);
                tool.SetNormal(Vector3.Up);tool.AddVertex(a);tool.AddVertex(c);tool.AddVertex(b);tool.AddVertex(b);tool.AddVertex(c);tool.AddVertex(d);
            }
            border=tool.Commit();Borders[key]=border;
        }
        cell.GetNode<Node3D>("VisualRoot/DecorationRoot").AddChild(new MeshInstance3D {Name="PaperRim",Mesh=border,
            MaterialOverride=OrigamiMapPresentation.PaperMaterial(SunActive?(landmark?new Color("f1dfae"):PaperSunlitStyle.Current.SeamColor):new Color(landmark?"b9ab82":"b2c0a0")),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
    }
}
