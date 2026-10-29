# Schemas — descripciones de acciones, comandos y campos de datos

Schemas es una biblioteca: permite a cada mod describir, como datos, qué aceptan sus acciones de escenario, cómo son sus comandos DSL y qué campos tienen sus tipos de datos. A partir de esa descripción, las herramientas construyen formularios, selectores y comprobaciones, de modo que el autor rellena un formulario en lugar de memorizar nombres de parámetros, y un error se detecta antes de ejecutar el juego.

Este mod no tiene ventana propia. Lo que de él se ve lo muestran otras herramientas:

- **Schema Tools** — la paleta de comandos y la ventana Editor de datos;
- **Validator** — parámetros desconocidos o ausentes y referencias rotas en su informe;
- los **editores de objetos y personajes** — los campos que otros mods añaden a objetos y personajes.

## De la descripción al formulario y a la comprobación

```text
items_base/schemas/items_base.json        la descripción, junto al código de la acción
        │
        ▼
catálogo de esquemas (todos los mods, en orden de carga)
        │
        ├──► formulario en la paleta         un campo por parámetro
        ├──► línea de escenario              action item.remove id=… count=…
        └──► comprobación                    el mismo veredicto en la paleta y en el validador
```

Items Engine describe su acción `item.remove` así:

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

La paleta de comandos de RPG Plus lo muestra como formulario: **Item** es una lista de los objetos del juego (tipo `ref`, entidad `item`), **Count** es un número con el 1 ya rellenado. Mientras no se elija un objeto, la comprobación dice qué falta:

![La paleta de comandos con Remove item seleccionado: su descripción, «Descrito por el mod items_base», los campos Item (vacío) y Count (1), la línea de escenario «action item.remove» y el error «'item.remove' necesita 'id'».](assets/palette-item-remove.jpg)

El `stat.archetype` de Stats Engine no tiene parámetro obligatorio, así que el mismo formulario vacío se acepta:

![La paleta de comandos con Give an archetype seleccionado: los campos Archetype y Who, la línea «action stat.archetype» y «La línea está bien».](assets/palette-archetype.jpg)

Cada acción y comando que describen los mods cargados aparece en la lista de la paleta, agrupado por categorías; el pie los cuenta:

![La paleta de comandos completa: la lista de acciones y comandos por categorías (Battle, Characters, Items, Quests, Stats, World, Dialog, Flow) y «Descritos: 25 acciones, 10 comandos».](assets/palette.jpg)

## Archivos de esquemas

Un mod incluye `schemas/<mod>.json` con hasta tres listas:

- `Actions` — acciones de escenario (`action <id> key=value …`): sus parámetros;
- `Commands` — comandos DSL como `check` o `select`: valores posicionales y parámetros con nombre;
- `Kinds` — tipos de datos (`item`, `character`, `shop`…): los campos de sus entidades, la carpeta para archivos nuevos y el patrón de un id nuevo.

Los nombres y las descripciones son claves de traducción de la localización del propio mod, así que los formularios hablan todos los idiomas del juego.

Tipos de campo: `text`, `locale` (un texto en todos los idiomas), `int`, `number`, `bool`, `choice` (con `Options`), `ref` y `ref_list` (ids de un tipo), `who`, `picture`, `formula`, `condition`, `scene`, `flag`, `label`, `list` (una lista de objetos con campos propios), `values`, `map`, `json`. Un tipo desconocido se edita como texto.

## Varios mods describen un mismo tipo

Los archivos se leen en el orden de carga de los mods:

- una acción o un comando descritos de nuevo se **sustituyen enteros** por el mod posterior;
- un tipo se **fusiona campo a campo**: el campo con la misma clave se sustituye y los campos nuevos se añaden.

Así Equipment añade sus campos al `item` de Items Engine sin tocar ese archivo:

```json
{ "Kind": "item",
  "Fields": [
    { "Key": "Equip", "Type": "text", "Name": "equipment/schema/field/equip" },
    { "Key": "Archetypes", "Type": "ref_list", "Kind": "archetype", "Name": "equipment/schema/field/archetypes" }
  ] }
```

Cada campo recuerda qué mod lo describió («Descrito por el mod items_base» en la paleta). Un archivo de esquema roto se informa y se salta; los demás archivos siguen contando.

La regla es que cada mod describa sus propias acciones, comandos y tipos, junto a su código. Un mod puede describir la acción de otro para corregirla, y entonces gana el orden de carga.

## Qué informa la comprobación

- **falta un parámetro** — ausenta un parámetro obligatorio (error);
- **no existe el parámetro** — la línea nombra un parámetro que la descripción no conoce (aviso);
- **valor incorrecto** — no es un número entero, no es un número, no está entre las opciones (error, o aviso para sí/no y opciones);
- **referencia desconocida** — el id nombra un objeto, personaje u otra entidad que ningún mod cargado define (error).

El validador informa de lo mismo como `SCHEMA_MISSING_PARAM`, `SCHEMA_UNKNOWN_PARAM`, `SCHEMA_BAD_VALUE` y `SCHEMA_UNKNOWN_REF`. La comprobación solo sabe lo que dicen las descripciones: una línea que acepta todavía puede hacer algo inesperado en el juego.

## Si algo sale mal

1. **Una acción no aparece en la paleta.** Ningún mod cargado la describe; la acción sigue funcionando en los escenarios.
2. **Un campo se muestra como caja de texto sencilla.** Su tipo es desconocido, o el campo no tiene descripción alguna (el Editor de datos lo muestra entonces entre otros campos, como JSON).
3. **Un formulario ignora tu esquema.** Un mod más tarde en el orden de carga describió la misma acción y sustituyó la tuya.
