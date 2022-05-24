using System;
using System.Collections.Generic;

namespace RectangleBinPack
{
    public class GuillotineBinPack
    {
        /// Specifies the different choice heuristics that can be used when deciding which of the free subrectangles
        /// to place the to-be-packed rectangle into.
        public enum FreeRectChoiceHeuristic
        {
            RectBestAreaFit, //< -BAF
            RectBestShortSideFit, //< -BSSF
            RectBestLongSideFit, //< -BLSF
            RectWorstAreaFit, //< -WAF
            RectWorstShortSideFit, //< -WSSF
            RectWorstLongSideFit //< -WLSF
        }

        /// Specifies the different choice heuristics that can be used when the packer needs to decide whether to
        /// subdivide the remaining free space in horizontal or vertical direction.
        public enum GuillotineSplitHeuristic
        {
            SplitShorterLeftoverAxis, //< -SLAS
            SplitLongerLeftoverAxis, //< -LLAS
            SplitMinimizeArea, //< -MINAS, Try to make a single big rectangle at the expense of making the other small.
            SplitMaximizeArea, //< -MAXAS, Try to make both remaining rectangles as even-sized as possible.
            SplitShorterAxis, //< -SAS
            SplitLongerAxis //< -LAS
        }

        private int _binHeight;

        private int _binWidth;
        private readonly List<Rect> _freeRectangles;

        private readonly List<Rect> _usedRectangles;

#if DEBUG
        /// Used to track that the packer produces proper packings.
        DisjointRectCollection disjointRects;
#endif

        public GuillotineBinPack(int width, int height)
        {
            _binWidth = width;
            _binHeight = height;

            _usedRectangles = new List<Rect>();
            _freeRectangles = new List<Rect>();

            // We start with a single big free rectangle that spans the whole bin.
            Rect n = default;
            n.X = 0;
            n.Y = 0;
            n.Width = width;
            n.Height = height;

            _freeRectangles.Add(n);
        }

        public void Init(int width, int height)
        {
            _binWidth = width;
            _binHeight = height;

#if DEBUG
            disjointRects.Clear();
#endif

            // Clear any memory of previously packed rectangles.
            _usedRectangles.Clear();

            // We start with a single big free rectangle that spans the whole bin.
            Rect n = new Rect();
            n.X = 0;
            n.Y = 0;
            n.Width = width;
            n.Height = height;

            _freeRectangles.Clear();
            _freeRectangles.Add(n);
        }

        /// Returns the internal list of disjoint rectangles that track the free area of the bin. You may alter this vector
        /// any way desired, as long as the end result still is a list of disjoint rectangles.
        public List<Rect> GetFreeRectangles() { return _freeRectangles; }

        /// Returns the list of packed rectangles. You may alter this vector at will, for example, you can move a Rect from
        /// this list to the Free Rectangles list to free up space on-the-fly, but notice that this causes fragmentation.
        public List<Rect> GetUsedRectangles() { return _usedRectangles; }

