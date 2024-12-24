/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Text;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Slusser.Collections.Generic;

namespace FooEditEngine
{
    internal interface ITextLayout : IDisposable
    {
        /// <summary>
        /// 文字列の幅
        /// </summary>
        double Width
        {
            get;
        }

        /// <summary>
        /// 文字列の高さ
        /// </summary>
        double Height
        {
            get;
        }

        /// <summary>
        /// Disposeされているなら真を返す
        /// </summary>
        bool Disposed
        {
            get;
        }

        /// <summary>
        /// 破棄すべきなら真。そうでなければ偽
        /// </summary>
        bool Invaild
        {
            get;
        }

        /// <summary>
        /// 桁方向の座標に対応するインデックスを得る
        /// </summary>
        /// <param name="colpos">桁方向の座標</param>
        /// <returns>インデックス</returns>
        /// <remarks>行番号の幅は考慮されてないのでView以外のクラスは呼び出さないでください</remarks>
        int GetIndexFromColPostion(double colpos);

        /// <summary>
        /// インデックスに対応する文字の幅を得る
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>文字の幅</returns>
        double GetWidthFromIndex(int index);

        /// <summary>
        /// インデックスに対応する桁方向の座標を得る
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>桁方向の座標</returns>
        /// <remarks>行頭にEOFが含まれている場合、0が返ります</remarks>
        double GetColPostionFromIndex(int index);

        /// <summary>
        /// 座標に対応するインデックスを取得する
        /// </summary>
        /// <param name="x">桁方向の座標</param>
        /// <param name="y">行方向の座標</param>
        /// <returns>インデックス</returns>
        /// <remarks>行番号の幅は考慮されてないのでView以外のクラスは呼び出さないでください</remarks>
        int GetIndexFromPostion(double x, double y);

        /// <summary>
        /// インデックスに対応する座標を得る
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>行方向と桁方向の相対座標</returns>
        /// <remarks>行頭にEOFが含まれている場合、0が返ります</remarks>
        Point GetPostionFromIndex(int index);

        /// <summary>
        /// 適切な位置にインデックスを調整する
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="flow">真の場合は隣接するクラスターを指すように調整し、
        /// そうでない場合は対応するクラスターの先頭を指すように調整します</param>
        /// <returns>調整後のインデックス</returns>
        int AlignIndexToNearestCluster(int index, AlignDirection flow);
    }

    internal class SpilitStringEventArgs : EventArgs
    {
        public Document buffer;
        public int index;
        public int length;
        public int row;
        public SpilitStringEventArgs(Document buf, int index, int length,int row)
        {
            this.buffer = buf;
            this.index = index;
            this.length = length;
            this.row = row;
        }
    }

    internal struct SyntaxInfo
    {
        public TokenType type;
        public int index;
        public int length;
        public SyntaxInfo(int index, int length, TokenType type)
        {
            this.type = type;
            this.index = index;
            this.length = length;
        }
    }

    internal enum EncloserType
    {
        None,
        Begin,
        Now,
        End,
    }

    interface ILineInfoGenerator
    {
        void Update(Document doc, int startIndex, int insertLength, int removeLength);
        void Clear(LineToIndexTable lti);
        bool Generate(Document doc, LineToIndexTable lti, bool force = true);
    }

    public class LineToIndexTableData : IDisposable, IRange
    {
        public int start { get; set; }

        public int length { get; set; }

        /// <summary>
        /// マーカーの開始位置。-1を設定した場合、そのマーカーはレタリングされません。正しい先頭位置を取得するにはGetLineHeadIndex()を使用してください
        /// </summary>
        public int Index { get { return this.start; } set { this.start = value; } }

        public int Length
        {
            get { return this.length; }
            set { this.length = Length; }
        }

        /// <summary>
        /// 改行マークかEOFなら真を返す
        /// </summary>
        public bool LineEnd;
        internal SyntaxInfo[] Syntax;
        internal EncloserType EncloserType;
        internal ITextLayout Layout;
        public bool Dirty = false;

        /// <summary>
        /// コンストラクター。LineToIndexTable以外のクラスで呼び出さないでください
        /// </summary>
        internal LineToIndexTableData()
        {
        }

