namespace KillerTools.Engine.SystemTools;

public readonly record struct PermissionGroup(bool Read, bool Write, bool Execute);
public readonly record struct UnixPermissionSet(PermissionGroup Owner, PermissionGroup Group, PermissionGroup Public);
public readonly record struct UnixPermissionResult(string Octal, string Symbolic, string Command);

public static class UnixPermissions
{
    public static UnixPermissionResult Calculate(UnixPermissionSet permissions)
    {
        var octal = $"{Value(permissions.Owner)}{Value(permissions.Group)}{Value(permissions.Public)}";
        var symbolic = Symbols(permissions.Owner) + Symbols(permissions.Group) + Symbols(permissions.Public);
        return new UnixPermissionResult(octal, symbolic, $"chmod {octal} path");
    }

    private static int Value(PermissionGroup value) => (value.Read ? 4 : 0) + (value.Write ? 2 : 0) + (value.Execute ? 1 : 0);
    private static string Symbols(PermissionGroup value) => $"{(value.Read ? 'r' : '-')}{(value.Write ? 'w' : '-')}{(value.Execute ? 'x' : '-')}";
}
