using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RectangleBinPack
{
    internal struct SkylineNode
    {
        /// The starting x-coordinate (leftmost).
        public int X;

        /// The y-coordinate of the skyline level line.
        public int Y;

        /// The line width. The ending coordinate (inclusive) will be x+width-1.
        public int Width;
    }

    public class SkylineBinPack
    {
        private int _binHeight;

        private int _binWidth;

#if DEBUG
        /// Used to track that the packer produces proper packings.
        private readonly DisjointRectCollection _disjointRects = new();
#endif

        private readonly List<SkylineNode> _skyLine = new();

        private ulong _usedSurfaceArea;

        /// If true, we use the GuillotineBinPack structure to recover wasted areas into a waste map.
        private bool _useWasteMap;

        private GuillotineBinPack _wasteMap;

        public SkylineBinPack(int width, int height, bool useWasteMap)
        {
            Init(width, height, useWasteMap);
        }

        public void Init(int width, int height, bool useWasteMap)
        {
            _binWidth = width;
            _binHeight = height;

            _useWasteMap = useWasteMap;

#if DEBUG
            _disjointRects.Clear();
#endif

            _usedSurfaceArea = 0;
            _skyLine.Clear();
            var node = new SkylineNode { X = 0, Y = 0, Width = _binWidth };
            _skyLine.Add(node);

            if (_useWasteMap)
            {
                _wasteMap = new GuillotineBinPack(_binWidth, _binHeight);
                _wasteMap.Init(width, height);
                _wasteMap.GetFreeRectangles().Clear();
            }
        }

        private void Insert(List<RectSize> rects, List<Rect> dst, LevelChoiceHeuristic method)
        {
            dst.Clear();

            while (rects.Count > 0)
            {
                var bestNode = new Rect();
                var bestScore1 = int.MaxValue;
                var bestScore2 = int.MaxValue;
                var bestSkylineIndex = -1;
                var bestRectIndex = -1;
                for (var i = 0; i < rects.Count; ++i)
                {
                    var newNode = new Rect();
                    var score1 = 0;
                    var score2 = 0;
                    var index = 0;
                    switch (method)
                    {
                        case LevelChoiceHeuristic.LevelBottomLeft:
                            newNode = FindPositionForNewNodeBottomLeft(rects[i].width, rects[i].height, ref score1,
                                ref score2, ref index);
                            Debug.Assert(_disjointRects.Disjoint(newNode));
                            break;
                        case LevelChoiceHeuristic.LevelMinWasteFit:
                            newNode = FindPositionForNewNodeMinWaste(rects[i].width, rects[i].height, ref score2,
                                ref score1, ref index);
                            Debug.Assert(_disjointRects.Disjoint(newNode));
                            break;
                    }

                    if (newNode.Height != 0)
                        if (score1 < bestScore1 || score1 == bestScore1 && score2 < bestScore2)
                        {
                            bestNode = newNode;
                            bestScore1 = score1;
                            bestScore2 = score2;
                            bestSkylineIndex = index;
                            bestRectIndex = i;
                        }
                }

                if (bestRectIndex == -1)
                    return;

                // Perform the actual packing.
#if DEBUG
                Debug.Assert(_disjointRects.Disjoint(bestNode));
                _disjointRects.Add(bestNode);
#endif
                AddSkylineLevel(bestSkylineIndex, ref bestNode);
                _usedSurfaceArea += (ulong)(rects[bestRectIndex].width * rects[bestRectIndex].height);
                rects.RemoveAt(bestRectIndex);
                dst.Add(bestNode);
            }
        }

        private Rect Insert(int width, int height, LevelChoiceHeuristic method)
        {
            // First try to pack this rectangle into the waste map, if it fits.
            var node = _wasteMap.Insert(width, height, true,
                GuillotineBinPack.FreeRectChoiceHeuristic.RectBestShortSideFit,
                GuillotineBinPack.GuillotineSplitHeuristic.SplitMaximizeArea);
            Debug.Assert(_disjointRects.Disjoint(node));

            if (node.Height != 0)
            {
                var newNode = new Rect();
                newNode.X = node.X;
                newNode.Y = node.Y;
                newNode.Width = node.Width;
                newNode.Height = node.Height;
                _usedSurfaceArea += (ulong)(width * height);
#if DEBUG
                Debug.Assert(_disjointRects.Disjoint(newNode));
                _disjointRects.Add(newNode);
#endif
                return newNode;
            }

            switch (method)
            {
                case LevelChoiceHeuristic.LevelBottomLeft: return InsertBottomLeft(width, height);
                case LevelChoiceHeuristic.LevelMinWasteFit: return InsertMinWaste(width, height);
                default:
                    return node;
            }
        }

        private bool RectangleFits(int skylineNodeIndex, int width, int height, ref int y)
        {
            var x = _skyLine[skylineNodeIndex].X;
            if (x + width > _binWidth)
                return false;
            var widthLeft = width;
            var i = skylineNodeIndex;
            y = _skyLine[skylineNodeIndex].Y;
            while (widthLeft > 0)
            {
                y = Math.Max(y, _skyLine[i].Y);
                if (y + height > _binHeight)
                    return false;
                widthLeft -= _skyLine[i].Width;
                ++i;
                Debug.Assert(i < _skyLine.Count || widthLeft <= 0);
            }

            return true;
        }

        private int ComputeWastedArea(int skylineNodeIndex, int width, int height, int y)
        {
            var wastedArea = 0;
            var rectLeft = _skyLine[skylineNodeIndex].X;
            var rectRight = rectLeft + width;
            for (; skylineNodeIndex < _skyLine.Count && _skyLine[skylineNodeIndex].X < rectRight; ++skylineNodeIndex)
            {
                if (_skyLine[skylineNodeIndex].X >= rectRight ||
                    _skyLine[skylineNodeIndex].X + _skyLine[skylineNodeIndex].Width <= rectLeft)
                    break;

                var leftSide = _skyLine[skylineNodeIndex].X;
                var rightSide = Math.Max(rectRight, leftSide + _skyLine[skylineNodeIndex].Width);
                Debug.Assert(y >= _skyLine[skylineNodeIndex].Y);
                wastedArea += (rightSide - leftSide) * (y - _skyLine[skylineNodeIndex].Y);
            }

            return wastedArea;
        }

        private bool RectangleFits(int skylineNodeIndex, int width, int height, ref int y, ref int wastedArea)
        {
            var fits = RectangleFits(skylineNodeIndex, width, height, ref y);
            if (fits)
                wastedArea = ComputeWastedArea(skylineNodeIndex, width, height, y);

            return fits;
        }

        private void AddWasteMapArea(int skylineNodeIndex, int width, int height, int y)
        {
            // int wastedArea = 0; // unused
            var rectLeft = _skyLine[skylineNodeIndex].X;
            var rectRight = rectLeft + width;
            for (; skylineNodeIndex < _skyLine.Count && _skyLine[skylineNodeIndex].X < rectRight; ++skylineNodeIndex)
            {
                if (_skyLine[skylineNodeIndex].X >= rectRight ||
                    _skyLine[skylineNodeIndex].X + _skyLine[skylineNodeIndex].Width <= rectLeft)
                    break;

                var leftSide = _skyLine[skylineNodeIndex].X;
                var rightSide = Math.Max(rectRight, leftSide + _skyLine[skylineNodeIndex].Width);
                Debug.Assert(y >= _skyLine[skylineNodeIndex].Y);

                var waste = new Rect();
                waste.X = leftSide;
                waste.Y = _skyLine[skylineNodeIndex].Y;
                waste.Width = rightSide - leftSide;
                waste.Height = y - _skyLine[skylineNodeIndex].Y;

                Debug.Assert(_disjointRects.Disjoint(waste));
                _wasteMap.GetFreeRectangles().Add(waste);
            }
        }

        private void AddSkylineLevel(int skylineNodeIndex, ref Rect rect)
        {
            // First track all wasted areas and mark them into the waste map if we're using one.
            if (_useWasteMap)
                AddWasteMapArea(skylineNodeIndex, rect.Width, rect.Height, rect.Y);

            var newNode = new SkylineNode();
            newNode.X = rect.X;
            newNode.Y = rect.Y + rect.Height;
            newNode.Width = rect.Width;
            _skyLine.Insert(skylineNodeIndex, newNode);

            Debug.Assert(newNode.X + newNode.Width <= _binWidth);
            Debug.Assert(newNode.Y <= _binHeight);

            for (var i = skylineNodeIndex + 1; i < _skyLine.Count; ++i)
            {
                Debug.Assert(_skyLine[i - 1].X <= _skyLine[i].X);

                if (_skyLine[i].X < _skyLine[i - 1].X + _skyLine[i - 1].Width)
                {
                    var shrink = _skyLine[i - 1].X + _skyLine[i - 1].Width - _skyLine[i].X;

                    var tempNode = _skyLine[i];
                    tempNode.X += shrink;
                    tempNode.Width -= shrink;
                    _skyLine[i] = tempNode;

                    if (_skyLine[i].Width <= 0)
                    {
                        _skyLine.RemoveAt(i);
                        --i;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            MergeSkylines();
        }

        private void MergeSkylines()
        {
            for (var i = 0; i < _skyLine.Count - 1; ++i)
                if (_skyLine[i].Y == _skyLine[i + 1].Y)
                {
                    var node = _skyLine[i];
                    node.Width += _skyLine[i + 1].Width;
                    _skyLine[i] = node;
                    _skyLine.RemoveAt(i + 1);
                    --i;
                }
        }

        private Rect InsertBottomLeft(int width, int height)
        {
            var bestHeight = 0;
            var bestWidth = 0;
            var bestIndex = 0;
            var newNode = FindPositionForNewNodeBottomLeft(width, height, ref bestHeight, ref bestWidth, ref bestIndex);

            if (bestIndex != -1)
            {
                Debug.Assert(_disjointRects.Disjoint(newNode));

                // Perform the actual packing.
                AddSkylineLevel(bestIndex, ref newNode);

                _usedSurfaceArea += (ulong)(width * height);
# if DEBUG
                _disjointRects.Add(newNode);
#endif
            }
            else
            {
                newNode = new Rect();
            }

            return newNode;
        }

        private Rect FindPositionForNewNodeBottomLeft(int width, int height, ref int bestHeight, ref int bestWidth,
            ref int bestIndex)
        {
            bestHeight = int.MaxValue;
            bestIndex = -1;
            // Used to break ties if there are nodes at the same level. Then pick the narrowest one.
            bestWidth = int.MaxValue;
            var newNode = new Rect();
            for (var i = 0; i < _skyLine.Count; ++i)
            {
                var y = 0;
                if (RectangleFits(i, width, height, ref y))
                    if (y + height < bestHeight || y + height == bestHeight && _skyLine[i].Width < bestWidth)
                    {
                        bestHeight = y + height;
                        bestIndex = i;
                        bestWidth = _skyLine[i].Width;
                        newNode.X = _skyLine[i].X;
                        newNode.Y = y;
                        newNode.Width = width;
                        newNode.Height = height;
                        Debug.Assert(_disjointRects.Disjoint(newNode));
                    }

                if (RectangleFits(i, height, width, ref y))
                    if (y + width < bestHeight || y + width == bestHeight && _skyLine[i].Width < bestWidth)
                    {
                        bestHeight = y + width;
                        bestIndex = i;
                        bestWidth = _skyLine[i].Width;
                        newNode.X = _skyLine[i].X;
                        newNode.Y = y;
                        newNode.Width = height;
                        newNode.Height = width;
                        Debug.Assert(_disjointRects.Disjoint(newNode));
                    }
            }

            return newNode;
        }

        private Rect InsertMinWaste(int width, int height)
        {
            var bestHeight = 0;
            var bestWastedArea = 0;
            var bestIndex = -1;
            var newNode =
                FindPositionForNewNodeMinWaste(width, height, ref bestHeight, ref bestWastedArea, ref bestIndex);

            if (bestIndex != -1)
            {
                Debug.Assert(_disjointRects.Disjoint(newNode));

                // Perform the actual packing.
                AddSkylineLevel(bestIndex, ref newNode);

                _usedSurfaceArea += (ulong)(width * height);
#if DEBUG
                _disjointRects.Add(newNode);
#endif
            }
            else
            {
                newNode = new Rect();
            }

            return newNode;
        }

        private Rect FindPositionForNewNodeMinWaste(int width, int height, ref int bestHeight, ref int bestWastedArea,
            ref int bestIndex)
        {
            bestHeight = int.MaxValue;
            bestWastedArea = int.MaxValue;
            bestIndex = -1;
            var newNode = new Rect();
            for (var i = 0; i < _skyLine.Count; ++i)
            {
                var y = 0;
                var wastedArea = 0;

                if (RectangleFits(i, width, height, ref y, ref wastedArea))
                    if (wastedArea < bestWastedArea || wastedArea == bestWastedArea && y + height < bestHeight)
                    {
                        bestHeight = y + height;
                        bestWastedArea = wastedArea;
                        bestIndex = i;
                        newNode.X = _skyLine[i].X;
                        newNode.Y = y;
                        newNode.Width = width;
                        newNode.Height = height;
                        Debug.Assert(_disjointRects.Disjoint(newNode));
                    }

                if (RectangleFits(i, height, width, ref y, ref wastedArea))
                    if (wastedArea < bestWastedArea || wastedArea == bestWastedArea && y + width < bestHeight)
                    {
                        bestHeight = y + width;
                        bestWastedArea = wastedArea;
                        bestIndex = i;
                        newNode.X = _skyLine[i].X;
                        newNode.Y = y;
                        newNode.Width = height;
                        newNode.Height = width;
                        Debug.Assert(_disjointRects.Disjoint(newNode));
                    }
            }

            return newNode;
        }

        /// Computes the ratio of used surface area.
        private float Occupancy()
        {
            return (float)_usedSurfaceArea / (_binWidth * _binHeight);
        }

        private enum LevelChoiceHeuristic
        {
            LevelBottomLeft,
            LevelMinWasteFit
        }
    }
}