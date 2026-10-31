namespace Cthangover.Mods.Schemas.Tests
{
    using System.Linq;
    using Cthangover.Core.Mods.Testing;

    /// <summary>Finds and renames the references to one id: typed fields, list rows, DSL lines and conditions.</summary>
    public class SchemaReferenceScannerTests
    {
        private const string Schemas = @"{
  ""Actions"": [ { ""Id"": ""item.add"", ""Params"": [ { ""Key"": ""id"", ""Type"": ""ref"", ""Kind"": ""item"" }, { ""Key"": ""count"", ""Type"": ""int"" } ] } ],
  ""Commands"": [ { ""Id"": ""give"", ""Positional"": [ { ""Key"": ""item"", ""Type"": ""ref"", ""Kind"": ""item"" } ], ""Params"": [ { ""Key"": ""to"", ""Type"": ""who"" } ] } ],
  ""Kinds"": [
    { ""Kind"": ""item"", ""Fields"": [ { ""Key"": ""Name"", ""Type"": ""locale"" }, { ""Key"": ""Sprite"", ""Type"": ""picture"", ""Group"": ""items"" } ] },
    { ""Kind"": ""shop"", ""Fields"": [ { ""Key"": ""Currency"", ""Type"": ""ref"", ""Kind"": ""item"" }, { ""Key"": ""Sells"", ""Type"": ""list"", ""Of"": [ { ""Key"": ""Item"", ""Type"": ""ref"", ""Kind"": ""item"" } ] } ] },
    { ""Kind"": ""character"", ""Fields"": [ { ""Key"": ""Gear"", ""Type"": ""ref_list"", ""Kind"": ""item"" } ] }
  ]
}";

        private static SchemaCatalog Catalog() => SchemaCatalog.Build(new[] { ("test", "schemas/test.json", Schemas) });

        private static ReferenceHit Scan(string path, string text, string oldId = "mod/amber", string newId = "mod/amber2", string kind = "item")
            => SchemaReferenceScanner.Scan(Catalog(), "mod", path, text, kind, oldId, newId);

        [ModTest("scanner/own_row_id_and_picture")]
        static void OwnRow(ModTestContext ctx)
        {
            var hit = Scan("items/mod.json", "{ \"Kind\": \"item\", \"Items\": [ { \"Id\": \"mod/amber\", \"Sprite\": \"mod/amber\" } ] }");
            ModAssert.NotNull(hit, "the row is found");
            ModAssert.Equal(1, hit.Count, "only the id counts: a picture named like the id is no reference");
            ModAssert.True(hit.NewText.Contains("\"Id\": \"mod/amber2\"") && hit.NewText.Contains("\"Sprite\": \"mod/amber\""), "the id is renamed, the picture stays");
        }

        [ModTest("scanner/list_rows_and_ref_list")]
        static void ListRows(ModTestContext ctx)
        {
            var shop = Scan("shops/mod.json", "{ \"Kind\": \"shop\", \"Items\": [ { \"Id\": \"s\", \"Currency\": \"mod/amber\", \"Sells\": [ { \"Item\": \"mod/amber\", \"Price\": 3 } ] } ] }");
            ModAssert.Equal(2, shop.Count, "a ref and a list row");
            ModAssert.True(shop.Places.Contains("Currency") && shop.Places.Contains("Sells[].Item"), "places name the fields");
            var character = Scan("characters/mod.json", "{ \"Kind\": \"character\", \"Items\": [ { \"Id\": \"hero\", \"Gear\": [ \"mod/amber\", \"x\" ] } ] }");
            ModAssert.Equal(1, character.Count, "an array of a ref_list");
            ModAssert.True(character.NewText.Contains("\"mod/amber2\""), "the array item is renamed");
        }

        [ModTest("scanner/scenario_lines")]
        static void ScenarioLines(ModTestContext ctx)
        {
            var hit = Scan("scenarios/a.scenario", "action item.add id=mod/amber count=1\nflag x 1\naction item.add id=mod/amber_2\n");
            ModAssert.Equal(1, hit.Count, "exact ids only: mod/amber_2 is another item");
            ModAssert.Equal("action item.add id=mod/amber2 count=1\nflag x 1\naction item.add id=mod/amber_2\n", hit.NewText, "the line is rewritten, the rest is byte for byte");
        }

        [ModTest("scanner/positional_and_quotes")]
        static void Positional(ModTestContext ctx)
        {
            var hit = Scan("scenarios/b.scenario", "give \"mod/amber\" to=hero -> :done\n");
            ModAssert.Equal(1, hit.Count, "a positional ref");
            ModAssert.Equal("give mod/amber2 to=hero -> :done\n", hit.NewText, "the positional value is rewritten");
        }

        [ModTest("scanner/item_condition_in_json_string")]
        static void Condition(ModTestContext ctx)
        {
            var hit = Scan("maps/m.json", "{ \"Kind\": \"placement\", \"Items\": [ { \"Id\": \"p\", \"When\": \"item('mod/amber') == 0 && flag == 1\" } ] }");
            ModAssert.Equal(1, hit.Count, "one item() condition");
            ModAssert.True(hit.NewText.Contains("item('mod/amber2') == 0"), "the quote style is kept");
        }

        [ModTest("scanner/other_kind_and_patches")]
        static void OtherKinds(ModTestContext ctx)
        {
            var patch = Scan("patches/item/mod.json", "{ \"Kind\": \"item\", \"Items\": [ { \"Id\": \"mod/amber\", \"Cost\": 5 } ] }");
            ModAssert.Equal(1, patch.Count, "a patch entry of the id is a reference");
            ModAssert.Equal(0, Scan("things/x.json", "{ \"Kind\": \"stat\", \"Items\": [ { \"Id\": \"mod/amber\" } ] }").Count, "a stat with the same id is another kind");
            ModAssert.Equal(0, Scan("things/y.json", "{ \"Kind\": \"item\", \"Items\": [ { \"Id\": \"other\", \"Name\": \"mod/amber\" } ] }").Count, "a text that names the id is no reference");
        }

        [ModTest("scanner/plan_splits_edited_and_other_mods")]
        static void Plan(ModTestContext ctx)
        {
            var files = new[]
            {
                ("mod", "shops/a.json", "{ \"Kind\": \"shop\", \"Items\": [ { \"Id\": \"s\", \"Currency\": \"mod/amber\" } ] }"),
                ("other", "scenarios/o.scenario", "action item.add id=mod/amber\n"),
                ("mod", "scenarios/n.scenario", "flag a 1\n"),
            };
            var plan = SchemaReferenceScanner.Plan(Catalog(), "mod", files, "item", "mod/amber", "mod/amber2");
            ModAssert.Equal(1, plan.Own.Count, "one own file changes");
            ModAssert.NotNull(plan.Own[0].NewText, "the own file has its new text");
            ModAssert.Equal(1, plan.Other.Count, "one other mod file is listed");
            ModAssert.Null(plan.Other[0].NewText, "and is not changed");
        }
    }
}
