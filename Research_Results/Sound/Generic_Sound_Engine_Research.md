The original effect is a 22-entry descriptor table and a nibble-coded sample. CSBwin turns that sample into an 8-bit, 6000 Hz mono WAV and plays it through the system mixer. Distance is Manhattan distance on the current coordinates. Walls, doors, and facing do not change the volume. There is no background music in the dungeon loop.

The Atari interrupt routine in `Sound.cpp` 774–809 is commented out. Its note says the author thinks it ran at about 8 kHz. That rate is his guess, not a measured value. The running decoder writes 6000 samples per second (`Sound.cpp` 302). No DOS sound routine was opened.

## 1. What still runs

`QueueSound` (`CSBCode.cpp` 1878) is the original entry. It reads `sound1772[soundNum]`, tests distance, and either starts the sample or schedules timer 20.

`StartSound` (`Sound.cpp` 635) is the CSBwin replacement for the Atari output routine. It reads the sample count, decodes the nibbles, builds a WAV header, and hands the buffer to `SOUNDER`. The second argument, `byte3` from the descriptor, is ignored.

`SOUNDER` (`Sound.cpp` 368 and 611) is also CSBwin. Windows keeps three wave slots. Linux keeps one. A new sound is queued behind a playing one when a slot is free. The Linux path stops the current sound and replaces it (`Sound.cpp` 556–597). If every slot is full, the new sound is dropped. `VBLMultiplier` other than 1 discards every sound (`Sound.cpp` 617).

The old priority test is still visible and is not compiled. It compared `byte4` of the new sound with the current sound and kept the higher value (`CSBCode.cpp` 1962–1970).

`SoundFilter` (`Sound.cpp` 659) is a CSBwin DSA hook. It can swallow the sound before playback. The Euclidean distance it computes is passed to that filter only. The original audibility test does not use it.

Custom sounds from `ReadSound` (`Sound.cpp` 977) are a separate CSBwin file format: delta-encoded bytes, then 11025 Hz playback. They are not `GRAPHICS.DAT` samples.

The MIDI player in `CSBUI.cpp` is compiled only with `_MIDI`. It plays a media file. It is not called by dungeon events.

## 2. The 22 descriptors

Graphic `0x232` (decimal 562) holds `sound1772[22]` at offset 58. Each record is 8 bytes. `word0` is little-endian (`CSBCode.cpp` 10645). The other six bytes are signed `i8` fields, but the distance tests cast `byte6` and `byte7` to unsigned.

| **Offset** | **Field** | **Live use**                                                                      |
| ---------: | :-------- | :-------------------------------------------------------------------------------- |
|          0 | `word0`   | Unexpanded graphic index. Playback ORs it with `0x8000`.                          |
|          2 | `byte2`   | Not read.                                                                         |
|          3 | `byte3`   | Passed to `StartSound` and ignored.                                               |
|          4 | `byte4`   | Stored in a delayed timer and ignored. It was the priority in the commented test. |
|          5 | `byte5`   | Not read.                                                                         |
|          6 | `byte6`   | Maximum Manhattan distance for full volume, inclusive.                            |
|          7 | `byte7`   | Distance at which the sound is discarded, inclusive.                              |

The numeric bytes and graphic indices were not dumped. Sample length is not in this table. It is the first word of each sound graphic. Duration is that count divided by 6000. There is no per-sound volume in the descriptor. Full volume uses divisor 1. The far band uses divisor 18, then the game-volume divisor (`Sound.cpp` 650–654).

These IDs are the ones the game actually passes to `QueueSound`. IDs with no direct call can still be reached through the monster attack map.