        /// <summary>
        /// コンストラクター。LineToIndexTable以外のクラスで呼び出さないでください
        /// </summary>
        internal LineToIndexTableData(int index, int length, bool lineend,bool dirty, SyntaxInfo[] syntax)
        {
            this.start = index;
            this.length = length;
            this.LineEnd = lineend;
            this.Syntax = syntax;
            this.EncloserType = EncloserType.None;
            this.Dirty = dirty;
        }

        public void Dispose()
        {
            if(this.Layout != null)
                this.Layout.Dispose();
        }
    }

    internal delegate IList<LineToIndexTableData> SpilitStringEventHandler(object sender, SpilitStringEventArgs e);

    internal sealed class CreateLayoutEventArgs
    {
        /// <summary>
        /// 開始インデックス
        /// </summary>
        public int Index
        {
            get;
            private set;
        }
        /// <summary>
        /// 長さ
        /// </summary>
        public int Length
        {
            get;
            private set;
        }
        /// <summary>
        /// 文字列
        /// </summary>
        public string Content
        {
            get;
            private set;
        }
        public CreateLayoutEventArgs(int index, int length,string content)
        {
            this.Index = index;
            this.Length = length;
            this.Content = content;
        }
    }

    /// <summary>
    /// 行番号とインデックスを相互変換するためのクラス
    /// </summary>
    public sealed class LineToIndexTable : RangeCollection<LineToIndexTableData>, IEnumerable<string>
    {
        const int MaxEntries = 100;
        GapBuffer<LineToIndexTableData> _Lines { get { return this.collection; } }
        Document Document;
        ITextRender render;

        const int FOLDING_INDEX = 0;
        const int SYNTAX_HIGLITHER_INDEX = 1;
        ILineInfoGenerator[] _generators = new ILineInfoGenerator[2];

        internal LineToIndexTable(Document buf)
        {
            this.Document = buf;
            this.Document.Markers.Updated += Markers_Updated;
            this._generators[FOLDING_INDEX] = new FoldingGenerator();
            this._generators[SYNTAX_HIGLITHER_INDEX] = new SyntaxHilightGenerator();
            this.WrapWidth = NONE_BREAK_LINE;
#if DEBUG && !NETFX_CORE
            if (!Debugger.IsAttached)
            {
                Guid guid = Guid.NewGuid();
                string path = string.Format("{0}\\footextbox_lti_debug_{1}.log", System.IO.Path.GetTempPath(), guid);
                //TODO: .NET core3だと使えない
                //Debug.Listeners.Add(new TextWriterTraceListener(path));
                //Debug.AutoFlush = true;
            }
#endif
        }

        void Markers_Updated(object sender, EventArgs e)
        {
            this.ClearLayoutCache();
        }

        /// <summary>
        /// ITextRenderインターフェイスのインスタンス。必ずセットすること
        /// </summary>
        internal ITextRender Render
        {
            get { return this.render; }
            set
            {
                this.render = value;
            }
        }

        internal SpilitStringEventHandler SpilitString;

        /// <summary>
        /// 折り畳み関係の情報を収めたコレクション
        /// </summary>
        public FoldingCollection FoldingCollection
        {
            get
            {
                return ((FoldingGenerator)this._generators[FOLDING_INDEX]).FoldingCollection;
            }
            private set
            {
            }
        }

        /// <summary>
        /// シンタックスハイライター
        /// </summary>
        public IHilighter Hilighter
        {
            get
            {
                return ((SyntaxHilightGenerator)this._generators[SYNTAX_HIGLITHER_INDEX]).Hilighter;
            }
            set
            {
                ((SyntaxHilightGenerator)this._generators[SYNTAX_HIGLITHER_INDEX]).Hilighter = value;
                if (value == null)
                    this._generators[FOLDING_INDEX].Clear(this);
            }
        }

        /// <summary>
        /// 折り畳み
        /// </summary>
        public IFoldingStrategy FoldingStrategy
        {
            get
            {
                return ((FoldingGenerator)this._generators[FOLDING_INDEX]).FoldingStrategy;
            }
            set
            {
                ((FoldingGenerator)this._generators[FOLDING_INDEX]).FoldingStrategy = value;
                if (value == null)
                    this._generators[FOLDING_INDEX].Clear(this);
            }
        }

        /// <summary>
        /// ピクセル単位で折り返すかどうか
        /// </summary>
        public double WrapWidth
        {
            get;
            set;
        }

