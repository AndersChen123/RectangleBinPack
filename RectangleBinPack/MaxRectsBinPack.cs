using System;
using System.Collections.Generic;

namespace RectangleBinPack
{
    public class MaxRectsBinPack
    {
        public enum FreeRectChoiceHeuristic
        {
            RectBestShortSideFit,

            ///< -BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
            RectBestLongSideFit,

            ///< -BLSF: Positions the rectangle against the long side of a free rectangle into which it fits the best.
            RectBestAreaFit,

            ///< -BAF: Positions the rectangle into the smallest free rect into which it fits.
            RectBottomLeftRule,

            ///< -BL: Does the Tetris placement.
            RectContactPointRule ///< -CP: Choosest the placement where the rectangle touches other rects as much as possible.
        }

        private readonly List<Rect> _freeRectangles = new();

        private readonly List<Rect> _usedRectangles = new();
        private bool _binAllowFlip;
        private int _binHeight;

        private int _binWidth;

        public MaxRectsBinPack(int width, int height, bool allowFlip)
        {
            Init(width, height, allowFlip);
        }

        public void Init(int width, int height, bool allowFlip)
        {
            _binAllowFlip = allowFlip;
            _binWidth = width;
            _binHeight = height;

            var n = new Rect {X = 0, Y = 0, Width = width, Height = height};

            _usedRectangles.Clear();

            _freeRectangles.Clear();
            _freeRectangles.Add(n);
        }

        public Rect Insert(int width, int height, FreeRectChoiceHeuristic method)
        {
            var newNode = new Rect();
            // Unused in this function. We don't need to know the score after finding the position.
            var score1 = int.MaxValue;
            var score2 = int.MaxValue;
            switch (method)
            {
                case FreeRectChoiceHeuristic.RectBestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectBottomLeftRule:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestLongSideFit:
                    newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                    break;
            }

            if (newNode.Height == 0)
                return newNode;

            var numRectanglesToProcess = _freeRectangles.Count;
            for (var i = 0; i < numRectanglesToProcess; ++i)
                if (SplitFreeNode(_freeRectangles[i], ref newNode))
                {
                    _freeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }

            PruneFreeList();

            _usedRectangles.Add(newNode);
            return newNode;
        }

        public void Insert(List<RectSize> rects, List<Rect> dst, FreeRectChoiceHeuristic method)
        {
            dst.Clear();

            while (rects.Count > 0)
            {
                var bestScore1 = int.MaxValue;
                var bestScore2 = int.MaxValue;
                var bestRectIndex = -1;
                var bestNode = new Rect();

                for (var i = 0; i < rects.Count; ++i)
                {
                    int score1;
                    int score2;
                    var newNode = ScoreRect(rects[i].width, rects[i].height, method, out score1, out score2);

                    if (score1 < bestScore1 || score1 == bestScore1 && score2 < bestScore2)
                    {
                        bestScore1 = score1;
                        bestScore2 = score2;
                        bestNode = newNode;
                        bestRectIndex = i;
                    }
                }

                if (bestRectIndex == -1)
                    return;

                PlaceRect(ref bestNode);
                dst.Add(bestNode);
                rects.RemoveAt(bestRectIndex);
            }
        }

        private void PlaceRect(ref Rect node)
        {
            var numRectanglesToProcess = _freeRectangles.Count;
            for (var i = 0; i < numRectanglesToProcess; ++i)
                if (SplitFreeNode(_freeRectangles[i], ref node))
                {
                    _freeRectangles.RemoveAt(i);
                    --i;
                    --numRectanglesToProcess;
                }

            PruneFreeList();

            _usedRectangles.Add(node);
        }

        private Rect ScoreRect(int width, int height, FreeRectChoiceHeuristic method, out int score1, out int score2)
        {
            var newNode = new Rect();
            score1 = int.MaxValue;
            score2 = int.MaxValue;
            switch (method)
            {
                case FreeRectChoiceHeuristic.RectBestShortSideFit:
                    newNode = FindPositionForNewNodeBestShortSideFit(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectBottomLeftRule:
                    newNode = FindPositionForNewNodeBottomLeft(width, height, ref score1, ref score2);
                    break;
                case FreeRectChoiceHeuristic.RectContactPointRule:
                    newNode = FindPositionForNewNodeContactPoint(width, height, ref score1);
                    score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
                    break;
                case FreeRectChoiceHeuristic.RectBestLongSideFit:
                    newNode = FindPositionForNewNodeBestLongSideFit(width, height, ref score2, ref score1);
                    break;
                case FreeRectChoiceHeuristic.RectBestAreaFit:
                    newNode = FindPositionForNewNodeBestAreaFit(width, height, ref score1, ref score2);
                    break;
            }

            // Cannot fit the current rectangle.
            if (newNode.Height == 0)
            {
                score1 = int.MaxValue;
                score2 = int.MaxValue;
            }

            return newNode;
        }

        /// Computes the ratio of used surface area.
        public float Occupancy()
        {
            ulong usedSurfaceArea = 0;
            for (var i = 0; i < _usedRectangles.Count; ++i)
                usedSurfaceArea += (ulong) (_usedRectangles[i].Width * _usedRectangles[i].Height);

            return (float) usedSurfaceArea / (_binWidth * _binHeight);
        }

        private Rect FindPositionForNewNodeBottomLeft(int width, int height, ref int bestY, ref int bestX)
        {
            var bestNode = new Rect();

            bestY = int.MaxValue;
            bestX = int.MaxValue;

            for (var i = 0; i < _freeRectangles.Count; ++i)
            {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (_freeRectangles[i].Width >= width && _freeRectangles[i].Height >= height)
                {
                    var topSideY = _freeRectangles[i].Y + height;
                    if (topSideY < bestY || topSideY == bestY && _freeRectangles[i].X < bestX)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestY = topSideY;
                        bestX = _freeRectangles[i].X;
                    }
                }

                if (_binAllowFlip && _freeRectangles[i].Width >= height && _freeRectangles[i].Height >= width)
                {
                    var topSideY = _freeRectangles[i].Y + width;
                    if (topSideY < bestY || topSideY == bestY && _freeRectangles[i].X < bestX)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestY = topSideY;
                        bestX = _freeRectangles[i].X;
                    }
                }
            }

