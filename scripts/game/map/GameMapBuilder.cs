using System;
using System.Collections.Generic;
using System.Linq;
using EquestriaStar.Api;
using Godot;

namespace EquestriaStar.Game.Map;

public sealed record MapBuildResult(bool Ok, string Error, int CellCount, int EdgeCount, Aabb Bounds)
{
    public static MapBuildResult Failure(string error)
    {
        return new MapBuildResult(false, error, 0, 0, default);
    }
}

public sealed class GameMapBuilder
{
    private Node3D? _generatedRoot;
    private Dictionary<long, HexCell3D> _cellsById = new();
    private Dictionary<string, HexCell3D> _cellsByCode = new(StringComparer.Ordinal);
    private IReadOnlyList<MapEdgeDto> _edges = Array.Empty<MapEdgeDto>();

    public IReadOnlyDictionary<long, HexCell3D> CellsById => _cellsById;
    public IReadOnlyDictionary<string, HexCell3D> CellsByCode => _cellsByCode;
    public IReadOnlyList<MapEdgeDto> Edges => _edges;

    public MapBuildResult Build(
        GameMapDto map,
        Node3D mapRoot,
        PackedScene cellScene,
        float radius,
        float height,
        float bevelWidth,
        bool showDebugLabels,
        bool useOrigamiArt = true,
        bool useEnvironmentArt = true,
        bool useTokenLayout = true
    )
    {
        var validation = MapDataValidator.Validate(map);
        if (!validation.IsValid)
        {
            return MapBuildResult.Failure(validation.Error);
        }

        var safeRadius = Math.Max(0.1f, radius);
        var detailCamera=mapRoot.GetParent() is GameMapScene stage?stage.CameraController?.Camera:null;
        var safeHeight = Math.Max(0.02f, height);
        var newRoot = new Node3D { Name = "GeneratedCells" };
        var newCellsById = new Dictionary<long, HexCell3D>();
        var newCellsByCode = new Dictionary<string, HexCell3D>(StringComparer.Ordinal);
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minZ = float.MaxValue;
        var maxZ = float.MinValue;
        var maxVisualHeight = 0.0f;

        try
        {
            foreach (var dto in map.Cells!)
            {
                var q = dto.Q!.Value;
                var r = dto.R!.Value;
                var cell = cellScene.Instantiate<HexCell3D>();
                cell.Name = dto.CellCode;
                cell.Radius = safeRadius;
                cell.Height = safeHeight;
                cell.BevelWidth = bevelWidth;
                cell.TopMaterial = useOrigamiArt && useEnvironmentArt ? EquestriaStar.Art.ConceptPaperStyle.Ground(dto.BoardCode) : MapLayoutConfig.GetTopMaterial(dto.BoardCode);
                cell.SideMaterial = MapLayoutConfig.GetSideMaterial(dto.BoardCode);
                cell.ShowDebugLabel = showDebugLabels;
                cell.Position = HexCoordinateConverter.ToWorld(q, r, dto.BoardCode, safeRadius);

                var tags = new Godot.Collections.Array<string>();
                if (dto.InteractionTags != null)
                {
                    foreach (var tag in dto.InteractionTags)
                    {
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            tags.Add(tag);
                        }
                    }
                }

                cell.BindData(new HexCellData
                {
                    CellId = dto.CellId,
                    CellCode = dto.CellCode,
                    BoardCode = dto.BoardCode,
                    CellName = dto.CellName,
                    CellNameText = dto.CellNameText,
                    Q = q,
                    R = r,
                    InteractionTags = tags
                });

                var art = useOrigamiArt ? OrigamiArtCatalog.Resolve(cell.Data!) : null;
                if (art != null)
                {
                    if (useEnvironmentArt) cell.CustomVisualYawDegrees = OrigamiMapPresentation.Orientation(art, cell.Data!);
                    maxVisualHeight = Math.Max(maxVisualHeight, OrigamiArtCatalog.Apply(cell, art));
                }

                var debugLabel = cell.GetNode<Label3D>("DebugLabel");
                debugLabel.FontSize = 34;
                debugLabel.OutlineSize = 8;
                debugLabel.PixelSize = 0.0024f;
                debugLabel.Position = new Vector3(0.0f, safeHeight + 0.06f, 0.0f);

                newRoot.AddChild(cell);
                newCellsById.Add(dto.CellId, cell);
                newCellsByCode.Add(dto.CellCode, cell);
                minX = Math.Min(minX, cell.Position.X - safeRadius);
                maxX = Math.Max(maxX, cell.Position.X + safeRadius);
                minZ = Math.Min(minZ, cell.Position.Z - safeRadius);
                maxZ = Math.Max(maxZ, cell.Position.Z + safeRadius);
            }
        }
        catch (Exception exception)
        {
            newRoot.Free();
            return MapBuildResult.Failure($"实例化地图格子失败：{exception.Message}");
        }