        /// <summary>
        /// 行を折り返さないことを表す
        /// </summary>
        public const double NONE_BREAK_LINE = -1;

        /// <summary>
        /// 保持しているレイアウトキャッシュをクリアーする
        /// </summary>
        public void ClearLayoutCache()
        {
            foreach (LineToIndexTableData data in this._Lines)
            {
                data.Dispose();
            }
        }

        /// <summary>
        /// 保持しているレイアウトキャッシュをクリアーする
        /// </summary>
        public void ClearLayoutCache(int index,int length)
        {
            if (index >= this.Document.Length)
                return;
            int startRow = this.GetLineNumberFromIndex(index);
            int lastIndex = Math.Min(index + length - 1, this.Document.Length - 1);
            if (lastIndex < 0)
                lastIndex = 0;
            int endRow = this.GetLineNumberFromIndex(lastIndex);
            for (int i = startRow; i <= endRow; i++)
                this._Lines[i].Dispose();
        }

        /// <summary>
        /// 行番号に対応する文字列を返します
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public new string this[int n]
        {
            get
            {
                LineToIndexTableData data = this.GetRaw(n);
                string str = this.Document.ToString(this.GetLineHeadIndex(n), data.Length);

                return str;
            }
        }

        /// <summary>
        /// 更新フラグを更新しないなら真
        /// </summary>
        public bool IsFrozneDirtyFlag
        {
            get;
            set;
        }

        internal void UpdateLineAsReplace(int row,int removedLength, int insertedLength)
        {
            if (row >=  this._Lines.Count)
                return;

            int deltaLength = insertedLength - removedLength;

            this._Lines[row] = new LineToIndexTableData(this.GetLineHeadIndex(row), this.GetLengthFromLineNumber(row) + deltaLength, true, true, null);

            //行テーブルを更新する
            this.UpdateStartIndex(deltaLength, row);

            foreach(var generator in this._generators)
                generator.Update(this.Document, this.GetLineHeadIndex(row), insertedLength, removedLength);
        }

        internal void UpdateAsReplace(int index, int removedLength, int insertedLength)
        {
#if DEBUG
            Debug.WriteLine("Replaced Index:{0} RemoveLength:{1} InsertLength:{2}", index, removedLength, insertedLength);
#endif
            int startRow, endRow;
            GetRemoveRange(index, removedLength, out startRow, out endRow);

            //行が存在しない場合、後で構築されるので何もしてはならない
            if (startRow == -1 || endRow == -1)
                return;

            int deltaLength = insertedLength - removedLength;

            var result = GetAnalyzeLength(startRow, endRow, index, removedLength, insertedLength);
            int HeadIndex = result.Item1;
            int analyzeLength = result.Item2;

            //挿入範囲内のドキュメントから行を生成する
            SpilitStringEventArgs e = new SpilitStringEventArgs(this.Document, HeadIndex, analyzeLength, startRow);
            IList<LineToIndexTableData> newLines = this.CreateLineList(e.index, e.length, Document.MaximumLineLength);

            int removeCount = endRow - startRow + 1;
            this.ReplaceRange(startRow, newLines, removeCount, deltaLength);

            this.AddDummyLine();

            foreach (var generator in this._generators)
                generator.Update(this.Document, index, insertedLength, removedLength);
        }

        internal IEnumerable<Tuple<int, int>> ForEachLines(int startIndex, int endIndex, int maxCharCount = -1)
        {
            int currentLineHeadIndex = startIndex;
            int currentLineLength = 0;

            for (int i = startIndex; i <= endIndex; i++)
            {
                currentLineLength++;
                char c = this.Document[i];
                if (c == Document.NewLine ||
                    (maxCharCount != -1 && currentLineLength >= maxCharCount))
                {
                    UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (uc != UnicodeCategory.NonSpacingMark &&
                    uc != UnicodeCategory.SpacingCombiningMark &&
                    uc != UnicodeCategory.EnclosingMark &&
                    uc != UnicodeCategory.Surrogate)
                    {
                        yield return new Tuple<int, int>(currentLineHeadIndex, currentLineLength);
                        currentLineHeadIndex += currentLineLength;
                        currentLineLength = 0;
                    }
                }
            }
            if (currentLineLength > 0)
                yield return new Tuple<int, int>(currentLineHeadIndex, currentLineLength);
        }

