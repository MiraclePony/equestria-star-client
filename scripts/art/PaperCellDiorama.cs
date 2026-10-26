using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EquestriaStar.Game.Map;

namespace EquestriaStar.Art;

/// <summary>
/// Close-zoom diorama (P9). When a cell fills enough of the screen, ZoomArtDetailController asks
/// for a richer, themed set dressing of that cell — the Everfree becomes a real forest the pawns
/// stand in, Ponyville gets flower beds and fences, Manehattan small streets, the Dragon Lands lava
/// cracks, and so on (strategy-map style zoom tiers, not mesh LOD). Claude Code, 2026-10-26.
///
/// Rules: purely cosmetic, deterministic per cell, never on the pawn landing disc, only around the
/// rim of landmark cells and never inside a landmark building's footprint.
/// </summary>
public static class PaperCellDiorama
{
    public static bool Enabled => ArtRenderProfile.IsSunlit;
    /// <summary>Free radius around the pawn anchor, as a fraction of the cell radius.</summary>
    public const float PawnClearing = .34f;

    private sealed record Item(string Id, float Size, int Count, float Inner = .40f, float Outer = .90f, float Y = 0, float MaxHeight = 0);

    private static Item[] Theme(string board, bool forest) => forest ? [
            new("@thorn", .34f, 10, .37f, .96f),
            new("VEG006|VEG007|VEG008|VEG009", .26f, 24, .36f, .97f, 0, .46f),
            new("@moss", .30f, 5, .36f, .92f),
            new("@shroom", .07f, 6, .36f, .9f),
            new("VEG104", .12f, 9, .36f, .92f, 0, .14f),
            new("DEC101", .08f, 8, .36f, .92f, 0, .10f),
        ] : board switch
        {
            "A" => [new("DEC102", .08f, 12), new("VEG105", .11f, 4), new("VEG005", .17f, 2, .55f), new("DEC104", .12f, 2, .6f, .90f, 0, .14f), new("DEC105", .14f, 1, .45f)],
            "C" => [new("VEG103", .15f, 7), new("DEC001|DEC002|DEC003|DEC004|DEC005", .09f, 6), new("@snow", .16f, 5)],
            "D" => [new("DEC103", .11f, 8), new("VEG004", .15f, 2, .6f), new("@crag", .14f, 3, .55f)],
            "E" => [new("@tower", .13f, 7, .45f), new("DEC104", .10f, 3, .40f, .90f, 0, .14f)],
            "F" => [new("VEG105", .11f, 5), new("DEC102", .075f, 6), new("DEC104", .10f, 3, .40f, .90f, 0, .14f), new("VEG101", .15f, 2, .6f)],
            "G" => [new("@house", .12f, 6, .45f), new("DEC103", .09f, 3)],
            "H" => [new("@lava", .26f, 3, .40f, .85f), new("DEC103", .10f, 6), new("@ember", .09f, 3)],
            "I" => [new("DEC103", .09f, 4), new("VEG004", .15f, 3), new("@pool", .26f, 1, .55f, .75f)],
            "J" => [new("@mesa", .13f, 4), new("DEC103", .09f, 5), new("VEG004", .14f, 1, .6f)],
            "K" => [new("DEC102", .075f, 7), new("VEG101", .15f, 3), new("@pool", .24f, 1, .55f, .75f)],
            "L" => [new("@cloud", .17f, 6, .40f, .92f), new("DEC102", .07f, 3)],
            _ => [new("DEC102", .075f, 5), new("VEG105", .10f, 3)],
        };

