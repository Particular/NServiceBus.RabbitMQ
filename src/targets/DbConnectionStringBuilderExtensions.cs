using System.Data.Common;

static class DbConnectionStringBuilderExtensions
{
    public static string GetOrDefault(this DbConnectionStringBuilder builder, string key, string defaultValue) =>
        builder.TryGetValue(key, out var value) ? value.ToString() : defaultValue;
}

