using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

class WindowButton {
	public Rectangle Rectangle;
	public Window Window;
}

class Region {
	public Rectangle Rectangle;
}

public class Game1 : Game {
	GraphicsDeviceManager graphics;
	SpriteBatch spriteBatch;
	SpriteFont font;
	Texture2D square;

	WindowManager windowManager;
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
		// Window.IsBorderless = true;
	}

	void OnWindowCreated(Window window) {
		var button = new WindowButton() {
			Window = window,
			Rectangle = new Rectangle((windowButtons.Count * buttonWidth) + 5, 0, buttonWidth, barHeight)
		};

		if (window.IsVisible) {
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
	}

	protected override void LoadContent() {
		spriteBatch = new SpriteBatch(GraphicsDevice);
		font = Content.Load<SpriteFont>("Font");
		FileStream fileStream = new FileStream("Content/square.png", FileMode.Open);
		square = Texture2D.FromStream(graphics.GraphicsDevice, fileStream);
		fileStream.Dispose();
	}

	protected override void Update(GameTime gameTime) {
		if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
			Exit();

		base.Update(gameTime);
	}

	Color bgColor = new Color(0.025f, 0.025f, 0.025f);
	Color bgColorHover = new Color(0.2f, 0.2f, 0.2f);

	bool Button(string text, Rectangle rect, Texture2D icon, bool hilight) {
		var hovered = rect.Contains(mouseState.Position);
		// Draw background
		spriteBatch.Draw(square, rect, (hovered && draggedButton == null) || hilight ? bgColorHover : bgColor);

		if (icon != null) {

			spriteBatch.Draw(icon, new Rectangle(rect.Center.X - 8, rect.Y + 4, 16, 16), Color.White);
		} else if (text.Length > 0) {
			// spriteBatch.DrawString(font, "???", new Vector2(rect.X + 10, 4), Color.White, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 1);
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
				if (Button(button.Window.Title, button.Rectangle, button.Window.Icon, false)) {
					button.Window.SetActive();
				}

				x += button.Rectangle.Width + 3;
			}

			// Draw date
			var dateString = System.DateTime.Now.ToString("ddd - yyyy-MM-dd - hh:mm:ss");
			var week = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(System.DateTime.Now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
			dateString = $"W{week} " + dateString;

			var dateWidth = font.MeasureString(dateString).X / 2 + 10;
			spriteBatch.DrawString(
				font,
				dateString,
				new Vector2(region.Rectangle.Right - 48 - barOffset - dateWidth, 6),
				Color.White,
				0, Vector2.Zero, 0.5f, SpriteEffects.None, 1
			);

			if (Button("G", new Rectangle(region.Rectangle.Right - 48 - barOffset, 0, 48, barHeight), null, false)) {
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