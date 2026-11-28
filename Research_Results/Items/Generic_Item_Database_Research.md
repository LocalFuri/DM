Every carried or dropped thing is a database record. `DescIndex` turns that record into one of 180 object descriptors. The descriptor supplies the name index, the attack class, and the floor-graphic class. Inventory pictures are a second mapping, from the name index into graphics 42–48. Weights, charges, and the 44 attack rows were not read from the graphics.

`GetObjectWeight`’s chest default of 50, and the extension-pool override, are CSBwin. The descriptor indexes and the special-item branches below are the original mapping.

## 1. From a record to a name

`RecordName::DescIndex` (`data.cpp` 569) is the whole type map:

| **Database**      | **Descriptor index**                                                                                                          |
| :---------------- | :---------------------------------------------------------------------------------------------------------------------------- |
| 7, scroll         | Always 0.                                                                                                                     |
| 9, chest          | `bits 1–2 of word 4 + 1`. Zero selects descriptor 1. Nonzero selects 2, 3, or 4, which are also the first potion descriptors. |
| 8, potion         | `potion type + 2`. Types run from Mon Potion at 2 through Empty Flask at 22.                                                  |
| 5, weapon         | `weapon type + 23`. Forty-six types, descriptors 23–68. Type 0 is Eye of Time. Type 2 is the torch.                           |
| 6, clothing       | `clothing type + 69`. Fifty-eight types, descriptors 69–126.                                                                  |
| 10, miscellaneous | `misc type + 127`. Fifty-three types, descriptors 127–179.                                                                    |

`GetBasicObjectType` (`CSBCode.cpp` 1295) reads `ObjDesc[index].objectType()`. That is the base name index. `NameIndex` (`data.cpp` 611) then adjusts a few of those names:

| **Base name**                                                    | **When it changes**                                                     |
| :--------------------------------------------------------------- | :---------------------------------------------------------------------- |
| Compass North, 0                                                 | Add the party facing, so the name is 0–3.                               |
| Torch, 4                                                         | If the lit bit is set, add `Byte640[charges]`. The result stays in 4–7. |
| Waterskin, Illumulet, Jewel Symal                                | Add 1 when the miscellaneous value is nonzero.                          |
| Storm, Flamitt, Stormring, Ra Blade, Eye of Time, Staff of Claws | Add 1 when the weapon charge is nonzero.                                |
| Open Scroll, 30                                                  | Becomes Scroll, 31, when the scroll is closed.                          |
| Filled flasks 148–163                                            | Unchanged by this function.                                             |

The names and descriptor names are the enumerations in `Objects.h` 243–648. They cover weapons, armor, shields, eight foods, potions and bombs, scrolls, sixteen keys, three coins, gems, the named magical objects, chests, the torch, the waterskin, bones, and the quest objects through Corbum. There is no second hidden catalogue.

## 2. Descriptor layouts

All of these words are little-endian where the loader swaps them.

`OBJDESC` is 6 bytes, 180 records, graphic `0x22f` offset 1958 (`CSB.h` 1712):

| **Bytes** | **Field**                                     | **Consumer**                                                                                                                                         |
| :-------- | :-------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0–1       | Name index.                                   | `GetBasicObjectType`.                                                                                                                                |
| 2         | Graphic class, an index into `GRAPHIC_CLASS`. | `GetGraphicClass` and the floor drawer.                                                                                                              |
| 3         | Attack class, signed.                         | `DrawAttackButton` and the attack menu. Zero means the weapon has no attack menu. An empty hand uses class 2 (`Character.cpp` 4932–4944).            |
| 4–5       | Carry mask.                                   | `CarryLocation` tests. Bit 0 mouth, 1 head, 2 neck, 3 torso, 4 legs, 5 feet, 6 ready quiver, 7 stored quiver, 8 pouch, 10 chest (`CSB.h` 1720–1730). |

`WEAPONDESC` is 6 bytes, 46 records, offset 1682:

| **Bytes** | **Use that was found**                                                                                                                                                                                                  |
| :-------- | :---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0         | Weight, in the same unit as `CHARDESC.load`.                                                                                                                                                                            |
| 1         | Ammunition class. Shoot requires 10 or 11, or a launcher class of 16–31 or 32–47 (`Attack.cpp` 1846–1872).                                                                                                              |
| 2         | Not read in this pass.                                                                                                                                                                                                  |
| 3         | Added to the ammunition’s byte 3 to make shoot range (`Attack.cpp` 1879).                                                                                                                                               |
| 4–5       | Bits 8–12, if nonzero, replace the floor graphic with projectile class `-bits` (`Code51a4.cpp` 2902–2908). The whole word is also added into one shoot-damage expression and then overwritten (`Attack.cpp` 1880–1881). |

