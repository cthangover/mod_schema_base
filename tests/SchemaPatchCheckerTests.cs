namespace Cthangover.Mods.Schemas.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;

    /// <summary>What the validator reports about entries of <c>patches/&lt;kind&gt;.json</c>.</summary>
    public class SchemaPatchCheckerTests
    {
        private static readonly KindSchema Item = new()
        {
            Kind = "item",
            Fields = new List<FieldSchema>
            {
                new() { Key = "Name", Type = "locale" },
                new() { Key = "Cost", Type = "int" },
                new() { Key = "Mode", Type = "choice", Options = new List<string> { "fast", "slow" } },
                new() { Key = "Tags", Type = "list" },
                new() { Key = "Effects", Type = "ref_list", Kind = "status_effect" },
                new() { Key = "Stats", Type = "map", Kind = "stat" },
                new() { Key = "Extra", Type = "json" },
            },
        };

        private static List<string> Codes(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return SchemaPatchChecker.Check(Item, doc.RootElement).Select(i => (i.IsError ? "E:" : "W:") + i.Code).ToList();
        }

        private static string Message(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return string.Join(" | ", SchemaPatchChecker.Check(Item, doc.RootElement).Select(i => i.Message));
        }

        [ModTest("patches/clean_patch_passes")]
        static void CleanPatchPasses(ModTestContext ctx)
        {
            var codes = Codes(@"{""Id"":""food/ration"",""Cost"":12,""Mode"":""SLOW"",""Name"":null,""Tags$add"":[""a""],""Tags$remove"":{""$key"":""Id"",""$items"":[""b""]},
                 ""Effects$set"":[""burn""],""Stats"":{""str"":1},""Extra"":{""Any$add"":1},""$delete"":false}");
            ModAssert.Equal(0, codes.Count, "no issue: " + string.Join(",", codes));
        }

        [ModTest("patches/typo_in_field_name_is_named_with_the_near_field")]
        static void Typo(ModTestContext ctx)
        {
            ModAssert.True(Codes(@"{""Id"":""x"",""Cots"":3}").SequenceEqual(new[] { "W:SCHEMA_PATCH_UNKNOWN_FIELD" }), "unknown field");
            ModAssert.True(Message(@"{""Id"":""x"",""Cots"":3}").Contains("did you mean 'Cost'"), "near field suggested");
            ModAssert.True(Message(@"{""Id"":""x"",""Tgas$add"":[""a""]}").Contains("did you mean 'Tags'"), "typo with an operation");
            ModAssert.False(Message(@"{""Id"":""x"",""Durability"":3}").Contains("did you mean"), "nothing near — no guess");
        }

        [ModTest("patches/wrong_value_type_is_an_error")]
        static void WrongType(ModTestContext ctx)
        {
            ModAssert.True(Codes(@"{""Id"":""x"",""Cost"":{""A"":1}}").SequenceEqual(new[] { "E:SCHEMA_PATCH_BAD_TYPE" }), "object for an int");
            ModAssert.True(Codes(@"{""Id"":""x"",""Cost"":""many""}").SequenceEqual(new[] { "E:SCHEMA_BAD_VALUE" }), "text for an int");
            ModAssert.True(Codes(@"{""Id"":""x"",""Tags"":""a""}").SequenceEqual(new[] { "E:SCHEMA_PATCH_BAD_TYPE" }), "scalar for a list");
            ModAssert.True(Codes(@"{""Id"":""x"",""Stats"":[1]}").SequenceEqual(new[] { "E:SCHEMA_PATCH_BAD_TYPE" }), "array for a map");
            ModAssert.True(Codes(@"{""Id"":""x"",""Tags$add"":""a""}").SequenceEqual(new[] { "E:SCHEMA_PATCH_BAD_TYPE" }), "operation value not an array");
            ModAssert.True(Codes(@"{""Id"":""x"",""Mode$set"":""medium""}").SequenceEqual(new[] { "W:SCHEMA_BAD_VALUE" }), "$set is checked like a value");
        }

        [ModTest("patches/add_or_remove_on_a_scalar_is_an_error")]
        static void OperationOnScalar(ModTestContext ctx)
        {
            ModAssert.True(Codes(@"{""Id"":""x"",""Name$add"":[""broken""]}").SequenceEqual(new[] { "E:SCHEMA_PATCH_OP_ON_SCALAR" }), "$add on a locale field");
            ModAssert.True(Codes(@"{""Id"":""x"",""Cost$remove"":[1]}").SequenceEqual(new[] { "E:SCHEMA_PATCH_OP_ON_SCALAR" }), "$remove on an int");
            ModAssert.True(Codes(@"{""Id"":""x"",""Stats$add"":[{""str"":1}]}").SequenceEqual(new[] { "E:SCHEMA_PATCH_OP_ON_SCALAR" }), "$add on a map");
            ModAssert.True(Codes(@"{""Id"":""x"",""Tags$push"":[""a""]}").SequenceEqual(new[] { "E:SCHEMA_PATCH_BAD_OPERATION" }), "unknown operation");
            ModAssert.True(Codes(@"{""Id"":""x"",""$add"":[""a""]}").SequenceEqual(new[] { "E:SCHEMA_PATCH_BAD_OPERATION" }), "operation without a field");
        }
    }
}
