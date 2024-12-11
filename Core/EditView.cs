/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Linq;
using System.Collections.Generic;

namespace FooEditEngine
{
    enum AdjustFlow
    {
        Row,
        Col,
        Both,
    }

    enum TextPointSearchRange
    {
        TextAreaOnly,
        Full
    }

    /// <summary>
    /// キャレットとドキュメントの表示を担当します。レイアウト関連もこちらで行います
    /// </summary>
    sealed class EditView : ViewBase

    {
        internal const float LineMarkerThickness = 2;
        long tickCount;
        bool _CaretBlink;
        internal const int LineNumberLength = 6;
        const int UpdateAreaPaddingWidth = 2;
        const int UpdateAreaWidth = 4;
        const int UpdateAreaTotalWidth = UpdateAreaWidth + UpdateAreaPaddingWidth;

        /// <summary>
        /// コンストラクター
        /// </summary>
        public EditView(Document doc, IEditorRender r, int MarginLeftAndRight = 5)
            : this(doc, r, new Padding(MarginLeftAndRight, 0, MarginLeftAndRight, 0))
        {
        }

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="doc">ドキュメント</param>
        /// <param name="r">レンダー</param>
        /// <param name="margin">マージン（１番目：左、２番目：上、３番目：右、４番目：下）</param>
        public EditView(Document doc, IEditorRender r, Padding margin)
            : base(doc, r, margin)
        {
            this.CaretBlinkTime = 500;
            this.CaretWidthOnInsertMode = 1;
            this.LayoutLines.FoldingCollection.StatusChanged += FoldingCollection_StatusChanged;
            this.IsFocused = false;
        }

        /// <summary>
        /// 選択範囲コレクション
        /// </summary>
        internal SelectCollection Selections
        {
            get { return this.Document.Selections; }
            set { this.Document.Selections = value; }
        }

        /// <summary>
        /// ラインマーカーを描くなら偽。そうでなければ真
        /// </summary>
        public bool HideLineMarker
        {
            get { return this.Document.HideLineMarker; }
            set { this.Document.HideLineMarker = value; }
        }

        /// <summary>
        /// キャレットを描くなら偽。そうでなければ真
        /// </summary>
        public bool HideCaret
        {
            get { return this.Document.HideCaret; }
            set { this.Document.HideCaret = value; }
        }

        /// <summary>
        /// 挿入モードなら真を返し、上書きモードなら偽を返す
        /// </summary>
        public bool InsertMode
        {
            get { return this.Document.InsertMode; }
            set { this.Document.InsertMode = value; }
        }

        /// <summary>
        /// キャレットの点滅間隔
        /// </summary>
        public int CaretBlinkTime
        {
            get;
            set;
        }

        /// <summary>
        /// 挿入モード時のキャレットの幅
        /// </summary>
        public double CaretWidthOnInsertMode
        {
            get;
            set;
        }

        /// <summary>
        /// フォーカスがあるなら真をセットする
        /// </summary>
        public bool IsFocused
        {
            get;
            set;
        }

        /// <summary>
        /// キャレットを点滅させるなら真。そうでないなら偽
        /// </summary>
        /// <remarks>キャレット点滅タイマーもリセットされます</remarks>
        public bool CaretBlink
        {
            get { return this._CaretBlink; }
            set
            {
                this._CaretBlink = value;
                if (value)
                    this.tickCount = DateTime.Now.Ticks + this.To100nsTime(this.CaretBlinkTime);
            }
        }

        /// <summary>
        /// 一ページの高さに収まる行数を返す（こちらは表示されていない行も含みます）
        /// </summary>
        public int LineCountOnScreenWithInVisible
        {
            get;
            private set;
        }

        /// <summary>
        /// スクロール時に確保するマージン幅
        /// </summary>
        public double ScrollMarginWidth
        {
            get { return this.PageBound.Width * 20 / 100; }
        }

        /// <summary>
        /// キャレットがある領域を示す
        /// </summary>
        public Point CaretLocation
        {
            get;
            private set;
        }

        /// <summary>
        /// ヒットテストを行う
        /// </summary>
        /// <param name="x">x座標</param>
        /// <param name="y">y座標</param>
        /// <returns>テキストエリア内にあれば真。そうでなければ偽</returns>
        public bool HitTextArea(double x, double y)
        {
            if (this.render == null)
                return false;
            if (x >= this.render.TextArea.X && x <= this.render.TextArea.Right &&
                y >= this.render.TextArea.Y && y <= this.render.TextArea.Bottom)
                return true;
            else
                return false;
        }

