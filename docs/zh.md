# Schemas — 动作、命令与数据字段的描述

Schemas 是一个程序库：它让每个模组都能用数据来描述自己的剧本动作接受什么参数、DSL 命令长什么样、各类数据有哪些字段。工具根据这些描述构建表单、选择器和校验，作者只需填表单而不用背参数名，错误在运行游戏之前就会被指出来。

这个模组没有自己的窗口。你能看到的部分都由其他工具呈现：

- **Schema Tools** —— 命令面板和「数据编辑器」窗口；
- **Validator** —— 在报告中指出未知或缺失的参数、断裂的引用；
- **物品与角色编辑器** —— 其他模组加到物品和角色上的字段。

## 从描述到表单与校验

```text
items_base/schemas/items_base.json        描述文件，就放在动作代码旁边
        │
        ▼
模式目录（所有模组，按加载顺序）
        │
        ├──► 命令面板中的表单                每个参数一个字段
        ├──► 剧本行                          action item.remove id=… count=…
        └──► 检查器                          在面板和验证器中给出同样的结论
```

Items Engine 这样描述自己的动作 `item.remove`：

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

RPG Plus 的命令面板把它显示为表单：**Item** 是游戏物品的列表（`ref` 类型，指向 `item` 实体），**Count** 是已填好 1 的数字。在选定物品之前，检查器会说明缺了什么：

![选中 Remove item 的命令面板：描述文字、「由模组 items_base 描述」、字段 Item（空）和 Count（1）、剧本行「action item.remove」以及错误「'item.remove' 需要 'id'」。](assets/palette-item-remove.jpg)

Stats Engine 的 `stat.archetype` 没有必填参数，所以同样的空表单也能通过：

![选中 Give an archetype 的命令面板：字段 Archetype 和 Who、剧本行「action stat.archetype」以及「这一行没有问题」。](assets/palette-archetype.jpg)

已加载模组描述的每个动作和命令都会出现在面板列表中，按类别分组；底部给出统计：

![完整的命令面板：按类别分组的动作与命令列表（Battle、Characters、Items、Quests、Stats、World、Dialog、Flow）以及「已描述：25 个动作、10 个命令」。](assets/palette.jpg)

## 模式文件

模组附带 `schemas/<mod>.json`，其中最多有三份列表：

- `Actions` —— 剧本动作（`action <id> key=value …`）：它们的参数；
- `Commands` —— `check`、`select` 之类的 DSL 命令：位置参数和命名参数；
- `Kinds` —— 数据类型（`item`、`character`、`shop`…）：其实体的字段、新文件的目录和新 id 的命名规则。

名称和描述是模组自己本地化里的翻译键，所以表单会说游戏里的每种语言。

字段类型：`text`、`locale`（每种语言一段文本）、`int`、`number`、`bool`、`choice`（带 `Options`）、`ref` 和 `ref_list`（某类型的 id）、`who`、`picture`、`formula`、`condition`、`scene`、`flag`、`label`、`list`（由带各自字段的对象组成的列表）、`values`、`map`、`json`。未知类型按文本编辑。

## 多个模组描述同一个类型

文件按模组加载顺序读取：

- 再次描述的动作或命令会被后面的模组**整体替换**；
- 类型则**按字段合并**：键相同的字段被替换，新字段被添加。

于是 Equipment 给 Items Engine 的 `item` 加了自己的字段，而不用碰那个文件：

```json
{ "Kind": "item",
  "Fields": [
    { "Key": "Equip", "Type": "text", "Name": "equipment/schema/field/equip" },
    { "Key": "Archetypes", "Type": "ref_list", "Kind": "archetype", "Name": "equipment/schema/field/archetypes" }
  ] }
```

每个字段都记得是哪个模组描述了它（面板中的「由模组 items_base 描述」）。有问题的模式文件会被报告并跳过；其余文件照常生效。

规则是：每个模组在自己的代码旁描述自己的动作、命令和类型。模组也可以描述别的模组的动作来纠正它——这时加载顺序靠后者获胜。

## 检查器报告什么

- **缺少参数** —— 缺了必填参数（错误）；
- **参数不存在** —— 行里写了描述中不认识的参数（警告）；
- **值不对** —— 不是整数、不是数字、不在可选值里（错误；对是否值和选项则是警告）；
- **引用不存在** —— id 指向的物品、角色或其他实体没有任何已加载模组定义（错误）。

验证器把同样的问题报告为 `SCHEMA_MISSING_PARAM`、`SCHEMA_UNKNOWN_PARAM`、`SCHEMA_BAD_VALUE` 和 `SCHEMA_UNKNOWN_REF`。检查器只知道描述里写的内容：它放过的行仍可能在游戏里做出意料之外的事。

## 出问题了怎么办

1. **动作不在面板里。** 没有任何已加载模组描述它；它在剧本里仍然可用。
2. **字段显示为普通文本框。** 它的类型未知，或者根本没有描述（这时「数据编辑器」会把它作为 JSON 放在其他字段里）。
3. **表单不理会你的模式。** 加载顺序靠后的模组描述了同一个动作，替换了你的。