        public void Insert(List<Rect> rects, bool merge, FreeRectChoiceHeuristic rectChoice,
            GuillotineSplitHeuristic splitMethod)
        {
            // Remember variables about the best packing choice we have made so far during the iteration process.
            var bestFreeRect = 0;
            var bestRect = 0;
            var bestFlipped = false;

            // Pack rectangles one at a time until we have cleared the rects array of all rectangles.
            // rects will get destroyed in the process.
            while (rects.Count > 0)
            {
                // Stores the penalty score of the best rectangle placement - bigger=worse, smaller=better.
                var bestScore = int.MaxValue;

                for (var i = 0; i < _freeRectangles.Count; ++i)
                    for (var j = 0; j < rects.Count; ++j)
                        // If this rectangle is a perfect match, we pick it instantly.
                        if (rects[j].Width == _freeRectangles[i].Width && rects[j].Height == _freeRectangles[i].Height)
                        {
                            bestFreeRect = i;
                            bestRect = j;
                            bestFlipped = false;
                            bestScore = int.MinValue;
                            i = _freeRectangles
                                .Count; // Force a jump out of the outer loop as well - we got an instant fit.
                            break;
                        }
                        // If flipping this rectangle is a perfect match, pick that then.
                        else if (rects[j].Height == _freeRectangles[i].Width && rects[j].Width == _freeRectangles[i].Height)
                        {
                            bestFreeRect = i;
                            bestRect = j;
                            bestFlipped = true;
                            bestScore = int.MinValue;
                            i = _freeRectangles
                                .Count; // Force a jump out of the outer loop as well - we got an instant fit.
                            break;
                        }
                        // Try if we can fit the rectangle upright.
                        else if (rects[j].Width <= _freeRectangles[i].Width && rects[j].Height <= _freeRectangles[i].Height)
                        {
                            var score = ScoreByHeuristic(rects[j].Width, rects[j].Height, _freeRectangles[i], rectChoice);
                            if (score < bestScore)
                            {
                                bestFreeRect = i;
                                bestRect = j;
                                bestFlipped = false;
                                bestScore = score;
                            }
                        }
                        // If not, then perhaps flipping sideways will make it fit?
                        else if (rects[j].Height <= _freeRectangles[i].Width && rects[j].Width <= _freeRectangles[i].Height)
                        {
                            var score = ScoreByHeuristic(rects[j].Height, rects[j].Width, _freeRectangles[i], rectChoice);
                            if (score < bestScore)
                            {
                                bestFreeRect = i;
                                bestRect = j;
                                bestFlipped = true;
                                bestScore = score;
                            }
                        }

                // If we didn't manage to find any rectangle to pack, abort.
                if (bestScore == int.MaxValue)
                    return;

                // Otherwise, we're good to go and do the actual packing.
                Rect newNode = default;
                newNode.X = _freeRectangles[bestFreeRect].X;
                newNode.Y = _freeRectangles[bestFreeRect].Y;
                newNode.Width = rects[bestRect].Width;
                newNode.Height = rects[bestRect].Height;

                if (bestFlipped)
                    newNode.Swap();

                // Remove the free space we lost in the bin.
                SplitFreeRectByHeuristic(_freeRectangles[bestFreeRect], newNode, splitMethod);
                _freeRectangles.RemoveAt(bestFreeRect);

                // Remove the rectangle we just packed from the input list.
                rects.RemoveAt(bestRect);

                // Perform a Rectangle Merge step if desired.
                if (merge)
                    MergeFreeList();

                // Remember the new used rectangle.
                _usedRectangles.Add(newNode);

                // Check that we're really producing correct packings here.
                //debug_assert(disjointRects.Add(newNode) == true);
            }
        }

        public Rect Insert(int width, int height, bool merge, FreeRectChoiceHeuristic rectChoice,
            GuillotineSplitHeuristic splitMethod)
        {
            // Find where to put the new rectangle.
            var freeNodeIndex = 0;
            var newRect = FindPositionForNewNode(width, height, rectChoice, ref freeNodeIndex);

            // Abort if we didn't have enough space in the bin.
            if (newRect.Height == 0)
                return newRect;

            // Remove the space that was just consumed by the new rectangle.
            SplitFreeRectByHeuristic(_freeRectangles[freeNodeIndex], newRect, splitMethod);
            _freeRectangles.RemoveAt(freeNodeIndex);

            // Perform a Rectangle Merge step if desired.
            if (merge)
                MergeFreeList();

            // Remember the new used rectangle.
            _usedRectangles.Add(newRect);

            // Check that we're really producing correct packings here.
            //debug_assert(disjointRects.Add(newRect) == true);

            return newRect;
        }

        /// Computes the ratio of used surface area to the total bin area.
        public float Occupancy()
        {
            //\todo The occupancy rate could be cached/tracked incrementally instead
            //      of looping through the list of packed rectangles here.
            ulong usedSurfaceArea = 0;
            for (var i = 0; i < _usedRectangles.Count; ++i)
                usedSurfaceArea += (ulong)(_usedRectangles[i].Width * _usedRectangles[i].Height);

            return (float)usedSurfaceArea / (_binWidth * _binHeight);
        }


        /// Returns the heuristic score value for placing a rectangle of size width*height into freeRect. Does not try to rotate.
        private int ScoreByHeuristic(int width, int height, Rect freeRect, FreeRectChoiceHeuristic rectChoice)
        {
            switch (rectChoice)
            {
                case FreeRectChoiceHeuristic.RectBestAreaFit: return ScoreBestAreaFit(width, height, freeRect);
                case FreeRectChoiceHeuristic.RectBestShortSideFit:
                    return ScoreBestShortSideFit(width, height, freeRect);
                case FreeRectChoiceHeuristic.RectBestLongSideFit: return ScoreBestLongSideFit(width, height, freeRect);
                case FreeRectChoiceHeuristic.RectWorstAreaFit: return ScoreWorstAreaFit(width, height, freeRect);
                case FreeRectChoiceHeuristic.RectWorstShortSideFit:
                    return ScoreWorstShortSideFit(width, height, freeRect);
                case FreeRectChoiceHeuristic.RectWorstLongSideFit:
                    return ScoreWorstLongSideFit(width, height, freeRect);
                default:
                    return int.MaxValue;
            }
        }

        private int ScoreBestAreaFit(int width, int height, Rect freeRect)
        {
            return freeRect.Width * freeRect.Height - width * height;
        }