        public bool IsUpperTextArea(double x, double y)
        {
            if (this.render == null)
                return false;
            if (x >= this.render.TextArea.X && x <= this.render.TextArea.Right && y < this.render.TextArea.Y)
                return true;
            else
                return false;
        }

        public bool IsUnderTextArea(double x,double y)
        {
            if (this.render == null)
                return false;
            if (x >= this.render.TextArea.X && x <= this.render.TextArea.Right && y > this.render.TextArea.Bottom)
                return true;
            else
                return false;
        }

        /// <summary>
        /// ヒットテストを行う
        /// </summary>
        /// <param name="x">x座標</param>
        /// <param name="row">行</param>
        /// <returns>ヒットした場合はFoldingDataオブジェクトが返され、そうでない場合はnullが返る</returns>
        public FoldingItem HitFoldingData(double x, int row)
        {
            IEditorRender render = (IEditorRender)base.render;

            if (render == null)
                return null;

            if (x >= this.GetRealtiveX(AreaType.FoldingArea) && x <= this.GetRealtiveX(AreaType.FoldingArea) + render.FoldingWidth)
            {
                int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(row);
                int lineLength = this.LayoutLines.GetLengthFromLineNumber(row);
                FoldingItem foldingData = this.LayoutLines.FoldingCollection.Get(lineHeadIndex, lineLength);
                if (foldingData != null && foldingData.IsFirstLine(this.LayoutLines,row))
                    return foldingData;
            }
            return null;
        }

        /// <summary>
        /// Rectで指定された範囲にドキュメントを描く
        /// </summary>
        /// <param name="updateRect">描写する範囲</param>
        /// <param name="force">キャッシュした内容を使用しない場合は真を指定する</param>
        /// <remarks>描写する範囲がPageBoundより小さい場合、キャッシュされた内容を使用することがあります。なお、レタリング後にrender.CacheContent()を呼び出さなかった場合は更新範囲にかかわらずキャッシュを使用しません</remarks>
        public override void Draw(Rectangle updateRect,bool force = false)
        {
            if (this.LayoutLines.Count == 0)
                return;

            IEditorRender render = (IEditorRender)base.render;

            if (render == null)
                return;

            if ((updateRect.Height < this.PageBound.Height ||
                updateRect.Width < this.PageBound.Width) && 
                render.IsVaildCache() &&
                !force)
            {
                render.DrawCachedBitmap(updateRect,updateRect);
            }
            else
            {
                Rectangle background = this.PageBound;
                render.FillBackground(background);

                if (this.Document.HideRuler == false)
                    this.DrawRuler();

                double endposy = this.render.TextArea.Bottom;
                Point pos = this.render.TextArea.TopLeft;
                pos.X -= this.Src.X;
                //画面上では行をずらして表示する
                pos.Y += this.Src.OffsetY;

                this.render.BeginClipRect(this.render.TextArea);

                for (int i = this.Src.Row; i < this.LayoutLines.Count; i++)
                {
                    int lineIndex = this.LayoutLines.GetIndexFromLineNumber(i);
                    int lineLength = this.LayoutLines.GetLengthFromLineNumber(i);
                    ITextLayout layout = this.LayoutLines.GetLayout(i);

                    if (pos.Y > endposy)
                        break;

                    FoldingItem foldingData = this.LayoutLines.FoldingCollection.Get(lineIndex, lineLength);

                    if (foldingData != null)
                    {
                        if (this.LayoutLines.FoldingCollection.IsHidden(lineIndex))
                            continue;
                    }

                    if (i == this.Document.CaretPostion.row)
                        this.DrawLineMarker(pos, layout);

                    this.render.DrawOneLine(this.Document, this.LayoutLines, i, pos.X, pos.Y);

                    pos.Y += layout.Height;
                }

                this.render.EndClipRect();

                //リセットしないと行が正しく描けない
                pos = this.render.TextArea.TopLeft;
                //画面上では行をずらして表示する
                pos.Y += this.Src.OffsetY;

                Size lineNumberSize = new Size(this.render.LineNemberWidth, this.render.TextArea.Height);
                
                for (int i = this.Src.Row; i < this.LayoutLines.Count; i++)
                {
                    int lineIndex = this.LayoutLines.GetIndexFromLineNumber(i);
                    int lineLength = this.LayoutLines.GetLengthFromLineNumber(i);
                    ITextLayout layout = this.LayoutLines.GetLayout(i);

                    if (pos.Y > endposy)
                        break;

                    FoldingItem foldingData = this.LayoutLines.FoldingCollection.Get(lineIndex, lineLength);

                    if (foldingData != null)
                    {
                        if (this.LayoutLines.FoldingCollection.IsHidden(lineIndex))
                            continue;
                        if (foldingData.IsFirstLine(this.LayoutLines, i) && foldingData.End >= lineIndex + lineLength)
                            render.DrawFoldingMark(foldingData.Expand, this.PageBound.X + this.GetRealtiveX(AreaType.FoldingArea), pos.Y);
                    }

                    if (this.Document.DrawLineNumber)
                    {
                        this.render.DrawString((i + 1).ToString(), this.PageBound.X + this.GetRealtiveX(AreaType.LineNumberArea), pos.Y, StringAlignment.Right, lineNumberSize, StringColorType.LineNumber);
                    }

                    DrawUpdateArea(i, pos.Y);

                    pos.Y += layout.Height;
                }

                this.Document.SelectGrippers.BottomLeft.Draw(this.render);
                this.Document.SelectGrippers.BottomRight.Draw(this.render);

                render.CacheContent();
            }

            this.DrawInsertPoint();

            this.DrawCaret();
        }

