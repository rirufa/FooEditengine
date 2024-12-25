/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
//#define TEST_ASYNC
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Nito.AsyncEx;
using System.Threading;
using System.Threading.Tasks;
using Slusser.Collections.Generic;

namespace FooEditEngine
{
    /// <summary>
    /// ランダムアクセス可能な列挙子を提供するインターフェイス
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRandomEnumrator<T>
    {
        /// <summary>
        /// インデクサーを表す
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>Tを返す</returns>
        T this[int index] { get; }
    }

    sealed class StringBuffer : IEnumerable<char>, IRandomEnumrator<char>
    {
        GapBuffer<char> buf = new GapBuffer<char>();
        const int MaxSemaphoreCount = 1;
        AsyncReaderWriterLock rwlock = new AsyncReaderWriterLock();

        public StringBuffer()
        {
            this.Update = (s, e) => { };
        }

        public StringBuffer(StringBuffer buffer)
            : this()
        {
            buf.AddRange(buffer.buf);
        }


        public char this[int index]
        {
            get
            {
                char c = buf[index];
                return c;
            }
        }

        public string ToString(int index, int length)
        {
            StringBuilder temp = new StringBuilder();
            temp.Clear();
            using (this.rwlock.ReaderLock())
            {
                for (int i = index; i < index + length; i++)
                    temp.Append(buf[i]);
            }
            return temp.ToString();
        }

        public int Length
        {
            get { return this.buf.Count; }
        }

        internal DocumentUpdateEventHandler Update;

        internal void Allocate(int count)
        {
            this.buf.Allocate(count);
        }

        internal void Replace(StringBuffer buf)
        {
            this.Replace(buf.buf);
        }

        internal void Replace(GapBuffer<char> buf)
        {
            using (this.rwlock.WriterLock())
            {
                this.Clear();
                this.buf = buf;
            }

            this.Update(this, new DocumentUpdateEventArgs(UpdateType.Replace, 0, 0, buf.Count));
        }

        internal void Replace(int index, int length, IEnumerable<char> chars, int count)
        {
            using (this.rwlock.WriterLock())
            {
                if (length > 0)
                    this.buf.RemoveRange(index, length);
                this.buf.InsertRange(index, chars);
            }
            this.Update(this, new DocumentUpdateEventArgs(UpdateType.Replace, index, length, count));
        }

        internal async Task LoadAsync(TextReader fs, CancellationTokenSource tokenSource = null)
        {
            char[] str = new char[1024 * 1024];
            int readCount;
            do
            {
                readCount = await fs.ReadAsync(str, 0, str.Length).ConfigureAwait(false);

                //内部形式に変換する
                var internal_str = from s in str where s != '\r' && s != '\0' select s;

                using (await this.rwlock.WriterLockAsync())
                {
                    //str.lengthは事前に確保しておくために使用するので影響はない
                    this.buf.AddRange(internal_str);
                }

                if (tokenSource != null)
                    tokenSource.Token.ThrowIfCancellationRequested();
#if TEST_ASYNC
                DebugLog.WriteLine("waiting now");
                await Task.Delay(100).ConfigureAwait(false);
#endif
                Array.Clear(str, 0, str.Length);
            } while (readCount > 0);
        }

        internal async Task SaveAsync(TextWriter fs, CancellationTokenSource tokenSource = null)
        {
            using(await this.rwlock.ReaderLockAsync())
            {
                StringBuilder line = new StringBuilder();
                for (int i = 0; i < this.Length; i++)
                {
                    char c = this[i];
                    line.Append(c);
                    if (c == Document.NewLine || i == this.Length - 1)
                    {
                        string str = line.ToString();
                        str = str.Replace(Document.NewLine.ToString(), fs.NewLine);
                        await fs.WriteAsync(str).ConfigureAwait(false);
                        line.Clear();
                        if (tokenSource != null)
                            tokenSource.Token.ThrowIfCancellationRequested();
#if TEST_ASYNC
                        System.Threading.Thread.Sleep(10);
#endif
                    }
                }
            }
        }

        internal void ReplaceRegexAll(LineToIndexTable layoutlines, Regex regex, string pattern, bool groupReplace)
        {
            for (int i = 0; i < layoutlines.Count; i++)
            {
                int lineHeadIndex = layoutlines.GetIndexFromLineNumber(i), lineLength = layoutlines.GetLengthFromLineNumber(i);
                int left = lineHeadIndex, right = lineHeadIndex;
                string output;

                output = regex.Replace(layoutlines[i], (m) => {
                    if (groupReplace)
                        return m.Result(pattern);
                    else
                        return pattern;
                });

                using (this.rwlock.WriterLock())
                {
                    //空行は削除する必要はない
                    if (lineHeadIndex < this.buf.Count)
                        this.buf.RemoveRange(lineHeadIndex, lineLength);
                    this.buf.InsertRange(lineHeadIndex, output);
                }

                this.Update(this, new DocumentUpdateEventArgs(UpdateType.Replace, lineHeadIndex, lineLength, output.Length, i));
            }
        }

        internal void ReplaceAll(LineToIndexTable layoutlines, string target, string pattern, bool ci = false)
        {
            TextSearch ts = new TextSearch(target, ci);
            char[] pattern_chars = pattern.ToCharArray();
            for (int i = 0; i < layoutlines.Count; i++)
            {
                int lineHeadIndex = layoutlines.GetIndexFromLineNumber(i), lineLength = layoutlines.GetLengthFromLineNumber(i);
                int left = lineHeadIndex, right = lineHeadIndex;
                int newLineLength = lineLength;
                while ((right = ts.IndexOf(this.buf, left, lineHeadIndex + newLineLength)) != -1)
                {
                    using (this.rwlock.WriterLock())
                    {
                        this.buf.RemoveRange(right, target.Length);
                        this.buf.InsertRange(right, pattern_chars);
                    }
                    left = right + pattern.Length;
                    newLineLength += pattern.Length - target.Length;
                }

                this.Update(this, new DocumentUpdateEventArgs(UpdateType.Replace, lineHeadIndex, lineLength, newLineLength, i));
            }
        }

