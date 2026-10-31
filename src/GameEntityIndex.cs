namespace Cthangover.Mods.Schemas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Godot;

    /// <summary>
    /// Entity ids and names per kind of the loaded mods, read from their data files the way
    /// the entity resolver reads them (top-level <c>"Kind"</c>, <c>"Items"</c> with
    /// <c>"Id"</c>). The tools need it for any kind a schema names — including kinds whose
    /// C# type the tools have never heard of — to fill reference pickers and to check that
    /// a referenced id exists. Built on demand; <see cref="Refresh"/> after a save.
    /// </summary>
    public static class GameEntityIndex
    {
        private static Dictionary<string, SortedDictionary<string, string>> byKind;
        private static Dictionary<string, SortedSet<string>> valuesByField;

        /// <summary>Ids of <paramref name="kind"/> with their name keys, sorted by id.</summary>
        public static IReadOnlyDictionary<string, string> Of(string kind)
        {
            Ensure();
            return kind != null && byKind.TryGetValue(kind, out var ids) ? ids : new SortedDictionary<string, string>();
        }

        /// <summary>Whether an entity exists (the checker's callback).</summary>
        public static bool Exists(string kind, string id) => id != null && Of(kind).Keys.Any(k => string.Equals(k, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Every kind found in data files, with its entity count.</summary>
        public static IReadOnlyDictionary<string, int> Kinds()
        {
            Ensure();
            return byKind.ToDictionary(p => p.Key, p => p.Value.Count, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The text shown for an entity: its translated name and id, or the id.</summary>
        public static string Label(string kind, string id)
        {
            var key = Of(kind).TryGetValue(id ?? string.Empty, out var name) ? name : null;
            var text = string.IsNullOrEmpty(key) ? null : TranslationServer.Translate(key).ToString();
            return string.IsNullOrEmpty(text) || text == key ? id : $"{text}  ·  {id}";
        }

        /// <summary>Forgets the index (data changed on disk).</summary>
        public static void Refresh()
        {
            byKind = null;
            valuesByField = null;
        }

        /// <summary>
        /// The values a field holds across the entities of a kind (a string, or the strings of a list; a comma list is split):
        /// the options of a <c>pick</c> field. Read once from the mods' data, then kept until <see cref="Refresh"/>.
        /// </summary>
        public static IReadOnlyCollection<string> ValuesOf(string kind, string field)
        {
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(field))
                return Array.Empty<string>();
            valuesByField ??= new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            var key = kind + "|" + field;
            if (valuesByField.TryGetValue(key, out var found))
                return found;
            var values = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var (_, _, text) in ReferenceFiles())
            {
                if (!text.Contains($"\"{kind}\"", StringComparison.Ordinal))
                    continue;
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Kind", out var rootKind) || !string.Equals(rootKind.GetString(), kind, StringComparison.OrdinalIgnoreCase)
                        || !root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var item in items.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object && i.TryGetProperty(field, out _)))
                    {
                        var value = item.GetProperty(field);
                        if (value.ValueKind == JsonValueKind.String)
                            values.UnionWith(value.GetString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                        else if (value.ValueKind == JsonValueKind.Array)
                            values.UnionWith(value.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()));
                    }
                }
                catch (JsonException)
                {
                    // A broken file is the validator's business.
                }
            }
            valuesByField[key] = values;
            return values;
        }

        /// <summary>
        /// Every file a reference can live in, of every loaded mod: JSON data (patches included) and scenario text, with their
        /// texts (read now). Schemas, manifests and locale files are left out: they name no entity.
        /// </summary>
        public static IEnumerable<(string Mod, string Path, string Text)> ReferenceFiles()
        {
            var manager = ModManager.Instance;
            if (manager?.Mods == null)
                yield break;
            foreach (var mod in manager.Mods.Keys.ToList())
            {
                foreach (var path in AllFiles(manager, mod, string.Empty).Where(IsReferenceFile).ToList())
                {
                    var text = manager.ReadFileText(mod, path);
                    if (text != null)
                        yield return (mod, path, text);
                }
            }
        }

        private static IEnumerable<string> AllFiles(ModManager manager, string mod, string directory)
        {
            foreach (var entry in manager.ListFiles(mod, directory))
            {
                if (entry.EndsWith("/"))
                {
                    foreach (var nested in AllFiles(manager, mod, entry.TrimEnd('/')))
                        yield return nested;
                }
                else
                {
                    yield return entry.Replace('\\', '/');
                }
            }
        }

        private static bool IsReferenceFile(string path)
            => (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".scenario", StringComparison.OrdinalIgnoreCase))
               && !path.StartsWith("schemas/", StringComparison.OrdinalIgnoreCase)
               && !path.StartsWith("locale/", StringComparison.OrdinalIgnoreCase)
               && !path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase);

        private static void Ensure()
        {
            if (byKind != null)
                return;
            byKind = new Dictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var manager = ModManager.Instance;
            if (manager?.Mods == null)
                return;
            foreach (var mod in manager.Mods.Keys)
            {
                foreach (var path in JsonFiles(manager, mod, string.Empty).Where(IsData))
                    Read(manager.ReadFileText(mod, path));
            }
        }

        private static IEnumerable<string> JsonFiles(ModManager manager, string mod, string directory)
        {
            // Entries are mod-relative paths; directories end with a slash.
            foreach (var entry in manager.ListFiles(mod, directory))
            {
                if (entry.EndsWith("/"))
                {
                    foreach (var nested in JsonFiles(manager, mod, entry.TrimEnd('/')))
                        yield return nested;
                }
                else if (entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    yield return entry.Replace('\\', '/');
                }
            }
        }

        private static bool IsData(string path)
            => !path.StartsWith("schemas/", StringComparison.OrdinalIgnoreCase)
               && !path.StartsWith("patches/", StringComparison.OrdinalIgnoreCase)
               && !path.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase);

        private static void Read(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Kind", out var kind) || kind.ValueKind != JsonValueKind.String
                    || !root.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array)
                    return;
                if (!byKind.TryGetValue(kind.GetString(), out var ids))
                    byKind[kind.GetString()] = ids = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (var item in items.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object))
                {
                    if (item.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String)
                        ids[id.GetString()] = item.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
                }
            }
            catch (JsonException)
            {
                // A broken data file is the validator's business; the index just skips it.
            }
        }
    }
}
