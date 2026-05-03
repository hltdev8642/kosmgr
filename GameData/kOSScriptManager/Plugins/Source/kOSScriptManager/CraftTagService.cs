using System;
using System.Collections.Generic;
using System.Linq;
using kOS.Module;

namespace kOSScriptManager
{
    internal sealed class CraftPartTagEntry
    {
        public Part Part = null!;
        public string DisplayName = string.Empty;
        public bool IsProcessor;
        public string Tag = string.Empty;
    }

    internal sealed class CraftTagService
    {
        public List<CraftPartTagEntry> BuildPartList()
        {
            var entries = new List<CraftPartTagEntry>(128);
            var parts = GetSceneParts();
            if (parts == null)
            {
                return entries;
            }

            foreach (var part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                var processor = part.Modules.OfType<kOSProcessor>().FirstOrDefault();
                var tagModule = part.Modules.OfType<KOSNameTag>().FirstOrDefault();
                var tag = processor != null ? processor.Tag : (tagModule != null ? tagModule.nameTag : string.Empty);

                var hasInterestingState = processor != null || !string.IsNullOrWhiteSpace(tag);
                if (!hasInterestingState)
                {
                    continue;
                }

                entries.Add(new CraftPartTagEntry
                {
                    Part = part,
                    IsProcessor = processor != null,
                    Tag = tag ?? string.Empty,
                    DisplayName = string.Format("{0} [{1}]", part.partInfo != null ? part.partInfo.title : part.name, part.craftID)
                });
            }

            entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            return entries;
        }

        public bool SetTag(Part part, string tagValue, out string error)
        {
            error = string.Empty;
            if (part == null)
            {
                error = "Part is null.";
                return false;
            }

            var cleaned = (tagValue ?? string.Empty).Trim();
            var processor = part.Modules.OfType<kOSProcessor>().FirstOrDefault();
            if (processor != null)
            {
                processor.Tag = cleaned;
                return true;
            }

            var tagModule = part.Modules.OfType<KOSNameTag>().FirstOrDefault();
            if (tagModule == null)
            {
                try
                {
                    var created = part.AddModule("KOSNameTag");
                    tagModule = created as KOSNameTag;
                }
                catch (Exception ex)
                {
                    error = "Unable to add KOSNameTag: " + ex.Message;
                    return false;
                }
            }

            if (tagModule == null)
            {
                error = "kOS tag module is not available on this part.";
                return false;
            }

            tagModule.nameTag = cleaned;
            return true;
        }

        public string BuildTagReference(string tagValue)
        {
            var safeTag = (tagValue ?? string.Empty).Replace("\"", "");
            return string.Format("ship:partstagged(\"{0}\")[0]", safeTag);
        }

        private static IReadOnlyList<Part>? GetSceneParts()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return EditorLogic.fetch != null && EditorLogic.fetch.ship != null
                    ? EditorLogic.fetch.ship.parts
                    : null;
            }

            var vessel = FlightGlobals.ActiveVessel;
            return vessel != null ? vessel.parts : null;
        }
    }
}