        void DrawUpdateArea(int row,double ypos)
        {
            IEditorRender render = (IEditorRender)base.render;
            if(this.LayoutLines.GetDirtyFlag(row))
            {
                Point pos = new Point(this.PageBound.X + this.GetRealtiveX(AreaType.UpdateArea), ypos);
                Rectangle rect = new Rectangle(pos.X, pos.Y, UpdateAreaWidth, this.LayoutLines.GetLayout(row).Height);
                render.FillRectangle(rect, FillRectType.UpdateArea);
            }
        }

        void DrawRuler()
        {
            IEditorRender render = (IEditorRender)base.render;

            Point pos, from, to;
            Size emSize = render.emSize;
            double lineHeight = emSize.Height * render.LineEmHeight;
            Rectangle clipRect = this.render.TextArea;
            int count = 0;
            double markerHeight = lineHeight / 2;
            if (this.Document.RightToLeft)
            {
                pos = new Point(clipRect.TopRight.X, clipRect.TopRight.Y - lineHeight - LineMarkerThickness);
                for (; pos.X >= clipRect.TopLeft.X; pos.X -= emSize.Width, count++)
                {
                    from = pos;
                    to = new Point(pos.X, pos.Y + lineHeight);
                    int mod = count % 10;
                    if (mod == 0)
                    {
                        string countStr = (count / 10).ToString();
                        double counterWidth = emSize.Width * countStr.Length;
                        this.render.DrawString(countStr, pos.X - counterWidth, pos.Y, StringAlignment.Right, new Size(counterWidth, double.MaxValue));
                    }
                    else if (mod == 5)
                        from.Y = from.Y + lineHeight / 2;
                    else
                        from.Y = from.Y + lineHeight * 3 / 4;
                    render.DrawLine(from, to);
                    if (this.CaretLocation.X >= pos.X && this.CaretLocation.X < pos.X + emSize.Width)
                        render.FillRectangle(new Rectangle(pos.X, pos.Y + markerHeight, emSize.Width, markerHeight), FillRectType.OverwriteCaret);
                }
            }
            else
            {
                pos = new Point(clipRect.TopLeft.X, clipRect.TopLeft.Y - lineHeight - LineMarkerThickness);
                for (; pos.X < clipRect.TopRight.X; pos.X += emSize.Width, count++)
                {
                    from = pos;
                    to = new Point(pos.X, pos.Y + lineHeight);
                    int mod = count % 10;
                    if (mod == 0)
                        this.render.DrawString((count / 10).ToString(), pos.X, pos.Y, StringAlignment.Left, new Size(double.MaxValue, double.MaxValue));
                    else if (mod == 5)
                        from.Y = from.Y + lineHeight / 2;
                    else
                        from.Y = from.Y + lineHeight * 3 / 4;
                    render.DrawLine(from, to);
                    if (this.CaretLocation.X >= pos.X && this.CaretLocation.X < pos.X + emSize.Width)
                        render.FillRectangle(new Rectangle(pos.X, pos.Y + markerHeight, emSize.Width, markerHeight), FillRectType.OverwriteCaret);
                }
            }
            from = clipRect.TopLeft;
            from.Y -= LineMarkerThickness;
            to = clipRect.TopRight;
            to.Y -= LineMarkerThickness;
            render.DrawLine(from, to);
        }

