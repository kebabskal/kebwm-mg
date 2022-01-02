using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using Vanara.PInvoke;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

class Window {
	public static string[] BorderList = new String[] {
		"chrome",
		"unity",
	};

	public WindowManager WindowManager;
	public HWND Hwnd;
	public string Title = "";
	public Process Process;
	public Rectangle Rectangle;
	public bool BorderHack = false;
	public bool CompactHack = false;

	public Texture2D Icon;

	public bool IsMinimized => User32.IsIconic(Hwnd);
	public bool IsValid => User32.IsWindow(Hwnd);
	public bool IsVisible => User32.IsWindowVisible(Hwnd);

	public bool IsManageable =>
		IsValid &&
		IsVisible &&
		Rectangle.Width > 1 &&
		Rectangle.Height > 1 &&
		!IsMinimized
	;

	public Window(WindowManager windowManager, HWND hwnd, Process process) {
		WindowManager = windowManager;
		Hwnd = hwnd;
		Process = process;
		try {
			var filename = Process.MainModule.FileName.ToLower();
			BorderHack = BorderList.Any(bl => filename.Contains(bl));
		} catch {

		}
	}

	public void GetIcon() {
		if (Process == null)
			return;

		var icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.MainModule.FileName);
		if (icon != null) {
			var iconBitmap = icon.ToBitmap();
			Console.WriteLine($"GetIcon {this}");
			Icon = GetTexture(WindowManager.Graphics.GraphicsDevice, iconBitmap);
		}
	}

	public override string ToString() {
		return $"{Title} ({((IntPtr)Hwnd).ToString()}) ({Rectangle})";
	}

	void SetActiveThread() {
		User32.SetActiveWindow(Hwnd);
		User32.SetForegroundWindow(Hwnd);
	}

	public void SetActive() {
		Console.WriteLine($"Activating: {Title}");
		var thread = new Thread(SetActiveThread);
		thread.Start();
	}

	// FROM: https://stackoverflow.com/questions/2869801/is-there-a-fast-alternative-to-creating-a-texture2d-from-a-bitmap-object-in-xna
	Texture2D GetTexture(GraphicsDevice dev, System.Drawing.Bitmap bmp) {
		int[] imgData = new int[bmp.Width * bmp.Height];
		Texture2D texture = new Texture2D(dev, bmp.Width, bmp.Height);

		unsafe {
			// Lock bitmap
			System.Drawing.Imaging.BitmapData origdata =
					bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

			uint* byteData = (uint*)origdata.Scan0;

			// Switch bgra -> rgba
			for (int i = 0; i < imgData.Length; i++) {
				byteData[i] = (byteData[i] & 0x000000ff) << 16 | (byteData[i] & 0x0000FF00) | (byteData[i] & 0x00FF0000) >> 16 | (byteData[i] & 0xFF000000);
			}

			// Copy data
			System.Runtime.InteropServices.Marshal.Copy(origdata.Scan0, imgData, 0, bmp.Width * bmp.Height);

			byteData = null;

			// Unlock bitmap
			bmp.UnlockBits(origdata);
		}

		texture.SetData(imgData);

		return texture;
	}

	public void SetSize(Rectangle rectangle) {
		var b = 8;
		var left = rectangle.Left;
		var top = rectangle.Top;
		var width = rectangle.Width;
		var height = rectangle.Height;

		if (BorderHack) {
			left -= b;
			width += b * 2;
			height += b;
		}

		if (CompactHack) {
			top -= 53;
			height += 53;
		}

		User32.MoveWindow(Hwnd, left, top, width, height, true);
	}

	internal void ToggleCompact() {
		CompactHack = !CompactHack;
	}
}


