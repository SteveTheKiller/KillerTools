using KillerTools.Engine.Documents;

internal static class StructuredJsonDepthChecks
{
    public static void RejectsNestedArrays() => Refuses(new string('[', 256) + "0" + new string(']', 256));

    public static void RejectsNestedObjects() => Refuses(string.Concat(Enumerable.Repeat("{x:", 256)) + "0" + new string('}', 256));

    public static void PreservesBoundaryAndSiblingDepth()
    {
        string arrays = new string('[', 64) + "0" + new string(']', 64);
        string objects = string.Concat(Enumerable.Repeat("{x:", 64)) + "0" + new string('}', 64);
        UnwrapArrays(StructuredJson.Parse(arrays), 64);
        UnwrapObjects(StructuredJson.Parse(objects), 64);
        Refuses("[" + arrays + "]");
        Refuses("{x:" + objects + "}");

        string mixed = "0";
        for (int depth = 0; depth < 64; depth++) mixed = depth % 2 == 0 ? "[" + mixed + "]" : "{x:" + mixed + "}";
        object? value = StructuredJson.Parse(mixed);
        for (int depth = 0; depth < 64; depth++)
            value = value is List<object?> array ? array.Single() : ((Dictionary<string, object?>)value!)["x"];
        if (!Equals(0L, value)) throw new InvalidOperationException("Mixed nesting changed its leaf.");
        Refuses("[" + mixed + "]");

        string left = new string('[', 63) + "0" + new string(']', 63);
        string right = string.Concat(Enumerable.Repeat("{x:", 63)) + "0" + new string('}', 63);
        var siblings = (Dictionary<string, object?>)StructuredJson.Parse("{left:" + left + ",right:" + right + ",tail:1}")!;
        UnwrapArrays(siblings["left"], 63);
        UnwrapObjects(siblings["right"], 63);
        if (!Equals(1L, siblings["tail"])) throw new InvalidOperationException("Sibling depth did not unwind.");
    }

    public static void PreservesShallowAndWideReports()
    {
        var shallow = (Dictionary<string, object?>)StructuredJson.Parse("{/*report*/name:'fixture',enabled:true,values:[1,2,],}")!;
        if (!Equals("fixture", shallow["name"]) || !Equals(true, shallow["enabled"]) || ((List<object?>)shallow["values"]!).Count != 2)
            throw new InvalidOperationException("Shallow JSON5 behavior changed.");
        string report = "{items:[" + string.Join(',', Enumerable.Repeat("0", 100_000)) + "]}";
        var wide = (Dictionary<string, object?>)StructuredJson.Parse(report)!;
        var items = (List<object?>)wide["items"]!;
        if (items.Count != 100_000 || !Equals(0L, items[99_999]))
            throw new InvalidOperationException("A flat 100,000-item report was not preserved.");
    }

    private static void UnwrapArrays(object? value, int depth)
    {
        for (int level = 0; level < depth; level++) value = ((List<object?>)value!).Single();
        if (!Equals(0L, value)) throw new InvalidOperationException("Array depth boundary changed its leaf.");
    }

    private static void UnwrapObjects(object? value, int depth)
    {
        for (int level = 0; level < depth; level++) value = ((Dictionary<string, object?>)value!)["x"];
        if (!Equals(0L, value)) throw new InvalidOperationException("Object depth boundary changed its leaf.");
    }

    private static void Refuses(string text)
    {
        try { StructuredJson.Parse(text); }
        catch (FormatException) { return; }
        throw new InvalidOperationException("Expected FormatException for excessive JSON nesting.");
    }
}
