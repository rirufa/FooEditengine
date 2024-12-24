/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
#if WINFORM
using System.Drawing;
#endif

namespace FooEditEngine
{
    /// <summary>
    /// 移動量の単位を表す
    /// </summary>
    public enum MoveFlow
    {
        /// <summary>
        /// 文字
        /// </summary>
        Character,
        /// <summary>
        /// 単語単位
        /// </summary>
        Word,
        /// <summary>
        /// 行単位
        /// </summary>
        Line,
        /// <summary>
        /// パラグラフ単位
        /// </summary>
        Paragraph
    }
    internal enum ScrollDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// インデントの方法を表す
    /// </summary>
    public enum IndentMode
    {
        /// <summary>
        /// タブ
        /// </summary>
        Tab,
        /// <summary>
        /// スペース
        /// </summary>
        Space,
    }

    /// <summary>
    /// ユーザー側からの処理を担当するクラス。一部を除き、こちらで行われた操作はアンドゥの対象になります
    /// </summary>
    internal sealed class Controller
    {
        EditView View;
        Document _Document;
        
        public Controller(Document doc, EditView view)
        {
            this.Document = doc;
            this.View = view;
            this.View.PageBoundChanged += View_PageBoundChanged;
            //this.Document.Clear();
        }

        public Document Document
        {
            get
            {
                return this._Document;
            }
            set
            {
                //メモリリークを防ぐためにパンドラーを除く
                if (this._Document != null)
                {
                    this._Document.Update -= Document_Update;
                    this._Document.StatusUpdate -= Document_StatusChanged;
                    this._Document.SelectionChanged -= Document_SelectionChanged;
                    this._Document.PerformLayouted -= View_LineBreakChanged;
                    this._Document.CaretChanged -= _Document_CaretChanged;
                }

                this._Document = value;

                this._Document.Update += new DocumentUpdateEventHandler(Document_Update);
                this._Document.StatusUpdate += Document_StatusChanged;
                this._Document.SelectionChanged += Document_SelectionChanged;
                this._Document.PerformLayouted += View_LineBreakChanged;
                this._Document.CaretChanged += _Document_CaretChanged;
            }
        }

        private void _Document_CaretChanged(object sender, EventArgs e)
        {
            TextPoint pos = this.Document.CaretPostion;
            this.JumpCaret(pos.row, pos.col);
        }

        private void Document_SelectionChanged(object sender, EventArgs e)
        {
            if (this.IsReverseSelect())
            {
                if (this.Document.SelectGrippers.BottomRight.Enabled)
                    this.Document.SelectGrippers.BottomRight.MoveByIndex(this.View, this.SelectionStart);
                if (this.Document.SelectGrippers.BottomLeft.Enabled)
                    this.Document.SelectGrippers.BottomLeft.MoveByIndex(this.View, this.SelectionStart + this.SelectionLength);
            }
            else
            {
                if (this.Document.SelectGrippers.BottomLeft.Enabled)
                    this.Document.SelectGrippers.BottomLeft.MoveByIndex(this.View, this.SelectionStart);
                if (this.Document.SelectGrippers.BottomRight.Enabled)
                    this.Document.SelectGrippers.BottomRight.MoveByIndex(this.View, this.SelectionStart + this.SelectionLength);
            }
        }

        void Document_StatusChanged(object sender,EventArgs e)
        {
            this.AdjustCaret();
        }

        /// <summary>
        /// 矩形選択モードなら真を返し、そうでない場合は偽を返す
        /// </summary>
        public bool RectSelection
        {
            get { return this.Document.RectSelection; }
            set { this.Document.RectSelection = value; }
        }

        /// <summary>
        /// インデントの方法を表す
        /// </summary>
        public IndentMode IndentMode
        {
            get { return this.Document.IndentMode; }
            set { this.Document.IndentMode = value; }
        }

        /// <summary>
        /// 選択範囲の開始位置
        /// </summary>
        /// <remarks>SelectionLengthが0の場合、キャレット位置を表します</remarks>
        public int SelectionStart
        {
            get
            {
                if (this.View.Selections.Count == 0)
                    return this.Document.AnchorIndex;
                else
                    return this.View.Selections.First().start;
            }
        }

        /// <summary>
        /// 選択範囲の長さ
        /// </summary>
        /// <remarks>矩形選択モードの場合、選択範囲の文字数ではなく、開始位置から終了位置までの長さとなります</remarks>
        public int SelectionLength
        {
            get
            {
                if (this.View.Selections.Count == 0)
                    return 0;
                Selection last = this.View.Selections.Last();
                return last.start + last.length - this.SelectionStart;
            }
        }

        /// <summary>
        /// 選択範囲内の文字列を返す
        /// </summary>
        /// <remarks>
        /// 未選択状態で代入したときは追加され、そうでない場合は選択範囲の文字列と置き換えられます。
        /// </remarks>
        public string SelectedText
        {
            get
            {
                if (this.View.LayoutLines.Count == 0 || this.View.Selections.Count == 0)
                    return null;
                if (this.RectSelection)
                    return GetTextFromRectangleSelectArea(this.View.Selections);
                else
                    return GetTextFromLineSelectArea(this.View.Selections).Replace(Document.NewLine.ToString(), Environment.NewLine);
            }
            set
            {
                if (this.Document.FireUpdateEvent == false)
                    throw new InvalidOperationException("");
                if (value == null)
                    return;
                this.RepleaceSelectionArea(this.View.Selections, value.Replace(Environment.NewLine,Document.NewLine.ToString()));
            }
        }

