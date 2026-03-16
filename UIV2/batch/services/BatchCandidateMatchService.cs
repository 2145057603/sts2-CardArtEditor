using InfectionCardReplacer.UIV2.Batch.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InfectionCardReplacer.UIV2.Batch.Services;

internal sealed class BatchCandidateMatchService
{
    public List<BatchTaskEntry> BuildEntries(
        IReadOnlyList<CardCatalogRegistry.Entry> cards,
        IReadOnlyList<BatchTaskPackSource> sources,
        BatchTaskMatchMode matchMode)
    {
        List<BatchTaskEntry> entries = [];
        FilenameLookup lookup = BuildFilenameLookup(sources);

        foreach (CardCatalogRegistry.Entry card in cards)
        {
            BatchTaskEntry entry = new()
            {
                CardId = card.FullTypeName,
                CardName = card.Title,
                CardType = card.TypeName,
                RoleName = card.RoleName
            };

            if (matchMode is BatchTaskMatchMode.ExplicitAndFileName or BatchTaskMatchMode.ExplicitOnly)
            {
                List<BatchMatchCandidate> explicitCandidates = FindExplicitCandidates(card, sources);
                if (TryApplyCandidates(entry, explicitCandidates, "显式映射"))
                {
                    entries.Add(entry);
                    continue;
                }
            }

            if (matchMode is BatchTaskMatchMode.ExplicitAndFileName or BatchTaskMatchMode.FileNameOnly)
            {
                List<BatchMatchCandidate> autoCandidates = FindFilenameCandidates(card, lookup);
                if (TryApplyCandidates(entry, autoCandidates, "文件名匹配"))
                {
                    entries.Add(entry);
                    continue;
                }
            }

            entry.Status = BatchTaskEntryStatus.Missing;
            entry.MatchReason = "未匹配";
            entries.Add(entry);
        }

        return entries;
    }

    private static List<BatchMatchCandidate> FindExplicitCandidates(
        CardCatalogRegistry.Entry card,
        IReadOnlyList<BatchTaskPackSource> sources)
    {
        List<BatchMatchCandidate> candidates = [];
        foreach (BatchTaskPackSource source in sources)
        {
            if (!source.Index.DefaultMappings.TryGetValue(card.FullTypeName, out string? relativePath))
            {
                continue;
            }

            PackSystem.Models.PackEntry? matchedEntry = source.Index.Entries.FirstOrDefault(item =>
                item.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
            if (matchedEntry is null)
            {
                continue;
            }

            candidates.Add(new BatchMatchCandidate
            {
                PackId = source.Descriptor.PackId,
                PackName = source.Descriptor.DisplayName,
                RelativePath = matchedEntry.RelativePath,
                AbsolutePath = matchedEntry.AbsolutePath
            });
        }

        return candidates;
    }

    private static List<BatchMatchCandidate> FindFilenameCandidates(CardCatalogRegistry.Entry card, FilenameLookup lookup)
    {
        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
        keys.Add(NormalizeToken(card.TypeName));
        keys.Add(NormalizeToken(card.Title));
        keys.Add(NormalizeToken(card.FullTypeName));
        keys.Add(NormalizeToken(card.FullTypeName.Split('.').LastOrDefault() ?? card.TypeName));

        List<BatchMatchCandidate> matches = [];
        HashSet<string> seenAbsolutePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in keys.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            if (!lookup.Index.TryGetValue(key, out List<BatchMatchCandidate>? candidates))
            {
                continue;
            }

            foreach (BatchMatchCandidate candidate in candidates)
            {
                if (seenAbsolutePaths.Add(candidate.AbsolutePath))
                {
                    matches.Add(candidate);
                }
            }
        }

        return matches;
    }

    private static bool TryApplyCandidates(BatchTaskEntry entry, List<BatchMatchCandidate> candidates, string reasonPrefix)
    {
        if (candidates.Count == 1)
        {
            BatchMatchCandidate candidate = candidates[0];
            entry.Status = BatchTaskEntryStatus.Matched;
            entry.MatchReason = reasonPrefix;
            entry.SourcePackId = candidate.PackId;
            entry.SourcePackName = candidate.PackName;
            entry.CandidateRelativePath = candidate.RelativePath;
            entry.CandidateImagePath = candidate.AbsolutePath;
            return true;
        }

        if (candidates.Count > 1)
        {
            entry.Status = BatchTaskEntryStatus.Conflict;
            entry.MatchReason = $"{reasonPrefix} 冲突";
            entry.ConflictCount = candidates.Count;
            entry.SourcePackName = string.Join(" / ", candidates.Select(item => item.PackName).Distinct(StringComparer.OrdinalIgnoreCase));
            return true;
        }

        return false;
    }

    private static FilenameLookup BuildFilenameLookup(IReadOnlyList<BatchTaskPackSource> sources)
    {
        FilenameLookup lookup = new();
        foreach (BatchTaskPackSource source in sources)
        {
            foreach (PackSystem.Models.PackEntry entry in source.Index.Entries)
            {
                string key = NormalizeToken(Path.GetFileNameWithoutExtension(entry.FileName));
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!lookup.Index.TryGetValue(key, out List<BatchMatchCandidate>? items))
                {
                    items = [];
                    lookup.Index[key] = items;
                }

                items.Add(new BatchMatchCandidate
                {
                    PackId = source.Descriptor.PackId,
                    PackName = source.Descriptor.DisplayName,
                    RelativePath = entry.RelativePath,
                    AbsolutePath = entry.AbsolutePath
                });
            }
        }

        return lookup;
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        char[] filtered = value
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch))
            .ToArray();
        return new string(filtered);
    }

    private sealed class BatchMatchCandidate
    {
        public string PackId { get; init; } = string.Empty;
        public string PackName { get; init; } = string.Empty;
        public string RelativePath { get; init; } = string.Empty;
        public string AbsolutePath { get; init; } = string.Empty;
    }

    private sealed class FilenameLookup
    {
        public Dictionary<string, List<BatchMatchCandidate>> Index { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