    public static Node3D Build(HexCell3D cell)
    {
        var r = cell.Radius;
        var root = new Node3D { Name = "PaperCloseDiorama" };
        cell.GetNode<Node3D>("VisualRoot/DecorationRoot").AddChild(root);
        var art = OrigamiArtCatalog.Resolve(cell.Data!);
        var forest = art?.Id == "everfree";
        var landmark = art != null && !forest;
        var rng = new RandomNumberGenerator { Seed = OrigamiMapPresentation.Seed(cell.Data!.CellCode) * 7919UL + 17 };
        var anchor = PawnArtLayout.Anchor(cell); anchor.Y = 0;
        // Landmark footprint, in cell space, keeps the building clear.
        Rect2? building = null;
        var b = cell.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset/Building");
        if (landmark && b != null)
        {
            var box = LocalBounds(cell, b);
            if (box is { } bb) building = new Rect2(bb.Position.X, bb.Position.Z, bb.Size.X, bb.Size.Z).Grow(r * .05f);
        }
        // pieces stand on the card's real top (the Everfree terrain is a raised forest floor)
        var groundY = 0f;
        var terrainNode = cell.GetNodeOrNull<Node3D>("VisualRoot/CustomVisualRoot/OrigamiAsset/Terrain");
        if (terrainNode != null && LocalBounds(root, terrainNode) is { } tb) groundY = Math.Max(0, tb.End.Y);
        var placed = new List<(Vector2 P, float R)>();
        var procedural = new SurfaceTool(); procedural.Begin(Mesh.PrimitiveType.Triangles);
        var anyProcedural = false;
        foreach (var item in Theme(cell.Data.BoardCode, forest))
        {
            var count = landmark ? Math.Max(1, item.Count / 2) : item.Count;
            var inner = landmark ? Math.Max(item.Inner, .68f) : item.Inner;
            for (int k = 0, tries = 0; k < count && tries < count * 14; tries++)
            {
                var angle = rng.RandfRange(0, Mathf.Tau);
                var dist = Mathf.Sqrt(rng.RandfRange(inner * inner, item.Outer * item.Outer)) * r;
                var p = new Vector3(Mathf.Cos(angle) * dist, item.Y * r + groundY, Mathf.Sin(angle) * dist);
                if (!InsideHex(p, r * .97f)) continue;
                var size = item.Size * r * rng.RandfRange(.8f, 1.2f);
                var flat = new Vector2(p.X, p.Z);
                if (flat.DistanceTo(new Vector2(anchor.X, anchor.Z)) < r * PawnClearing + size * .5f) continue;
                if (building is { } fp && fp.Grow(size * .5f).HasPoint(flat)) continue;
                if (placed.Any(o => o.P.DistanceTo(flat) < (o.R + size * .5f) * .85f)) continue;
                var yaw = rng.RandfRange(0, 360);
                if (item.Id.StartsWith('@')) { Procedural(procedural, item.Id, p, size, yaw, r, rng); anyProcedural = true; }
                else
                {
                    var ids = item.Id.Split('|');
                    var id = ids[rng.RandiRange(0, ids.Length - 1)];
                    if (!EnvironmentArtCatalog.Entries.TryGetValue(id, out var entry)) continue;
                    var parts = entry.Components.Keys.ToArray();
                    if (Put(root, id, parts[rng.RandiRange(0, parts.Length - 1)], p, size, yaw, (item.MaxHeight > 0 ? item.MaxHeight : item.Size * 2.2f) * r) == null) continue;
                }
                placed.Add((flat, size * .5f)); k++;
            }
        }
        if (anyProcedural)
        {
            var mesh = new MeshInstance3D { Name = "PaperDioramaShapes", Mesh = procedural.Commit() };
            mesh.MaterialOverride = OrigamiMapPresentation.PaperMaterial(Colors.White);
            root.AddChild(mesh);
        }
        ConceptPaperStyle.Apply(root);
        SceneryShadowBudget.Apply(root, r);
        root.SetMeta("diorama_items", placed.Count);
        return root;
    }

    /// <summary>Place one asset part sized by its footprint (fraction-free world size) with its base on the card.</summary>
    private static Node3D? Put(Node3D root, string id, string part, Vector3 at, float footprint, float yaw, float maxHeight)
    {
        var node = EnvironmentArtCatalog.PlaceParts(root, id, [part], at, 1, yaw);
        if (node == null) return null;
        var box = LocalBounds(root, node);
        if (box is not { } b || Math.Max(b.Size.X, b.Size.Z) < 1e-5f) { node.Free(); return null; }
        // thin tall parts (posts, lamps) are capped by height so they never tower over the pawns
        node.Scale *= Math.Min(footprint / Math.Max(b.Size.X, b.Size.Z), maxHeight / Math.Max(b.Size.Y, 1e-5f));
        if (LocalBounds(root, node) is { } scaled) node.Position += Vector3.Up * (at.Y - scaled.Position.Y);
        return node;
    }

