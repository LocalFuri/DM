`ReadDatabases` (`SaveGame.cpp` 2511) reads the dungeon in one fixed order. The first word decides whether the rest is compressed. After that, every section is a counted array of little-endian words, and a trailing checksum word must match the sum the loader accumulated. No separate DOS layout appears in this loader. The extended-feature block in front of the dungeon is CSBwin.

## 1. File order

If the first little-endian word is 33028 (`0x8104`), the file is compressed (`SaveGame.cpp` 2538):

| **Offset** | **Size** | **Meaning**                                                                 |
| ---------: | -------: | :-------------------------------------------------------------------------- |
|          0 |        2 | Sentinel 33028.                                                             |
|          2 |        4 | Uncompressed size.                                                          |
|          6 |        2 | Stored in `Word22592`. The comment says `0x000c` distinguishes DM from CSB. |
|          8 |     rest | Compressed payload.                                                         |

`ExpandData` (`CSBCode.cpp` 10299) starts with 20 lookup bytes, then 16-bit words. A bit string `0xx` indexes the 4-byte table, `10yyyy` indexes the 16-byte table, and `11zzzzzzzz` is a literal byte. If the sentinel is absent, the loader rewinds and reads the file as already expanded.

A CSBwin extended-feature block may sit before this header. Its size is skipped when locating the payload (`SaveGame.cpp` 2540–2556). Flags in that block select indirect text, 10-byte actuators, sequenced timers, and expanded portraits (`SaveGame.cpp` 405–412). Version `'@'` means the file still uses the original 16-bit object names and must be converted.

The expanded stream is then:

1. 44-byte `DUNGEONDATINDEX`.
2. 16 bytes times the number of levels.
3. One little-endian word per column across all levels.
4. One little-endian object name per object-list entry.
5. Text. The original form is `NumWordsInTextArray` little-endian words. The indirect form is that many 32-bit big-endian offsets, then a big-endian size, then the compressed words.
6. Databases 0 through 15, in order. Scrolls use a separate reader.
7. `LegacyCellFlagArraySize` raw cell bytes.
8. One little-endian checksum word, read but not added into the sum (`SaveGame.cpp` 2837–2840).

The running sum is updated by `FetchDataBytes`. A mismatch returns 0. The generator in `GenChecksum` (`SaveGame.cpp` 651) adds each word and that word exclusive-or the previous seed, then adds the remaining word count to the seed.

The 44-byte index (`CSB.h` 2090) is twelve header bytes plus sixteen database counts:

| **Offset** | **Field**                                                   |
| ---------: | :---------------------------------------------------------- |
|          0 | Sentinel.                                                   |
|          2 | Cell-flag array size, in bytes.                             |
|          4 | Bits 8–15 are the level count. Bits 0–7 are not read.       |
|          6 | Number of text words, or indirect-text entries.             |
|          8 | Bits 0–4 starting X, 5–9 starting Y, 10–11 starting facing. |
|         10 | Number of object-list entries stored in the file.           |
|         12 | Sixteen little-endian counts, databases 0–15.               |

The party is placed at level index 0 using that X, Y, and facing (`SaveGame.cpp` 2592–2597). On a new game the loader also reserves 300 extra empty object-list entries and extra database slots from `Byte7302`. Those reservations are not part of the file.

## 2. Level descriptor

Each level is 16 bytes (`CSB.h` 2117). Words 0, 8, 10, 12, and 14 are swapped. Bytes 2–5 are not swapped and are not read.

| **Offset** | **Field**                                                                                                            |
| ---------: | :------------------------------------------------------------------------------------------------------------------- |
|          0 | Low 16 bits of the cell-array offset.                                                                                |
|          2 | Four unused bytes.                                                                                                   |
|          6 | Global X origin.                                                                                                     |
|          7 | Global Y origin.                                                                                                     |
|          8 | Bits 0–5 level number, 6–10 width minus 1, 11–15 height minus 1.                                                     |
|         10 | Bits 0–3 wall-decoration count, 4–7 random wall decorations, 8–11 floor decorations, 12–15 random floor decorations. |
|         12 | Bits 0–3 door decorations, 4–7 monster types. Bits 8–11 are not read. Bits 12–15 are the experience multiplier.      |
|         14 | Bits 0–3 floor and ceiling set, 4–7 wall set, 8–11 first door type, 12–15 second door type.                          |

Width is `LastColumn() + 1`. Height is `LastRow() + 1`. The level-number field is not required to equal the descriptor’s array index.

