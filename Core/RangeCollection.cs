/* https://github.com/mbuchetics/RangeTree よりコピペ。このファイルのみMITライセンスに従います */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Slusser.Collections.Generic;

namespace FooEditEngine
{
    /// <summary>
    /// マーカーを表す
    /// </summary>
    public interface IRange
    {
        /// <summary>
        /// マーカーの開始位置。-1を設定した場合、そのマーカーはレタリングされません。正しい先頭位置を取得するにはGetLineHeadIndex()を使用してください
        /// </summary>
        int start { get; set; }
        /// <summary>
        /// マーカーの長さ。0を設定した場合、そのマーカーはレタリングされません
        /// </summary>
        int length { get; set; }
    }

    public class RangeCollection<T> : IEnumerable<T>
        where T : IRange
    {
        private protected GapBuffer<T> collection;
        protected int stepRow = -1, stepLength = 0;
        protected const int STEP_ROW_IS_NONE = -1;

        public RangeCollection()
            : this(null)
        {
        }

        public RangeCollection(IEnumerable<T> collection)
        {
            this.collection = new GapBuffer<T>();
            if (collection != null)
                this.collection.AddRange(collection);
        }

        public T this[int i]
        {
            get
            {
                return this.collection[i];
            }
            set
            {
                this.collection[i] = value;
            }
        }

        public int Count
        {
            get
            {
                return this.collection.Count;
            }
        }

        public void Add(T item)
        {
            this.CommiteChange();
            this.collection.Add(item);
            for (int i = this.collection.Count - 1; i >= 0; i--)
            {
                if (i > 0 && this.collection[i].start < this.collection[i - 1].start)
                {
                    T temp = this.collection[i];
                    this.collection[i] = this.collection[i - 1];
                    this.collection[i - 1] = temp;
                }
                else
                {
                    break;
                }
            }
        }

        public void ReplaceRange(int startRow, IList<T> new_collection, int removeCount, int deltaLength)
        {
            //消すべき行が複数ある場合は消すが、そうでない場合は最適化のため長さを変えるだけにとどめておく
            if (removeCount == 1 && new_collection != null && new_collection.Count == 1)
            {
                this.collection[startRow] = new_collection.First();
            }
            else
            {
                if(typeof(T) == typeof(IDisposable))
                {
                    for (int i = startRow; i < startRow + removeCount; i++)
                    {
                        IDisposable item = (IDisposable)this.collection[i];
                        item.Dispose();
                    }
                }

                //行を挿入する
                this.collection.RemoveRange(startRow, removeCount);

                if (new_collection != null)
                {
                    int newCount = new_collection.Count;
                    if (this.stepRow > startRow && newCount > 0 && newCount != removeCount)
                    {
                        //stepRowは1か2のうち、大きな方になる
                        // 1.stepRow - (削除された行数 - 挿入された行数)
                        // 2.行の挿入箇所
                        //行が削除や置換された場合、1の処理をしないと正しいIndexが求められない
                        this.stepRow = Math.Max(this.stepRow - (removeCount - newCount), startRow);
#if DEBUG
                        if (this.stepRow < 0 || this.stepRow > this.collection.Count + newCount)
                        {
                            DebugLog.WriteLine(DebugLogLevel.Important,"step row < 0 or step row >= lines.count");
                            System.Diagnostics.Debugger.Break();
                        }
#endif
                    }

                    //startRowが挿入した行の開始位置なのであらかじめ引いておく
                    for (int i = 1; i < new_collection.Count; i++)
                    {
                        if (this.stepRow != STEP_ROW_IS_NONE && startRow + i > this.stepRow)
                            new_collection[i].start -= deltaLength + this.stepLength;
                        else
                            new_collection[i].start -= deltaLength;
                    }
                    this.collection.InsertRange(startRow, new_collection);
                }
            }

            //行テーブルを更新する
            this.UpdateStartIndex(deltaLength, startRow);
        }

        public void Remove(int start, int length)
        {
            if (this.collection.Count == 0)
                return;

            int at = this.IndexOf(start);
            int endAt = this.IndexOf(start + length - 1);

            int startRow = 0;
            int removeCount = 0;

            if(at != -1 && endAt != -1)
            {
                startRow = at;
                removeCount = endAt - at + 1;
            }
            else if (at != -1)
            {
                startRow = at;
                removeCount = 1;
            }
            else if(endAt != -1)
            {
                startRow = endAt;
                removeCount = 1;
            }
            else
            {
                return;
            }
            this.ReplaceRange(startRow, null, removeCount, 0);
            this.UpdateStartIndex(0,startRow);
        }

        public void RemoveAt(int startRow)
        {
            this.ReplaceRange(startRow, null, 1, 0);
            this.UpdateStartIndex(0, startRow);
        }

        public int IndexOf(int start)
        {
            int dummy;
            return this.IndexOfNearest(start, out dummy);
        }

        public int IndexOfLoose(int start)
        {
            int dummy;
            int result = this.IndexOfNearest(start, out dummy);
            if(result == -1)
            {
                int lastRow = this.collection.Count - 1;
                var line = this.collection[lastRow];
                var lineHeadIndex = this.GetLineHeadIndex(lastRow);
                if (start >= lineHeadIndex && start <= lineHeadIndex + line.length)   //最終行長+1までキャレットが移動する可能性があるので
                {
                    lastLineNumber = this.collection.Count - 1;
                    return lastLineNumber;
                }
            }
            return result;
        }

        int lastLineNumber;
        int IndexOfNearest(int start,out int nearIndex)
        {
            if (start < 0)
                throw new ArgumentOutOfRangeException("indexに負の値を設定することはできません");

            nearIndex = -1;
            if (this.collection.Count == 0)
                return -1;

            if (start == 0 && this.collection.Count > 0)
                return 0;

            T line;
            int lineHeadIndex;

            if (lastLineNumber < this.collection.Count - 1)
            {
                line = this.collection[lastLineNumber];
                lineHeadIndex = this.GetLineHeadIndex(lastLineNumber);
                if (start >= lineHeadIndex && start < lineHeadIndex + line.length)
                    return lastLineNumber;
            }

            int left = 0, right = this.collection.Count - 1, mid;
            while (left <= right)
            {
                mid = (left + right) / 2;
                line = this.collection[mid];
                lineHeadIndex = this.GetLineHeadIndex(mid);
                if (start >= lineHeadIndex && start < lineHeadIndex + line.length)
                {
                    lastLineNumber = mid;
                    return mid;
                }
                if (start < lineHeadIndex)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }

            System.Diagnostics.Debug.Assert(left >= 0 || right >= 0);
            nearIndex = left >= 0 ? left : right;
            if (nearIndex > this.collection.Count - 1)
                nearIndex = right;

            return -1;
        }

        public IEnumerable<T> Get(int index)
        {
            //TODO:インデックスがおかしくなってる可能性がある
            int at = this.IndexOf(index);
            if (at == -1)
                yield break;
            yield return this.collection[at];
        }

        public IEnumerable<T> Get(int start, int length)
        {
            //TODO:インデックスがおかしくなってる可能性がある
            int nearAt;
            int at = this.IndexOfNearest(start,out nearAt);
            if (at == -1)
                at = nearAt;

            if (at == -1)
                yield break;

            int end = start + length - 1;
            for (int i = at; i < this.collection.Count; i++)
            {
                int markerEnd = this.collection[i].start + this.collection[i].length - 1;
                if (this.collection[i].start >= start && markerEnd <= end ||
                    markerEnd >= start && markerEnd <= end ||
                    this.collection[i].start >= start && this.collection[i].start <= end ||
                    this.collection[i].start < start && markerEnd > end)
                    yield return this.collection[i];
                else if (this.collection[i].start > start + length)
                    yield break;
            }
        }

        public virtual void Clear()
        {
            this.collection.Clear();
            this.stepRow = STEP_ROW_IS_NONE;
            this.stepLength = 0;
            DebugLog.WriteLine("Clear");
        }

        public void UpdateStartIndex(int deltaLength, int startRow)
        {
            if (this.collection.Count == 0)
            {
                this.stepRow = STEP_ROW_IS_NONE;
                this.stepLength = 0;
                return;
            }

            if (this.stepRow == STEP_ROW_IS_NONE)
            {
                this.stepRow = startRow;
                this.stepLength = deltaLength;
                return;
            }


            if (startRow < this.stepRow)
            {
                //ドキュメントの後半部分をごっそり削除した場合、this.stepRow >= this.Lines.Countになる可能性がある
                if (this.stepRow >= this.collection.Count)
                    this.stepRow = this.collection.Count - 1;
                for (int i = this.stepRow; i > startRow; i--)
                    this.collection[i].start -= this.stepLength;
            }
            else if (startRow > this.stepRow)
            {
                for (int i = this.stepRow + 1; i < startRow; i++)
                    this.collection[i].start += this.stepLength;
            }

            this.stepRow = startRow;
            this.stepLength += deltaLength;
        }

        /// <summary>
        /// 今までの変更をすべて反映させる
        /// </summary>
        public void CommiteChange()
        {
            for (int i = this.stepRow + 1; i < this.collection.Count; i++)
                this.collection[i].start += this.stepLength;

            this.stepRow = STEP_ROW_IS_NONE;
            this.stepLength = 0;
        }

        /// <summary>
        /// 当該行の先頭インデックスを取得する
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public int GetLineHeadIndex(int row)
        {
            if (this.collection.Count == 0)
                return 0;
            if (this.stepRow != STEP_ROW_IS_NONE && row > this.stepRow)
                return this.collection[row].start + this.stepLength;
            else
                return this.collection[row].start;
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (T item in this.collection)
                yield return item;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }

}
