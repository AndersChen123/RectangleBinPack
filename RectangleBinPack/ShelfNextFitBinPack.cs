using System;

namespace RectangleBinPack
{
    public struct Node
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public bool Flipped;
    }

    public class ShelfNextFitBinPack
    {
        private int _binWidth;
        private int _binHeight;

        private int _currentX;
        private int _currentY;
        private int _shelfHeight;
        private ulong _usedSurfaceArea;

        public void Init(int width, int height)
        {
            _binWidth = width;
            _binHeight = height;

            _currentX = 0;
            _currentY = 0;
            _shelfHeight = 0;
            _usedSurfaceArea = 0;
        }

        private static void Swap(ref int a, ref int b)
        {
            var temp = a;
            a = b;
            b = temp;
        }

        public Node Insert(int width, int height)
        {
            Node newNode = default;
            // There are three cases:
            // 1. short edge <= long edge <= shelf height. Then store the long edge vertically.
            // 2. short edge <= shelf height <= long edge. Then store the short edge vertically.
            // 3. shelf height <= short edge <= long edge. Then store the short edge vertically.

            // If the long edge of the new rectangle fits vertically onto the current shelf,
            // flip it. If the short edge is larger than the current shelf height, store
            // the short edge vertically.
            if (((width > height && width < _shelfHeight) ||
                 (width < height && height > _shelfHeight)))
            {
                newNode.Flipped = true;
                Swap(ref width, ref height);
            }
            else
                newNode.Flipped = false;

            if (_currentX + width > _binWidth)
            {
                _currentX = 0;
                _currentY += _shelfHeight;
                _shelfHeight = 0;

                // When starting a new shelf, store the new long edge of the new rectangle horizontally
                // to minimize the new shelf height.
                if (width < height)
                {
                    Swap(ref width, ref height);
                    newNode.Flipped = !newNode.Flipped;
                }
            }

            // If the rectangle doesn't fit in this orientation, try flipping.
            if (width > _binWidth || _currentY + height > _binHeight)
            {
                Swap(ref width, ref height);
                newNode.Flipped = !newNode.Flipped;
            }

            // If flipping didn't help, return failure.
            if (width > _binWidth || _currentY + height > _binHeight)
            {
                return newNode;
            }

            newNode.Width = width;
            newNode.Height = height;
            newNode.X = _currentX;
            newNode.Y = _currentY;

            _currentX += width;
            _shelfHeight = Math.Max(_shelfHeight, height);

            _usedSurfaceArea += (ulong)(width * height);

            return newNode;
        }

        /// Computes the ratio of used surface area.
        public float Occupancy()
        {
            return (float)_usedSurfaceArea / (_binWidth * _binHeight);
        }
    }
}
