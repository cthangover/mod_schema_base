# Schemas — descriptions of actions, commands and data fields

Schemas is a library: it lets every mod describe, as data, what its scenario actions take, what its DSL commands look like and which fields its kinds of data have. From one such description the tools build forms, pickers and checks, so an author fills in a form instead of remembering parameter names, and a mistake is reported before the game is run.

This mod has no window of its own. What you see of it is shown by other tools:

- **Schema Tools** — the command palette and the Data Editor window;
- **Validator** — unknown or missing parameters and broken references in its report;
- the **items and characters editors** — fields that other mods add to items and characters.

## From a description to a form and a check

```text
items_base/schemas/items_base.json        the description, next to the action's code
        │
        ▼
schema catalog (all mods, in load order)
        │
        ├──► form in the command palette     one field per parameter
        ├──► scenario line                   action item.remove id=… count=…
        └──► checker                         same verdict in the palette and in the validator
```

Items Engine describes its action `item.remove` like this:

```json
{
  "Id": "item.remove",
  "Name": "items/schema/remove",
  "Description": "items/schema/remove_desc",
  "Category": "items",
  "Params": [
    { "Key": "id", "Type": "ref", "Kind": "item", "Required": true, "Name": "items/schema/field/item" },
    { "Key": "count", "Type": "int", "Default": "1", "Name": "items/schema/field/count" }
  ]
}
```

The command palette of RPG Plus shows it as a form: **Item** is a list of the game's items (type `ref`, kind `item`), **Count** is a number with 1 already filled in. Until an item is chosen, the checker says what is missing:

![The command palette with Remove item selected: its description, "Described by the mod items_base", the fields Item (empty) and Count (1), the scenario line "action item.remove" and the error "'item.remove' needs 'id'".](assets/palette-item-remove.jpg)

Stats Engine's `stat.archetype` has no required parameter, so the same empty form is accepted:

![The command palette with Give an archetype selected: the fields Archetype and Who, the line "action stat.archetype" and "The line is fine".](assets/palette-archetype.jpg)

Every action and command the loaded mods describe appears in the palette's list, grouped by category; the footer counts them:

![The whole command palette: the list of actions and commands by category (Battle, Characters, Items, Quests, Stats, World, Dialog, Flow) and "Described: 25 actions, 10 commands".](assets/palette.jpg)

## Schema files

A mod ships `schemas/<mod>.json` with up to three lists:

- `Actions` — scenario actions (`action <id> key=value …`): their parameters;
- `Commands` — DSL commands such as `check` or `select`: positional values and named parameters;
- `Kinds` — kinds of data (`item`, `character`, `shop`…): the fields of their entities, the folder for new files and the pattern of a new id.

Names and descriptions are translation keys in the mod's own locale, so the forms speak every language of the game.

Field types: `text`, `locale` (a text in every language), `int`, `number`, `bool`, `choice` (with `Options`), `ref` and `ref_list` (ids of a kind), `who`, `picture`, `formula`, `condition`, `scene`, `flag`, `label`, `list` (a list of objects with fields of their own), `values`, `map`, `json`. An unknown type is edited as text.

## Several mods describe one kind

Files are read in mod load order:

- an action or a command described again is **replaced whole** by the later mod;
- a kind is **merged field by field**: a field with the same key is replaced, new fields are added.

So Equipment adds its fields to Items Engine's `item` without touching that file:

```json
{ "Kind": "item",
  "Fields": [
    { "Key": "Equip", "Type": "text", "Name": "equipment/schema/field/equip" },
    { "Key": "Archetypes", "Type": "ref_list", "Kind": "archetype", "Name": "equipment/schema/field/archetypes" }
  ] }
```

Every field remembers which mod described it ("Described by the mod items_base" in the palette). A broken schema file is reported and skipped; the other files still count.

The rule is that each mod describes its own actions, commands and kinds, next to their code. A mod may describe another mod's action to correct it, and then wins by load order.

## What the checker reports

- **needs a parameter** — a required parameter is missing (error);
- **has no parameter** — the line names a parameter the description does not know (warning);
- **bad value** — not a whole number, not a number, not one of the choices (error, or a warning for yes/no and choices);
- **unknown reference** — the id names an item, character or other entity that no loaded mod defines (error).

The validator reports the same things as `SCHEMA_MISSING_PARAM`, `SCHEMA_UNKNOWN_PARAM`, `SCHEMA_BAD_VALUE` and `SCHEMA_UNKNOWN_REF`. The checker only knows what the descriptions say: a line it accepts can still do something unexpected in the game.

## If something goes wrong

1. **An action is missing from the palette.** No loaded mod describes it; the action still works in scenarios.
2. **A field shows as a plain text box.** Its type is unknown, or the field has no description at all (the Data Editor then shows it under other fields as JSON).
3. **A form ignores your schema.** A mod later in the load order describes the same action and replaced yours.
