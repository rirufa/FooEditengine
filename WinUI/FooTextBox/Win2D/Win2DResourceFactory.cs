using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Text;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition;
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
    public enum ShowSymbol
    {
        HalfSpace,
        FullSpace,
        Tab,
    }

    class Win2DResourceFactory
    {
        ResourceManager<Windows.UI.Color, CanvasSolidColorBrush> color_brush_cache = new ResourceManager<Windows.UI.Color, CanvasSolidColorBrush>();
        ResourceManager<HilightType, CanvasStrokeStyle> stroke_style_cache = new ResourceManager<HilightType, CanvasStrokeStyle>();
        ResourceManager<ShowSymbol, CanvasCachedGeometry> symbol_cache = new ResourceManager<ShowSymbol, CanvasCachedGeometry>();

        CanvasDevice _Device;
        public CanvasDevice Device
        {
            get
            {
                return this._Device;
            }
        }

        public Win2DResourceFactory()
        {
            this._Device = CanvasDevice.GetSharedDevice();
        }

        public Win2DResourceFactory(CanvasDevice device)
        {
            this._Device = device;
        }

        public void ClearCahe()
        {
            this.color_brush_cache.Clear();
            this.stroke_style_cache.Clear();
            this.symbol_cache.Clear();
        }

        public bool ProcessDeviceLost(int hresult)
        {
            if (this._Device.IsDeviceLost(hresult) || hresult == -2144665568)   //E_SURFACE_CONTENTS_LOST
            {
                this.color_brush_cache.Clear();
                this._Device = CanvasDevice.GetSharedDevice();
                return true;
            }
            return false;
        }

        public CanvasSolidColorBrush CreateSolidColorBrush(Windows.UI.Color key)
        {
            CanvasSolidColorBrush brush;

            bool result = color_brush_cache.TryGetValue(key, out brush);
            if (!result)
            {
                brush = new CanvasSolidColorBrush(this._Device, key);
                color_brush_cache.Add(key, brush);
            }

            return brush;
        }

        public CanvasStrokeStyle CreateStrokeStyle(HilightType key)
        {
            CanvasStrokeStyle stroke;
            bool result = stroke_style_cache.TryGetValue(key, out stroke);
            if (!result)
            {
                stroke = new CanvasStrokeStyle();
                stroke.DashCap = CanvasCapStyle.Flat;
                stroke.DashOffset = 0;
                stroke.DashStyle = CanvasDashStyle.Solid;
                stroke.EndCap = CanvasCapStyle.Flat;
                stroke.LineJoin = CanvasLineJoin.Miter;
                stroke.MiterLimit = 0;
                stroke.StartCap = CanvasCapStyle.Flat;
                switch (key)
                {
                    case HilightType.Sold:
                    case HilightType.Url:
                    case HilightType.Squiggle:
                        stroke.DashStyle = CanvasDashStyle.Solid;
                        break;
                    case HilightType.Dash:
                        stroke.DashStyle = CanvasDashStyle.Dash;
                        break;
                    case HilightType.DashDot:
                        stroke.DashStyle = CanvasDashStyle.DashDot;
                        break;
                    case HilightType.DashDotDot:
                        stroke.DashStyle = CanvasDashStyle.DashDotDot;
                        break;
                    case HilightType.Dot:
                        stroke.DashStyle = CanvasDashStyle.Dot;
                        break;
                }
                stroke_style_cache.Add(key, stroke);
            }

            return stroke;
        }

        public CanvasTextLayout CreateTextLayout(string str, CanvasTextFormat format, double width, double height, float dip, bool showLineBreak)
        {
            CanvasTextLayout layout;
            layout = new CanvasTextLayout(this.Device, str, format, (float)width, (float)height);
            return layout;
        }

        public CanvasCachedGeometry CreateSymbol(ShowSymbol sym, CanvasTextFormat format,double lineHeight)
        {
            CanvasCachedGeometry cached_geo;

            bool result = symbol_cache.TryGetValue(sym, out cached_geo);

            if(!result)
            {
                const int margin = 2;
                const int half_space_circle_radious = 1;
                CanvasGeometry geo = null;
                Windows.Foundation.Rect rect;
                CanvasTextLayout layout = null;
                CanvasPathBuilder path = null;
                CanvasStrokeStyle stroke = this.CreateStrokeStyle(HilightType.Sold);
                switch (sym)
                {
                    case ShowSymbol.FullSpace:
                        layout = new CanvasTextLayout(this.Device, "　", format, float.MaxValue, float.MaxValue);
                        rect = layout.GetCharacterRegions(0, 1)[0].LayoutBounds;
                        rect.Width = Math.Max(1, rect.Width - margin * 2);
                        rect.Height = Math.Max(1, lineHeight - margin * 2);
                        rect.X += margin;
                        rect.Y += margin;
                        geo = CanvasGeometry.CreateRectangle(this.Device, rect);
                        break;
                    case ShowSymbol.HalfSpace:
                        layout = new CanvasTextLayout(this.Device, " ", format, float.MaxValue, float.MaxValue);
                        rect = layout.GetCharacterRegions(0, 1)[0].LayoutBounds;
                        geo = CanvasGeometry.CreateCircle(_Device,(float)(rect.Width / 2),(float)(lineHeight / 2), half_space_circle_radious);
                        break;
                    case ShowSymbol.Tab:
                        path = new CanvasPathBuilder(this.Device);
                        path.BeginFigure(0, 0);
                        path.AddLine((float)0, (float)lineHeight);
                        path.EndFigure(CanvasFigureLoop.Open);
                        geo = CanvasGeometry.CreatePath(path);
                        stroke = this.CreateStrokeStyle(HilightType.Dash);
                        break;
                }
                cached_geo = CanvasCachedGeometry.CreateStroke(geo, Win2DRenderBase.NormalThickness, stroke);
                symbol_cache.Add(sym, cached_geo);
            }
            return cached_geo;
        }
    }
}
