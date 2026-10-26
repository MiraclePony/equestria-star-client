using Godot;

namespace EquestriaStar.Art;

public partial class PaperAtmosphere : CanvasLayer
{
    private BackBufferCopy _copy=null!;
    private ColorRect _rect=null!;
    public ShaderMaterial GradeMaterial {get;private set;}=null!;
    public bool IsActive=>Visible && !GetViewport().UseHdr2D;
    public override void _Ready()
    {
        Layer=-10; // 3D, one grade, then default Canvas/UI and all HUD layers.
        _copy=new BackBufferCopy {Name="ExplicitSceneCopy",CopyMode=BackBufferCopy.CopyModeEnum.Viewport};AddChild(_copy);
        GradeMaterial=new ShaderMaterial {Shader=GD.Load<Shader>("res://assets/shaders/paper_grade.gdshader")};
        _rect=new ColorRect {Name="PaperGrade",MouseFilter=Control.MouseFilterEnum.Ignore,Material=GradeMaterial};AddChild(_rect);
        _rect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);Refresh();
    }
    public override void _Process(double delta)=>Refresh();
    private void Refresh()
    {if(_rect==null)return;_rect.Visible=IsActive;_copy.CopyMode=IsActive?BackBufferCopy.CopyModeEnum.Viewport:BackBufferCopy.CopyModeEnum.Disabled;}
    public void Palette(int index)
    {
        GradeMaterial.SetShaderParameter("fairy",index>=3);
        GradeMaterial.SetShaderParameter("sun_strength",0f);
        if(index==PaperSunlitStyle.PresetIndex)
        {
            GradeMaterial.SetShaderParameter("npr_strength",0f);
            GradeMaterial.SetShaderParameter("strength",1f);
            PaperSunlitStyle.ApplyGrade(GradeMaterial);
            return;
        }
        if(index>=7)
        {
            var npr=PaperNprStyle.Current;
            GradeMaterial.SetShaderParameter("npr_strength",npr.GradeStrength);
            GradeMaterial.SetShaderParameter("npr_saturation",npr.GradeSaturation);
            GradeMaterial.SetShaderParameter("npr_contrast",npr.GradeContrast);
            GradeMaterial.SetShaderParameter("npr_shadow_depth",npr.GradeShadowDepth);
            GradeMaterial.SetShaderParameter("npr_highlight_soft",npr.GradeHighlightSoft);
            GradeMaterial.SetShaderParameter("npr_vignette",npr.GradeVignette);
            GradeMaterial.SetShaderParameter("shadow_shift",npr.GradeShadowShift);
            GradeMaterial.SetShaderParameter("highlight_shift",npr.GradeHighlightShift);
            GradeMaterial.SetShaderParameter("strength",1f);
            return;
        }
        GradeMaterial.SetShaderParameter("npr_strength",0f);
        GradeMaterial.SetShaderParameter("strength",index==4?1f:index==3?.88f:.50f);
        GradeMaterial.SetShaderParameter("contrast",index==4?1.12f:index==3?1.085f:1.025f);
        if(index>=3)
        {
            if(index>=5)
            {
                GradeMaterial.SetShaderParameter("strength",1f);
                GradeMaterial.SetShaderParameter("saturation",index==6?1.24f:1.18f);
                GradeMaterial.SetShaderParameter("contrast",index==6?1.14f:1.10f);
                GradeMaterial.SetShaderParameter("shadow_shift",index==6?new Vector3(-.024f,-.006f,.098f):new Vector3(-.017f,-.004f,.077f));
                GradeMaterial.SetShaderParameter("highlight_shift",index==6?new Vector3(.037f,.012f,-.020f):new Vector3(.030f,.010f,-.017f));
                return;
            }
            GradeMaterial.SetShaderParameter("saturation",index==4?1.20f:1.14f);
            GradeMaterial.SetShaderParameter("shadow_shift",index==4?new Vector3(-.018f,-.005f,.085f):new Vector3(-.012f,-.003f,.060f));
            GradeMaterial.SetShaderParameter("highlight_shift",index==4?new Vector3(.033f,.010f,-.019f):new Vector3(.025f,.008f,-.014f));
            return;
        }
        GradeMaterial.SetShaderParameter("shadow_shift",new Vector3(-.004f,-.001f,.012f));
        GradeMaterial.SetShaderParameter("saturation",index==2?1f:1.02f);
        GradeMaterial.SetShaderParameter("highlight_shift",index==2?new Vector3(.012f,.004f,-.006f):new Vector3(.006f,.003f,-.004f));
    }
    public override void _ExitTree(){GradeMaterial=null!;_rect=null!;_copy=null!;}
}
