using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vanara.PInvoke;

class WindowButton {
	public Rectangle Rectangle;
	public Window Window;
}

class Region {
	public Rectangle Rectangle;
	public Window LastActiveWindow;
}

public class Game1 : Game {
	public static string[] Blacklist = new string[] {
		"Microsoft Text Input Application",
		"Program Manager",
		"",
	};

	GraphicsDeviceManager graphics;
	SpriteBatch spriteBatch;
	SpriteFont font;
	SpriteFont fontBold;
	Texture2D square;

	WindowManager windowManager;
	WeatherManager weatherManager;
	AudioManager audioManager;
	UI ui;

	List<WindowButton> windowButtons;
	List<Region> regions;

	MouseState mouseState;
	MouseState lastMouseState;
	KeyboardState keyboardState;
	KeyboardState lastKeyboardState;
	bool altPressed = false;
	bool ctrlPressed = false;

	int buttonWidth = 32;
	int barHeight = 28;
	int barOffset = 64;

	Queue<Window> getIconQueue = new Queue<Window>();
	Dictionary<string, Texture2D> replacementIcons = new Dictionary<string, Texture2D>();

	public void UpdateWindowManager() {
		while (true) {
			windowManager.Update();
			Thread.Sleep(250);
		}
	}

	public void GetIconThread() {
		while (true) {
			if (getIconQueue.Count > 0) {
				var window = getIconQueue.Dequeue();
				window.GetIcon();
			}

			Thread.Sleep(100);
		}
	}

	public Game1() {
		graphics = new GraphicsDeviceManager(this);
		Content.RootDirectory = "Content";
		IsMouseVisible = true;
	}

	void OnWindowCreated(Window window) {
		if (Blacklist.Contains(window.Title))
			return;

		var button = new WindowButton() {
			Window = window,
			Rectangle = new Rectangle(0, 0, buttonWidth, barHeight)
		};

		var moduleName = window.Process.MainModule.ModuleName.ToLower().Split('.')[0];
		if (replacementIcons.ContainsKey(moduleName)) {
			button.Window.Icon = replacementIcons[moduleName];
		} else if (window.IsVisible) {
			window.GetIcon();
		}

		windowButtons.Add(button);
	}

	void OnWindowDestroyed(Window window) {
		windowButtons.RemoveAll(wb => wb.Window == window);
	}

	protected override void Initialize() {
		base.Initialize();

		graphics.PreferredBackBufferWidth = GraphicsDevice.DisplayMode.Width;
		graphics.PreferredBackBufferHeight = barHeight;
		graphics.ApplyChanges();

		windowButtons = new List<WindowButton>();
		windowManager = new WindowManager(graphics);
		windowManager.WindowCreated += OnWindowCreated;
		windowManager.WindowDestroyed += OnWindowDestroyed;
		windowManager.ForegroundWindowChanged += OnForegroundWindowChanged;

		var w = 5120;
		var p = w / 4;
		regions = new List<Region>() {
			new Region() { Rectangle = new Rectangle(barOffset,barHeight, p - barOffset, 1440 - barHeight)},
			new Region() { Rectangle = new Rectangle(p,barHeight, p*2, 1440 - barHeight)},
			new Region() { Rectangle = new Rectangle(p*3,barHeight, p, 1440 - barHeight)},
		};

		var thread = new Thread(UpdateWindowManager);
		thread.Start();

		var thread2 = new Thread(GetIconThread);
		thread2.Start();

		GlobalHotKey.RegisterHotKey("Control + Shift + G", () => {
			System.Console.WriteLine("Group!");

			foreach (var region in regions.ToArray())
				foreach (var windowButton in windowButtons.ToArray())
					if (region.Rectangle.Contains(windowButton.Window.Rectangle.Center))
						windowButton.Window.SetSize(region.Rectangle);

		});

		weatherManager = new WeatherManager();
		audioManager = new AudioManager();
	}

	private void OnForegroundWindowChanged(Window window) {
		if (window == null)
			return;

		foreach (var region in regions.ToArray())
			if (region.Rectangle.Contains(window.Rectangle.Center))
				region.LastActiveWindow = window;
	}