    private static Aabb? LocalBounds(Node3D space, Node3D node)
    {
        Aabb? box = null;
        var inv = space.GlobalTransform.AffineInverse();
        foreach (var m in node.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().Append(node as MeshInstance3D))
        {
            if (m?.Mesh == null) continue;
            var b = inv * m.GlobalTransform * m.GetAabb();
            box = box == null ? b : box.Value.Merge(b);
        }
        return box;
    }

    private static bool InsideHex(Vector3 p, float radius)
    {
        // pointy-top cell: edge normals at 0/60/120 degrees, apothem = radius*cos30
        var q = new Vector2(Math.Abs(p.X), Math.Abs(p.Z));
        return q.X <= radius * .866f && q.Dot(new Vector2(.5f, .866f)) <= radius * .866f;
    }

    // ---- small procedural paper pieces for themes the asset set does not cover ----
    private static void Procedural(SurfaceTool t, string kind, Vector3 p, float size, float yaw, float r, RandomNumberGenerator rng)
    {
        var rot = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(yaw), 0));
        switch (kind)
        {
            case "@tower":
                Prism(t, p, rot, size * .5f, size * .5f, size * rng.RandfRange(1.4f, 2.6f), Pick(rng, "cfd6de", "b7c3cf", "e3d9c9", "aebfd0", "d8c6d6"), new Color("8fa3b8"), 4);
                break;
            case "@house":
                var h = size * rng.RandfRange(.7f, .9f);
                Prism(t, p, rot, size * .5f, size * .42f, h, Pick(rng, "f0d8b8", "e6c3a3", "f2e6d0"), Pick(rng, "c8574a", "b5483f"), 4);
                Roof(t, p + Vector3.Up * h, rot, size * .55f, size * .47f, size * .45f, Pick(rng, "c8574a", "b5483f"));
                break;
            case "@mesa":
                Prism(t, p, rot, size * .5f, size * .45f, size * rng.RandfRange(.5f, .9f), Pick(rng, "c9794a", "d08a52"), new Color("e0a46a"), 6);
                break;
            case "@crag":
                Spire(t, p, size * .45f, size * rng.RandfRange(1.4f, 2.2f), Pick(rng, "9a8a78", "857766"), 5);
                break;
            case "@ember":
                Spire(t, p, size * .4f, size * rng.RandfRange(.9f, 1.4f), Pick(rng, "3f3239", "4a3a42"), 5);
                break;
            case "@thorn":
                // Everfree bramble: a dark, sharply zig-zagging vine with spiral tips and spikes
                {
                    var dark = Pick(rng, "1d2a2e", "22343a", "18242a");
                    var pt = p; var heading = rng.RandfRange(0, Mathf.Tau); var w = size * .055f;
                    var segs = rng.RandiRange(5, 7);
                    for (var i = 0; i < segs; i++)
                    {
                        heading += (i % 2 == 0 ? 1 : -1) * rng.RandfRange(.9f, 1.6f);
                        var step = new Vector3(Mathf.Cos(heading) * .45f, rng.RandfRange(.55f, 1.0f), Mathf.Sin(heading) * .45f).Normalized() * size * .42f;
                        if (i > segs / 2) step.Y *= -.35f;   // arch over and droop like a bramble
                        var next = pt + step; if (next.Y < p.Y) next.Y = p.Y + r * .01f;
                        Beam(t, pt, next, w * (1f - i * .1f), dark);
                        if (i % 2 == 1) Spike(t, (pt + next) * .5f, step.Cross(Vector3.Up).Normalized() * size * .09f + Vector3.Up * size * .03f, w * .8f, dark);
                        pt = next;
                    }
                    for (var k = 0; k < 4; k++)   // curled tip
                    {
                        heading += 1.3f; var next = pt + new Vector3(Mathf.Cos(heading), .15f, Mathf.Sin(heading)) * size * .07f * (1 - k * .2f);
                        Beam(t, pt, next, w * .5f, dark); pt = next;
                    }
                }
                break;
            case "@shroom":
                {
                    var cap = Pick(rng, "e57fd0", "b48cf0", "ff9fd6");
                    for (var i = 0; i < rng.RandiRange(2, 3); i++)
                    {
                        var q = p + new Vector3(rng.RandfRange(-.5f, .5f), 0, rng.RandfRange(-.5f, .5f)) * size; var stem = size * rng.RandfRange(.5f, 1f);
                        Prism(t, q, rot, size * .06f, size * .06f, stem, new Color("f3ead8"), new Color("f3ead8"), 5);
                        Mound(t, q + Vector3.Up * stem, size * .26f, size * .18f, cap, 7);
                    }
                }
                break;
            case "@moss":
                // shade pools under the canopy: dark teal moss patches give the Everfree its gloom
                {
                    // irregular, soft-edged: a darker core inside a ring close to the floor colour
                    const int n = 11; var rad = new float[n];
                    for (var i = 0; i < n; i++) rad[i] = size * .5f * rng.RandfRange(.6f, 1.05f);
                    Vector3 Rim(int i, float k) { var a = Mathf.Tau * (i % n) / n; return p + Vector3.Up * r * .004f + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rad[i % n] * k; }
                    var outer = new Color("2a5232"); var core = Pick(rng, "1f4436", "1d3f34");
                    for (var i = 0; i < n; i++) { Quad(t, Rim(i, .55f), Rim(i + 1, .55f), Rim(i + 1, 1f), Rim(i, 1f), outer); Tri(t, p + Vector3.Up * r * .005f, Rim(i, .55f), Rim(i + 1, .55f), core); }
                }
                break;
            case "@snow":
                Mound(t, p, size * .5f, size * .22f, new Color("f2f5f8"), 7);
                break;
            case "@cloud":
                for (var i = 0; i < 3; i++)
                    Mound(t, p + rot * new Vector3((i - 1) * size * .32f, 0, (i % 2) * size * .12f), size * (i == 1 ? .38f : .28f), size * (i == 1 ? .34f : .24f), new Color("fbfcff"), 7);
                break;
            case "@pool":
                Disc(t, p + Vector3.Up * r * .006f, size * .5f, new Color("e2d6a6"), 10);
                Disc(t, p + Vector3.Up * r * .010f, size * .42f, new Color("5bb3c4"), 10);
                break;
            case "@lava":
                // a jagged crack: dark rim with a glowing core, lying flat on the card
                var dir = rot * Vector3.Right; var side = rot * Vector3.Back;
                var pts = Enumerable.Range(0, 5).Select(i => p + dir * (i - 2) * size * .25f + side * (i % 2 == 0 ? .05f : -.05f) * size + Vector3.Up * r * .006f).ToArray();
                for (var i = 0; i + 1 < pts.Length; i++)
                {
                    Strip(t, pts[i], pts[i + 1], size * .09f, new Color("2b2228"));
                    Strip(t, pts[i] + Vector3.Up * r * .002f, pts[i + 1] + Vector3.Up * r * .002f, size * .04f, new Color("f2873a"));
                }
                break;
        }
    }

    private static Color Pick(RandomNumberGenerator rng, params string[] colors) => new(colors[rng.RandiRange(0, colors.Length - 1)]);

    private static void Tri(SurfaceTool t, Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        var n = (b - a).Cross(c - a).Normalized();
        if (n.Y < -.01f) { (b, c) = (c, b); n = -n; }
        t.SetColor(col.SrgbToLinear()); t.SetNormal(n);
        t.AddVertex(a); t.AddVertex(c); t.AddVertex(b);
    }
    private static void Quad(SurfaceTool t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col) { Tri(t, a, b, c, col); Tri(t, a, c, d, col); }
    private static void Prism(SurfaceTool t, Vector3 p, Basis rot, float hx, float hz, float h, Color wall, Color top, int sides)
    {
        var ring = Enumerable.Range(0, sides).Select(i => { var a = Mathf.Tau * i / sides + Mathf.Pi / sides; return p + rot * new Vector3(Mathf.Cos(a) * hx * 1.41f, 0, Mathf.Sin(a) * hz * 1.41f); }).ToArray();
        for (var i = 0; i < sides; i++)
        {
            var a = ring[i]; var b = ring[(i + 1) % sides];
            Quad(t, a, b, b + Vector3.Up * h, a + Vector3.Up * h, i % 2 == 0 ? wall : wall.Darkened(.1f));
            Tri(t, p + Vector3.Up * h, a + Vector3.Up * h, b + Vector3.Up * h, top);
        }
    }
    private static void Roof(SurfaceTool t, Vector3 p, Basis rot, float hx, float hz, float h, Color col)
    {
        Vector3 P(float x, float y, float z) => p + rot * new Vector3(x, y, z);
        Quad(t, P(-hx, 0, -hz), P(hx, 0, -hz), P(hx, h, 0), P(-hx, h, 0), col);
        Quad(t, P(-hx, 0, hz), P(hx, 0, hz), P(hx, h, 0), P(-hx, h, 0), col.Darkened(.1f));
        Tri(t, P(-hx, 0, -hz), P(-hx, 0, hz), P(-hx, h, 0), col.Lightened(.1f));
        Tri(t, P(hx, 0, -hz), P(hx, 0, hz), P(hx, h, 0), col.Lightened(.1f));
    }
    private static void Spire(SurfaceTool t, Vector3 p, float radius, float h, Color col, int sides)
    {
        var tip = p + Vector3.Up * h;
        for (var i = 0; i < sides; i++)
        {
            float A(int k) => Mathf.Tau * k / sides;
            Tri(t, tip, p + new Vector3(Mathf.Cos(A(i)), 0, Mathf.Sin(A(i))) * radius, p + new Vector3(Mathf.Cos(A(i + 1)), 0, Mathf.Sin(A(i + 1))) * radius, i % 2 == 0 ? col : col.Lightened(.08f));
        }
    }
    private static void Mound(SurfaceTool t, Vector3 p, float radius, float h, Color col, int sides)
    {
        var top = p + Vector3.Up * h;
        for (var i = 0; i < sides; i++)
        {
            float A(int k) => Mathf.Tau * k / sides;
            Vector3 B(int k) => p + new Vector3(Mathf.Cos(A(k)), 0, Mathf.Sin(A(k))) * radius;
            Vector3 M(int k) => p + new Vector3(Mathf.Cos(A(k)), 0, Mathf.Sin(A(k))) * radius * .62f + Vector3.Up * h * .72f;
            Quad(t, B(i), B(i + 1), M(i + 1), M(i), col.Darkened(.04f));
            Tri(t, top, M(i), M(i + 1), col);
        }
    }
    private static void Disc(SurfaceTool t, Vector3 c, float radius, Color col, int sides)
    {
        for (var i = 0; i < sides; i++)
        {
            float A(int k) => Mathf.Tau * k / sides;
            Tri(t, c, c + new Vector3(Mathf.Cos(A(i)), 0, Mathf.Sin(A(i))) * radius, c + new Vector3(Mathf.Cos(A(i + 1)), 0, Mathf.Sin(A(i + 1))) * radius, col);
        }
    }
    private static void Beam(SurfaceTool t, Vector3 a, Vector3 b, float width, Color col)
    {
        var d = (b - a).Normalized(); var u = Math.Abs(d.Y) > .9f ? Vector3.Right : Vector3.Up;
        var s1 = d.Cross(u).Normalized() * width; var s2 = d.Cross(s1).Normalized() * width;
        Vector3[] o = [s1, s2, -s1, -s2];
        for (var i = 0; i < 4; i++) { var c1 = o[i]; var c2 = o[(i + 1) % 4]; QuadAny(t, a + c1, b + c1, b + c2, a + c2, i % 2 == 0 ? col : col.Lightened(.08f)); }
    }
    private static void Spike(SurfaceTool t, Vector3 at, Vector3 dir, float width, Color col)
    {
        var side = dir.Cross(Vector3.Up).Normalized() * width; if (side.LengthSquared() < 1e-8f) side = Vector3.Right * width;
        TriAny(t, at - side, at + side, at + dir, col); TriAny(t, at - side * .2f + Vector3.Up * width, at + side * .2f - Vector3.Up * width, at + dir, col.Lightened(.06f));
    }
    // two-sided triangles for thin vines (no upward flip)
    private static void TriAny(SurfaceTool t, Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        var n = (b - a).Cross(c - a).Normalized();
        t.SetColor(col.SrgbToLinear()); t.SetNormal(n); t.AddVertex(a); t.AddVertex(c); t.AddVertex(b);
        t.SetColor(col.SrgbToLinear()); t.SetNormal(-n); t.AddVertex(a); t.AddVertex(b); t.AddVertex(c);
    }
    private static void QuadAny(SurfaceTool t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col) { TriAny(t, a, b, c, col); TriAny(t, a, c, d, col); }
    private static void Strip(SurfaceTool t, Vector3 a, Vector3 b, float width, Color col)
    {
        var w = (b - a).Cross(Vector3.Up).Normalized() * width;
        Quad(t, a - w, b - w, b + w, a + w, col);
    }
}