| **ID** | **Where it is used**                                                                                                                                                                                                                   |
| -----: | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
|      0 | A dropped weapon. Also a thrown weapon that is not a spell (`Code11f52.cpp` 813, 930, 2131).                                                                                                                                           |
|      1 | Pressure plate and wall button. `soundPRESSUREPAD` (`Code11f52.cpp` 3930, `Mouse.cpp` 552 and 1104).                                                                                                                                   |
|      2 | Every door-animation step (`Timer.cpp` 1348).                                                                                                                                                                                          |
|      4 | Monster death when no weapon drops. Door striking a monster. Bash result. A thrown object that is not a weapon or poison bolt. A click on stone (`Code11f52.cpp` 930 and 2143, `Timer.cpp` 1325, `Attack.cpp` 1786, `Mouse.cpp` 1175). |
|      5 | Fireball, poison, or lightning cloud whose size is greater than 80. Also cloud type `0xffe4` (`Code11f52.cpp` 1457, `Timer.cpp` 548).                                                                                                  |
|      6 | A pit fall that damages the party, and champion death (`Code11f52.cpp` 3118, `Code1f9e6.cpp` 129).                                                                                                                                     |
|      8 | One gulp while eating or drinking (`Character.cpp` 4135, 4150, 4307, 4507).                                                                                                                                                            |
|   9–12 | Champion injury. The index is `champion + 9` (`Monster.cpp` 4582).                                                                                                                                                                     |
|     13 | A cloud that is not fireball, poison, lightning, or monster death. Also a poison-bolt impact (`Code11f52.cpp` 1466 and 2137).                                                                                                          |
|     16 | A physical attack, including the start of a door bash (`Attack.cpp` 1063 and 1776).                                                                                                                                                    |
|     17 | Teleport. `soundTELEPORT`. Also an audible monster generator (`Code11f52.cpp` 3005, `Timer.cpp` 1758).                                                                                                                                 |
|     18 | Party movement is blocked and the collision damages a champion (`CSBCode.cpp` 8647–8671). `soundDOOROPENING` is this same number, played when a closing door damages the party (`Timer.cpp` 1278).                                     |
|     20 | The same explosion clouds when size is 80 or less (`Code11f52.cpp` 1457).                                                                                                                                                              |

IDs 3, 7, 14, 15, 19, and 21 have no direct call. They remain in the 22-entry table. Their graphic numbers and distance bytes were not read, so they are not named here.

## 3. Sample coding

Archive decompression and sample decoding are separate. `GetBasicGraphicAddress` with bit `0x8000` returns the unexpanded graphic (`Graphics.cpp` 2625–2630). `StartSound` then treats that block as audio:

count = little-endian word at byte 0

nibbles start at byte 2

Each nibble is one event (`Sound.cpp` 205–278):

even nibble = high 4 bits of the current byte

odd nibble  = low 4 bits, then advance one byte

if nibble != 0:

&#x20;   emit one unsigned sample, nibble << 4

else:

&#x20;   repeat = 0

&#x20;   repeat reading nibbles:

&#x20;       repeat = (repeat << 3) | (nibble & 7)

&#x20;       stop when (nibble & 8) == 0

&#x20;   repeat += 3

&#x20;   if repeat > samples remaining: repeat = remaining

&#x20;   emit the previous sample that many times

A zero nibble is a repeat code, not silence. The sample values that occur are `0x10` through `0xF0`. The decoder copies the previous output byte, so the first nibble of a valid sample is nonzero. The output is unsigned 8-bit, one channel. The WAV header says 6000 Hz, 8 bits, block align 1.

Volume is applied by rebuilding a 256-byte table. For source values below 128:

(sample - 128 - volume/2) / volume + 128

For values from 128 up, the volume term is added instead of subtracted (`Sound.cpp` 190–193). Full distance uses volume 1. The far band uses 18. That result is multiplied by the menu divisor: 1, 2, 4, or 8 (`CSBwin.cpp` 199–202).

samples = []

previous = None

while len(samples) < count:

&#x20;   nibble = next_nibble()

&#x20;   if nibble != 0:

&#x20;       previous = nibble << 4

&#x20;       samples.append(previous)

&#x20;   else:

&#x20;       repeat = 0

&#x20;       while True:

&#x20;           nibble = next_nibble()

&#x20;           repeat = (repeat << 3) | (nibble & 7)

&#x20;           if (nibble & 8) == 0:

&#x20;               break

&#x20;       repeat += 3

&#x20;       samples.extend([previous] \* min(repeat, count - len(samples)))

Write those bytes as unsigned 8-bit PCM at 6000 Hz. Do not run this decoder on a normal image graphic, and do not treat the archive decompressor as this step.

## 4. Distance

distance = abs(soundX - partyX) + abs(soundY - partyY)

if distance <= byte6:        full volume

else if distance >= byte7:   discard

else:                        divisor 18

(`CSBCode.cpp` 1909–1941). This is Manhattan distance. There is no facing test, no stereo pan, and no test against walls or closed doors. A sound behind the party uses the same formula.

A delayed request is discarded immediately when `LoadedLevel != partyLevel` (`CSBCode.cpp` 1886–1889). An immediate request does not compare levels. It uses the loaded level’s coordinates against the party coordinates.

The only attenuation steps are divisor 1 and divisor 18, followed by the four menu divisors. There is no distance-to-volume table.

## 5. Doors

Every door uses sound 2. The door type, including the values that select a portcullis or wooden panel, is not read by the sound call. Sound 2 is queued once per state step, with delay 1 (`Timer.cpp` 1348). The state changes by one per timer tick, from 0 through 4. The sound length does not set that interval.

