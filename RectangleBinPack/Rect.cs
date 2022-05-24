using System.Collections.Generic;

namespace RectangleBinPack
{
    /// Describes a horizontal slab of space where rectangles may be placed.
    public struct Shelf
    {
        public Shelf(int currentX, int startY, int height)
        {
            CurrentX = currentX;
            StartY = startY;
            Height = height;

            UsedRectangles = new List<Rect>();
        }

        /// The x-coordinate that specifies where the used shelf space ends.
        /// Space between [0, currentX[ has been filled with rectangles, [currentX, binWidth[ is still available for filling.
        public int CurrentX {get;set;}

        /// The y-coordinate of where this shelf starts, inclusive.
        public int StartY { get;set; }

        /// Specifices the height of this shelf. The topmost shelf is "open" and its height may grow.
        public int Height {get;set;}

        /// Lists all the rectangles in this shelf.
        public List<Rect> UsedRectangles {get;set;}
    }

    public struct RectSize
    {
        public int width;
        public int height;
    }

    public struct Rect
    {
        public Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public bool IsContainedIn(Rect other)
        {
            return X >= other.X && Y >= other.Y && X + Width <= other.X + other.Width &&
                   Y + Height <= other.Y + other.Height;
        }

        public void Swap()
        {
            var temp = Width;
            Width = Height;
            Height = temp;
        }
    }

    public class DisjointRectCollection : List<Rect>
    {
        public new bool Add(Rect rect)
        {
            // Degenerate rectangles are ignored.
            if (rect.Width == 0 || rect.Height == 0) return true;

            if (!Disjoint(rect)) return false;

            base.Add(rect);

            return true;
        }

        public bool Disjoint(Rect rect)
        {
            // Degenerate rectangles are ignored.
            if (rect.Width == 0 || rect.Height == 0) return true;

            for (var i = 0; i < Count; i++)
            {
                if (!Disjoint(this[i], rect)) return false;
            }

            return true;
        }

        public bool Disjoint(Rect a, Rect b)
        {
            return a.X + a.Width <= b.X ||
                   b.X + b.Width <= a.X ||
                   a.Y + a.Height <= b.Y ||
                   b.Y + b.Height <= a.Y;
        }
    }
}