`CLOTHINGDESC` is 4 bytes, 58 records, offset 1450:

| **Byte** | **Use**                                                                                                                        |
| :------- | :----------------------------------------------------------------------------------------------------------------------------- |
| 0        | Weight.                                                                                                                        |
| 1        | Protection. Returned directly, or scaled by `(byte2 & 7) + 4` through `ScaledMultiply(..., 3, ...)` (`Character.cpp` 167–174). |
| 2        | Low 3 bits are the scale. Bit 7 must be set before a worn shield enters `TAG01680a`.                                           |
| 3        | Not read.                                                                                                                      |

Miscellaneous weights are `Byte8946[53]`, signed bytes, graphic `0x22f` offset 1394. A waterskin, miscellaneous type 1, adds `2 * value` (`Mouse.cpp` 49–56).

`FoodValue[8]` is eight little-endian words at offset 1378. Eating uses `FoodValue[name - Apple]` (`Character.cpp` 4303). The eight numbers were not read.

Potions have no separate descriptor table. Database 8 stores strength in bits 0–7 and type in bits 8–14 (`CSB.h` 1252–1259). An empty flask weighs 1. Any other potion weighs 3 (`Mouse.cpp` 78–88). A scroll weighs 1.

`CarryLocation[38]` is 38 little-endian words at graphic `0x232` offset 788. Placement succeeds only when the object’s carry mask shares a bit with the slot’s word (`CSBCode.cpp` 7332, `MoveObject.cpp` 447).

The weapon-class table is `legalAttacks[44]`, 8 bytes each, graphic `0x230` offset 672. Bytes 0–2 are attack-type numbers. Byte 255 skips an entry. Bytes 3–5 are the mastery required, and bit 7 of that byte also requires a remaining charge (`Character.cpp` 4783–4818). Bytes 6 and 7 are not read. The first action is always offered. The other two require `DetermineMastery(SkillNumber[action])` to be at least the stored mastery.

## 3. Properties the code actually computes

Weight is the sum above. A chest starts at 50 when no CSBwin weight record exists, then adds every contained object (`Mouse.cpp` 59–75). There is no stack count. Identical objects are separate records. A container is a linked list ending at `RNeof`. No maximum length was found.

Melee damage is not a weapon byte. It is throwing distance, multiplied by `Byte19958[action] / 32`, then reduced by monster defense, as traced in the combat report. `Byte20002[action]` is the luck argument `75 - value`. Shoot damage is set from the launcher’s byte 1 after the ammunition test, not from the discarded `word4` sum (`Attack.cpp` 1875–1889).

Armor is clothing byte 1, passed through `TAG009470`, then averaged by `DamageCharacter`. Charges are weapon bits 10–13 or clothing bits 9–12. Cursed is weapon or clothing bit 8, and it subtracts 3 from Luck’s minimum while the object is in slots 0–12 (`Character.cpp` 1629–1648).

Food, potion strength, and waterskin value are the fields already named. Nothing in these functions gives a container a fixed slot count.

## 4. Graphics

record

&#x20; -> DescIndex()

&#x20; -> ObjDesc.name and ObjDesc.graphicClass

&#x20; -> NameIndex() for the icon

&#x20; -> graphic class for the floor

The floor bitmap is graphic `360 + GRAPHIC_CLASS[class].objectGraphicIndex` (`Code51a4.cpp` 2607). `GRAPHIC_CLASS` is 85 six-byte records at graphic `0x22e` offset 1642. A weapon whose descriptor bits 8–12 are nonzero uses the projectile table `STRUCT5688` instead.

The inventory icon does not use that class. `DrawItem` (`Character.cpp` 31) finds the first `Word612` boundary above the name index and copies a 16×16 cell from graphic `42 + group`. `Word612[7]` is at graphic `0x232` offset 1218. The group boundaries were not read, so the icon index of each object is not stated here.

Alternate pictures that are code, not extra descriptors, are the torch, waterskin, Illumulet, Jewel Symal, the six charged weapons, and the open or closed scroll. An open chest is drawn as name `OpenChest` while the chest screen is up (`Character.cpp` 3067). Its stored descriptor remains the chest record.

## 5. The 44 actions

Graphic `0x230`, 44 signed or unsigned bytes each:

