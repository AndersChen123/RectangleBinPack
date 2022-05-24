using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RectangleBinPack
{
    public class ShelfBinPack
    {
        public enum ShelfChoiceHeuristic
        {
            ShelfNextFit, //< -NF: We always put the new rectangle to the last open shelf.
            ShelfFirstFit, //< -FF: We test each rectangle against each shelf in turn and pack it to the first where it fits.
            ShelfBestAreaFit, //< -BAF: Choose the shelf with smallest remaining shelf area.
            ShelfWorstAreaFit, //< -WAF: Choose the shelf with the largest remaining shelf area.
            ShelfBestHeightFit, //< -BHF: Choose the smallest shelf (height-wise) where the rectangle fits.
            ShelfBestWidthFit, //< -BWF: Choose the shelf that has the least remaining horizontal shelf space available after packing.
            ShelfWorstWidthFit //< -WWF: Choose the shelf that will have most remainining horizontal shelf space available after packing.
        }

        private readonly int _binHeight;
        private readonly int _binWidth;

        /// Stores the starting y-coordinate of the latest (topmost) shelf.
        private int _currentY;

        private readonly List<Shelf> _shelves;

        /// Tracks the total consumed surface area.
        private ulong _usedSurfaceArea;

        /// If true, the following GuillotineBinPack structure is used to recover the SHELF data structure from losing space.
        private readonly bool _useWasteMap;

        private readonly GuillotineBinPack _wasteMap;

        public ShelfBinPack(int width, int height, bool useWasteMap)
        {
            _useWasteMap = useWasteMap;
            _binWidth = width;
            _binHeight = height;

            _currentY = 0;
            _usedSurfaceArea = 0;

            _shelves = new List<Shelf>();
            StartNewShelf(0);

            if (_useWasteMap)
            {
                _wasteMap = new GuillotineBinPack(width, height);
                _wasteMap.GetFreeRectangles().Clear();
            }
        }

        private bool CanStartNewShelf(int height)
        {
            return _shelves.Last().StartY + _shelves.Last().Height + height <= _binHeight;
        }

        private void StartNewShelf(int startingHeight)
        {
            if (_shelves.Count > 0)
            {
                Debug.Assert(_shelves.Last().Height != 0);
                _currentY += _shelves.Last().Height;

                Debug.Assert(_currentY < _binHeight);
            }

            Shelf shelf = default;
            shelf.CurrentX = 0;
            shelf.Height = startingHeight;
            shelf.StartY = _currentY;

            Debug.Assert(shelf.StartY + shelf.Height <= _binHeight);
            _shelves.Add(shelf);
        }

        private bool FitsOnShelf(Shelf shelf, int width, int height, bool canResize)
        {
            var shelfHeight = canResize ? _binHeight - shelf.StartY : shelf.Height;
            if (shelf.CurrentX + width <= _binWidth && height <= shelfHeight ||
                shelf.CurrentX + height <= _binWidth && width <= shelfHeight)
                return true;
            return false;
        }

        private void RotateToShelf(Shelf shelf, ref int width, ref int height)
        {
            // If the width > height and the long edge of the new rectangle fits vertically onto the current shelf,
            // flip it. If the short edge is larger than the current shelf height, store
            // the short edge vertically.
            if (width > height && width > _binWidth - shelf.CurrentX ||
                width > height && width < shelf.Height ||
                width < height && height > shelf.Height && height <= _binWidth - shelf.CurrentX)
            {
                var temp = width;
                width = height;
                height = temp;
            }
        }

        private void AddToShelf(Shelf shelf, int width, int height, Rect newNode)
        {
            Debug.Assert(FitsOnShelf(shelf, width, height, true));

            // Swap width and height if the rect fits better that way.
            RotateToShelf(shelf, ref width, ref height);

            // Add the rectangle to the shelf.
            newNode.X = shelf.CurrentX;
            newNode.Y = shelf.StartY;
            newNode.Width = width;
            newNode.Height = height;
            shelf.UsedRectangles.Add(newNode);

            // Advance the shelf end position horizontally.
            shelf.CurrentX += width;
            Debug.Assert(shelf.CurrentX <= _binWidth);

            // Grow the shelf height.
            shelf.Height = Math.Max(shelf.Height, height);
            Debug.Assert(shelf.Height <= _binHeight);

            _usedSurfaceArea += (ulong) (width * height);
        }

        private Rect Insert(int width, int height, ShelfChoiceHeuristic method)
        {
            Rect newNode = default;

            // First try to pack this rectangle into the waste map, if it fits.
            if (_useWasteMap)
            {
                newNode = _wasteMap.Insert(width, height, true,
                    GuillotineBinPack.FreeRectChoiceHeuristic.RectBestShortSideFit,
                    GuillotineBinPack.GuillotineSplitHeuristic.SplitMaximizeArea);
                if (newNode.Height != 0)
                {
                    // Track the space we just used.
                    _usedSurfaceArea += (ulong) (width * height);

                    return newNode;
                }
            }

            switch (method)
            {
                case ShelfChoiceHeuristic.ShelfNextFit:
                    if (FitsOnShelf(_shelves.Last(), width, height, true))
                    {
                        AddToShelf(_shelves.Last(), width, height, newNode);
                        return newNode;
                    }

                    break;
                case ShelfChoiceHeuristic.ShelfFirstFit:
                    for (var i = 0; i < _shelves.Count; ++i)
                        if (FitsOnShelf(_shelves[i], width, height, i == _shelves.Count - 1))
                        {
                            AddToShelf(_shelves[i], width, height, newNode);
                            return newNode;
                        }

                    break;

                case ShelfChoiceHeuristic.ShelfBestAreaFit:
                {
                    // Best Area Fit rule: Choose the shelf with smallest remaining shelf area.
                    Shelf bestShelf = default;
                    var bestShelfSurfaceArea = ulong.MinValue;
                    for (var i = 0; i < _shelves.Count; ++i)
                    {
                        // Pre-rotate the rect onto the shelf here already so that the area fit computation
                        // is done correctly.
                        RotateToShelf(_shelves[i], ref width, ref height);
                        if (FitsOnShelf(_shelves[i], width, height, i == _shelves.Count - 1))
                        {
                            var surfaceArea = (ulong) ((_binWidth - _shelves[i].CurrentX) * _shelves[i].Height);
                            if (surfaceArea < bestShelfSurfaceArea)
                            {
                                bestShelf = _shelves[i];
                                bestShelfSurfaceArea = surfaceArea;
                            }
                        }
                    }

                    if (!Equals(bestShelf, default(Shelf)))
                    {
                        AddToShelf(bestShelf, width, height, newNode);
                        return newNode;
                    }
                }
                    break;

                case ShelfChoiceHeuristic.ShelfWorstAreaFit:
                {
                    // Worst Area Fit rule: Choose the shelf with smallest remaining shelf area.
                    Shelf bestShelf = default;
                    var bestShelfSurfaceArea = -1;
                    for (var i = 0; i < _shelves.Count; ++i)
                    {
                        // Pre-rotate the rect onto the shelf here already so that the area fit computation
                        // is done correctly.
                        RotateToShelf(_shelves[i], ref width, ref height);
                        if (FitsOnShelf(_shelves[i], width, height, i == _shelves.Count - 1))
                        {
                            var surfaceArea = (_binWidth - _shelves[i].CurrentX) * _shelves[i].Height;
                            if (surfaceArea > bestShelfSurfaceArea)
                            {
                                bestShelf = _shelves[i];
                                bestShelfSurfaceArea = surfaceArea;
                            }
                        }
                    }

                    if (!Equals(bestShelf, default(Shelf)))
                    {
                        AddToShelf(bestShelf, width, height, newNode);
                        return newNode;
                    }
                }
                    break;

                case ShelfChoiceHeuristic.ShelfBestHeightFit:
                {
                    // Best Height Fit rule: Choose the shelf with best-matching height.
                    Shelf bestShelf = default;
                    var bestShelfHeightDifference = 0x7FFFFFFF;
                    for (var i = 0; i < _shelves.Count; ++i)
                    {
                        // Pre-rotate the rect onto the shelf here already so that the height fit computation
                        // is done correctly.
                        RotateToShelf(_shelves[i], ref width, ref height);
                        if (FitsOnShelf(_shelves[i], width, height, i == _shelves.Count - 1))
                        {
                            var heightDifference = Math.Max(_shelves[i].Height - height, 0);
                            Debug.Assert(heightDifference >= 0);

                            if (heightDifference < bestShelfHeightDifference)
                            {
                                bestShelf = _shelves[i];
                                bestShelfHeightDifference = heightDifference;
                            }
                        }
                    }

                    if (!Equals(bestShelf, default(Shelf)))
                    {
                        AddToShelf(bestShelf, width, height, newNode);
                        return newNode;
                    }
                }
                    break;

                case ShelfChoiceHeuristic.ShelfBestWidthFit:
                {
                    // Best Width Fit rule: Choose the shelf with smallest remaining shelf width.
                    Shelf bestShelf = default;
                    var bestShelfWidthDifference = 0x7FFFFFFF;
                    for (var i = 0; i < _shelves.Count; ++i)
                    {
                        // Pre-rotate the rect onto the shelf here already so that the height fit computation
                        // is done correctly.
                        RotateToShelf(_shelves[i], ref width, ref height);
                        if (FitsOnShelf(_shelves[i], width, height, i == _shelves.Count - 1))
                        {
                            var widthDifference = _binWidth - _shelves[i].CurrentX - width;
                            Debug.Assert(widthDifference >= 0);

                            if (widthDifference < bestShelfWidthDifference)
                            {
                                bestShelf = _shelves[i];
                                bestShelfWidthDifference = widthDifference;
                            }
                        }
                    }

                    if (!Equals(bestShelf, default(Shelf)))
                    {
                        AddToShelf(bestShelf, width, height, newNode);
                        return newNode;
                    }
                }
                    break;

                case ShelfChoiceHeuristic.ShelfWorstWidthFit:
                {
                    // Worst Width Fit rule: Choose the shelf with smallest remaining shelf width.
                    Shelf bestShelf = default;
                    var bestShelfWidthDifference = -1;
                    for (var i = 0; i < _shelves.Count; ++i)
                    {
                        // Pre-rotate the rect onto the shelf here already so that the height fit computation
                        // is done correctly.
                        RotateToShelf(_shelves[i], ref width, ref height);
                        if (FitsOnShelf(_shelves[i], width, height, i == _shelves.Count - 1))
                        {
                            var widthDifference = _binWidth - _shelves[i].CurrentX - width;
                            Debug.Assert(widthDifference >= 0);

                            if (widthDifference > bestShelfWidthDifference)
                            {
                                bestShelf = _shelves[i];
                                bestShelfWidthDifference = widthDifference;
                            }
                        }
                    }

                    if (!Equals(bestShelf, default(Shelf)))
                    {
                        AddToShelf(bestShelf, width, height, newNode);
                        return newNode;
                    }
                }
                    break;
            }

            // The rectangle did not fit on any of the shelves. Open a new shelf.

            // Flip the rectangle so that the long side is horizontal.
            if (width < height && height <= _binWidth)
            {
                var temp = width;
                width = height;
                height = temp;
            }

            if (CanStartNewShelf(height))
            {
                if (_useWasteMap)
                    MoveShelfToWasteMap(_shelves.Last());

                StartNewShelf(height);
                Debug.Assert(FitsOnShelf(_shelves.Last(), width, height, true));
                AddToShelf(_shelves.Last(), width, height, newNode);
                return newNode;
            }
            /*
                ///\todo This is problematic: If we couldn't start a new shelf - should we give up
                ///      and move all the remaining space of the bin for the waste map to track,
                ///      or should we just wait if the next rectangle would fit better? For now,
                ///      don't add the leftover space to the waste map. 
                else if (useWasteMap)
                {
                    assert(binHeight - shelves.back().startY >= shelves.back().height);
                    shelves.back().height = binHeight - shelves.back().startY;
                    if (shelves.back().height > 0)
                        MoveShelfToWasteMap(shelves.back());

                    // Try to pack the rectangle again to the waste map.
                    GuillotineBinPack::Node node = wasteMap.Insert(width, height, true, 1, 3);
                    if (node.height != 0)
                    {
                        newNode.x = node.x;
                        newNode.y = node.y;
                        newNode.width = node.width;
                        newNode.height = node.height;
                        return newNode;
                    }
                }
            */

            // The rectangle didn't fit.
            //memset(&newNode, 0, sizeof(Rect));
            return newNode;
        }

        private void MoveShelfToWasteMap(Shelf shelf)
        {
            var freeRects = _wasteMap.GetFreeRectangles();

            // Add the gaps between each rect top and shelf ceiling to the waste map.
            for (var i = 0; i < shelf.UsedRectangles.Count; ++i)
            {
                var r = shelf.UsedRectangles[i];
                Rect newNode = default;
                newNode.X = r.X;
                newNode.Y = r.Y + r.Height;
                newNode.Width = r.Width;
                newNode.Height = shelf.Height - r.Height;
                if (newNode.Height > 0)
                    freeRects.Add(newNode);
            }

            shelf.UsedRectangles.Clear();

            // Add the space after the shelf end (right side of the last rect) and the shelf right side. 
            Rect newNode1 = default;
            newNode1.X = shelf.CurrentX;
            newNode1.Y = shelf.StartY;
            newNode1.Width = _binWidth - shelf.CurrentX;
            newNode1.Height = shelf.Height;
            if (newNode1.Width > 0)
                freeRects.Add(newNode1);

            // This shelf is DONE.
            shelf.CurrentX = _binWidth;

            // Perform a rectangle merge step.
            _wasteMap.MergeFreeList();
        }

        /// Computes the ratio of used surface area to the bin area.
        private float Occupancy()
        {
            return (float) _usedSurfaceArea / (_binWidth * _binHeight);
        }
    }
}