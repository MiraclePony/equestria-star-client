using System;
using System.Threading.Tasks;
using Godot;

namespace EquestriaStar.UI;

/// <summary>
/// 入口立体书的程序化翻页：
/// - 切换书页（登录 / 注册 / 大厅 / 房间 / 选角）时，截下旧画面，让一页纸绕书脊翻过去，背面是新一页的书本背景；
/// - 客户端启动时先显示一张程序生成的布面封面，再把封面翻开。
/// 纯视觉叠层，不改动页面切换逻辑；无头模式（测试）与 <see cref="Enabled"/> = false 时直接跳过。
/// </summary>
public partial class PageTurn : Control
{
    public const float Duration = 0.8f;
    private const float PageTop = 30f;
    private const float PageBottom = 1005f;

    /// <summary>出图 / 测试可关闭。</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>出图用：非空时把翻页定格在这个角度（弧度，0..π），不自动播放、不自动移除。</summary>
    public static float? FreezeAngle { get; set; }

    /// <summary>当前定格中的叠层（出图时手动清理）。</summary>
    public static PageTurn? Frozen { get; private set; }

    private ShaderMaterial _material = null!;

    /// <summary>书本几何（舞台像素）：书脊、左页外缘、右页外缘。</summary>
    public static (float Spine, float Left, float Right) Geometry(string background) => background switch
    {
        "login" => (838f, 52f, 1838f),
        "select" => (734f, 60f, 1830f),
        _ => (742f, 52f, 1840f)
    };