	protected override void LoadContent() {
		spriteBatch = new SpriteBatch(GraphicsDevice);
		font = Content.Load<SpriteFont>("Font");
		fontBold = Content.Load<SpriteFont>("FontBold");
		FileStream fileStream = new FileStream("Content/square.png", FileMode.Open);
		square = Texture2D.FromStream(graphics.GraphicsDevice, fileStream);
		fileStream.Dispose();

		// Load replacement icons
		var dirInfo = new System.IO.DirectoryInfo("Content/icons");
		foreach (var fileInfo in dirInfo.GetFiles("*.png")) {
			fileStream = fileInfo.OpenRead();
			var icon = Texture2D.FromStream(graphics.GraphicsDevice, fileStream);
			var name = fileInfo.Name.Split('.')[0];
			replacementIcons.Add(name, icon);
			fileStream.Dispose();

			System.Console.WriteLine($"Loading Replacement Icon: {name}");
		}

		ui = new UI(spriteBatch, graphics.GraphicsDevice);
		ui.LoadContent(Content);
	}

	protected override void Update(GameTime gameTime) {
		keyboardState = Keyboard.GetState();
		mouseState = Mouse.GetState();

		ctrlPressed = keyboardState.IsKeyDown(Keys.LeftControl) ||
			keyboardState.IsKeyDown(Keys.RightControl);
		altPressed = keyboardState.IsKeyDown(Keys.LeftAlt) ||
			keyboardState.IsKeyDown(Keys.RightAlt);

		// Update button widths
		foreach (var button in windowButtons.ToArray()) {
			button.Rectangle.Width = (int)MathHelper.Lerp(
				button.Rectangle.Width,
				regions.Any(r => r.LastActiveWindow == button.Window) ? buttonWidth * 1.5f : buttonWidth,
				(float)gameTime.ElapsedGameTime.TotalSeconds * 15f
			);
		}

		base.Update(gameTime);
	}

	bool hasBeenSetup = false;
	protected override void Draw(GameTime gameTime) {
		if (!hasBeenSetup) {
			// Setup the window style, position and make it always on top
			var process = System.Diagnostics.Process.GetCurrentProcess();
			var extStyle = User32.GetWindowLong(process.MainWindowHandle, User32.WindowLongFlags.GWL_EXSTYLE);
			extStyle |= (int)User32.WindowStylesEx.WS_EX_TOPMOST;
			User32.SetWindowLong(process.MainWindowHandle, User32.WindowLongFlags.GWL_EXSTYLE, extStyle);
			User32.SetWindowPos(process.MainWindowHandle, HWND.HWND_TOPMOST, barOffset, -32, 0, 0, 0);

			hasBeenSetup = true;
		}

		GraphicsDevice.Clear(Color.Black);

		spriteBatch.Begin();
		ui.BeginUpdate();


		foreach (var region in regions) {
			var regionRect = new Rectangle(
				region.Rectangle.Left - barOffset + 5,
				0,
				region.Rectangle.Width - 10,
				barHeight
			);

			ui.Begin(UILayoutMode.Horizontal, regionRect);

			var buttons = windowButtons.Where(wb =>
				region.Rectangle.Contains(wb.Window.Rectangle.Center) &&
				wb.Window.IsVisible
			).ToArray();
			foreach (var button in buttons) {
				var hilight = button.Window == region.LastActiveWindow;
				if (
					ui.Button(
						button.Window.Icon,
						new Rectangle(0, 0, button.Rectangle.Width, button.Rectangle.Height),
						hilight,
						hilight
					)
				) {
					button.Window.SetActive();
					if (ctrlPressed) {
						button.Window.ToggleCompact();
						button.Window.SetSize(region.Rectangle);
					}

					region.LastActiveWindow = button.Window;
				}
			}

			ui.Begin(UILayoutMode.HorizontalReverse, regionRect);


			if (ui.Button("G", new Rectangle(0, 0, 48, barHeight))) {
				foreach (var button in buttons) {
					button.Window.SetSize(region.Rectangle);
				}
			}

			// Draw date
			var dateString = System.DateTime.Now.ToString("ddd - yyyy-MM-dd - HH:mm:ss").ToUpper();
			var week = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(System.DateTime.Now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
			dateString = $"W{week} " + dateString;
			ui.Label(dateString, font, new Rectangle(0, 4, 210, barHeight));

			// Draw temperature
			ui.Label(weatherManager.CurrentTemperature, font, new Rectangle(0, 4, 64, barHeight));

			// Draw volume
			ui.Label($"{System.Math.Round(audioManager.Volume)}%", font, new Rectangle(0, 4, 64, barHeight));
			audioManager.Volume = ui.Slider(
				(float)audioManager.Volume / 100f,
				new Rectangle(0, 4, 64, 16)
			) * 100f;
		}

		spriteBatch.End();
		ui.EndUpdate();

		base.Draw(gameTime);

		lastMouseState = mouseState;
		lastKeyboardState = keyboardState;
	}
}