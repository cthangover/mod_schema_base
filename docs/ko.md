# Schemas — 행동, 명령, 데이터 필드의 설명

Schemas는 라이브러리입니다. 어떤 모드든 자신의 시나리오 행동이 무엇을 받는지, DSL 명령이 어떤 모양인지, 데이터 종류마다 어떤 필드가 있는지를 데이터로 설명할 수 있게 해 줍니다. 도구는 이 설명으로 양식, 선택기, 검사를 만들므로, 작성자는 매개변수 이름을 외우는 대신 양식을 채우고, 실수는 게임을 실행하기 전에 잡힙니다.

이 모드에는 자체 창이 없습니다. 눈에 보이는 부분은 다른 도구가 보여 줍니다:

- **Schema Tools** — 명령 팔레트와 '데이터 에디터' 창;
- **Validator** — 보고서의 알 수 없거나 빠진 매개변수, 깨진 참조;
- **아이템·캐릭터 에디터** — 다른 모드가 아이템과 캐릭터에 더하는 필드.

## 설명에서 양식과 검사로

```text
items_base/schemas/items_base.json        설명. 행동 코드 바로 옆에 둠
        │
        ▼
스키마 카탈로그 (모든 모드, 로드 순서)
        │
        ├──► 명령 팔레트의 양식              매개변수마다 필드 하나
        ├──► 시나리오 줄                     action item.remove id=… count=…
        └──► 검사기                          팔레트에서도 검증기에서도 같은 판정
```

Items Engine은 자신의 행동 `item.remove`을 이렇게 설명합니다:

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

RPG Plus의 명령 팔레트는 이것을 양식으로 보여 줍니다: **Item**은 게임 아이템의 목록(유형 `ref`, 종류 `item`), **Count**는 1이 채워진 숫자입니다. 아이템을 고르기 전까지 검사기는 무엇이 빠졌는지 알려 줍니다:

![Remove item을 고른 명령 팔레트: 설명, "모드 items_base가 설명함", 필드 Item(비어 있음)과 Count(1), 시나리오 줄 "action item.remove", 오류 "'item.remove'에 'id'가 필요합니다".](assets/palette-item-remove.jpg)

Stats Engine의 `stat.archetype`에는 필수 매개변수가 없으므로 같은 빈 양식도 받아들여집니다:

![Give an archetype를 고른 명령 팔레트: 필드 Archetype와 Who, 줄 "action stat.archetype", "줄에 문제 없음".](assets/palette-archetype.jpg)

불러온 모드가 설명한 모든 행동과 명령이 팔레트 목록에 카테고리별로 나타나고, 바닥글이 개수를 세어 줍니다:

![명령 팔레트 전체: 카테고리별 행동과 명령 목록(Battle, Characters, Items, Quests, Stats, World, Dialog, Flow)과 "설명됨: 행동 25개, 명령 10개".](assets/palette.jpg)

## 스키마 파일

모드는 `schemas/<mod>.json`에 최대 세 개의 목록을 담습니다:

- `Actions` — 시나리오 행동(`action <id> key=value …`): 그 매개변수;
- `Commands` — `check`나 `select` 같은 DSL 명령: 위치 값과 이름 있는 매개변수;
- `Kinds` — 데이터 종류(`item`, `character`, `shop`…): 그 엔티티의 필드, 새 파일의 폴더, 새 id의 패턴.

이름과 설명은 모드 자체 로컬라이즈의 번역 키이므로, 양식은 게임의 모든 언어로 말합니다.

필드 유형: `text`, `locale`(모든 언어의 텍스트), `int`, `number`, `bool`, `choice`(`Options`와 함께), `ref`와 `ref_list`(한 종류의 id), `who`, `picture`, `formula`, `condition`, `scene`, `flag`, `label`, `list`(자체 필드를 가진 객체의 목록), `values`, `map`, `json`. 알 수 없는 유형은 텍스트로 편집됩니다.

## 여러 모드가 한 종류를 설명할 때

파일은 모드 로드 순서대로 읽힙니다:

- 행동이나 명령이 다시 설명되면 나중 모드가 **통째로 대체**합니다;
- 종류는 **필드별로 합쳐집니다**: 같은 키의 필드는 대체되고, 새 필드는 더해집니다.

그래서 Equipment는 Items Engine의 `item`에 자신의 필드를, 그 파일을 건드리지 않고 더합니다:

```json
{ "Kind": "item",
  "Fields": [
    { "Key": "Equip", "Type": "text", "Name": "equipment/schema/field/equip" },
    { "Key": "Archetypes", "Type": "ref_list", "Kind": "archetype", "Name": "equipment/schema/field/archetypes" }
  ] }
```

모든 필드는 어느 모드가 설명했는지 기억합니다(팔레트의 "모드 items_base가 설명함"). 깨진 스키마 파일은 보고되고 건너뛰며, 나머지 파일은 계속 효력을 가집니다.

규칙은 각 모드가 자신의 행동, 명령, 종류를 그 코드 옆에 설명하는 것입니다. 다른 모드의 행동을 고치기 위해 설명할 수도 있고, 그때는 로드 순서가 이깁니다.

## 검사기가 보고하는 것

- **매개변수 필요** — 필수 매개변수가 없음(오류);
- **알 수 없는 매개변수** — 줄이 설명에 없는 매개변수를 이름으로 지목함(경고);
- **잘못된 값** — 정수가 아님, 숫자가 아님, 선택지에 없음(오류. 예/아니오와 선택지는 경고);
- **알 수 없는 참조** — id가 불러온 어느 모드도 정의하지 않는 아이템, 캐릭터 따위의 엔티티를 가리킴(오류).

검증기는 같은 내용을 `SCHEMA_MISSING_PARAM`, `SCHEMA_UNKNOWN_PARAM`, `SCHEMA_BAD_VALUE`, `SCHEMA_UNKNOWN_REF`로 보고합니다. 검사기는 설명에 쓰인 것만 압니다: 받아들인 줄이라도 게임 안에서는 뜻밖의 일을 할 수 있습니다.

## 문제가 생겼을 때

1. **행동이 팔레트에 없습니다.** 불러온 모드 중 아무도 설명하지 않았습니다. 그래도 행동은 시나리오 안에서 작동합니다.
2. **필드가 평범한 텍스트 상자로 보입니다.** 유형을 알 수 없거나 필드 설명이 아예 없습니다(이때 데이터 에디터는 그것을 다른 필드 아래 JSON으로 보여 줍니다).
3. **양식이 당신의 스키마를 무시합니다.** 로드 순서에서 뒤인 모드가 같은 행동을 설명해 당신 것을 대체했습니다.