        private int ScoreBestShortSideFit(int width, int height, Rect freeRect)
        {
            var leftoverHoriz = Math.Abs(freeRect.Width - width);
            var leftoverVert = Math.Abs(freeRect.Height - height);
            var leftover = Math.Min(leftoverHoriz, leftoverVert);
            return leftover;
        }

        private int ScoreBestLongSideFit(int width, int height, Rect freeRect)
        {
            var leftoverHoriz = Math.Abs(freeRect.Width - width);
            var leftoverVert = Math.Abs(freeRect.Height - height);
            var leftover = Math.Max(leftoverHoriz, leftoverVert);
            return leftover;
        }

        private int ScoreWorstAreaFit(int width, int height, Rect freeRect)
        {
            return -ScoreBestAreaFit(width, height, freeRect);
        }

        private int ScoreWorstShortSideFit(int width, int height, Rect freeRect)
        {
            return -ScoreBestShortSideFit(width, height, freeRect);
        }

        private int ScoreWorstLongSideFit(int width, int height, Rect freeRect)
        {
            return -ScoreBestLongSideFit(width, height, freeRect);
        }

        private Rect FindPositionForNewNode(int width, int height, FreeRectChoiceHeuristic rectChoice,
            ref int nodeIndex)
        {
            Rect bestNode = default;

            var bestScore = int.MaxValue;

            // Try each free rectangle to find the best one for placement.
            for (var i = 0; i < _freeRectangles.Count; ++i)
                // If this is a perfect fit upright, choose it immediately.
                if (width == _freeRectangles[i].Width && height == _freeRectangles[i].Height)
                {
                    bestNode.X = _freeRectangles[i].X;
                    bestNode.Y = _freeRectangles[i].Y;
                    bestNode.Width = width;
                    bestNode.Height = height;
                    bestScore = int.MinValue;
                    nodeIndex = i;
                    //debug_assert(disjointRects.Disjoint(bestNode));
                    break;
                }
                // If this is a perfect fit sideways, choose it.
                else if (height == _freeRectangles[i].Width && width == _freeRectangles[i].Height)
                {
                    bestNode.X = _freeRectangles[i].X;
                    bestNode.Y = _freeRectangles[i].Y;
                    bestNode.Width = height;
                    bestNode.Height = width;
                    bestScore = int.MaxValue;
                    nodeIndex = i;
                    //debug_assert(disjointRects.Disjoint(bestNode));
                    break;
                }
                // Does the rectangle fit upright?
                else if (width <= _freeRectangles[i].Width && height <= _freeRectangles[i].Height)
                {
                    var score = ScoreByHeuristic(width, height, _freeRectangles[i], rectChoice);

                    if (score < bestScore)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = width;
                        bestNode.Height = height;
                        bestScore = score;
                        nodeIndex = i;
                        //debug_assert(disjointRects.Disjoint(bestNode));
                    }
                }
                // Does the rectangle fit sideways?
                else if (height <= _freeRectangles[i].Width && width <= _freeRectangles[i].Height)
                {
                    var score = ScoreByHeuristic(height, width, _freeRectangles[i], rectChoice);

                    if (score < bestScore)
                    {
                        bestNode.X = _freeRectangles[i].X;
                        bestNode.Y = _freeRectangles[i].Y;
                        bestNode.Width = height;
                        bestNode.Height = width;
                        bestScore = score;
                        nodeIndex = i;
                        //debug_assert(disjointRects.Disjoint(bestNode));
                    }
                }

            return bestNode;
        }

        private void SplitFreeRectByHeuristic(Rect freeRect, Rect placedRect, GuillotineSplitHeuristic method)
        {
            // Compute the lengths of the leftover area.
            var w = freeRect.Width - placedRect.Width;
            var h = freeRect.Height - placedRect.Height;

            // Placing placedRect into freeRect results in an L-shaped free area, which must be split into
            // two disjoint rectangles. This can be achieved with by splitting the L-shape using a single line.
            // We have two choices: horizontal or vertical.	

            // Use the given heuristic to decide which choice to make.

            bool splitHorizontal;
            switch (method)
            {
                case GuillotineSplitHeuristic.SplitShorterLeftoverAxis:
                    // Split along the shorter leftover axis.
                    splitHorizontal = w <= h;
                    break;
                case GuillotineSplitHeuristic.SplitLongerLeftoverAxis:
                    // Split along the longer leftover axis.
                    splitHorizontal = w > h;
                    break;
                case GuillotineSplitHeuristic.SplitMinimizeArea:
                    // Maximize the larger area == minimize the smaller area.
                    // Tries to make the single bigger rectangle.
                    splitHorizontal = placedRect.Width * h > w * placedRect.Height;
                    break;
                case GuillotineSplitHeuristic.SplitMaximizeArea:
                    // Maximize the smaller area == minimize the larger area.
                    // Tries to make the rectangles more even-sized.
                    splitHorizontal = placedRect.Width * h <= w * placedRect.Height;
                    break;
                case GuillotineSplitHeuristic.SplitShorterAxis:
                    // Split along the shorter total axis.
                    splitHorizontal = freeRect.Width <= freeRect.Height;
                    break;
                case GuillotineSplitHeuristic.SplitLongerAxis:
                    // Split along the longer total axis.
                    splitHorizontal = freeRect.Width > freeRect.Height;
                    break;
                default:
                    splitHorizontal = true;
                    //assert(false);
                    break;
            }

            // Perform the actual split.
            SplitFreeRectAlongAxis(freeRect, placedRect, splitHorizontal);
        }

