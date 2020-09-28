using System.Data.Common;

internal static class DbConnectionStringBuilderExtensions
{
    public static string GetOrDefault(this DbConnectionStringBuilder builder, string key, string defaultValue) =>
        builder.TryGetValue(key, out var value) ? value.ToString() : defaultValue;
}