/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using FooEditEngine;
using FooEditEngine.WPF;
using FooEditEngine.Test;
using Microsoft.Win32;

namespace Test
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        System.Threading.CancellationTokenSource cancleTokenSrc = new System.Threading.CancellationTokenSource();

        List<Document> Documents = new List<Document>();

        public MainWindow()
        {
            InitializeComponent();
            this.fooTextBox.MouseDoubleClick += new System.Windows.Input.MouseButtonEventHandler(fooTextBox_MouseDoubleClick);
            this.fooTextBox.ShowTab = true;
            this.fooTextBox.ShowHalfSpace = true;
            this.fooTextBox.ShowFullSpace = true;
            this.fooTextBox.ShowLineBreak = true;

            var complete_collection = new CompleteCollection<ICompleteItem>();
            complete_collection.Add(new CompleteWord("int"));
            complete_collection.Add(new CompleteWord("float"));
            complete_collection.Add(new CompleteWord("double"));
            complete_collection.Add(new CompleteWord("byte"));
            complete_collection.Add(new CompleteWord("char"));
            complete_collection.Add(new CompleteWord("var"));

            Document doc = this.fooTextBox.Document;
            doc.AutoComplete = new AutoCompleteBox(doc);
            doc.AutoComplete.Items = complete_collection;
            doc.AutoComplete.Enabled = true;
            doc.LayoutLines.FoldingStrategy = new CharFoldingMethod('{', '}');
            //doc.LayoutLines.FoldingStrategy = new WZTextFoldingGenerator();
            doc.Update += Document_Update;

            this.Closed += MainWindow_Closed;
        }

        void MainWindow_Closed(object sender, System.EventArgs e)
        {
            this.cancleTokenSrc.Cancel();
            this.fooTextBox.Dispose();
        }

        void fooTextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FooMouseButtonEventArgs fe = (FooMouseButtonEventArgs)e;
            foreach (Marker m in this.fooTextBox.Document.GetMarkers(MarkerIDs.URL, fe.Index))
            {
                if (m.hilight == HilightType.Url)
                {
                    MessageBox.Show(this.fooTextBox.Document.ToString(m.start, m.length));

                    fe.Handled = true;
                }
            }
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            PrintDialog pd = new PrintDialog();
            pd.PageRangeSelection = PageRangeSelection.AllPages;
            pd.UserPageRangeEnabled = true;
            if (pd.ShowDialog() == false)
                return;
            FooPrintText printtext = new FooPrintText();
            printtext.Document = this.fooTextBox.Document;
            printtext.Font = this.fooTextBox.FontFamily;
            printtext.FontSize = this.fooTextBox.FontSize;
            printtext.DrawLineNumber = this.fooTextBox.DrawLineNumber;
            printtext.Header = "header";
            printtext.Footer = "footter";
            printtext.LineBreakMethod = this.fooTextBox.LineBreakMethod;
            printtext.LineBreakCharCount = this.fooTextBox.LineBreakCharCount;
            printtext.MarkURL = true;
            printtext.Hilighter = this.fooTextBox.Hilighter;
            printtext.Foreground = this.fooTextBox.Foreground;
            printtext.URL = this.fooTextBox.URL;
            printtext.Comment = this.fooTextBox.Comment;
            printtext.Keyword1 = this.fooTextBox.Keyword1;
            printtext.Keyword2 = this.fooTextBox.Keyword2;
            printtext.Litral = this.fooTextBox.Literal;
            printtext.FlowDirection = this.fooTextBox.FlowDirection;
            if (pd.PageRangeSelection == PageRangeSelection.AllPages)
            {
                printtext.StartPage = -1;
                printtext.EndPage = -1;
            }
            else
            {
                printtext.StartPage = pd.PageRange.PageFrom;
                printtext.EndPage = pd.PageRange.PageTo;
            }
            printtext.PageRect = new Rect(0,0,pd.PrintableAreaWidth, pd.PrintableAreaHeight);
            printtext.Print(pd);
        }

        private async void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            bool result = (bool)ofd.ShowDialog(this);
            if (result == true)
            {
                this.fooTextBox.IsEnabled = false;
                System.IO.FileStream file = System.IO.File.Open(ofd.FileName,System.IO.FileMode.Open);
                System.IO.StreamReader sr = new System.IO.StreamReader(file, Encoding.Default);
                await this.fooTextBox.Document.LoadAsync(sr, this.cancleTokenSrc, (int)file.Length);
                sr.Close();
                file.Close();
                this.fooTextBox.IsEnabled = true;
                this.fooTextBox.Refresh();
            }
        }

        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            Document doc = this.fooTextBox.Document;
            doc.Insert(0,"this is a pen");
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(0, 4, HilightType.Sold));
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(8, 1, HilightType.Select, new FooEditEngine.Color(255, 0, 255, 0)));
            doc.SetMarker(MarkerIDs.Defalut, Marker.Create(10, 3, HilightType.Squiggle, new FooEditEngine.Color(255, 255, 0, 0)));
            this.fooTextBox.Refresh();
        }

        private void MenuItem_Click_7(object sender, RoutedEventArgs e)
        {
            if (this.fooTextBox.Hilighter == null)
            {
                this.fooTextBox.Hilighter = new XmlHilighter();
                this.fooTextBox.LayoutLineCollection.HilightAll();
            }
            this.fooTextBox.Refresh();
        }

        private void MenuItem_Click_8(object sender, RoutedEventArgs e)
        {
            if (this.fooTextBox.Hilighter != null)
            {
                this.fooTextBox.Hilighter = null;
                this.fooTextBox.LayoutLineCollection.ClearHilight();
            }
            this.fooTextBox.Refresh();
        }

        private void MenuItem_Click_9(object sender, RoutedEventArgs e)
        {
            this.fooTextBox.LayoutLineCollection.GenerateFolding();
            this.fooTextBox.Refresh();
        }

        private async void MenuItem_Click_10(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            bool result = (bool)sfd.ShowDialog(this);
            if (result == true)
            {
                await this.fooTextBox.SaveFile(sfd.FileName,Encoding.Default,"\r\n",cancleTokenSrc);
                MessageBox.Show("complete");
            }
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Stopwatch time = new System.Diagnostics.Stopwatch();
            time.Start();
            this.fooTextBox.Document.FireUpdateEvent = false;
            this.fooTextBox.Document.ReplaceAll2(this.FindPattern.Text, this.ReplacePattern.Text,true);
            this.fooTextBox.Document.FireUpdateEvent = true;
            time.Stop();
            this.fooTextBox.Refresh();
            MessageBox.Show(string.Format("complete elpased time:{0}s",time.ElapsedMilliseconds/1000.0f));
        }

        IEnumerator<SearchResult> it;
        private void Find_Click(object sender, RoutedEventArgs e)
        {
            const int findID = 2;
            if (it == null)
            {
                this.fooTextBox.Document.SetFindParam(this.FindPattern.Text, false, System.Text.RegularExpressions.RegexOptions.None);
                var dog = this.fooTextBox.Document.CreateWatchDogByFindParam(HilightType.Select,new FooEditEngine.Color(64,128,128,128));
                this.fooTextBox.MarkerPatternSet.Remove(findID);
                this.fooTextBox.MarkerPatternSet.Add(findID, dog);
                this.it = this.fooTextBox.Document.Find();
            }
            this.it.MoveNext();
            if (this.it.Current != null)
            {
                SearchResult sr = this.it.Current;
                this.fooTextBox.JumpCaret(sr.Start);
                this.fooTextBox.Selection = new FooEditEngine.TextRange(sr.Start, sr.End - sr.Start + 1);
                this.fooTextBox.Refresh();
            }
        }

        private void FindPattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.it = null;
        }

        void Document_Update(object sender, DocumentUpdateEventArgs e)
        {
            this.it = null;
        }

        private void MenuItem_Click_11(object sender, RoutedEventArgs e)
        {
            this.fooTextBox.Padding = new Thickness(20);
            this.fooTextBox.Refresh();
        }

        private void MenuItem_Click_16(object sender, RoutedEventArgs e)
        {
            if(this.fooTextBox.LineBreakMethod != LineBreakMethod.CharUnit)
            {
                this.fooTextBox.LineBreakMethod = LineBreakMethod.CharUnit;
                this.fooTextBox.LineBreakCharCount = 10;
                this.fooTextBox.PerfomLayouts();
                this.fooTextBox.Refresh();
            }
        }
        private void MenuItem_Click_17(object sender, RoutedEventArgs e)
        {
            if (this.fooTextBox.LineBreakMethod != LineBreakMethod.PageBound)
            {
                this.fooTextBox.LineBreakMethod = LineBreakMethod.PageBound;
                this.fooTextBox.PerfomLayouts();
                this.fooTextBox.Refresh();
            }
        }
        private void MenuItem_Click_18(object sender, RoutedEventArgs e)
        {
            if (this.fooTextBox.LineBreakMethod != LineBreakMethod.None)
            {
                this.fooTextBox.LineBreakMethod = LineBreakMethod.None;
                this.fooTextBox.PerfomLayouts();
                this.fooTextBox.Refresh();
            }
        }

        private void MenuItem_Click12(object sender, RoutedEventArgs e)
        {
            if (this.fooTextBox.IndentMode == IndentMode.Space)
            {
                this.fooTextBox.IndentMode = IndentMode.Tab;
            }
            else
            {
                this.fooTextBox.IndentMode = IndentMode.Space;
            }
            this.fooTextBox.Refresh();
        }

        private void Jamp_Click(object sender, RoutedEventArgs e)
        {
            this.fooTextBox.JumpCaret(int.Parse(this.JumpRow.Text), 0);
        }
    }
    public class FlowDirectionConveter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            FlowDirection flow = (FlowDirection)value;
            return flow == FlowDirection.RightToLeft;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool is_rtl = (bool)value;
            return is_rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
    }
    public class TextRangeConveter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            FooEditEngine.TextRange range = (FooEditEngine.TextRange)value;
            return string.Format("Index:{0} Length:{1}", range.Index, range.Length);
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