            return bestNode;
        }

        private Rect FindPositionForNewNodeBestShortSideFit(int width, int height, ref int bestShortSideFit,
            ref int bestLongSideFit)
        {
            var bestNode = new Rect();

            bestShortSideFit = int.MaxValue;
            bestLongSideFit = int.MaxValue;

            for (var i = 0; i < _freeRectangles.Count; ++i)
            {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (_freeRectangles[i].Width >= width && _freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(_freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(_freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (shortSideFit < bestShortSideFit ||
                        shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (_binAllowFlip && _freeRectangles[i].Width >= height && _freeRectangles[i].Height >= width)
                {
                    var flippedLeftoverHoriz = Math.Abs(_freeRectangles[i].Width - height);
                    var flippedLeftoverVert = Math.Abs(_freeRectangles[i].Height - width);
                    var flippedShortSideFit = Math.Min(flippedLeftoverHoriz, flippedLeftoverVert);
                    var flippedLongSideFit = Math.Max(flippedLeftoverHoriz, flippedLeftoverVert);

                    if (flippedShortSideFit < bestShortSideFit || flippedShortSideFit == bestShortSideFit &&
                        flippedLongSideFit < bestLongSideFit)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = flippedShortSideFit;
                        bestLongSideFit = flippedLongSideFit;
                    }
                }
            }

            return bestNode;
        }

        private Rect FindPositionForNewNodeBestLongSideFit(int width, int height, ref int bestShortSideFit,
            ref int bestLongSideFit)
        {
            var bestNode = new Rect();

            bestShortSideFit = int.MaxValue;
            bestLongSideFit = int.MaxValue;

            for (var i = 0; i < _freeRectangles.Count; ++i)
            {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (_freeRectangles[i].Width >= width && _freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(_freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(_freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit ||
                        longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }

                if (_binAllowFlip && _freeRectangles[i].Width >= height && _freeRectangles[i].Height >= width)
                {
                    var leftoverHoriz = Math.Abs(_freeRectangles[i].Width - height);
                    var leftoverVert = Math.Abs(_freeRectangles[i].Height - width);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
                    var longSideFit = Math.Max(leftoverHoriz, leftoverVert);

                    if (longSideFit < bestLongSideFit ||
                        longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = shortSideFit;
                        bestLongSideFit = longSideFit;
                    }
                }
            }

            return bestNode;
        }

        private Rect FindPositionForNewNodeBestAreaFit(int width, int height, ref int bestAreaFit,
            ref int bestShortSideFit)
        {
            var bestNode = new Rect();

            bestAreaFit = int.MaxValue;
            bestShortSideFit = int.MaxValue;

            for (var i = 0; i < _freeRectangles.Count; ++i)
            {
                var areaFit = _freeRectangles[i].Width * _freeRectangles[i].Height - width * height;

                // Try to place the rectangle in upright (non-flipped) orientation.
                if (_freeRectangles[i].Width >= width && _freeRectangles[i].Height >= height)
                {
                    var leftoverHoriz = Math.Abs(_freeRectangles[i].Width - width);
                    var leftoverVert = Math.Abs(_freeRectangles[i].Height - height);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || areaFit == bestAreaFit && shortSideFit < bestShortSideFit)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }

                if (_binAllowFlip && _freeRectangles[i].Width >= height && _freeRectangles[i].Height >= width)
                {
                    var leftoverHoriz = Math.Abs(_freeRectangles[i].Width - height);
                    var leftoverVert = Math.Abs(_freeRectangles[i].Height - width);
                    var shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

                    if (areaFit < bestAreaFit || areaFit == bestAreaFit && shortSideFit < bestShortSideFit)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestShortSideFit = shortSideFit;
                        bestAreaFit = areaFit;
                    }
                }
            }

            return bestNode;
        }

        /// Returns 0 if the two intervals i1 and i2 are disjoint, or the length of their overlap otherwise.
        private int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end)
        {
            if (i1end < i2start || i2end < i1start)
                return 0;
            return Math.Min(i1end, i2end) - Math.Max(i1start, i2start);
        }

        private int ContactPointScoreNode(int x, int y, int width, int height)
        {
            var score = 0;

            if (x == 0 || x + width == _binWidth)
                score += height;
            if (y == 0 || y + height == _binHeight)
                score += width;

            for (var i = 0; i < _usedRectangles.Count; ++i)
            {
                if (_usedRectangles[i].X == x + width || _usedRectangles[i].X + _usedRectangles[i].Width == x)
                    score += CommonIntervalLength(_usedRectangles[i].Y,
                        _usedRectangles[i].Y + _usedRectangles[i].Height,
                        y, y + height);
                if (_usedRectangles[i].Y == y + height || _usedRectangles[i].Y + _usedRectangles[i].Height == y)
                    score += CommonIntervalLength(_usedRectangles[i].X, _usedRectangles[i].X + _usedRectangles[i].Width,
                        x,
                        x + width);
            }

            return score;
        }

        private Rect FindPositionForNewNodeContactPoint(int width, int height, ref int bestContactScore)
        {
            var bestNode = new Rect();

            bestContactScore = -1;

            for (var i = 0; i < _freeRectangles.Count; ++i)
            {
                // Try to place the rectangle in upright (non-flipped) orientation.
                if (_freeRectangles[i].Width >= width && _freeRectangles[i].Height >= height)
                {
                    var score = ContactPointScoreNode(_freeRectangles[i].X, _freeRectangles[i].Y, width, height);
                    if (score > bestContactScore)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestContactScore = score;
                    }
                }

                if (_binAllowFlip && _freeRectangles[i].Width >= height && _freeRectangles[i].Height >= width)
                {
                    var score = ContactPointScoreNode(_freeRectangles[i].X, _freeRectangles[i].Y, height, width);
                    if (score > bestContactScore)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestContactScore = score;
                    }
                }
            }

            return bestNode;
        }