        /// <summary>
        /// 選択範囲が逆転しているかどうかを判定する
        /// </summary>
        /// <returns>逆転しているなら真を返す</returns>
        public bool IsReverseSelect()
        {
            int index = this.View.LayoutLines.GetIndexFromTextPoint(this.Document.CaretPostion);
            return index < this.Document.AnchorIndex;
        }

        /// <summary>
        /// 選択範囲内のUTF32コードポイントを文字列に変換します
        /// </summary>
        /// <returns>成功した場合は真。そうでない場合は偽を返す</returns>
        public bool ConvertToChar()
        {
            if (this.SelectionLength == 0 || this.RectSelection)
                return false;
            string str = this.Document.ToString(this.SelectionStart, this.SelectionLength);
            string[] codes = str.Split(new char[] { ' ' },StringSplitOptions.RemoveEmptyEntries);
            StringBuilder result = new StringBuilder();
            foreach (string code in codes)
            {
                int utf32_code;
                if (code[0] != 'U')
                    return false;
                if (Int32.TryParse(code.TrimStart('U'),NumberStyles.HexNumber,null, out utf32_code))
                    result.Append(Char.ConvertFromUtf32(utf32_code));
                else
                    return false;
            }
            this.Document.Replace(this.SelectionStart, this.SelectionLength, result.ToString());
            return true;
        }

        /// <summary>
        /// 選択文字列をUTF32のコードポイントに変換します
        /// </summary>
        public void ConvertToCodePoint()
        {
            if (this.SelectionLength == 0 || this.RectSelection)
                return;
            string str = this.Document.ToString(this.SelectionStart, this.SelectionLength);
            StringInfo info = new StringInfo(str);
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < str.Length;)
            {
                int utf32_code = Char.ConvertToUtf32(str, i); 
                result.Append("U" + Convert.ToString(utf32_code,16));
                result.Append(' ');
                if(Char.IsHighSurrogate(str[i]))
                    i += 2;
                else
                    i++;
            }
            this.Document.Replace(this.SelectionStart, this.SelectionLength, result.ToString());
        }