        void DrawInsertPoint()
        {
            //一つしかない場合は行選択の可能性がある
            if (this.Selections.Count <= 1)
                return;
            IEditorRender render = (IEditorRender)base.render;
            foreach (Selection sel in this.Selections)
            {
                if (sel.length == 0)
                {
                    TextPoint tp = this.GetLayoutLineFromIndex(sel.start);
                    Point left = this.GetPostionFromTextPoint(tp);
                    double lineHeight = render.emSize.Height * render.LineEmHeight;
                    Rectangle InsertRect = new Rectangle(left.X,
                        left.Y,
                        CaretWidthOnInsertMode,
                        lineHeight);
                    render.FillRectangle(InsertRect, FillRectType.InsertPoint);
                }
            }
        }

        bool DrawCaret()
        {
            if (this.HideCaret || !this.IsFocused)
                return false;

            long diff = DateTime.Now.Ticks - this.tickCount;
            long blinkTime = this.To100nsTime(this.CaretBlinkTime);

            if (this.CaretBlink && diff % blinkTime >= blinkTime / 2)
                return false;

            Rectangle CaretRect = new Rectangle();

            IEditorRender render = (IEditorRender)base.render;

            int row = this.Document.CaretPostion.row;
            ITextLayout layout = this.LayoutLines.GetLayout(row);
            double lineHeight = render.emSize.Height * render.LineEmHeight;
            double charWidth = layout.GetWidthFromIndex(this.Document.CaretPostion.col);

            if (this.InsertMode || charWidth == 0)
            {
                CaretRect.Size = new Size(CaretWidthOnInsertMode, lineHeight);
                CaretRect.Location = new Point(this.CaretLocation.X, this.CaretLocation.Y);
                render.FillRectangle(CaretRect, FillRectType.InsertCaret);
            }
            else
            {
                double height = lineHeight / 3;
                CaretRect.Size = new Size(charWidth, height);
                CaretRect.Location = new Point(this.CaretLocation.X, this.CaretLocation.Y + lineHeight - height);
                render.FillRectangle(CaretRect, FillRectType.OverwriteCaret);
            }
            return true;
        }

        long To100nsTime(int ms)
        {
            return ms * 10000;
        }

        public void DrawLineMarker(Point pos, ITextLayout layout)
        {
            if (this.HideLineMarker)
                return;
            IEditorRender render = (IEditorRender)base.render;
            Point p = this.CaretLocation;
            double height = layout.Height;
            double width = this.render.TextArea.Width;
            render.FillRectangle(new Rectangle(this.PageBound.X + this.render.TextArea.X, pos.Y, width, height), FillRectType.LineMarker);
        }

        /// <summary>
        /// 現在のキャレット位置の領域を返す
        /// </summary>
        /// <returns>矩形領域を表すRectangle</returns>
        public Rectangle GetCurrentCaretRect()
        {
            ITextLayout layout = this.LayoutLines.GetLayout(this.Document.CaretPostion.row);
            double width = layout.GetWidthFromIndex(this.Document.CaretPostion.col);
            if (width == 0.0)
                width = this.CaretWidthOnInsertMode;
            double height = this.LayoutLines.GetLineHeight(this.Document.CaretPostion);
            Rectangle updateRect = new Rectangle(
                this.CaretLocation.X,
                this.CaretLocation.Y,
                width,
                height);
            return updateRect;
        }