        private bool SplitFreeNode(Rect freeNode, ref Rect usedNode)
        {
            // Test with SAT if the rectangles even intersect.
            if (usedNode.X >= freeNode.X + freeNode.Width || usedNode.X + usedNode.Width <= freeNode.X ||
                usedNode.Y >= freeNode.Y + freeNode.Height || usedNode.Y + usedNode.Height <= freeNode.Y)
                return false;

            if (usedNode.X < freeNode.X + freeNode.Width && usedNode.X + usedNode.Width > freeNode.X)
            {
                // New node at the top side of the used node.
                if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
                {
                    var newNode = freeNode;
                    newNode.Height = usedNode.Y - newNode.Y;
                    _freeRectangles.Add(newNode);
                }

                // New node at the bottom side of the used node.
                if (usedNode.Y + usedNode.Height < freeNode.Y + freeNode.Height)
                {
                    var newNode = freeNode;
                    newNode.Y = usedNode.Y + usedNode.Height;
                    newNode.Height = freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height);
                    _freeRectangles.Add(newNode);
                }
            }

            if (usedNode.Y < freeNode.Y + freeNode.Height && usedNode.Y + usedNode.Height > freeNode.Y)
            {
                // New node at the left side of the used node.
                if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
                {
                    var newNode = freeNode;
                    newNode.Width = usedNode.X - newNode.X;
                    _freeRectangles.Add(newNode);
                }

                // New node at the right side of the used node.
                if (usedNode.X + usedNode.Width < freeNode.X + freeNode.Width)
                {
                    var newNode = freeNode;
                    newNode.X = usedNode.X + usedNode.Width;
                    newNode.Width = freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width);
                    _freeRectangles.Add(newNode);
                }
            }

            return true;
        }

        private void PruneFreeList()
        {
            /* 
            ///  Would be nice to do something like this, to avoid a Theta(n^2) loop through each pair.
            ///  But unfortunately it doesn't quite cut it, since we also want to detect containment. 
            ///  Perhaps there's another way to do this faster than Theta(n^2).

            if (freeRectangles.size() > 0)
                clb::sort::QuickSort(&freeRectangles[0], freeRectangles.size(), NodeSortCmp);

            for(size_t i = 0; i < freeRectangles.size()-1; ++i)
                if (freeRectangles[i].x == freeRectangles[i+1].x &&
                    freeRectangles[i].y == freeRectangles[i+1].y &&
                    freeRectangles[i].width == freeRectangles[i+1].width &&
                    freeRectangles[i].height == freeRectangles[i+1].height)
                {
                    freeRectangles.erase(freeRectangles.begin() + i);
                    --i;
                }
            */

            // Go through each pair and remove any rectangle that is redundant.
            for (var i = 0; i < _freeRectangles.Count; ++i)
            for (var j = i + 1; j < _freeRectangles.Count; ++j)
            {
                if (_freeRectangles[i].IsContainedIn(_freeRectangles[j]))
                {
                    _freeRectangles.RemoveAt(i);
                    --i;
                    break;
                }

                if (_freeRectangles[j].IsContainedIn(_freeRectangles[i]))
                {
                    _freeRectangles.RemoveAt(j);
                    --j;
                }
            }
        }
    }
}