/// <summary>
/// Placeholder pawns until the real pieces are modelled: a paper base disc, a tapered body and a
/// faceted head in the player's colour. Up to four share a cell in a small ring around its anchor.
/// </summary>
public static class PaperPawnPlaceholder
{
    public static readonly Color[] PlayerColors = [new("e86fa8"), new("5a9be0"), new("f2c14e"), new("9a6fd6")];

    public static Node3D Place(HexCell3D cell, int player, int slot = 0, int slotsInCell = 1)
    {
        var r = cell.Radius;
        var colour = PlayerColors[Math.Abs(player) % PlayerColors.Length];
        var pawn = new Node3D { Name = $"PawnPlaceholder_{player}" };
        var offset = slotsInCell <= 1 ? Vector3.Zero : new Vector3(Mathf.Cos(Mathf.Tau * slot / slotsInCell + .6f), 0, Mathf.Sin(Mathf.Tau * slot / slotsInCell + .6f)) * r * .16f;
        pawn.Position = offset;
        var paper = OrigamiMapPresentation.PaperMaterial(colour);
        var cream = OrigamiMapPresentation.PaperMaterial(new Color("fbf3e2"));
        pawn.AddChild(new MeshInstance3D { Name = "Base", Mesh = new CylinderMesh { TopRadius = r * .085f, BottomRadius = r * .095f, Height = r * .035f, RadialSegments = 8, Rings = 1 }, Position = Vector3.Up * r * .0175f, MaterialOverride = cream });
        pawn.AddChild(new MeshInstance3D { Name = "Body", Mesh = new CylinderMesh { TopRadius = r * .035f, BottomRadius = r * .07f, Height = r * .24f, RadialSegments = 7, Rings = 1 }, Position = Vector3.Up * r * .155f, MaterialOverride = paper });
        pawn.AddChild(new MeshInstance3D { Name = "Head", Mesh = new SphereMesh { Radius = r * .055f, Height = r * .11f, RadialSegments = 7, Rings = 4 }, Position = Vector3.Up * r * .31f, MaterialOverride = paper });
        pawn.AddChild(new MeshInstance3D { Name = "Crest", Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = r * .025f, Height = r * .06f, RadialSegments = 5, Rings = 1 }, Position = Vector3.Up * r * .37f, MaterialOverride = cream });
        pawn.SetMeta("placeholder_pawn", true);
        pawn.SetMeta("player_color", colour);
        cell.GetNode<Node3D>("TokenRoot").AddChild(pawn);
        ConceptPaperStyle.Apply(pawn);
        return pawn;
    }
}