Sound 18 plays when a closing door damages the party. Sound 4 plays when the door damages a monster (`Timer.cpp` 1278 and 1325). Sound 16 plays when a bash starts, and sound 4 is queued with delay 2 after `HitDoor` (`Attack.cpp` 1776–1786). A plate or button plays sound 1 once, from the actuator, not from the door.

The original door has five logical states. A 4-frame, 16-frame, 24-frame, 32-frame, or 48-frame animation is interpolation between those states. The original audio event belongs to the state change, not to each interpolated frame.

## 6. Monsters

Movement does not call `QueueSound`. No footstep function appears in the monster timer path.

`MONSTERDESC.attackSound` is byte 1 of each 26-byte record (`CSB.h` 2301). The 27 records start at offset 676 of graphic `0x22f`. If the byte is nonzero, the attack plays:

QueueSound(uByte9672[attackSound - 1], monsterX, monsterY, 1)

(`Monster.cpp` 1260–1265`). `uByte9672\` is eight bytes at offset 668 of the same graphic. Neither the 27 ordinals nor the eight sound IDs were read, so there is no per-creature assignment in this report.

Death uses sound 4 unless a weapon is dropped, in which case it uses sound 0 (`Code11f52.cpp` 930). A generator uses sound 17 only when its audible bit is set (`Timer.cpp` 1756–1758).

Champion injury from a monster is sound `9 + championIndex`, delayed by two ticks (`Monster.cpp` 4582).

## 7. Champions and objects

Party steps are silent. Sound 18 plays only when blocked movement also damages a champion. A physical attack plays sound 16 at the start. A thrown weapon plays sound 0 when it finishes as a weapon. Other thrown objects play sound 4, and a poison bolt plays sound 13 (`Code11f52.cpp` 2131–2146).

Eating and drinking play sound 8 once per gulp. Champion death plays sound 6. Picking an object up has no `QueueSound`. Clicking stone in the center of the view plays sound 4 (`Mouse.cpp` 1171–1175\`). Placing an object plays sound 1 only when that placement activates an audible actuator. Stairs have no sound call.

## 8. Magic and the dungeon

Fireball, poison, and lightning choose sound 5 or 20 from the cloud size. Other spell clouds use sound 13. Teleporters use sound 17. Pits use sound 6 when the fall damages the party. Switches and plates use sound 1 when the actuator is marked audible. A false wall has no sound of its own.

Nothing in the dungeon loop schedules a looping ambience. Each call is one event. Spell failure has no `QueueSound` in `CastSpell`.

## 9. Several sounds at once

if delay > 1:

&#x20;   if not on the party level: discard

&#x20;   timer 20 at time + delay - 1

&#x20;   timer 20 calls QueueSound again with delay 1

distance = Manhattan distance

if distance >= byte7: discard

if a sound-filter DSA consumes it: discard

decode and give the WAV to SOUNDER

if a slot is free: queue it

if Windows and all three slots are full: drop it

if Linux and one sound is already playing: stop it and play the new one

There is no duplicate suppression and no live priority comparison. A plate and the door it opens are two calls: sound 1 from the actuator, then sound 2 on each later door tick. A fireball during that door is another independent call. Several falling objects each call the pit or impact sound themselves.

Timer 20 is function 20 (`CSBCode.cpp` 6832). It does not carry the volume. The delayed call repeats the distance test.

## 10. Table locations

| **Table**           | **Graphic**                |                  **Offset** |             **Count** | **Stride** |
| :------------------ | :------------------------- | --------------------------: | --------------------: | ---------: |
| `sound1772`         | `0x232` / 562              |                          58 |                    22 |          8 |
| `uByte9672`         | `0x22f` / 559              |                         668 |                     8 |          1 |
| `MonsterDescriptor` | `0x22f` / 559              |                         676 |                    27 |         26 |
| Sound samples       | index in `sound1772.word0` | 0 of the unexpanded graphic | one word plus nibbles |          — |

`MonsterDescriptor` byte 1 is the attack-sound ordinal. The sample graphic’s first word is the unsigned little-endian sample count. The menu volume divisors are code, not a graphic table: 1, 2, 4, 8 (`CSBwin.cpp` 199–202).

## Unresolved

The 22 descriptor bytes, the eight attack-map bytes, and the 27 `attackSound` bytes were not read from the graphics. Sample counts were not read, so durations are not known. The commented Atari routine’s 8 kHz note is not a verified rate. No DOS playback code was compared.


