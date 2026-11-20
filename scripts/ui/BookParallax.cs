using Godot;

namespace EquestriaStar.UI;

/// <summary>
/// 书本背景的鼠标视差：根据鼠标相对屏幕中心的位置，平滑地让左页隧道书里的深层纸片多移动一点。
/// 深度图在 res://assets/art/ui/entry_book/depth/&lt;页&gt;.png；缺失时退化为普通静态背景。
/// </summary>
public partial class BookParallax : TextureRect
{
    /// <summary>最大位移（UV）。0.006 ≈ 1920 宽下 11 像素，只给最远的天空。</summary>
    public const float MaxOffset = 0.006f;

    /// <summary>出图 / 测试用：非空时固定位移（-1..1），不跟随鼠标。</summary>
    public static Vector2? ForcedDirection { get; set; }

    /// <summary>出图 / 测试用：关闭视差（保证截图确定）。</summary>
    public static bool Enabled { get; set; } = true;

    private ShaderMaterial? _material;
    private Vector2 _current;

    public void Setup(Texture2D? depth)
    {
        if (depth == null)
        {
            return;
        }

        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/book_parallax.gdshader") };
        _material.SetShaderParameter("depth_map", depth);
        _material.SetShaderParameter("offset", Vector2.Zero);
        Material = _material;
    }

    public override void _Process(double delta)
    {
        if (_material == null)
        {
            return;
        }

        var target = Vector2.Zero;
        if (Enabled)
        {
            if (ForcedDirection is { } forced)
            {
                target = forced;
            }
            else
            {
                var viewport = GetViewportRect().Size;
                if (viewport.X > 0 && viewport.Y > 0)
                {
                    var mouse = GetViewport().GetMousePosition();
                    target = ((mouse / viewport) - new Vector2(0.5f, 0.5f)) * 2f;
                    target = target.Clamp(-Vector2.One, Vector2.One);
                }
            }
        }

        var next = ForcedDirection != null ? target : _current.Lerp(target, 1f - Mathf.Exp(-(float)delta * 5f));
        if (next.DistanceSquaredTo(_current) < 1e-8f && ForcedDirection == null)
        {
            return;
        }

        _current = next;
        // 鼠标向右 → 深处向左（与视线相反），纵向幅度减半。
        _material.SetShaderParameter("offset", new Vector2(-_current.X, -_current.Y * 0.5f) * MaxOffset);
    }
}