        /// <summary>
        /// 選択を解除する
        /// </summary>
        public void DeSelectAll()
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            this.View.Selections.Clear();
        }

        /// <summary>
        /// 任意のマーカーかどうか
        /// </summary>
        /// <param name="tp"></param>
        /// <param name="type"></param>
        /// <returns>真ならマーカーがある</returns>
        public bool IsMarker(TextPoint tp,HilightType type)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            int index = this.View.LayoutLines.GetIndexFromTextPoint(tp);
            return this.IsMarker(index, type);
        }

        /// <summary>
        /// 任意のマーカーかどうか判定する
        /// </summary>
        /// <param name="index"></param>
        /// <param name="type"></param>
        /// <returns>真ならマーカーがある</returns>
        public bool IsMarker(int index, HilightType type)
        {
            foreach(int id in this.Document.Markers.IDs)
            {
                foreach (Marker m in this.Document.GetMarkers(id, index))
                {
                    if (m.hilight == type)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// キャレット位置を再調整する
        /// </summary>
        public void AdjustCaret()
        {
            if (this.View.render == null)
                return;
            int row = this.Document.CaretPostion.row;
            if (row > this.View.LayoutLines.Count - 1)
                row = this.View.LayoutLines.Count - 1;
            int col = this.Document.CaretPostion.col;
            if (col > 0 && col > this.View.LayoutLines[row].Length)
                col = this.View.LayoutLines[row].Length;

            //選択領域が消えてしまうので覚えておく
            int sel_start = this.SelectionStart;
            int sel_length = this.SelectionLength;

            this.JumpCaret(row, col);

            this.Document.Select(sel_start, sel_length);
        }

        /// <summary>
        /// キャレットを指定した位置に移動させる
        /// </summary>
        /// <param name="index"></param>
        /// <param name="autoExpand">折り畳みを展開するなら真</param>
        public void JumpCaret(int index,bool autoExpand = true)
        {
            if (index < 0 || index > this.Document.Length)
                throw new ArgumentOutOfRangeException("indexが設定できる範囲を超えています");
            TextPoint tp = this.View.GetLayoutLineFromIndex(index);

            this.JumpCaret(tp.row, tp.col,autoExpand);
         }

        /// <summary>
        /// キャレットを指定した位置に移動させる
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="autoExpand">折り畳みを展開するなら真</param>
        public void JumpCaret(int row, int col, bool autoExpand = true)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            this.Document.SetCaretPostionWithoutEvent(row, col,autoExpand);

            this.View.AdjustCaretAndSrc();

            this.SelectWithMoveCaret(false);
        }

        /// <summary>
        /// 行の先頭に移動する
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="isSelected">選択状態にするかどうか</param>
        public void JumpToLineHead(int row,bool isSelected)
        {
            this.Document.SetCaretPostionWithoutEvent(row, 0);
            this.View.AdjustCaretAndSrc();
            this.SelectWithMoveCaret(isSelected);
        }

        /// <summary>
        /// 行の終わりに移動する
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="isSelected">選択状態にするかどうか</param>
        public void JumpToLineEnd(int row, bool isSelected)
        {
            this.Document.SetCaretPostionWithoutEvent(row, this.View.LayoutLines[row].Length - 1);
            this.View.AdjustCaretAndSrc();
            this.SelectWithMoveCaret(isSelected);
        }

        /// <summary>
        /// ドキュメントの先頭に移動する
        /// </summary>
        /// <param name="isSelected"></param>
        public void JumpToHead(bool isSelected)
        {
            if (this.View.TryScroll(0, 0))
                return;
            this.Document.SetCaretPostionWithoutEvent(0, 0);
            this.View.AdjustCaretAndSrc();
            this.SelectWithMoveCaret(isSelected);
        }

        /// <summary>
        /// ドキュメントの終わりにに移動する
        /// </summary>
        /// <param name="isSelected"></param>
        public void JumpToEnd(bool isSelected)
        {
            int srcRow = this.View.LayoutLines.Count - this.View.LineCountOnScreen - 1;
            if(srcRow < 0)
                srcRow = 0;
            if (this.View.TryScroll(0, srcRow))
                return;
            this.Document.SetCaretPostionWithoutEvent(this.View.LayoutLines.Count - 1, 0);
            this.View.AdjustCaretAndSrc();
            this.SelectWithMoveCaret(isSelected);
        }

        /// <summary>
        /// スクロールする
        /// </summary>
        /// <param name="dir">方向を指定する</param>
        /// <param name="delta">ピクセル単位の値でスクロール量を指定する</param>
        /// <param name="isSelected">選択状態にするなら真</param>
        /// <param name="withCaret">同時にキャレットを移動させるなら真</param>
        double totalDelta = 0;
        public void ScrollByPixel(ScrollDirection dir,double delta, bool isSelected, bool withCaret)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            if (dir == ScrollDirection.Left || dir == ScrollDirection.Right)
            {
                this.Scroll(dir ,(int)delta, isSelected, withCaret);
                return;
            }

            totalDelta += delta;
            if (totalDelta > this.View.ScrollNoti)
            {
                double lineHeight = this.View.render.emSize.Height * this.View.render.LineEmHeight;
                int numRow = (int)(totalDelta / lineHeight) ;
                this.Scroll(dir, numRow, isSelected, withCaret);
                totalDelta = 0;
            }
        }

        /// <summary>
        /// スクロールする
        /// </summary>
        /// <param name="dir">方向を指定する</param>
        /// <param name="delta">スクロールする量。ScrollDirectionの値がUpやDownなら行数。LeftやRightならピクセル単位の値となる</param>
        /// <param name="isSelected">選択状態にするなら真</param>
        /// <param name="withCaret">同時にキャレットを移動させるなら真</param>
        public void Scroll(ScrollDirection dir, int delta, bool isSelected,bool withCaret)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            int toRow = this.View.Src.Row;
            double toX = this.View.Src.X;
            switch (dir)
            {
                case ScrollDirection.Up:
                    toRow = Math.Max(0, this.View.Src.Row - delta);
                    toRow = this.View.AdjustRow(toRow, false);
                    break;
                case ScrollDirection.Down:
                    toRow = Math.Min(this.View.Src.Row + delta, this.View.LayoutLines.Count - 1);
                    toRow = this.View.AdjustRow(toRow, true);
                    break;
                case ScrollDirection.Left:
                    toX -= delta;
                    break;
                case ScrollDirection.Right:
                    toX += delta;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            this.Scroll(toX, toRow, isSelected, withCaret);
        }

        /// <summary>
        /// スクロールする
        /// </summary>
        /// <param name="toX">スクロール先の座標</param>
        /// <param name="toRow">スクロール先の行</param>
        /// <param name="isSelected">選択状態にするなら真</param>
        /// <param name="withCaret">同時にキャレットを移動させるなら真</param>
        public void Scroll(double toX, int toRow, bool isSelected, bool withCaret)
        {
            if (withCaret)
            {
                this.View.Scroll(toX, toRow);
                this.Document.SetCaretPostionWithoutEvent(toRow, 0);
                this.View.AdjustCaretAndSrc();
                this.SelectWithMoveCaret(isSelected);
            }
            else
            {
                this.View.HideCaret = true;
                this.View.Scroll(toX, toRow);
            }

            this.Document.SelectGrippers.BottomLeft.MoveByIndex(this.View, this.SelectionStart);
            this.Document.SelectGrippers.BottomRight.MoveByIndex(this.View, this.SelectionStart + this.SelectionLength);
        }

        /// <summary>
        /// キャレットを桁方向に移動させる
        /// </summary>
        /// <returns>移動できない場合は真を返す</returns>
        /// <param name="realLength">負の値なら左側へ、そうでないなら右側へ移動する</param>
        /// <param name="isSelected">選択範囲とするなら真。そうでないなら偽</param>
        /// <param name="alignWord">単語単位で移動するなら真。そうでないなら偽</param>
        public void MoveCaretHorizontical(int realLength, bool isSelected,bool alignWord = false)
        {
            TextPoint caret = this.Document.CaretPostion;
            int moved;
            caret = GetNextCaret(caret, realLength, alignWord ? MoveFlow.Word : MoveFlow.Character,out moved);
            this.Document.SetCaretPostionWithoutEvent(caret.row, caret.col, false);
            this.View.AdjustCaretAndSrc(AdjustFlow.Both);
            this.SelectWithMoveCaret(isSelected);
        }

        /// <summary>
        /// 移動後のキャレット位置を求める
        /// </summary>
        /// <param name="caret">起点となるキャレット位置</param>
        /// <param name="count">移動量</param>
        /// <param name="method">移動方法</param>
        /// <param name="moved">実際に移動した量</param>
        /// <returns>移動後のキャレット位置</returns>
        public TextPoint GetNextCaret(TextPoint caret, int count,MoveFlow method,out int moved)
        {
            moved = 0;
            if(method == MoveFlow.Character || method == MoveFlow.Word)
            {
                for (int i = Math.Abs(count); i > 0; i--)
                {
                    bool moveFlow = count > 0;
                    if (this.Document.RightToLeft)
                        moveFlow = !moveFlow;
                    caret = this.MoveCaretHorizontical(caret, moveFlow);

                    if (method == FooEditEngine.MoveFlow.Word)
                        caret = this.AlignNearestWord(caret, moveFlow);
                    moved++;
                }
            }
            if(method == MoveFlow.Line || method == MoveFlow.Paragraph)
            {
                for (int i = Math.Abs(count); i > 0; i--)
                {
                    caret = this.MoveCaretVertical(caret, count > 0, method == MoveFlow.Paragraph);
                    moved++;
                }
            }
            if (count < 0)
                moved = -moved;
            return caret;
        }

        TextPoint AlignNearestWord(TextPoint caret,bool MoveFlow)
        {
            string str = this.View.LayoutLines[caret.row];
            while (caret.col > 0 &&
                caret.col < str.Length &&
                str[caret.col] != Document.NewLine)
            {
                if (!Util.IsWordSeparator(str[caret.col]))
                {
                    caret = this.MoveCaretHorizontical(caret, MoveFlow);
                }
                else
                {
                    if(MoveFlow)
                        caret = this.MoveCaretHorizontical(caret, MoveFlow);
                    break;
                }
            }
            return caret;
        }

        /// <summary>
        /// キャレットを行方向に移動させる
        /// </summary>
        /// <returns>再描写する必要があるなら真を返す</returns>
        /// <param name="deltarow">移動量</param>
        /// <param name="isSelected"></param>
        public void MoveCaretVertical(int deltarow,bool isSelected)
        {
            TextPoint caret = this.Document.CaretPostion;
            int moved;
            caret = this.GetNextCaret(caret, deltarow, MoveFlow.Line,out moved);
            this.Document.SetCaretPostionWithoutEvent(caret.row, caret.col, true);
            this.View.AdjustCaretAndSrc(AdjustFlow.Both);
            this.SelectWithMoveCaret(isSelected);
        }

        /// <summary>
        /// キャレット位置の文字を一文字削除する
        /// </summary>
        public void DoDeleteAction()
        {
            if (this.SelectionLength != 0)
            {
                this.SelectedText = "";
                return;
            }
            
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            TextPoint CaretPostion = this.Document.CaretPostion;
            int index = this.View.GetIndexFromLayoutLine(CaretPostion);

            if (index == this.Document.Length)
                return;

            int lineHeadIndex = this.View.LayoutLines.GetIndexFromLineNumber(CaretPostion.row);
            int next = this.View.LayoutLines.GetLayout(CaretPostion.row).AlignIndexToNearestCluster(CaretPostion.col, AlignDirection.Forward) + lineHeadIndex;

            if (this.Document[index] == Document.NewLine)
                next = index + 1;

            this.Document.Replace(index, next - index, "", true);
        }

        public bool IsRectInsertMode()
        {
            if (!this.RectSelection || this.View.Selections.Count == 0)
                return false;
            foreach(Selection sel in this.View.Selections)
            {
                if (sel.length != 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// キャレット位置の文字を一文字削除し、キャレット位置を後ろにずらす
        /// </summary>
        public void DoBackSpaceAction()
        {
            if (this.IsRectInsertMode())
            {
                this.ReplaceBeforeSelectionArea(this.View.Selections, 1, "");
                return;
            }
            else if (this.SelectionLength > 0)
            {
                this.SelectedText = "";
                return;
            }

            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            TextPoint CurrentPostion = this.Document.CaretPostion;

            if (CurrentPostion.row == 0 && CurrentPostion.col == 0)
                return;

            int oldIndex = this.View.GetIndexFromLayoutLine(CurrentPostion);

            int newCol, newIndex;
            if (CurrentPostion.col > 0)
            {
                newCol = this.View.LayoutLines.GetLayout(CurrentPostion.row).AlignIndexToNearestCluster(CurrentPostion.col - 1, AlignDirection.Back);
                newIndex = this.View.GetIndexFromLayoutLine(new TextPoint(CurrentPostion.row, newCol));
            }
            else
            {
                newIndex = this.View.GetIndexFromLayoutLine(CurrentPostion);
                newIndex--;
            }

            this.Document.Replace(newIndex, oldIndex - newIndex, "", true);
        }

        /// <summary>
        /// キャレット位置で行を分割する
        /// </summary>
        public void DoEnterAction()
        {            
            this.DoInputChar('\n');
        }

        /// <summary>
        /// キャレット位置に文字を入力し、その分だけキャレットを進める。isInsertModeの値により動作が変わります
        /// </summary>
        /// <param name="ch"></param>
        public void DoInputChar(char ch)
        {
            this.DoInputString(ch.ToString());
        }

        string GetIndentSpace(int col_index)
        {
            int space_count = this.Document.TabStops - (col_index % this.Document.TabStops);
            return new string(Enumerable.Repeat(' ',space_count).ToArray());
        }

        /// <summary>
        /// キャレット位置に文字列を挿入し、その分だけキャレットを進める。isInsertModeの値により動作が変わります
        /// </summary>
        /// <param name="str">挿入したい文字列</param>
        /// <param name="fromTip">真の場合、矩形選択の幅にかかわらず矩形編集モードとして動作します。そうでない場合は選択領域を文字列で置き換えます</param>
        public void DoInputString(string str,bool fromTip = false)
        {
            TextPoint CaretPos = this.Document.CaretPostion;

            if (str == "\t" && this.IndentMode == IndentMode.Space)
                str = this.GetIndentSpace(CaretPos.col);

            if (this.IsRectInsertMode())
            {
                this.ReplaceBeforeSelectionArea(this.View.Selections, 0, str);
                return;
            }
            else if (this.SelectionLength != 0)
            {
                this.RepleaceSelectionArea(this.View.Selections, str, fromTip);
                return;
            }

            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            int index = this.View.GetIndexFromLayoutLine(this.Document.CaretPostion);
            int length = 0;
            if (this.View.InsertMode == false && index < this.Document.Length && this.Document[index] != Document.NewLine)
            {
                string lineString = this.View.LayoutLines[CaretPos.row];
                int end = this.View.LayoutLines.GetLayout(CaretPos.row).AlignIndexToNearestCluster(CaretPos.col + str.Length - 1, AlignDirection.Forward);
                if (end > lineString.Length - 1)
                    end = lineString.Length - 1;
                end += this.View.LayoutLines.GetIndexFromLineNumber(CaretPos.row);
                length = end - index;
            }
            if (str == Document.NewLine.ToString())
            {
                int lineHeadIndex = this.View.LayoutLines.GetIndexFromLineNumber(CaretPos.row);
                int lineLength = this.View.LayoutLines.GetLengthFromLineNumber(CaretPos.row);
                FoldingItem foldingData = this.View.LayoutLines.FoldingCollection.GetFarestHiddenFoldingData(lineHeadIndex, lineLength);
                if (foldingData != null && !foldingData.Expand && index > foldingData.Start && index <= foldingData.End)
                    index = foldingData.End + 1;
            }
            this.Document.Replace(index, length, str, true);
        }

        /// <summary>
        /// キャレットの移動に合わせて選択する
        /// </summary>
        /// <param name="isSelected">選択状態にするかどうか</param>
        /// <remarks>
        /// キャレットを移動後、このメソッドを呼び出さない場合、Select()メソッドは正常に機能しません
        /// </remarks>
        void SelectWithMoveCaret(bool isSelected)
        {
            if (this.Document.CaretPostion.col < 0 || this.Document.CaretPostion.row < 0)
                return;

            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            int CaretPostion = this.View.GetIndexFromLayoutLine(this.Document.CaretPostion);
            
            SelectCollection Selections = this.View.Selections;
            if (isSelected)
            {
                this.Document.Select(this.Document.AnchorIndex, CaretPostion - this.Document.AnchorIndex);
            }else{
                this.Document.AnchorIndex = CaretPostion;
                this.Document.Select(CaretPostion, 0);
            }
        }

        //拡大時の変化量の累積。絶対値で格納される。
        double totalScaleDelta = 0;

        /// <summary>
        /// 拡大する
        /// </summary>
        /// <param name="scale"></param>
        public bool Scale(double Scale,Action<double> scaleProcessFunc)
        {
            if (Scale < 1)
            {
                if (totalScaleDelta > this.View.ScaleNoti)
                {
                    scaleProcessFunc(Scale);
                    totalScaleDelta = 0;
                    return true;
                }
                totalScaleDelta += Math.Abs(Scale);
            }

            if (Scale > 1)
            {
                System.Diagnostics.Debug.WriteLine("scale:" + totalScaleDelta);
                if (totalScaleDelta > this.View.ScaleNoti)
                {
                    scaleProcessFunc(Scale);
                    totalScaleDelta = 0;
                    return true;
                }
                totalScaleDelta += Math.Abs(Scale);
            }

            if(Scale == 1)
            {
                totalScaleDelta = 0;
            }

            return false;
        }

        /// <summary>
        /// JumpCaretで移動した位置からキャレットを移動し、選択状態にする
        /// </summary>
        /// <param name="tp"></param>
        /// <param name="alignWord">単語単位で選択するかどうか</param>
        public void MoveCaretAndSelect(TextPoint tp,bool alignWord = false)
        {
            TextPoint endSelectPostion = tp;
            int CaretPostion = this.View.GetIndexFromLayoutLine(tp);
            if (alignWord)
            {
                if (this.IsReverseSelect())
                    while (CaretPostion >= 0 && CaretPostion < this.Document.Length && !Util.IsWordSeparator(this.Document[CaretPostion])) CaretPostion--;
                else
                    while (CaretPostion < this.Document.Length && !Util.IsWordSeparator(this.Document[CaretPostion])) CaretPostion++;
                if (CaretPostion < 0)
                    CaretPostion = 0;
                endSelectPostion = this.View.LayoutLines.GetTextPointFromIndex(CaretPostion);
            }
            this.Document.Select(this.Document.AnchorIndex, CaretPostion - this.Document.AnchorIndex);
            this.Document.SetCaretPostionWithoutEvent(endSelectPostion.row, endSelectPostion.col);
            this.View.AdjustCaretAndSrc();
        }

        /// <summary>
        /// グリッパーとキャレットを同時に移動する
        /// </summary>
        /// <param name="p">ポインターの座標</param>
        /// <param name="hittedGripper">動かす対象となるグリッパー</param>
        /// <returns>移動できた場合は真を返す。そうでなければ偽を返す</returns>
        /// <remarks>グリッパー内にポインターが存在しない場合、グリッパーはポインターの座標近くの行に移動する</remarks>
        public bool MoveCaretAndGripper(Point p, Gripper hittedGripper)
        {
            bool HittedCaret = false;
            TextPoint tp = this.View.GetTextPointFromPostion(p);
            if (tp == this.Document.CaretPostion)
            {
                HittedCaret = true;
            }

            if (HittedCaret || hittedGripper != null)
            {
                TextPointSearchRange searchRange;
                if (this.View.HitTextArea(p.X, p.Y))
                    searchRange = TextPointSearchRange.TextAreaOnly;
                else if (this.SelectionLength > 0)
                    searchRange = TextPointSearchRange.Full;
                else
                    return false;

                if (hittedGripper != null)
                {
                    tp = this.View.GetTextPointFromPostion(hittedGripper.AdjustPoint(p), searchRange);
                    if (tp == TextPoint.Null)
                        return false;
                    if (Object.ReferenceEquals(hittedGripper, this.Document.SelectGrippers.BottomRight))
                        this.MoveCaretAndSelect(tp);
                    else if(Object.ReferenceEquals(hittedGripper, this.Document.SelectGrippers.BottomLeft))
                        this.MoveSelectBefore(tp);
                }
                else
                {
                    tp = this.View.GetTextPointFromPostion(p, searchRange);
                    if (tp != TextPoint.Null)
                    {
                        this.MoveCaretAndSelect(tp);
                    }
                    else
                    {
                        return false;
                    }
                }
                this.Document.SelectGrippers.BottomLeft.Enabled = this.SelectionLength != 0;
                return true;
            }
            return false;
        }

        void MoveSelectBefore(TextPoint tp)
        {
            int NewAnchorIndex;
            int SelectionLength;
            if (this.IsReverseSelect())
            {
                NewAnchorIndex = this.View.GetIndexFromLayoutLine(tp);
                SelectionLength = this.SelectionLength + NewAnchorIndex - this.Document.AnchorIndex;
                this.Document.Select(this.SelectionStart, SelectionLength);
            }
            else
            {
                NewAnchorIndex = this.View.GetIndexFromLayoutLine(tp);
                SelectionLength = this.SelectionLength + this.Document.AnchorIndex - NewAnchorIndex;
                this.Document.Select(NewAnchorIndex, SelectionLength);
            }
            this.Document.AnchorIndex = NewAnchorIndex;
        }

        /// <summary>
        /// 行単位で移動後のキャレット位置を取得する
        /// </summary>
        /// <param name="count">移動量</param>
        /// <param name="current">現在のキャレット位置</param>
        /// <param name="move_pargraph">パラグラフ単位で移動するなら真</param>
        /// <returns>移動後のキャレット位置</returns>
        TextPoint GetTextPointAfterMoveLine(int count, TextPoint current, bool move_pargraph = false)
        {
            if(this.Document.LineBreak == LineBreakMethod.None || move_pargraph == true)
            {
                int row = current.row + count;

                this.Document.LayoutLines.FetchLine(row);

                if (row < 0)
                    row = 0;
                else if (row >= this.View.LayoutLines.Count)
                    row = this.View.LayoutLines.Count - 1;

                row = this.View.AdjustRow(row, count > 0);

                Point pos = this.View.LayoutLines.GetLayout(current.row).GetPostionFromIndex(current.col);
                int col = this.View.LayoutLines.GetLayout(row).GetIndexFromPostion(pos.X, pos.Y);
                return new TextPoint(row, col);
            }
            else
            {
                Point pos = this.View.GetPostionFromTextPoint(current);
                pos.Y += this.View.render.emSize.Height * count;
                //この値を足さないとうまく動作しない
                pos.Y += this.View.render.emSize.Height / 2;   
                var new_tp = this.View.GetTextPointFromPostion(pos,TextPointSearchRange.Full);
                if (new_tp == TextPoint.Null)
                    return current;
                return new_tp;

            }
        }

        /// <summary>
        /// 選択文字列のインデントを一つ増やす
        /// </summary>
        public void UpIndent()
        {
            if (this.RectSelection || this.SelectionLength == 0)
                return;
            int selectionStart = this.SelectionStart;
            string insertStr = this.IndentMode == IndentMode.Space ? this.GetIndentSpace(0) : "\t";
            string text = this.InsertLineHead(GetTextFromLineSelectArea(this.View.Selections), insertStr);
            this.RepleaceSelectionArea(this.View.Selections,text);
            this.Document.Select(selectionStart, text.Length);
        }

        /// <summary>
        /// 選択文字列のインデントを一つ減らす
        /// </summary>
        public void DownIndent()
        {
            if (this.RectSelection || this.SelectionLength == 0)
                return;
            int selectionStart = this.SelectionStart;
            string insertStr = this.IndentMode == IndentMode.Space ? this.GetIndentSpace(0) : "\t";
            string text = this.RemoveLineHead(GetTextFromLineSelectArea(this.View.Selections), insertStr, insertStr.Length);
            this.RepleaceSelectionArea(this.View.Selections, text);
            this.Document.Select(selectionStart, text.Length);
        }

        string InsertLineHead(string s, string str)
        {
            string[] lines = s.Split(new string[] { Document.NewLine.ToString() }, StringSplitOptions.None);
            StringBuilder output = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if(lines[i].Length > 0)
                    output.Append(str + lines[i] + Document.NewLine);
                else if(i < lines.Length - 1)
                    output.Append(lines[i] + Document.NewLine);
            }
            return output.ToString();
        }

        public string RemoveLineHead(string s, string str,int remove_count)
        {
            string[] lines = s.Split(new string[] { Document.NewLine.ToString() }, StringSplitOptions.None);
            StringBuilder output = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(str))
                    output.Append(lines[i].Substring(remove_count) + Document.NewLine);
                else if (i < lines.Length - 1)
                    output.Append(lines[i] + Document.NewLine);
            }
            return output.ToString();
        }

        /// <summary>
        /// キャレットを一文字移動させる
        /// </summary>
        /// <param name="caret">キャレット</param>
        /// <param name="isMoveNext">真なら１文字すすめ、そうでなければ戻す</param>
        /// <remarks>このメソッドを呼び出した後でScrollToCaretメソッドとSelectWithMoveCaretメソッドを呼び出す必要があります</remarks>
        TextPoint MoveCaretHorizontical(TextPoint caret,bool isMoveNext)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            int delta = isMoveNext ? 0 : -1;
            int prevcol = caret.col;
            int col = caret.col + delta;
            string lineString = this.View.LayoutLines[caret.row];
            if (col < 0 || caret.row >= this.View.LayoutLines.Count)
            {
                if (caret.row == 0)
                {
                    caret.col = 0;
                    return caret;
                }
                caret = this.MoveCaretVertical(caret,false);
                caret.col = this.View.LayoutLines.GetLengthFromLineNumber(caret.row) - 1;  //最終行以外はすべて改行コードが付くはず
            }
            else if (col >= lineString.Length || lineString[col] == Document.NewLine)
            {
                if (caret.row < this.View.LayoutLines.Count - 1)
                {
                    caret = this.MoveCaretVertical(caret, true);
                    caret.col = 0;
                }
            }
            else
            {
                AlignDirection direction = isMoveNext ? AlignDirection.Forward : AlignDirection.Back;
                caret.col = this.View.LayoutLines.GetLayout(caret.row).AlignIndexToNearestCluster(col, direction);
            }
            return caret;
        }

        /// <summary>
        /// キャレットを行方向に移動させる
        /// </summary>
        /// <param name="caret">計算の起点となるテキストポイント</param>
        /// <param name="isMoveNext">プラス方向に移動するなら真</param>
        /// <param name="move_pargraph">パラグラフ単位で移動するするなら真</param>
        /// <remarks>このメソッドを呼び出した後でScrollToCaretメソッドとSelectWithMoveCaretメソッドを呼び出す必要があります</remarks>
        TextPoint MoveCaretVertical(TextPoint caret,bool isMoveNext, bool move_pargraph = false)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            return this.GetTextPointAfterMoveLine(isMoveNext ? 1 : -1, this.Document.CaretPostion, move_pargraph);
        }

        private void ReplaceBeforeSelectionArea(SelectCollection Selections, int removeLength, string insertStr)
        {
            if (removeLength == 0 && insertStr.Length == 0)
                return;

            if (this.RectSelection == false || this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException();

            SelectCollection temp = this.View.Selections;
            int selectStart = temp.First().start;
            int selectEnd = temp.Last().start + temp.Last().length;

            //ドキュメント操作後に行うとうまくいかないので、あらかじめ取得しておく
            TextPoint start = this.View.LayoutLines.GetTextPointFromIndex(selectStart);
            TextPoint end = this.View.LayoutLines.GetTextPointFromIndex(selectEnd);

            bool reverse = temp.First().start > temp.Last().start;

            int lineHeadIndex = this.View.LayoutLines.GetIndexFromLineNumber(this.View.LayoutLines.GetLineNumberFromIndex(selectStart));
            if (selectStart - removeLength < lineHeadIndex)
                return;

            this.Document.UndoManager.BeginUndoGroup();
            this.Document.FireUpdateEvent = false;

            if (reverse)
            {
                for (int i = 0; i < temp.Count; i++)
                {
                    this.ReplaceBeforeSelection(temp[i], removeLength, insertStr);
                }
            }
            else
            {
                for (int i = temp.Count - 1; i >= 0; i--)
                {
                    this.ReplaceBeforeSelection(temp[i], removeLength, insertStr);
                }
            }

            this.Document.FireUpdateEvent = true;
            this.Document.UndoManager.EndUndoGroup();

            int delta = insertStr.Length - removeLength;
            start.col += delta;
            end.col += delta;

            if (reverse)
                this.JumpCaret(start.row, start.col);
            else
                this.JumpCaret(end.row, end.col);
            
            this.Document.Select(start, 0, end.row - start.row);
        }

        private void ReplaceBeforeSelection(Selection sel, int removeLength, string insertStr)
        {
            sel = Util.NormalizeIMaker<Selection>(sel);
            this.Document.Replace(sel.start - removeLength, removeLength, insertStr);
        }

        private void RepleaceSelectionArea(SelectCollection Selections, string value,bool updateInsertPoint = false)
        {
            if (value == null)
                return;

            if (this.RectSelection == false)
            {
                Selection sel = Selection.Create(this.Document.AnchorIndex, 0);
                if (Selections.Count > 0)
                    sel = Util.NormalizeIMaker<Selection>(this.View.Selections.First());

                this.Document.Replace(sel.start, sel.length, value);
                return;
            }

            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            int StartIndex = this.SelectionStart;

            SelectCollection newInsertPoint = new SelectCollection();

            if (this.SelectionLength == 0)
            {
                int i;

                this.Document.UndoManager.BeginUndoGroup();

                this.Document.FireUpdateEvent = false;

                string[] line = value.Split(new string[] { Document.NewLine.ToString() }, StringSplitOptions.RemoveEmptyEntries);

                TextPoint Current = this.View.GetLayoutLineFromIndex(this.SelectionStart);

                for (i = 0; i < line.Length && Current.row < this.View.LayoutLines.Count; i++, Current.row++)
                {
                    if (Current.col > this.View.LayoutLines[Current.row].Length)
                        Current.col = this.View.LayoutLines[Current.row].Length;
                    StartIndex = this.View.GetIndexFromLayoutLine(Current);
                    this.Document.Replace(StartIndex, 0, line[i]);
                    StartIndex += line[i].Length;
                }

                for (; i < line.Length; i++)
                {
                    StartIndex = this.Document.Length;
                    string str = Document.NewLine + line[i];
                    this.Document.Replace(StartIndex, 0, str);
                    StartIndex += str.Length;
                }

                this.Document.FireUpdateEvent = true;

                this.Document.UndoManager.EndUndoGroup();
            }
            else
            {
                SelectCollection temp = new SelectCollection(this.View.Selections); //コピーしないとReplaceCommandを呼び出した段階で書き換えられてしまう

                this.Document.UndoManager.BeginUndoGroup();

                this.Document.FireUpdateEvent = false;

                if (temp.First().start < temp.Last().start)
                {
                    for (int i = temp.Count - 1; i >= 0; i--)
                    {
                        Selection sel = Util.NormalizeIMaker<Selection>(temp[i]);

                        StartIndex = sel.start;

                        this.Document.Replace(sel.start, sel.length, value);

                        newInsertPoint.Add(Selection.Create(sel.start + (value.Length - sel.length) * i,0));
                    }
                }
                else
                {
                    for (int i = 0; i < temp.Count; i++)
                    {
                        Selection sel = Util.NormalizeIMaker<Selection>(temp[i]);

                        StartIndex = sel.start;

                        this.Document.Replace(sel.start, sel.length, value);

                        newInsertPoint.Add(Selection.Create(sel.start + (value.Length - sel.length) * i, 0));
                    }
                }

                this.Document.FireUpdateEvent = true;

                this.Document.UndoManager.EndUndoGroup();
            }
            this.JumpCaret(StartIndex);
            if (updateInsertPoint && newInsertPoint.Count > 0)
                this.View.Selections = newInsertPoint;
        }

        private string GetTextFromLineSelectArea(SelectCollection Selections)
        {
            Selection sel = Util.NormalizeIMaker<Selection>(Selections.First());

            string str = this.Document.ToString(sel.start, sel.length);

            return str;
        }

        string GetTextFromRectangleSelectArea(SelectCollection Selections)
        {
            StringBuilder temp = new StringBuilder();
            if (Selections.First().start < Selections.Last().start)
            {
                for (int i = 0; i < this.View.Selections.Count; i++)
                {
                    Selection sel = Util.NormalizeIMaker<Selection>(Selections[i]);

                    string str = this.Document.ToString(sel.start, sel.length);
                    if (str.IndexOf(Environment.NewLine) == -1)
                        temp.AppendLine(str);
                    else
                        temp.Append(str);
                }
            }
            else
            {
                for (int i = this.View.Selections.Count - 1; i >= 0; i--)
                {
                    Selection sel = Util.NormalizeIMaker<Selection>(Selections[i]);

                    string str = this.Document.ToString(sel.start, sel.length).Replace(Document.NewLine.ToString(), Environment.NewLine);
                    if (str.IndexOf(Environment.NewLine) == -1)
                        temp.AppendLine(str);
                    else
                        temp.Append(str);
                }
            }
            return temp.ToString();
        }

        void View_LineBreakChanged(object sender, EventArgs e)
        {
            this.DeSelectAll();
            this.AdjustCaret();
        }

        void View_PageBoundChanged(object sender, EventArgs e)
        {
            if (this.Document.LineBreak == LineBreakMethod.PageBound && this.View.PageBound.Width - this.View.LineBreakingMarginWidth > 0)
                this.Document.PerformLayout();
            this.AdjustCaret();
        }

        void Document_Update(object sender, DocumentUpdateEventArgs e)
        {
            switch (e.type)
            {
                case UpdateType.Replace:
                    this.JumpCaret(e.startIndex + e.insertLength,true);
                    break;
                case UpdateType.Clear:
                    this.JumpCaret(0,0, false);
                    break;
            }
        }
    }
}