The cell pointer for level 0 is `cellFlagArray + cellOffset`. Each later level adds the unsigned 16-bit difference of the stored offsets (`SaveGame.cpp` 2858–2865). Column 0 points at that cell. Column `n` is `height` bytes after column `n - 1`. Local coordinate `(x, y)` is global `(x + offsetX, y + offsetY)`. The object-list conversion uses that sum (`SaveGame.cpp` 2917).

A zero experience multiplier forces palette 0, which is the full-light case already traced. Any other value selects the torch calculation. The same field multiplies experience.

Floor and ceiling graphics are `2 * backgroundGraphicSet + 75` and `+ 76`, from the comment on bits 0–3. Wall decorations and door ornaments are ordinals into the level’s own lists, not absolute graphic numbers. The graphic-number formulas were traced in the ornament and door reports and are not repeated here.

## 3. The cell byte

Bits 5–7 are the room type (`CSBTypes.h` 202):

| **Value** | **Type**   | **Other bits**                                                                  |
| --------: | :--------- | :------------------------------------------------------------------------------ |
|         0 | Stone      | Bits 0–3 request a random decoration on the west, south, east, and north sides. |
|         1 | Open       | Bit 4 is the only shared flag below.                                            |
|         2 | Pit        | Bit 0 false pit, bit 2 obscure, bit 3 open.                                     |
|         3 | Stairs     | Bit 2 stairs up, bit 3 north/south.                                             |
|         4 | Door       | Bits 0–2 state 0–5, bit 3 north/south.                                          |
|         5 | Teleporter | Bit 2 visible, bit 3 active.                                                    |
|         6 | False wall | Bit 0 and bit 2. Bit 2 is the open state changed by timer 7.                    |

Bit 4 is set when the cell has an object list. It is not geometry. Door state, pit-open, teleporter-active, and false-wall-open are changed at runtime. The room type and the remaining geometry bits are loaded from the file and are not rewritten by the actuator timers, except those active bits.

The count of random decorations is in the level descriptor. The function that turns a set cell bit into a decoration ordinal was not re-read, so the index formula is not stated.

## 4. Databases

Every record begins with a 16-bit link. `0xfffe` ends a list. `0xffff` marks an unused record. The file sizes below are the sizes `ReadDatabases` uses. Actuators are two bytes shorter unless the CSBwin big-actuator flag is set. Runtime actuators are then padded to 10 bytes (`SaveGame.cpp` 2749–2785).

| **ID** |            **File size** | **Contents**                                                                                                               |
| -----: | -----------------------: | :------------------------------------------------------------------------------------------------------------------------- |
|      0 |                        4 | Door flags. Bit 0 type, 1–4 ornateness, 5 upward, 6 button, 7 fireball, 8 axe.                                             |
|      1 |                        6 | Teleporter destination and filter.                                                                                         |
|      2 |                        4 | Text. Bit 0 visible, bits 3–15 text index. The name array labels this “Scrolls”; the class and `dbTEXT` are text.          |
|      3 |                 8, or 10 | Actuator. Runtime adds two bytes.                                                                                          |
|      4 |                       16 | Monster group.                                                                                                             |
|      5 |                        4 | Weapon type and flags.                                                                                                     |
|      6 |                        4 | Clothing type and flags.                                                                                                   |
|      7 |                versioned | Scroll. Before extended version `'B'`, a 10-bit text index (`SaveGame.cpp` 2416). After that, a text reference plus flags. |
|      8 |                        4 | Potion strength and type.                                                                                                  |
|      9 |                        8 | Chest contents link and type bits.                                                                                         |
|     10 |                        4 | Miscellaneous type and value.                                                                                              |
|  11–13 | 0 in the source comments | The name array prints question marks. No record layout is defined.                                                         |
|     14 |  8 in the source comment | Missile.                                                                                                                   |
|     15 |   runtime `sizeof(DB15)` | Cloud.                                                                                                                     |

Databases 11–13 are present in the count table so the loop can skip them. They are not CSBwin extensions. The 10-byte actuator, indirect text, and the indirect object map are CSBwin.

## 5. Object lists

Original names are little-endian 16-bit words (`CSBTypes.h` 242–246, `SaveGame.cpp` 2162):

position = bits 14–15

database = bits 10–13

index    = bits 0–9

`0xfffe` ends the chain. `0xffff` is an empty record. Spell objects are `0xff80` through `0xfffd`.

Column index `objectLevelIndex[level] + x` selects a word in `objectListIndex`. That word is the first object-list entry for the column. Walking `y` from 0 and counting cells whose bit 4 is set gives the entry for `(x, y)` (`CSBCode.cpp` 5442). The first name in that entry is the head of the linked list. Containers, monster possessions, and missile payloads are separate lists reached from the record, and `ConvertListOfObjects` follows all three (`SaveGame.cpp` 2216–2240).