        internal int IndexOf(string target, int start, bool ci = false)
        {
            using (this.rwlock.ReaderLock())
            {
                TextSearch ts = new TextSearch(target, ci);
                int patternIndex = ts.IndexOf(this.buf, start, this.buf.Count);
                return patternIndex;
            }
        }

        /// <summary>
        /// 文字列を削除する
        /// </summary>
        internal void Clear()
        {
            this.buf.Clear();
            this.buf.TrimExcess();
            this.Update(this, new DocumentUpdateEventArgs(UpdateType.Clear, 0, this.buf.Count, 0));
        }

        internal IEnumerable<char> GetEnumerator(int start, int length)
        {
            for (int i = start; i < start + length; i++)
                yield return this.buf[i];
        }

        #region IEnumerable<char> メンバー

        public IEnumerator<char> GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this.buf[i];
        }

        #endregion

        #region IEnumerable メンバー

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this.Length; i++)
                yield return this[i];
        }

        #endregion
    }

    sealed class TextSearch
    {
        char[] pattern;
        int patternLength;
        Dictionary<char, int> qsTable = new Dictionary<char, int>();
        bool caseInsenstive;
        public TextSearch(string pattern, bool ci = false)
        {
            this.patternLength = pattern.Length;
            this.caseInsenstive = ci;
            if (ci)
            {
                this.CreateQSTable(pattern.ToLower());
                this.CreateQSTable(pattern.ToUpper());
                this.pattern = new char[pattern.Length];
                for (int i = 0; i < pattern.Length; i++)
                    this.pattern[i] = CharTool.ToUpperFastIf(pattern[i]);
            }
            else
            {
                this.CreateQSTable(pattern);
                this.pattern = pattern.ToCharArray();
            }
        }
        void CreateQSTable(string pattern)
        {
            int len = pattern.Length;
            for (int i = 0; i < len; i++)
            {
                if (!this.qsTable.ContainsKey(pattern[i]))
                    this.qsTable.Add(pattern[i], len - i);
                else
                    this.qsTable[pattern[i]] = len - i;
            }
        }
        public int IndexOf(GapBuffer<char> buf, int start, int end)
        {
            //QuickSearch法
            int buflen = buf.Count - 1;
            int plen = this.patternLength;
            int i = start;
            int search_end = end - plen;
            //最適化のためわざとコピペした
            if (this.caseInsenstive)
            {
                while (i <= search_end)
                {
                    int j = 0;
                    while (j < plen)
                    {
                        if (CharTool.ToUpperFastIf(buf[i + j]) != this.pattern[j])
                            break;
                        j++;
                    }
                    if (j == plen)
                    {
                        return i;
                    }
                    else
                    {
                        int k = i + plen;
                        if (k <= buflen)	//buffer以降にアクセスする可能性がある
                        {
                            int moveDelta;
                            if (this.qsTable.TryGetValue(buf[k], out moveDelta))
                                i += moveDelta;
                            else
                                i += plen;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

            }
            else
            {
                while (i <= search_end)
                {
                    int j = 0;
                    while (j < plen)
                    {
                        if (buf[i + j] != this.pattern[j])
                            break;
                        j++;
                    }
                    if (j == plen)
                    {
                        return i;
                    }
                    else
                    {
                        int k = i + plen;
                        if (k <= buflen)	//buffer以降にアクセスする可能性がある
                        {
                            int moveDelta;
                            if (this.qsTable.TryGetValue(buf[k], out moveDelta))
                                i += moveDelta;
                            else
                                i += plen;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            return -1;
        }
    }
    static class CharTool
    {
        /// <summary>
        /// Converts characters to lowercase.
        /// </summary>
        const string _lookupStringL =
        "---------------------------------!-#$%&-()*+,-./0123456789:;<=>?@abcdefghijklmnopqrstuvwxyz[-]^_`abcdefghijklmnopqrstuvwxyz{|}~-";

        /// <summary>
        /// Converts characters to uppercase.
        /// </summary>
        const string _lookupStringU =
        "---------------------------------!-#$%&-()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[-]^_`ABCDEFGHIJKLMNOPQRSTUVWXYZ{|}~-";

        /// <summary>
        /// Get lowercase version of this ASCII character.
        /// </summary>
        public static char ToLower(char c)
        {
            return _lookupStringL[c];
        }

        /// <summary>
        /// Get uppercase version of this ASCII character.
        /// </summary>
        public static char ToUpper(char c)
        {
            return _lookupStringU[c];
        }

        /// <summary>
        /// Translate uppercase ASCII characters to lowercase.
        /// </summary>
        public static char ToLowerFastIf(char c)
        {
            if (c >= 'A' && c <= 'Z')
            {
                return (char)(c + 32);
            }
            else
            {
                return c;
            }
        }

        /// <summary>
        /// Translate lowercase ASCII characters to uppercase.
        /// </summary>
        public static char ToUpperFastIf(char c)
        {
            if (c >= 'a' && c <= 'z')
            {
                return (char)(c - 32);
            }
            else
            {
                return c;
            }
        }
    }
}