    public static bool CanPlay(Node anyNode)
    {
        return Enabled
            && anyNode.IsInsideTree()
            && !string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>截取当前屏幕（上一帧已绘制的内容）。</summary>
    public static ImageTexture? CaptureScreen(Node anyNode)
    {
        var image = anyNode.GetViewport().GetTexture().GetImage();
        return image == null || image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// 播放翻页：oldScreen 是旧画面截图；oldBackground / newBackground 是两页的书本背景名；
    /// forward = true 时右页向左翻（进入下一页），false 时左页向右翻（返回上一页）。
    /// </summary>
    public static PageTurn? Play(Control host, Texture2D oldScreen, string oldBackground, string newBackground, bool forward)
    {
        var turn = new PageTurn { Name = "PageTurn", MouseFilter = MouseFilterEnum.Stop };
        turn.SetAnchorsPreset(LayoutPreset.FullRect);
        host.AddChild(turn);
        turn.Setup(oldScreen, oldBackground, newBackground, forward);
        turn.Run();
        return turn;
    }

    private void Setup(Texture2D oldScreen, string oldBackground, string newBackground, bool forward)
    {
        var (spine, left, right) = Geometry(oldBackground);
        var (newSpine, newLeft, newRight) = Geometry(newBackground);
        var pageWidth = forward ? right - spine : left - spine;
        var backWidth = forward ? newLeft - newSpine : newRight - newSpine;

        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/page_turn.gdshader") };
        _material.SetShaderParameter("old_tex", oldScreen);
        _material.SetShaderParameter("back_tex", EntryBook.Tex("bg/" + newBackground) ?? (Texture2D)oldScreen);
        _material.SetShaderParameter("spine", spine / EntryBook.StageWidth);
        _material.SetShaderParameter("page_w", pageWidth / EntryBook.StageWidth);
        _material.SetShaderParameter("back_spine", newSpine / EntryBook.StageWidth);
        _material.SetShaderParameter("back_w", backWidth / EntryBook.StageWidth);
        _material.SetShaderParameter("page_top", PageTop / EntryBook.StageHeight);
        _material.SetShaderParameter("page_bottom", PageBottom / EntryBook.StageHeight);
        _material.SetShaderParameter("angle", 0f);
        _material.SetShaderParameter("fade", 1f);
        Material = _material;
        UpdateStageRect();
        Resized += UpdateStageRect;
    }

    private void UpdateStageRect()
    {
        var size = Size;
        if (size.X <= 0 || size.Y <= 0)
        {
            return;
        }

        // 舞台居中（与 EntryBook.PlaceOnStage 一致）。
        var origin = (size - new Vector2(EntryBook.StageWidth, EntryBook.StageHeight)) / 2f;
        _material.SetShaderParameter("stage_origin", origin / size);
        _material.SetShaderParameter("stage_size", new Vector2(EntryBook.StageWidth, EntryBook.StageHeight) / size);
    }

    public override void _Draw()
    {
        // 着色器作用在整块矩形上（白色底，颜色全部来自着色器）。
        DrawRect(new Rect2(Vector2.Zero, Size), Colors.White);
    }

    private void Run()
    {
        if (FreezeAngle is { } frozen)
        {
            _material.SetShaderParameter("angle", frozen);
            Frozen = this;
            return;
        }

        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(a => _material.SetShaderParameter("angle", a)), 0f, Mathf.Pi, Duration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenMethod(Callable.From<float>(f => _material.SetShaderParameter("fade", f)), 1f, 0f, 0.18f);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public static void ClearFrozen()
    {
        if (Frozen != null && GodotObject.IsInstanceValid(Frozen))
        {
            Frozen.QueueFree();
        }

        Frozen = null;
    }

    // ------------------------------------------------------------------ 启动封面

    /// <summary>
    /// 启动时：在登录页上盖一张程序生成的封面（雾紫布面 + 低调金护角 + 标志），截图后把封面翻开。
    /// </summary>
    public static async Task PlayOpening(Control host)
    {
        if (!CanPlay(host))
        {
            return;
        }

        var cover = BuildCover();
        host.AddChild(cover);
        // 等封面真正画出来再截图（期间屏幕上只有封面，不会闪出登录页）。
        for (var i = 0; i < 3; i++)
        {
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (!GodotObject.IsInstanceValid(host) || !host.IsInsideTree())
        {
            return;
        }

        var shot = CaptureScreen(host);
        if (shot == null)
        {
            cover.QueueFree();
            return;
        }

        Play(host, shot, "login", "login", forward: true);
        cover.QueueFree();
    }

    private static Control BuildCover()
    {
        var root = new Control { Name = "BookCover", MouseFilter = MouseFilterEnum.Stop };
        root.SetAnchorsPreset(LayoutPreset.FullRect);

        // 合上的书：左边只有桌面（暖木色 + 低透明纸纹，接近背景图里的木桌）。
        var wood = new ColorRect { Color = new Color("6a4231"), MouseFilter = MouseFilterEnum.Ignore };
        wood.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddChild(wood);
        var grain = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/art/textures/paper/paper_felt.png"),
            StretchMode = TextureRect.StretchModeEnum.Tile,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Modulate = new Color(0.55f, 0.36f, 0.26f, 0.35f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        grain.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddChild(grain);

        var (spine, _, right) = Geometry("login");
        // 封面与右页同大（翻动的就是这块区域）。
        var coverRect = new Rect2(spine, PageTop, right - spine, PageBottom - PageTop);
        var board = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        board.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("9d8aa6"),
            BorderColor = new Color("7d6a86"),
            BorderWidthLeft = 10,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 6,
            CornerRadiusTopRight = 18,
            CornerRadiusBottomRight = 18,
            CornerRadiusTopLeft = 4,
            CornerRadiusBottomLeft = 4,
            ShadowColor = new Color(0.12f, 0.07f, 0.04f, 0.45f),
            ShadowSize = 22,
            ShadowOffset = new Vector2(6, 12),
            AntiAliasing = true
        });
        root.AddChild(board);
        EntryBook.PlaceOnStage(board, coverRect);

        // 布纹：纸纹贴图低透明度平铺。
        var weave = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/art/textures/paper/paper_felt.png"),
            StretchMode = TextureRect.StretchModeEnum.Tile,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Modulate = new Color(1, 1, 1, 0.16f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        weave.SetAnchorsPreset(LayoutPreset.FullRect);
        weave.OffsetLeft = 12;
        weave.OffsetTop = 4;
        weave.OffsetRight = -4;
        weave.OffsetBottom = -8;
        board.AddChild(weave);

        // 压印内框
        var inner = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        inner.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            DrawCenter = false,
            BorderColor = new Color("e9dcc6", 0.75f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            AntiAliasing = true
        });
        inner.SetAnchorsPreset(LayoutPreset.FullRect);
        inner.OffsetLeft = 70;
        inner.OffsetTop = 58;
        inner.OffsetRight = -58;
        inner.OffsetBottom = -58;
        board.AddChild(inner);

        // 中央：马蹄铁 + 标志 + 小字
        var center = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        center.AddThemeConstantOverride("separation", 18);
        board.AddChild(center);
        var shoe = EntryBook.MakeImage("parts/horseshoe_gold", new Vector2(90, 94));
        center.AddChild(shoe);
        var crest = EntryBook.Crest(520, 60);
        crest.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        center.AddChild(crest);
        var line = EntryBook.MakeLabel("一本会动的小马国立体书", 22, new Color("f4ead8"), EntryBook.BodyFont, HorizontalAlignment.Center);
        center.AddChild(line);

        // 四角低调金护角
        foreach (var (ax, ay) in new[] { (0f, 0f), (1f, 0f), (0f, 1f), (1f, 1f) })
        {
            var corner = new Panel { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(46, 46) };
            corner.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color("c99a45"),
                BorderColor = new Color("a77d36"),
                BorderWidthBottom = 2,
                BorderWidthRight = 2,
                CornerRadiusTopLeft = ax == 0 && ay == 0 ? 4 : 0,
                CornerRadiusTopRight = ax == 1 && ay == 0 ? 16 : 0,
                CornerRadiusBottomLeft = ax == 0 && ay == 1 ? 4 : 0,
                CornerRadiusBottomRight = ax == 1 && ay == 1 ? 16 : 0,
                AntiAliasing = true
            });
            corner.AnchorLeft = ax;
            corner.AnchorRight = ax;
            corner.AnchorTop = ay;
            corner.AnchorBottom = ay;
            corner.OffsetLeft = ax == 0 ? 6 : -50;
            corner.OffsetRight = corner.OffsetLeft + 46;
            corner.OffsetTop = ay == 0 ? 2 : -52;
            corner.OffsetBottom = corner.OffsetTop + 46;
            board.AddChild(corner);
        }

        return root;
    }
}
