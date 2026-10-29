# Schemas — mô tả hành động, lệnh và trường dữ liệu

Schemas là một thư viện: nó cho phép mọi mod mô tả, dưới dạng dữ liệu, hành động kịch bản của mình nhận những gì, lệnh DSL của mình có dạng thế nào và các loại dữ liệu của mình có những trường nào. Từ một mô tả như vậy, các công cụ dựng biểu mẫu, bộ chọn và phép kiểm tra, nên tác giả chỉ cần điền biểu mẫu thay vì nhớ tên tham số, và lỗi được báo trước khi chạy game.

Mod này không có cửa sổ riêng. Phần bạn thấy được do các công cụ khác hiển thị:

- **Schema Tools** — bảng lệnh và cửa sổ Trình sửa dữ liệu;
- **Validator** — tham số lạ hoặc thiếu, tham chiếu hỏng trong báo cáo của nó;
- các **trình sửa vật phẩm và nhân vật** — những trường mà mod khác thêm vào vật phẩm và nhân vật.

## Từ mô tả đến biểu mẫu và phép kiểm tra

```text
items_base/schemas/items_base.json        mô tả, đặt cạnh mã của hành động
        │
        ▼
danh mục lược đồ (mọi mod, theo thứ tự tải)
        │
        ├──► biểu mẫu trong bảng lệnh        mỗi tham số một trường
        ├──► dòng kịch bản                   action item.remove id=… count=…
        └──► bộ kiểm tra                     cùng một kết luận trong bảng lệnh và trình kiểm tra
```

Items Engine mô tả hành động `item.remove` của mình thế này:

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

Bảng lệnh của RPG Plus hiển thị nó như một biểu mẫu: **Item** là danh sách vật phẩm của game (kiểu `ref`, loại `item`), **Count** là một số đã điền sẵn 1. Cho đến khi chọn một vật phẩm, bộ kiểm tra sẽ cho biết thiếu gì:

![Bảng lệnh với Remove item được chọn: mô tả của nó, "Mod items_base mô tả", các trường Item (trống) và Count (1), dòng kịch bản "action item.remove" và lỗi "'item.remove' cần 'id'".](assets/palette-item-remove.jpg)

`stat.archetype` của Stats Engine không có tham số bắt buộc nên cùng một biểu mẫu trống vẫn được chấp nhận:

![Bảng lệnh với Give an archetype được chọn: các trường Archetype và Who, dòng "action stat.archetype" và "Dòng ổn".](assets/palette-archetype.jpg)

Mọi hành động và lệnh mà các mod đã tải mô tả đều xuất hiện trong danh sách của bảng lệnh, nhóm theo loại; chân bảng đếm chúng:

![Toàn bộ bảng lệnh: danh sách hành động và lệnh theo loại (Battle, Characters, Items, Quests, Stats, World, Dialog, Flow) và "Đã mô tả: 25 hành động, 10 lệnh".](assets/palette.jpg)

## Tệp lược đồ

Mod kèm `schemas/<mod>.json` với tối đa ba danh sách:

- `Actions` — hành động kịch bản (`action <id> key=value …`): các tham số của chúng;
- `Commands` — các lệnh DSL như `check` hay `select`: giá trị vị trí và tham số đặt tên;
- `Kinds` — các loại dữ liệu (`item`, `character`, `shop`…): trường của thực thể, thư mục cho tệp mới và mẫu của id mới.

Tên và mô tả là khóa dịch trong bản địa hóa của chính mod đó, nên biểu mẫu nói được mọi ngôn ngữ của game.

Các kiểu trường: `text`, `locale` (một văn bản cho mọi ngôn ngữ), `int`, `number`, `bool`, `choice` (kèm `Options`), `ref` và `ref_list` (id của một loại), `who`, `picture`, `formula`, `condition`, `scene`, `flag`, `label`, `list` (danh sách các đối tượng có trường riêng), `values`, `map`, `json`. Kiểu lạ được sửa như văn bản.

## Nhiều mod mô tả một loại

Tệp được đọc theo thứ tự tải mod:

- hành động hay lệnh được mô tả lại sẽ bị mod sau **thay thế toàn bộ**;
- loại thì được **gộp theo từng trường**: trường cùng khóa bị thay, trường mới được thêm.

Nhờ vậy Equipment thêm trường của mình vào `item` của Items Engine mà không đụng tới tệp đó:

```json
{ "Kind": "item",
  "Fields": [
    { "Key": "Equip", "Type": "text", "Name": "equipment/schema/field/equip" },
    { "Key": "Archetypes", "Type": "ref_list", "Kind": "archetype", "Name": "equipment/schema/field/archetypes" }
  ] }
```

Mỗi trường đều nhớ mod nào đã mô tả nó ("Mod items_base mô tả" trong bảng lệnh). Tệp lược đồ hỏng được báo cáo và bỏ qua; các tệp khác vẫn được tính.

Nguyên tắc là mỗi mod mô tả hành động, lệnh và loại của chính mình, cạnh mã của chúng. Một mod có thể mô tả hành động của mod khác để sửa nó, và khi đó thứ tự tải quyết định.

## Bộ kiểm tra báo cáo gì

- **cần một tham số** — thiếu tham số bắt buộc (lỗi);
- **không có tham số này** — dòng nêu một tham số mà mô tả không biết (cảnh báo);
- **giá trị sai** — không phải số nguyên, không phải số, không nằm trong các lựa chọn (lỗi, hoặc cảnh báo với có/không và các lựa chọn);
- **tham chiếu lạ** — id gọi tới một vật phẩm, nhân vật hay thực thể khác mà không mod nào đã tải định nghĩa (lỗi).

Trình kiểm tra báo những điều tương tự dưới mã `SCHEMA_MISSING_PARAM`, `SCHEMA_UNKNOWN_PARAM`, `SCHEMA_BAD_VALUE` và `SCHEMA_UNKNOWN_REF`. Bộ kiểm tra chỉ biết những gì mô tả nói: một dòng nó chấp nhận vẫn có thể làm điều bất ngờ trong game.

## Khi có trục trặc

1. **Hành động không có trong bảng lệnh.** Không mod nào đã tải mô tả nó; hành động vẫn chạy trong kịch bản.
2. **Một trường hiện thành ô văn bản thường.** Kiểu của nó lạ, hoặc trường không có mô tả nào (Trình sửa dữ liệu khi đó hiện nó trong phần trường khác, dưới dạng JSON).
3. **Biểu mẫu bỏ qua lược đồ của bạn.** Một mod đứng sau trong thứ tự tải đã mô tả cùng hành động và thay thế mô tả của bạn.
