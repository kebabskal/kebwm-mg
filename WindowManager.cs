using System.Drawing;
using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Vanara.PInvoke;

using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Point = System.Drawing.Point;
using System.Diagnostics;
using System.Text;

class WindowManager {
	// Interface
	public Dictionary<HWND, Window> Windows => windows;
	public Window ForegroundWindow { get; private set; }

	// Events
	public event System.Action<Window> WindowCreated;
	public event System.Action<Window> WindowDestroyed;
	public event System.Action<Window> ForegroundWindowChanged;
	public event System.Action<Window> WindowRectangleChanged;
	public event System.Action<Window> WindowTitleChanged;

	// Internals
	Dictionary<HWND, Window> windows = new Dictionary<HWND, Window>();
	HWND lastForegroundWindowHandle;

	// Utils
	void log(string message) {
		Console.WriteLine(message);
	}

	public GraphicsDeviceManager Graphics => graphics;
	GraphicsDeviceManager graphics;

	public WindowManager(GraphicsDeviceManager graphics) {
		this.graphics = graphics;
	}

	public static string GetText(HWND _hwnd) {
		// Allocate correct string length first
		var hwnd = _hwnd.DangerousGetHandle();
		int length = User32.GetWindowTextLength(hwnd);
		StringBuilder sb = new StringBuilder(length + 1);
		User32.GetWindowText(hwnd, sb, sb.Capacity);
		return sb.ToString();
	}

	public void Update() {
		var hwnds = new List<HWND>();
		var currentProcess = Process.GetCurrentProcess();
		User32.EnumWindows((hwnd, b) => {
			hwnds.Add(hwnd);
			var rect = new RECT();
			var title = GetText(hwnd);
			User32.GetWindowRect(hwnd, out rect);

			Window window = null;
			if (!windows.ContainsKey(hwnd)) {
				uint processId = 0;
				User32.GetWindowThreadProcessId(hwnd, out processId);
				var process = Process.GetProcessById((int)processId);

				// Do not handle own process
				if (currentProcess.Id == process.Id)
					return true;

				window = new Window(this, hwnd, process) {
					Title = title.ToString(),
					Rectangle = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height),
				};
				windows.Add(hwnd, window);

				OnWindowCreated(window);
			} else {
				window = windows[hwnd];
			}

			if (title.ToString() != window.Title)
				OnWindowTitleChanged(window, title.ToString());

			if (
				rect.X != window.Rectangle.X ||
				rect.Y != window.Rectangle.Y ||
				rect.Width != window.Rectangle.Width ||
				rect.Height != window.Rectangle.Height
			) {
				OnWindowRectangleChanged(window, rect.Location, rect.Size);
			}

			return true;
		}, IntPtr.Zero);

		// Remove all windows that no longer exists
		var removedWindows = windows.Where(window => !hwnds.Contains(window.Key)).Select(window => window);
		foreach (var window in removedWindows) {
			OnWindowDestroyed(window.Value);
			windows.Remove(window.Key);
		}

		// Update foreground window
		var foregroundWindowHandle = User32.GetForegroundWindow();
		if (foregroundWindowHandle != lastForegroundWindowHandle) {
			if (windows.ContainsKey(foregroundWindowHandle)) {
				ForegroundWindow = windows[foregroundWindowHandle];
			} else {
				ForegroundWindow = null;
			}
			OnForegroundWindowChanged(ForegroundWindow);
			lastForegroundWindowHandle = foregroundWindowHandle;
		}
	}

	void OnWindowDestroyed(Window window) {
		log($"OnWindowDestroyed ({window})");
		WindowDestroyed?.Invoke(window);
	}

	void OnForegroundWindowChanged(Window window) {
		log($"OnForegroundWindowChanged: {window}");
		ForegroundWindowChanged?.Invoke(window);
	}

	void OnWindowCreated(Window window) {
		log($"OnWindowCreated: {window}");
		WindowCreated?.Invoke(window);
	}

	void OnWindowRectangleChanged(Window window, Point location, Size size) {
		window.Rectangle = new Rectangle(location.X, location.Y, size.Width, size.Height);
		log($"OnWindowRectangleChanged: {window}");
		WindowRectangleChanged?.Invoke(window);
	}

	void OnWindowTitleChanged(Window window, string title) {
		window.Title = title;
		log($"OnWindowTitleChanged: {window.Title}");
		WindowTitleChanged?.Invoke(window);
	}


}