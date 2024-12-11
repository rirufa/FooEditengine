/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FooEditEngine;

namespace UnitTest
{
    [TestClass]
    public class ControllerTest
    {
        [TestMethod]
        public void SelectByWordTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("this is a pen");
            doc.SelectWord(0);
            Assert.IsTrue(ctrl.SelectedText == "this");
        }

        [TestMethod]
        public void ConvertToChar()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("U0030");
            doc.Select(0,5);
            ctrl.ConvertToChar();
            Assert.IsTrue(doc.ToString(0) == "0");
        }

        [TestMethod]
        public void ConvertToCodePoint()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("0");
            doc.Select(0, 1);
            ctrl.ConvertToCodePoint();
            Assert.IsTrue(doc.ToString(0) == "U30 ");
        }

        [TestMethod()]
        public void DeSelectAllTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("0");
            doc.Select(0, 1);
            ctrl.DeSelectAll();
            Assert.IsTrue(doc.Selections.Count == 0);
        }

        [TestMethod()]
        public void IsMarkerTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Append("this is a pen");
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(0, 4, HilightType.Sold));
            Assert.IsTrue(ctrl.IsMarker(new TextPoint(0, 0), HilightType.Sold) == true);
            Assert.IsTrue(ctrl.IsMarker(0, HilightType.Sold) == true);
            Assert.IsTrue(ctrl.IsMarker(new TextPoint(0, 5), HilightType.Sold) == false);
            Assert.IsTrue(ctrl.IsMarker(5, HilightType.Sold) == false);
        }

        [TestMethod()]
        public void AdjustCaretTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Append("this is a pen");
            doc.Select(0, 1);
            int old_sel_start = ctrl.SelectionStart;
            int old_sel_length = ctrl.SelectionLength;
            TextPoint old_caret = doc.CaretPostion;
            ctrl.AdjustCaret();
            Assert.IsTrue(old_sel_start == ctrl.SelectionStart);
            Assert.IsTrue(old_sel_length == ctrl.SelectionLength);
            Assert.IsTrue(old_caret == doc.CaretPostion);
        }

        [TestMethod()]
        public void JumpCaretTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Append("1\n2\n3\n4");
            doc.LayoutLines.FoldingCollection.Add(new FoldingItem(2, 6, false));
            ctrl.JumpCaret(4);
            Assert.IsTrue(doc.CaretPostion == new TextPoint(2, 0));
            var folding = doc.LayoutLines.FoldingCollection.Get(2, 6);
            Assert.IsTrue(folding.Expand == true);
            folding.Expand = false;
            ctrl.JumpCaret(0);
            ctrl.JumpCaret(4,false);
            Assert.IsTrue(folding.Expand == false);
            ctrl.JumpCaret(1, 0);
            Assert.IsTrue(doc.CaretPostion == new TextPoint(1, 0));
        }

        [TestMethod()]
        [Ignore]
        public void ScrollByPixelTest()
        {
            Assert.Fail("目視で確認すること");
        }

        [TestMethod()]
        [Ignore]
        public void ScrollTest()
        {
            Assert.Fail("目視で確認すること");
        }

        [TestMethod()]
        public void GetNextCaretTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Append("1234\n1234");
            ctrl.JumpCaret(0);
            TextPoint nextCaret;
            int moved;
            nextCaret = ctrl.GetNextCaret(doc.CaretPostion, 1, MoveFlow.Character, out moved);
            Assert.IsTrue(nextCaret.row == 0 && nextCaret.col == 1);
            Assert.IsTrue(moved == 1);
            nextCaret = ctrl.GetNextCaret(doc.CaretPostion, 1, MoveFlow.Word, out moved);
            Assert.IsTrue(nextCaret.row == 0 && nextCaret.col == 4);
            Assert.IsTrue(moved == 1);
            nextCaret = ctrl.GetNextCaret(doc.CaretPostion, 1, MoveFlow.Paragraph, out moved);
            Assert.IsTrue(nextCaret.row == 1 && nextCaret.col == 0);
            Assert.IsTrue(moved == 1);
            nextCaret = ctrl.GetNextCaret(doc.CaretPostion, 1, MoveFlow.Line, out moved);
            Assert.IsTrue(nextCaret.row == 1 && nextCaret.col == 0);
            Assert.IsTrue(moved == 1);
        }

        [TestMethod()]
        public void DoDeleteActionTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Append("1234");
            ctrl.JumpCaret(0);
            ctrl.DoDeleteAction();
            Assert.IsTrue(doc[0] == '2');
            doc.Select(0, 1);
            ctrl.DoDeleteAction();
            Assert.IsTrue(doc[0] == '3');
        }

        [TestMethod()]
        public void DoEnterActionTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Append("1234");
            ctrl.JumpCaret(1);
            ctrl.DoEnterAction();
            Assert.IsTrue(doc[1] == '\n');
        }

        [TestMethod()]
        public void MoveCaretAndSelectTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("abc def\nef");
            ctrl.JumpCaret(0);
            ctrl.MoveCaretAndSelect(new TextPoint(0, 1));
            Assert.IsTrue(ctrl.SelectionStart == 0 && ctrl.SelectionLength == 1);
            Assert.IsTrue(doc.CaretPostion.row == 0 && doc.CaretPostion.col == 1);
            ctrl.JumpCaret(0);
            ctrl.MoveCaretAndSelect(new TextPoint(0, 1),true);
            Assert.IsTrue(ctrl.SelectionStart == 0 && ctrl.SelectionLength == 3);
            Assert.IsTrue(doc.CaretPostion.row == 0 && doc.CaretPostion.col == 3);
        }

        [TestMethod()]
        [Ignore]
        public void MoveCaretAndGripperTest()
        {
            Assert.Fail("目視で確認すること");
        }

        [TestMethod]
        public void CaretTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("abc\nef");
            ctrl.JumpCaret(1);
            Assert.IsTrue(ctrl.SelectionStart == 1);
            ctrl.JumpToLineHead(0, false);
            Assert.IsTrue(ctrl.SelectionStart == 0);
            ctrl.JumpToLineEnd(0,false);
            Assert.IsTrue(ctrl.SelectionStart == 3);
            ctrl.JumpToHead(false);
            Assert.IsTrue(ctrl.SelectionStart == 0);
            ctrl.JumpToEnd(false);
            Assert.IsTrue(ctrl.SelectionStart == 4);

            doc.Clear();
            doc.Append("a c\ndef");
            ctrl.JumpCaret(0);
            ctrl.MoveCaretHorizontical(4, false, false);
            Assert.IsTrue(ctrl.SelectionStart == 4);
            ctrl.MoveCaretHorizontical(-4, false, false);
            Assert.IsTrue(ctrl.SelectionStart == 0);
            ctrl.MoveCaretHorizontical(-1, false, false);
            Assert.IsTrue(ctrl.SelectionStart == 0);    //ドキュメントの先端を超えることはないはず
            ctrl.MoveCaretHorizontical(1, false, true);
            Assert.IsTrue(ctrl.SelectionStart == 2);

            ctrl.JumpCaret(0);
            ctrl.MoveCaretVertical(1, false);
            Assert.IsTrue(ctrl.SelectionStart == 4);
            ctrl.MoveCaretVertical(-1, false);
            Assert.IsTrue(ctrl.SelectionStart == 0);
            ctrl.MoveCaretVertical(-1, false);
            Assert.IsTrue(ctrl.SelectionStart == 0);    //ドキュメントの先端を超えることはないはず
        }

        [TestMethod]
        public void LineModeEditTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("abc");
            ctrl.JumpCaret(0);
            ctrl.DoDeleteAction();
            Assert.IsTrue(doc.ToString(0) == "bc");
            ctrl.JumpCaret(1);
            ctrl.DoBackSpaceAction();
            Assert.IsTrue(doc.ToString(0) == "c");
            ctrl.DoInputChar('a');
            Assert.IsTrue(doc.ToString(0) == "ac");
            doc.Select(0, 2);
            ctrl.DoInputString("xb");
            Assert.IsTrue(doc.ToString(0) == "xb");
            doc.InsertMode = false;
            ctrl.JumpCaret(0);
            ctrl.DoInputChar('a');
            Assert.IsTrue(doc.ToString(0) == "ab");
            doc.Append("\n");
            ctrl.JumpCaret(2);
            ctrl.DoInputChar('a');
            Assert.IsTrue(doc.LayoutLines[0] == "aba\n");

            doc.Clear();
            doc.Append("a\na");
            doc.Select(0, 3);
            ctrl.UpIndent();
            Assert.IsTrue(doc.ToString(0) == "\ta\n\ta\n");
            ctrl.DownIndent();
            Assert.IsTrue(doc.ToString(0) == "a\na\n");
        }

        [TestMethod]
        public void SelectTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("a\nb\nc");
            doc.Select(0, 5);
            Assert.IsTrue(ctrl.SelectedText == "a\r\nb\r\nc");
        }

        [TestMethod]
        public void ReplaceSelectionTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("a\nb\nc");
            doc.Select(0, 5);
            ctrl.SelectedText = "a";
            doc.Select(0, 1);
            Assert.IsTrue(ctrl.SelectedText == "a");
        }

        [TestMethod]
        public void SelectByRectTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            string str = "aa\nbb\ncc";
            doc.Append(str);
            ctrl.RectSelection = true;
            doc.Select(0,7);
            Assert.IsTrue(ctrl.SelectedText == "a\r\nb\r\nc\r\n");
        }

        [TestMethod]
        public void RectEditTest()
        {
            DummyRender render = new DummyRender();
            Document doc = new Document();
            doc.LayoutLines.Render = render;
            EditView view = new EditView(doc, render);
            Controller ctrl = new Controller(doc, view);
            doc.Clear();
            doc.Append("a\nb\nc");
            ctrl.RectSelection = true;
            doc.Select(0, 5);
            ctrl.DoInputString("x",true);
            Assert.IsTrue(
                view.LayoutLines[0] == "x\n" &&
                view.LayoutLines[1] == "x\n" &&
                view.LayoutLines[2] == "x");
            Assert.IsTrue(
                view.Selections[0].start == 0 &&
                view.Selections[1].start == 2 &&
                view.Selections[2].start == 4);

            ctrl.DoInputString("x", true);
            Assert.IsTrue(
                view.Selections[0].start == 1 &&
                view.Selections[1].start == 4 &&
                view.Selections[2].start == 7);

            doc.Clear();
            doc.Append("a\nb\nc");
            doc.Select(0, 4);
            Assert.IsTrue(ctrl.IsRectInsertMode() == true);
            ctrl.DoInputString("x");
            Assert.IsTrue(
                view.LayoutLines[0] == "xa\n" &&
                view.LayoutLines[1] == "xb\n" &&
                view.LayoutLines[2] == "xc");

            ctrl.DoBackSpaceAction();
            Assert.IsTrue(
                view.LayoutLines[0] == "a\n" &&
                view.LayoutLines[1] == "b\n" &&
                view.LayoutLines[2] == "c");
        }
    }
}
