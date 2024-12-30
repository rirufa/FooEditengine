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
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FooEditEngine;

namespace UnitTest
{
    [TestClass]
    public class DocumentTest
    {
        [TestMethod]
        public void InsertSingleLineTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            DummyView view = new DummyView(doc, render);
            doc.Clear();
            doc.Append("a\nb\nc\nd");
            Assert.IsTrue(view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "b\n" &&
                view.LayoutLines[2] == "c\n" &&
                view.LayoutLines[3] == "d");

            doc.Insert(2, "x");
            Assert.IsTrue(view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "xb\n" &&
                view.LayoutLines[2] == "c\n" &&
                view.LayoutLines[3] == "d");

            doc.Insert(3, "x");
            Assert.IsTrue(view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "xxb\n" &&
                view.LayoutLines[2] == "c\n" &&
                view.LayoutLines[3] == "d");

            doc.Insert(6, "x");
            Assert.IsTrue(view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "xxb\n" &&
                view.LayoutLines[2] == "xc\n" &&
                view.LayoutLines[3] == "d");

            doc.Insert(0, "x");
            Assert.IsTrue(view.LayoutLines[0] == "xa\n" &&
                view.LayoutLines[1] == "xxb\n" &&
                view.LayoutLines[2] == "xc\n" &&
                view.LayoutLines[3] == "d");

        }

        [TestMethod]
        public void InsertMultiLineTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            DummyView view = new DummyView(doc, render);

            doc.Clear();
            doc.Append("a\nb\nc\nd");

            doc.Insert(2, "f\ne");
            Assert.IsTrue(
                view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "f\n" &&
                view.LayoutLines[2] == "eb\n" &&
                view.LayoutLines[3] == "c\n" &&
                view.LayoutLines[4] == "d");

            doc.Insert(3, "g\nh");
            Assert.IsTrue(
                view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "fg\n" &&
                view.LayoutLines[2] == "h\n" &&
                view.LayoutLines[3] == "eb\n" &&
                view.LayoutLines[4] == "c\n" &&
                view.LayoutLines[5] == "d");

