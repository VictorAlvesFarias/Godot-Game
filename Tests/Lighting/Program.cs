using System;
using System.Collections.Generic;
using System.Diagnostics;
using Jogo25D.Light;

sealed class Geometry : ILightGeometry
{
    public readonly Dictionary<LightCell, byte> Materials = new();
    public readonly Dictionary<LightCell, byte> Seeds = new();
    public byte Opacity(int x, int y) => Materials.GetValueOrDefault(new(x, y));
    public byte Sky(int x, int y) => Opacity(x, y) == 255 ? (byte)0 : Seeds.GetValueOrDefault(new(x, y));
}

static class Program
{
    static int checks;
    static void Assert(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Settle(LightingField field)
    {
        int runs = 0;
        while (!field.Settled) { field.Process(1000000, 10000); if (++runs > 100) throw new Exception("No convergence"); }
    }

    // Independent full rebuild: descending-intensity traversal starting only at real sources.
    static Dictionary<LightCell, int> Reference(Geometry geometry)
    {
        var result = new Dictionary<LightCell, int>();
        var queue = new PriorityQueue<LightCell, int>();
        foreach (var seed in geometry.Seeds)
            if (geometry.Opacity(seed.Key.X, seed.Key.Y) != 255) { result[seed.Key] = seed.Value; queue.Enqueue(seed.Key, -seed.Value); }
        while (queue.TryDequeue(out var p, out int priority))
        {
            int value = -priority;
            if (result[p] != value) continue;
            foreach (var direction in LightingField.Neighbours)
            {
                var q = p + direction;
                byte opacity = geometry.Opacity(q.X, q.Y);
                if (opacity == 255) continue;
                int next = value - LightingField.AirLoss - opacity / 3;
                if (next <= result.GetValueOrDefault(q)) continue;
                result[q] = next; queue.Enqueue(q, -next);
            }
        }
        return result;
    }

    static void Compare(LightingField field, Geometry geometry)
    {
        var reference = Reference(geometry);
        for (int y = -24; y < 24; y++)
            for (int x = -40; x < 40; x++)
                Assert(field.Get(x, y).Sky == reference.GetValueOrDefault(new(x, y)), $"Reference mismatch at {x},{y}");
    }

    static void Main()
    {
        var clock = Stopwatch.StartNew();
        var geometry = new Geometry();
        var field = new LightingField(geometry);
        field.SetRegion(-40, -24, 80, 48);
        for (int x = -8; x <= 8; x++) { geometry.Materials[new(x, -8)] = 255; geometry.Materials[new(x, 8)] = 255; }
        for (int y = -8; y <= 8; y++) { geometry.Materials[new(-8, y)] = 255; geometry.Materials[new(8, y)] = 255; }
        geometry.Seeds[new(0, -10)] = 255;
        for (int x = -8; x <= 8; x++) field.GeometryChanged(x, 0);
        Settle(field);
        Assert(field.Get(0, 0).Sky == 0, "Sealed room must be black");
        geometry.Materials.Remove(new(0, -8)); field.GeometryChanged(0, -8); Settle(field);
        Assert(field.Get(0, 0).Sky > 0, "Opening must admit light");
        Compare(field, geometry);
        geometry.Materials[new(0, -8)] = 255; field.GeometryChanged(0, -8); Settle(field);
        Assert(field.Get(0, 0).Sky == 0, "Closing must remove all unsupported light");
        field.SetSource(1, new(-3, 0), new(0, 255, 100, 0));
        field.SetSource(2, new(3, 0), new(0, 255, 0, 100)); Settle(field);
        byte oldRed = field.Get(0, 0).R;
        field.RemoveSource(1); Settle(field);
        Assert(field.Get(0, 0).R == oldRed && field.Get(0, 0).G == 0, "Alternate source must survive removal; unsupported green must disappear");
        field.RemoveSource(2); Settle(field);
        Assert(field.Get(0, 0) == default, "No stale RGB after removal");
        var random = new Random(73);
        for (int edit = 0; edit < 120; edit++)
        {
            int x = random.Next(-35, 36), y = random.Next(-20, 21);
            var p = new LightCell(x, y);
            if (edit % 4 == 0) geometry.Seeds[p] = (byte)random.Next(80, 256);
            else if (edit % 4 == 1) geometry.Materials[p] = 255;
            else if (edit % 4 == 2) geometry.Materials[p] = 72;
            else { geometry.Materials.Remove(p); geometry.Seeds.Remove(p); }
            field.GeometryChanged(x, y); Settle(field); Compare(field, geometry);
        }
        var before = field.Get(-32, 0);
        field.SetRegion(100000, -100000, 32, 32); Settle(field);
        Assert(field.ResidentChunks <= 16, "Resident cache must remain bounded");
        field.SetRegion(-40, -24, 80, 48); Settle(field);
        Compare(field, geometry);
        Assert(field.Get(-32, 0) == before, "Unload/reload and negative chunks must be invariant");
        long processed = field.ProcessedCells;
        field.GeometryChanged(1000000, 1000000); Settle(field);
        Assert(field.ProcessedCells == processed, "Unrelated remote edit should do no propagation");
        Console.WriteLine($"PASS: {checks:N0} assertions; {field.ProcessedCells:N0} processed cells; {clock.ElapsedMilliseconds} ms");
    }
}
