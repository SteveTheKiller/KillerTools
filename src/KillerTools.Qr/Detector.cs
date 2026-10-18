namespace KillerTools.Qr;

/// <summary>A code's position: the transform from module coordinates to image pixels and its side length in modules.</summary>
internal sealed record Placement(Perspective Transform, int Dimension, Finder TopLeft, Finder TopRight, Finder BottomLeft);

/// <summary>Turns three finder patterns into a placement and samples the module grid.</summary>
internal static class Detector
{
    /// <summary>Orders three finders as top-left (the corner of the right angle), top-right and bottom-left.</summary>
    public static (Finder TopLeft, Finder TopRight, Finder BottomLeft) Order(Finder a, Finder b, Finder c)
    {
        double ab = Dist(a, b), bc = Dist(b, c), ac = Dist(a, c);
        Finder topLeft, p, q;
        if (bc >= ab && bc >= ac) { topLeft = a; p = b; q = c; }
        else if (ac >= ab && ac >= bc) { topLeft = b; p = a; q = c; }
        else { topLeft = c; p = a; q = b; }
        // In image coordinates (y down), top-right lies clockwise from bottom-left around top-left.
        double cross = (p.X - topLeft.X) * (q.Y - topLeft.Y) - (p.Y - topLeft.Y) * (q.X - topLeft.X);
        return cross > 0 ? (topLeft, p, q) : (topLeft, q, p);
    }

    /// <summary>Whether three finders can be the corners of one code: similar module sizes, two similar
    /// legs from the top-left one, and a roughly right angle between them (perspective bends all three a little).</summary>
    public static bool Plausible(Finder tl, Finder tr, Finder bl)
    {
        double min = Math.Min(tl.ModuleSize, Math.Min(tr.ModuleSize, bl.ModuleSize));
        double max = Math.Max(tl.ModuleSize, Math.Max(tr.ModuleSize, bl.ModuleSize));
        if (max > min * 1.6) return false;
        double across = Dist(tl, tr), down = Dist(tl, bl);
        if (Math.Max(across, down) > Math.Min(across, down) * 1.6) return false;
        if (Math.Min(across, down) < 10 * min) return false;
        double cos = ((tr.X - tl.X) * (bl.X - tl.X) + (tr.Y - tl.Y) * (bl.Y - tl.Y)) / (across * down);
        return Math.Abs(cos) < 0.45;
    }

    /// <summary>The side length in modules, rounded to a valid size (4 times version plus 17).</summary>
    public static int Dimension(Finder tl, Finder tr, Finder bl, double module)
    {
        int across = (int)Math.Round(Dist(tl, tr) / module), down = (int)Math.Round(Dist(tl, bl) / module);
        int dimension = (across + down) / 2 + 7;
        return (dimension & 3) switch { 0 => dimension + 1, 2 => dimension - 1, 3 => dimension + 2, _ => dimension };
    }

    /// <summary>Measures finder widths along the code's own axes rather than the image axes.</summary>
    public static double ModuleSize(BitImage image, Finder tl, Finder tr, Finder bl)
    {
        double Width(Finder from, Finder toward)
        {
            double length = Dist(from, toward), dx = (toward.X - from.X) / length, dy = (toward.Y - from.Y) / length;
            double Half(int sign)
            {
                bool dark = true;
                int transitions = 0;
                for (double distance = 0; distance < from.ModuleSize * 8; distance += 0.25)
                {
                    bool next = image.At(from.X + sign * dx * distance, from.Y + sign * dy * distance);
                    if (next == dark) continue;
                    dark = next;
                    if (++transitions == 3) return distance;
                }
                return double.NaN;
            }
            return (Half(1) + Half(-1) - 0.25) / 7;
        }
        double[] sizes = [Width(tl, tr), Width(tr, tl), Width(tl, bl), Width(bl, tl)];
        var valid = sizes.Where(double.IsFinite).ToArray();
        return valid.Length == 0 ? (tl.ModuleSize + tr.ModuleSize + bl.ModuleSize) / 3 : valid.Average();
    }

    /// <summary>Places a code of the given side from its finder centers, assuming no perspective.</summary>
    public static Placement Place(Finder tl, Finder tr, Finder bl, int dimension, QrPoint? alignment = null)
    {
        double far = dimension - 3.5;
        var grid = new QrPoint[] { new(3.5, 3.5), new(far, 3.5), default, new(3.5, far) };
        var image = new QrPoint[] { new(tl.X, tl.Y), new(tr.X, tr.Y), default, new(bl.X, bl.Y) };
        if (alignment is { } a)
        {
            grid[2] = new QrPoint(dimension - 6.5, dimension - 6.5);
            image[2] = a;
        }
        else
        {
            grid[2] = new QrPoint(far, far);
            image[2] = new QrPoint(tr.X - tl.X + bl.X, tr.Y - tl.Y + bl.Y);
        }
        return new Placement(Perspective.Between(grid, image), dimension, tl, tr, bl);
    }

    /// <summary>Reads each module at its center.</summary>
    public static bool[,] Sample(BitImage image, Placement placement)
    {
        int n = placement.Dimension;
        var modules = new bool[n, n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = placement.Transform.Map(x + 0.5, y + 0.5);
                modules[y, x] = image.At(p.X, p.Y);
            }
        return modules;
    }

    public static QrPoint[] Corners(Placement placement)
    {
        int n = placement.Dimension;
        return [placement.Transform.Map(0, 0), placement.Transform.Map(n, 0), placement.Transform.Map(n, n), placement.Transform.Map(0, n)];
    }

    internal static double Dist(Finder a, Finder b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