        /// <summary>
        /// 指定した座標の一番近くにあるTextPointを取得する
        /// </summary>
        /// <param name="p">ビューエリアを左上とする相対位置</param>
        /// <param name="searchRange">探索範囲</param>
        /// <returns>レイアウトラインを指し示すTextPoint</returns>
        public TextPoint GetTextPointFromPostion(Point p,TextPointSearchRange searchRange = TextPointSearchRange.TextAreaOnly)
        {
            if(searchRange == TextPointSearchRange.TextAreaOnly)
            {
                if (p.Y < this.render.TextArea.TopLeft.Y ||
                    p.Y > this.render.TextArea.BottomRight.Y)
                    return TextPoint.Null;
            }

            TextPoint tp = new TextPoint();

            if (this.LayoutLines.Count == 0)
                return tp;

            //表示領域から探索を始めるのでパディングの分だけ引く
            p.Y -= this.render.TextArea.Y;

            //p.Y に最も近い行を調べる(OffsetY分引かれてるので、その分足す)
            SrcPoint t = this.GetNearstRowAndOffsetY(this.Src.Row, p.Y);
            t.OffsetY -= this.Src.OffsetY;

            double relX = 0, relY;
            tp.row = t.Row;
            relY = t.OffsetY;    //相対位置がマイナスなので反転させる

            if (searchRange == TextPointSearchRange.TextAreaOnly)
            {
                if (p.X < this.render.TextArea.X)
                    return tp;
            }

            relX = p.X - this.render.TextArea.X + this.Document.Src.X;
            tp.col = this.LayoutLines.GetLayout(tp.row).GetIndexFromPostion(relX,relY);

            int lineLength = this.LayoutLines.GetLengthFromLineNumber(tp.row);
            if (tp.col > lineLength)
                tp.col = lineLength;

            return tp;
        }

        /// <summary>
        /// TextPointに対応する座標を得る
        /// </summary>
        /// <param name="tp">レイアウトライン上の位置</param>
        /// <returns>テキストエリアを左上とする相対位置</returns>
        public Point GetPostionFromTextPoint(TextPoint tp)
        {
            Point p = new Point();
            //表示上のずれを考慮せずキャレット位置を求める
            for (int i = this.Src.Row; i < tp.row; i++)
            {
                int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(i);
                int lineLength = this.LayoutLines.GetLengthFromLineNumber(i);
                if (this.LayoutLines.FoldingCollection.IsHidden(lineHeadIndex))
                    continue;
                p.Y += this.LayoutLines.GetLayout(i).Height;
            }
            Point relP = this.LayoutLines.GetLayout(tp.row).GetPostionFromIndex(tp.col);
            p.X += relP.X - Src.X + this.render.TextArea.X;
            p.Y += this.render.TextArea.Y + relP.Y + this.Src.OffsetY;  //実際に返す値は表示上のずれを考慮しないといけない
            return p;
        }

        public Gripper HitGripperFromPoint(Point p)
        {
            if (this.Document.SelectGrippers.BottomLeft.IsHit(p))
                return this.Document.SelectGrippers.BottomLeft;
            if (this.Document.SelectGrippers.BottomRight.IsHit(p))
                return this.Document.SelectGrippers.BottomRight;
            return null;
        }

        public Rectangle GetRectFromIndex(int index,int width,int height)
        {
            TextPoint tp = this.LayoutLines.GetTextPointFromIndex(index);
            return this.GetRectFromTextPoint(tp, width, height);
        }

        public Rectangle GetRectFromTextPoint(TextPoint tp, int width, int height)
        {
            if (tp.row < this.Src.Row)
                return Rectangle.Empty;
            //画面外にある時は計算する必要がそもそもない
            if (tp.row - this.Src.Row > this.LineCountOnScreen)
                return Rectangle.Empty;
            double radius = width / 2;
            Point point = this.GetPostionFromTextPoint(tp);
            double lineHeight = this.render.emSize.Height;


            Rectangle rect =  new Rectangle(point.X - radius, point.Y + lineHeight, width, height);

            if (rect.BottomLeft.Y >= this.render.TextArea.BottomLeft.Y ||
                rect.BottomRight.X < this.render.TextArea.BottomLeft.X ||
                rect.BottomLeft.X > this.render.TextArea.BottomRight.X)
                return Rectangle.Empty;
            return rect;
        }

        /// <summary>
        /// index上の文字が表示されるようにSrcを調整する
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>調整されたら真。そうでなければ偽</returns>
        public bool AdjustSrc(int index)
        {
            TextPoint startTextPoint = this.GetLayoutLineFromIndex(index);
            var result = this.AdjustSrc(startTextPoint, AdjustFlow.Row);
            return result.Item1;
        }