        IList<LineToIndexTableData> CreateLineList(int index, int length, int lineLimitLength = -1)
        {
            int startIndex = index;
            int endIndex = index + length - 1;
            List<LineToIndexTableData> output = new List<LineToIndexTableData>();

            foreach (Tuple<int, int> range in this.ForEachLines(startIndex, endIndex, lineLimitLength))
            {
                int lineHeadIndex = range.Item1;
                int lineLength = range.Item2;
                char c = this.Document[lineHeadIndex + lineLength - 1];
                bool hasNewLine = c == Document.NewLine;
                LineToIndexTableData result = new LineToIndexTableData(lineHeadIndex, lineLength, hasNewLine, this.IsFrozneDirtyFlag == false, null);
                output.Add(result);
            }

            if (output.Count > 0)
                output.Last().LineEnd = true;

            return output;
        }

        void GetRemoveRange(int index,int length,out int startRow,out int endRow)
        {
            if(this.TryGetLineNumberFromIndex(index, out startRow) == false)
            {
                startRow = -1;
                endRow = -1;
                return;
            }
            while (startRow > 0 && this._Lines[startRow - 1].LineEnd == false)
                startRow--;

            if (this.TryGetLineNumberFromIndex(index + length, out endRow) == false)
                endRow = this._Lines.Count - 1;
            while (endRow < this._Lines.Count && this._Lines[endRow].LineEnd == false)
                endRow++;
            if (endRow >= this._Lines.Count)
                endRow = this._Lines.Count - 1;
        }

        Tuple<int,int> GetAnalyzeLength(int startRow,int endRow,int updateStartIndex,int removedLength,int insertedLength)
        {
            int HeadIndex = this.GetIndexFromLineNumber(startRow);
            int LastIndex = this.GetIndexFromLineNumber(endRow) + this.GetLengthFromLineNumber(endRow) - 1;

            //SpilitStringの対象となる範囲を求める
            int fisrtPartLength = updateStartIndex - HeadIndex;
            int secondPartLength = LastIndex - (updateStartIndex + removedLength - 1);
            int analyzeLength = fisrtPartLength + secondPartLength + insertedLength;
            Debug.Assert(analyzeLength <= this.Document.Length - 1 - HeadIndex + 1);

            //分析する範囲とドキュメントの長さが一致しているかどうか
            int IndexAnayzed = HeadIndex + analyzeLength - 1;
            if (IndexAnayzed < this.Document.Length -1)
            {
                int i;
                for (i = IndexAnayzed; i < this.Document.Length; i++)
                {
                    if (this.Document.StringBuffer[i] == Document.NewLine)
                        break;
                }
                analyzeLength = i - HeadIndex + 1;
            }

            return new Tuple<int, int>(HeadIndex, analyzeLength);
        }

        void AddDummyLine()
        {
            LineToIndexTableData dummyLine = null;
            if (this._Lines.Count == 0)
            {
                dummyLine = new LineToIndexTableData();
                this._Lines.Add(dummyLine);
                return;
            }

            int lastLineRow = this._Lines.Count > 0 ? this._Lines.Count - 1 : 0;
            int lastLineHeadIndex = this.GetIndexFromLineNumber(lastLineRow);
            int lastLineLength = this.GetLengthFromLineNumber(lastLineRow);

            if (lastLineLength != 0 && this.Document[Document.Length - 1] == Document.NewLine)
            {
                int realIndex = lastLineHeadIndex + lastLineLength;
                if (lastLineRow >= this.stepRow)
                    realIndex -= this.stepLength;
                dummyLine = new LineToIndexTableData(realIndex, 0, true,false, null);
                this._Lines.Add(dummyLine);
            }
        }

        /// <summary>
        /// 生データを取得します
        /// </summary>
        /// <param name="row">行</param>
        /// <returns>LineToIndexTableData</returns>
        /// <remarks>いくつかの値は実態とかけ離れた値を返します。詳しくはLineToIndexTableDataの注意事項を参照すること</remarks>
        internal LineToIndexTableData GetRaw(int row)
        {
            LineToIndexTableData lineData;
            if(this.TryGetRaw(row,out lineData))
            {
                return lineData;
            }
            throw new ArgumentOutOfRangeException();
        }

