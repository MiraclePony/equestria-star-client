using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using EquestriaStar.Api;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

/// <summary>Offline inspection scene. No login, API writes, or invented gameplay.</summary>
public partial class OrigamiGallery : Node3D
{
    private GameMapScene _stage = null!;
    private Node3D _root = null!;
    private Label _description = null!;
    private OptionButton _picker = null!;
    private readonly GameMapBuilder _builder = new();
    private string? _capturePath;
    private int _captureFrames;
    private bool _enhanced = true;
    private bool _clearance;
    private bool _railSample;
    private bool _grade = true;
    private bool _comparison;
    private bool _tokenFit=true;
    private bool _referenceRail;
    private bool _photoFocus;
    private string? _statsPath;
    private readonly System.Collections.Generic.List<double> _processSamples=[];
    private HexCell3D? _selectedCell;
    private Label _selectedLabel = null!;
    private Label _zoomStatus=null!;
    private bool _detailsEnabled=true,_animationsEnabled=true;
    private string? _detailReport;
    private PanelContainer _inspector=null!;
    private ScrollContainer _inspectorScroll=null!;
    private bool _closing;
    private bool _reducedMotion,_motionFrozen,_haloEnabled=true;
    private Button _settingsButton=null!;
    private string? _motionAuditPath;
    private double _auditSeconds,_nextAudit;
    private readonly System.Collections.Generic.List<object> _audit=[];
    public PaperEnvironmentMotion? EnvironmentMotion=>_root?.GetNodeOrNull<PaperEnvironmentMotion>("GeneratedCells/PaperEnvironmentMotion");
    public void RebuildForInspection()=>ShowSelection(_picker.Selected);

