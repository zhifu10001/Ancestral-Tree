﻿/*
 * A panel drawing an abego tree. Provides hover/click events for
 * tree nodes.
 */

using AncesTree.TreeLayout;
using AncesTree.TreeModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Point = System.Drawing.Point;

// TODO are duplicated nodes for multi-marriage getting connected? i.e. where is 'DrawDuplicateNode(PersonNode)'?

namespace AncesTree
{
    public class TreePanel2 : Panel
    {
        #region Provided Events
        public delegate void NodeClick(object sender, ITreeData node);
        public delegate void NodeHover(object sender, ITreeData node);

        [Browsable(true)]
        public event NodeClick OnNodeClick;
        [Browsable(true)]
        public event NodeHover OnNodeHover;
        #endregion

        private TreeConfiguration _config;

        public TreePanel2()
        {
            BorderStyle = BorderStyle.FixedSingle;
            ResizeRedraw = true;
            BackColor = Color.Beige;
            DoubleBuffered = true;
            Zoom = 1.0f;

            MouseClick += TreePanel2_MouseClick;
            MouseMove += TreePanel2_MouseMove;
        }

        #region Mouse Event Handling
        int lastX = -1;
        int lastY = -1;
        private void TreePanel2_MouseMove(object sender, MouseEventArgs e)
        {
            if (Math.Abs(lastX - e.X) < 3 && Math.Abs(lastY - e.Y) < 3) // ignore minor move delta
                return;
            lastX = e.X;
            lastY = e.Y;

            ITreeData node = findPersonFromPoint(e.X, e.Y);
            if (node != null)
                OnNodeHover?.Invoke(this, node);
        }

        private void TreePanel2_MouseClick(object sender, MouseEventArgs e)
        {
            ITreeData node = findPersonFromPoint(e.X, e.Y);
            if (node != null)
                OnNodeClick?.Invoke(this, node);
        }
        #endregion

        private ITreeData findNodeByPoint(int x0, int y0)
        {
            if (_boxen == null)
                return null;

            float x = x0 / _zoom; // 'undo' impact of zoom: box bounds are un-zoomed
            float y = y0 / _zoom;

            foreach (var nodeRect in _boxen.getNodeBounds().Values)
            {
                if (nodeRect.Contains(x, y))
                    return _boxen.getNodeBounds().FirstOrDefault(i => i.Value == nodeRect).Key;
            }
            return null;
        }

        private ITreeData getPersonFromUnion(UnionNode un, int x0, int y0)
        {
            float x = x0 / _zoom; // 'undo' impact of zoom: box bounds are un-zoomed
            float y = y0 / _zoom;

            var box = _boxen.getNodeBounds()[un]; //drawBounds(un);
            var box1 = new Rect(box.X, box.Y, un.P1.Wide, un.P1.High);
            if (box1.Contains(x, y))
                return un.P1;

            Rect box2;
            if (un.Vertical)
            {
                box2 = new Rect(box.X, box.Y + un.P1.High + UNION_BAR_WIDE, un.P2.Wide, un.P2.High);
            }
            else
            {
                box2 = new Rect(box.X + un.P1.Wide + UNION_BAR_WIDE, box.Y, un.P2.Wide, un.P2.High);
            }
            if (box2.Contains(x, y))
                return un.P2;
            return null;
        }

        /// <summary>
        /// Determine which Person box is under the mouse cursor. I.e. finds which person in
        /// a UnionNode, or the PersonNode.
        /// </summary>
        /// <param name="X">mouse cursor x pos</param>
        /// <param name="Y">mouse cursor y pos</param>
        /// <returns></returns>
        private ITreeData findPersonFromPoint(int X, int Y)
        {
            ITreeData node = findNodeByPoint(X - 8, Y - 8); // TODO WHY is this delta necessary?
            UnionNode un = node as UnionNode;
            if (un != null)
            {
                node = getPersonFromUnion(un, X - 8, Y - 8);
            }
            return node;
        }

