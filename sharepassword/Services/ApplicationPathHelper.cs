using Microsoft.AspNetCore.Http;

namespace SharePassword.Services;

internal static class ApplicationPathHelper
{
    public static string BuildAppPath(PathString pathBase, string path)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? "/" : path;
        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = "/" + normalizedPath;
        }

        var pathBaseValue = pathBase.Value;
        if (string.IsNullOrEmpty(pathBaseValue) || string.Equals(pathBaseValue, "/", StringComparison.Ordinal))
        {
            return normalizedPath;
        }

        if (string.Equals(normalizedPath, pathBaseValue, StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(pathBaseValue + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        return pathBaseValue.TrimEnd('/') + normalizedPath;
    }

    public static string BuildAbsoluteAppUrl(HttpRequest request, string path)
    {
        return $"{request.Scheme}://{request.Host}{BuildAppPath(request.PathBase, path)}";
    }
}