| **Offset** | **Field**                | **What the attack code does with it**                                                                 |
| ---------: | :----------------------- | :---------------------------------------------------------------------------------------------------- |
|         64 | `experienceForAttacking` | Experience awarded (`Attack.cpp` 1711). The dump header calls this “Time”.                            |
|        108 | `SkillNumber`            | Skill passed to `DetermineMastery`.                                                                   |
|        152 | `Byte20090`              | Stored in `word64` for the duration of the swing (`Character.cpp` 4984). The dump calls it “Defense”. |
|        196 | `Byte20046`              | Stamina cost, plus `random(2)` (`Attack.cpp` 1704).                                                   |
|        240 | `Byte20002`              | Passed as `P7`. A failed quickness test can still hit through `IsCharacterLucky(champion, 75 - P7)`.  |
|        284 | `Byte19958`              | Passed as `P8`. Hit damage is multiplied by `P8 / 32`.                                                |
|        328 | `Byte19914`              | Recovery time before timer 11 (`Attack.cpp` 1697).                                                    |
|        372 | `AttackNames`            | 300 bytes of packed names.                                                                            |

The dump labels in `AsciiDump.cpp` 1820–1836 do not match those uses. No action row was read, so the 44 names and numbers are not repeated here. Accuracy is the quickness test in `DeterminePhysicalAttackDamage`, not a column in this table. Mana is zero except for wizard skills 3 and 16–19.

## 6. Code branches, not table rows

These names are tested directly:

| **Object**                                                           | **Branch**                                                                                                               |
| :------------------------------------------------------------------- | :----------------------------------------------------------------------------------------------------------------------- |
| Firestaff `_a`                                                       | Mastery +1 (`Code17818.cpp` 76).                                                                                         |
| Firestaff `_b`                                                       | Mastery +2.                                                                                                              |
| Firestaff `_c`                                                       | A name exists. No mastery branch was found.                                                                              |
| Vorpal Blade                                                         | Allows a hit on a non-material monster. Against a material monster the damage is halved (`Attack.cpp` 837–842 and 1140). |
| Diamond Edge                                                         | Monster defense reduced by one quarter.                                                                                  |
| Executioner                                                          | Monster defense reduced by one eighth.                                                                                   |
| Named staves and wands                                               | Fixed skill bonuses from 1 to 16 while held (`Character.cpp` 1680–1697).                                                 |
| Illumulet                                                            | Wearing it adds `Word1074[2]` to brightness. The name becomes the `_b` form.                                             |
| Elven Boots                                                          | `MaxLoad` gains one sixteenth.                                                                                           |
| Cursed weapon or clothing                                            | Luck minimum −3.                                                                                                         |
| Compass                                                              | The icon follows party facing.                                                                                           |
| Ekkhard Cross, Moonstone, Gem of Ages, Pendant Feral, Sceptre of Lyf | Mastery +1 for one priest or wizard skill.                                                                               |
| Crown, Dexhelm, Flamebain, Cloak of Night, Powertowers               | Fixed attribute bonuses on slots 2, 3, and 4.                                                                            |
| Bones `_a`                                                           | The Vi altar matches the saved fingerprint and removes the bones.                                                        |
| Party shield and fire shield                                         | Global values subtracted in `DamageCharacter`, not fields of a clothing record.                                          |

No branch was found that combines the Firestaff with a separate power gem. Jewel Symal only changes its name when its value word is set.

## 7. Locations

| **Table**                     | **Graphic**   |                          **Offset** | **Count** | **Stride** |
| :---------------------------- | :------------ | ----------------------------------: | --------: | ---------: |
| `GRAPHIC_CLASS`               | `0x22e` / 558 |                                1642 |        85 |          6 |
| Food values                   | `0x22f` / 559 |                                1378 |         8 |          2 |
| Miscellaneous weights         | `0x22f`       |                                1394 |        53 |          1 |
| Clothing                      | `0x22f`       |                                1450 |        58 |          4 |
| Weapons                       | `0x22f`       |                                1682 |        46 |          6 |
| Object descriptors            | `0x22f`       |                                1958 |       180 |          6 |
| Seven attack-parameter tables | `0x230` / 560 |                        64, then +44 |        44 |          1 |
| Attack names                  | `0x230`       |                                 372 | 300 bytes |          — |
| Legal attacks                 | `0x230`       |                                 672 |        44 |          8 |
| `CarryLocation`               | `0x232` / 562 |                                 788 |        38 |          2 |
| `Word612` icon groups         | `0x232`       |                                1218 |         7 |          2 |
| `Byte640` torch pictures      | `0x232`       | the 16 bytes at the `Byte640` field |        16 |          1 |

Database records themselves are not in those graphics. A weapon is 4 bytes in database 5, clothing 4 bytes in database 6, a potion 4 bytes in database 8, and a miscellaneous object 4 bytes in database 10. The chest adds a contents link.

## Unresolved

The 180 graphic-class bytes, 46 weapon rows, 58 clothing rows, 53 miscellaneous weights, 8 food values, 44 action bytes, and 7 icon boundaries were not read. Chest descriptor indexes 2–4 collide with potion indexes in the enumeration. The shoot-damage assignment overwrites the expression that used `word4`, so that expression has no effect in the current function. No DOS item table was compared.