        Clear(mapRoot);
        mapRoot.AddChild(newRoot);
        _generatedRoot = newRoot;
        _cellsById = newCellsById;
        _cellsByCode = newCellsByCode;
        _edges = map.Edges!.AsReadOnly();
        if (useOrigamiArt && useEnvironmentArt)
            foreach (var cell in _cellsById.Values)
            {
                OrigamiMapPresentation.Decorate(cell, OrigamiArtCatalog.Resolve(cell.Data!),detailCamera!=null);
                if(useTokenLayout)PawnArtLayout.Apply(cell,OrigamiArtCatalog.Resolve(cell.Data!));
                EquestriaStar.Art.PaperFoldGeometry.Apply(cell.GetNode<Node3D>("VisualRoot"));
                if(EquestriaStar.Art.ArtRenderProfile.IsFairy)
                {EquestriaStar.Art.PaperFoldGeometry.PrepareWind(cell.GetNode<Node3D>("VisualRoot"));EquestriaStar.Art.PaperFlagMotion.Prepare(cell.GetNode<Node3D>("VisualRoot"));}
                EquestriaStar.Art.PaperGroundContact.Apply(cell);
                EquestriaStar.Art.ConceptPaperStyle.ApplyCell(cell);
                EquestriaStar.Art.ConceptPaperStyle.Apply(cell.GetNode<Node3D>("VisualRoot/DecorationRoot"));
            }
        var bounds = new Aabb(
            new Vector3(minX, -safeHeight, minZ),
            new Vector3(maxX - minX, safeHeight + maxVisualHeight, maxZ - minZ)
        );
        if (useOrigamiArt && useEnvironmentArt)
        {
            bounds = OrigamiMapPresentation.AddContinent(newRoot, _cellsById.Values.ToArray(), bounds, safeRadius,_edges);
            var scenery=newRoot.GetNodeOrNull<Node3D>("ContinentalScenery");
            if(scenery!=null)EquestriaStar.Art.ConceptPaperStyle.Apply(scenery);
            if(EquestriaStar.Art.ArtRenderProfile.IsFairy)EquestriaStar.Art.PaperPlaceComposition.Build(newRoot,_cellsById.Values.ToArray());
            var environmentMotion=new EquestriaStar.Art.PaperEnvironmentMotion {Name="PaperEnvironmentMotion"};newRoot.AddChild(environmentMotion);
            environmentMotion.Configure(mapRoot,_cellsById.Values.ToArray(),detailCamera);
            var smallShadows=0;
            foreach(var cell in _cellsById.Values)
            {
                var imported=cell.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset/Decoration");
                var added=cell.GetNodeOrNull<Node3D>("VisualRoot/DecorationRoot/LocalVegetation");
                if(imported!=null)smallShadows+=SceneryShadowBudget.Apply(imported,safeRadius);
                if(added!=null)smallShadows+=SceneryShadowBudget.Apply(added,safeRadius);
            }
            newRoot.SetMeta("tiny_shadow_casters_disabled",smallShadows);
            if(detailCamera!=null)
            {
                var details=new ZoomArtDetailController {Name="ZoomArtDetailController"};newRoot.AddChild(details);
                details.Configure(detailCamera,_cellsById.Values,mapRoot,_edges);
            }
        }
        return new MapBuildResult(true, "", _cellsById.Count, _edges.Count, bounds);
    }

    public void Clear(Node3D mapRoot)
    {
        foreach (var child in mapRoot.GetChildren())
        {
            mapRoot.RemoveChild(child);
            child.QueueFree();
        }

        _generatedRoot = null;
        _cellsById = new Dictionary<long, HexCell3D>();
        _cellsByCode = new Dictionary<string, HexCell3D>(StringComparer.Ordinal);
        _edges = Array.Empty<MapEdgeDto>();
    }
}
