using System;
using System.Linq;

namespace FooEditEngine
{
    /// <summary>
    /// イベントパラメーター
    /// </summary>
    public sealed class ShowingCompleteBoxEventArgs : EventArgs
    {
        /// <summary>
        /// 入力された文字
        /// </summary>
        public string KeyChar;
        /// <summary>
        /// 入力した単語と一致したコレクションのインデックス。一致しないなら-1をセットする
        /// </summary>
        public int foundIndex;
        /// <summary>
        /// 入力しようとした単語を設定する
        /// </summary>
        public string inputedWord;
        /// <summary>
        /// 補完対象のテキストボックス
        /// </summary>
        public Document textbox;
        /// <summary>
        /// キャレット座標
        /// </summary>
        public Point CaretPostion;
        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="keyChar"></param>
        /// <param name="textbox"></param>
        /// <param name="caret_pos"></param>
        public ShowingCompleteBoxEventArgs(string keyChar, Document textbox, Point caret_pos)
        {
            this.inputedWord = null;
            this.KeyChar = keyChar;
            this.foundIndex = -1;
            this.textbox = textbox;
            this.CaretPostion = caret_pos;
        }
    }

    /// <summary>
    /// イベントパラメーター
    /// </summary>
    public sealed class SelectItemEventArgs : EventArgs
    {
        /// <summary>
        /// 入力中の単語
        /// </summary>
        public string inputing_word;
        /// <summary>
        /// 補完対象のテキストボックス
        /// </summary>
        public Document textbox;
        /// <summary>
        /// 補完候補
        /// </summary>
        public ICompleteItem item;
        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="item"></param>
        /// <param name="inputing_word"></param>
        /// <param name="textbox"></param>
        public SelectItemEventArgs(ICompleteItem item, string inputing_word, Document textbox)
        {
            this.item = item;
            this.inputing_word = inputing_word;
            this.textbox = textbox;
        }
    }

    /// <summary>
    /// イベントパラメーターイベントパラメーター
    /// </summary>
    public sealed class CollectCompleteItemEventArgs : EventArgs
    {
        /// <summary>
        /// 入力された行
        /// </summary>
        public int InputedRow;
        /// <summary>
        /// 補完対象のテキストボックス
        /// </summary>
        public Document textbox;
        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="textbox"></param>
        public CollectCompleteItemEventArgs(Document textbox)
        {
            this.textbox = textbox;
            this.InputedRow = textbox.CaretPostion.row - 1;
            if (this.InputedRow < 0)
                this.InputedRow = 0;
        }
    }

    /// <summary>
    /// イベントパンドラーの定義
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void SelectItemEventHandler(object sender,SelectItemEventArgs e);
    /// <summary>
    /// イベントパンドラーの定義
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void ShowingCompleteBoxEnventHandler(object sender, ShowingCompleteBoxEventArgs e);

    /// <summary>
    /// 自動補完のベースクラス
    /// </summary>
    public class AutoCompleteBoxBase
    {
        const int InputLength = 2;  //補完を開始する文字の長さ

        /// <summary>
        /// 対象となるドキュメント
        /// </summary>
        protected Document Document
        {
            get;
            private set;
        }

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="document">対象となるDocumentWindow</param>
        public AutoCompleteBoxBase(Document document)
        {
            this.SelectItem = (s, e) => {
                string inputing_word = e.inputing_word;
                string word = e.item.word;

                var doc = e.textbox;
                //キャレットは入力された文字の後ろにあるので、一致する分だけ選択して置き換える
                int caretIndex = doc.LayoutLines.GetIndexFromTextPoint(e.textbox.CaretPostion);
                int start = caretIndex - inputing_word.Length;
                if (start < 0)
                    start = 0;
                doc.Replace(start, inputing_word.Length, word);
                doc.RequestRedraw();
            };
            this.ShowingCompleteBox = (s, e) => {
                AutoCompleteBoxBase box = (AutoCompleteBoxBase)s;

                var doc = e.textbox;
                int caretIndex = doc.LayoutLines.GetIndexFromTextPoint(e.textbox.CaretPostion);
                int inputingIndex = caretIndex - 1;
                if (inputingIndex < 0)
                    inputingIndex = 0;

                e.inputedWord = CompleteHelper.GetWord(doc, inputingIndex, box.Operators) + e.KeyChar;

                if (e.inputedWord == null)
                    return;

                for (int i = 0; i < box.Items.Count; i++)
                {
                    CompleteWord item = (CompleteWord)box.Items[i];
                    if (item.word.StartsWith(e.inputedWord))
                    {
                        e.foundIndex = i;
                        break;
                    }
                }
            };
            this.CollectItems = (s, e) =>
            {
                AutoCompleteBoxBase box = (AutoCompleteBoxBase)s;
                CompleteHelper.AddCompleteWords(box.Items, box.Operators, e.textbox.LayoutLines[e.InputedRow]);
            };
            this.Operators = new char[] { ' ', '\t', Document.NewLine };
            this.Document = document;
        }

