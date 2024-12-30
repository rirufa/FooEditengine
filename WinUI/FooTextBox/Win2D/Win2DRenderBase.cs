using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Text;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Composition;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Graphics.DirectX;
using FooEditEngine;

namespace FooEditEngine.WinUI
{
    class Win2DRenderBase : IEditorRender
    {
        protected Win2DResourceFactory _factory;
        FontFamily fontFamily;
        public FontFamily FontFamily
        {
            get { return this.fontFamily; }
            set
            {
                this.fontFamily = value;
                this._format.FontFamily = value.Source;
                this.CaclulateTextMetrics();
                this.ChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Font));
            }
        }

        double _FontSize;
        public double FontSize
        {
            get
            {
                return this._FontSize;
            }
            set
            {
                this._FontSize = value;
                this._format.FontSize = GetDipFontSize((float)value);
                this.CaclulateTextMetrics();
                this.ChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Font));
            }
        }

        FontWeight fontWeigth;
        public FontWeight FontWeigth
        {
            get
            {
                return this.fontWeigth;
            }
            set
            {
                this.fontWeigth = value;
                this._format.FontWeight = value;
                this.CaclulateTextMetrics();
                this.ChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Font));
            }
        }

        FontStyle fontStyle;
        public FontStyle FontStyle
        {
            get
            {
                return this.fontStyle;
            }
            set
            {
                this.fontStyle = value;
                this._format.FontStyle = value;
                this.CaclulateTextMetrics();
                this.ChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Font));
            }
        }

        TextAntialiasMode _TextAntialiasMode;
        public TextAntialiasMode TextAntialiasMode
        {
            get
            {
                return this._TextAntialiasMode;
            }
            set
            {
                if (this.offScreenSession == null)
                    throw new InvalidOperationException();
                this._TextAntialiasMode = value;
                switch (value)
                {
                    case TextAntialiasMode.Aliased:
                        this.offScreenSession.TextAntialiasing = CanvasTextAntialiasing.Aliased;
                        break;
                    case TextAntialiasMode.Default:
                        this.offScreenSession.TextAntialiasing = CanvasTextAntialiasing.Auto;
                        break;
                    case TextAntialiasMode.ClearType:
                        this.offScreenSession.TextAntialiasing = CanvasTextAntialiasing.ClearType;
                        break;
                    case TextAntialiasMode.GrayScale:
                        this.offScreenSession.TextAntialiasing = CanvasTextAntialiasing.Grayscale;
                        break;
                }
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Antialias));
            }
        }

        Windows.UI.Color _Forground = Microsoft.UI.Colors.Black;
        public Windows.UI.Color Foreground
        {
            get
            {
                return this._Forground;
            }
            set
            {
                this._Forground = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _HilightForeground;
        public Windows.UI.Color HilightForeground
        {
            get
            {
                return this._HilightForeground;
            }
            set
            {
                this._HilightForeground = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _Background;
        public Windows.UI.Color Background
        {
            get
            {
                return this._Background;
            }
            set
            {
                this._Background = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _InsertCaret;
        public Windows.UI.Color InsertCaret
        {
            get
            {
                return this._InsertCaret;
            }
            set
            {
                this._InsertCaret = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _OverwriteCaret;
        public Windows.UI.Color OverwriteCaret
        {
            get
            {
                return this._OverwriteCaret;
            }
            set
            {
                this._OverwriteCaret = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _LineMarker;
        public Windows.UI.Color LineMarker
        {
            get
            {
                return this._LineMarker;
            }
            set
            {
                this._LineMarker = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _UpdateArea;
        public Windows.UI.Color UpdateArea
        {
            get
            {
                return this._UpdateArea;
            }
            set
            {
                this._UpdateArea = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _LineNumber;
        public Windows.UI.Color LineNumber
        {
            get
            {
                return this._LineNumber;
            }
            set
            {
                this._LineNumber = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _ControlChar;
        public Windows.UI.Color ControlChar
        {
            get
            {
                return this._ControlChar;
            }
            set
            {
                this._ControlChar = value;
                this._factory.ClearCahe();
                //隠し文字の色も定義する
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _URL;
        public Windows.UI.Color Url
        {
            get
            {
                return this._URL;
            }
            set
            {
                this._URL = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _Hilight;
        public Windows.UI.Color Hilight
        {
            get
            {
                return this._Hilight;
            }
            set
            {
                this._Hilight = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _Comment;
        public Windows.UI.Color Comment
        {
            get
            {
                return this._Comment;
            }
            set
            {
                this._Comment = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _Literal;
        public Windows.UI.Color Literal
        {
            get
            {
                return this._Literal;
            }
            set
            {
                this._Literal = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _Keyword1;
        public Windows.UI.Color Keyword1
        {
            get
            {
                return this._Keyword1;
            }
            set
            {
                this._Keyword1 = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        Windows.UI.Color _Keyword2;
        public Windows.UI.Color Keyword2
        {
            get
            {
                return this._Keyword2;
            }
            set
            {
                this._Keyword2 = value;
                this._factory.ClearCahe();
                this.OnChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Brush));
            }
        }

        bool _RightToLeft;
        public bool RightToLeft
        {
            get
            {
                return _RightToLeft;
            }
            set
            {
                _RightToLeft = value;
                this._format.Direction = GetDWRightDirection(value);
                this.ChangedRightToLeft(this, null);
            }
        }

        public Rectangle TextArea
        {
            get;
            set;
        }

        public double LineNemberWidth
        {
            get
            {
                return this.emSize.Width * EditView.LineNumberLength;
            }
        }

        int _TabWidthChar = 8;
        public int TabWidthChar
        {
            get { return this._TabWidthChar; }
            set
            {
                if (value == 0)
                    return;
                this._TabWidthChar = value;
                this.CaclulateTextMetrics();
            }
        }

        public bool ShowFullSpace
        {
            get;
            set;
        }
        public bool ShowHalfSpace
        {
            get;
            set;
        }
        public bool ShowTab
        {
            get;
            set;
        }
        public bool ShowLineBreak
        {
            get;
            set;
        }

        public Size emSize
        {
            get;
            private set;
        }

        public double FoldingWidth
        {
            get;
            private set;
        }

        double _LineEmHeight = 1.5;
        public double LineEmHeight
        {
            get
            {
                return _LineEmHeight;
            }
            set
            {
                _LineEmHeight = value;
                //フォントが変わった扱いにしないと反映されない
                this.ChangedRenderResource(this, new ChangedRenderRsourceEventArgs(ResourceType.Font));
            }
        }

        public event ChangedRenderResourceEventHandler ChangedRenderResource;
        public event EventHandler ChangedRightToLeft;

        static CanvasTextDirection GetDWRightDirection(bool value)
        {
            return value ? CanvasTextDirection.RightToLeftThenBottomToTop : CanvasTextDirection.LeftToRightThenTopToBottom;
        }

        private float GetDipFontSize(float fontSize)
        {
            return (float)fontSize * 96.0f / 72.0f; 
        }

        CanvasTextFormat _format;
        public void InitTextFormat(string fontName, float fontSize)
        {
            _format = new CanvasTextFormat();
            _format.FontFamily = fontName;
            _format.FontSize = GetDipFontSize(fontSize);
            _format.WordWrapping = CanvasWordWrapping.NoWrap;
            _format.Direction = GetDWRightDirection(this.RightToLeft);
            this.CaclulateTextMetrics();
        }

        public void ClearCache()
        {
            this._factory.ClearCahe();
        }

        void CaclulateTextMetrics()
        {
            this._factory.ClearCahe();

            float dpix, dpiy;
            Util.GetDpi(out dpix, out dpiy);

            var layout = this._factory.CreateTextLayout("0", _format, float.MaxValue, float.MaxValue, dpix, false);
            this.emSize = new Size(layout.ClusterMetrics[0].Width, layout.LineMetrics[0].Height);

            layout = this._factory.CreateTextLayout("+", _format, float.MaxValue, float.MaxValue, dpix, false);
            this.FoldingWidth = layout.ClusterMetrics[0].Width;

            this._format.IncrementalTabStop = (float)(this.emSize.Width * this._TabWidthChar);
        }

        public void OnChangedRenderResource(object sender, ChangedRenderRsourceEventArgs e)
        {
            if (this.ChangedRenderResource != null)
                this.ChangedRenderResource(sender, e);
        }

        protected CanvasDrawingSession offScreenSession = null;
        CanvasActiveLayer layer;
        public void BeginClipRect(Rectangle rect)
        {
            layer = this.offScreenSession.CreateLayer(1.0f, rect);
        }

        public void DrawMarkerEffect(Win2DTextLayout layout, HilightType type, int start, int length, double x, double y, bool isBold, Windows.UI.Color? effectColor = null)
        {
            if (type == HilightType.None)
                return;

            float thickness = isBold ? BoldThickness : NormalThickness;

            Windows.UI.Color color;
            if (effectColor != null)
                color = (Windows.UI.Color)effectColor;
            else if (type == HilightType.Select)
                color = this.Hilight;
            else
                color = this.Foreground;

            IMarkerEffecter effecter = null;
            CanvasSolidColorBrush brush = this._factory.CreateSolidColorBrush(color);

            var stroke = this._factory.CreateStrokeStyle(type);

            if (type == HilightType.Squiggle)
                effecter = new Win2DSquilleLineMarker(this.offScreenSession, brush, stroke, thickness);
            else if (type == HilightType.Select)
                effecter = new Win2DHilightMarker(this.offScreenSession, brush);
            else if (type == HilightType.None)
                effecter = null;
            else
                effecter = new Win2DLineMarker(this.offScreenSession, brush, stroke, thickness);

            if (effecter != null)
            {
                bool isUnderline = type != HilightType.Select;

                var metrics = layout.RawLayout.GetCharacterRegions(start, length);
                foreach (var metric in metrics)
                {
                    double offset = isUnderline ? metric.LayoutBounds.Height : 0;
                    var rect = metric.LayoutBounds;
                    rect.X += x;
                    rect.Y += y + offset;
                    effecter.Draw(rect.Left, rect.Top, rect.Width, rect.Height);
                }
            }
        }

        public ITextLayout CreateLaytout(string str, SyntaxInfo[] syntaxCollection, IEnumerable<Marker> MarkerRanges, IEnumerable<Selection> Selections, double WrapWidth)
        {
            float dpix, dpiy;
            Util.GetDpi(out dpix, out dpiy);

            double layoutWidth = this.TextArea.Width;
            if (WrapWidth != LineToIndexTable.NONE_BREAK_LINE)
            {
                this._format.WordWrapping = CanvasWordWrapping.Wrap;
                layoutWidth = WrapWidth;
            }
            else
            {
                this._format.WordWrapping = CanvasWordWrapping.NoWrap;
            }

            bool hasNewLine = str.Length > 0 && str[str.Length - 1] == Document.NewLine;
            Win2DTextLayout newLayout = new Win2DTextLayout(this._factory, str, this._format, layoutWidth, this.TextArea.Height, dpiy, hasNewLine && this.ShowLineBreak, this.emSize.Height * this.LineEmHeight);

            if (syntaxCollection != null)
            {
                foreach (SyntaxInfo s in syntaxCollection)
                {
                    CanvasSolidColorBrush brush = null;
                    switch (s.type)
                    {
                        case TokenType.Comment:
                            brush = this._factory.CreateSolidColorBrush(this.Comment);
                            break;
                        case TokenType.Keyword1:
                            brush = this._factory.CreateSolidColorBrush(this.Keyword1);
                            break;
                        case TokenType.Keyword2:
                            brush = this._factory.CreateSolidColorBrush(this.Keyword2);
                            break;
                        case TokenType.Literal:
                            brush = this._factory.CreateSolidColorBrush(this.Literal);
                            break;
                    }
                    newLayout.RawLayout.SetBrush(s.index, s.length, brush);
                }
            }

            if (MarkerRanges != null)
            {
                newLayout.Markers = MarkerRanges.ToArray();
                foreach (Marker sel in newLayout.Markers)
                {
                    if (sel.length == 0 || sel.start == -1)
                        continue;
                    if (sel.hilight == HilightType.Url)
                        newLayout.RawLayout.SetBrush(sel.start, sel.length, this._factory.CreateSolidColorBrush(this.Url));
                }
            }

            if (Selections != null)
            {
                newLayout.Selects = Selections.ToArray();
                if(this.HilightForeground.A > 0.0)
                {
                    foreach (Selection sel in Selections)
                    {
                        if (sel.length == 0 || sel.start == -1)
                            continue;

                        newLayout.RawLayout.SetBrush(sel.start, sel.length, this._factory.CreateSolidColorBrush(this.HilightForeground));
                    }
                }
            }

            this._format.WordWrapping = CanvasWordWrapping.NoWrap;

            return newLayout;
        }

        public void DrawGripper(Point p, double radius)
        {
            System.Numerics.Vector2 v = new System.Numerics.Vector2();
            v.X = (float)p.X;
            v.Y = (float)p.Y;
            this.offScreenSession.DrawCircle(v, (float)radius, this._factory.CreateSolidColorBrush(this.Foreground));
            this.offScreenSession.FillCircle(v, (float)radius, this._factory.CreateSolidColorBrush(this.Background));
        }

        public void DrawOneLine(Document doc, LineToIndexTable lti, int row, double x, double y)
        {
            int lineLength = lti.GetLengthFromLineNumber(row);

            if (lineLength == 0 || this.offScreenSession == null)
                return;

            Win2DTextLayout layout = (Win2DTextLayout)lti.GetLayout(row);

            if (layout.Markers != null)
            {
                foreach (Marker sel in layout.Markers)
                {
                    if (sel.length == 0 || sel.start == -1)
                        continue;
                    this.DrawMarkerEffect(layout, sel.hilight, sel.start, sel.length, x, y, sel.isBoldLine, new Windows.UI.Color() { A = sel.color.A, R = sel.color.R, B = sel.color.B, G = sel.color.G});
                }
            }
            if (layout.Selects != null)
            {
                foreach (Selection sel in layout.Selects)
                {
                    if (sel.length == 0 || sel.start == -1)
                        continue;

                    this.DrawMarkerEffect(layout, HilightType.Select, sel.start, sel.length, x, y, false);
                }
            }

            if (this.ShowFullSpace || this.ShowHalfSpace || this.ShowTab)
            {
                string str = lti[row];
                CanvasCachedGeometry geo = null;
                double lineHeight = this.emSize.Height * this.LineEmHeight;
                for(int i = 0; i < lineLength; i++)
                {
                    Point pos = new Point(0,0);
                    if(this.ShowTab && str[i] == '\t')
                    {
                        pos = layout.GetPostionFromIndex(i);
                        geo = this._factory.CreateSymbol(ShowSymbol.Tab, this._format, lineHeight);
                    }
                    else if(this.ShowFullSpace && str[i] == '　')
                    {
                        pos = layout.GetPostionFromIndex(i);
                        geo = this._factory.CreateSymbol(ShowSymbol.FullSpace, this._format, lineHeight);
                    }
                    else if (this.ShowHalfSpace && str[i] == ' ')
                    {
                        pos = layout.GetPostionFromIndex(i);
                        geo = this._factory.CreateSymbol(ShowSymbol.HalfSpace, this._format, lineHeight);
                    }
                    if(geo != null)
                    {
                        offScreenSession.DrawCachedGeometry(geo, (float)(x + pos.X), (float)(y + pos.Y), this._factory.CreateSolidColorBrush(this.ControlChar));
                        geo = null;
                    }
                }
            }

            offScreenSession.DrawTextLayout(layout.RawLayout, (float)x, (float)y, this._factory.CreateSolidColorBrush(this.Foreground));
        }

        public const int BoldThickness = 2;
        public const int NormalThickness = 1;

        public void DrawString(string str, double x, double y, StringAlignment align, Size layoutRect, StringColorType colorType = StringColorType.Forground)
        {
            CanvasSolidColorBrush brush;
            switch (colorType)
            {
                case StringColorType.Forground:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.Foreground);
                    break;
                case StringColorType.LineNumber:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.LineNumber);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            float dpix, dpiy;
            Util.GetDpi(out dpix, out dpiy);
            var layout = new Win2DTextLayout(this._factory, str, this._format, layoutRect.Width, layoutRect.Height, dpiy, false, this.emSize.Height * this.LineEmHeight);
            switch (align)
            {
                case StringAlignment.Left:
                    layout.RawLayout.HorizontalAlignment = CanvasHorizontalAlignment.Left;
                    break;
                case StringAlignment.Center:
                    layout.RawLayout.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                    break;
                case StringAlignment.Right:
                    layout.RawLayout.HorizontalAlignment = CanvasHorizontalAlignment.Right;
                    break;
            }
            this.offScreenSession.DrawTextLayout(layout.RawLayout, (float)x, (float)y, brush);
        }

        public void EndClipRect()
        {
            layer.Dispose();
        }

        public void DrawCachedBitmap(Rectangle dst,Rectangle src)
        {
        }

        public void DrawLine(Point from, Point to)
        {
            System.Numerics.Vector2 v1 = new System.Numerics.Vector2();
            v1.X = (float)from.X;
            v1.Y = (float)from.Y;
            System.Numerics.Vector2 v2 = new System.Numerics.Vector2();
            v2.X = (float)to.X;
            v2.Y = (float)to.Y;
            this.offScreenSession.DrawLine(v1, v2, new CanvasSolidColorBrush(this.offScreenSession, this.Foreground));
        }

        public void CacheContent()
        {
        }

        public bool IsVaildCache()
        {
            return false;
        }

        public void FillRectangle(Rectangle rect, FillRectType type)
        {
            CanvasSolidColorBrush brush = null;
            switch (type)
            {
                case FillRectType.OverwriteCaret:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.OverwriteCaret);
                    this.offScreenSession.FillRectangle(rect, brush);
                    break;
                case FillRectType.InsertCaret:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.InsertCaret);
                    this.offScreenSession.FillRectangle(rect, brush);
                    break;
                case FillRectType.InsertPoint:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.Hilight);
                    this.offScreenSession.FillRectangle(rect, brush);
                    break;
                case FillRectType.LineMarker:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.LineMarker);
                    this.offScreenSession.DrawRectangle(rect, brush, EditView.LineMarkerThickness);
                    break;
                case FillRectType.UpdateArea:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.UpdateArea);
                    this.offScreenSession.FillRectangle(rect, brush);
                    break;
                case FillRectType.Background:
                    brush = new CanvasSolidColorBrush(this.offScreenSession, this.Background);
                    this.offScreenSession.FillRectangle(rect, brush);
                    break;
            }
        }

        public void DrawFoldingMark(bool expand, double x, double y)
        {
            string mark = expand ? "-" : "+";
            this.DrawString(mark, x, y, StringAlignment.Left, new Size(this.FoldingWidth, this.emSize.Height));
        }

        public void FillBackground(Rectangle rect)
        {
            this.offScreenSession.Clear(this.Background);
        }

    }
}