        /// <summary>
        /// キャレットがあるところまでスクロールする
        /// </summary>
        /// <return>再描写する必要があるなら真を返す</return>
        /// <remarks>Document.Update(type == UpdateType.Clear)イベント時に呼び出した場合、例外が発生します</remarks>
        public bool AdjustCaretAndSrc(AdjustFlow flow = AdjustFlow.Both)
        {
            IEditorRender render = (IEditorRender)base.render;

            bool result;
            Point pos;

            if (this.PageBound.Width == 0 || this.PageBound.Height == 0)
            {
                this.SetCaretPostion(this.Padding.Left + render.FoldingWidth, 0);
                return false;
            }

            (result,pos) = this.AdjustSrc(this.Document.CaretPostion, flow);

            this.SetCaretPostion(pos.X, pos.Y);

            return result;
        }

        /// <summary>
        /// テキストポイントが含まれるように表示位置を調整します
        /// </summary>
        /// <param name="tp">テキストポイント</param>
        /// <param name="flow">調整方向</param>
        /// <returns>調整位置が変わったなら真。変わってないなら偽</returns>
        public (bool,Point) AdjustSrc(TextPoint tp,AdjustFlow flow)
        {
            bool result = false;
            Point tpPoint = new Point();
            double x = this.CaretLocation.X;
            double y = this.CaretLocation.Y;
            Point relPoint = this.LayoutLines.GetLayout(tp.row).GetPostionFromIndex(tp.col);

            if (flow == AdjustFlow.Col || flow == AdjustFlow.Both)
            {
                x = relPoint.X;

                double left = this.Src.X;
                double right = this.Src.X + this.render.TextArea.Width;

                if (x >= left && x <= right)    //xは表示領域にないにある
                {
                    x -= left;
                }
                else if (x > right) //xは表示領域の右側にある
                {
                    this.Document.Src = new SrcPoint(x - this.render.TextArea.Width + this.ScrollMarginWidth, this.Document.Src.Row, this.Document.Src.OffsetY);
                    if (this.Document.RightToLeft && this.Document.Src.X > 0)
                    {
                        System.Diagnostics.Debug.Assert(x > 0);
                        this.Document.Src = new SrcPoint(0, this.Document.Src.Row, this.Document.Src.OffsetY);
                    }
                    else
                    {
                        x = this.render.TextArea.Width - this.ScrollMarginWidth;
                    }
                    result = true;
                }
                else if (x < left)    //xは表示領域の左側にある
                {
                    this.Document.Src = new SrcPoint(x - this.ScrollMarginWidth, this.Document.Src.Row, this.Document.Src.OffsetY);
                    if (!this.Document.RightToLeft && this.Document.Src.X < this.render.TextArea.X)
                    {
                        this.Document.Src = new SrcPoint(0, this.Document.Src.Row, this.Document.Src.OffsetY);
                    }
                    else
                    {
                        x = this.ScrollMarginWidth;
                    }
                    result = true;
                }
                x += this.render.TextArea.X;
            }

            if (flow == AdjustFlow.Row || flow == AdjustFlow.Both)
            {
                double lineHeight = this.render.emSize.Height * this.render.LineEmHeight;

                int PhyLineCountOnScreen = (int)(this.render.TextArea.Height / lineHeight);
                //計算量を減らすため
                if (tp.row < this.Src.Row || this.Src.Row + PhyLineCountOnScreen * 2 < tp.row)
                    this.Document.Src = new SrcPoint(this.Src.X, tp.row, -relPoint.Y);

                //キャレットのY座標を求める
                double caret_y = this.Src.OffsetY;  //src.rowからキャレット位置
                double alignedHeight = PhyLineCountOnScreen * lineHeight - lineHeight;
                for (int i = this.Src.Row; i < tp.row; i++)
                {
                    int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(i);
                    int lineLength = this.LayoutLines.GetLengthFromLineNumber(i);

                    if (this.LayoutLines.FoldingCollection.IsHidden(lineHeadIndex))
                        continue;
                    caret_y += this.LayoutLines.GetLayout(i).Height;
                }
                caret_y += relPoint.Y;

                if (caret_y < 0)
                {
                    this.Document.Src = new SrcPoint(this.Src.X, tp.row, -relPoint.Y);
                    y = 0;
                }
                else if (caret_y >= 0 && caret_y < alignedHeight)
                {
                    y = caret_y;
                }
                else if (caret_y >= alignedHeight)
                {
                    SrcPoint newsrc = this.GetNearstRowAndOffsetY(tp.row, -(alignedHeight - relPoint.Y));
                    this.Document.Src = new SrcPoint(this.Src.X, newsrc.Row, -newsrc.OffsetY);
                    y = alignedHeight;
                }
                y += this.render.TextArea.Y;
                result = true;
            }

            if (result)
            {
                this.OnSrcChanged(null);
            }

            tpPoint.X = x;
            tpPoint.Y = y;

            return (result,tpPoint);
        }

