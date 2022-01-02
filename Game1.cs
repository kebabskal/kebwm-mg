using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Windows.UI.ViewManagement;

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
	Texture2D square;

	WindowManager windowManager;
	WeatherManager weatherManager;
	AudioManager audioManager;
	List<WindowButton> windowButtons;
	List<Region> regions;

	MouseState mouseState;
	MouseState lastMouseState;

	// Reordering
	WindowButton draggedButton = null;
	Point dragOffset = new Point(0, 0);
	Point dragStart = new Point(0, 0);

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
			Rectangle = new Rectangle((windowButtons.Count * buttonWidth) + 5, 0, buttonWidth, barHeight)
		};

		var moduleName = window.Process.MainModule.ModuleName.ToLower().Split('.')[0];
		if (replacementIcons.ContainsKey(moduleName)) {
			button.Window.Icon = replacementIcons[moduleName];
		} else if (window.IsVisible) {
			window.GetIcon();
			// getIconQueue.Append(window);
		}

		windowButtons.Add(button);
	}

	void OnWindowDestroyed(Window window) {
		windowButtons.RemoveAll(wb => wb.Window == window);
	}

	protected override void Initialize() {
		base.Initialize();

		Window.Position = new Point(64, 0);
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

		// Get windows accent color
		var uiSettings = new UISettings();
		var ac = uiSettings.GetColorValue(UIColorType.Accent);
		accentColor = new Color(ac.R, ac.G, ac.B, ac.A);
	}

	protected override void Update(GameTime gameTime) {
		if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
			Exit();

		foreach (var button in windowButtons.ToArray()) {
			button.Rectangle.Width = (int)MathHelper.Lerp(
				button.Rectangle.Width,
				regions.Any(r => r.LastActiveWindow == button.Window) ? 48 : 32,
				(float)gameTime.ElapsedGameTime.TotalSeconds * 15f
			);
		}

		base.Update(gameTime);
	}

	Color bgColor = new Color(0.025f, 0.025f, 0.025f);
	Color bgColorHover = new Color(0.0f, 0.0f, 0.0f);
	Color accentColor = new Color(0.0f, 0.6f, 0.7f);

	bool Button(string text, Rectangle rect, Texture2D icon, bool hilight, bool underline) {
		var hovered = rect.Contains(mouseState.Position);
		// Draw background
		spriteBatch.Draw(square, rect, (hovered && draggedButton == null) || hilight ? bgColorHover : bgColor);
		if (underline)
			spriteBatch.Draw(square, new Rectangle(rect.X, rect.Y + rect.Height - 3, rect.Width, 3), accentColor);

		if (icon != null) {
			spriteBatch.Draw(icon, new Rectangle(rect.Center.X - 8, rect.Y + 4, 16, 16), hilight || hovered ? Color.White : Color.Gray);
		} else if (text.Length > 0) {
			try {
				Vector2 size = font.MeasureString(text);
				if (size.X > rect.Width - 10) {
					text = text.Substring(0, 2);
					size.X = rect.Width / 2.0f;
				}

				var textOffset = new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 4);
				spriteBatch.DrawString(font, text, textOffset, Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 1);
			} catch {
				Vector2 size = font.MeasureString("???");
				var textOffset = new Vector2(rect.Center.X - size.X / 4, rect.Center.Y - size.Y / 4);
				spriteBatch.DrawString(font, "???", textOffset, Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 1);
			}
		}

		return hovered && mouseState.LeftButton == ButtonState.Pressed && lastMouseState.LeftButton == ButtonState.Released;
	}

	public float Slider(float value, Rectangle rect) {
		var hovered = rect.Contains(mouseState.Position);
		spriteBatch.Draw(square, rect, bgColor);
		var barRect = rect;
		barRect.Width = (int)(rect.Width * value);
		spriteBatch.Draw(square, barRect, hovered ? accentColor : new Color(0.1f, 0.1f, 0.1f, 1.0f));

		if (hovered && mouseState.LeftButton == ButtonState.Pressed) {
			return (float)(mouseState.Position.X - rect.Left) / rect.Width;
		}

		return value;
	}


	protected override void Draw(GameTime gameTime) {
		GraphicsDevice.Clear(Color.Black);

		spriteBatch.Begin();

		mouseState = Mouse.GetState();

		foreach (var region in regions) {
			var x = region.Rectangle.Left + 10 - barOffset;
			var buttons = windowButtons.Where(wb =>
				region.Rectangle.Contains(wb.Window.Rectangle.Center) &&
				wb.Window.IsVisible
			).ToArray();
			foreach (var button in buttons) {
				button.Rectangle.X = x;
				var hilight = button.Window == region.LastActiveWindow;
				var rect = button.Rectangle;
				if (
					Button(
						button.Window.Title,
						rect,
						button.Window.Icon,
						hilight,
						hilight
					)
				) {
					button.Window.SetActive();
					region.LastActiveWindow = button.Window;
				}

				x += rect.Width;
			}

			// Draw date
			var dateString = System.DateTime.Now.ToString("ddd - yyyy-MM-dd - HH:mm:ss").ToUpper();
			var week = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(System.DateTime.Now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
			dateString = $"W{week} " + dateString;

			var dateWidth = font.MeasureString(dateString).X / 2 + 10;
			spriteBatch.DrawString(
				font,
				dateString,
				new Vector2(region.Rectangle.Right - 48 - barOffset - dateWidth, 4),
				Color.Gray,
				0, Vector2.Zero, 0.5f, SpriteEffects.None, 1
			);

			// Draw temperature
			var tempWidth = font.MeasureString(weatherManager.CurrentTemperature).X / 2 + 10;
			spriteBatch.DrawString(
				font,
				weatherManager.CurrentTemperature,
				new Vector2(region.Rectangle.Right - 48 - barOffset - dateWidth - tempWidth - 20, 4),
				Color.Gray,
				0, Vector2.Zero, 0.5f, SpriteEffects.None, 1
			);

			// Draw volume
			audioManager.Volume = Slider(
				(float)audioManager.Volume / 100f,
				new Rectangle(
					(int)(region.Rectangle.Right - 48 - barOffset - dateWidth - tempWidth - 20 - 64 - 10),
					4,
					64,
					16
				)
			) * 100f;

			if (Button("G", new Rectangle(region.Rectangle.Right - 48 - barOffset, -2, 48, barHeight), null, false, false)) {
				foreach (var button in buttons) {
					button.Window.SetSize(region.Rectangle);
				}
			}
		}

		spriteBatch.End();

		base.Draw(gameTime);

		lastMouseState = mouseState;
	}
}