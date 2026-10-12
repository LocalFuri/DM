#if UNITY_EDITOR && UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Process = System.Diagnostics.Process;

// Editor-only. Overrides WM_SETCURSOR for the GameView child HWND, but ONLY
// while the pointer is inside the supplied GameView rectangle and an item is held.
// Never calls SetSystemCursor or changes the cursor for other applications.
[InitializeOnLoad]
internal static class WindowsHeldItemCursor
{
    private const uint WM_SETCURSOR = 0x0020;
    private const uint GA_ROOT = 2;
    private const uint DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;
    private static readonly UIntPtr HookId = (UIntPtr)0x444D4355u;
    private delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
    private static readonly SubclassProc Proc = HandleMessage;
    private static IntPtr hwndHooked, itemCursor;
    private static Texture2D itemTexture;
    private static Rect rectScreenPixels;
    private static bool active;
    // Native window callback only updates counters. Logging is deferred to
    // EditorApplication.update to avoid calling Unity APIs in a Win32 callback.
    private static int cursorMessages, cursorOverrides, diagnosticFrames;
    private static int setCursorAttempts, setCursorMatched, setCursorMismatched;
    private static IntPtr lastBeforeCursor, lastAfterCursor;
    private static int rejectedInactive, rejectedMissingCursor, rejectedPointerRead,
        rejectedOutsideRect;
    private static int observedMinX = int.MaxValue, observedMinY = int.MaxValue,
        observedMaxX = int.MinValue, observedMaxY = int.MinValue;
    private static int lastCursorHitTest;
    // Read-only global cursor sampling: unlike GetCursor(), GetCursorInfo
    // reports the cursor currently displayed by Windows across UI threads.
    private static int cursorSamples, cursorSamplesTorch, cursorSamplesOther;
    private static IntPtr lastObservedGlobalCursor;
    private static int cursorCorrections, cursorCorrectionFailures;
    [StructLayout(LayoutKind.Sequential)] private struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(ref CURSORINFO info);