        internal void ParseInput(string input_text)
        {
            if (this.Operators == null ||
                this.ShowingCompleteBox == null ||
                (this.IsCloseCompleteBox == false && input_text == "\b"))
                return;

            if (input_text == "\r" || input_text == "\n")
            {
                this.CollectItems(this, new CollectCompleteItemEventArgs(this.Document));
                return;
            }

            this.OpenCompleteBox(input_text);
        }

        /// <summary>
        /// 補完候補を追加可能な時に発生するイベント
        /// </summary>
        public EventHandler<CollectCompleteItemEventArgs> CollectItems;
        /// <summary>
        /// 補完すべき単語が選択されたときに発生するイベント
        /// </summary>
        public SelectItemEventHandler SelectItem;
        /// <summary>
        /// UI表示前のイベント
        /// </summary>
        public ShowingCompleteBoxEnventHandler ShowingCompleteBox;

        /// <summary>
        /// 区切り文字のリスト
        /// </summary>
        public char[] Operators
        {
            get;
            set;
        }

        /// <summary>
        /// オートコンプリートの対象となる単語のリスト
        /// </summary>
        public virtual CompleteCollection<ICompleteItem> Items
        {
            get;
            set;
        }

        internal Func<TextPoint,Document, Point> GetPostion;

        /// <summary>
        /// 自動補完リストが表示されているかどうか
        /// </summary>
        protected virtual bool IsCloseCompleteBox
        {
            get;
        }

        /// <summary>
        /// 自動補完を行うかどうか。行うなら真
        /// </summary>
        public bool Enabled
        {
            get;
            set;
        }

        /// <summary>
        /// 補完候補の表示要求を処理する
        /// </summary>
        /// <param name="ev"></param>
        protected virtual void RequestShowCompleteBox(ShowingCompleteBoxEventArgs ev)
        {
        }

        /// <summary>
        /// 補完候補の非表示要求を処理する
        /// </summary>
        protected virtual void RequestCloseCompleteBox()
        {
        }

        /// <summary>
        /// 補完候補を表示する
        /// </summary>
        /// <param name="key_char">入力しようとしていた文字列</param>
        /// <param name="force">補完候補がなくても表示するなら真。そうでないなら偽</param>
        public void OpenCompleteBox(string key_char, bool force = false)
        {
            if (!this.Enabled)
                return;

            if (this.GetPostion == null)
                throw new InvalidOperationException("GetPostionがnullです");
            Point p = this.GetPostion(this.Document.CaretPostion,this.Document);

            ShowingCompleteBoxEventArgs ev = new ShowingCompleteBoxEventArgs(key_char, this.Document, p);
            ShowingCompleteBox(this, ev);

            bool hasCompleteItem = ev.foundIndex != -1 && ev.inputedWord != null && ev.inputedWord != string.Empty && ev.inputedWord.Length >= InputLength;
            DebugLog.WriteLine(String.Format("hasCompleteItem:{0}", hasCompleteItem));
            if (force || hasCompleteItem)
            {
                RequestShowCompleteBox(ev);
            }
            else
            {
                RequestCloseCompleteBox();
            }
        }

    }
}
