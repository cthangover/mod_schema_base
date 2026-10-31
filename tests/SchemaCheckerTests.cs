namespace Cthangover.Mods.Schemas.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Mods.Testing;

    /// <summary>What the validator and the palette report about values.</summary>
    public class SchemaCheckerTests
    {
        private static readonly List<FieldSchema> Fields = new()
        {
            new() { Key = "id", Type = "ref", Kind = "item", Required = true },
            new() { Key = "count", Type = "int" },
            new() { Key = "rate", Type = "number" },
            new() { Key = "mode", Type = "choice", Options = new List<string> { "fast", "slow" } },
            new() { Key = "on", Type = "bool" },
            new() { Key = "who", Type = "who" },
            new() { Key = "enemies", Type = "ref_list", Kind = "character" },
        };

        private static bool Exists(string kind, string id) => kind == "item" && id == "sword" || kind == "character" && id is "hero" or "wolf";

        private static List<string> Codes(Dictionary<string, string> named) => SchemaChecker.CheckNamed("t", Fields, named, Exists).Select(i => (i.IsError ? "E:" : "W:") + i.Code).ToList();

        [ModTest("checker/clean_values_pass")]
        static void CleanValuesPass(ModTestContext ctx)
        {
            var codes = Codes(new Dictionary<string, string> { ["id"] = "sword", ["count"] = "-2", ["rate"] = "0.5", ["mode"] = "FAST", ["on"] = "1", ["who"] = "party", ["enemies"] = "hero, wolf" });
            ModAssert.Equal(0, codes.Count, "no issue: " + string.Join(",", codes));
        }

        [ModTest("checker/each_problem_is_named")]
        static void EachProblemIsNamed(ModTestContext ctx)
        {
            ModAssert.True(Codes(new Dictionary<string, string>()).SequenceEqual(new[] { "E:SCHEMA_MISSING_PARAM" }), "missing required");
            ModAssert.True(Codes(new Dictionary<string, string> { ["id"] = "sword", ["nope"] = "1" }).SequenceEqual(new[] { "W:SCHEMA_UNKNOWN_PARAM" }), "unknown parameter");
            ModAssert.True(Codes(new Dictionary<string, string> { ["id"] = "axe" }).SequenceEqual(new[] { "E:SCHEMA_UNKNOWN_REF" }), "unknown entity");
            ModAssert.True(Codes(new Dictionary<string, string> { ["id"] = "sword", ["count"] = "1.5" }).SequenceEqual(new[] { "E:SCHEMA_BAD_VALUE" }), "not a whole number");
            ModAssert.True(Codes(new Dictionary<string, string> { ["id"] = "sword", ["mode"] = "medium" }).SequenceEqual(new[] { "W:SCHEMA_BAD_VALUE" }), "outside the options");
            ModAssert.True(Codes(new Dictionary<string, string> { ["id"] = "sword", ["who"] = "ghost" }).SequenceEqual(new[] { "E:SCHEMA_UNKNOWN_REF" }), "an unknown character as who");
            ModAssert.True(Codes(new Dictionary<string, string> { ["id"] = "sword", ["enemies"] = "wolf,bear" }).SequenceEqual(new[] { "E:SCHEMA_UNKNOWN_REF" }), "one unknown in a list");
        }

        [ModTest("checker/runtime_templates_and_expression_syntax")]
        static void DynamicValues(ModTestContext ctx)
        {
            ModAssert.Equal(0, Codes(new Dictionary<string,string> { ["id"]="${item}", ["count"]="${money}" }).Count, "runtime values are not static reference or integer errors");
            ModAssert.True(Codes(new Dictionary<string,string> { ["id"]="sword", ["count"]="${money" }).Contains("E:SCHEMA_BAD_VALUE"), "broken template rejected");
            var field = new FieldSchema { Key="value", Type="expression", Required=true };
            ModAssert.Equal(0, SchemaChecker.CheckValue("cset",field,"rnd(3,5)+${Value}",null).Count, "syntax only: no variables read or randomness consumed");
            ModAssert.Equal(1, SchemaChecker.CheckValue("cset",field,"rnd(3)+",null).Count, "arity and syntax errors are visible in the palette");
        }

        [ModTest("checker/no_callback_skips_references")]
        static void NoCallbackSkipsReferences(ModTestContext ctx)
        {
            var issues = SchemaChecker.CheckNamed("t", Fields, new Dictionary<string, string> { ["id"] = "anything" }, null);
            ModAssert.Equal(0, issues.Count, "references are not checked without a callback");
            var positional = SchemaChecker.CheckPositional("t", Fields.Take(2).ToList(), new List<string> { "sword", "x" }, Exists);
            ModAssert.Equal(1, positional.Count, "positional values are checked by index");
        }
    }
}