            doc.Insert(0, "x\ny");
            Assert.IsTrue(
                view.LayoutLines[0] == "x\n" &&
                view.LayoutLines[1] == "ya\n" &&
                view.LayoutLines[2] == "fg\n" &&
                view.LayoutLines[3] == "h\n" &&
                view.LayoutLines[4] == "eb\n" &&
                view.LayoutLines[5] == "c\n" &&
                view.LayoutLines[6] == "d");
        }

        [TestMethod]
        public void RemoveSingleLineTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            DummyView view = new DummyView(doc, render);
            doc.Clear();
            doc.Append("aa\nbb\ncc\ndd");

            doc.Remove(9, 1);
            Assert.IsTrue(
                view.LayoutLines[0] == "aa\n" &&
                view.LayoutLines[1] == "bb\n" &&
                view.LayoutLines[2] == "cc\n" &&
                view.LayoutLines[3] == "d"
                );

            doc.Remove(9, 1);
            Assert.IsTrue(
                view.LayoutLines[0] == "aa\n" &&
                view.LayoutLines[1] == "bb\n" &&
                view.LayoutLines[2] == "cc\n" &&
                view.LayoutLines[3] == ""
                );

            doc.Remove(0, 1);
            Assert.IsTrue(
                view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "bb\n" &&
                view.LayoutLines[2] == "cc\n" &&
                view.LayoutLines[3] == ""
                );
        }

        [TestMethod]
        public void RemoveMultiLineTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            DummyView view = new DummyView(doc, render);

            doc.Clear();
            doc.Append("a\n");
            doc.Append("b\n");
            doc.Append("c\n");
            doc.Append("d\n");
            doc.Append("e\n");
            doc.Append("f\n");

            doc.Remove(2, 4);
            Assert.IsTrue(
                view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "d\n" &&
                view.LayoutLines[2] == "e\n" &&
                view.LayoutLines[3] == "f\n");

            doc.Remove(4, 4);
            Assert.IsTrue(
                view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "d\n");

            doc.Clear();
            doc.Append("a\n");
            doc.Append("b\n");
            doc.Append("c\n");
            doc.Append("d\n");

            doc.Remove(2, 6);
            Assert.IsTrue(view.LayoutLines[0] == "a\n");

            doc.Clear();
            doc.Append("a\n");
            doc.Append("b\n");
            doc.Append("c\n");
            doc.Append("d\n");
            doc.Append("e\n");
            doc.Append("f\n");
            doc.Insert(4, "a");
            doc.Remove(2, 5);
            Assert.IsTrue(
                view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "d\n" &&
                view.LayoutLines[2] == "e\n" &&
                view.LayoutLines[3] == "f\n");
        }

        [TestMethod]
        public void QueryTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            DummyView view = new DummyView(doc, render);
            doc.Clear();
            doc.Append("a\nb\nc");

            Assert.IsTrue(view.LayoutLines.GetIndexFromLineNumber(1) == 2);
            Assert.IsTrue(view.LayoutLines.GetLengthFromLineNumber(1) == 2);
            Assert.IsTrue(view.LayoutLines.GetLineNumberFromIndex(2) == 1);
            TextPoint tp = view.LayoutLines.GetTextPointFromIndex(2);
            Assert.IsTrue(tp.row == 1 && tp.col == 0);
            Assert.IsTrue(view.LayoutLines.GetIndexFromTextPoint(tp) == 2);

            doc.Insert(2, "a");

            Assert.IsTrue(view.LayoutLines.GetIndexFromLineNumber(2) == 5);
            Assert.IsTrue(view.LayoutLines.GetLineNumberFromIndex(5) == 2);
            tp = view.LayoutLines.GetTextPointFromIndex(5);
            Assert.IsTrue(tp.row == 2 && tp.col == 0);
            Assert.IsTrue(view.LayoutLines.GetIndexFromTextPoint(tp) == 5);

            doc.Insert(0, "a");

            Assert.IsTrue(view.LayoutLines.GetIndexFromLineNumber(2) == 6);
            Assert.IsTrue(view.LayoutLines.GetLineNumberFromIndex(6) == 2);
            tp = view.LayoutLines.GetTextPointFromIndex(6);
            Assert.IsTrue(tp.row == 2 && tp.col == 0);
            Assert.IsTrue(view.LayoutLines.GetIndexFromTextPoint(tp) == 6);
        }

        [TestMethod]
        public void FindTest1()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("is this a pen");
            doc.SetFindParam("is", false, RegexOptions.None);
            IEnumerator<SearchResult> it = doc.Find();
            it.MoveNext();
            SearchResult sr = it.Current;
            Assert.IsTrue(sr.Start == 0 && sr.End == 1);
            it.MoveNext();
            sr = it.Current;
            Assert.IsTrue(sr.Start == 5 && sr.End == 6);
        }

        [TestMethod]
        public void FindTest2()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("is this a pen");
            doc.SetFindParam("is", false, RegexOptions.None);
            IEnumerator<SearchResult> it = doc.Find(3,4);
            it.MoveNext();
            SearchResult sr = it.Current;
            Assert.IsTrue(sr.Start == 5 && sr.End == 6);
        }

        [TestMethod]
        public void FindTest3()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("is this a pen");
            doc.SetFindParam("is", false, RegexOptions.None);
            IEnumerator<SearchResult> it = doc.Find(0, 8);
            it.MoveNext();
            SearchResult sr = it.Current;
            doc.Replace(sr.Start, sr.End - sr.Start + 1, "aaa");
            it.MoveNext();
            sr = it.Current;
            Assert.IsTrue(sr.Start == 6 && sr.End == 7);
        }

        [TestMethod]
        public void ReaderTest1()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("a");
            DocumentReader reader = doc.CreateReader();
            Assert.IsTrue(reader.Read() == 'a');
            Assert.IsTrue(reader.Peek() == -1);
        }

        [TestMethod]
        public void ReaderTest2()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("abc");
            DocumentReader reader = doc.CreateReader();
            char[] buf = new char[2];
            int count = reader.Read(buf,1,2);
            Assert.IsTrue(buf[0] == 'b' && buf[1] == 'c');
            Assert.IsTrue(count == 2);
            Assert.IsTrue(reader.Peek() == -1);
        }

        [TestMethod]
        public void GetLinesText()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("a\nb\nc");
            var result = doc.GetLines(0, doc.Length - 1).ToArray();
            Assert.AreEqual("a\n", result[0]);
            Assert.AreEqual("b\n", result[1]);
            Assert.AreEqual("c", result[2]);
        }

        [TestMethod]
        public void FetchLineAndTryGetRaw()
        {
            DummyRender render = new DummyRender();
            Document.PreloadLength = 64;
            Document doc = new Document();
            doc.LayoutLines.Render = render;

            for (int i = 0; i < 20; i++)
                doc.Append("01234567890123456789\n");

            //普通に追加すると余計なものがあるので、再構築する
            doc.PerformLayout(false);

            LineToIndexTableData lineData;
            bool result = doc.LayoutLines.TryGetRaw(20, out lineData);
            Assert.AreEqual(false, result);
            Assert.AreEqual(null, lineData);

            doc.Append("a\nb\nc");
            doc.LayoutLines.FetchLine(23);
            Assert.AreEqual("a\n", doc.LayoutLines[20]);
            Assert.AreEqual("c", doc.LayoutLines[22]);
        }

        [TestMethod]
        public void ReplaceNonRegexAllTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("this is a pen\n");
            doc.Append("this is a pen\n");
            doc.SetFindParam("is", false, RegexOptions.None);
            doc.ReplaceAll("aaa", false);
            Assert.IsTrue(doc.ToString(0) == "thaaa aaa a pen\nthaaa aaa a pen\n");
        }

        [TestMethod]
        public void ReplaceRegexAllTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("this is a pen\n");
            doc.Append("this is a pen\n");
            doc.SetFindParam("[a-z]+", true, RegexOptions.None);
            doc.ReplaceAll("aaa", false);
            Assert.IsTrue(doc.ToString(0) == "aaa aaa aaa aaa\naaa aaa aaa aaa\n");
        }

        [TestMethod]
        public void ReplaceAllTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("this is a pen");
            doc.ReplaceAll2("is", "aaa");
            Assert.IsTrue(doc.ToString(0) == "thaaa aaa a pen");
        }

        [TestMethod]
        public void MarkerTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("this is a pen");
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(0, 4, HilightType.Sold));

            var markers = doc.Markers.Get(MarkerIDs.Defalut);
            foreach(var m in markers)
                Assert.IsTrue(m.start == 0 && m.length == 4);

            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(5, 2, HilightType.Sold));
            doc.RemoveMarker(MarkerIDs.Defalut, 0, 4);
            markers = doc.Markers.Get(MarkerIDs.Defalut);
            foreach (var m in markers)
                Assert.IsTrue(m.start == 5 && m.length == 2);

            doc.Insert(5, "a");
            markers = doc.Markers.Get(MarkerIDs.Defalut, 0);
            foreach (var m in markers)
                Assert.IsTrue(m.start == 6 && m.length == 2);

            doc.Insert(10, "a");
            markers = doc.Markers.Get(MarkerIDs.Defalut, 0);
            foreach (var m in markers)
                Assert.IsTrue(m.start == 6 && m.length == 2);

            doc.SetMarker(MarkerIDs.URL, Marker.Create(0, 4, HilightType.Sold));
            doc.Markers.Clear(MarkerIDs.Defalut);
            foreach (int id in doc.Markers.IDs)
            {
                markers = doc.Markers.Get(id, 0);
                foreach (var m in markers)
                    Assert.IsTrue(m.start == 0 && m.length == 4);
            }
        }

        [TestMethod]
        public void MultiMakerTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            doc.Append("this");
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(0, 2, HilightType.Sold));
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(2, 2, HilightType.Dash));
            var markers = doc.Markers.Get(MarkerIDs.Defalut).ToArray();
            Assert.IsTrue(markers[0].start == 0 && markers[0].length == 2 && markers[0].hilight == HilightType.Sold);
            Assert.IsTrue(markers[1].start == 2 && markers[1].length == 2 && markers[1].hilight == HilightType.Dash);

            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(0, 2, HilightType.Dash));
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(2, 2, HilightType.Sold));
            markers = doc.Markers.Get(MarkerIDs.Defalut).ToArray();
            Assert.IsTrue(markers[0].start == 0 && markers[0].length == 2 && markers[0].hilight == HilightType.Dash);
            Assert.IsTrue(markers[1].start == 2 && markers[1].length == 2 && markers[1].hilight == HilightType.Sold);
        }

        [TestMethod]
        public void WatchDogTest()
        {
            RegexMarkerPattern dog = new RegexMarkerPattern(new Regex("h[a-z]+"), HilightType.Url,new Color(0,0,0,255));
            IEnumerable<Marker> result = new List<Marker>(){
                Marker.Create(0,4,HilightType.Url,new Color(0,0,0,255)),
                Marker.Create(5,4,HilightType.Url,new Color(0,0,0,255))
            };
            string str = "html haml";

            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            DummyView view = new DummyView(doc, render);
            doc.MarkerPatternSet.Add(MarkerIDs.Defalut, dog);
            doc.Clear();
            doc.Append(str);
            IEnumerable<Marker> actual = doc.MarkerPatternSet.GetMarkers(new CreateLayoutEventArgs(0, str.Length, str));
            this.AreEqual(result, actual);
        }

        [TestMethod]
        public void CallAutoIndentHookWhenEnter()
        {
            bool called = false;
            Document doc = new Document();
            doc.AutoIndentHook += (s, e) => { called = true; };
            doc.Replace(0, 0, Document.NewLine.ToString(), true);
            Assert.AreEqual(true, called);

            called = false;
            doc.Replace(0, 0, "aaa", true);
            Assert.AreEqual(false, called);
        }

        [TestMethod]
        public void SaveAndLoadFile()
        {
            const string content = "aaaa";
            Document doc = new Document();
            doc.Append(content);

            System.IO.StreamWriter sw = new System.IO.StreamWriter("test.txt");
            System.Threading.Tasks.Task t = doc.SaveAsync(sw);
            t.Wait();
            sw.Close();

            doc.Clear();

            System.IO.StreamReader sr = new System.IO.StreamReader("test.txt");
            t = doc.LoadAsync(sr);
            t.Wait();
            sr.Close();

            Assert.AreEqual(content, doc.ToString(0));
        }

        void AreEqual<T>(IEnumerable<T> t1, IEnumerable<T> t2)
        {
            Assert.AreEqual(t1.Count(), t2.Count());

            IEnumerator<T> e1 = t1.GetEnumerator();
            IEnumerator<T> e2 = t2.GetEnumerator();

            while (e1.MoveNext() && e2.MoveNext())
            {
                Assert.AreEqual(e1.Current, e2.Current);
            }
        }
    }
}
