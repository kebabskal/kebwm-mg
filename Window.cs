using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using Vanara.PInvoke;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

class Window {
	public WindowManager WindowManager;
	public HWND Hwnd;
	public string Title = "";
	public string Executable = "";
	public Process Process;
	public Rectangle Rectangle;
	public bool BorderHack = false;

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
			BorderHack = Process.MainModule.FileName.ToLower().Contains("chrome");
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
		return $"{Title} ({Hwnd}) ({Rectangle}) ({Executable})";
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

	Texture2D GetTexture(GraphicsDevice dev, System.Drawing.Bitmap bmp) {
		int[] imgData = new int[bmp.Width * bmp.Height];
		Texture2D texture = new Texture2D(dev, bmp.Width, bmp.Height);

		unsafe {
			// lock bitmap
			System.Drawing.Imaging.BitmapData origdata =
					bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

			uint* byteData = (uint*)origdata.Scan0;

			// Switch bgra -> rgba
			for (int i = 0; i < imgData.Length; i++) {
				byteData[i] = (byteData[i] & 0x000000ff) << 16 | (byteData[i] & 0x0000FF00) | (byteData[i] & 0x00FF0000) >> 16 | (byteData[i] & 0xFF000000);
			}

			// copy data
			System.Runtime.InteropServices.Marshal.Copy(origdata.Scan0, imgData, 0, bmp.Width * bmp.Height);

			byteData = null;

			// unlock bitmap
			bmp.UnlockBits(origdata);
		}

		texture.SetData(imgData);

		return texture;
	}

	public void SetSize(Rectangle rectangle) {
		var b = 8;
		if (BorderHack)
			User32.MoveWindow(Hwnd, rectangle.Left - b, rectangle.Top, rectangle.Width + b * 2, rectangle.Height + b, true);
		else
			User32.MoveWindow(Hwnd, rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height, true);
	}
}