        /// <summary>
        /// 生データを取得します
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="lineData">取得できた行データー。存在しないときはnullが入る。</param>
        /// <returns>取得できた場合は真を返し、そうでない場合は偽を返す。</returns>
        /// <remarks>いくつかの値は実態とかけ離れた値を返します。詳しくはLineToIndexTableDataの注意事項を参照すること。</remarks>
        internal bool TryGetRaw(int row,out LineToIndexTableData lineData)
        {
            if(row > this._Lines.Count)
            {
                lineData = null;
                return false;
            }
            lineData = this._Lines[row];
            return true;
        }

        /// <summary>
        /// 指定された行までレイアウト行を構築します
        /// </summary>
        /// <param name="row">行</param>
        internal void FetchLine(int row)
        {
            while (row >= this._Lines.Count - 1)
            {
                //直接最終行を取得すると後々おかしくなる
                int lastRow = this.Count - 1;
                int LineHeadIndex = this.GetIndexFromLineNumber(lastRow);
                int Length = this.GetLengthFromLineNumber(lastRow);
                if (LineHeadIndex + Length >= this.Document.Length)
                {
                    return;
                }
                this.Document.BuildLayout(LineHeadIndex + Length, Document.PreloadLength);
            }
        }

        /// <summary>
        /// 行番号をインデックスに変換します
        /// </summary>
        /// <param name="row">行番号</param>
        /// <returns>0から始まるインデックスを返す</returns>
        public int GetIndexFromLineNumber(int row)
        {
            if (row < 0 || row > this._Lines.Count)
                throw new ArgumentOutOfRangeException();
            return this.GetLineHeadIndex(row);
        }

        /// <summary>
        /// 行の長さを得ます
        /// </summary>
        /// <param name="row">行番号</param>
        /// <returns>行の文字長を返します</returns>
        public int GetLengthFromLineNumber(int row)
        {
            if (row < 0 || row > this._Lines.Count)
                throw new ArgumentOutOfRangeException();
            return this.collection[row].Length;
        }

        /// <summary>
        /// 更新フラグを取得します
        /// </summary>
        /// <param name="row">行番号</param>
        /// <returns>更新されていれば真。そうでなければ偽</returns>
        public bool GetDirtyFlag(int row)
        {
            if (row < 0 || row > this._Lines.Count)
                throw new ArgumentOutOfRangeException();
            return this.GetRaw(row).Dirty;
        }

        /// <summary>
        /// 行の高さを返す
        /// </summary>
        /// <param name="tp">テキストポイント</param>
        /// <returns>テキストポイントで指定された行の高さを返します</returns>
        public double GetLineHeight(TextPoint tp)
        {
            return this.render.emSize.Height * this.Render.LineEmHeight;
        }

        internal ITextLayout GetLayout(int row)
        {
            var lineData = this.GetRaw(row);
            if (lineData.Layout != null && lineData.Layout.Invaild)
            {
                lineData.Layout.Dispose();
                lineData.Layout = null;
            }
            if (lineData.Layout == null || lineData.Layout.Disposed)
                lineData.Layout = this.CreateLayout(row);
            return lineData.Layout;
        }

        internal event EventHandler<CreateLayoutEventArgs> CreateingLayout;

        ITextLayout CreateLayout(int row)
        {
            ITextLayout layout;
            LineToIndexTableData lineData = this.GetRaw(row);
            if (lineData.Length == 0)
            {
                layout = this.render.CreateLaytout("", null, null, null,this.WrapWidth);
            }
            else
            {
                int lineHeadIndex = this.GetLineHeadIndex(row);

                string content = this.Document.ToString(lineHeadIndex, lineData.Length);

                if (this.CreateingLayout != null)
                    this.CreateingLayout(this, new CreateLayoutEventArgs(lineHeadIndex, lineData.Length,content));

                var userMarkerRange = from id in this.Document.Markers.IDs
                                  from s in this.Document.Markers.Get(id,lineHeadIndex,lineData.Length)
                                  let n = Util.ConvertAbsIndexToRelIndex(s, lineHeadIndex, lineData.Length)
                                  select n;
                var watchdogMarkerRange = from s in this.Document.MarkerPatternSet.GetMarkers(new CreateLayoutEventArgs(lineHeadIndex, lineData.Length, content))
                                          let n = Util.ConvertAbsIndexToRelIndex(s, lineHeadIndex, lineData.Length)
                                          select n;
                var markerRange = watchdogMarkerRange.Concat(userMarkerRange);
                var selectRange = from s in this.Document.Selections.Get(lineHeadIndex, lineData.Length)
                                  let n = Util.ConvertAbsIndexToRelIndex(s, lineHeadIndex, lineData.Length)
                                  select n;

                layout = this.render.CreateLaytout(content, lineData.Syntax, markerRange, selectRange,this.WrapWidth);
            }

            return layout;
        }

