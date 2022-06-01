using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace D2dQuickResize
{
    public unsafe class QuickD2dWindow
    {
        public HWND HWND { get; set; }

        private bool deviceCreated = false;
        ID2D1Factory* d2dFactory = null;
        ID2D1HwndRenderTarget* d2dRenderTarget = null;
        DXGI_RGBA backgroundColor = new DXGI_RGBA(0.0f, 0.0f, 0.0f, 1.0f);
        DXGI_RGBA foregroundColor = new DXGI_RGBA(0.67f, 0.67f, 0.67f, 1.0f);
        ID2D1SolidColorBrush* backgroundBrush = null;
        ID2D1SolidColorBrush* foregroundBrush = null;

        uint width = 640;
        uint height = 360;

        bool isFullScreen = false;
        RECT lastRestoredPosition = default;

        public QuickD2dWindow()
        {
            HRESULT retval;
            fixed (ID2D1Factory** pd2dFactory = &d2dFactory)
                retval = DirectX.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_MULTI_THREADED, Windows.__uuidof<ID2D1Factory>(), (void**)pd2dFactory);


            if (retval.FAILED)
            {
                ShowErrorMessage($"D2D1CreateFactory returned 0x{retval:x} !", "FATAL ERROR");
                throw new Win32Exception(retval.Value);
            }
        }

        public LRESULT WindowProc (HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
        {
            // presume that we're not getting messages that don't match our HWND 
            switch(uMsg)
            {
                case WM.WM_PAINT:
                    DoPaint();
                    break;
                case WM.WM_SIZE:
                    DoResize();
                    break;
                case WM.WM_CLOSE:
                    Windows.PostQuitMessage(0);
                    Environment.Exit(0);
                    break;
                case WM.WM_KEYUP:
                    if (wParam.Value == VK.VK_F11)
                    {
                        if (isFullScreen)
                        {
                            // restore win
                            Windows.SetWindowLongPtr(
                                    HWND,
                                    GWL.GWL_STYLE,
                                    (nint)(WS.WS_OVERLAPPEDWINDOW));
                            Windows.ShowWindow(HWND, SW.SW_RESTORE);
                            Windows.SetWindowPos(HWND, HWND.NULL,
                                lastRestoredPosition.left,
                                lastRestoredPosition.top,
                                lastRestoredPosition.right -
                                lastRestoredPosition.left,
                                lastRestoredPosition.bottom -
                                lastRestoredPosition.top,
                                SWP.SWP_SHOWWINDOW);
                        }
                        else
                        {
                            // maximize win
                            RECT r = new RECT();
                            Windows.GetWindowRect(HWND, &r);
                            lastRestoredPosition = r;
                            unchecked
                            {
                                Windows.SetWindowLongPtr(
                                    HWND,
                                    GWL.GWL_STYLE,
                                    (nint)WS.WS_POPUP);
                                Windows.ShowWindow(HWND, SW.SW_MAXIMIZE);
                            }
                        }

                        isFullScreen = !isFullScreen;
                    }

                    
                    break;
            }
            return Windows.DefWindowProc (hwnd, uMsg, wParam, lParam);
        }

        private void DoPaint()
        {
            HRESULT retval = default;
            if(!deviceCreated)
            {
                deviceCreated = true;
                retval = CreateDeviceResources();
                // TODO: handle errors better
            }

            d2dRenderTarget->BeginDraw();

            DXGI_RGBA bgc = backgroundColor;
            d2dRenderTarget->Clear(&bgc);

            D2D_RECT_F bigRect = new D2D_RECT_F(5, 5, width - 5.0f, height - 5.0f);

            d2dRenderTarget->DrawRectangle(&bigRect, (ID2D1Brush*)foregroundBrush, 1.0f, null);

            D2D_RECT_F smallRect = new D2D_RECT_F(10, 10, MathF.Min(200, width - 10.0f), MathF.Min(200, height - 10.0f));

            d2dRenderTarget->DrawRectangle(&smallRect, (ID2D1Brush*)foregroundBrush, 1.0f, null);

            retval = d2dRenderTarget->EndDraw();
            if (retval == D2DERR.D2DERR_RECREATE_TARGET)
            {
                CreateDeviceResources();
            }
        }

        private void DoResize()
        {
            RECT tempRect = new RECT();
            Windows.GetClientRect(HWND, &tempRect);
            width = (uint)(tempRect.right - tempRect.left);
            height = (uint)(tempRect.bottom - tempRect.top);
            D2D_SIZE_U size = new D2D_SIZE_U(width, height);
            var retval = d2dRenderTarget->Resize(&size);
            if (retval == D2DERR.D2DERR_RECREATE_TARGET)
            {
                CreateDeviceResources();
            }
        }

        private HRESULT CreateDeviceResources()
        {
            HRESULT retval = -1;

            if (d2dRenderTarget != null)
            {
                d2dRenderTarget->Release();
                d2dRenderTarget = null;
            }

            var d2dRenderProperties = DirectX.RenderTargetProperties();

            RECT tempRect = new RECT();
            Windows.GetClientRect(HWND, &tempRect);
            width = (uint)(tempRect.right - tempRect.left);
            height = (uint)(tempRect.bottom - tempRect.top);
            D2D_SIZE_U size = new D2D_SIZE_U(width, height);

            var d2dHwndRenderProperties = DirectX.HwndRenderTargetProperties(HWND, size);
            d2dHwndRenderProperties.presentOptions = D2D1_PRESENT_OPTIONS.D2D1_PRESENT_OPTIONS_RETAIN_CONTENTS;

            fixed (ID2D1HwndRenderTarget** pd2dRenderTarget = &d2dRenderTarget)
                retval = d2dFactory->CreateHwndRenderTarget(&d2dRenderProperties, &d2dHwndRenderProperties, pd2dRenderTarget);


            if (retval.FAILED)
            {
                ShowErrorMessage($"CreateHwndRenderTarget returned 0x{retval:x} !", "FATAL ERROR");
                return retval;
            }

            if (backgroundBrush != null)
            {
                backgroundBrush->Release();
                backgroundBrush = null;
            }

            DXGI_RGBA bgc = backgroundColor;
            fixed (ID2D1SolidColorBrush** bb = &backgroundBrush)
                retval = d2dRenderTarget->CreateSolidColorBrush(&bgc, bb);

            if (retval.FAILED)
            {
                ShowErrorMessage($"CreateSolidColorBrush returned 0x{retval:x} !", "FATAL ERROR");
                Environment.Exit(retval);
            }

            if (foregroundBrush != null)
            {
                foregroundBrush->Release();
                foregroundBrush = null;
            }

            DXGI_RGBA fgc = foregroundColor;
            fixed (ID2D1SolidColorBrush** fb = &foregroundBrush)
                retval = d2dRenderTarget->CreateSolidColorBrush(&fgc, fb);

            if (retval.FAILED)
            {
                ShowErrorMessage($"CreateSolidColorBrush returned 0x{retval:x} !", "FATAL ERROR");
            }

            return retval;
        }

        public static unsafe void ShowErrorMessage(string message, string caption)
        {
            fixed (char* captionW = caption)
            {
                fixed (char* messageW = message)
                {
                    Windows.MessageBoxExW(HWND.NULL, (ushort*)messageW, (ushort*)captionW, MB.MB_OK, MB.MB_ICONERROR);
                }
            }
        }
    }
}
