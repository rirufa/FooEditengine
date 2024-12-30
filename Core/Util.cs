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
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Reflection;
using System.Collections;
#if WINUI
using Windows.Foundation.Diagnostics;
using Microsoft.UI.Xaml.Media.Imaging;
#endif

namespace FooEditEngine
{
    enum DebugLogLevel
    {
        Infomation, Important,
    }
    class DebugLog
    {
        public static void WriteLine(string message)
        {
            WriteLine(DebugLogLevel.Infomation, message);
        }

        public static void WriteLine(string message,params object[] args)
        {
            WriteLine(DebugLogLevel.Infomation, message, args);
        }

        public static void WriteLine(DebugLogLevel level,string message, params object[] args)
        {
#if DEBUG_DETAIL
            if(level == DebugLogLevel.Infomation) {
                if(args.Length > 0)
                    System.Diagnostics.Debug.WriteLine(message,args);
                else
                    System.Diagnostics.Debug.WriteLine(message);
            }
#elif DEBUG
            if (level == DebugLogLevel.Important) {
                if(args.Length > 0)
                    System.Diagnostics.Debug.WriteLine(message,args);
                else
                    System.Diagnostics.Debug.WriteLine(message);
            }
#endif
        }
    }
    class Util
    {
#if METRO || WINDOWS_UWP
        static float? _LogicalDpi;
        public static void GetDpi(out float dpix, out float dpiy)
        {
            if(_LogicalDpi == null)
                _LogicalDpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;
            dpix = _LogicalDpi.Value;
            dpiy = _LogicalDpi.Value;
        }

        public static double GetScale()
        {
            float dpi;
            Util.GetDpi(out dpi, out dpi);
            return dpi / 96.0;
        }

        public static Point GetClientPoint(Point screen, Windows.UI.Xaml.UIElement element)
        {
            //Windows10以降では補正する必要がある
            Windows.Foundation.Rect win_rect = Windows.UI.Xaml.Window.Current.CoreWindow.Bounds;
            screen = screen.Offset(-win_rect.X, -win_rect.Y);

            var gt = Windows.UI.Xaml.Window.Current.Content.TransformToVisual(element);
            return gt.TransformPoint(screen);
        }

        public static Point GetPointInWindow(Point client, Windows.UI.Xaml.UIElement element)
        {
            //ウィンドウ内での絶対座標を取得する
            var gt = element.TransformToVisual(Windows.UI.Xaml.Window.Current.Content);
            return gt.TransformPoint(client);
        }

        public static Point GetScreentPoint(Point client, Windows.UI.Xaml.UIElement element)
        {
            var gt = element.TransformToVisual(Windows.UI.Xaml.Window.Current.Content);
            Point p = gt.TransformPoint(client);

            //Windows10以降では補正する必要がある
            Windows.Foundation.Rect win_rect = Windows.UI.Xaml.Window.Current.CoreWindow.Bounds;
            var screenPoint = p.Offset(win_rect.X, win_rect.Y);
            return screenPoint;
        }
        public static Windows.Foundation.Rect GetClientRect(Windows.Foundation.Rect screen, Windows.UI.Xaml.UIElement element)
        {
            //Windows10以降では補正する必要がある
            Windows.Foundation.Rect win_rect = Windows.UI.Xaml.Window.Current.CoreWindow.Bounds;
            screen.X -= win_rect.X;
            screen.Y -= win_rect.Y;

            var gt = Windows.UI.Xaml.Window.Current.Content.TransformToVisual(element);
            return gt.TransformBounds(screen);
        }
        public static Windows.Foundation.Rect GetScreentRect(Windows.Foundation.Rect client, Windows.UI.Xaml.UIElement element)
        {
            //ウィンドウ内での絶対座標を取得する
            var gt = element.TransformToVisual(Windows.UI.Xaml.Window.Current.Content);
            Windows.Foundation.Rect screenRect = gt.TransformBounds(client);

            //Windows10以降では補正する必要がある
            Windows.Foundation.Rect win_rect = Windows.UI.Xaml.Window.Current.CoreWindow.Bounds;
            screenRect.X += win_rect.X;
            screenRect.Y += win_rect.Y;

            return screenRect;
        }
        public static IEnumerable<char> GetEnumrator(string s)
        {
            char[] chars = s.ToCharArray();
            return chars;
        }
#elif WINUI
        static float? _LogicalDpi = 96.0f;

        public static void SetDpi(float dpi)
        {
            _LogicalDpi = dpi;
        }

        public static void GetDpi(out float dpix, out float dpiy)
        {
            dpix = _LogicalDpi.Value;
            dpiy = _LogicalDpi.Value;
        }

        public static double GetScale()
        {
            float dpi;
            Util.GetDpi(out dpi, out dpi);
            return dpi / 96.0;
        }

        public static Point GetClientPoint(Point screen, Microsoft.UI.Xaml.UIElement element)
        {
            //Windows10以降では補正する必要がある
            var appWnd = GetAppWindow(element);
            screen = screen.Offset(-appWnd.Position.X - appWnd.Size.Width + appWnd.ClientSize.Width, -appWnd.Position.Y - appWnd.Size.Height + appWnd.ClientSize.Height);

            var gt = Microsoft.UI.Xaml.Window.Current.Content.TransformToVisual(element);
            return gt.TransformPoint(screen);
        }

