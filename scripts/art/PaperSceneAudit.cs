using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace EquestriaStar.Art;

/// <summary>Read-only inventory; shader behavior is checked separately from this report.</summary>
public static class PaperSceneAudit
{
    public static object Collect(Node root)
    {
        var meshes=new List<object>();var lights=new List<object>();var environments=new List<object>();var cameras=new List<object>();
        void Visit(Node node)
        {
            if(node is MeshInstance3D mesh && mesh.Mesh!=null)
            {
                var surfaces=new List<object>();
                for(var i=0;i<mesh.Mesh.GetSurfaceCount();i++)
                {
                    var mat=mesh.GetActiveMaterial(i);
                    surfaces.Add(mat switch
                    {
                        BaseMaterial3D p=>new {index=i,kind=mat.GetClass().ToString(),shading=p.ShadingMode.ToString(),metallic=p.Metallic,roughness=p.Roughness,emission=p.EmissionEnabled,vertexColor=p.VertexColorUseAsAlbedo,vertexSrgb=p.VertexColorIsSrgb,texture=p.AlbedoTexture?.ResourcePath??"",color=p.AlbedoColor.ToHtml(),transparency=p.Transparency.ToString()} as object,
                        ShaderMaterial p=>new {index=i,kind="ShaderMaterial",shader=p.Shader?.ResourcePath??""},
                        _=>new {index=i,kind=mat?.GetClass().ToString()??"none"}
                    });
                }
                meshes.Add(new {path=node.GetPath().ToString(),visible=mesh.IsVisibleInTree(),scale=mesh.GlobalBasis.Scale.ToString(),bounds=mesh.GetAabb().ToString(),shadow=mesh.CastShadow.ToString(),surfaces});
            }
            if(node is Light3D light)lights.Add(new {path=node.GetPath().ToString(),type=light.GetClass().ToString(),visible=light.IsVisibleInTree(),energy=light.LightEnergy,color=light.LightColor.ToHtml(),shadows=light.ShadowEnabled,rotation=light.RotationDegrees.ToString()});
            if(node is WorldEnvironment world)environments.Add(new {path=node.GetPath().ToString(),resource=world.Environment?.GetInstanceId(),ambient=world.Environment?.AmbientLightEnergy,exposure=world.Environment?.TonemapExposure,glow=world.Environment?.GlowEnabled,ssao=world.Environment?.SsaoEnabled,ssil=world.Environment?.SsilEnabled});
            if(node is Camera3D c)cameras.Add(new {path=node.GetPath().ToString(),current=c.Current,environmentOverride=c.Environment?.GetInstanceId(),attributes=c.Attributes?.GetClass().ToString(),autoExposure=c.Attributes?.AutoExposureEnabled,near=c.Near,far=c.Far,size=c.Size,transform=c.GlobalTransform.ToString()});
            foreach(var child in node.GetChildren())Visit(child);
        }
        Visit(root);
        return new {engine=Engine.GetVersionInfo().ToString(),renderer=RenderingServer.GetCurrentRenderingMethod(),physicalLightUnits=ProjectSettings.GetSetting("rendering/lights_and_shadows/use_physical_light_units",false).AsBool(),hdr2d=root.GetViewport().UseHdr2D,meshes,lights,environments,cameras};
    }
    public static void Save(Node root,string path)
    {using var f=FileAccess.Open(path,FileAccess.ModeFlags.Write);f.StoreString(JsonSerializer.Serialize(Collect(root),new JsonSerializerOptions{WriteIndented=true}));}
}
