/*
 * Copyright (C) 2013 FooProject
 * * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources.Core;
using Windows.System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Text;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.UI.Text.Core;

// テンプレート コントロールのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=234235 を参照してください
namespace FooEditEngine.WinUI
{
    /// <summary>
    /// テキストボックスコントロール
    /// </summary>
    public sealed class FooTextBox : Control, IDisposable
    {
        EditView _View;
        Controller _Controller;
#if !DUMMY_RENDER
        Win2DRender Render;
#else
        DummyRender Render;
#endif
        ScrollBar horizontalScrollBar, verticalScrollBar;
        Microsoft.UI.Xaml.Shapes.Rectangle rectangle;
        GestureRecognizer gestureRecongnizer = new GestureRecognizer();
        CoreTextEditContext textEditContext;
        CoreTextServicesManager textServiceManager;
#if ENABLE_AUTMATION
        FooTextBoxAutomationPeer peer;
#endif
        bool nowCaretMove = false;
        bool nowCompstion = false;
        bool requestSizeChange = false;
        Document _Document;
        DispatcherTimer timer = new DispatcherTimer();

        const int Interval = 32;
        const int IntervalWhenLostFocus = 160;

        /// <summary>
        /// コンストラクター
        /// </summary>
        public FooTextBox()
        {
            this.DefaultStyleKey = typeof(FooTextBox);

            this.rectangle = new Microsoft.UI.Xaml.Shapes.Rectangle();
            this.rectangle.Margin = this.Padding;
#if !DUMMY_RENDER
            this.Render = new Win2DRender(this);
#else
            this.Render = new DummyRender();
#endif
            this.Render.InitTextFormat(this.FontFamily.Source, (float)this.FontSize);

            this.Document = new Document();

            this._View = new EditView(this.Document, this.Render, new Padding(5, 5, Gripper.HitAreaWidth / 2, Gripper.HitAreaWidth / 2));
            this._View.SrcChanged += View_SrcChanged;
            this._View.InsertMode = this.InsertMode;
            this.Document.DrawLineNumber = this.DrawLineNumber;
            this._View.HideCaret = !this.DrawCaret;
            this._View.HideLineMarker = !this.DrawCaretLine;
            this.Document.HideRuler = !this.DrawRuler;
            this.Document.UrlMark = this.MarkURL;
            this.Document.TabStops = this.TabChars;

            this._Controller = new Controller(this.Document, this._View);

            this.gestureRecongnizer.GestureSettings = GestureSettings.Drag |
                GestureSettings.RightTap |
                GestureSettings.Tap |
                GestureSettings.DoubleTap |
                GestureSettings.ManipulationTranslateX |
                GestureSettings.ManipulationTranslateY |
                GestureSettings.ManipulationScale |
                GestureSettings.ManipulationTranslateInertia |
                GestureSettings.ManipulationScaleInertia;
            this.gestureRecongnizer.RightTapped += gestureRecongnizer_RightTapped;
            this.gestureRecongnizer.Tapped += gestureRecongnizer_Tapped;
            this.gestureRecongnizer.Dragging += gestureRecongnizer_Dragging;
            this.gestureRecongnizer.ManipulationInertiaStarting += GestureRecongnizer_ManipulationInertiaStarting; ;
            this.gestureRecongnizer.ManipulationStarted += gestureRecongnizer_ManipulationStarted;
            this.gestureRecongnizer.ManipulationUpdated += gestureRecongnizer_ManipulationUpdated;
            this.gestureRecongnizer.ManipulationCompleted += gestureRecongnizer_ManipulationCompleted;

            this.timer.Interval = new TimeSpan(0, 0, 0, 0, Interval);
            this.timer.Tick += this.timer_Tick;
            this.timer.Start();

            this.GettingFocus += FooTextBox_GettingFocus;
            this.LosingFocus += FooTextBox_LosingFocus;

            this.SizeChanged += FooTextBox_SizeChanged;

            this.Loaded += FooTextBox_Loaded;

            this.RegisterPropertyChangedCallback(Control.FlowDirectionProperty, InheritanceDependecyPropertyCallback);
            this.RegisterPropertyChangedCallback(Control.FontFamilyProperty, InheritanceDependecyPropertyCallback);
            this.RegisterPropertyChangedCallback(Control.FontSizeProperty, InheritanceDependecyPropertyCallback);
            this.RegisterPropertyChangedCallback(Control.FontWeightProperty, InheritanceDependecyPropertyCallback);
            this.RegisterPropertyChangedCallback(Control.ForegroundProperty, InheritanceDependecyPropertyCallback);
            this.RegisterPropertyChangedCallback(Control.BackgroundProperty, InheritanceDependecyPropertyCallback);

            this.CharacterReceived += (s, e) => {
                if (e.Handled)
                    return;
                var c = e.Character;
                if(!Char.IsControl(c))
                {
                    this.Controller.DoInputChar(c);
                    this.Refresh();
                }
            };
        }


        /// <summary>
        /// ファイナライザー
        /// </summary>
        ~FooTextBox()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// アンマネージドリソースを解放する
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool Disposed = false;
        private void Dispose(bool disposing)
        {
            if (this.Disposed)
                return;
            if (disposing)
            {
                this._View.Dispose();
            }
            this.Document.Clear();
            this.Disposed = true;
        }

        /// <summary>
        /// オーナーウィンドウ
        /// </summary>
        public static Window OwnerWindow
        {
            get;
            set;
        }

        /// <summary>
        /// ドキュメントを選択する
        /// </summary>
        /// <param name="start">開始インデックス</param>
        /// <param name="length">長さ</param>
        public void Select(int start, int length)
        {
            this.Document.Select(start, length);
        }

        /// <summary>
        /// キャレットを指定した行に移動させます
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <remarks>このメソッドを呼び出すと選択状態は解除されます</remarks>
        public void JumpCaret(int index)
        {
            this._Controller.JumpCaret(index);
        }
        /// <summary>
        /// キャレットを指定した行と桁に移動させます
        /// </summary>
        /// <param name="row">行番号</param>
        /// <param name="col">桁</param>
        /// <remarks>このメソッドを呼び出すと選択状態は解除されます</remarks>
        public void JumpCaret(int row, int col)
        {
            this._Controller.JumpCaret(row, col);
        }

        /// <summary>
        /// 選択中のテキストをクリップボードにコピーします
        /// </summary>
        public void Copy()
        {
            string text = this._Controller.SelectedText;
            if (text != null && text != string.Empty)
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.RequestedOperation = DataPackageOperation.Copy;
                dataPackage.SetText(text);

                Clipboard.SetContent(dataPackage); 
            }
        }

        /// <summary>
        /// 選択中のテキストをクリップボードに切り取ります
        /// </summary>
        public void Cut()
        {
            string text = this._Controller.SelectedText;
            if (text != null && text != string.Empty)
            {
                DataPackage dataPackage = new DataPackage();
                dataPackage.RequestedOperation = DataPackageOperation.Copy;
                dataPackage.SetText(text);

                Clipboard.SetContent(dataPackage);
                
                this._Controller.SelectedText = "";
            }
        }

        /// <summary>
        /// 選択中のテキストを貼り付けます
        /// </summary>
        public async Task PasteAsync()
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                try
                {
                    this._Controller.SelectedText = await dataPackageView.GetTextAsync();
                }catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("past error:" + e.Message);
                }
            }
        }

        /// <summary>
        /// すべて選択する
        /// </summary>
        public void SelectAll()
        {
            this.Document.Select(0, this.Document.Length);
        }

        /// <summary>
        /// 選択を解除する
        /// </summary>
        public void DeSelectAll()
        {
            this._Controller.DeSelectAll();
        }

        /// <summary>
        /// 対応する座標を返します
        /// </summary>
        /// <param name="tp">テキストポイント</param>
        /// <returns>座標</returns>
        /// <remarks>テキストポイントがクライアント領域の原点より外にある場合、返される値は原点に丸められます</remarks>
        public Windows.Foundation.Point GetPostionFromTextPoint(TextPoint tp)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            return this._View.GetPostionFromTextPoint(tp);
        }

        /// <summary>
        /// 対応するテキストポイントを返します
        /// </summary>
        /// <param name="p">クライアント領域の原点を左上とする座標</param>
        /// <returns>テキストポイント</returns>
        public TextPoint GetTextPointFromPostion(Windows.Foundation.Point p)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            return this._View.GetTextPointFromPostion(p);
        }

        /// <summary>
        /// 行の高さを取得します
        /// </summary>
        /// <param name="row">レイアウト行</param>
        /// <returns>行の高さ</returns>
        public double GetLineHeight(int row)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            return this._View.LayoutLines.GetLayout(row).Height; ;
        }

        /// <summary>
        /// インデックスに対応する座標を得ます
        /// </summary>
        /// <param name="index">インデックス</param>
        /// <returns>座標を返す</returns>
        public Windows.Foundation.Point GetPostionFromIndex(int index)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            TextPoint tp = this._View.GetLayoutLineFromIndex(index);
            return this._View.GetPostionFromTextPoint(tp);
        }

        /// <summary>
        /// 座標からインデックスに変換します
        /// </summary>
        /// <param name="p">座標</param>
        /// <returns>インデックスを返す</returns>
        public int GetIndexFromPostion(Windows.Foundation.Point p)
        {
            if (this.Document.FireUpdateEvent == false)
                throw new InvalidOperationException("");
            TextPoint tp = this._View.GetTextPointFromPostion(p);
            return this._View.GetIndexFromLayoutLine(tp);
        }
        

        /// <summary>
        /// 再描写する
        /// </summary>
        internal void Refresh(bool immidately=true)
        {
            if(immidately)
                this.Refresh(this._View.PageBound);
            else
                this.Document.RequestRedraw();
        }

        /// <summary>
        /// レイアウト行をすべて破棄し、再度レイアウトを行う
        /// </summary>
        public void PerfomLayouts()
        {
            this.Document.PerformLayout();
        }

        /// <summary>
        /// 指定行までスクロールする
        /// </summary>
        /// <param name="row">行</param>
        /// <param name="alignTop">指定行を画面上に置くなら真。そうでないなら偽</param>
        public void ScrollIntoView(int row, bool alignTop)
        {
            this._View.ScrollIntoView(row, alignTop);
        }

        /// <summary>
        /// ファイルからドキュメントを構築する
        /// </summary>
        /// <param name="sr">StremReader</param>
        /// <returns>Taskオブジェクト</returns>
        public async Task LoadFileAsync(System.IO.StreamReader sr, System.Threading.CancellationTokenSource token)
        {
            await this.Document.LoadAsync(sr, token);
        }

        private void Document_LoadProgress(object sender, ProgressEventArgs e)
        {
            if(e.state == ProgressState.Start)
            {
                this.IsEnabled = false;
            }
            else if(e.state == ProgressState.Complete)
            {
                CoreTextRange modified_range = new CoreTextRange();
                modified_range.StartCaretPosition = 0;
                modified_range.EndCaretPosition = 0;
                //キャレット位置はロード前と同じにしないと違和感を感じる
                if (this.textEditContext != null)
                    this.textEditContext.NotifyTextChanged(modified_range, this.Document.Length, modified_range);

                if (this.verticalScrollBar != null)
                    this.verticalScrollBar.Maximum = this._View.LayoutLines.Count;
                this.IsEnabled = true;
                this.Refresh(false);
            }
        }

        /// <summary>
        /// ドキュメントの内容をファイルに保存する
        /// </summary>
        /// <param name="sw">StreamWriter</param>
        /// <param name="enc">エンコード</param>
        /// <returns>Taskオブジェクト</returns>
        public async Task SaveFile(System.IO.StreamWriter sw, System.Threading.CancellationTokenSource token)
        {
            await this.Document.SaveAsync(sw, token);
        }

#region command
        void CopyCommand()
        {
            this.Copy();
        }

        void CutCommand()
        {
            this.Cut();
            this.Refresh();
        }

        async Task PasteCommand()
        {
            await this.PasteAsync();
            this.Refresh();
        }

#endregion

#region event

#if ENABLE_AUTMATION
        /// <inheritdoc/>
        protected override Microsoft.UI.Xaml.Automation.Peers.AutomationPeer OnCreateAutomationPeer()
        {
            this.peer = new FooTextBoxAutomationPeer(this);
            return this.peer;
        }
#endif

        /// <inheritdoc/>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Grid grid = this.GetTemplateChild("PART_Grid") as Grid;
            if (grid != null)
            {
                Grid.SetRow(this.rectangle, 0);
                Grid.SetColumn(this.rectangle, 0);
                grid.Children.Add(this.rectangle);
            }

            this.horizontalScrollBar = this.GetTemplateChild("PART_HorizontalScrollBar") as ScrollBar;
            if (this.horizontalScrollBar != null)
            {
                this.horizontalScrollBar.SmallChange = 10;
                this.horizontalScrollBar.LargeChange = 100;
                this.horizontalScrollBar.Maximum = this.horizontalScrollBar.LargeChange + 1;
                this.horizontalScrollBar.Scroll += new ScrollEventHandler(horizontalScrollBar_Scroll);
            }
            this.verticalScrollBar = this.GetTemplateChild("PART_VerticalScrollBar") as ScrollBar;
            if (this.verticalScrollBar != null)
            {
                this.verticalScrollBar.SmallChange = 1;
                this.verticalScrollBar.LargeChange = 10;
                this.verticalScrollBar.Maximum = this._View.LayoutLines.Count;
                this.verticalScrollBar.Scroll += new ScrollEventHandler(verticalScrollBar_Scroll);
            }
        }

        private void FooTextBox_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            this.RemoveTextContext();
            
            if (this.textServiceManager != null)
            {
                this.textServiceManager.InputLanguageChanged -= TextServiceManager_InputLanguageChanged;
                this.textServiceManager = null;
            }

            System.Diagnostics.Debug.WriteLine("losing focus");
        }

        private async void FooTextBox_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("getting focus");
            if (this.textServiceManager == null)
            {
                await Task.Delay(500);
                this.textServiceManager = CoreTextServicesManager.GetForCurrentView();
                this.textServiceManager.InputLanguageChanged += TextServiceManager_InputLanguageChanged;
            }

            this.CreateTextContext();
        }

        /// <inheritdoc/>
        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

            System.Diagnostics.Debug.WriteLine("got focus");

            this._View.IsFocused = true;
            this.timer.Interval = new TimeSpan(0, 0, 0, 0, Interval);
            this.Refresh(false);
        }

        private void TextServiceManager_InputLanguageChanged(CoreTextServicesManager sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("input language changed input script:"+  this.textServiceManager.InputLanguage.Script);
        }

　      /// <inheritdoc/>
        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            System.Diagnostics.Debug.WriteLine("lost focus");
            
            this._View.IsFocused = false;
            this.Refresh(false);
            this.timer.Interval = new TimeSpan(0, 0, 0, 0, IntervalWhenLostFocus);
        }

        private void CreateTextContext()
        {
            if(this.textServiceManager != null)
            {
                this.textEditContext = this.textServiceManager.CreateEditContext();
                this.textEditContext.InputScope = CoreTextInputScope.Default;
                this.textEditContext.CompositionStarted += TextEditContext_CompositionStarted;
                this.textEditContext.CompositionCompleted += TextEditContext_CompositionCompleted;
                this.textEditContext.LayoutRequested += TextEditContext_LayoutRequested;
                this.textEditContext.TextUpdating += TextEditContext_TextUpdating;
                this.textEditContext.TextRequested += TextEditContext_TextRequested;
                this.textEditContext.SelectionRequested += TextEditContext_SelectionRequested;
                this.textEditContext.SelectionUpdating += TextEditContext_SelectionUpdating;
                this.textEditContext.FormatUpdating += TextEditContext_FormatUpdating;
                this.textEditContext.FocusRemoved += TextEditContext_FocusRemoved;
                this.textEditContext.NotifyFocusLeaveCompleted += TextEditContext_NotifyFocusLeaveCompleted;
                this.textEditContext.NotifyFocusEnter();
            }
        }

        private void RemoveTextContext()
        {
            if(this.textEditContext != null)
            {
                this.textEditContext.NotifyFocusLeave();
                this.textEditContext.CompositionStarted -= TextEditContext_CompositionStarted;
                this.textEditContext.CompositionCompleted -= TextEditContext_CompositionCompleted;
                this.textEditContext.LayoutRequested -= TextEditContext_LayoutRequested;
                this.textEditContext.TextUpdating -= TextEditContext_TextUpdating;
                this.textEditContext.TextRequested -= TextEditContext_TextRequested;
                this.textEditContext.SelectionRequested -= TextEditContext_SelectionRequested;
                this.textEditContext.SelectionUpdating -= TextEditContext_SelectionUpdating;
                this.textEditContext.FormatUpdating -= TextEditContext_FormatUpdating;
                this.textEditContext.FocusRemoved -= TextEditContext_FocusRemoved;
                this.textEditContext.NotifyFocusLeaveCompleted -= TextEditContext_NotifyFocusLeaveCompleted;
                this.textEditContext = null;
            }
        }

        /// <inheritdoc/>
        protected override async void OnKeyDown(KeyRoutedEventArgs e)
        {
            bool isControlPressed = this.IsModiferKeyPressed(VirtualKey.Control);
            bool isShiftPressed = this.IsModiferKeyPressed(VirtualKey.Shift);
            bool isMovedCaret = false;

            var autocomplete = this.Document.AutoComplete as AutoCompleteBox;
            if (autocomplete != null && autocomplete.ProcessKeyDown(this, e, isControlPressed, isShiftPressed))
                return;

            double lineHeight = this.Render.emSize.Height * this.Render.LineEmHeight;
            double alignedPage = (int)(this.Render.TextArea.Height / lineHeight) * lineHeight;
            switch (e.Key)
            {
                case VirtualKey.Up:
                    this._Controller.MoveCaretVertical(-1, isShiftPressed);
                    this.Refresh();
                    e.Handled = true;
                    isMovedCaret = true;
                    break;
                case VirtualKey.Down:
                    this._Controller.MoveCaretVertical(+1, isShiftPressed);
                    this.Refresh();
                    e.Handled = true;
                    isMovedCaret = true;
                    break;
                case VirtualKey.Left:
                    this._Controller.MoveCaretHorizontical(-1, isShiftPressed, isControlPressed);
                    this.Refresh();
                    e.Handled = true;
                    isMovedCaret = true;
                    break;
                case VirtualKey.Right:
                    this._Controller.MoveCaretHorizontical(1, isShiftPressed, isControlPressed);
                    this.Refresh();
                    e.Handled = true;
                    isMovedCaret = true;
                    break;
                case VirtualKey.PageUp:
                    this._Controller.ScrollByPixel(ScrollDirection.Up, alignedPage, isShiftPressed, true);
                    this.Refresh();
                    isMovedCaret = true;
                    break;
                case VirtualKey.PageDown:
                    this._Controller.ScrollByPixel(ScrollDirection.Down, alignedPage, isShiftPressed, true);
                    this.Refresh();
                    isMovedCaret = true;
                    break;
                case VirtualKey.Home:
                    if (isControlPressed)
                        this._Controller.JumpToHead(isShiftPressed);
                    else
                        this.Controller.JumpToLineHead(this.Document.CaretPostion.row,isShiftPressed);
                    this.Refresh();
                    isMovedCaret = true;
                    break;
                case VirtualKey.End:
                    if (isControlPressed)
                        this._Controller.JumpToEnd(isShiftPressed);
                    else
                        this.Controller.JumpToLineEnd(this.Document.CaretPostion.row,isShiftPressed);
                    this.Refresh();
                    isMovedCaret = true;
                    break;
                case VirtualKey.Tab:
                    if (!isControlPressed)
                    {
                        if (this._Controller.SelectionLength == 0)
                            this._Controller.DoInputChar('\t');
                        else if (isShiftPressed)
                            this._Controller.DownIndent();
                        else
                            this._Controller.UpIndent();
                        this.Refresh();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.Enter:
                    this._Controller.DoEnterAction();
                    this.Refresh();
                    e.Handled = true;
                    break;
                case VirtualKey.Insert:
                    if(this._View.InsertMode)
                        this._View.InsertMode = false;
                    else
                        this._View.InsertMode = true;
                    this.Refresh();
                    e.Handled = true;
                    break;
                case VirtualKey.A:
                    if (isControlPressed)
                    {
                        this.SelectAll();
                        this.Refresh();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.B:
                    if (isControlPressed)
                    {
                        if (this._Controller.RectSelection)
                            this._Controller.RectSelection = false;
                        else
                            this._Controller.RectSelection = true;
                        this.Refresh();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.C:
                    if (isControlPressed)
                    {
                        this.CopyCommand();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.X:
                    if (isControlPressed)
                    {
                        this.CutCommand();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.V:
                    if (isControlPressed)
                    {
                        await this.PasteCommand();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.Y:
                    if (isControlPressed)
                    {
                        this.Document.UndoManager.redo();
                        this.Refresh();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.Z:
                    if (isControlPressed)
                    {
                        this.Document.UndoManager.undo();
                        this.Refresh();
                        e.Handled = true;
                    }
                    break;
                case VirtualKey.Back:
                    this._Controller.DoBackSpaceAction();
                    this.Refresh();
                    e.Handled = true;
                    break;
                case VirtualKey.Delete:
                    this._Controller.DoDeleteAction();
                    this.Refresh();
                    e.Handled = true;
                    break;
            }
            base.OnKeyDown(e);
        }

        /// <inheritdoc/>
        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("pointer pressed");
            this.CapturePointer(e.Pointer);
            this.gestureRecongnizer.ProcessDownEvent(e.GetCurrentPoint(this));
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("pointer moved");
            try
            {
                this.gestureRecongnizer.ProcessMoveEvents(e.GetIntermediatePoints(this));
            }catch(System.Runtime.InteropServices.COMException ex)
            {
                //ピンチズームでこの例外が発生するが、回避できない
                System.Diagnostics.Debug.WriteLine("expection:" + ex);
            }
            e.Handled = true;

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                Point p = e.GetCurrentPoint(this).Position;
                if (this._View.HitTextArea(p.X, p.Y))
                {
                    TextPoint tp = this._View.GetTextPointFromPostion(p);
                    if (this._Controller.IsMarker(tp, HilightType.Url))
                        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
                    else
                        this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.IBeam);
                }
                else
                {
                    this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("pointer released");
            this.gestureRecongnizer.ProcessUpEvent(e.GetCurrentPoint(this));
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("pointer canceled");
            this.gestureRecongnizer.CompleteGesture();
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("pointer wheelchanged");
            bool shift = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) ==
                Windows.System.VirtualKeyModifiers.Shift;
            bool ctrl = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) ==
                Windows.System.VirtualKeyModifiers.Control;
            this.gestureRecongnizer.ProcessMouseWheelEvent(e.GetCurrentPoint(this), shift, ctrl);
            e.Handled = true;
        }

        private void TextEditContext_FormatUpdating(CoreTextEditContext sender, CoreTextFormatUpdatingEventArgs args)
        {
            if (this.Document.Length == 0 || args.IsCanceled)
            {
                args.Result = CoreTextFormatUpdatingResult.Failed;
                return;
            }
            System.Diagnostics.Debug.WriteLine("core text format updating range({0}-{1}) underline type:{2} underline color:{3} reason:{4} textcolor:{5} background:{6}",
                args.Range.StartCaretPosition,
                args.Range.EndCaretPosition,
                args.UnderlineType,
                args.UnderlineColor,
                args.Reason,
                args.TextColor,
                args.BackgroundColor
                );
            HilightType type = HilightType.None;
            SolidColorBrush colorBrush = (SolidColorBrush)this.Foreground;
            Windows.UI.Color uicolor = colorBrush.Color;
            Color color = new Color(uicolor.A, uicolor.R, uicolor.G, uicolor.B);
            bool isBoldLine = false;
            switch (args.UnderlineType)
            {
                case UnderlineType.Dotted:
                    type = HilightType.Dot;
                    break;
                case UnderlineType.Single:
                    type = HilightType.Sold;
                    break;
                case UnderlineType.Dash:
                    type = HilightType.Dash;
                    break;
                case UnderlineType.Wave:
                    type = HilightType.Squiggle;
                    break;
                case UnderlineType.Thin:
                    type = HilightType.Sold;
                    break;
                case UnderlineType.Thick:
                    type = HilightType.Sold;
                    isBoldLine = true;
                    break;
            }
            int start = args.Range.StartCaretPosition;
            int lengt = args.Range.EndCaretPosition - args.Range.StartCaretPosition;
            this.Document.SetMarker(MarkerIDs.IME, Marker.Create(start, lengt, type, color, isBoldLine));

            if (args.Reason == CoreTextFormatUpdatingReason.CompositionTargetConverted)
            {
                var tp = this.Document.LayoutLines.GetTextPointFromIndex(args.Range.StartCaretPosition);
                this._View.AdjustSrc(tp, AdjustFlow.Both);
            }
            this.Refresh();

            args.Result = CoreTextFormatUpdatingResult.Succeeded;
        }

        private void TextEditContext_TextRequested(CoreTextEditContext sender, CoreTextTextRequestedEventArgs args)
        {
            CoreTextTextRequest req = args.Request;

            if (this.Document.Length == 0 || req.IsCanceled)
            {
                return;
            }

            int start = req.Range.StartCaretPosition;
            int end = req.Range.EndCaretPosition;
            if (end > this.Document.Length)
                end = this.Document.Length;

            int length = end - start;

            System.Diagnostics.Debug.WriteLine("req text start:{0} length:{1}", start, length);

            //キャレット位置も含むので+1する必要はない
            req.Text = this.Document.ToString(start,length);
        }

        private void TextEditContext_LayoutRequested(CoreTextEditContext sender, CoreTextLayoutRequestedEventArgs args)
        {
            //変換候補の範囲を取得する
            Point startPos, endPos;
            int i_startIndex = args.Request.Range.StartCaretPosition;
            int i_endIndex = args.Request.Range.EndCaretPosition;

            if(args.Request.IsCanceled)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("core text layoutreq range({0}-{1})",i_startIndex,i_endIndex);

            double scale = Util.GetScale();
            Point screenStartPos, screenEndPos;

            if (i_startIndex != i_endIndex && i_startIndex != -1 && i_endIndex != -1)
            {
                TextStoreHelper.GetStringExtent(this.Document, this._View, i_startIndex, i_endIndex, out startPos, out endPos);

                //Core.Textはスクリーン座標に変換してくれないので自前で変換する（しかも、デバイス依存の座標で返さないといけない）
                screenStartPos = Util.GetScreentPoint(startPos, this);
                screenEndPos = Util.GetScreentPoint(endPos, this);
                args.Request.LayoutBounds.TextBounds = new Rect(
                    screenStartPos.X,
                    screenStartPos.Y,
                    Math.Max(0,screenEndPos.X - screenStartPos.X),  //折り返されている場合、負になることがある
                    Math.Max(0,screenEndPos.Y - screenStartPos.Y)
                    );
            }

            //コントロールの範囲を取得する
            var controlTopLeft = new Point(0, 0);
            var controlBottomRight = new Point(this.ActualWidth, this.ActualHeight);

            //Core.Textはスクリーン座標に変換してくれないので自前で変換する（しかも、デバイス依存の座標で返さないといけない）
            screenStartPos = Util.GetScreentPoint(controlTopLeft, this);
            screenEndPos = Util.GetScreentPoint(controlBottomRight, this);

            args.Request.LayoutBounds.ControlBounds = new Rect(
                screenStartPos.X,
                screenStartPos.Y,
                screenEndPos.X - screenStartPos.X,
                screenEndPos.Y - screenStartPos.Y
                );
        }

        private void TextEditContext_SelectionRequested(CoreTextEditContext sender, CoreTextSelectionRequestedEventArgs args)
        {
            if(args.Request.IsCanceled || this.Document.Length == 0)
            {
                return;
            }
            TextRange currentSelection = new TextRange();
            TextStoreHelper.GetSelection(this._Controller, this._View.Selections, out currentSelection);

            CoreTextRange currentSelectionRange = new CoreTextRange();
            currentSelectionRange.StartCaretPosition = currentSelection.Index;
            currentSelectionRange.EndCaretPosition = currentSelection.Index + currentSelection.Length;
            args.Request.Selection = currentSelectionRange;
            System.Diagnostics.Debug.WriteLine("req selection start:{0} end:{1}", currentSelectionRange.StartCaretPosition, currentSelectionRange.EndCaretPosition);
        }

        private void TextEditContext_SelectionUpdating(CoreTextEditContext sender, CoreTextSelectionUpdatingEventArgs args)
        {
            if(this.Document.Length == 0 || args.IsCanceled)
            {
                args.Result = CoreTextSelectionUpdatingResult.Failed;
                return;
            }
            CoreTextRange sel = args.Selection;
            System.Diagnostics.Debug.WriteLine("update selection start:{0} end:{1}", sel.StartCaretPosition, sel.EndCaretPosition);
            TextStoreHelper.SetSelectionIndex(this.Controller, this._View, sel.StartCaretPosition, sel.EndCaretPosition);
            args.Result = CoreTextSelectionUpdatingResult.Succeeded;
            this.Refresh();
        }

        private void TextEditContext_TextUpdating(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs args)
        {
            this.nowCompstion = true;

            System.Diagnostics.Debug.WriteLine("update text (modify start:{0} end:{1}) text:{2} (new sel start:{0} end:{1})",
                args.Range.StartCaretPosition, 
                args.Range.EndCaretPosition, 
                args.Text, 
                args.NewSelection.StartCaretPosition, 
                args.NewSelection.EndCaretPosition);
            bool isTip = args.InputLanguage.Script == "Latan";
            CoreTextRange sel = args.Range;
            TextStoreHelper.SetSelectionIndex(this.Controller, this._View, sel.StartCaretPosition, sel.EndCaretPosition);
            TextStoreHelper.InsertTextAtSelection(this._Controller, args.Text, isTip);
            this.Refresh();
            args.Result = CoreTextTextUpdatingResult.Succeeded;

            this.nowCompstion = false;
        }

        private void TextEditContext_CompositionCompleted(CoreTextEditContext sender, CoreTextCompositionCompletedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("end compostion");
            TextStoreHelper.EndCompostion(this.Document);
            this.Document.RemoveAllMarker(MarkerIDs.IME);
            this.Refresh();
        }

        private void TextEditContext_CompositionStarted(CoreTextEditContext sender, CoreTextCompositionStartedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("start compstion");
            TextStoreHelper.StartCompstion(this.Document);
        }

        private void TextEditContext_NotifyFocusLeaveCompleted(CoreTextEditContext sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("notify focus leaved");
        }

        private void TextEditContext_FocusRemoved(CoreTextEditContext sender, object args)
        {
            System.Diagnostics.Debug.WriteLine("focus leaved");
        }

        void Controller_SelectionChanged(object sender, EventArgs e)
        {
            if (this._Controller == null || this.Document == null)
                return;

            //こうしないと選択できなくなってしまう
            this.nowCaretMove = true;
            SetValue(SelectedTextProperty, this._Controller.SelectedText);
            SetValue(SelectionProperty, new TextRange(this._Controller.SelectionStart, this._Controller.SelectionLength));
            SetValue(CaretPostionPropertyKey, this.Document.CaretPostion);
            this.nowCaretMove = false;

            if(!this.nowCompstion)
            {
                TextRange currentSelection = new TextRange();
                TextStoreHelper.GetSelection(this._Controller, this._View.Selections, out currentSelection);

                CoreTextRange currentSelectionRange = new CoreTextRange();
                currentSelectionRange.StartCaretPosition = currentSelection.Index;
                currentSelectionRange.EndCaretPosition = currentSelection.Index + currentSelection.Length;

                System.Diagnostics.Debug.WriteLine("notify selection start:{0} end:{1}", currentSelectionRange.StartCaretPosition, currentSelectionRange.EndCaretPosition);
                //変換中に呼び出してはいけない
                if (this.textEditContext != null)
                    this.textEditContext.NotifySelectionChanged(currentSelectionRange);
            }
#if ENABLE_AUTMATION
            if (this.peer != null)
                this.peer.OnNotifyCaretChanged();
#endif
        }

        Gripper hittedGripper;
        void gestureRecongnizer_ManipulationStarted(GestureRecognizer sender, ManipulationStartedEventArgs e)
        {
            //Updateedの段階でヒットテストしてしまうとグリッパーを触ってもヒットしないことがある
            this.hittedGripper = this._View.HitGripperFromPoint(e.Position);
        }

        private void GestureRecongnizer_ManipulationInertiaStarting(GestureRecognizer sender, ManipulationInertiaStartingEventArgs args)
        {
            //sender.InertiaTranslationDeceleration = 0.001f;
            //sender.InertiaExpansionDeceleration = 100.0f * 96.0f / 1000.0f;
            //sender.InertiaRotationDeceleration = 720.0f / (1000.0f * 1000.0f);
        }

        void gestureRecongnizer_ManipulationUpdated(GestureRecognizer sender, ManipulationUpdatedEventArgs e)
        {
            bool scaleResult = this._Controller.Scale(e.Delta.Scale, (scale) => {
                if (e.Delta.Scale < 1) {
                    double newSize = this.Render.FontSize - 1;
                    if (newSize < 1)
                        newSize = 1;
                    this.Render.FontSize = newSize;
                    this.Refresh();
                    SetValue(MagnificationPowerPropertyKey, this.Render.FontSize / this.FontSize);
                }
                if (e.Delta.Scale > 1) {
                    double newSize = this.Render.FontSize + 1;
                    if (newSize > 72)
                        newSize = 72;
                    this.Render.FontSize = newSize;
                    this.Refresh();
                    SetValue(MagnificationPowerPropertyKey, this.Render.FontSize / this.FontSize);
                }
            });

            if (scaleResult)
                return;

            if (this._Controller.MoveCaretAndGripper(e.Position, this.hittedGripper))
            {
                this.Refresh();                
                return;
            }
            
            Point translation = e.Delta.Translation;

            //Xの絶対値が大きければ横方向のスクロールで、そうでなければ縦方向らしい
            if (Math.Abs(e.Cumulative.Translation.X) < Math.Abs(e.Cumulative.Translation.Y))
            {
                int deltay = (int)Math.Abs(Math.Ceiling(translation.Y));
                if (translation.Y < 0)
                    this._Controller.ScrollByPixel(ScrollDirection.Down, deltay, false, false);
                else
                    this._Controller.ScrollByPixel(ScrollDirection.Up, deltay, false, false);
                this.Refresh();
                return;
            }

            int deltax = (int)Math.Abs(Math.Ceiling(translation.X));
            if (deltax != 0)
            {
                if (translation.X < 0)
                    this._Controller.Scroll(ScrollDirection.Left, deltax, false, false);
                else
                    this._Controller.Scroll(ScrollDirection.Right, deltax, false, false);
                this.Refresh();
            }
        }

        void gestureRecongnizer_ManipulationCompleted(GestureRecognizer sender, ManipulationCompletedEventArgs e)
        {
        }

        public const string ResourceKey = "FooEditEngine.WinUI/Resources";

        void gestureRecongnizer_RightTapped(GestureRecognizer sender, RightTappedEventArgs e)
        {
            ResourceMap map = ResourceManager.Current.MainResourceMap.GetSubtree(ResourceKey);
            ResourceContext context = ResourceManager.Current.DefaultContext;
            if (this._View.HitTextArea(e.Position.X, e.Position.Y))
            {
                FooContextMenuEventArgs args = new FooContextMenuEventArgs(e.Position);
                if (this.ContextMenuOpening != null)
                    this.ContextMenuOpening(this, args);
                if (!args.Handled)
                {
                    MenuFlyout ContextMenu = new MenuFlyout();

                    var cmd = new XamlUICommand();
                    cmd.ExecuteRequested += (s, e) =>
                    {
                        this.CopyCommand();
                    };
                    ContextMenu.Items.Add(new MenuFlyoutItem() {
                        Icon = new SymbolIcon(Symbol.Copy),
                        Command = cmd,
                        Text = map.GetValue("CopyMenuName", context).ValueAsString});

                    cmd = new XamlUICommand();
                    cmd.ExecuteRequested += (s, e) =>
                    {
                        this.CutCommand();
                    };
                    ContextMenu.Items.Add(new MenuFlyoutItem()
                    {
                        Icon = new SymbolIcon(Symbol.Cut),
                        Command = cmd,
                        Text = map.GetValue("CutMenuName", context).ValueAsString
                    });

                    cmd = new XamlUICommand();
                    cmd.ExecuteRequested += async (s, e) =>
                    {
                        await this.PasteCommand();
                    };
                    ContextMenu.Items.Add(new MenuFlyoutItem()
                    {
                        Icon = new SymbolIcon(Symbol.Paste),
                        Command = cmd,
                        Text = map.GetValue("PasteMenuName", context).ValueAsString
                    });
                    if (this._Controller.RectSelection)
                    {
                        cmd = new XamlUICommand();
                        cmd.ExecuteRequested += (s, e) =>
                        {
                            this._Controller.RectSelection = false;
                        };
                        ContextMenu.Items.Add(new MenuFlyoutItem()
                        {
                            Text = map.GetValue("LineSelectMenuName", context).ValueAsString,
                            Command = cmd,
                        });
                    }
                    else
                    {
                        cmd = new XamlUICommand();
                        cmd.ExecuteRequested += (s, e) =>
                        {
                            this._Controller.RectSelection = true;
                        };
                        ContextMenu.Items.Add(new MenuFlyoutItem()
                        {
                            Text = map.GetValue("RectSelectMenuName", context).ValueAsString,
                            Command = cmd,
                        });
                    }
                    ContextMenu.ShowAt(this, new FlyoutShowOptions() { Position = e.Position, ShowMode = FlyoutShowMode.Standard });
                }
            }
        }

        long lastDouleTapTick;
        const long allowTripleTapTimeSpan = 500;
        void gestureRecongnizer_Tapped(GestureRecognizer sender, TappedEventArgs e)
        {
            bool touched = e.PointerDeviceType == PointerDeviceType.Touch;
            this.Document.SelectGrippers.BottomLeft.Enabled = false;
            this.Document.SelectGrippers.BottomRight.Enabled = touched;
            this.JumpCaret(e.Position);
            if(e.TapCount == 1 && System.DateTime.Now.Ticks - lastDouleTapTick < allowTripleTapTimeSpan * 10000)    //トリプルタップ
            {
                //タッチスクリーンで行選択した場合、アンカーインデックスを単語の先頭にしないとバグる
                this.Document.SelectGrippers.BottomLeft.Enabled = touched;
                this.Document.SelectLine(this.Controller.SelectionStart, touched);
                this.Refresh();
            }
            else  if(e.TapCount == 2)   //ダブルタップ
            {
                //タッチスクリーンで単語選択した場合、アンカーインデックスを単語の先頭にしないとバグる
                this.Document.SelectGrippers.BottomLeft.Enabled = touched;
                if (e.Position.X < this.Render.TextArea.X)
                    this.Document.SelectLine(this.Controller.SelectionStart, touched);
                else
                    this.Document.SelectWord(this.Controller.SelectionStart, touched);
                this.lastDouleTapTick = System.DateTime.Now.Ticks;
                this.Refresh();
            }
        }

        void JumpCaret(Point p)
        {
            TextPoint tp = this._View.GetTextPointFromPostion(p);
            if (tp == TextPoint.Null)
                return;

            int index = this._View.LayoutLines.GetIndexFromTextPoint(tp);

            FoldingItem foldingData = this._View.HitFoldingData(p.X, tp.row);
            if (foldingData != null)
            {
                if (foldingData.Expand)
                    this._View.LayoutLines.FoldingCollection.Collapse(foldingData);
                else
                    this._View.LayoutLines.FoldingCollection.Expand(foldingData);
                this._Controller.JumpCaret(foldingData.Start, false);
            }
            else
            {
                this._Controller.JumpCaret(tp.row, tp.col, false);
            }
            this._View.HideCaret = false;
            this._View.IsFocused = true;
            this.Focus(FocusState.Programmatic);
            this.Refresh();
        }

        void gestureRecongnizer_Dragging(GestureRecognizer sender, DraggingEventArgs e)
        {
            Point p = e.Position;
            TextPointSearchRange searchRange;
            if (this._View.HitTextArea(p.X, p.Y))
                searchRange = TextPointSearchRange.TextAreaOnly;
            else if (this._Controller.SelectionLength > 0)
                searchRange = TextPointSearchRange.Full;
            else
                return;
            TextPoint tp = this._View.GetTextPointFromPostion(p, searchRange);
            this._Controller.MoveCaretAndSelect(tp, this.IsModiferKeyPressed(VirtualKey.LeftControl));
            this.Refresh();
        }

        bool IsModiferKeyPressed(VirtualKey key)
        {
            CoreVirtualKeyStates state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }
        void Refresh(Rectangle updateRect)
        {
            if (this.rectangle.ActualWidth == 0 || this.rectangle.ActualHeight == 0 || this.Visibility == Microsoft.UI.Xaml.Visibility.Collapsed)
                return;

            this.Render.Draw(this.rectangle, (e) => {
                if (IsEnabled)
                    _View.Draw(this._View.PageBound);
                else
                    this.Render.FillBackground(this._View.PageBound);
            });

            this.Document.IsRequestRedraw = false;
        }


        bool Resize(double width, double height)
        {
            if (width == 0 || height == 0)
                return false;
            if(this.Render.Resize(this.rectangle,width,height))
            {
                this._View.PageBound = new Rectangle(0, 0, width, height);

                if (this.horizontalScrollBar != null)
                {
                    this.horizontalScrollBar.LargeChange = this._View.PageBound.Width;
                    this.horizontalScrollBar.Maximum = this._View.LongestWidth + this.horizontalScrollBar.LargeChange + 1;
                }
                if (this.verticalScrollBar != null)
                {
                    this.verticalScrollBar.LargeChange = this._View.LineCountOnScreen;
                    this.verticalScrollBar.Maximum = this._View.LayoutLines.Count + this.verticalScrollBar.LargeChange + 1;
                }
                return true;
            }
            return false;
        }

        void View_SrcChanged(object sender, EventArgs e)
        {
            if (this.horizontalScrollBar == null || this.verticalScrollBar == null)
                return;
            EditView view = this._View;
            if (view.Src.Row > this.verticalScrollBar.Maximum)
                this.verticalScrollBar.Maximum = view.Src.Row + view.LineCountOnScreen + 1;
            double absoulteX = Math.Abs(view.Src.X);
            if (absoulteX > this.horizontalScrollBar.Maximum)
                this.horizontalScrollBar.Maximum = absoulteX + view.PageBound.Width + 1;
            if (view.Src.Row != this.verticalScrollBar.Value)
                this.verticalScrollBar.Value = view.Src.Row;
            if (view.Src.X != this.horizontalScrollBar.Value)
                this.horizontalScrollBar.Value = Math.Abs(view.Src.X);
        }

        void FooTextBox_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //LostFocusやGotFocusなどと競合するとDirect2Dでエラーが起きるので、timer_tickイベントでサイズ変更を行うことにした
            this.requestSizeChange = true;
        }

        void horizontalScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (this.horizontalScrollBar == null)
                return;
            double toX;
            if (this.FlowDirection == FlowDirection.LeftToRight)
                toX = this.horizontalScrollBar.Value;
            else
                toX = -this.horizontalScrollBar.Value;
            this._Controller.Scroll(toX, this._View.Src.Row, false, false);
            this.Refresh();
        }

        void verticalScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            if (this.verticalScrollBar == null)
                return;
            int newRow = (int)this.verticalScrollBar.Value;
            if (newRow >= this._View.LayoutLines.Count)
                return;
            this._Controller.Scroll(this._View.Src.X, newRow, false, false);
            this.Refresh();
        }

        void Document_Update(object sender, DocumentUpdateEventArgs e)
        {
            if (e.type == UpdateType.Replace && !this.nowCompstion)
            {
                CoreTextRange oldTextRange = new CoreTextRange();
                oldTextRange.StartCaretPosition = e.startIndex;
                oldTextRange.EndCaretPosition = e.startIndex;
                //削除する範囲が1以上の場合、ドキュメントを飛び越えることはできない
                //https://msdn.microsoft.com/en-us/windows/uwp/input-and-devices/custom-text-input
                if (e.removeLength > 0)
                    oldTextRange.EndCaretPosition += e.removeLength;

                TextRange currentSelection = new TextRange();
                TextStoreHelper.GetSelection(this._Controller, this._View.Selections, out currentSelection);

                CoreTextRange newSelection = new CoreTextRange();
                newSelection.StartCaretPosition = e.startIndex;
                newSelection.EndCaretPosition = e.startIndex;

                //置き換え後の長さを指定する
                //（注意：削除された文字数のほうが多い場合は0を指定しないいけない）
                int newTextLength = e.insertLength;

                System.Diagnostics.Debug.WriteLine("notify text change (modify start:{0} end:{1}) newlength:{2} (new sel start:{3} end:{4})",
                    oldTextRange.StartCaretPosition, oldTextRange.EndCaretPosition, newTextLength, newSelection.StartCaretPosition, newSelection.EndCaretPosition);
                //変換中に呼び出してはいけない
                if(this.textEditContext != null)
                    this.textEditContext.NotifyTextChanged(oldTextRange, newTextLength, newSelection);
            }
#if ENABLE_AUTMATION
            if (this.peer != null)
                this.peer.OnNotifyTextChanged();
#endif
        }

        void FooTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            Util.SetDpi((float)(this.XamlRoot.RasterizationScale * 96.0f));
            this.View.CaretWidthOnInsertMode *= Math.Ceiling(this.XamlRoot.RasterizationScale);
            this.Render.CreateSurface(this.rectangle, 100, 100);
            this.Focus(FocusState.Programmatic);
        }

        void timer_Tick(object sender, object e)
        {
            this.timer.Stop();
            if(this.requestSizeChange)
            {
                if (this.Resize(this.rectangle.ActualWidth, this.rectangle.ActualHeight))
                {
                    //普通に再描写するとちらつく
                    this.Refresh(this._View.PageBound);
                }
                this.requestSizeChange = false;
            }
            else if(this._View != null && this.Document != null)
            {
                if (this._View.LayoutLines.HilightAll() || this._View.LayoutLines.GenerateFolding() || this.Document.IsRequestRedraw)
                {
                    this.Refresh(this._View.PageBound);
                }
            }
            this.timer.Start();
        }
        private void SetDocument(Document value)
        {
            if (value == null)
                return;

            Document old_doc = this._Document;

            if (this._Document != null)
            {
                old_doc.Update -= new DocumentUpdateEventHandler(Document_Update);
                this._Document.SelectionChanged -= Controller_SelectionChanged;
                this._Document.LoadProgress -= Document_LoadProgress;
                this._Document.AutoCompleteChanged -= _Document_AutoCompleteChanged;
                if (this._Document.AutoComplete != null)
                {
                    this._Document.AutoComplete.GetPostion = null;
                    this._Document.AutoComplete = null;
                }

                //NotifyTextChanged()を呼び出すと落ちるのでTextConextをごっそり作り替える
                this.RemoveTextContext();
            }

            System.Diagnostics.Debug.WriteLine("document switched");

            this._Document = value;
            this._Document.LayoutLines.Render = this.Render;
            this._Document.Update += new DocumentUpdateEventHandler(Document_Update);
            this._Document.LoadProgress += Document_LoadProgress;
            this._Document.AutoCompleteChanged += _Document_AutoCompleteChanged;
            if (this._Document.AutoComplete != null && this._Document.AutoComplete.GetPostion == null)
                this._Document_AutoCompleteChanged(this._Document, null);
            //初期化が終わっていればすべて存在する
            if (this.Controller != null && this._View != null)
            {
                this.Controller.Document = value;
                this._View.Document = value;

                this.Controller.AdjustCaret();

                this.CreateTextContext();

                //依存プロパティとドキュメント内容が食い違っているので再設定する
                this.ShowFullSpace = value.ShowFullSpace;
                this.ShowHalfSpace = value.ShowHalfSpace;
                this.ShowLineBreak = value.ShowLineBreak;
                this.ShowTab = value.ShowTab;
                this.FlowDirection = value.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                this.IndentMode = value.IndentMode;
                this.DrawCaretLine = !value.HideLineMarker;
                this.InsertMode = value.InsertMode;
                this.DrawRuler = !value.HideRuler;
                this.DrawLineNumber = value.DrawLineNumber;
                this.MarkURL = value.UrlMark;
                this.LineBreakMethod = value.LineBreak;
                this.LineBreakCharCount = value.LineBreakCharCount;
                this.TabChars = value.TabStops;

                this.Refresh();
            }
            //TextEditContext作成後に設定しないと落ちることがある
            this._Document.SelectionChanged += Controller_SelectionChanged;
        }

        private void _Document_AutoCompleteChanged(object sender, EventArgs e)
        {
            Document doc = (Document)sender;
            AutoCompleteBox autoCompleteBox = (AutoCompleteBox)doc.AutoComplete;
            autoCompleteBox.Target = this;
            autoCompleteBox.GetPostion = (tp, e_doc) =>
            {
                var p = this._View.GetPostionFromTextPoint(tp);
                int height = (int)e_doc.LayoutLines.GetLayout(e_doc.CaretPostion.row).Height;

                if (p.Y + AutoCompleteBox.CompleteListBoxHeight + height > e_doc.LayoutLines.Render.TextArea.Height)
                    p.Y -= AutoCompleteBox.CompleteListBoxHeight;
                else
                    p.Y += height;
                return Util.GetPointInWindow(p,this);
            };
        }

        void InheritanceDependecyPropertyCallback(DependencyObject sender, DependencyProperty dp)
        {
            if (dp.Equals(Control.FlowDirectionProperty))
            {
                this.Document.RightToLeft = this.FlowDirection == Microsoft.UI.Xaml.FlowDirection.RightToLeft;
                if (this.horizontalScrollBar != null)
                    this.horizontalScrollBar.FlowDirection = this.FlowDirection;
            }
#if !DUMMY_RENDER
            if (this.Render == null)
                return;
            if (dp.Equals(Control.FontFamilyProperty))
                this.Render.FontFamily = this.FontFamily;
            if (dp.Equals(Control.FontStyleProperty))
                this.Render.FontStyle = this.FontStyle;
            if (dp.Equals(Control.FontWeightProperty))
                this.Render.FontWeigth = this.FontWeight;
            if (dp.Equals(Control.FontSizeProperty))
                this.Render.FontSize = this.FontSize;
            if (dp.Equals(Control.ForegroundProperty))
                this.Render.Foreground = ((SolidColorBrush)this.Foreground).Color;
            if (dp.Equals(Control.BackgroundProperty))
                this.Render.Background = ((SolidColorBrush)this.Background).Color;
#endif
        }

        /// <inheritdoc/>
        public static void OnPropertyChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            FooTextBox source = (FooTextBox)sender;
            if(e.Property.Equals(SelectedTextProperty) && !source.nowCaretMove)
                source._Controller.SelectedText = source.SelectedText;
            if (e.Property.Equals(DocumentProperty))
                source.SetDocument(source.Document);
            if(e.Property.Equals(HilighterProperty))
                source._View.Hilighter = source.Hilighter;
            if (e.Property.Equals(FoldingStrategyProperty))
                source._View.LayoutLines.FoldingStrategy = source.FoldingStrategy;
            if (e.Property.Equals(IndentModeProperty))
                source.Controller.IndentMode = source.IndentMode;
            if (e.Property.Equals(SelectionProperty) && !source.nowCaretMove)
                source.Document.Select(source.Selection.Index,source.Selection.Length);
            if (e.Property.Equals(CaretPostionPropertyKey) && !source.nowCaretMove)
                source.JumpCaret(source.CaretPostion.row, source.CaretPostion.col);
            if (e.Property.Equals(InsertModeProperty))
                source._View.InsertMode = source.InsertMode;
            if (e.Property.Equals(TabCharsProperty))
                source.Document.TabStops = source.TabChars;
            if (e.Property.Equals(RectSelectModeProperty))
                source._Controller.RectSelection = source.RectSelectMode;
            if (e.Property.Equals(DrawCaretProperty))
                source._View.HideCaret = !source.DrawCaret;
            if (e.Property.Equals(DrawCaretLineProperty))
                source._View.HideLineMarker = !source.DrawCaretLine;
            if (e.Property.Equals(DrawLineNumberProperty))
                source.Document.DrawLineNumber = source.DrawLineNumber;
            if (e.Property.Equals(MarkURLProperty))
                source.Document.UrlMark = source.MarkURL;
            if (e.Property.Equals(LineBreakProperty))
                source.Document.LineBreak = source.LineBreakMethod;
            if (e.Property.Equals(LineBreakCharCountProperty))
                source.Document.LineBreakCharCount = source.LineBreakCharCount;
            if (e.Property.Equals(DrawRulerProperty))
            {
                source.Document.HideRuler = !source.DrawRuler;
                source._Controller.JumpCaret(source.Document.CaretPostion.row, source.Document.CaretPostion.col);
            }
#if !DUMMY_RENDER
            if (source.Render == null)
                return;
            if (e.Property.Equals(TextAntialiasModeProperty))
                source.Render.TextAntialiasMode = source.TextAntialiasMode;
            if(e.Property.Equals(MagnificationPowerPropertyKey))
                source.Render.FontSize = source.FontSize * source.MagnificationPower;
            if (e.Property.Equals(HilightForegroundProperty))
                source.Render.HilightForeground = source.HilightForeground.Color;
            if (e.Property.Equals(ControlCharProperty))
                source.Render.ControlChar = source.ControlChar.Color;
            if (e.Property.Equals(HilightProperty))
                source.Render.Hilight = source.Hilight.Color;
            if (e.Property.Equals(Keyword1Property))
                source.Render.Keyword1 = source.Keyword1.Color;
            if (e.Property.Equals(Keyword2Property))
                source.Render.Keyword2 = source.Keyword2.Color;
            if (e.Property.Equals(CommentProperty))
                source.Render.Comment = source.Comment.Color;
            if (e.Property.Equals(LiteralProperty))
                source.Render.Literal = source.Literal.Color;
            if (e.Property.Equals(URLProperty))
                source.Render.Url = source.URL.Color;
            if (e.Property.Equals(InsertCaretProperty))
                source.Render.InsertCaret = source.InsertCaret.Color;
            if (e.Property.Equals(OverwriteCaretProperty))
                source.Render.OverwriteCaret = source.OverwriteCaret.Color;
            if (e.Property.Equals(PaddingProperty))
                source._View.Padding = new Padding((int)source.Padding.Left, (int)source.Padding.Top, (int)source.Padding.Right, (int)source.Padding.Bottom);
            if (e.Property.Equals(LineMarkerProperty))
                source.Render.LineMarker = source.LineMarker.Color;
            if (e.Property.Equals(ShowFullSpaceProperty))
                source.Render.ShowFullSpace = source.ShowFullSpace;
            if (e.Property.Equals(ShowHalfSpaceProperty))
                source.Render.ShowHalfSpace = source.ShowHalfSpace;
            if (e.Property.Equals(ShowTabProperty))
                source.Render.ShowTab = source.ShowTab;
            if (e.Property.Equals(ShowLineBreakProperty))
                source.Render.ShowLineBreak = source.ShowLineBreak;
            if (e.Property.Equals(UpdateAreaProperty))
                source.Render.UpdateArea = source.UpdateArea.Color;
            if (e.Property.Equals(LineNumberProperty))
                source.Render.LineNumber = source.LineNumber.Color;
            if (e.Property.Equals(LineEmHeightProperty))
                source.Render.LineEmHeight = source.LineEmHeight;
#endif
        }

        /// <summary>
        /// コンテキストメニューが表示されるときに呼び出されます
        /// </summary>
        public event EventHandler<FooContextMenuEventArgs> ContextMenuOpening;

#endregion

#region property

        internal Controller Controller
        {
            get
            {
                return this._Controller;
            }
        }

        internal EditView View
        {
            get
            {
                return this._View;
            }
        }

        /// <summary>
        /// 文字列の描写に使用されるアンチエイリアシング モードを表します
        /// </summary>
        public TextAntialiasMode TextAntialiasMode
        {
            get { return (TextAntialiasMode)GetValue(TextAntialiasModeProperty); }
            set { SetValue(TextAntialiasModeProperty, value); }
        }

        /// <summary>
        /// TextAntialiasModeの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty TextAntialiasModeProperty =
            DependencyProperty.Register("TextAntialiasMode", typeof(TextAntialiasMode), typeof(FooTextBox), new PropertyMetadata(TextAntialiasMode.Default, OnPropertyChanged));

        /// <summary>
        /// シンタックスハイライターを表す
        /// </summary>
        public IHilighter Hilighter
        {
            get { return (IHilighter)GetValue(HilighterProperty); }
            set { SetValue(HilighterProperty, value); }
        }

        /// <summary>
        /// Hilighterの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty HilighterProperty =
            DependencyProperty.Register("Hilighter", typeof(IHilighter), typeof(FooTextBox), new PropertyMetadata(null, OnPropertyChanged));

        /// <summary>
        /// フォールティングを作成するインターフェイスを表す
        /// </summary>
        public IFoldingStrategy FoldingStrategy
        {
            get { return (IFoldingStrategy)GetValue(FoldingStrategyProperty); }
            set { SetValue(FoldingStrategyProperty, value); }
        }

        /// <summary>
        /// FoldingStrategyの依存プロパティ
        /// </summary>
        public static readonly DependencyProperty FoldingStrategyProperty =
            DependencyProperty.Register("FoldingStrategy", typeof(IFoldingStrategy), typeof(FooTextBox), new PropertyMetadata(null,OnPropertyChanged));

        /// <summary>
        /// マーカーパターンセットを表す
        /// </summary>
        public MarkerPatternSet MarkerPatternSet
        {
            get
            {
                return this.Document.MarkerPatternSet;
            }
        }

        /// <summary>
        /// ドキュメントを表す
        /// </summary>
        /// <remarks>切り替え後に再描写が行われます</remarks>
        public Document Document
        {
            get { return (Document)GetValue(DocumentProperty); }
            set { SetValue(DocumentProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Document.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DocumentProperty =
            DependencyProperty.Register("Document", typeof(Document), typeof(FooTextBox), new PropertyMetadata(null, OnPropertyChanged));



        /// <summary>
        /// レイアウト行を表す
        /// </summary>
        public LineToIndexTable LayoutLineCollection
        {
            get { return this._View.LayoutLines; }
        }

        /// <summary>
        /// 選択中の文字列を表す
        /// </summary>
        public string SelectedText
        {
            get { return (string)GetValue(SelectedTextProperty); }
            set { SetValue(SelectedTextProperty, value); }
        }

        /// <summary>
        /// SelectedTextの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty SelectedTextProperty =
            DependencyProperty.Register("SelectedText", typeof(string), typeof(FooTextBox), new PropertyMetadata(null, OnPropertyChanged));

        /// <summary>
        /// インデントの方法を表す
        /// </summary>
        public IndentMode IndentMode
        {
            get { return (IndentMode)GetValue(IndentModeProperty); }
            set { SetValue(IndentModeProperty, value); }
        }

        /// <summary>
        /// IndentModeの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty IndentModeProperty =
            DependencyProperty.Register("IndentMode", typeof(IndentMode), typeof(FooTextBox), new PropertyMetadata(IndentMode.Tab,OnPropertyChanged));

        /// <summary>
        /// 選択範囲を表す
        /// </summary>
        /// <remarks>
        /// Lengthが0の場合はキャレット位置を表します。
        /// 矩形選択モードの場合、選択範囲の文字数ではなく、開始位置から終了位置までの長さとなります
        /// </remarks>
        public TextRange Selection
        {
            get { return (TextRange)GetValue(SelectionProperty); }
            set { SetValue(SelectionProperty, value); }
        }

        /// <summary>
        /// Selectionの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty SelectionProperty =
            DependencyProperty.Register("Selection", typeof(TextRange), typeof(FooTextBox), new PropertyMetadata(TextRange.Null, OnPropertyChanged));

        /// <summary>
        /// 拡大率を表す
        /// </summary>
        public double MagnificationPower
        {
            get { return (double)GetValue(MagnificationPowerPropertyKey); }
            set { SetValue(MagnificationPowerPropertyKey, value); }
        }

        /// <summary>
        /// 拡大率を表す依存プロパティ
        /// </summary>
        public static readonly DependencyProperty MagnificationPowerPropertyKey =
            DependencyProperty.Register("MagnificationPower", typeof(double), typeof(FooTextBox), new PropertyMetadata(1.0, OnPropertyChanged));

        /// <summary>
        /// キャレット位置を表す
        /// </summary>
        public TextPoint CaretPostion
        {
            get { return (TextPoint)GetValue(CaretPostionPropertyKey); }
            set { SetValue(CaretPostionPropertyKey, value); }
        }

        static readonly DependencyProperty CaretPostionPropertyKey =
            DependencyProperty.Register("CaretPostion", typeof(TextPoint), typeof(FooTextBox), new PropertyMetadata(new TextPoint(), OnPropertyChanged));

        /// <summary>
        /// 選択時の文字色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush HilightForeground
        {
            get { return (SolidColorBrush)GetValue(HilightForegroundProperty); }
            set { SetValue(HilightForegroundProperty, value); }
        }

        /// <summary>
        /// HilightForegroundForegroundの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty HilightForegroundProperty =
            DependencyProperty.Register("HilightForeground", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.White), OnPropertyChanged));

        /// <summary>
        /// コントロールコードの文字色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush ControlChar
        {
            get { return (SolidColorBrush)GetValue(ControlCharProperty); }
            set { SetValue(ControlCharProperty, value); }
        }

        /// <summary>
        /// ControlCharの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty ControlCharProperty =
            DependencyProperty.Register("ControlChar", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnPropertyChanged));

        /// <summary>
        /// 選択時の背景色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush Hilight
        {
            get { return (SolidColorBrush)GetValue(HilightProperty); }
            set { SetValue(HilightProperty, value); }
        }

        /// <summary>
        /// Hilightの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty HilightProperty =
            DependencyProperty.Register("Hilight", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.DodgerBlue), OnPropertyChanged));

        /// <summary>
        /// キーワード１の文字色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush Keyword1
        {
            get { return (SolidColorBrush)GetValue(Keyword1Property); }
            set { SetValue(Keyword1Property, value); }
        }

        /// <summary>
        /// Keyword1の依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty Keyword1Property =
            DependencyProperty.Register("Keyword1", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Blue), OnPropertyChanged));

        /// <summary>
        /// キーワード2の文字色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush Keyword2
        {
            get { return (SolidColorBrush)GetValue(Keyword2Property); }
            set { SetValue(Keyword2Property, value); }
        }

        /// <summary>
        /// Keyword2の依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty Keyword2Property =
            DependencyProperty.Register("Keyword2", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.DarkCyan), OnPropertyChanged));

        /// <summary>
        /// コメントの文字色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush Comment
        {
            get { return (SolidColorBrush)GetValue(CommentProperty); }
            set { SetValue(CommentProperty, value); }
        }

        /// <summary>
        /// Commentの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty CommentProperty =
            DependencyProperty.Register("Comment", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Green), OnPropertyChanged));

        /// <summary>
        /// 文字リテラルの文字色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush Literal
        {
            get { return (SolidColorBrush)GetValue(LiteralProperty); }
            set { SetValue(LiteralProperty, value); }
        }

        /// <summary>
        /// Literalの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty LiteralProperty =
            DependencyProperty.Register("Literal", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Brown), OnPropertyChanged));

        /// <summary>
        /// URLの文字色を表す。これは依存プロパティです
        /// </summary>
        public SolidColorBrush URL
        {
            get { return (SolidColorBrush)GetValue(URLProperty); }
            set { SetValue(URLProperty, value); }
        }

        /// <summary>
        /// URLの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty URLProperty =
            DependencyProperty.Register("URL", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Blue), OnPropertyChanged));

        /// <summary>
        /// 行更新フラグの色を表す
        /// </summary>
        public SolidColorBrush UpdateArea
        {
            get { return (SolidColorBrush)GetValue(UpdateAreaProperty); }
            set { SetValue(UpdateAreaProperty, value); }
        }

        /// <summary>
        /// UpdateAreaの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty UpdateAreaProperty =
            DependencyProperty.Register("UpdateArea", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.MediumSeaGreen), OnPropertyChanged));

        /// <summary>
        /// ラインマーカーの色を表す
        /// </summary>
        public SolidColorBrush LineMarker
        {
            get { return (SolidColorBrush)GetValue(LineMarkerProperty); }
            set { SetValue(LineMarkerProperty, value); }
        }

        /// <summary>
        /// LineMarkerの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty LineMarkerProperty =
            DependencyProperty.Register("LineMarker", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnPropertyChanged));

        /// <summary>
        /// 挿入モード時のキャレットの色を表す
        /// </summary>
        public SolidColorBrush InsertCaret
        {
            get { return (SolidColorBrush)GetValue(InsertCaretProperty); }
            set { SetValue(InsertCaretProperty, value); }
        }

        /// <summary>
        /// InsertCaretの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty InsertCaretProperty =
            DependencyProperty.Register("InsertCaret", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Black), OnPropertyChanged));

        /// <summary>
        /// 上書きモード時のキャレット職を表す
        /// </summary>
        public SolidColorBrush OverwriteCaret
        {
            get { return (SolidColorBrush)GetValue(OverwriteCaretProperty); }
            set { SetValue(OverwriteCaretProperty, value); }
        }

        /// <summary>
        /// OverwriteCaretの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty OverwriteCaretProperty =
            DependencyProperty.Register("OverwriteCaret", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.Black), OnPropertyChanged));

        /// <summary>
        /// 行番号の色を表す
        /// </summary>
        public SolidColorBrush LineNumber
        {
            get { return (SolidColorBrush)GetValue(LineNumberProperty); }
            set { SetValue(LineNumberProperty, value); }
        }

        /// <summary>
        /// Using a DependencyProperty as the backing store for LineNumber.  This enables animation, styling, binding, etc...
        /// </summary>
        public static readonly DependencyProperty LineNumberProperty =
            DependencyProperty.Register("LineNumber", typeof(SolidColorBrush), typeof(FooTextBox), new PropertyMetadata(new SolidColorBrush(Colors.DimGray),OnPropertyChanged));

        /// <summary>
        /// 余白を表す
        /// </summary>
        public new Thickness Padding
        {
            get { return (Thickness)GetValue(PaddingProperty); }
            set { SetValue(PaddingProperty, value); }
        }

        /// <summary>
        /// Paddingの依存プロパティを表す
        /// </summary>
        public new static readonly DependencyProperty PaddingProperty =
            DependencyProperty.Register("Padding", typeof(Thickness), typeof(FooTextBox), new PropertyMetadata(new Thickness(),OnPropertyChanged));        

        /// <summary>
        /// 挿入モードなら真を返し、そうでないなら、偽を返す。これは依存プロパティです
        /// </summary>
        public bool InsertMode
        {
            get { return (bool)GetValue(InsertModeProperty); }
            set { SetValue(InsertModeProperty, value); }
        }

        /// <summary>
        /// InsertModeの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty InsertModeProperty =
            DependencyProperty.Register("InsertMode",
            typeof(bool),
            typeof(FooTextBox),
            new PropertyMetadata(true, OnPropertyChanged));

        /// <summary>
        /// タブの文字数を表す。これは依存プロパティです
        /// </summary>
        public int TabChars
        {
            get { return (int)GetValue(TabCharsProperty); }
            set { SetValue(TabCharsProperty, value); }
        }

        /// <summary>
        /// TabCharsの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty TabCharsProperty =
            DependencyProperty.Register("TabChars",
            typeof(int),
            typeof(FooTextBox),
            new PropertyMetadata(4, OnPropertyChanged));

        /// <summary>
        /// 矩形選択モードなら真を返し、そうでないなら偽を返す。これは依存プロパティです
        /// </summary>
        public bool RectSelectMode
        {
            get { return (bool)GetValue(RectSelectModeProperty); }
            set { SetValue(RectSelectModeProperty, value); }
        }

        /// <summary>
        /// RectSelectModeの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty RectSelectModeProperty =
            DependencyProperty.Register("RectSelectMode", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));

        /// <summary>
        /// 折り返しの方法を指定する
        /// </summary>
        /// <remarks>
        /// 変更した場合、レイアウトの再構築を行う必要があります
        /// </remarks>
        public LineBreakMethod LineBreakMethod
        {
            get { return (LineBreakMethod)GetValue(LineBreakProperty); }
            set { SetValue(LineBreakProperty, value); }
        }

        /// <summary>
        /// LineBreakMethodの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty LineBreakProperty =
            DependencyProperty.Register("LineBreakMethod", typeof(LineBreakMethod), typeof(FooTextBox), new PropertyMetadata(LineBreakMethod.None, OnPropertyChanged));

        /// <summary>
        /// 行の高さをem単位で指定します
        /// </summary>
        public double LineEmHeight
        {
            get { return (double)GetValue(LineEmHeightProperty); }
            set { SetValue(LineEmHeightProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BaseLineRaito.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineEmHeightProperty =
            DependencyProperty.Register("LineEmHeight", typeof(double), typeof(FooTextBox), new PropertyMetadata(1.6 ,OnPropertyChanged));



        /// <summary>
        /// 折り返しの幅を指定する。LineBreakMethod.CharUnit以外の時は無視されます
        /// </summary>
        /// <remarks>
        /// 変更した場合、レイアウトの再構築を行う必要があります
        /// </remarks>
        public int LineBreakCharCount
        {
            get { return (int)GetValue(LineBreakCharCountProperty); }
            set { SetValue(LineBreakCharCountProperty, value); }
        }

        /// <summary>
        /// LineBreakCharCountの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty LineBreakCharCountProperty =
            DependencyProperty.Register("LineBreakCharCount", typeof(int), typeof(FooTextBox), new PropertyMetadata(80, OnPropertyChanged));        

        /// <summary>
        /// キャレットを描くなら真。そうでないなら偽を返す。これは依存プロパティです
        /// </summary>
        public bool DrawCaret
        {
            get { return (bool)GetValue(DrawCaretProperty); }
            set { SetValue(DrawCaretProperty, value); }
        }

        /// <summary>
        /// DrawCaretの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty DrawCaretProperty =
            DependencyProperty.Register("DrawCaret", typeof(bool), typeof(FooTextBox), new PropertyMetadata(true, OnPropertyChanged));


        /// <summary>
        /// キャレットラインを描くなら真。そうでないなら偽を返す。これは依存プロパティです
        /// </summary>
        public bool DrawCaretLine
        {
            get { return (bool)GetValue(DrawCaretLineProperty); }
            set { SetValue(DrawCaretLineProperty, value); }
        }

        /// <summary>
        /// DrawCaretLineの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty DrawCaretLineProperty =
            DependencyProperty.Register("DrawCaretLine", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));

        /// <summary>
        /// 行番号を描くなら真。そうでなければ偽。これは依存プロパティです
        /// </summary>
        public bool DrawLineNumber
        {
            get { return (bool)GetValue(DrawLineNumberProperty); }
            set { SetValue(DrawLineNumberProperty, value); }
        }

        /// <summary>
        /// ルーラーを描くなら真。そうでなければ偽。これは依存プロパティです
        /// </summary>
        public bool DrawRuler
        {
            get { return (bool)GetValue(DrawRulerProperty); }
            set { SetValue(DrawRulerProperty, value); }
        }

        /// <summary>
        /// DrawRulerの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty DrawRulerProperty =
            DependencyProperty.Register("DrawRuler", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));


        /// <summary>
        /// DrawLineNumberの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty DrawLineNumberProperty =
            DependencyProperty.Register("DrawLineNumber", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));

        /// <summary>
        /// URLに下線を引くなら真。そうでないなら偽を表す。これは依存プロパティです
        /// </summary>
        public bool MarkURL
        {
            get { return (bool)GetValue(MarkURLProperty); }
            set { SetValue(MarkURLProperty, value); }
        }

        /// <summary>
        /// MarkURLの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty MarkURLProperty =
            DependencyProperty.Register("MarkURL", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));

        /// <summary>
        /// 全角スペースを表示するなら真。そうでないなら偽
        /// </summary>
        public bool ShowFullSpace
        {
            get { return (bool)GetValue(ShowFullSpaceProperty); }
            set { SetValue(ShowFullSpaceProperty, value); }
        }

        /// <summary>
        /// ShowFullSpaceの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty ShowFullSpaceProperty =
            DependencyProperty.Register("ShowFullSpace", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));

        /// <summary>
        /// 半角スペースを表示するなら真。そうでないなら偽
        /// </summary>
        public bool ShowHalfSpace
        {
            get { return (bool)GetValue(ShowHalfSpaceProperty); }
            set { SetValue(ShowHalfSpaceProperty, value); }
        }

        /// <summary>
        /// ShowHalfSpaceの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty ShowHalfSpaceProperty =
            DependencyProperty.Register("ShowHalfSpace", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));

        /// <summary>
        /// タブを表示するなら真。そうでないなら偽
        /// </summary>
        public bool ShowTab
        {
            get { return (bool)GetValue(ShowTabProperty); }
            set { SetValue(ShowTabProperty, value); }
        }

        /// <summary>
        /// ShowTabの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty ShowTabProperty =
            DependencyProperty.Register("ShowTab", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false, OnPropertyChanged));

        /// <summary>
        /// 改行マークを表示するなら真。そうでないなら偽
        /// </summary>
        public bool ShowLineBreak
        {
            get { return (bool)GetValue(ShowLineBreakProperty); }
            set { SetValue(ShowLineBreakProperty, value); }
        }

        /// <summary>
        /// ShowLineBreakの依存プロパティを表す
        /// </summary>
        public static readonly DependencyProperty ShowLineBreakProperty =
            DependencyProperty.Register("ShowLineBreak", typeof(bool), typeof(FooTextBox), new PropertyMetadata(false,OnPropertyChanged));

        
#endregion
    }
    /// <summary>
    /// コンテキストメニューのイベントデーターを表す
    /// </summary>
    public class FooContextMenuEventArgs
    {
        /// <summary>
        /// 処理済みなら真。そうでないなら偽
        /// </summary>
        public bool Handled = false;
        /// <summary>
        /// コンテキストメニューを表示すべき座標を表す
        /// </summary>
        public Windows.Foundation.Point Postion;
        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="pos"></param>
        public FooContextMenuEventArgs(Windows.Foundation.Point pos)
        {
            this.Postion = pos;
        }
    }
}
