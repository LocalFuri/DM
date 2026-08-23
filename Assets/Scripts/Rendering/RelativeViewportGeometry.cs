using System;
using DM.Dungeon;

namespace DM.Rendering
{
    /// <summary>
    /// One dungeon-map cell expressed in viewport-relative geometry.
    /// Coordinates remain the original map coordinates; Type is preserved when inside the map.
    /// Out-of-bounds is treated as solid by IsWall, matching the existing ViewEdit wall rules.
    /// </summary>
    public readonly struct RelativeViewportCell
    {
        public int X { get; }
        public int Y { get; }
        public bool IsInside { get; }
        public DungeonTileType Type { get; }

        public bool IsWall => !IsInside || Type == DungeonTileType.Wall;

        public RelativeViewportCell(int x, int y, bool isInside, DungeonTileType type)
        {
            X = x;
            Y = y;
            IsInside = isInside;
            Type = type;
        }
    }

    /// <summary>
    /// Deterministic map-to-viewport geometry for one player pose.
    /// This class contains no rendering, piece visibility, width, mirror, X/Y placement,
    /// draw-order, pose-store, or special-case logic.
    /// </summary>
    public sealed class RelativeViewportGeometry
    {
        public int PlayerX { get; }
        public int PlayerY { get; }
        public DungeonFacing Facing { get; }

        public RelativeViewportCell F0Left { get; }
        public RelativeViewportCell F0Right { get; }

        public RelativeViewportCell F1Left { get; }
        public RelativeViewportCell F1Center { get; }
        public RelativeViewportCell F1Right { get; }

        public RelativeViewportCell F2Left { get; }
        public RelativeViewportCell F2Center { get; }
        public RelativeViewportCell F2Right { get; }

        public RelativeViewportCell F3Left { get; }
        public RelativeViewportCell F3Center { get; }
        public RelativeViewportCell F3Right { get; }

        private RelativeViewportGeometry(
            int playerX,
            int playerY,
            DungeonFacing facing,
            RelativeViewportCell f0Left,
            RelativeViewportCell f0Right,
            RelativeViewportCell f1Left,
            RelativeViewportCell f1Center,
            RelativeViewportCell f1Right,
            RelativeViewportCell f2Left,
            RelativeViewportCell f2Center,
            RelativeViewportCell f2Right,
            RelativeViewportCell f3Left,
            RelativeViewportCell f3Center,
            RelativeViewportCell f3Right)
        {
            PlayerX = playerX;
            PlayerY = playerY;
            Facing = facing;

            F0Left = f0Left;
            F0Right = f0Right;

            F1Left = f1Left;
            F1Center = f1Center;
            F1Right = f1Right;

            F2Left = f2Left;
            F2Center = f2Center;
            F2Right = f2Right;

            F3Left = f3Left;
            F3Center = f3Center;
            F3Right = f3Right;
        }

        public static RelativeViewportGeometry Calculate(
            DungeonMap map,
            int playerX,
            int playerY,
            DungeonFacing facing)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            DungeonMap.GetForwardOffset(
                facing,
                out int forwardX,
                out int forwardY);
            DungeonMap.GetRightOffset(
                facing,
                out int rightX,
                out int rightY);

            RelativeViewportCell CellAt(int forwardSteps, int rightSteps)
            {
                int x = playerX + forwardX * forwardSteps + rightX * rightSteps;
                int y = playerY + forwardY * forwardSteps + rightY * rightSteps;
                bool inside = map.IsInside(x, y);
                DungeonTileType type = inside
                    ? map.GetTile(x, y).Type
                    : default;

                return new RelativeViewportCell(x, y, inside, type);
            }

            return new RelativeViewportGeometry(
                playerX,
                playerY,
                facing,
                CellAt(0, -1),
                CellAt(0, 1),
                CellAt(1, -1),
                CellAt(1, 0),
                CellAt(1, 1),
                CellAt(2, -1),
                CellAt(2, 0),
                CellAt(2, 1),
                CellAt(3, -1),
                CellAt(3, 0),
                CellAt(3, 1));
        }
    }
}
