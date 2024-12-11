/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FooEditEngine
{
    /// <summary>
    /// オートインデントを行うためのデリゲートを表す
    /// </summary>
    /// <param name="sender">イベント発生元のオブジェクト</param>
    /// <param name="e">イベントデーター</param>
    public delegate void AutoIndentHookerHandler(object sender, EventArgs e);

    /// <summary>
    /// 進行状況を表す列挙体
    /// </summary>
    public enum ProgressState
    {
        /// <summary>
        /// 操作が開始したことを表す
        /// </summary>
        Start,
        /// <summary>
        /// 操作が終了したことを表す
        /// </summary>
        Complete,
    }
    /// <summary>
    /// 進行状況を表すためのイベントデータ
    /// </summary>
    public sealed class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 進行状況
        /// </summary>
        public ProgressState state;
        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="state">ProgressStateオブジェクト</param>
        public ProgressEventArgs(ProgressState state)
        {
            this.state = state;
        }
    }

    /// <summary>
    /// 進行状況を通知するためのデリゲート
    /// </summary>
    /// <param name="sender">送信元クラス</param>
    /// <param name="e">イベントデータ</param>
    public delegate void ProgressEventHandler(object sender, ProgressEventArgs e);

    /// <summary>
    /// 更新タイプを表す列挙体
    /// </summary>
    public enum UpdateType
    {
        /// <summary>
        /// ドキュメントが置き換えられたことを表す
        /// </summary>
        Replace,
        /// <summary>
        /// ドキュメント全体が削除されたことを表す
        /// </summary>
        Clear,
        /// <summary>
        /// レイアウトが再構築されたことを表す
        /// </summary>
        RebuildLayout,
        /// <summary>
        /// レイアウトの構築が必要なことを示す
        /// </summary>
        BuildLayout,
    }

    /// <summary>
    /// 更新タイプを通知するためのイベントデータ
    /// </summary>
    public sealed class DocumentUpdateEventArgs : EventArgs
    {
        /// <summary>
        /// 値が指定されていないことを示す
        /// </summary>
        public const int EmptyValue = -1;
        /// <summary>
        /// 更新タイプ
        /// </summary>
        public UpdateType type;
        /// <summary>
        /// 開始位置
        /// </summary>
        public int startIndex;
        /// <summary>
        /// 削除された長さ
        /// </summary>
        public int removeLength;
        /// <summary>
        /// 追加された長さ
        /// </summary>
        public int insertLength;
        /// <summary>
        /// 更新イベントが発生した行。行が不明な場合や行をまたぐ場合はnullを指定すること。
        /// </summary>
        public int? row;
        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="type">更新タイプ</param>
        /// <param name="startIndex">開始インデックス</param>
        /// <param name="removeLength">削除された長さ</param>
        /// <param name="insertLength">追加された長さ</param>
        /// <param name="row">開始行。nullを指定することができる</param>
        public DocumentUpdateEventArgs(UpdateType type, int startIndex = EmptyValue, int removeLength = EmptyValue, int insertLength = EmptyValue, int? row = null)
        {
            this.type = type;
            this.startIndex = startIndex;
            this.removeLength = removeLength;
            this.insertLength = insertLength;
            this.row = row;
        }
    }

    /// <summary>
    /// ドキュメントに更新があったことを伝えるためのデリゲート
    /// </summary>
    /// <param name="sender">送信元クラス</param>
    /// <param name="e">イベントデータ</param>
    public delegate void DocumentUpdateEventHandler(object sender, DocumentUpdateEventArgs e);

    /// <summary>
    /// ドキュメントの管理を行う
    /// </summary>
    /// <remarks>この型のすべてのメソッド・プロパティはスレッドセーフです</remarks>
    public sealed class Document : IEnumerable<char>, IRandomEnumrator<char>, IDisposable
    {

        Regex regex;
        Match match;
        StringBuffer buffer;
        LineToIndexTable _LayoutLines;
        bool _EnableFireUpdateEvent = true,_UrlMark = false, _DrawLineNumber = false, _HideRuler = true, _RightToLeft = false;
        LineBreakMethod _LineBreak;
        int _TabStops, _LineBreakCharCount = 80;
        bool _ShowFullSpace, _ShowHalfSpace, _ShowTab, _ShowLineBreak,_InsertMode, _HideCaret, _HideLineMarker, _RectSelection;
        IndentMode _IndentMode;

        /// <summary>
        /// 一行当たりの最大文字数
        /// </summary>
        public const int MaximumLineLength = 1000;
        /// <summary>
        /// 事前読み込みを行う長さ
        /// </summary>
        /// <remarks>値を反映させるためにはレイアウト行すべてを削除する必要があります</remarks>
        public static int PreloadLength = 1024 * 1024 * 5;

        /// <summary>
        /// コンストラクター
        /// </summary>
        public Document()
            : this(null)
        {
        }

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="doc">ドキュメントオブジェクト</param>
        /// <remarks>docが複製されますが、プロパティは引き継がれません</remarks>
        public Document(Document doc)
        {
            if (doc == null)
                this.buffer = new StringBuffer();
            else
                this.buffer = new StringBuffer(doc.buffer);
            this.buffer.Update = new DocumentUpdateEventHandler(buffer_Update);
            this.Update += new DocumentUpdateEventHandler((s, e) => { });
            this.ChangeFireUpdateEvent += new EventHandler((s, e) => { });
            this.StatusUpdate += new EventHandler((s, e) => { });
            this.Markers = new MarkerCollection();
            this.UndoManager = new UndoManager();
            this._LayoutLines = new LineToIndexTable(this);
            this._LayoutLines.Clear();
            this.MarkerPatternSet = new MarkerPatternSet(this._LayoutLines, this.Markers);
            this.MarkerPatternSet.Updated += WacthDogPattern_Updated;
            this.Selections = new SelectCollection();
            this.CaretPostion = new TextPoint();
            this.HideLineMarker = true;
            this.SelectGrippers = new GripperRectangle(new Gripper(), new Gripper());
            this.SelectionChanged += new EventHandler((s, e) => { });
            this.CaretChanged += (s, e) => { };
            this.AutoIndentHook += (s, e) => { };
            this.LineBreakChanged += (s, e) => { };
            this.Dirty = false;
        }

        void WacthDogPattern_Updated(object sender, EventArgs e)
        {
            this._LayoutLines.ClearLayoutCache();
        }

        /// <summary>
        /// ダーティフラグ。保存されていなければ真、そうでなければ偽。
        /// </summary>
        public bool Dirty
        {
            get;
            set;
        }

        /// <summary>
        /// キャレットでの選択の起点となる位置
        /// </summary>
        internal int AnchorIndex
        {
            get;
            set;
        }

        /// <summary>
        /// レタリングの開始位置を表す
        /// </summary>
        internal SrcPoint Src
        {
            get;
            set;
        }

        /// <summary>
        /// ドキュメントのタイトル
        /// </summary>
        public string Title
        {
            get;
            set;
        }

        /// <summary>
        /// 補完候補プロセッサーが切り替わったときに発生するイベント
        /// </summary>
        public event EventHandler AutoCompleteChanged;

        AutoCompleteBoxBase _AutoComplete;
        /// <summary>
        /// 補完候補プロセッサー
        /// </summary>
        public AutoCompleteBoxBase AutoComplete
        {
            get
            {
                return this._AutoComplete;
            }
            set
            {
                this._AutoComplete = value;
                if (this.AutoCompleteChanged != null)
                    this.AutoCompleteChanged(this, null);
            }
        }

        /// <summary>
        /// 読み込み中に発生するイベント
        /// </summary>
        public event ProgressEventHandler LoadProgress;

        /// <summary>
        /// ルーラーやキャレット・行番号などの表示すべきものが変化した場合に呼び出される。ドキュメントの内容が変化した通知を受け取り場合はUpdateを使用してください
        /// </summary>
        public event EventHandler StatusUpdate;

        /// <summary>
        /// 全角スペースを表示するかどうか
        /// </summary>
        public bool ShowFullSpace
        {
            get { return this._ShowFullSpace; }
            set
            {
                if (this._ShowFullSpace == value)
                    return;
                this._ShowFullSpace = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// 半角スペースを表示するかどうか
        /// </summary>
        public bool ShowHalfSpace
        {
            get { return this._ShowHalfSpace; }
            set
            {
                if (this._ShowHalfSpace == value)
                    return;
                this._ShowHalfSpace = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// TABを表示するかどうか
        /// </summary>
        public bool ShowTab
        {
            get { return this._ShowTab; }
            set
            {
                if (this._ShowTab == value)
                    return;
                this._ShowTab = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// 改行を表示するかどうか
        /// </summary>
        public bool ShowLineBreak
        {
            get { return this._ShowLineBreak; }
            set
            {
                if (this._ShowLineBreak == value)
                    return;
                this._ShowLineBreak = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// 選択範囲にあるグリッパーのリスト
        /// </summary>
        internal GripperRectangle SelectGrippers
        {
            private set;
            get;
        }

        /// <summary>
        /// 右から左に表示するなら真
        /// </summary>
        public bool RightToLeft {
            get { return this._RightToLeft; }
            set
            {
                if (this._RightToLeft == value)
                    return;
                this._RightToLeft = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// 矩形選択モードなら真を返し、そうでない場合は偽を返す
        /// </summary>
        public bool RectSelection
        {
            get
            {
                return this._RectSelection;
            }
            set
            {
                this._RectSelection = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// インデントの方法を表す
        /// </summary>
        public IndentMode IndentMode
        {
            get
            {
                return this._IndentMode;
            }
            set
            {
                this._IndentMode = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// ラインマーカーを描くなら偽。そうでなければ真
        /// </summary>
        public bool HideLineMarker
        {
            get
            {
                return this._HideLineMarker;
            }
            set
            {
                this._HideLineMarker = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// キャレットを描くなら偽。そうでなければ真
        /// </summary>
        public bool HideCaret
        {
            get
            {
                return this._HideCaret;
            }
            set
            {
                this._HideCaret = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// 挿入モードなら真を返し、上書きモードなら偽を返す
        /// </summary>
        public bool InsertMode
        {
            get
            {
                return this._InsertMode;
            }
            set
            {
                this._InsertMode = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// ルーラーを表示しないなら真、そうでないなら偽
        /// </summary>
        public bool HideRuler
        {
            get { return this._HideRuler; }
            set
            {
                if (this._HideRuler == value)
                    return;
                this._HideRuler = value;
                this.LayoutLines.ClearLayoutCache();
                this.StatusUpdate(this, null);
            }
        }

        TextPoint _CaretPostion;
        /// <summary>
        /// レイアウト行のどこにキャレットがあるかを表す
        /// </summary>
        public TextPoint CaretPostion
        {
            get
            {
                return this._CaretPostion;
            }
            set
            {
                if(this._CaretPostion != value)
                {
                    this._CaretPostion = value;
                    this.RaiseCaretPostionChanged();
                }
            }
        }

        public void RaiseCaretPostionChanged()
        {
            if(this.CaretPostion != null)
                this.CaretChanged(this, null);
        }

        /// <summary>
        /// キャレットを指定した位置に移動させる
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="autoExpand">折り畳みを展開するなら真</param>
        internal void SetCaretPostionWithoutEvent(int row, int col, bool autoExpand = true)
        {
            //this.LayoutLines.FetchLine(row);
            if (autoExpand)
            {
                int lineHeadIndex = this.LayoutLines.GetIndexFromLineNumber(row);
                int lineLength = this.LayoutLines.GetLengthFromLineNumber(row);
                FoldingItem foldingData = this.LayoutLines.FoldingCollection.Get(lineHeadIndex, lineLength);
                if (foldingData != null)
                {
                    if (this.LayoutLines.FoldingCollection.IsParentHidden(foldingData) || !foldingData.IsFirstLine(this.LayoutLines, row))
                    {
                        this.LayoutLines.FoldingCollection.Expand(foldingData);
                    }
                }
            }
            this._CaretPostion = new TextPoint(row, col);
        }

        /// <summary>
        /// 選択範囲コレクション
        /// </summary>
        internal SelectCollection Selections
        {
            get;
            set;
        }

        /// <summary>
        /// 行番号を表示するかどうか
        /// </summary>
        public bool DrawLineNumber
        {
            get { return this._DrawLineNumber; }
            set
            {
                if (this._DrawLineNumber == value)
                    return;
                this._DrawLineNumber = value;
                this._LayoutLines.ClearLayoutCache();
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// URLをハイパーリンクとして表示するなら真。そうでないなら偽
        /// </summary>
        public bool UrlMark
        {
            get { return this._UrlMark; }
            set
            {
                if (this._UrlMark == value)
                    return;
                this._UrlMark = value;
                if (value)
                {
                    Regex regex = new Regex("(http|https|ftp)(:\\/\\/[-_.!~*\\'()a-zA-Z0-9;\\/?:\\@&=+\\$,%#]+)");
                    this.MarkerPatternSet.Add(MarkerIDs.URL, new RegexMarkerPattern(regex, HilightType.Url, new Color()));
                }
                else
                {
                    this.MarkerPatternSet.Remove(MarkerIDs.URL);
                }
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// 桁折りの方法が変わったことを表す
        /// </summary>
        public event EventHandler LineBreakChanged;

        /// <summary>
        /// 桁折り処理の方法を指定する
        /// </summary>
        /// <remarks>
        /// 変更した場合、呼び出し側で再描写とレイアウトの再構築を行う必要があります
        /// また、StatusUpdatedではなく、LineBreakChangedイベントが発生します
        /// </remarks>
        public LineBreakMethod LineBreak
        {
            get
            {
                return this._LineBreak;
            }
            set
            {
                if (this._LineBreak == value)
                    return;
                this._LineBreak = value;
                this.LineBreakChanged(this, null);
            }
        }

        /// <summary>
        /// 折り返し行う文字数。実際に折り返しが行われる幅はem単位×この値となります
        /// </summary>
        /// <remarks>この値を変えた場合、LineBreakChangedイベントが発生します</remarks>
        public int LineBreakCharCount
        {
            get
            {
                return this._LineBreakCharCount;
            }
            set
            {
                if (this._LineBreakCharCount == value)
                    return;
                this._LineBreakCharCount = value;
                this.LineBreakChanged(this, null);
            }
        }

        /// <summary>
        /// タブの幅
        /// </summary>
        /// <remarks>変更した場合、呼び出し側で再描写する必要があります</remarks>
        public int TabStops
        {
            get { return this._TabStops; }
            set {
                if (this._TabStops == value)
                    return;
                this._TabStops = value;
                this.StatusUpdate(this, null);
            }
        }

        /// <summary>
        /// マーカーパターンセット
        /// </summary>
        public MarkerPatternSet MarkerPatternSet
        {
            get;
            private set;
        }

        /// <summary>
        /// レイアウト行を表す
        /// </summary>
        public LineToIndexTable LayoutLines
        {
            get
            {
                return this._LayoutLines;
            }
        }

        internal void FireUpdate(DocumentUpdateEventArgs e)
        {
            this.buffer_Update(this.buffer, e);
        }

        /// <summary>
        /// ドキュメントが更新された時に呼ばれるイベント
        /// </summary>
        public event DocumentUpdateEventHandler Update;

        /// <summary>
        /// FireUpdateEventの値が変わったときに呼び出されるイベント
        /// </summary>
        public event EventHandler ChangeFireUpdateEvent;

        /// <summary>
        /// 改行コードの内部表現
        /// </summary>
        public const char NewLine = '\n';

        /// <summary>
        /// EOFの内部表現
        /// </summary>
        public const char EndOfFile = '\u001a';

        /// <summary>
        /// アンドゥ管理クラスを表す
        /// </summary>
        public UndoManager UndoManager
        {
            get;
            private set;
        }

        /// <summary>
        /// 文字列の長さ
        /// </summary>
        public int Length
        {
            get
            {
                return this.buffer.Length;
            }
        }

        /// <summary>
        /// 変更のたびにUpdateイベントを発生させるかどうか
        /// </summary>
        public bool FireUpdateEvent
        {
            get
            {
                return this._EnableFireUpdateEvent;
            }
            set
            {
                this._EnableFireUpdateEvent = value;
                this.ChangeFireUpdateEvent(this, null);
            }
        }

        /// <summary>
        /// インデクサー
        /// </summary>
        /// <param name="i">インデックス（自然数でなければならない）</param>
        /// <returns>Char型</returns>
        public char this[int i]
        {
            get
            {
                return this.buffer[i];
            }
        }

        /// <summary>
        /// マーカーコレクション
        /// </summary>
        public MarkerCollection Markers
        {
            get;
            private set;
        }

        internal StringBuffer StringBuffer
        {
            get
            {
                return this.buffer;
            }
        }

        /// <summary>
        /// 再描写を要求しているなら真
        /// </summary>
        public bool IsRequestRedraw { get; internal set; }

        /// <summary>
        /// 再描写を要求する
        /// </summary>
        public void RequestRedraw()
        {
            this.IsRequestRedraw = true;
        }

        /// <summary>
        /// レイアウト行が構築されたときに発生するイベント
        /// </summary>
        public event EventHandler PerformLayouted;
        /// <summary>
        /// レイアウト行をすべて破棄し、再度レイアウトを行う
        /// </summary>
        /// <param name="quick">真の場合、レイアウトキャッシュのみ再構築します</param>
        public void PerformLayout(bool quick = true)
        {
            if (quick)
            {
                this.LayoutLines.ClearLayoutCache();
            }
            else
            {
                this.LayoutLines.IsFrozneDirtyFlag = true;
                this.FireUpdate(new DocumentUpdateEventArgs(UpdateType.RebuildLayout, -1, -1, -1));
                this.LayoutLines.IsFrozneDirtyFlag = false;
            }
            if (this.PerformLayouted != null)
                this.PerformLayouted(this, null);
        }

        /// <summary>
        /// レイアウト行を構築します
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="Length"></param>
        public void BuildLayout(int startIndex,int Length)
        {
            this.LayoutLines.IsFrozneDirtyFlag = true;
            this.FireUpdate(
                new DocumentUpdateEventArgs(UpdateType.BuildLayout,
                startIndex,
                0,
                Length));
            this.LayoutLines.IsFrozneDirtyFlag = false;
        }

        /// <summary>
        /// オードインデントが可能になった時に通知される
        /// </summary>
        /// <remarks>
        /// FireUpdateEventの影響を受けます
        /// </remarks>
        public event AutoIndentHookerHandler AutoIndentHook;

        /// <summary>
        /// 選択領域変更時に通知される
        /// </summary>
        public event EventHandler SelectionChanged;

        /// <summary>
        /// キャレット移動時に通知される
        /// </summary>
        public event EventHandler CaretChanged;

        /// <summary>
        /// 指定された範囲を選択する
        /// </summary>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <remarks>RectSelectionの値によって動作が変わります。真の場合は矩形選択モードに、そうでない場合は行ごとに選択されます</remarks>
        public void Select(int start, int length)
        {
            if (this.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            if (start < 0 || start + length < 0 || start + length > this.Length)
                throw new ArgumentOutOfRangeException("startかendが指定できる範囲を超えてます");
            //選択範囲が消されたとき
            foreach (Selection sel in this.Selections)
                this.LayoutLines.ClearLayoutCache(sel.start, sel.length);
            this.Selections.Clear();
            if (length < 0)
            {
                int oldStart = start;
                start += length;
                length = oldStart - start;
            }
            if (this.RectSelection && length != 0)
            {
                TextPoint startTextPoint = this.LayoutLines.GetTextPointFromIndex(start);
                TextPoint endTextPoint = this.LayoutLines.GetTextPointFromIndex(start + length);
                this.SelectByRectangle(new TextRectangle(startTextPoint, endTextPoint));
                this.LayoutLines.ClearLayoutCache(start, length);
            }
            else if (length != 0)
            {
                this.Selections.Add(Selection.Create(start, length));
                this.LayoutLines.ClearLayoutCache(start, length);
            }
            this.SelectionChanged(this, null);
        }

        /// <summary>
        /// 矩形選択を行う
        /// </summary>
        /// <param name="tp">開始位置</param>
        /// <param name="width">桁数</param>
        /// <param name="height">行数</param>
        public void Select(TextPoint tp, int width, int height)
        {
            if (this.FireUpdateEvent == false || !this.RectSelection)
                throw new InvalidOperationException("");
            TextPoint end = tp;

            end.row = tp.row + height;
            end.col = tp.col + width;

            if (end.row > this.LayoutLines.Count - 1)
                throw new ArgumentOutOfRangeException("");

            this.Selections.Clear();

            this.SelectByRectangle(new TextRectangle(tp, end));

            this.SelectionChanged(this, null);
        }

        private void SelectByRectangle(TextRectangle rect)
        {
            if (this.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            if (rect.TopLeft <= rect.BottomRight)
            {
                for (int i = rect.TopLeft.row; i <= rect.BottomLeft.row; i++)
                {
                    int length = this.LayoutLines.GetLengthFromLineNumber(i);
                    int leftCol = rect.TopLeft.col, rightCol = rect.TopRight.col, lastCol = length;
                    if (length > 0 && this.LayoutLines[i][length - 1] == Document.NewLine)
                        lastCol = length - 1;
                    if (lastCol < 0)
                        lastCol = 0;
                    if (rect.TopLeft.col > lastCol)
                        leftCol = lastCol;
                    if (rect.TopRight.col > lastCol)
                        rightCol = lastCol;

                    int StartIndex = this.LayoutLines.GetIndexFromTextPoint(new TextPoint(i, leftCol));
                    int EndIndex = this.LayoutLines.GetIndexFromTextPoint(new TextPoint(i, rightCol));

                    Selection sel;
                    sel = Selection.Create(StartIndex, EndIndex - StartIndex);

                    this.Selections.Add(sel);
                }
            }
        }

        /// <summary>
        /// 単語単位で選択する
        /// </summary>
        /// <param name="index">探索を開始するインデックス</param>
        /// <param name="changeAnchor">選択の起点となるとインデックスを変更するなら真。そうでなければ偽</param>
        public void SelectWord(int index, bool changeAnchor = false)
        {
            this.SelectSepartor(index, (c) => Util.IsWordSeparator(c), changeAnchor);
        }

        /// <summary>
        /// 行単位で選択する
        /// </summary>
        /// <param name="index">探索を開始するインデックス</param>
        /// <param name="changeAnchor">選択の起点となるとインデックスを変更するなら真。そうでなければ偽</param>
        public void SelectLine(int index,bool changeAnchor = false)
        {
            this.SelectSepartor(index, (c) => c == Document.NewLine, changeAnchor);
        }

        /// <summary>
        /// セパレーターで区切られた領域を取得する
        /// </summary>
        /// <param name="index">探索を開始するインデックス</param>
        /// <param name="find_sep_func">セパレーターなら真を返し、そうでないなら偽を返す</param>
        /// <returns>開始インデックス、終了インデックス</returns>
        public Tuple<int,int> GetSepartor(int index, Func<char, bool> find_sep_func)
        {
            if (find_sep_func == null)
                throw new ArgumentNullException("find_sep_func must not be null");

            if (this.Length <= 0 || index >= this.Length)
                return null;

            Document str = this;

            int start = index;
            while (start > 0 && !find_sep_func(str[start]))
                start--;

            if (find_sep_func(str[start]))
            {
                start++;
            }

            int end = index;
            while (end < this.Length && !find_sep_func(str[end]))
                end++;

            return new Tuple<int, int>(start, end);
        }

        /// <summary>
        /// セパレーターで囲まれた範囲内を選択する
        /// </summary>
        /// <param name="index">探索を開始するインデックス</param>
        /// <param name="find_sep_func">セパレーターなら真を返し、そうでないなら偽を返す</param>
        /// <param name="changeAnchor">選択の起点となるとインデックスを変更するなら真。そうでなければ偽</param>
        public void SelectSepartor(int index,Func<char,bool> find_sep_func, bool changeAnchor = false)
        {
            if (this.FireUpdateEvent == false)
                throw new InvalidOperationException("");

            if (find_sep_func == null)
                throw new ArgumentNullException("find_sep_func must not be null");

            var t = this.GetSepartor(index, find_sep_func);
            if (t == null)
                return;

            int start = t.Item1, end = t.Item2;

            this.Select(start, end - start);

            if (changeAnchor)
                this.AnchorIndex = start;
        }

        /// <summary>
        /// DocumentReaderを作成します
        /// </summary>
        /// <returns>DocumentReaderオブジェクト</returns>
        public DocumentReader CreateReader()
        {
            return new DocumentReader(this.buffer);
        }

        /// <summary>
        /// マーカーを設定する
        /// </summary>
        /// <param name="id">マーカーID</param>
        /// <param name="m">設定したいマーカー</param>
        public void SetMarker(int id,Marker m)
        {
            if (m.start < 0 || m.start + m.length > this.Length)
                throw new ArgumentOutOfRangeException("startもしくはendが指定できる範囲を超えています");

            this.Markers.Add(id,m);
        }

        /// <summary>
        /// マーカーを削除する
        /// </summary>
        /// <param name="id">マーカーID</param>
        /// <param name="start">開始インデックス</param>
        /// <param name="length">削除する長さ</param>
        public void RemoveMarker(int id,int start, int length)
        {
            if (start < 0 || start + length > this.Length)
                throw new ArgumentOutOfRangeException("startもしくはendが指定できる範囲を超えています");

            this.Markers.RemoveAll(id,start, length);
        }

        /// <summary>
        /// マーカーを削除する
        /// </summary>
        /// <param name="id">マーカーID</param>
        /// <param name="type">削除したいマーカーのタイプ</param>
        public void RemoveMarker(int id, HilightType type)
        {
            this.Markers.RemoveAll(id,type);
        }

        /// <summary>
        /// すべてのマーカーを削除する
        /// </summary>
        /// <param name="id">マーカーID</param>
        public void RemoveAllMarker(int id)
        {
            this.Markers.RemoveAll(id);
        }

        /// <summary>
        /// インデックスに対応するマーカーを得る
        /// </summary>
        /// <param name="id">マーカーID</param>
        /// <param name="index">インデックス</param>
        /// <returns>Marker構造体の列挙子</returns>
        public IEnumerable<Marker> GetMarkers(int id, int index)
        {
            if (index < 0 || index > this.Length)
                throw new ArgumentOutOfRangeException("indexが範囲を超えています");
            return this.Markers.Get(id,index);
        }

        /// <summary>
        /// 部分文字列を取得する
        /// </summary>
        /// <param name="index">開始インデックス</param>
        /// <param name="length">長さ</param>
        /// <returns>Stringオブジェクト</returns>
        public string ToString(int index, int length)
        {
            return this.buffer.ToString(index, length);
        }

        /// <summary>
        /// インデックスを開始位置とする文字列を返す
        /// </summary>
        /// <param name="index">開始インデックス</param>
        /// <returns>Stringオブジェクト</returns>
        public string ToString(int index)
        {
            return this.ToString(index, this.buffer.Length - index);
        }

        /// <summary>
        /// 行を取得する
        /// </summary>
        /// <param name="startIndex">開始インデックス</param>
        /// <param name="endIndex">終了インデックス</param>
        /// <param name="maxCharCount">最大長</param>
        /// <returns>行イテレーターが返される</returns>
        public IEnumerable<string> GetLines(int startIndex, int endIndex, int maxCharCount = -1)
        {
            foreach (Tuple<int, int> range in this.LayoutLines.ForEachLines(startIndex, endIndex, maxCharCount))
            {
                StringBuilder temp = new StringBuilder();
                temp.Clear();
                int lineEndIndex = range.Item1;
                if (range.Item2 > 0)
                    lineEndIndex += range.Item2 - 1;
                for (int i = range.Item1; i <= lineEndIndex; i++)
                    temp.Append(this.buffer[i]);
                yield return temp.ToString();
            }
        }

        /// <summary>
        /// 文字列を追加する
        /// </summary>
        /// <param name="s">追加したい文字列</param>
        /// <remarks>非同期操作中はこのメソッドを実行することはできません</remarks>
        public void Append(string s)
        {
            this.Replace(this.buffer.Length, 0, s);
        }

        /// <summary>
        /// 文字列を挿入する
        /// </summary>
        /// <param name="index">開始インデックス</param>
        /// <param name="s">追加したい文字列</param>
        /// <remarks>読み出し操作中はこのメソッドを実行することはできません</remarks>
        public void Insert(int index, string s)
        {
            this.Replace(index, 0, s);
        }

        /// <summary>
        /// 文字列を削除する
        /// </summary>
        /// <param name="index">開始インデックス</param>
        /// <param name="length">長さ</param>
        /// <remarks>読み出し操作中はこのメソッドを実行することはできません</remarks>
        public void Remove(int index, int length)
        {
            this.Replace(index, length, "");
        }

        /// <summary>
        /// ドキュメントを置き換える
        /// </summary>
        /// <param name="index">開始インデックス</param>
        /// <param name="length">長さ</param>
        /// <param name="s">文字列</param>
        /// <param name="UserInput">ユーザーからの入力として扱うなら真</param>
        /// <remarks>読み出し操作中はこのメソッドを実行することはできません</remarks>
        public void Replace(int index, int length, string s, bool UserInput = false)
        {
            if (index < 0 || index > this.buffer.Length || index + length > this.buffer.Length || length < 0)
                throw new ArgumentOutOfRangeException();
            if (length == 0 && (s == string.Empty || s == null))
                return;

            foreach(int id in this.Markers.IDs)
                this.RemoveMarker(id,index, length);

            ReplaceCommand cmd = new ReplaceCommand(this.buffer, index, length, s);
            this.UndoManager.push(cmd);
            cmd.redo();

            if (this.FireUpdateEvent && UserInput)
            {
                var input_str = string.Empty;
                if (s == Document.NewLine.ToString())
                    input_str = s;
                else if (s == string.Empty && length > 0)
                    input_str = "\b";
                //入力は終わっているので空文字を渡すが処理の都合で一部文字だけはそのまま渡す
                if (this.AutoComplete != null)
                    this.AutoComplete.ParseInput(input_str);
                if (s == Document.NewLine.ToString())
                    this.AutoIndentHook(this, null);
            }
        }

        /// <summary>
        /// 物理行をすべて削除する
        /// </summary>
        /// <remarks>Dirtyフラグも同時にクリアーされます</remarks>
        /// <remarks>非同期操作中はこのメソッドを実行することはできません</remarks>
        public void Clear()
        {
            this.buffer.Clear();
            this.Dirty = false;
        }

        /// <summary>
        /// ストリームからドキュメントを非同期的に構築します
        /// </summary>
        /// <param name="fs">IStreamReaderオブジェクト</param>
        /// <param name="tokenSource">キャンセルトークン</param>
        /// <param name="file_size">ファイルサイズ。-1を指定しても動作しますが、読み取りが遅くなります</param>
        /// <returns>Taskオブジェクト</returns>
        /// <remarks>
        /// 読み取り操作は別スレッドで行われます。
        /// また、非同期操作中はこのメソッドを実行することはできません。
        /// </remarks>
        public async Task LoadAsync(TextReader fs, CancellationTokenSource tokenSource = null, int file_size = -1)
        {
            if (fs.Peek() == -1)
                return;

            if (this.LoadProgress != null)
                this.LoadProgress(this, new ProgressEventArgs(ProgressState.Start));

            try
            {
                this.Clear();
                if (file_size > 0)
                    this.buffer.Allocate(file_size);
                await this.buffer.LoadAsync(fs, tokenSource);
            }
            finally
            {
                this.PerformLayout(false);
                if (this.LoadProgress != null)
                    this.LoadProgress(this, new ProgressEventArgs(ProgressState.Complete));
            }
        }

        /// <summary>
        /// ストリームに非同期モードで保存します
        /// </summary>
        /// <param name="fs">IStreamWriterオブジェクト</param>
        /// <param name="tokenSource">キャンセルトークン</param>
        /// <returns>Taskオブジェクト</returns>
        /// <remarks>非同期操作中はこのメソッドを実行することはできません</remarks>
        public async Task SaveAsync(TextWriter fs, CancellationTokenSource tokenSource = null)
        {
            await this.buffer.SaveAsync(fs, tokenSource);
        }

        /// <summary>
        /// Find()およびReplaceAll()で使用するパラメーターをセットします
        /// </summary>
        /// <param name="pattern">検索したい文字列</param>
        /// <param name="UseRegex">正規表現を使用するなら真</param>
        /// <param name="opt">RegexOptions列挙体</param>
        public void SetFindParam(string pattern, bool UseRegex, RegexOptions opt)
        {
            this.match = null;
            if (UseRegex)
                this.regex = new Regex(pattern, opt);
            else
                this.regex = new Regex(Regex.Escape(pattern), opt);
        }

        /// <summary>
        /// 現在の検索パラメーターでWatchDogを生成する
        /// </summary>
        /// <param name="type">ハイライトタイプ</param>
        /// <param name="color">色</param>
        /// <returns>WatchDogオブジェクト</returns>
        public RegexMarkerPattern CreateWatchDogByFindParam(HilightType type,Color color)
        {
            if (this.regex == null)
                throw new InvalidOperationException("SetFindParam()を呼び出してください");
            return new RegexMarkerPattern(this.regex,type,color);
        }

        /// <summary>
        /// 指定した文字列を検索します
        /// </summary>
        /// <returns>見つかった場合はSearchResult列挙子を返却します</returns>
        /// <remarks>見つかったパターン以外を置き換えた場合、正常に動作しないことがあります</remarks>
        public IEnumerator<SearchResult> Find()
        {
            return this.Find(0, this.Length);
        }

        /// <summary>
        /// 指定した文字列を検索します
        /// </summary>
        /// <returns>見つかった場合はSearchResult列挙子を返却します</returns>
        /// <param name="start">開始インデックス</param>
        /// <param name="length">検索する長さ</param>
        /// <remarks>見つかったパターン以外を置き換えた場合、正常に動作しないことがあります</remarks>
        public IEnumerator<SearchResult> Find(int start, int length)
        {
            if (this.regex == null)
                throw new InvalidOperationException();
            if (start < 0 || start >= this.Length)
                throw new ArgumentOutOfRangeException();

            int end = start + length - 1;

            if(end > this.Length - 1)
                throw new ArgumentOutOfRangeException();

            StringBuilder line = new StringBuilder();
            int oldLength = this.Length;
            for (int i = start; i <= end; i++)
            {
                char c = this[i];
                line.Append(c);
                if (c == Document.NewLine || i == end)
                {
                    this.match = this.regex.Match(line.ToString());
                    while (this.match.Success)
                    {
                        int startIndex = i - line.Length + 1 + this.match.Index;
                        int endIndex = startIndex + this.match.Length - 1;

                        yield return new SearchResult(this.match, startIndex, endIndex);

                        if (this.Length != oldLength)   //長さが変わった場合は置き換え後のパターンの終点＋１まで戻る
                        {
                            int delta = this.Length - oldLength;
                            i = endIndex + delta;
                            end = end + delta;
                            oldLength = this.Length;
                            break;
                        }

                        this.match = this.match.NextMatch();
                    }
                    line.Clear();
                }
            }
        }

        /// <summary>
        /// 任意のパターンですべて置き換えます
        /// </summary>
        /// <param name="replacePattern">置き換え後のパターン</param>
        /// <param name="groupReplace">グループ置き換えを行うなら真。そうでないなら偽</param>
        public void ReplaceAll(string replacePattern,bool groupReplace)
        {
            if (this.regex == null)
                throw new InvalidOperationException();
            ReplaceAllCommand cmd = new ReplaceAllCommand(this.buffer, this.LayoutLines, this.regex, replacePattern, groupReplace);
            this.UndoManager.push(cmd);
            cmd.redo();
        }

        /// <summary>
        /// 任意のパターンで置き換える
        /// </summary>
        /// <param name="target">対象となる文字列</param>
        /// <param name="pattern">置き換え後の文字列</param>
        /// <param name="ci">大文字も文字を区別しないなら真。そうでないなら偽</param>
        /// <remarks>
        /// 検索時に大文字小文字を区別します。また、このメソッドでは正規表現を使用することはできません
        /// </remarks>
        public void ReplaceAll2(string target, string pattern,bool ci = false)
        {
            FastReplaceAllCommand cmd = new FastReplaceAllCommand(this.buffer, this.LayoutLines, target, pattern,ci);
            this.UndoManager.push(cmd);
            cmd.redo();
        }

        #region IEnumerable<char> メンバー

        /// <summary>
        /// 列挙子を返します
        /// </summary>
        /// <returns>IEnumeratorオブジェクトを返す</returns>
        public IEnumerator<char> GetEnumerator()
        {
            return this.buffer.GetEnumerator();
        }

        #endregion

        #region IEnumerable メンバー

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        #endregion
        void buffer_Update(object sender, DocumentUpdateEventArgs e)
        {
            switch (e.type)
            {
                case UpdateType.RebuildLayout:
                    {
                        this._LayoutLines.Clear();
                        int analyzeLength = PreloadLength;
                        if (analyzeLength > this.Length)
                            analyzeLength = this.Length;
                        this._LayoutLines.UpdateAsReplace(0, 0, analyzeLength);
                        break;
                    }
                case UpdateType.BuildLayout:
                    {
                        int analyzeLength = PreloadLength;
                        if (e.startIndex + analyzeLength > this.Length)
                            analyzeLength = this.Length - e.startIndex;
                        this._LayoutLines.UpdateAsReplace(e.startIndex, 0, analyzeLength);
                        break;
                    }
                case UpdateType.Replace:
                    if (e.row == null)
                    {
                        this._LayoutLines.UpdateAsReplace(e.startIndex, e.removeLength, e.insertLength);
                        this.Markers.UpdateMarkers(e.startIndex, e.insertLength, e.removeLength);
                    }
                    else
                    {
                        this._LayoutLines.UpdateLineAsReplace(e.row.Value, e.removeLength, e.insertLength);
                        this.Markers.UpdateMarkers(this.LayoutLines.GetIndexFromLineNumber(e.row.Value), e.insertLength, e.removeLength);
                    }
                    this.Dirty = true;
                    break;
                case UpdateType.Clear:
                    this._LayoutLines.Clear();
                    this.Dirty = true;
                    break;
            }
            if(this.FireUpdateEvent)
                this.Update(this, e);
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.buffer.Clear();
                    this.LayoutLines.Clear();
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// ドキュメントを破棄する
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    /// <summary>
    /// 検索結果を表す
    /// </summary>
    public class SearchResult
    {
        private Match Match;

        /// <summary>
        /// 一致した場所の開始位置を表す
        /// </summary>
        public int Start;

        /// <summary>
        /// 一致した場所の終了位置を表す
        /// </summary>
        public int End;

        /// <summary>
        /// 見つかった文字列を返す
        /// </summary>
        public string Value
        {
            get { return this.Match.Value; }
        }

        /// <summary>
        /// 指定したパターンを置き換えて返す
        /// </summary>
        /// <param name="replacement">置き換える文字列</param>
        /// <returns>置き換え後の文字列</returns>
        public string Result(string replacement)
        {
            return this.Match.Result(replacement);
        }

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="m">Matchオブジェクト</param>
        /// <param name="start">開始インデックス</param>
        /// <param name="end">終了インデックス</param>
        public SearchResult(Match m, int start,int end)
        {
            this.Match = m;
            this.Start = start;
            this.End = end;
        }
    }

    /// <summary>
    /// ドキュメントリーダー
    /// </summary>
    public class DocumentReader : TextReader
    {
        StringBuffer document;      
        int currentIndex;

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="doc"></param>
        internal DocumentReader(StringBuffer doc)
        {
            if (doc == null)
                throw new ArgumentNullException();
            this.document = doc;
        }

        /// <summary>
        /// 文字を取得する
        /// </summary>
        /// <returns>文字。取得できない場合は-1</returns>
        public override int Peek()
        {
            if (this.document == null)
                throw new InvalidOperationException();
            if (this.currentIndex >= this.document.Length)
                return -1;
            return this.document[this.currentIndex];
        }

        /// <summary>
        /// 文字を取得し、イテレーターを一つ進める
        /// </summary>
        /// <returns>文字。取得できない場合は-1</returns>
        public override int Read()
        {
            int c = this.Peek();
            if(c != -1)
                this.currentIndex++;
            return c;
        }

        /// <summary>
        /// 文字列を読み取りバッファーに書き込む
        /// </summary>
        /// <param name="buffer">バッファー</param>
        /// <param name="index">開始インデックス</param>
        /// <param name="count">カウント</param>
        /// <returns>読み取られた文字数</returns>
        public override int Read(char[] buffer, int index, int count)
        {
            if (this.document == null)
                throw new InvalidOperationException();

            if (buffer == null)
                throw new ArgumentNullException();

            if (this.document.Length < count)
                throw new ArgumentException();

            if (index < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (this.document.Length == 0)
                return 0;

            int actualCount = count;
            if (index + count - 1 > this.document.Length - 1)
                actualCount = this.document.Length - index;

            string str = this.document.ToString(index, actualCount);

            for (int i = 0; i < str.Length; i++)    //ToCharArray()だと戻った時に消えてしまう
                buffer[i] = str[i];

            this.currentIndex = index + actualCount;
            
            return actualCount;
        }

        /// <summary>
        /// オブジェクトを破棄する
        /// </summary>
        /// <param name="disposing">真ならアンマネージドリソースを解放する</param>
        protected override void Dispose(bool disposing)
        {
            this.document = null;
        }

    }
}