/// <summary>
/// Widest zoom: where are the pawns? Each occupied cell gets a ring in the players' colours and a
/// map pin per player that keeps a constant on-screen size, so the board reads like a strategy map.
/// Hidden once the camera is close enough for the pawns themselves to read.
/// </summary>
public static class PaperPawnBeacon
{
    private const float HeadRadius = .16f;   // fraction of the cell radius at scale 1

    public static Color PawnColour(Node pawn) => pawn.HasMeta("player_color") ? pawn.GetMeta("player_color").AsColor() : new Color("fff7d7");

    /// <summary>Create, refresh or remove the beacon of one cell. Returns the beacon or null when the cell is empty.</summary>
    public static Node3D? Sync(HexCell3D cell)
    {
        var colours = cell.GetNode("TokenRoot").GetChildren().Select(PawnColour).ToArray();
        var existing = cell.GetNodeOrNull<Node3D>("PawnBeacon");
        var signature = string.Join(",", colours.Select(c => c.ToHtml(false)));
        if (existing != null && existing.GetMeta("signature", "").AsString() == signature) return existing;
        existing?.Free();
        if (colours.Length == 0) return null;
        var r = cell.Radius;
        var beacon = new Node3D { Name = "PawnBeacon" };
        beacon.SetMeta("signature", signature);
        cell.AddChild(beacon);
        // ring around the cell top, edges dealt to the players in turn
        var ring = new SurfaceTool(); ring.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 P(int i, float rad, float y) { var a = Mathf.DegToRad(i * 60 - 90); return new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad); }
        for (var i = 0; i < 6; i++)
        {
            ring.SetColor(colours[i % colours.Length].SrgbToLinear()); ring.SetNormal(Vector3.Up);
            var a = P(i, r * .99f, r * .05f); var b = P(i + 1, r * .99f, r * .05f); var c = P(i, r * .80f, r * .05f); var d = P(i + 1, r * .80f, r * .05f);
            ring.AddVertex(a); ring.AddVertex(c); ring.AddVertex(b); ring.AddVertex(b); ring.AddVertex(c); ring.AddVertex(d);
        }
        beacon.AddChild(new MeshInstance3D { Name = "Ring", Mesh = ring.Commit(), MaterialOverride = OrigamiMapPresentation.PaperMaterial(Colors.White), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        for (var k = 0; k < colours.Length; k++)
        {
            var pin = new Node3D { Name = $"Pin{k}", Position = new Vector3((k - (colours.Length - 1) * .5f) * r * .42f, 0, 0) };
            pin.SetMeta("beacon_pin", true);
            var paper = OrigamiMapPresentation.PaperMaterial(colours[k]);
            var cream = OrigamiMapPresentation.PaperMaterial(new Color("fffaf0"));
            pin.AddChild(new MeshInstance3D { Name = "Pole", Mesh = new CylinderMesh { TopRadius = r * .025f, BottomRadius = r * .01f, Height = r * .9f, RadialSegments = 6, Rings = 1 }, Position = Vector3.Up * r * .45f, MaterialOverride = cream, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            pin.AddChild(new MeshInstance3D { Name = "Head", Mesh = new SphereMesh { Radius = r * HeadRadius, Height = r * HeadRadius * 2, RadialSegments = 8, Rings = 4 }, Position = Vector3.Up * r * 1.02f, MaterialOverride = paper, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            pin.AddChild(new MeshInstance3D { Name = "Dot", Mesh = new SphereMesh { Radius = r * HeadRadius * .42f, Height = r * HeadRadius * .84f, RadialSegments = 6, Rings = 3 }, Position = new Vector3(0, r * 1.06f, r * HeadRadius * .72f), MaterialOverride = cream, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            beacon.AddChild(pin);
        }
        ConceptPaperStyle.Apply(beacon);
        return beacon;
    }

    /// <summary>Keep the pin heads about <paramref name="screenFraction"/> of the view height tall.</summary>
    public static void Scale(Node3D beacon, float cameraSize, float radius, float screenFraction = .028f)
    {
        var s = Mathf.Clamp(cameraSize * screenFraction / (2 * HeadRadius * radius), 1f, 6f);
        foreach (var pin in beacon.GetChildren().OfType<Node3D>().Where(n => n.HasMeta("beacon_pin"))) pin.Scale = Vector3.One * s;
    }
}
