using System.ComponentModel;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace D2dQuickResize
{
    public unsafe class Program
    {
        static ushort windowClassHandle;
        static List<QuickD2dWindow> windowList = new List<QuickD2dWindow>();

        public static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            HMODULE module = Windows.GetModuleHandleW(null);
            HWND hwnd = default;

            string windowClassName = Guid.NewGuid().ToString();
            fixed (char* szWindowClassName = windowClassName)
            {
                // init the class of window you want
                WNDCLASSEXW windowClassEx = new()
                {
                    cbSize = (uint)sizeof(WNDCLASSEXW),
                    style = CS.CS_HREDRAW | CS.CS_VREDRAW,
                    lpfnWndProc = &WndProc,
                    hInstance = module,
                    hCursor = Windows.LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW),
                    lpszClassName = (ushort*)szWindowClassName,
                    cbWndExtra = 4,
                };

                windowClassHandle = Windows.RegisterClassExW(&windowClassEx);

                if (windowClassHandle == 0)
                {
                    throw new Win32Exception((int)Windows.GetLastError());
                }

                RECT windowRectangle = new(50, 50, 640, 360);

                int adjustWindowsRectRetval = Windows.AdjustWindowRect(&windowRectangle, WS.WS_OVERLAPPEDWINDOW, Windows.FALSE);

                if (adjustWindowsRectRetval == 0)
                {
                    throw new Win32Exception((int)Windows.GetLastError());
                }

                var windowStyle = WS.WS_OVERLAPPEDWINDOW;
                fixed (char* pWindowTitle = "Am I a window title?")
                    hwnd = Windows.CreateWindowExW(
                        0,
                        windowClassEx.lpszClassName,
                        (ushort*)pWindowTitle,
                        (uint)windowStyle,
                        windowRectangle.left,
                        windowRectangle.top,
                        windowRectangle.right - windowRectangle.left,
                        windowRectangle.bottom - windowRectangle.top,
                        HWND.NULL,
                        HMENU.NULL,
                        module,
                        null);
                if (hwnd == HWND.NULL || hwnd == HWND.INVALID_VALUE)
                {
                    throw new Win32Exception((int)Windows.GetLastError());
                }
            }

            _ = Windows.ShowWindow(hwnd, SW.SW_SHOWDEFAULT);

            RECT rect = new();

            Windows.GetClientRect(hwnd, &rect);


            MSG msg = default;

            
            QuickD2dWindow window = new QuickD2dWindow();
            window.HWND = hwnd;
            windowList.Add(window);

            Windows.SetWindowLongPtrW(hwnd, 0, windowList.IndexOf(window));
            // Process any messages in the queue, you gotta do this or else Windows will think you're having a bad time. 
            while (msg.message != WM.WM_QUIT)
            {
                while (Windows.PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE) != 0)
                {
                    _ = Windows.TranslateMessage(&msg);
                    _ = Windows.DispatchMessageW(&msg);
                }
                // sleep harder
                Thread.Sleep(1);
            }
        }

        // quick dispatch, look up our QuickD2dWindow by index obtaining index by OS call
        [UnmanagedCallersOnly]
        private static unsafe LRESULT WndProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
        {
            if (windowList.Count == 0)
                return Windows.DefWindowProcW(hwnd, uMsg, wParam, lParam); ;
            QuickD2dWindow? win = null;
            int index = (int)Windows.GetWindowLongPtrW(hwnd, 0);
            win = windowList[index];
            if (win == null)
                return Windows.DefWindowProcW(hwnd, uMsg, wParam, lParam);
            return win.WindowProc(hwnd, uMsg, wParam, lParam);
        }
    }
}