        /// <summary>
        /// レイアウト行をテキストポイントからインデックスに変換する
        /// </summary>
        /// <param name="tp">テキストポイント表す</param>
        /// <returns>インデックスを返す</returns>
        public int GetIndexFromLayoutLine(TextPoint tp)
        {
            return this.LayoutLines.GetIndexFromTextPoint(tp);
        }

        /// <summary>
        /// インデックスからレイアウト行を指し示すテキストポイントに変換する
        /// </summary>
        /// <param name="index">インデックスを表す</param>
        /// <returns>テキストポイント返す</returns>
        public TextPoint GetLayoutLineFromIndex(int index)
        {
            return this.LayoutLines.GetTextPointFromIndex(index);
        }

        /// <summary>
        /// 指定した座標までスクロールする
        /// </summary>
        /// <param name="x"></param>
        /// <param name="row"></param>
        /// <remarks>
        /// 範囲外の座標を指定した場合、範囲内に収まるように調整されます
        /// </remarks>
        public void Scroll(double x, int row)
        {
            if (x < 0)
                x = 0;
            if(row < 0)
                row = 0;
            int endRow = this.LayoutLines.Count - 1 - this.LineCountOnScreen;
            if (endRow < 0)
                endRow = 0;
            base.TryScroll(x, row);
        }

        /// <summary>
        /// 指定行までスクロールする
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="alignTop">指定行を画面上に置くなら真。そうでないなら偽</param>
        public void ScrollIntoView(int row, bool alignTop)
        {
            this.Scroll(0, row);
            if (alignTop)
                return;
            bool result;
            Point pos;
            (result, pos) = this.AdjustSrc(new TextPoint(row, 0), AdjustFlow.Both);
        }

        /// <summary>
        /// 折り畳みを考慮して行を調整します
        /// </summary>
        /// <param name="row">調整前の行</param>
        /// <param name="isMoveNext">移動方向</param>
        /// <returns>調整後の行</returns>
        public int AdjustRow(int row, bool isMoveNext)
        {
            if (this.LayoutLines.FoldingStrategy == null)
                return row;
            int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(row);
            int lineLength = this.LayoutLines.GetLengthFromLineNumber(row);
            FoldingItem foldingData = this.LayoutLines.FoldingCollection.GetFarestHiddenFoldingData(lineHeadIndex, lineLength);
            if (foldingData != null && !foldingData.Expand)
            {
                if (foldingData.End == this.Document.Length)
                    return row;
                if (isMoveNext && lineHeadIndex > foldingData.Start)
                    row = this.LayoutLines.GetLineNumberFromIndex(foldingData.End) + 1;
                else
                    row = this.LayoutLines.GetLineNumberFromIndex(foldingData.Start);
                if(row > this.LayoutLines.Count - 1)
                    row = this.LayoutLines.GetLineNumberFromIndex(foldingData.Start);
            }
            return row;
        }
        protected override void OnRenderChanged(EventArgs e)
        {
            base.OnRenderChanged(e);
            if (this.render == null)
                this.CaretLocation = new Point(0, 0);
            else
                this.CaretLocation = new Point(this.render.TextArea.X, this.render.TextArea.Y);
        }