        public void drawTree(Graphics g)
        {
            if (_boxen == null)
                return;

            _config = _boxen.getConfiguration() as TreeConfiguration;
            if (_config == null)
                return;

            _g = g;

            //_g.SmoothingMode = SmoothingMode.AntiAlias;
            //_g.TextRenderingHint = TextRenderingHint.AntiAlias;

            g.Clear(_config.BackColor.GetColor());
            _g.ScaleTransform(_zoom, _zoom);
            _g.TranslateTransform(_margin, _margin);

            // TODO create and cache outside paint
            _duplPen = _config.DuplLine.GetPen();
            _multEdge = _config.MMargLine.GetPen();
            _border = _config.NodeBorder.GetPen();
            _spousePen = _config.SpouseLine.GetPen();
            _childPen = _config.ChildLine.GetPen();
            {
                PaintEdges(GetTree().getRoot());

                _nextLevel = 1;
                // paint the boxes
                foreach (var node in _boxen.getNodeBounds().Keys)
                {
                    paintNode(node);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            drawTree(e.Graphics);
        }

        private int _margin = 10; // TODO from configuration

        public int TreeMargin
        {
            get { return _margin; }
            set { _margin = Math.Max(value, 0); ResizeMe(); }
        }

        private float _zoom;
        public float Zoom 
        { 
            get { return _zoom; }
            set { _zoom = Math.Max(value, 0.1f); ResizeMe(); } 
        }

        private Graphics _g;
        private TreeLayout<ITreeData> _boxen;

        public TreeLayout<ITreeData> Boxen
        {
            set { _boxen = value; ResizeMe(); }
        }

        private void ResizeMe()
        {
            // Tree or scale changed. Update control size to fit.

            if (_boxen == null)
                return;
            var newSize = _boxen.getBounds();
            // NOTE side-effect: control is invalidated from resize BUG except when size doesn't change!
            this.Size = new System.Drawing.Size
                ((int)((newSize.Width + 2 * _margin) * _zoom),
                 (int)((newSize.Height + 2 * _margin) * _zoom));
            Invalidate();
        }

        private Pen _border;

        private Pen _multEdge;

        private Pen _duplPen;

        private Pen _spousePen;

        private Pen _childPen;

        #region TreeLayout Access/Helper functions

        private Rectangle drawBounds(ITreeData node)
        {
            Rect r = getBoundsOfNode(node);
            return new Rectangle((int)r.Left, (int)r.Top, (int)r.Width, (int)r.Height);
        }

        private Rect getBoundsOfNode(ITreeData node)
        {
            return _boxen.getNodeBounds()[node];
        }

        private IEnumerable<ITreeData> getChildren(ITreeData parent)
        {
            return GetTree().getChildren(parent);
        }

        private bool hasChildren(ITreeData parent)
        {
            var enumer = getChildren(parent);
            return (enumer != null && enumer.Any());
        }

        private TreeForTreeLayout<ITreeData> GetTree()
        {
            return _boxen.getTree();
        }

        #endregion

        private readonly static Color TEXT_COLOR = Color.Black; // TODO configuration

        private const int UNION_BAR_WIDE = 20; // TODO pull from configuration

        private const int gapBetweenLevels = 30; // TODO pull from configuration

        private int _nextLevel;

        private void paintNode(ITreeData node)
        {
            int currDepth = 0;
            int genLineY = 0;
            bool drawVert = false;

            Rectangle box = drawBounds(node);

            PersonNode foo = node as PersonNode;
            if (foo != null)
            {
                // Don't draw the fake for multi-marriage at root
                if (foo.Text != " " || foo.Who != null)
                    paintABox(foo, box);
                currDepth = foo.Depth;
                genLineY = foo.Vertical ? box.Right : box.Bottom;
                drawVert = foo.Vertical;
            }
            else
            {
                // Drawing a union node. Said node consists of two Person boxes.
                // (Spouse connector drawn in PaintEdges).
                UnionNode bar = node as UnionNode;
                if (bar != null)
                {
                    Rectangle box1 = new Rectangle(box.X, box.Y, bar.P1.Wide, bar.P1.High);
                    Rectangle box2;
                    if (bar.Vertical)
                    {
                        box2 = new Rectangle(box.X, box.Y + bar.P1.High + UNION_BAR_WIDE, bar.P2.Wide, bar.P2.High);
                    }
                    else
                    {
                        box2 = new Rectangle(box.X + bar.P1.Wide + UNION_BAR_WIDE, box.Y, bar.P2.Wide, bar.P2.High);
                    }
                    paintABox(bar.P1, box1);
                    paintABox(bar.P2, box2);

                    currDepth = bar.P1.Depth;
                    genLineY = bar.Vertical ? Math.Max(box1.Right, box2.Right) : Math.Max(box1.Bottom, box2.Bottom);
                    drawVert = bar.Vertical;
                }

                // debugging
                //using (var pen = new Pen(Color.Magenta))
                //    _g.DrawRectangle(pen, box);
            }

            if (currDepth == _nextLevel && _config.GenLines)
            {
                _nextLevel += 1;
                genLineY += 8;
                if (drawVert)
                    _g.DrawLine(Pens.Blue, new Point(genLineY,0), new Point(genLineY, Height));
                else
                    _g.DrawLine(Pens.Blue, new Point(0, genLineY), new Point(Width, genLineY));

            }
        }

        private void paintABox(PersonNode tib, Rectangle box)
        {
            using (Brush b = new SolidBrush(tib.BackColor))
                _g.FillRectangle(b, box);
            _g.DrawRectangle(_border, box);
            using (var font = tib.DrawVert ? _config.MajorFont.GetFont() : _config.MinorFont.GetFont())
                _g.DrawString(tib.Text, font, new SolidBrush(TEXT_COLOR), box.X, box.Y);
        }

        private void PaintEdges(ITreeData parent)
        {
            if (parent is PersonNode)
            {
                var p = parent as PersonNode;
                // Don't draw the fake for multi-marriage at root
                if (!string.IsNullOrEmpty(p.Text) || p.Who != null)
                    paintPersonEdges(parent as PersonNode);
            }
            else
            {
                if (parent.Vertical)
                    paintUnionEdgesV(parent as UnionNode);
                else
                    paintUnionEdgesH(parent as UnionNode);
            }
            foreach (var child in getChildren(parent))
            {
                PaintEdges(child);
            }
        }

        private void paintPersonEdges(PersonNode parent)
        {
            var b1 = drawBounds(parent);

            // Spouse connectors in a multi-marriage case.
            // All spouses have been drawn to the right/below  
            // this node.
            if (parent.HasSpouses)
            {
                if (parent.Vertical)
                {
                    // Need to determine the left-most node with narrow spouse nodes.
                    int topX = b1.Left + b1.Width / 2; 
                    foreach (var node in parent.Spouses)
                    {
                        var b2 = drawBounds(node);
                        topX = Math.Min((b2.Left + b2.Width / 2), topX);
                    }

                    int topY = b1.Bottom;
                    foreach (var node in parent.Spouses)
                    {
                        var b3 = drawBounds(node);
                        int botY = b3.Top;
                        _g.DrawLine(_multEdge, topX, topY, topX, botY);
                    }
                }
                else
                {
                    int leftX = b1.Right;
                    int leftY = b1.Top + b1.Height / 2;
                    foreach (var node in parent.Spouses)
                    {
                        var b3 = drawBounds(node);
                        int rightX = b3.Left;

                        // TODO consider drawing distinct line for each?
                        _g.DrawLine(_multEdge, leftX, leftY, rightX, leftY);
                    }
                }
            }

            if (GetTree().isLeaf(parent)) // No children, nothing further to do
                return;

            // center-bottom of parent
            int parentX = b1.Left + b1.Width / 2;
            int parentY = b1.Bottom;

            if (parent.Vertical)
                DrawChildrenEdgesV(parent);
            else
                DrawChildrenEdgesH(parent, parentX, parentY);
        }

        private void DrawChildrenEdgesV(ITreeData parent)
        {
            var b1 = drawBounds(parent);

            // Line from left side of children to half way across the gen. gap
            // This *only* works if alignment for nodes is "toward root"!
            // [because with align==center, narrow children are further right, i.e.
            // the 'left side of children' is not constant].
            // 20180818 In theory, getSizeOfLevel could work for calculating this, except
            // we need to be able to determine a node's level, which doesn't work
            // right now for 'pseudo' nodes.
            // e.g. double levelWide = _boxen.getSizeOfLevel(_boxen.getLevelForNode(parent))

            int targetX = -1;
            int startY = b1.Top + b1.Height / 2;

            // Determine the top/bottom of the child-line
            int minChildY = int.MaxValue;
            int maxChildY = int.MinValue;
            foreach (var child in getChildren(parent))
            {
                // Do not draw 'I'm a child' line for spouses
                if (child is PersonNode && !((PersonNode)child).DrawVert)
                    continue;

                var b2 = drawBounds(child);

                // Determine the location of the child line relative to the
                // child left side [requires align == towardroot!]
                if (targetX == -1)
                    targetX = b2.Left - gapBetweenLevels / 2;

                int childX = b2.Left;
                int childY = b2.Top + child.ParentConnectLoc;

                minChildY = Math.Min(minChildY, childY);
                maxChildY = Math.Max(maxChildY, childY);

                _g.DrawLine(_childPen, targetX, childY, childX, childY);
            }

            // In the case a union has a single child, the horz. from child unlikely to
            // connect to horz. from union. Draw a vert. connector.
            if (minChildY == maxChildY)
                _g.DrawLine(_childPen, targetX, minChildY, targetX, startY);
            else
                _g.DrawLine(_childPen, targetX, minChildY, targetX, maxChildY);

            // TargetX has been calculated, can draw from the parent to the 
            // child line.
            _g.DrawLine(_childPen, b1.Right, startY, targetX, startY);
        }

        private void DrawChildrenEdgesH(ITreeData parent, int startx, int starty)
        {
            // Common code to draw edges from parent-node to children-nodes.
            // Horizontal (root at top) variant.
            // Used for UnionNode parent, and PersonNode parent in the multi-marriage
            // scenario.
            // Start position: place to start whether a union or a person node

            var b1 = drawBounds(parent);

            // Bottom point - vertical line from start position to child line
            int targetY = b1.Bottom + (gapBetweenLevels / 2);

            // Vertical connector from start position to child-line
            _g.DrawLine(_childPen, startx, starty, startx, targetY);

            // determine the left/right of the child-line
            int minChildX = int.MaxValue;
            int maxChildX = int.MinValue;

            foreach (var child in getChildren(parent))
            {
                // Do not draw 'I'm a child' line for spouses
                if (child is PersonNode && !((PersonNode)child).DrawVert)
                    continue;

                var b2 = drawBounds(child);
                int childX = b2.Left + child.ParentConnectLoc;
                int childY = b2.Top;

                minChildX = Math.Min(minChildX, childX);
                maxChildX = Math.Max(maxChildX, childX);

                // vertical line from top of child to half-way up to previous level
                _g.DrawLine(_childPen, childX, childY, childX, targetY);
            }
            _g.DrawLine(_childPen, minChildX, targetY, maxChildX, targetY);

            // Union has a single child. Vertical from child unlikely to
            // connect to vertical from union. Draw a horizontal connector.
            if (minChildX == maxChildX)
                _g.DrawLine(_childPen, minChildX, targetY, startx, targetY);
        }

        private void paintUnionEdgesV(UnionNode parent)
        {
            Rectangle b1 = drawBounds(parent);

            // spouse connector between boxes
            int x = b1.Left + Math.Min(parent.P1.Wide, parent.P2.Wide) / 2;
            int y = b1.Top + parent.P1.High;
            _g.DrawLine(_spousePen, x, y, x, y + UNION_BAR_WIDE);

            // nothing further to do if this is a "duplicate" or there are no children
            if (DrawDuplicateNode(parent) || !hasChildren(parent))
                return;

            // Draw the connector from the spouse-line to the children-line
            int horzLx = x;
            int horzLy = y + UNION_BAR_WIDE / 2;
            int targetX = -1; // Need to calculate the child-line loc relative to the children
                              // NOTE: requires align==towardsroot!

            // determine top/bottom of children line
            int minChildY = int.MaxValue;
            int maxChildY = int.MinValue;

            foreach (var child in getChildren(parent))
            {
                // Do not draw 'I'm a child' line for spouses
                if (child is PersonNode && !((PersonNode)child).DrawVert)
                    continue;

                var b2 = drawBounds(child);
                int childX = b2.Left;
                int childY = b2.Top + child.ParentConnectLoc;

                if (targetX == -1)
                    targetX = childX - gapBetweenLevels / 2;

                minChildY = Math.Min(minChildY, childY);
                maxChildY = Math.Max(maxChildY, childY);

                // connector from child to child-line
                _g.DrawLine(_childPen, childX, childY, targetX, childY);
            }

            // Union has a single child. Connector from child unlikely to
            // match to connector from union. Draw an extra connector.
            if (minChildY == maxChildY)
                _g.DrawLine(_childPen, targetX, minChildY, targetX, horzLy);
            else
                _g.DrawLine(_childPen, targetX, minChildY, targetX, maxChildY); // the child-line proper

            // union-link to child line
            _g.DrawLine(_childPen, horzLx, horzLy, targetX, horzLy);
        }

        private void paintUnionEdgesH(UnionNode parent)
        {
            // Draw edge connectors associated with a Union. This is the horizontal
            // (root at top) variant.

            var b1 = drawBounds(parent);

            // connector between spouse boxes
            int y = b1.Top + (b1.Height / 2);
            int x = b1.Left + parent.P1.Wide;
            _g.DrawLine(_spousePen, x, y, x + UNION_BAR_WIDE, y);

            // if this is a duplicate, or there are no children, we're done
            if (DrawDuplicateNode(parent) || !hasChildren(parent))
                return;

            // Top point - vertical line from connector to child line
            int vertLx = x + UNION_BAR_WIDE / 2;
            int vertLy = y;

            DrawChildrenEdgesH(parent, vertLx, vertLy);
        }

        /// <summary>
        /// Draw a connection between two, duplicated union nodes
        /// </summary>
        /// <param name="union"></param>
        /// <returns></returns>
        private bool DrawDuplicateNode(UnionNode union)
        {
            // This is not a duplicate, nothing to do
            if (union.DupNode == null)
                return false;

            var thisRect = drawBounds(union);
            var destRect = drawBounds(union.DupNode);

            thisRect.Inflate(1, 1); // TODO need to use the pen thickness
            destRect.Inflate(1, 1); // TODO need to use the pen thickness

            int midXDest = destRect.Left + (destRect.Right - destRect.Left) / 2;
            int midXthis = thisRect.Left + (thisRect.Right - thisRect.Left) / 2;

            int yThis = thisRect.Bottom;
            int yDest = destRect.Bottom;

            int midmidX = midXDest + (midXthis - midXDest) / 2;
            int midY = Math.Max(thisRect.Bottom, destRect.Bottom) + (gapBetweenLevels / 2) - 5; // TODO tweak/make constant

            if (union.Vertical)
            {
                midXthis = thisRect.Right;
                midXDest = destRect.Right;

                yThis = thisRect.Top + thisRect.Height / 2;
                yDest = destRect.Top + destRect.Height / 2;

                midmidX = Math.Max(midXthis, midXDest) + (gapBetweenLevels / 2) - 5; // TODO tweak/make constant
                midY = Math.Min(yThis, yDest) + Math.Abs(yThis - yDest) / 2;
            }

            // Curve points from middle of two boxes to a point half-way between; said point below/right of boxes
            Point p1 = new Point(midXthis, yThis);
            Point p2 = new Point(midmidX, midY);
            Point p3 = new Point(midXDest, yDest);

            _g.DrawRectangle(_duplPen, thisRect);
            _g.DrawRectangle(_duplPen, destRect);

            // TODO p2 might be below the bottom of the panel.
            // TODO consider drawing from left edge of 'this' to right edge of 'dest'
            _g.DrawCurve(_duplPen, new[] { p1, p2, p3 });
            return true;
        }

    }
}