        /// This function will add the two generated rectangles into the freeRectangles array. The caller is expected to
        /// remove the original rectangle from the freeRectangles array after that.
        private void SplitFreeRectAlongAxis(Rect freeRect, Rect placedRect, bool splitHorizontal)
        {
            // Form the two new rectangles.
            Rect bottom = default;
            bottom.X = freeRect.X;
            bottom.Y = freeRect.Y + placedRect.Height;
            bottom.Height = freeRect.Height - placedRect.Height;

            Rect right = default;
            right.X = freeRect.X + placedRect.Width;
            right.Y = freeRect.Y;
            right.Width = freeRect.Width - placedRect.Width;

            if (splitHorizontal)
            {
                bottom.Width = freeRect.Width;
                right.Height = placedRect.Height;
            }
            else // Split vertically
            {
                bottom.Width = placedRect.Width;
                right.Height = freeRect.Height;
            }

            // Add the new rectangles into the free rectangle pool if they weren't degenerate.
            if (bottom.Width > 0 && bottom.Height > 0)
                _freeRectangles.Add(bottom);
            if (right.Width > 0 && right.Height > 0)
                _freeRectangles.Add(right);

            //debug_assert(disjointRects.Disjoint(bottom));
            //debug_assert(disjointRects.Disjoint(right));
        }

        public void MergeFreeList()
        {
            //#ifdef _DEBUG
            //		DisjointRectCollection test;
            //		for (size_t i = 0; i < freeRectangles.size(); ++i)
            //			assert(test.Add(freeRectangles[i]) == true);
            //#endif

            // Do a Theta(n^2) loop to see if any pair of free rectangles could me merged into one.
            // Note that we miss any opportunities to merge three rectangles into one. (should call this function again to detect that)
            for (var i = 0; i < _freeRectangles.Count; ++i)
                for (var j = i + 1; j < _freeRectangles.Count; ++j)
                    if (_freeRectangles[i].Width == _freeRectangles[j].Width &&
                        _freeRectangles[i].X == _freeRectangles[j].X)
                    {
                        if (_freeRectangles[i].Y == _freeRectangles[j].Y + _freeRectangles[j].Height)
                        {
                            _freeRectangles[i] = new Rect(_freeRectangles[i].X, _freeRectangles[j].Height,
                                _freeRectangles[i].Width, _freeRectangles[j].Height);
                            _freeRectangles.RemoveAt(j);
                            --j;
                        }
                        else if (_freeRectangles[i].Y + _freeRectangles[i].Height == _freeRectangles[j].Y)
                        {
                            _freeRectangles[i] = new Rect(_freeRectangles[i].X, _freeRectangles[i].Y,
                                _freeRectangles[i].Width, _freeRectangles[j].Height);
                            _freeRectangles.RemoveAt(j);
                            --j;
                        }
                    }
                    else if (_freeRectangles[i].Height == _freeRectangles[j].Height &&
                             _freeRectangles[i].Y == _freeRectangles[j].Y)
                    {
                        if (_freeRectangles[i].X == _freeRectangles[j].X + _freeRectangles[j].Width)
                        {
                            _freeRectangles[i] = new Rect(_freeRectangles[j].Width, _freeRectangles[i].Y,
                                _freeRectangles[j].Width, _freeRectangles[i].Height);
                            _freeRectangles.RemoveAt(j);
                            --j;
                        }
                        else if (_freeRectangles[i].X + _freeRectangles[i].Width == _freeRectangles[j].X)
                        {
                            _freeRectangles[i] = new Rect(_freeRectangles[i].X, _freeRectangles[i].Y,
                                _freeRectangles[j].Width, _freeRectangles[i].Height);
                            _freeRectangles.RemoveAt(j);
                            --j;
                        }
                    }

            //#ifdef _DEBUG
            //		test.Clear();
            //		for (size_t i = 0; i < freeRectangles.size(); ++i)
            //			assert(test.Add(freeRectangles[i]) == true);
            //#endif
        }
    }
}