    private static bool diagnosticPending;
    private static IntPtr lastWindowUnderProbe;
    private static IntPtr lastProbeTarget = IntPtr.Zero;
    private static int lastProbeReason = -1;
    private static int hookAttempts, hookSuccesses, hookFailures;
    private static int outsideProbeCount, crossThreadProbeCount;


    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public uint xHotspot, yHotspot;
        public IntPtr hbmMask, hbmColor;
    }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT pt);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("user32.dll")] private static extern IntPtr GetCursor();
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr module, IntPtr cursor);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern IntPtr CreateIconIndirect(ref ICONINFO icon);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateBitmap(int w, int h, uint planes, uint bpp, byte[] pixels);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("comctl32.dll", EntryPoint="SetWindowSubclass", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll", EntryPoint="RemoveWindowSubclass")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, UIntPtr id);
    [DllImport("comctl32.dll", EntryPoint="DefSubclassProc")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);

    static WindowsHeldItemCursor()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Reset;
        EditorApplication.quitting += Reset;
        EditorApplication.update += ReportDiagnostics;
    }

    private static IntPtr HandleMessage(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        if (msg == WM_SETCURSOR)
        {
            System.Threading.Interlocked.Increment(ref cursorMessages);
            // Count *why* messages do not install the cursor; do not log or
            // access Unity objects from the native Windows callback.
            lastCursorHitTest = unchecked((ushort)(lParam.ToInt64() & 0xffff));
            if (!active)
                System.Threading.Interlocked.Increment(ref rejectedInactive);
            else if (itemCursor == IntPtr.Zero)
                System.Threading.Interlocked.Increment(ref rejectedMissingCursor);
            else if (!GetCursorPos(out var pt))
                System.Threading.Interlocked.Increment(ref rejectedPointerRead);
            else
            {
                if (pt.x < observedMinX) observedMinX = pt.x;
                if (pt.x > observedMaxX) observedMaxX = pt.x;
                if (pt.y < observedMinY) observedMinY = pt.y;
                if (pt.y > observedMaxY) observedMaxY = pt.y;
                if (pt.x >= rectScreenPixels.xMin && pt.x < rectScreenPixels.xMax
                    && pt.y >= rectScreenPixels.yMin && pt.y < rectScreenPixels.yMax)
                {
                    System.Threading.Interlocked.Increment(ref setCursorAttempts);
                    lastBeforeCursor = GetCursor();
                    SetCursor(itemCursor);
                    lastAfterCursor = GetCursor();
                    if (lastAfterCursor == itemCursor)
                    {
                        System.Threading.Interlocked.Increment(ref setCursorMatched);
                        System.Threading.Interlocked.Increment(ref cursorOverrides);
                    }
                    else
                        System.Threading.Interlocked.Increment(ref setCursorMismatched);
                    return new IntPtr(1);
                }
                System.Threading.Interlocked.Increment(ref rejectedOutsideRect);
            }
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    internal static void Apply(EditorWindow gameView, Texture2D texture)
    {
        if (gameView == null || texture == null || Application.isPlaying) { Reset(); return; }
        float ppp = EditorGUIUtility.pixelsPerPoint;
        if (ppp <= 0) ppp = 1;
        var window = gameView.position;
        // Screen-space bounds: the window may be docked. Restrict to its rectangle.
        rectScreenPixels = new Rect(window.x * ppp, window.y * ppp, window.width * ppp, window.height * ppp);
        if (itemTexture != texture || itemCursor == IntPtr.Zero)
        {
            Reset();
            itemCursor = CreateCursor(texture);
            if (itemCursor == IntPtr.Zero) return;
            itemTexture = texture;
            Debug.Log("ITEM CURSOR DIAG: native cursor created; texture=" + texture.name
                + "; size=" + texture.width + "x" + texture.height
                + "; handle=0x" + itemCursor.ToInt64().ToString("X"));
        }
        // The pointer may be over a child GameView HWND rather than its top-level
        // Unity window. Only subclass an HWND owned by this process AND the
        // current thread: comctl32 SetWindowSubclass is not cross-thread safe.
        TryHookPointerWindow();
        if (!active)
        {
            active = true;
            cursorMessages = 0;
            cursorOverrides = 0;
            setCursorAttempts = setCursorMatched = setCursorMismatched = 0;
            cursorSamples = cursorSamplesTorch = cursorSamplesOther = 0;
            cursorCorrections = cursorCorrectionFailures = 0;
            lastObservedGlobalCursor = IntPtr.Zero;
            lastBeforeCursor = lastAfterCursor = IntPtr.Zero;
            rejectedInactive = rejectedMissingCursor = rejectedPointerRead = rejectedOutsideRect = 0;
            observedMinX = observedMinY = int.MaxValue;
            observedMaxX = observedMaxY = int.MinValue;
            lastCursorHitTest = -1;
            diagnosticFrames = 0;
            diagnosticPending = true;
            Debug.Log("ITEM CURSOR DIAG: activated; collecting WM_SETCURSOR messages");
            // Immediate presentation, once. WM_SETCURSOR owns later changes.
            if (GetCursorPos(out var current) && rectScreenPixels.Contains(new Vector2(current.x, current.y)))
                SetCursor(itemCursor);
        }
    }

    // Report each distinct failed condition once; no native callback logging.
    // A foreign-thread HWND must never be subclassed with SetWindowSubclass.
    private static void ReportProbe(IntPtr target, int reason, string details)
    {
        if (target == lastProbeTarget && reason == lastProbeReason) return;
        lastProbeTarget = target;
        lastProbeReason = reason;
        Debug.Log("ITEM CURSOR DIAG: child hook probe " + details);
    }

    private static void TryHookPointerWindow()
    {
        if (!GetCursorPos(out POINT mouse))
        {
            ReportProbe(IntPtr.Zero, 1, "GetCursorPos failed; Win32=" + Marshal.GetLastWin32Error());
            return;
        }
        if (!rectScreenPixels.Contains(new Vector2(mouse.x, mouse.y)))
        {
            outsideProbeCount++;
            ReportProbe(IntPtr.Zero, 2, "pointer outside GameView bounds; mouse=("
                + mouse.x + "," + mouse.y + "); bounds=" + rectScreenPixels);
            return;
        }
        IntPtr target = WindowFromPoint(mouse);
        if (target == IntPtr.Zero)
        {
            ReportProbe(IntPtr.Zero, 3, "WindowFromPoint returned zero at ("
                + mouse.x + "," + mouse.y + ")");
            return;
        }
        // Do not chase changing child HWNDs continuously. The first safely
        // subclassed GameView window is kept until Reset (Restore/reload).
        // Re-hooking on every pointer transition can cause cursor flicker.
        if (hwndHooked != IntPtr.Zero)
            return;
        uint targetThread = GetWindowThreadProcessId(target, out uint targetProcess);
        uint currentThread = GetCurrentThreadId();
        uint currentProcess = (uint)Process.GetCurrentProcess().Id;
        IntPtr root = GetAncestor(target, GA_ROOT);
        string candidate = "target=0x" + target.ToInt64().ToString("X")
            + "; root=0x" + root.ToInt64().ToString("X")
            + "; previousHook=0x" + hwndHooked.ToInt64().ToString("X")
            + "; process=" + targetProcess + "/" + currentProcess
            + "; thread=" + targetThread + "/" + currentThread;
        if (targetProcess != currentProcess || targetThread != currentThread || targetThread == 0)
        {
            crossThreadProbeCount++;
            ReportProbe(target, 5, "UNSAFE; refusing SetWindowSubclass; " + candidate);
            lastWindowUnderProbe = target;
            return;
        }
        ReportProbe(target, 6, "safe same-thread candidate; attempting SetWindowSubclass; " + candidate);
        // Install once. A different child HWND is a diagnostic question, not
        // permission to replace a live subclass while the cursor is moving.
        hookAttempts++;
        if (!SetWindowSubclass(target, Proc, HookId, UIntPtr.Zero))
        {
            hookFailures++;
            int error = Marshal.GetLastWin32Error();
            Debug.LogError("ITEM CURSOR DIAG: SetWindowSubclass FAILED; " + candidate
                + "; Win32=" + error + "; attempts=" + hookAttempts);
            return;
        }
        hookSuccesses++;
        hwndHooked = target;
        lastWindowUnderProbe = target;
        Debug.Log("ITEM CURSOR DIAG: child hook INSTALLED; " + candidate
            + "; successes=" + hookSuccesses);
    }

    // Unity can replace our cursor without sending WM_SETCURSOR to the
    // subclassed GameView window. Repair only that observed mismatch, and
    // never while the pointer is outside the hooked GameView window.
    // This does not install new hooks or update Unity's rendered textures.
    private static void MaintainHeldItemCursor()
    {
        if (!active || itemCursor == IntPtr.Zero || hwndHooked == IntPtr.Zero)
            return;
        if (!GetCursorPos(out POINT point)
            || !rectScreenPixels.Contains(new Vector2(point.x, point.y)))
            return;
        IntPtr underPointer = WindowFromPoint(point);
        if (underPointer == IntPtr.Zero)
            return;
        // Do not force a cursor over other editor windows or floating panels.
        IntPtr hookedRoot = GetAncestor(hwndHooked, GA_ROOT);
        if (underPointer != hwndHooked
            && GetAncestor(underPointer, GA_ROOT) != hookedRoot)
            return;
        var info = new CURSORINFO { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
        if (!GetCursorInfo(ref info) || info.hCursor == itemCursor)
            return;
        SetCursor(itemCursor);
        ++cursorCorrections;
        // A failed correction is reported in the existing one-time summary.
        if (GetCursorInfo(ref info) && info.hCursor != itemCursor)
            ++cursorCorrectionFailures;
    }

    private static void ReportDiagnostics()
    {
        if (active)
        {
            TryHookPointerWindow();
            MaintainHeldItemCursor();
        }
        if (!diagnosticPending || !active) return;
        ++diagnosticFrames;
        // Observe only. Never force SetCursor from this editor update loop.
        if (diagnosticFrames % 5 == 0)
        {
            var info = new CURSORINFO { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
            if (GetCursorInfo(ref info))
            {
                ++cursorSamples;
                lastObservedGlobalCursor = info.hCursor;
                if (info.hCursor == itemCursor) ++cursorSamplesTorch;
                else ++cursorSamplesOther;
            }
        }
        if (diagnosticFrames < 120) return;
        diagnosticPending = false;
        POINT pointer;
        IntPtr under = GetCursorPos(out pointer) ? WindowFromPoint(pointer) : IntPtr.Zero;
        IntPtr root = under == IntPtr.Zero ? IntPtr.Zero : GetAncestor(under, GA_ROOT);
        Debug.Log("ITEM CURSOR DIAG: after 120 editor updates; WM_SETCURSOR="
            + cursorMessages + "; overrides=" + cursorOverrides
            + "; pointerWindow=0x" + under.ToInt64().ToString("X")
            + "; pointerRoot=0x" + root.ToInt64().ToString("X")
            + "; hooked=0x" + hwndHooked.ToInt64().ToString("X")
            + "; mousePhysicalPixels=(" + pointer.x + "," + pointer.y + ")"
            + "; setCursor[attempted=" + setCursorAttempts
            + ", matched=" + setCursorMatched
            + ", mismatched=" + setCursorMismatched + "]"
            + "; globalCursorSamples[torch=" + cursorSamplesTorch
            + ", other=" + cursorSamplesOther
            + ", total=" + cursorSamples
            + ", last=0x" + lastObservedGlobalCursor.ToInt64().ToString("X") + "]"
            + "; cursorHandles[expected=0x" + itemCursor.ToInt64().ToString("X")
            + ", before=0x" + lastBeforeCursor.ToInt64().ToString("X")
            + ", after=0x" + lastAfterCursor.ToInt64().ToString("X") + "]"
            + "; rejects[inactive=" + rejectedInactive
            + ", noCursor=" + rejectedMissingCursor
            + ", pointerRead=" + rejectedPointerRead
            + ", outsideBounds=" + rejectedOutsideRect + "]"
            + "; expectedBounds=" + rectScreenPixels
            + "; observedCursorRange=[" + observedMinX + ".." + observedMaxX
            + ", " + observedMinY + ".." + observedMaxY + "]"
            + "; lastHitTest=" + lastCursorHitTest
            + "; cursorCorrections=" + cursorCorrections
            + "; correctionFailures=" + cursorCorrectionFailures
            + "; hookAttempts=" + hookAttempts
            + "; hookSuccesses=" + hookSuccesses
            + "; hookFailures=" + hookFailures
            + "; unsafeProbes=" + crossThreadProbeCount
            + "; outsideProbes=" + outsideProbeCount);
    }

    internal static void Reset()
    {
        if (active && diagnosticPending)
            Debug.Log("ITEM CURSOR DIAG: reset before sample; WM_SETCURSOR="
                + cursorMessages + "; overrides=" + cursorOverrides);
        diagnosticPending = false;
        active = false;
        if (hwndHooked != IntPtr.Zero)
        {
            RemoveWindowSubclass(hwndHooked, Proc, HookId);
            hwndHooked = IntPtr.Zero;
        }
        if (itemCursor != IntPtr.Zero) { DestroyIcon(itemCursor); itemCursor = IntPtr.Zero; }
        itemTexture = null;
        SetCursor(LoadCursor(IntPtr.Zero, new IntPtr(32512)));
    }

    private static IntPtr CreateCursor(Texture2D tex)
    {
        if (tex.width != 16 || tex.height != 16 || !tex.isReadable)
        {
            Debug.LogError("ITEM CURSOR: expected readable 16x16 texture: " + tex.name);
            return IntPtr.Zero;
        }
        Color32[] src = tex.GetPixels32();
        byte[] bgra = new byte[16 * 16 * 4];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                Color32 c = src[(15-y)*16 + x];
                int i = (y*16+x)*4;
                bgra[i] = c.b; bgra[i+1] = c.g; bgra[i+2] = c.r; bgra[i+3] = c.a;
            }
        IntPtr color = CreateBitmap(16, 16, 1, 32, bgra);
        IntPtr mask = CreateBitmap(16, 16, 1, 1, new byte[32]);
        if (color == IntPtr.Zero || mask == IntPtr.Zero)
        {
            if (color != IntPtr.Zero) DeleteObject(color);
            if (mask != IntPtr.Zero) DeleteObject(mask);
            Debug.LogError("ITEM CURSOR: could not create Windows bitmaps");
            return IntPtr.Zero;
        }
        var info = new ICONINFO { fIcon = false, xHotspot = 8, yHotspot = 8, hbmColor = color, hbmMask = mask };
        IntPtr result = CreateIconIndirect(ref info);
        DeleteObject(color); DeleteObject(mask);
        if (result == IntPtr.Zero) Debug.LogError("ITEM CURSOR: CreateIconIndirect failed");
        return result;
    }
}
#endif

// GameView child HWND same-thread hook diagnostic revision 2026-10-12-C

// Cursor stable-hook / SetCursor verification diagnostic: 2026-10-12-E.
