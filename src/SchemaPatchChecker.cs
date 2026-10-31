namespace Cthangover.Mods.Schemas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    /// <summary>
    /// Checks one entry of a <c>patches/&lt;kind&gt;.json</c> file against the kind's schema before
    /// the game runs, the way the patch engine (<c>JsonPatchMerger</c>) reads it: a key is a field
    /// name, optionally with an operation (<c>Name$add</c>, <c>Name$remove</c>, <c>Name$set</c>).
    /// <list type="bullet">
    /// <item><c>SCHEMA_PATCH_UNKNOWN_FIELD</c> (warning) — the schemas of the kind do not describe
    ///   the field; a near name is suggested (a typo). A warning, because a schema may leave out
    ///   fields a mod reads itself.</item>
    /// <item><c>SCHEMA_PATCH_BAD_OPERATION</c> — an operation the engine does not know, or a key
    ///   that starts with <c>$</c> (the engine rejects both).</item>
    /// <item><c>SCHEMA_PATCH_OP_ON_SCALAR</c> — <c>$add</c>/<c>$remove</c> on a field that is not a
    ///   list (<c>list</c>, <c>ref_list</c>).</item>
    /// <item><c>SCHEMA_PATCH_BAD_TYPE</c> — the value's JSON shape does not fit the field (an object
    ///   for a number, a scalar for a list, an operation value that is not an array or
    ///   <c>{"$key","$items"}</c>); scalar values are then checked like entity fields
    ///   (<see cref="SchemaChecker.CheckValue"/>).</item>
    /// </list>
    /// Only top-level keys are judged: nested objects of <c>json</c>/<c>values</c> fields and of
    /// fields the schemas do not describe are the owner's business.
    /// </summary>
    public static class SchemaPatchChecker
    {
        /// <summary>The operations of the patch engine.</summary>
        public static readonly IReadOnlyList<string> Operations = new[] { "$add", "$remove", "$set" };

        private static readonly string[] MetaKeys = { "Id", "$delete" };

        /// <summary>Issues of one patch entry (<paramref name="patch"/> is a JSON object).</summary>
        public static List<SchemaIssue> Check(KindSchema schema, JsonElement patch)
        {
            var issues = new List<SchemaIssue>();
            if (schema == null || patch.ValueKind != JsonValueKind.Object)
                return issues;
            var id = patch.TryGetProperty("Id", out var idProp) ? idProp.ToString() : "?";
            var owner = $"patch of {schema.Kind} '{id}'";
            foreach (var property in patch.EnumerateObject())
            {
                if (MetaKeys.Any(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var index = property.Name.IndexOf('$');
                var name = index < 0 ? property.Name : property.Name.Substring(0, index);
                var operation = index < 0 ? null : property.Name.Substring(index);
                if (index == 0 || operation != null && !Operations.Contains(operation, StringComparer.Ordinal))
                {
                    issues.Add(new SchemaIssue(true, "SCHEMA_PATCH_BAD_OPERATION",
                        $"'{owner}': '{property.Name}' is not a field with an operation (write Name$add, Name$remove or Name$set)"));
                    continue;
                }

                var field = schema.Fields?.FirstOrDefault(f => string.Equals(f.Key, name, StringComparison.OrdinalIgnoreCase));
                if (field == null)
                {
                    var near = Nearest(name, schema.Fields?.Select(f => f.Key) ?? Enumerable.Empty<string>());
                    issues.Add(new SchemaIssue(false, "SCHEMA_PATCH_UNKNOWN_FIELD",
                        $"'{owner}': {schema.Kind} has no described field '{name}'" + (near != null ? $" — did you mean '{near}'?" : string.Empty)));
                    continue;
                }

                CheckField(issues, owner, field, operation, property.Value);
            }
            return issues;
        }

        private static void CheckField(List<SchemaIssue> issues, string owner, FieldSchema field, string operation, JsonElement value)
        {
            var shape = ShapeOf(field.Type);
            if (operation is "$add" or "$remove")
            {
                if (shape is Shape.Scalar or Shape.Object)
                {
                    issues.Add(new SchemaIssue(true, "SCHEMA_PATCH_OP_ON_SCALAR",
                        $"'{owner}': '{field.Key}{operation}' — {field.Key} is {field.Type ?? "text"}, not a list; write '{field.Key}' to replace the value"));
                    return;
                }
                if (!IsOperationValue(value))
                    issues.Add(new SchemaIssue(true, "SCHEMA_PATCH_BAD_TYPE",
                        $"'{owner}': '{field.Key}{operation}' needs an array or {{\"$key\": …, \"$items\": […]}}, got {Describe(value)}"));
                return;
            }

            if (value.ValueKind == JsonValueKind.Null)
                return;
            switch (shape)
            {
                case Shape.List when value.ValueKind != JsonValueKind.Array:
                    issues.Add(new SchemaIssue(true, "SCHEMA_PATCH_BAD_TYPE", $"'{owner}': '{field.Key}' is a list, got {Describe(value)}"));
                    break;
                case Shape.Object when value.ValueKind != JsonValueKind.Object:
                    issues.Add(new SchemaIssue(true, "SCHEMA_PATCH_BAD_TYPE", $"'{owner}': '{field.Key}' is a map (object), got {Describe(value)}"));
                    break;
                case Shape.Scalar when value.ValueKind is JsonValueKind.Object or JsonValueKind.Array:
                    issues.Add(new SchemaIssue(true, "SCHEMA_PATCH_BAD_TYPE", $"'{owner}': '{field.Key}' is {field.Type ?? "text"}, got {Describe(value)}"));
                    break;
                case Shape.Scalar:
                    issues.AddRange(SchemaChecker.CheckValue(owner, field, value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString(), null));
                    break;
            }
        }

        private static bool IsOperationValue(JsonElement value)
            => value.ValueKind == JsonValueKind.Array
               || value.ValueKind == JsonValueKind.Object
               && value.TryGetProperty("$key", out _)
               && value.TryGetProperty("$items", out var items) && items.ValueKind == JsonValueKind.Array;

        private static Shape ShapeOf(string type) => type?.ToLowerInvariant() switch
        {
            "list" or "ref_list" => Shape.List,
            "map" => Shape.Object,
            "json" or "values" => Shape.Free,
            _ => Shape.Scalar,
        };

        private static string Describe(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Object => "an object",
            JsonValueKind.Array => "an array",
            JsonValueKind.String => "a string",
            JsonValueKind.Number => "a number",
            JsonValueKind.True or JsonValueKind.False => "a boolean",
            _ => value.ValueKind.ToString().ToLowerInvariant(),
        };

        /// <summary>The described field nearest to <paramref name="name"/> (at most two edits, case-insensitive), or null.</summary>
        internal static string Nearest(string name, IEnumerable<string> keys)
        {
            string best = null;
            var bestDistance = 3;
            foreach (var key in keys.Where(k => !string.IsNullOrEmpty(k)))
            {
                var distance = Distance(name.ToLowerInvariant(), key.ToLowerInvariant());
                if (distance < bestDistance)
                {
                    best = key;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++)
                previous[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }

        private enum Shape
        {
            Scalar,
            List,
            Object,
            Free,
        }
    }
}