        /// <summary>
        /// インデックスを行番号に変換します
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>行番号を返します</returns>
        public int GetLineNumberFromIndex(int index)
        {
            var result = this.IndexOfLoose(index);

            if(result == -1)
                throw new ArgumentOutOfRangeException("該当する行が見つかりませんでした");

            return result;
        }

        /// <summary>
        /// 行番号を返す
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <param name="resultRow">対応する行番号。存在しなければ-1。</param>
        /// <returns>存在しなければ偽。存在すれば真を返す。</returns>
        public bool TryGetLineNumberFromIndex(int index,out int resultRow)
        {
            resultRow = -1;
            var result = this.IndexOfLoose(index);
            if (result == -1)
                return false;

            resultRow = result;
            return true;
        }

        /// <summary>
        /// インデックスからテキストポイントに変換します
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>TextPoint構造体を返します</returns>
        public TextPoint GetTextPointFromIndex(int index)
        {
            TextPoint tp = new TextPoint();
            tp.row = GetLineNumberFromIndex(index);
            tp.col = index - this.GetLineHeadIndex(tp.row);
            Debug.Assert(tp.row < this._Lines.Count && tp.col <= this.GetRaw(tp.row).Length);
            return tp;
        }

        /// <summary>
        /// テキストポイントからインデックスに変換します
        /// </summary>
        /// <param name="tp">TextPoint構造体</param>
        /// <returns>インデックスを返します</returns>
        public int GetIndexFromTextPoint(TextPoint tp)
        {
            if (tp == TextPoint.Null)
                throw new ArgumentNullException("TextPoint.Null以外の値でなければなりません");
            if(tp.row < 0 || tp.row > this._Lines.Count)
                throw new ArgumentOutOfRangeException("tp.rowが設定できる範囲を超えています");
            if (tp.col < 0 || tp.col > this.GetRaw(tp.row).Length)
                throw new ArgumentOutOfRangeException("tp.colが設定できる範囲を超えています");
            return this.GetLineHeadIndex(tp.row) + tp.col;
        }

        /// <summary>
        /// フォールディングを再生成します
        /// </summary>
        /// <param name="force">ドキュメントが更新されていなくても再生成する</param>
        /// <returns>生成された場合は真を返す</returns>
        /// <remarks>デフォルトではドキュメントが更新されている時にだけ再生成されます</remarks>
        public bool GenerateFolding(bool force = false)
        {
            return this._generators[FOLDING_INDEX].Generate(this.Document, this, force);
        }

        /// <summary>
        /// フォールディングをすべて削除します
        /// </summary>
        public void ClearFolding()
        {
            this._generators[FOLDING_INDEX].Clear(this);
        }

        /// <summary>
        /// すべての行に対しシンタックスハイライトを行います
        /// </summary>
        public bool HilightAll(bool force = false)
        {
            return this._generators[SYNTAX_HIGLITHER_INDEX].Generate(this.Document, this, force);
        }

        /// <summary>
        /// ハイライト関連の情報をすべて削除します
        /// </summary>
        public void ClearHilight()
        {
            this._generators[SYNTAX_HIGLITHER_INDEX].Clear(this);
        }

        /// <summary>
        /// すべて削除します
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            this.ClearLayoutCache();
            this.ClearFolding();
            LineToIndexTableData dummy = new LineToIndexTableData();
            this._Lines.Add(dummy);
        }

        #region IEnumerable<string> メンバー

        /// <summary>
        /// コレクションを反復処理するためのIEnumeratorを返す
        /// </summary>
        /// <returns>IEnumeratorオブジェクト</returns>
        public new IEnumerator<string> GetEnumerator()
        {
            for (int i = 0; i < this._Lines.Count; i++)
                yield return this[i];
        }

        #endregion

        #region IEnumerable メンバー

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this._Lines.Count; i++)
                yield return this[i];
        }

        #endregion
    }

}