        public static Point GetPointInWindow(Point client, Microsoft.UI.Xaml.UIElement element)
        {
            //ウィンドウ内での絶対座標を取得する
            var gt = element.TransformToVisual(element.XamlRoot.Content);
            return gt.TransformPoint(client);
        }

        public static Point GetScreentPoint(Point client, Microsoft.UI.Xaml.UIElement element)
        {
            double scale = GetScale();
            var gt = element.TransformToVisual(element.XamlRoot.Content);
            Point p = gt.TransformPoint(client);
            p = p.Scale(scale); //XamlRootの(0,0)からクライアント座標までは自前でスケーリングしないといけない

            //Windows10以降では補正する必要がある
            var appWnd = GetAppWindow(element);
            var screenPoint = p.Offset(appWnd.Position.X + appWnd.Size.Width - appWnd.ClientSize.Width, appWnd.Position.Y + appWnd.Size.Height - appWnd.ClientSize.Height);
            return screenPoint;
        }
        public static Windows.Foundation.Rect GetClientRect(Windows.Foundation.Rect screen, Microsoft.UI.Xaml.UIElement element)
        {
            //Windows10以降では補正する必要がある
            var appWnd = GetAppWindow(element);
            screen.X -= appWnd.Position.X;
            screen.Y -= appWnd.Position.Y;

            var gt = element.XamlRoot.Content.TransformToVisual(element);
            return gt.TransformBounds(screen);
        }
        public static Windows.Foundation.Rect GetScreentRect(Windows.Foundation.Rect client, Microsoft.UI.Xaml.UIElement element)
        {
            //ウィンドウ内での絶対座標を取得する
            var gt = element.TransformToVisual(element.XamlRoot.Content);
            Windows.Foundation.Rect screenRect = gt.TransformBounds(client);

            //Windows10以降では補正する必要がある
            var appWnd = GetAppWindow(element);
            screenRect.X += appWnd.Position.X;
            screenRect.Y += appWnd.Position.Y;

            return screenRect;
        }
        public static Microsoft.UI.Windowing.AppWindow GetAppWindow(Microsoft.UI.Xaml.UIElement element)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(FooEditEngine.WinUI.FooTextBox.OwnerWindow);
            var windowID = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWnd = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowID);
            return appWnd;
        }
#elif WPF
        public static void GetDpi(out float dpix, out float dpiy)
        {
            var dpiXProperty = typeof(System.Windows.SystemParameters).GetProperty("DpiX", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var dpiYProperty = typeof(System.Windows.SystemParameters).GetProperty("Dpi", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            dpix = (int)dpiXProperty.GetValue(null, null);
            dpiy = (int)dpiYProperty.GetValue(null, null);
        }

        public static double GetScale()
        {
            float dpi;
            Util.GetDpi(out dpi, out dpi);
            return dpi / 96.0;
        }

        public static Point GetClientPoint(Point screen, System.Windows.FrameworkElement element)
        {
            return element.PointFromScreen(screen);
        }

        public static Point GetScreentPoint(Point client, System.Windows.FrameworkElement element)
        {
            return element.PointFromScreen(client);
        }

        public static IEnumerable<char> GetEnumrator(string s)
        {
            return s;
        }
#else
        public static IEnumerable<char> GetEnumrator(string s)
        {
            return s;
        }
#endif

        public static T ConvertAbsIndexToRelIndex<T>(T n, int StartIndex, int Length) where T : IRange
        {
            n = Util.NormalizeIMaker<T>(n);

            int markerEnd = n.start + n.length - 1;

            int EndIndex = StartIndex + Length;

            if (n.start >= StartIndex && markerEnd <= EndIndex)
                n.start -= StartIndex;
            else if (n.start >= StartIndex && n.start <= EndIndex)
            {
                n.start -= StartIndex;
                n.length = EndIndex - StartIndex + 1;
            }
            else if (markerEnd >= StartIndex && markerEnd <= EndIndex)
            {
                n.start = 0;
                n.length = markerEnd - StartIndex + 1;
            }
            else if (n.start >= StartIndex && markerEnd <= EndIndex)
                n.start -= StartIndex;
            else if (n.start <= StartIndex && markerEnd > EndIndex)
            {
                n.start = 0;
                n.length = EndIndex - StartIndex + 1;
            }
            else
            {
                n.start = -1;
                n.length = 0;
            }
            return n;
        }

        public static bool IsWordSeparator(char c)
        {
            if (c == Document.NewLine || char.IsSeparator(c) || char.IsPunctuation(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.MathSymbol)
                return true;
            else
                return false;
        }
        public static void Swap<T>(ref T a, ref T b)
        {
            T c = b;
            b = a;
            a = c;
        }

        public static string Generate(char c, int count)
        {
            StringBuilder tabstr = new StringBuilder();
            for (int j = count; j > 0; j--)
                tabstr.Append(c);
            return tabstr.ToString();
        }

        public static Rectangle OffsetAndDeflate(Rectangle r, Size s)
        {
            return new Rectangle(r.X + s.Width,r.Y + s.Height, r.Width - s.Width, r.Height - s.Height);
        }

        public static T NormalizeIMaker<T>(T m) where T : IRange
        {
            if (m.length > 0)
                return m;
            m.start = m.start + m.length;
            m.length = Math.Abs(m.length);
            return m;
        }

        public static int RoundUp(double x)
        {
            return (int)(x + 0.5);
        }

    }
}