        protected override void CalculateClipRect()
        {
            IEditorRender render = (IEditorRender)base.render;
            double x, y, width, height;

            if (this.Document.DrawLineNumber)
            {
                if (this.Document.RightToLeft)
                    x = this.Padding.Left;
                else
                    x = this.Padding.Left + UpdateAreaTotalWidth + this.render.LineNemberWidth + this.LineNumberMargin + render.FoldingWidth;
                width = this.PageBound.Width - this.render.LineNemberWidth - this.LineNumberMargin - this.Padding.Left - this.Padding.Right - render.FoldingWidth - UpdateAreaTotalWidth;
            }
            else
            {
                double foldingWidth = this.render != null ? render.FoldingWidth : 0;
                if (this.Document.RightToLeft)
                    x = this.Padding.Left;
                else
                    x = this.Padding.Left + UpdateAreaTotalWidth + foldingWidth;
                width = this.PageBound.Width - this.Padding.Left - this.Padding.Right - foldingWidth - UpdateAreaTotalWidth;
            }

            y = this.Padding.Top;
            height = this.PageBound.Height - this.Padding.Top - this.Padding.Bottom;

            if (this.Document.HideRuler == false)
            {
                double rulerHeight = this.render.emSize.Height * this.render.LineEmHeight + LineMarkerThickness;
                y += rulerHeight;
                height -= rulerHeight;
            }

            if (width < 0)
                width = 0;

            if (height < 0)
                height = 0;

            if(this.render != null)
                this.render.TextArea = new Rectangle(x, y, width, height);

            this.LineBreakingMarginWidth = width * 5 / 100;
        }

        public override void CalculateLineCountOnScreen()
        {
            if (this.LayoutLines.Count == 0 || this.PageBound.Height == 0)
                return;

            double y = 0;
            int i = this.Src.Row;
            int visualCount = this.Src.Row;
            for (; true; i++)
            {
                int row = i < this.LayoutLines.Count ? i : this.LayoutLines.Count - 1;

                int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(row);
                int lineLength = this.LayoutLines.GetLengthFromLineNumber(row);

                LineToIndexTableData lineData = null;
                this.LayoutLines.FetchLine(row);
                if (this.LayoutLines.FoldingCollection.IsHidden(lineHeadIndex) && this.LayoutLines.TryGetRaw(row,out lineData))
                    continue;

                ITextLayout layout = this.LayoutLines.GetLayout(row);

                double width = layout.Width;

                if (width > this._LongestWidth)
                    this._LongestWidth = width;

                double lineHeight = layout.Height;

                y += lineHeight;

                if (y >= this.render.TextArea.Height)
                    break;
                visualCount++;
            }
            this.LineCountOnScreen = Math.Max(visualCount - this.Src.Row - 1, 0);
            this.LineCountOnScreenWithInVisible = Math.Max(i - this.Src.Row - 1, 0);
        }

        void SetCaretPostion(double x, double y)
        {
            this.CaretLocation = new Point(x + this.PageBound.X, y + this.PageBound.Y);
        }

        void FoldingCollection_StatusChanged(object sender, FoldingItemStatusChangedEventArgs e)
        {
            this.CalculateLineCountOnScreen();
        }

        enum AreaType
        {
            UpdateArea,
            FoldingArea,
            LineNumberArea,
            TextArea
        }

        double GetRealtiveX(AreaType type)
        {
            IEditorRender render = (IEditorRender)base.render;
            switch (type)
            {
                case AreaType.UpdateArea:
                    if (this.Document.RightToLeft)
                        return this.PageBound.TopRight.X - UpdateAreaTotalWidth;
                    if (this.Document.DrawLineNumber)
                        return this.render.TextArea.X - this.render.LineNemberWidth - this.LineNumberMargin - render.FoldingWidth - UpdateAreaTotalWidth;
                    else
                        return this.render.TextArea.X - render.FoldingWidth - UpdateAreaTotalWidth;
                case AreaType.FoldingArea:
                    if (this.Document.RightToLeft)
                        return this.PageBound.TopRight.X - render.FoldingWidth;
                    if (this.Document.DrawLineNumber)
                        return this.render.TextArea.X - this.render.LineNemberWidth - this.LineNumberMargin - render.FoldingWidth;
                    else
                        return this.render.TextArea.X - render.FoldingWidth;
                case AreaType.LineNumberArea:
                    if (this.Document.DrawLineNumber == false)
                        throw new InvalidOperationException();
                    if (this.Document.RightToLeft)
                        return this.PageBound.TopRight.X - UpdateAreaTotalWidth - render.FoldingWidth - this.render.LineNemberWidth;
                    else
                        return this.render.TextArea.X - this.render.LineNemberWidth - this.LineNumberMargin;
                case AreaType.TextArea:
                    return this.render.TextArea.X;
            }
            throw new ArgumentOutOfRangeException();
        }
    }
}