columns_before = sum over earlier levels of (width)

column_base = objectListIndex[columns_before + x]

n = count of cells in this column with y' < y and bit 4 set

head = objectList[column_base + n]

while head != 0xfffe:

&#x20;   emit head

&#x20;   head = record[head].link

An index past the database count is rejected during conversion. A second reference to the same database record aborts unless the caller allows a duplicate, which is used for a missile’s payload (`SaveGame.cpp` 2165–2190).

## 6. Text and champions

Original text is a sequence of 16-bit words. Each word holds three 5-bit codes, and a code of 31 ends the entry (`SaveGame.cpp` 2430–2443). `DecodeText` (`CSBCode.cpp` 5834) expands the entry selected by the text record’s index. Type 0 is a wall inscription. Type `0x8002` is the champion text and is decoded even when the visible bit is clear.

`AddCharacter` (`Character.cpp` 553–724) parses that expansion, separated by byte 10:

1. Name, at most 7 characters, then 10.
2. Title, at most 19 characters. An embedded 10 is kept only once.
3. `M` or `F`, then one ignored byte.
4. Four characters of health, four of stamina, four of mana. Each character is `'A'` through `'P'`, a nibble, so four characters are 16 bits (`DecodeCharacterValue`, `Character.cpp` 421). Health starts equal to the maximum, and the same is true for stamina and mana.
5. One ignored byte, then seven attributes. Each is two nibbles, limited to 224. Maximum and current are set to that value. Minimum starts at 30, then Luck’s minimum is set to 10.
6. Sixteen letters for skills 4 through 19. `'A'` is zero experience. `'B'` through `'R'` store `125 << (letter - 'A')`, unless the neophyte option changes the zero case.
7. Three more letters: reincarnation attribute penalty, statistic penalty, and random points. Values above 16, 16, and 25 become 2, 8, and 3.
8. Eight nibbles of talents. Truncation is allowed.

The four main skills are then the sums of their four subskills. Nothing in this parser sets an inventory list. After the champion is accepted, objects on the cell in front of the party, at the position opposite the facing, are moved into the first matching slot (`Character.cpp` 740–804). Clothing tries slots 2–5 and then the neck. A weapon tries the weapon hand. Scrolls and potions try slot 11 and then slot 6. Anything else tries the neck and then the next backpack slot, starting at 13.

## 7. Decorations and puzzle data

Random wall and floor decorations are counts in the level descriptor plus the stone cell bits. Explicit wall ornaments, plates, keyholes, holders, alcoves, and mirrors are actuator records on the cell. The graphic ordinal is bits 12–15 of actuator word 4. Door ornaments and the door button are fields of the door record and the level’s door-decoration list, not cell bits.

The actuator, teleporter, door, pit, and false-wall fields already traced are the entire puzzle state stored in the file:

- Target X, Y, and position, delay, action, once-only, audible, and normally closed are the actuator words.
- Key type is the actuator value.
- Counters and four-bit gates are actuator types 6 and 5 and their values.
- Generators are type 6 on an open cell.
- Teleporter destination, rotation, facing mode, filter, and level are database 1.
- Stairs and pits do not store a destination. Movement calls `IncrementLevel`.
- Door state and orientation are the cell. Door type, direction of motion, button, fireball, and axe are database 0.

Importing those records unchanged preserves the puzzles. No coordinate-specific script is present in the file.

## 8. Parser outline

if first_word == 0x8104:

&#x20;   payload = expand(rest, uncompressed_size)

else:

&#x20;   payload = file

read 44-byte index

read 16 \* num_levels

read one word per column

read object_list_length words

read text section

for db in 0..15:

&#x20;   read count[db] records at the file size for that db

read cell_flag_size bytes

expect checksum

for each level:

&#x20;   bind columns from the 16-bit cell offsets

&#x20;   bind object lists by counting bit 4

keep every unread byte in a hex field

A lossless export needs the raw cell byte, the raw database words, the raw object-list word, and the raw text word alongside anything decoded. Unknown fields are level bytes 2–5, level word 12 bits 8–11, index word 4 bits 0–7, and any database bytes the current struct does not name.

The loader’s own checks are the useful validation. The checksum word must match. Every object index must be inside its database. Every list must end at `0xfffe` rather than repeat a record. Every cell offset must land inside the cell array. Column counts must equal the sum of the level widths. Object-list entries must equal the number of cells with bit 4 set. The level rectangle must contain the starting party position.

The request stopped at “Coordinate.” Those count and bounds checks are the coordinate validation the loader can actually fail.


