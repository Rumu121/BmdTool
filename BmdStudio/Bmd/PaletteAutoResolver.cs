using System.Text.RegularExpressions;

namespace BMDEditor.Bmd;

internal sealed class PaletteCandidate
{
    public required string Name { get; init; }
    public required string PcxPath { get; init; }
}

internal static class PaletteAutoResolver
{
    private static readonly Regex QuotedValueRegex = new("\"([^\"]+)\"", RegexOptions.Compiled);

    public static IReadOnlyList<PaletteCandidate> ResolvePaletteCandidates(string bmdPath, string? gameRootPath = null)
    {
        if (string.IsNullOrWhiteSpace(bmdPath) || !File.Exists(bmdPath))
        {
            return Array.Empty<PaletteCandidate>();
        }

        var modRoot = ResolveModRoot(bmdPath, gameRootPath);
        if (modRoot is null)
        {
            return Array.Empty<PaletteCandidate>();
        }

        var housesIniPath = Path.Combine(modRoot, "EdytorByRemik", "ejkfhsnkjehbhouses.ini");
        if (!File.Exists(housesIniPath))
        {
            return Array.Empty<PaletteCandidate>();
        }

        var paletteNames = FindPaletteNamesForBmd(housesIniPath, bmdPath);

        var palettesIniPath = Path.Combine(modRoot, "Data", "engine2d", "inis", "palettes", "palettes.ini");
        if (!File.Exists(palettesIniPath))
        {
            return Array.Empty<PaletteCandidate>();
        }

        var candidates = new List<PaletteCandidate>();
        foreach (var paletteName in paletteNames)
        {
            if (!TryFindPaletteFilePath(palettesIniPath, paletteName, out var paletteRelativePath))
            {
                continue;
            }

            var palettePcxPath = ResolvePath(modRoot, paletteRelativePath);
            if (!File.Exists(palettePcxPath))
            {
                continue;
            }

            candidates.Add(new PaletteCandidate
            {
                Name = paletteName,
                PcxPath = palettePcxPath
            });
        }

        return candidates;
    }

    private static IReadOnlyList<string> FindPaletteNamesForBmd(string housesIniPath, string bmdPath)
    {
        var normalizedBmdPath = NormalizePath(bmdPath);
        var ranked = new List<RankedPaletteName>();
        var order = 0;

        foreach (var block in EnumerateGfxBlocks(housesIniPath))
        {
            if (block.BobLibs.Count == 0 || block.PaletteNames.Count == 0)
            {
                continue;
            }

            for (var i = 0; i < block.BobLibs.Count; i++)
            {
                var bobPath = block.BobLibs[i];
                var normalizedBobPath = NormalizePath(bobPath);
                if (string.IsNullOrWhiteSpace(normalizedBobPath))
                {
                    continue;
                }

                var score = GetMatchScore(normalizedBmdPath, normalizedBobPath);
                if (score <= 0)
                {
                    continue;
                }

                var paletteIndex = i < block.PaletteNames.Count ? i : 0;
                if (paletteIndex >= 0 && paletteIndex < block.PaletteNames.Count)
                {
                    var mappedPalette = block.PaletteNames[paletteIndex];
                    if (!string.IsNullOrWhiteSpace(mappedPalette))
                    {
                        ranked.Add(new RankedPaletteName
                        {
                            Name = mappedPalette,
                            Score = score + 5000,
                            Order = order++
                        });
                    }
                }

                foreach (var paletteName in block.PaletteNames)
                {
                    if (string.IsNullOrWhiteSpace(paletteName))
                    {
                        continue;
                    }

                    ranked.Add(new RankedPaletteName
                    {
                        Name = paletteName,
                        Score = score,
                        Order = order++
                    });
                }
            }
        }

        return ranked
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Order)
                .First())
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Order)
            .Select(x => x.Name)
            .ToList();
    }

    private static IEnumerable<GfxBlock> EnumerateGfxBlocks(string housesIniPath)
    {
        var current = new GfxBlock();

        foreach (var raw in File.ReadLines(housesIniPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("//"))
            {
                continue;
            }

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                if (current.BobLibs.Count > 0 || current.PaletteNames.Count > 0)
                {
                    yield return current;
                }

                current = new GfxBlock();
                continue;
            }

            if (line.StartsWith("GfxBobLibs", StringComparison.OrdinalIgnoreCase))
            {
                current.BobLibs = ParseQuotedValues(line);
            }
            else if (line.StartsWith("GfxPalette", StringComparison.OrdinalIgnoreCase))
            {
                current.PaletteNames = ParseQuotedValues(line);
            }
        }

        if (current.BobLibs.Count > 0 || current.PaletteNames.Count > 0)
        {
            yield return current;
        }
    }

    private static bool TryFindPaletteFilePath(string palettesIniPath, string paletteName, out string paletteFilePath)
    {
        paletteFilePath = string.Empty;
        var targetName = paletteName.Trim();
        string? currentEditName = null;

        foreach (var raw in File.ReadLines(palettesIniPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("//"))
            {
                continue;
            }

            if (line.StartsWith("editname", StringComparison.OrdinalIgnoreCase))
            {
                currentEditName = ParseQuotedValues(line).FirstOrDefault();
                continue;
            }

            if (!line.StartsWith("gfxfile", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(currentEditName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var gfxFile = ParseQuotedValues(line).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(gfxFile))
            {
                continue;
            }

            paletteFilePath = gfxFile;
            return true;
        }

        return false;
    }

    private static string? ResolveModRoot(string bmdPath, string? gameRootPath)
    {
        if (!string.IsNullOrWhiteSpace(gameRootPath))
        {
            try
            {
                var full = Path.GetFullPath(gameRootPath);
                if (Directory.Exists(full))
                {
                    return full;
                }
            }
            catch
            {
                // Fallback to auto-detection from BMD path.
            }
        }

        return TryResolveModRootFromBmdPath(bmdPath);
    }

    private static string? TryResolveModRootFromBmdPath(string bmdPath)
    {
        var fullPath = Path.GetFullPath(bmdPath);
        var normalized = fullPath.Replace('/', '\\');
        var marker = "\\data\\";
        var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex <= 0)
        {
            return null;
        }

        return normalized[..markerIndex];
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        var normalized = path.Replace('/', '\\').Trim();
        if (Path.IsPathRooted(normalized))
        {
            return Path.GetFullPath(normalized);
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, normalized));
    }

    private static List<string> ParseQuotedValues(string line)
    {
        return QuotedValueRegex.Matches(line)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(v => v.Length > 0)
            .ToList();
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('/', '\\').Trim();
        while (normalized.StartsWith(".\\", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.ToLowerInvariant();
    }

    private static int GetMatchScore(string bmdPath, string bobPath)
    {
        if (bmdPath.EndsWith(bobPath, StringComparison.OrdinalIgnoreCase))
        {
            return 100000 + bobPath.Length;
        }

        var bmdFileName = Path.GetFileName(bmdPath);
        var bobFileName = Path.GetFileName(bobPath);
        if (string.Equals(bmdFileName, bobFileName, StringComparison.OrdinalIgnoreCase))
        {
            return 1000 + bobFileName.Length;
        }

        return -1;
    }

    private sealed class GfxBlock
    {
        public List<string> BobLibs { get; set; } = new();
        public List<string> PaletteNames { get; set; } = new();
    }

    private sealed class RankedPaletteName
    {
        public required string Name { get; init; }
        public required int Score { get; init; }
        public required int Order { get; init; }
    }
}