    public override void _Ready()
    {
        PaperMaterialLifetime.BeginGallerySession();
        GetTree().AutoAcceptQuit=false;
        GetTree().Root.CloseRequested+=OnCloseRequested;
        _stage = GD.Load<PackedScene>("res://scenes/game/map/GameMapScene.tscn").Instantiate<GameMapScene>();
        _stage.AutoLoadOnReady = false;
        AddChild(_stage);
        _root = _stage.GetNode<Node3D>("MapRoot");
        var floor = _stage.GetNode<MeshInstance3D>("Floor");
        ((PlaneMesh)floor.Mesh).Size = new Vector2(150, 150);
        floor.Position = new Vector3(0,-0.60f,0);
        var layer = new CanvasLayer { Layer = 30 };
        AddChild(layer);
        var panel = new PanelContainer { Position = new Vector2(24,24), Theme = GD.Load<Theme>("res://assets/theme/client_theme.tres") };
        _inspector=panel;
        panel.AddToGroup("blocks_map_input");
        layer.AddChild(panel);
        _settingsButton=new Button {Name="ArtSettingsButton",Position=new Vector2(24,24),Text="美术设置 · Tab"};layer.AddChild(_settingsButton);
        _settingsButton.AddToGroup("blocks_map_input");_settingsButton.Pressed+=()=>panel.Visible=!panel.Visible;
        panel.Position=new Vector2(24,76);
        _inspectorScroll=new ScrollContainer {HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,CustomMinimumSize=new Vector2(400,Math.Max(300,Math.Min(860,GetViewport().GetVisibleRect().Size.Y-48)))};
        panel.AddChild(_inspectorScroll);
        var content = new VBoxContainer(); _inspectorScroll.AddChild(content);
        content.AddChild(new Label { Text = "小马国之星 · 折纸美术", Theme = panel.Theme });
        content.AddChild(new Label { Text = "滚轮缩放 · 鼠标拖动棋盘 · 点击地块检查选中状态" });
        _zoomStatus=new Label {Text="缩放细节准备中"};content.AddChild(_zoomStatus);
        _picker = new OptionButton { CustomMinimumSize = new Vector2(360,42) };
        content.AddChild(_picker);
        _picker.AddItem("素材总览（43组）");
        _picker.AddItem("正式地图快照（离线检查）");
        foreach (var entry in OrigamiArtCatalog.Entries)
            _picker.AddItem(entry.Name + (entry.Status == "candidate" ? " · 候选" : ""));
        _picker.AddItem("小马谷 · 地标朝向近景");
        _picker.AddItem("无尽森林 · 疏密与留位");
        _picker.AddItem("雪域 · 山脉与装饰");
        _picker.AddItem("铁路样件 · 直轨与弯轨");
        _picker.AddItem("水域 · 河流接口与池塘");
        _picker.AddItem("概念图 · 大陆近景构图");
        _picker.AddItem("新增天空饰件 · 星纹气球与卷云");
        _picker.ItemSelected += ShowSelection;
        _description = new Label { CustomMinimumSize = new Vector2(360,0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(_description);
        var buttons = new HBoxContainer(); content.AddChild(buttons);
        var reset = new Button { Text = "重置镜头" }; buttons.AddChild(reset);
        reset.Pressed += () => _stage.CameraController.ResetToInitialView();
        _selectedLabel=new Label {Text="点击地标，可试转朝向"};content.AddChild(_selectedLabel);
        var rotations=new HBoxContainer();content.AddChild(rotations);
        foreach(var direction in new[]{-1,1})
        {
            var rotate=new Button {Text=direction<0?"左转60°":"右转60°"};rotations.AddChild(rotate);
            rotate.Pressed+=()=>RotateSelected(direction*60);
        }
        var save=new Button {Text="保存本地朝向"};rotations.AddChild(save);
        save.Pressed+=()=>_selectedLabel.Text=OrigamiMapPresentation.SaveOrientations()==Error.Ok?"已保存本地朝向；未上传":"保存失败，请检查项目写入权限";
        void Toggle(string text, bool state, Action<bool> change,bool rebuild=true)
        {
            var button = new CheckButton {Text=text,ButtonPressed=state}; content.AddChild(button);
            button.Toggled += value => {change(value);if(rebuild)ShowSelection(_picker.Selected);};
        }
        var args = OS.GetCmdlineUserArgs();
        PaperWorldBackground.Enabled=!args.Contains("--art-v4-baseline");
        panel.Visible=args.Contains("--art-show-panel") && !args.Contains("--art-clean");_settingsButton.Visible=!args.Contains("--art-clean");
        var selection = 1;
        for (var i=0; i<args.Length-1; i++)
        {
            if (args[i] == "--art-capture") _capturePath = args[i+1];
            if (args[i] == "--art-view") int.TryParse(args[i+1], out selection);
            if (args[i] == "--art-stats") _statsPath=args[i+1];
            if (args[i] == "--art-detail-report") _detailReport=args[i+1];
            if (args[i] == "--art-motion-audit") _motionAuditPath=args[i+1];
        }
        _enhanced = !args.Contains("--art-baseline");
        _grade = !args.Contains("--art-no-grade") && _enhanced;
        _clearance = args.Contains("--art-clearance");
        _railSample = args.Contains("--art-rail");
        _comparison = args.Contains("--art-comparison");
        _tokenFit = !args.Contains("--art-source-layout");
        _referenceRail=args.Contains("--art-reference-rail") || selection==OrigamiArtCatalog.Entries.Count+7;
        _photoFocus=args.Contains("--art-diorama");
        _reducedMotion=args.Contains("--paper-reduced");_motionFrozen=args.Contains("--paper-freeze");_haloEnabled=!args.Contains("--paper-no-halo");
        var lightProfile=new Label {Text=RenderingServer.GetCurrentRenderingMethod()+(ArtRenderProfile.IsSunlit?" · P9 阳光折纸":" · V5 D / D+纸艺世界")};content.AddChild(lightProfile);
        var palettes=new GridContainer {Columns=3};content.AddChild(palettes);
        foreach(var (label,index) in new[]{("V3/A",0),("B清透",1),("C午后",2),("V4 D",3),("V4鲜明",4),("V5 D",5),("D+",6),("P7强光影",7),("P8电影光",8),("P9阳光折纸",9)})
        {var b=new Button {Text=label};palettes.AddChild(b);b.Pressed+=()=>
            {var transform=_stage.CameraController.Camera.GlobalTransform;var size=_stage.CameraController.Camera.Size;
             lightProfile.Text=ArtRenderProfile.Preset(_stage,index);ShowSelection(_picker.Selected);
             _stage.CameraController.Camera.GlobalTransform=transform;_stage.CameraController.Camera.Size=size;};}
        Toggle("大陆背景与错落布置",_enhanced,v=>_enhanced=v);
        Toggle("旧版背景对照",!PaperWorldBackground.Enabled,v=>PaperWorldBackground.Enabled=!v);
        Toggle("纸艺调色（保护白纸）",_grade,v=>{_grade=v;_stage.PaperEffect.Visible=v;},false);
        Toggle("棋子净空与移动检查",_clearance,v=>_clearance=v);
        Toggle("棋子落点与地标适配",_tokenFit,v=>_tokenFit=v);
        Toggle("铁路样件（路线待核对）",_railSample,v=>_railSample=v);
        Toggle("棋盘参考铁路草稿",_referenceRail,v=>_referenceRail=v);
        Toggle("微缩景深（拍照预览）",_photoFocus,v=>_photoFocus=v,false);
        Toggle("缩放细节装饰",true,v=>_detailsEnabled=v,false);
        Toggle("环境动画",true,v=>_animationsEnabled=v,false);
        Toggle("减少动态",_reducedMotion,v=>{_reducedMotion=v;if(EnvironmentMotion!=null)EnvironmentMotion.Reduced=v;},false);
        Toggle("冻结环境（拍照）",_motionFrozen,v=>{_motionFrozen=v;if(EnvironmentMotion!=null)EnvironmentMotion.Frozen=v;},false);
        Toggle("局部魔法光晕",_haloEnabled,v=>{_haloEnabled=v;if(EnvironmentMotion!=null)EnvironmentMotion.HaloEnabled=v;},false);
        var zooms=new HBoxContainer();content.AddChild(zooms);
        foreach(var (label,ratio) in new[]{("总览",0f),("区域",.10f),("近景",.23f),("展示",.40f)})
        {
            var button=new Button {Text=label};zooms.AddChild(button);
            button.Pressed+=()=>FocusScale(ratio);
        }
        selection = Math.Clamp(selection,0,_picker.ItemCount-1);
        _picker.Select(selection);
        ShowSelection(selection);
        if(selection==50 && !args.Contains("--art-scale"))
        {if(_builder.CellsByCode.TryGetValue("A1",out var workCentre))_selectedCell=workCentre;FocusScale(.10f,true);}
        var focus=Array.IndexOf(args,"--art-focus");var scale=Array.IndexOf(args,"--art-scale");
        if(focus>=0 && focus+1<args.Length && _builder.CellsByCode.TryGetValue(args[focus+1],out var chosen))_selectedCell=chosen;
        if(scale>=0 && scale+1<args.Length && float.TryParse(args[scale+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var requestedRatio))FocusScale(requestedRatio,true);
    }

    private void ShowSelection(long selection)
    {
        var previousTime=EnvironmentMotion?.Clock??0;
        var previousWeight=EnvironmentMotion?.MotionWeight??(_animationsEnabled?1:0);
        _builder.Clear(_root);
        _selectedCell=null;
        _selectedLabel.Text="点击地标，可试转朝向";
        _stage.PaperEffect.Visible = _grade;
        _stage.GetNode<MeshInstance3D>("Floor").Position = new Vector3(0,_enhanced ? -.60f : -.23f,0);
        if(selection==OrigamiArtCatalog.Entries.Count+8)
        {
            SkyAssetPreview.Add(_root);_stage.CameraController.InitializeMap(new Aabb(new Vector3(-5.5f,-.2f,-5.5f),new Vector3(11,3,11)));
            _description.Text="BG101—109新交付 · 本地试排\n星纹气球、立体卷云、纸太阳、彩虹和纸鸟。\n保留分件；待美术验收，未上传。";return;
        }
        if(selection==OrigamiArtCatalog.Entries.Count+6)
        {
            WaterModulePreview.Add(_root);
            _stage.CameraController.InitializeMap(new Aabb(new Vector3(-3.8f,-.15f,-5.2f),new Vector3(9,1.4f,8.7f)));
            _description.Text="第二批水域实际GLB\nS弯 → 直河 → 河口按交付端口对接。\n此页检查接口，正式大陆水系尚待布置。";
            return;
        }
        if(selection==OrigamiArtCatalog.Entries.Count+5)
        {
            RailStylePreview.Add(_root,Vector3.Zero,1);
            _stage.CameraController.InitializeMap(new Aabb(new Vector3(-2,-.15f,-.6f),new Vector3(4,.4f,3)));
            _description.Text="奶油双轨与粉金枕木 · 本地样件\n轨距和跨格接口仍需与列车、车站核对。\n正式铁路路径尚未布置。";
            return;
        }
        if (selection == 1 || selection >= OrigamiArtCatalog.Entries.Count+2)
        {
            using var document = JsonDocument.Parse(FileAccess.GetFileAsString("res://tests/fixtures/main-map-20261024.json"));
            var map = JsonSerializer.Deserialize<GameMapDto>(document.RootElement.GetProperty("data").GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var result = _builder.Build(map,_root,GD.Load<PackedScene>("res://scenes/game/map/HexCell3D.tscn"),.55f,.11f,.025f,false,true,_enhanced,_tokenFit && _enhanced);
            if (!result.Ok) { _description.Text = result.Error; return; }
            if(EnvironmentMotion is {} movement){movement.Reduced=_reducedMotion;movement.HaloEnabled=_haloEnabled;movement.RestoreState(previousTime,_animationsEnabled,_motionFrozen,previousWeight);}
            foreach (var cell in _builder.CellsById.Values) WireCell(cell);
            _stage.CameraController.InitializeMap(result.Bounds);
            if(_comparison && selection==1)
            {
                var all=_builder.CellsById.Values.ToArray();
                var min=new Vector3(all.Min(c=>c.Position.X)-2.8f,-.5f,all.Min(c=>c.Position.Z)-2.5f);
                var max=new Vector3(all.Max(c=>c.Position.X)+2.8f,2.5f,all.Max(c=>c.Position.Z)+3.0f);
                _stage.CameraController.InitializeMap(new Aabb(min,max-min));
            }
            if(selection==OrigamiArtCatalog.Entries.Count+7)
            {
                var all=_builder.CellsById.Values.ToArray();
                var min=new Vector3(all.Min(c=>c.Position.X)-.75f,-.4f,all.Min(c=>c.Position.Z)-.65f);
                var max=new Vector3(all.Max(c=>c.Position.X)+.75f,1.75f,all.Max(c=>c.Position.Z)+.35f);
                _stage.CameraController.InitializeMap(new Aabb(min,max-min));
                _stage.CameraController.ApplyZoomStep(true);
                _stage.CameraController.ApplyZoomStep(true);
            }
            else if(selection>=OrigamiArtCatalog.Entries.Count+2)
            {
                var board=(selection-OrigamiArtCatalog.Entries.Count-2) switch {0=>"A",1=>"B",_=>"C"};
                var chosen=_builder.CellsById.Values.Where(c=>c.Data!.BoardCode==board).ToArray();
                var min=new Vector3(chosen.Min(c=>c.Position.X)-.9f,-.15f,chosen.Min(c=>c.Position.Z)-.9f);
                var max=new Vector3(chosen.Max(c=>c.Position.X)+.9f,1.7f,chosen.Max(c=>c.Position.Z)+.9f);
                if(board=="C") {min.X-=1.2f;min.Z-=1.6f;max.Y+=.3f;}
                _stage.CameraController.InitializeMap(new Aabb(min,max-min));
            }
            var installed = _builder.CellsById.Values.Count(c=>c.UsesCustomVisual);
            _description.Text = $"本地美术试排 · {result.CellCount} 格\n{installed} 格地标/森林；环境与候选外观待审阅。\n可关闭背景、滤镜进行对比；未上传。";
            if(_referenceRail)
            {
                ReferenceRailDraft.Add(_root,_builder.CellsByCode);
                _description.Text+="\n铁路为棋盘图描线草稿，站台端口尚待核对。";
            }
            if(_railSample)
                RailStylePreview.Add(_root,new Vector3(result.Bounds.GetCenter().X,-.10f,result.Bounds.End.Z-.65f),.8f);
            if(_clearance)
            {
                var probe=new TokenClearancePreview {Name="TokenClearancePreview"};_root.AddChild(probe);
                probe.Build(_builder.CellsById,_builder.Edges);
                _description.Text += $"\n{probe.FlaggedCells} 格网格占位预警 · 移动检查{probe.SweepWarnings.Count}处。";
                if(_capturePath!=null)probe.SaveReport("res://artifacts/token-clearance.json");
            }
            return;
        }
        var entries = selection == 0 ? OrigamiArtCatalog.Entries.ToArray() : new[] { OrigamiArtCatalog.Entries[(int)selection-2] };
        var cellScene = GD.Load<PackedScene>("res://scenes/game/map/HexCell3D.tscn");
        float maxHeight = 0;
        for (var i=0; i<entries.Length; i++)
        {
            var entry=entries[i];
            var cell=cellScene.Instantiate<HexCell3D>();
            cell.ConfigureGeometry(1,.2f,.025f);
            cell.BindData(new HexCellData { CellId=i+1,CellCode=entry.Id,CellName=entry.Name });
            cell.Position=new Vector3((i%8)*2.5f,0,(i/8)*2.7f);
            maxHeight=Math.Max(maxHeight,OrigamiArtCatalog.Apply(cell,entry));
            _root.AddChild(cell);
            WireCell(cell);
            if (selection==0)
            {
                var label=new Label3D { Text=entry.Name.Replace("_候选方案","*"),FontSize=36,PixelSize=.0028f,
                    Modulate=new Color("32473f"),OutlineModulate=new Color("fff6df"),OutlineSize=6,
                    Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Position=new Vector3(0,.15f,1.05f),NoDepthTest=true };
                cell.AddChild(label);
            }
        }
        _stage.CameraController.InitializeMap(new Aabb(new Vector3(-1,-.2f,-1),new Vector3(selection==0?19.5f:2,maxHeight+.2f,selection==0?15.5f:2)));
        if(selection!=0) _stage.GetNode<Camera3D>("CameraRig/Camera3D").Size=5;
        _description.Text = selection==0 ? "43组当前交付 · 地块 / 建筑 / 装饰独立\n候选稿仅在此展示；几何细节保持原制作版本。" :
            $"{entries[0].Name}\n{entries[0].Components.Count}个独立组件 · 纸边和折面保留\n"+(entries[0].Status=="candidate"?"候选方案，未自动放入正式地图。":"已接入；造型仍以原交付审阅状态为准。");
    }

    private void WireCell(HexCell3D cell) => cell.CellClicked += _ =>
    {
        if(GodotObject.IsInstanceValid(_selectedCell))_selectedCell!.SetSelected(false);
        _selectedCell=cell;cell.SetSelected(true);
        _selectedLabel.Text=cell.Data!.DisplayName+" · 附加朝向 "+cell.CustomVisualYawDegrees+"°";
    };

    private void RotateSelected(float delta)
    {
        if(!GodotObject.IsInstanceValid(_selectedCell))return;
        var selected=_selectedCell!;
        var entry=OrigamiArtCatalog.Resolve(selected.Data!,true);
        if(entry==null){_selectedLabel.Text="此格没有地标模型";return;}
        var yaw=selected.CustomVisualYawDegrees+delta;
        OrigamiMapPresentation.SetOrientation(entry.Id,yaw);
        foreach(var cell in _root.FindChildren("*","Node3D",true,false).OfType<HexCell3D>())
        {
            if(OrigamiArtCatalog.Resolve(cell.Data!,true)?.Id!=entry.Id)continue;
            cell.CustomVisualYawDegrees=yaw;
            cell.GetNode<Node3D>("VisualRoot/CustomVisualRoot").GetChild<Node3D>(0).RotationDegrees=new Vector3(0,30+yaw,0);
            if(_tokenFit && (_picker.Selected==1 || _picker.Selected>=OrigamiArtCatalog.Entries.Count+2))PawnArtLayout.Apply(cell,entry);
        }
        _selectedLabel.Text=entry.Name+" · 附加朝向 "+yaw+"°（可保存）";
    }

    private void FocusScale(float ratio,bool immediate=false)
    {
        if(ratio==0){_stage.CameraController.ResetToInitialView();return;}
        var cell=GodotObject.IsInstanceValid(_selectedCell)?_selectedCell:_builder.CellsById.Values.FirstOrDefault(c=>OrigamiArtCatalog.Resolve(c.Data!)?.Id=="everfree");
        if(cell==null)return;
        var camera=_stage.CameraController.Camera;
        var actual=ScreenDetailPolicy.Ratio(camera,cell.GlobalPosition,cell.Radius,camera.GetViewport().GetVisibleRect());
        _stage.CameraController.FocusGround(cell.GlobalPosition,camera.Size*actual/ratio,immediate);
    }

    public override void _Process(double delta)
    {
        _settingsButton.Text=_inspector.Visible?"收起设置 · Tab":"美术设置 · Tab";
        if(_motionAuditPath!=null && EnvironmentMotion is {} auditMotion)
        {
            _auditSeconds+=delta;
            if(_auditSeconds>=_nextAudit)
            {_nextAudit+=.20;_audit.Add(new {seconds=_auditSeconds,clock=auditMotion.Clock,enabled=auditMotion.Enabled,frozen=auditMotion.Frozen,camera=_stage.CameraController.Camera.GlobalTransform.ToString(),settingsVisible=_inspector.Visible,balloons=auditMotion.Aerial.Where(m=>m.IsBalloon).Select(m=>m.Diagnose(_stage.CameraController.Camera)).ToArray()});}
            if(_auditSeconds>=8.4)
            {
                using var f=FileAccess.Open(_motionAuditPath,FileAccess.ModeFlags.Write);f.StoreString(JsonSerializer.Serialize(new {source="actual interactive OrigamiGallery; automatic process; optional audit stops after 8.4 seconds",renderer=RenderingServer.GetCurrentRenderingMethod(),snapshots=_audit},new JsonSerializerOptions{WriteIndented=true}));
                using var im=GetViewport().GetTexture().GetImage();im.SavePng(_motionAuditPath.Replace(".json",".png"));_motionAuditPath=null;_audit.Clear();
            }
        }
        ArtRenderProfile.PhotoFocus(_stage.CameraController.Camera,_photoFocus);
        var detail=_stage.DetailController;
        if(detail!=null)
        {
            _inspectorScroll.CustomMinimumSize=new Vector2(400,Math.Max(300,Math.Min(860,GetViewport().GetVisibleRect().Size.Y-48)));
            detail.ViewInsets=new Vector4(_inspector.Visible?_inspector.GetGlobalRect().End.X+12:0,0,0,0);
            detail.DetailsEnabled=_detailsEnabled;detail.AnimationsEnabled=_animationsEnabled;
            _zoomStatus.Text=$"Z{detail.Stage} · 占屏{detail.ScreenRatio:P1} · 近景{detail.ResidentCellCount}格 / 资源{detail.Cache.ResidentCount} · 等待{detail.Cache.LoadingCount} / 失败{detail.Cache.FailedCount}";
        }
        else _zoomStatus.Text="独立素材检查 · 无棋盘缩放档";
        if (_capturePath == null)return;
        _captureFrames++;
        if(_statsPath!=null && _captureFrames>120)_processSamples.Add(Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000);
        if(_captureFrames<(_statsPath==null?60:180))return;
        using var capture=GetViewport().GetTexture().GetImage();
        capture.SavePng(_capturePath);
        if(_detailReport!=null && detail!=null)
        {using var file=FileAccess.Open(_detailReport,FileAccess.ModeFlags.Write);file.StoreString(JsonSerializer.Serialize(detail.Snapshot(),new JsonSerializerOptions{WriteIndented=true}));}
        if(_statsPath!=null)
        {
            var sorted=_processSamples.OrderBy(x=>x).ToArray();
            using var report=FileAccess.Open(_statsPath,FileAccess.ModeFlags.Write);
            report.StoreString(JsonSerializer.Serialize(new {renderer=RenderingServer.GetCurrentRenderingMethod(),device=RenderingServer.GetVideoAdapterName(),
                resolution=new[]{capture.GetWidth(),capture.GetHeight()},
                logicalViewport=new[]{GetViewport().GetVisibleRect().Size.X,GetViewport().GetVisibleRect().Size.Y},
                draws=Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),primitives=Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
                videoMemoryBytes=Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed),medianCpuProcessMs=sorted[sorted.Length/2],
                tinyShadowCastersSkipped=_root.GetNodeOrNull<Node3D>("GeneratedCells")?.GetMeta("tiny_shadow_casters_disabled",0).AsInt32()??0,
                note="One local scene after 120 warmup frames, 60 CPU process samples, VSync enabled. Not GPU frame time or a mobile performance benchmark."},new JsonSerializerOptions {WriteIndented=true}));
        }
        GD.Print($"ART_CAPTURE {_capturePath}");
        _=FinishAndQuit();
    }
    private void OnCloseRequested()=>_=FinishAndQuit();
    public override void _UnhandledKeyInput(InputEvent e)
    {if(e is InputEventKey {Pressed:true,Echo:false} key && key.PhysicalKeycode==Key.Tab){_inspector.Visible=!_inspector.Visible;GetViewport().SetInputAsHandled();}}
    public void FinishInspection(int exitCode=0)=>_=FinishAndQuit(exitCode);
    private async Task FinishAndQuit(int exitCode=0)
    {
        if(_closing)return;_closing=true;SetProcess(false);
        void Trace(string step)
        {
            var path=ProjectSettings.GlobalizePath("res://artifacts/paper-v5-shutdown-trace.log");
            if(!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(path)))return;
            try{System.IO.File.AppendAllText(path,$"{DateTimeOffset.UtcNow:O} {RenderingServer.GetCurrentRenderingMethod()} {step}\n");}
            catch(System.IO.IOException){}catch(UnauthorizedAccessException){}
        }
        Trace("begin");
        var tree=GetTree();PaperMaterialLifetime.Collect(this);_selectedCell=null;_builder.Clear(_root);QueueFree();
        // Detach geometry and let the render thread finish before releasing shared materials.
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Trace("scene detached / draw completed");
        ConceptPaperStyle.ReleaseAtShutdown();
        OrigamiArtCatalog.ReleaseSceneCache();EnvironmentArtCatalog.ReleaseSceneCache();DetailAssetCache.Shared.ClearAtShutdown();
        HexCellMeshFactory.ReleaseAtShutdown();MapLayoutConfig.ReleaseAtShutdown();
        RenderingServer.ForceSync();
        Trace("caches released / render sync");
        var released=PaperMaterialLifetime.ReleaseDetached();
        Trace("material handles released "+released);
        GD.Print("ART_DETACHED_MATERIAL_HANDLES_RELEASED "+released);
        // Run pending managed resource finalizers before Godot tears down RenderingServer.
        // Keep the engine thread pumping while deferred Godot wrappers finalize. Waiting
        // synchronously here can block native disposal that itself needs that thread.
        for(var i=0;i<3;i++){Trace("collect "+i);GC.Collect();await System.Threading.Tasks.Task.Run(GC.WaitForPendingFinalizers);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);Trace("finalizers completed "+i);}
        RenderingServer.ForceSync();
        Trace("quit");
        tree.Quit(exitCode);
    }
    public override void _ExitTree()=>GetTree().Root.CloseRequested-=OnCloseRequested;
}
