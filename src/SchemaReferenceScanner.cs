namespace Cthangover.Mods.Schemas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;

    /// <summary>What one file holds for a renamed id: how many references, where, and the file's text after the rename.</summary>
    public sealed class ReferenceHit
    {
        /// <summary>The mod the file belongs to.</summary>
        public string Mod { get; init; }

        /// <summary>The mod-relative path of the file.</summary>
        public string Path { get; init; }

        /// <summary>How many references (and the entity's own id) the file names.</summary>
        public int Count { get; set; }

        /// <summary>Where they are: a field name, a DSL line head, a condition.</summary>
        public List<string> Places { get; } = new();

        /// <summary>The file's text with the new id (set for the edited mod's files only; null for the others).</summary>
        public string NewText { get; set; }
    }

    /// <summary>The references to one id in every file of the loaded mods, split by the edited mod (changed) and the others (listed).</summary>
    public sealed class ReferencePlan
    {
        /// <summary>Files of the edited mod that change: their texts are written when the rename is applied.</summary>
        public List<ReferenceHit> Own { get; } = new();

        /// <summary>Files of other mods that name the id: they are not changed, the author sees them.</summary>
        public List<ReferenceHit> Other { get; } = new();
    }

    /// <summary>
    /// Finds and renames the references to one entity id (kind + id) in data files, the way the schemas
    /// describe them: a <c>ref</c> or <c>ref_list</c> field of that kind, a <c>who</c> naming a character,
    /// the keys of a <c>map</c>, the same fields inside <c>list</c> rows, and the DSL text of scenario lines
    /// and of JSON strings (<c>action item.add id=…</c>, commands with positional or named parameters, the
    /// <c>item('…')</c> condition for items). The entity's own <c>Id</c> is renamed in the edited mod's files.
    /// <para/>
    /// Only files the schemas and the DSL describe are read: a reference the schemas do not name (a
    /// <c>json</c> field, C# code) is not found, and the rename preview says nothing about it. Files whose
    /// JSON has to be re-written are written in the editors' format (two-space indent, LF).
    /// </summary>
    public static class SchemaReferenceScanner
    {
        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>The references to <paramref name="kind"/> <paramref name="oldId"/> in every file; the edited mod's files get their new text.</summary>
        public static ReferencePlan Plan(SchemaCatalog catalog, string editedMod, IEnumerable<(string Mod, string Path, string Text)> files, string kind, string oldId, string newId)
        {
            var plan = new ReferencePlan();
            foreach (var (mod, path, text) in files)
            {
                var edited = string.Equals(mod, editedMod, StringComparison.OrdinalIgnoreCase);
                var hit = Scan(catalog, mod, path, text, kind, oldId, newId);
                if (hit == null || hit.Count == 0)
                    continue;
                if (edited)
                    plan.Own.Add(hit);
                else
                {
                    hit.NewText = null;
                    plan.Other.Add(hit);
                }
            }
            return plan;
        }

        /// <summary>One file: its references to the id and its text with the id renamed (null when the file names none).</summary>
        public static ReferenceHit Scan(SchemaCatalog catalog, string mod, string path, string text, string kind, string oldId, string newId)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(kind) || text.IndexOf(oldId, StringComparison.Ordinal) < 0)
                return null;
            var walker = new Walker(catalog, kind, oldId, newId);
            var hit = new ReferenceHit { Mod = mod, Path = path };
            string result;
            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                JsonNode root;
                try
                {
                    root = JsonNode.Parse(text);
                }
                catch (JsonException)
                {
                    return null;
                }
                walker.Json(root);
                result = walker.Changed ? root.ToJsonString(WriteOptions).Replace("\r\n", "\n") + "\n" : null;
            }
            else
            {
                result = walker.Text(text);
            }
            hit.Count = walker.Count;
            hit.Places.AddRange(walker.Places.Distinct());
            hit.NewText = walker.Changed ? result : null;
            return hit;
        }

        /// <summary>Walks one file's JSON or text, counting and renaming the references to one id.</summary>
        private sealed class Walker
        {
            private readonly SchemaCatalog catalog;
            private readonly string kind;
            private readonly string oldId;
            private readonly string newId;
            private readonly bool rename;
            private readonly Regex itemCall;

            public Walker(SchemaCatalog catalog, string kind, string oldId, string newId)
            {
                this.catalog = catalog;
                this.kind = kind;
                this.oldId = oldId;
                this.newId = newId;
                rename = newId != null && newId != oldId;
                itemCall = string.Equals(kind, "item", StringComparison.OrdinalIgnoreCase)
                    ? new Regex(@"\bitem\(\s*(['""]?)" + Regex.Escape(oldId) + @"\1\s*\)", RegexOptions.CultureInvariant)
                    : null;
            }

            /// <summary>References found (the entity's own id included).</summary>
            public int Count { get; private set; }

            /// <summary>Where they are.</summary>
            public List<string> Places { get; } = new();

            /// <summary>Whether a rename changed anything.</summary>
            public bool Changed { get; private set; }

            /// <summary>Walks a JSON document: entity rows of the kind get their typed fields checked, every string its DSL text.</summary>
            public void Json(JsonNode root)
            {
                if (root is JsonObject obj && obj["Kind"] is JsonValue kindValue && kindValue.TryGetValue(out string rowKind) && obj["Items"] is JsonArray items)
                {
                    foreach (var item in items.OfType<JsonObject>())
                        Row(item, rowKind);
                }
                Strings(root, string.Empty);
            }

            /// <summary>Plain text (a scenario file): the DSL text of every line.</summary>
            public string Text(string text) => Line(text, string.Empty);

            private void Row(JsonObject item, string rowKind)
            {
                // The entity's own id is renamed only in rows of its own kind; every row's typed fields are checked.
                if (string.Equals(rowKind, kind, StringComparison.OrdinalIgnoreCase)
                    && item["Id"] is JsonValue idValue && idValue.TryGetValue(out string id) && id == oldId)
                {
                    Count++;
                    Places.Add("Id");
                    if (rename)
                    {
                        item["Id"] = JsonValue.Create(newId);
                        Changed = true;
                    }
                }
                Fields(item, catalog.FieldsOf(rowKind), string.Empty);
            }

            /// <summary>The typed fields of one object: references to the kind, the map keys, the list rows.</summary>
            private void Fields(JsonObject obj, IReadOnlyList<FieldSchema> fields, string prefix)
            {
                foreach (var field in fields ?? new List<FieldSchema>())
                {
                    var key = obj.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, field.Key, StringComparison.OrdinalIgnoreCase));
                    if (key == null)
                        continue;
                    var node = obj[key];
                    var place = prefix + field.Key;
                    switch (field.Type?.ToLowerInvariant())
                    {
                        case "ref" when Targets(field.Kind):
                        case "who" when Targets("character"):
                            if (node is JsonValue value && value.TryGetValue(out string single) && single.Trim() == oldId)
                            {
                                Hit(place);
                                if (rename)
                                    obj[key] = JsonValue.Create(newId);
                            }
                            break;
                        case "ref_list" when Targets(field.Kind):
                            RefList(obj, key, node, place);
                            break;
                        case "map" when Targets(field.Kind) && node is JsonObject map:
                            if (map.Any(p => p.Key == oldId))
                            {
                                Hit(place + " (key)");
                                if (rename)
                                {
                                    var renamed = new JsonObject();
                                    foreach (var pair in map.ToList())
                                        renamed[pair.Key == oldId ? newId : pair.Key] = pair.Value?.DeepClone();
                                    obj[key] = renamed;
                                }
                            }
                            break;
                        case "list" when node is JsonArray rows && field.Of != null:
                            foreach (var row in rows.OfType<JsonObject>())
                                Fields(row, field.Of, place + "[].");
                            break;
                    }
                }
            }

            private void RefList(JsonObject obj, string key, JsonNode node, string place)
            {
                if (node is JsonValue value && value.TryGetValue(out string text))
                {
                    var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (!parts.Contains(oldId))
                        return;
                    Hit(place);
                    if (rename)
                        obj[key] = JsonValue.Create(string.Join(",", parts.Select(p => p == oldId ? newId : p)));
                    return;
                }
                if (node is JsonArray array)
                {
                    for (var i = 0; i < array.Count; i++)
                    {
                        if (array[i] is JsonValue element && element.TryGetValue(out string name) && name.Trim() == oldId)
                        {
                            Hit(place);
                            if (rename)
                                array[i] = JsonValue.Create(newId);
                        }
                    }
                }
            }

            /// <summary>Every string of the tree: DSL lines and the item condition are renamed in place.</summary>
            private void Strings(JsonNode node, string place)
            {
                switch (node)
                {
                    case JsonObject obj:
                        foreach (var key in obj.Select(p => p.Key).ToList())
                        {
                            if (obj[key] is JsonValue value && value.TryGetValue(out string text))
                            {
                                var result = Line(text, key);
                                if (result != null && result != text)
                                    obj[key] = JsonValue.Create(result);
                            }
                            else if (obj[key] != null)
                                Strings(obj[key], key);
                        }
                        break;
                    case JsonArray array:
                        for (var i = 0; i < array.Count; i++)
                        {
                            if (array[i] is JsonValue value && value.TryGetValue(out string text))
                            {
                                var result = Line(text, place);
                                if (result != null && result != text)
                                    array[i] = JsonValue.Create(result);
                            }
                            else if (array[i] != null)
                                Strings(array[i], place);
                        }
                        break;
                }
            }

            /// <summary>A string (possibly several lines): each line's DSL and condition references. Returns the new text, or null when it names no reference.</summary>
            private string Line(string text, string place)
            {
                if (text == null || text.IndexOf(oldId, StringComparison.Ordinal) < 0)
                    return text;
                var lines = text.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                    lines[i] = ScanLine(lines[i]);
                return string.Join("\n", lines);
            }

            private string ScanLine(string line)
            {
                if (line.IndexOf(oldId, StringComparison.Ordinal) < 0)
                    return line;
                var result = Tokens(line);
                if (itemCall != null)
                {
                    result = itemCall.Replace(result, m =>
                    {
                        Hit("item() condition");
                        if (!rename)
                            return m.Value;
                        var quote = m.Groups[1].Value;
                        Changed = true;
                        return "item(" + quote + newId + quote + ")";
                    });
                }
                return result;
            }

            /// <summary>The DSL line: <c>action &lt;id&gt; k=v</c> or <c>command v k=v -&gt; :label</c>; the ref parameters of the kind are renamed.</summary>
            private string Tokens(string line)
            {
                var trimmed = line.TrimStart();
                if (trimmed.Length == 0 || trimmed[0] == '#' || trimmed.StartsWith("//", StringComparison.Ordinal))
                    return line;
                var tokens = Split(line);
                if (tokens.Count == 0)
                    return line;
                List<FieldSchema> named;
                List<FieldSchema> positional;
                int first;
                string head;
                if (string.Equals(tokens[0].Content, "action", StringComparison.Ordinal) && tokens.Count >= 2
                    && catalog.Actions.TryGetValue(tokens[1].Content, out var action))
                {
                    named = action.Params;
                    positional = new List<FieldSchema>();
                    first = 2;
                    head = "action " + action.Id;
                }
                else if (catalog.Commands.TryGetValue(tokens[0].Content, out var command))
                {
                    named = command.Params;
                    positional = command.Positional ?? new List<FieldSchema>();
                    first = 1;
                    head = command.Id;
                }
                else
                    return line;

                var replacements = new List<(Tok Token, string Text)>();
                var index = 0;
                for (var i = first; i < tokens.Count; i++)
                {
                    var token = tokens[i];
                    if (token.Content == "->")
                        break;
                    FieldSchema field = null;
                    string value = token.Content;
                    if (token.Key != null)
                    {
                        field = named.FirstOrDefault(f => string.Equals(f.Key, token.Key, StringComparison.OrdinalIgnoreCase));
                        value = token.Value;
                    }
                    else if (index < positional.Count)
                        field = positional[index++];
                    if (field == null)
                        continue;
                    var renamed = RenamedValue(field, value);
                    if (renamed == null)
                        continue;
                    Hit(head + ": " + field.Key);
                    if (!rename)
                        continue;
                    Changed = true;
                    replacements.Add((token, token.Key != null ? SchemaLine.Quote(token.Key + "=" + renamed) : SchemaLine.Quote(renamed)));
                }
                if (replacements.Count == 0)
                    return line;
                var sb = new StringBuilder(line);
                foreach (var (token, text) in replacements.OrderByDescending(r => r.Token.Start))
                    sb.Remove(token.Start, token.End - token.Start).Insert(token.Start, text);
                return sb.ToString();
            }

            /// <summary>The value with the renamed id, when the field refers to the kind and the value names the id; else null.</summary>
            private string RenamedValue(FieldSchema field, string value)
            {
                var type = field.Type?.ToLowerInvariant();
                if (value == null || value.Contains("${", StringComparison.Ordinal))
                    return null;
                switch (type)
                {
                    case "ref" when Targets(field.Kind):
                        return value.Trim() == oldId ? newId : null;
                    case "who" when Targets("character"):
                        return value.Trim() == oldId ? newId : null;
                    case "ref_list" when Targets(field.Kind):
                        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        if (!parts.Contains(oldId))
                            return null;
                        return string.Join(",", parts.Select(p => p == oldId ? newId : p));
                    default:
                        return null;
                }
            }

            private bool Targets(string targetKind) => !string.IsNullOrEmpty(targetKind) && string.Equals(targetKind, kind, StringComparison.OrdinalIgnoreCase);

            private void Hit(string place)
            {
                Count++;
                Places.Add(place);
                if (rename)
                    Changed = true;
            }
        }

        /// <summary>A token of a DSL line: its span in the line, its unquoted content and, for <c>key=value</c>, the parts.</summary>
        private sealed class Tok
        {
            public int Start { get; init; }
            public int End { get; init; }
            public string Content { get; init; }
            public string Key { get; init; }
            public string Value { get; init; }
        }

        /// <summary>Splits a DSL line into tokens: whitespace separates them, quotes group them (escapes as <c>\"</c>), <c>key=value</c> is a named token.</summary>
        private static List<Tok> Split(string line)
        {
            var tokens = new List<Tok>();
            var i = 0;
            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i]))
                {
                    i++;
                    continue;
                }
                var start = i;
                var content = new StringBuilder();
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                {
                    if (line[i] == '"')
                    {
                        i++;
                        while (i < line.Length && line[i] != '"')
                        {
                            if (line[i] == '\\' && i + 1 < line.Length)
                                i++;
                            content.Append(line[i]);
                            i++;
                        }
                        i++;
                        continue;
                    }
                    content.Append(line[i]);
                    i++;
                }
                var text = content.ToString();
                var equals = text.IndexOf('=');
                var named = equals > 0 && Regex.IsMatch(text.Substring(0, equals), "^[A-Za-z_][A-Za-z0-9_]*$");
                tokens.Add(new Tok
                {
                    Start = start,
                    End = i,
                    Content = text,
                    Key = named ? text.Substring(0, equals) : null,
                    Value = named ? text.Substring(equals + 1) : null,
                });
            }
            return tokens;
        }
    }
}
