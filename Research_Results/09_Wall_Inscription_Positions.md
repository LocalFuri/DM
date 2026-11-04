# Dungeon Master – Wall Inscription Positions

Research supplied by Cursor on 4 November 2026.

## Source and decoding

- Source: `C:\!!!Stick\Coder\DM\Maps\DUNGEON.DAT` (33,357 bytes), identical to `DM1\DATA\DUNGEON.DAT`.
- CSBwin 5-bit text encoding: 0–25 = A–Z; 26 = space; 27 = `.`; 28 = line break; 31 = end of string. Code 30 inserts a string from graphic `0x22f` in `CSBWin-master\graphics.dat` (THE and YOU); slot 1 (`<30>B` at the end of Lord Chaos's message) is empty.
- X and Y are local level coordinates, matching `DungeonMaster_LevelXX.json`. For a text stored on a stone tile, the table uses the neighboring readable floor tile and the direction faced to read the inscription. Texts on open/teleporter tiles retain their tile coordinates.
- 91 total text records: 24 champion definitions and **67 inscriptions**. Levels 07 and 13 have no inscriptions.
- `\n` within a quoted inscription below represents an **original stored line break**. Empty lines are preserved. The four Level 12 ending records share the same tile and wall.

## All 67 inscriptions

| Level | X | Y | Wall | Exact text (original line breaks shown as `<br>`) |
|---|---:|---:|:---:|---|
| 00 | 9 | 1 | N | IT IS TOO BAD YOU<br>DID NOT DISCOVER<br>THE TRUE SECRET<br>OF THE FIRESTAFF.<br>NOW THAT I HAVE<br>IT I HAVE NO<br>FURTHER NEED FOR<br>YOU |
| 00 | 1 | 4 | N | WELCOME BACK<br>BRAVE<br>ADVENTURERS. |
| 00 | 8 | 4 | S | HALL OF<br>CHAMPIONS |
| 00 | 4 | 17 | E | VI<br>ALTAR OF<br>REBIRTH |
| 01 | 29 | 2 | E | STEP INSIDE<br>TAKE A RIDE |
| 01 | 22 | 8 | W | THIS WALL SAYS<br>NOTHING |
| 01 | 13 | 16 | E | TO CLOSE PIT<br>LEAVE A<br>VALUABLE<br>ON FLOOR |
| 01 | 18 | 26 | S | THIS FOUNTAIN<br>ACCEPTS ONE<br>WISH. |
| 01 | 28 | 27 | S | NONE SHALL<br>PASS |
| 02 | 15 | 8 | E | HIT AND RUN |
| 02 | 13 | 12 | W | THE MATRIX |
| 02 | 15 | 12 | W | TIME IS OF<br>THE ESSENCE |
| 02 | 9 | 14 | S | YOU MUST PAY<br>FOR YOUR<br>ENTRANCE. |
| 02 | 11 | 14 | S | THE VAULT |
| 02 | 17 | 14 | N | ROOM OF THE<br>GEM |
| 02 | 20 | 14 | S | STEP RIGHT UP<br>GOING DOWN... |
| 02 | 7 | 16 | S | CAST YOUR<br>INFLUENCE<br>CAST YOUR<br>MIGHT |
| 02 | 11 | 16 | S | CHAMBERS OF<br>THE GUARDIAN |
| 02 | 17 | 16 | N | CREATURE<br>CAVERN |
| 02 | 14 | 18 | W | CHOOSE YOUR<br>DOOR<br>CHOOSE YOUR<br>FATE |
| 02 | 27 | 29 | E | VI<br>ALTAR OF<br>REBIRTH |
| 03 | 6 | 3 | S | YOU WILL<br>REGRET THAT. |
| 03 | 8 | 3 | S | THIS IS MY<br>PRISONER. LET<br>HIM SUFFER. |
| 03 | 29 | 7 | S | SHORTCUT |
| 03 | 14 | 17 | E | SHORTCUT BACK |
| 03 | 30 | 30 | N | PREPARE TO<br>MEET YOUR DOOM |
| 03 | 23 | 31 | W | DONT LET A<br>CLOSED DOOR<br>STOP YOU |
| 04 | 12 | 12 | E | TREASURE<br>STORES |
| 04 | 8 | 27 | W | STORE ROOMS |
| 05 | 14 | 4 | S | HA HA HA |
| 05 | 18 | 9 | W | TEST YOUR<br>STRENGTH |
| 05 | 20 | 12 | N | I HATE COWARDS |
| 05 | 20 | 13 | E | IF YOU WANT TO<br>STAY ALIVE YOU<br>BETTER TURN<br>AND RUN |
| 05 | 20 | 14 | S | I DONT LIKE TO<br>BE IGNORED |
| 05 | 19 | 17 | E | THE GRAVE OF<br>KING MILIAS<br>THE GOLDEN<br>WHO EVEN |
| 05 | 8 | 18 | N | I AM ALL<br>I AM NONE |
| 05 | 11 | 18 | N | A GOLDEN<br>HEAD AND TAIL<br>BUT NO<br>BODY |
| 05 | 19 | 18 | E | IN DEATH<br>THIRSTS FOR<br>BULLION. |
| 05 | 7 | 20 | S | THE RIDDLE<br>ROOM |
| 05 | 17 | 21 | N | THE GRAVE OF<br>KING FILIUS<br>EXPLORER OF<br>COMBINATIONS |
| 05 | 8 | 22 | S | HARD AS ROCKS<br>BLUE AS SKY<br>TWINKLE IN<br>WOMANS EYE |
| 05 | 11 | 22 | S | I ARCH YET<br>HAVE NO BACK |
| 05 | 25 | 28 | W | ALTAR OF VI |
| 06 | 4 | 1 | N | SUNDRY<br>SUPPLIES |
| 06 | 9 | 2 | E | FIRE<br>ELEMENTS |
| 06 | 13 | 2 | E | EARTH<br>ELEMENTS |
| 06 | 17 | 2 | E | AIR<br>ELEMENTS |
| 06 | 21 | 2 | E | WATER<br>ELEMENTS |
| 06 | 4 | 3 | W | CLEAN FLASKS |
| 06 | 5 | 3 | E | NOTES<br>SCROLLS<br>FORMULAS<br>SPELLS |
| 06 | 5 | 5 | N | DANGER<br><br>ENTER WITH<br>CAUTION |
| 06 | 28 | 6 | W | READING ROOM<br><br>KEEP OUT |
| 06 | 26 | 23 | W | THE TOMB OF<br>THE FIRESTAFF |
| 08 | 16 | 5 | E | WHAT IS UNDER<br>FOOT IS SOON<br>OVERHEAD |
| 08 | 23 | 15 | N | LIGHTER THAN<br>A FEATHER |
| 08 | 7 | 29 | S | WHEN IS ROCK<br>NOT ROCK |
| 09 | 3 | 12 | W | CHOOSE A DOOR |
| 09 | 14 | 12 | N | BEWARE MY<br>TWISTED HUMOR<br>THE DECEIVER<br>THE SNAKE |
| 09 | 22 | 13 | E | ZOOOOOM |
| 10 | 9 | 0 | E | CLOCKWISE |
| 10 | 0 | 3 | S | TURN BACK |
| 10 | 20 | 25 | E | ENLARGE<br>MY<br>VIEW |
| 11 | 24 | 10 | E | COWARDS WILL<br>BE HUNTED DOWN<br>AND<br>KILLED |
| 12 | 0 | 0 | N | ATHANK YOU MY<br>FRIENDS.  YOU<br>HAVE BANISHED<br>CHAOS AND<br>REJECTED THE<br>FALSE PATH OF<br>UNCOMPROMISING<br>ORDER. |
| 12 | 0 | 0 | N | BONLY BY LEARNING<br>THE TRUTH AND<br>SEEKING THE PATH<br>OF BALANCE<br>DID YOU GUESS THE<br>TRUE NATURE OF<br>THE FIRESTAFF. |
| 12 | 0 | 0 | N | CBY YOUR BRAVE<br>DEEDS I AM MADE<br>WHOLE AGAIN AND<br>HARMONY IS<br>RESTORED TO THE<br>WORLD. |
| 12 | 0 | 0 | N | DYOUR NAMES WILL<br>BE RECORDED IN<br>THE GREAT HALL<br>AND REMEMBERED BY<br>THE FUTURE<br>GENERATIONS YOU<br>HAVE MADE<br>POSSIBLE. |

## Visibility and special records

Eight records have their original **show bit clear** and are not drawn initially: `WELCOME BACK` (Level 00); `YOU WILL REGRET THAT.` and `SHORTCUT BACK` (Level 03); `READING ROOM` (Level 06); plus all four Level 12 ending strings. The Level 12 strings share local `(0,0)`, wall N, with level map offset `(25,30)`; each text begins with literal `A`, `B`, `C`, or `D` in the decoded output. In the first ending string, `FRIENDS.` is followed by **two spaces**. Cursor found no CSBwin code path stripping the leading A/B/C/D characters.

## Level JSON comparison

All 91 text records match `located_things` across all 14 levels. The `wall_inscriptions` field exists only on Level 00, and its coordinate/encoding conventions are inconsistent:

| Inscription | Research table | Existing Level 00 `wall_inscriptions` |
|---|---|---|
| HALL OF CHAMPIONS | (8,4), S | Same; converted from source record (8,5), N |
| WELCOME BACK | (1,4), N | Same tile; retains `<27>` instead of `.` |
| VI ALTAR OF REBIRTH | (4,17), E | Keeps stone record (5,17), W |
| Lord Chaos message | (9,1), N | Same tile; retains `<30>C` for `THE`, `<27>` for `.`, `<30>B` for empty slot |

## Champion text records (excluded from inscriptions)

The other 24 Level 00 text records are champion definitions (show bit clear), interpreted as names/titles/stats rather than wall inscriptions. Positions below refer to the text-object records:

| X | Y | Wall | Name | Title |
|---:|---:|:---:|---|---|
| 6 | 13 | N | DAROOU | |
| 7 | 9 | N | HALK | THE BARBARIAN |
| 7 | 13 | N | WU TSE | SON OF HEAVEN |
| 7 | 16 | N | AZIZI | JOHARI |
| 8 | 15 | N | LEIF | THE VALIANT |
| 9 | 7 | N | ELIJA | LION OF YAITOPYA |
| 9 | 9 | N | SYRA | CHILD OF NATURE |
| 9 | 13 | N | TIGGY | TAMAL |
| 10 | 4 | N | IAIDO | RUYITO CHIBURI |
| 10 | 5 | N | ZED | DUKE OF BANVILLE |
| 11 | 10 | N | GANDO | THURFOOT |
| 11 | 15 | N | STAMM | BLADECASTER |
| 12 | 9 | N | LINFLAS | |
| 12 | 13 | N | WUUF | THE BIKA |
| 13 | 12 | N | LEYLA | SHADOWSEEK |
| 13 | 14 | N | MOPHUS | THE HEALER |
| 14 | 3 | N | CHANI | SAYYADINA SIHAYA |
| 14 | 6 | N | BORIS | WIZARD OF BALDOR |
| 14 | 12 | N | SONJA | SHE DEVIL |
| 15 | 4 | N | HAWK | THE FEARLESS |
| 16 | 8 | N | ALEX | ANDER |
| 16 | 14 | N | HISSSSA | LIZAR OF MAKAN |
| 16 | 17 | N | GOTHMOG | |
| 17 | 9 | N | NABI | THE PROPHET |

*This document transcribes Cursor's provided research; no independent re-extraction of `DUNGEON.DAT` was performed here.*
