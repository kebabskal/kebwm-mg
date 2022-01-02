using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Windows.UI.ViewManagement;

public enum UILayoutMode {
	None,
	Horizontal,
	HorizontalReverse,
}

public class UI {
	// Drawing
	GraphicsDevice device;
	SpriteBatch spriteBatch;

	// Content
	Texture2D square;
	SpriteFont font;
	SpriteFont fontBold;

	// Layout
	Rectangle layoutRect;
	UILayoutMode layoutMode = UILayoutMode.Horizontal;
	Vector2 cursor = Vector2.Zero;
	float spacing = 2f;
	int iconSize = 16;

	// Input
	MouseState mouseState;
	MouseState lastMouseState;

	// Style
	Color bgColor = new Color(0.025f, 0.025f, 0.025f);
	Color bgColorHover = new Color(0.2f, 0.1f, 0.3f);
	Color accentColor = new Color(0.0f, 0.6f, 0.7f);


	public UI(SpriteBatch batch, GraphicsDevice device) {
		this.spriteBatch = batch;
		this.device = device;
		mouseState = Mouse.GetState();
	}

	public void LoadContent(ContentManager content) {
		font = content.Load<SpriteFont>("Font");
		fontBold = content.Load<SpriteFont>("FontBold");
		FileStream fileStream = new FileStream("Content/square.png", FileMode.Open);
		square = Texture2D.FromStream(device, fileStream);
		fileStream.Dispose();

		// Get windows accent color
		var uiSettings = new UISettings();
		var ac = uiSettings.GetColorValue(UIColorType.Accent);
		accentColor = new Color(ac.R, ac.G, ac.B, ac.A);
		bgColorHover = new Color(ac.R / 3, ac.G / 3, ac.B / 3, ac.A);

	}

	public void Begin(UILayoutMode mode, Rectangle rect) {
		layoutRect = rect;
		layoutMode = mode;
		switch (mode) {
			case (UILayoutMode.None):
			case (UILayoutMode.Horizontal):
				cursor = new Vector2(rect.Left, rect.Top);
				break;
			case (UILayoutMode.HorizontalReverse):
				cursor = new Vector2(rect.Right, rect.Top);
				break;
		}
	}

	void MoveCursor(Rectangle rect) {
		switch (layoutMode) {
			case (UILayoutMode.None):
				break;
			case (UILayoutMode.Horizontal):
				cursor.X += rect.Width + spacing;
				break;
			case (UILayoutMode.HorizontalReverse):
				cursor.X -= rect.Width + spacing;
				break;
		}
	}

	Rectangle OffsetRect(Rectangle inRect) {
		var rect = inRect;
		switch (layoutMode) {
			case (UILayoutMode.None):
				break;
			case (UILayoutMode.Horizontal):
				rect.X += (int)cursor.X;
				break;
			case (UILayoutMode.HorizontalReverse):
				rect.X = (int)cursor.X - inRect.Width + inRect.X;
				break;
		}

		return rect;
	}

	public void Label(string text, SpriteFont font, Rectangle inRect) {
		var rect = OffsetRect(inRect);
		spriteBatch.DrawString(font, text, new Vector2(rect.X, rect.Y), Color.Gray, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 1);
		MoveCursor(inRect);
	}

	public bool Button(Texture2D icon, Rectangle inRect, bool hilight = false, bool underline = false) {
		var rect = OffsetRect(inRect);
		var hovered = rect.Contains(mouseState.Position);

		// Draw background
		spriteBatch.Draw(square, rect, hovered || hilight ? bgColorHover : bgColor);
		if (underline)
			spriteBatch.Draw(square, new Rectangle(rect.X, rect.Y + rect.Height - 3, rect.Width, 3), accentColor);

		// Draw Icon
		spriteBatch.Draw(
			icon,
			new Rectangle(
				rect.Center.X - iconSize / 2,
				rect.Center.Y - iconSize / 2,
				iconSize,
				iconSize
			),
			hilight || hovered ? Color.White : Color.Gray
		);

		MoveCursor(inRect);
		return hovered && mouseState.LeftButton == ButtonState.Pressed && lastMouseState.LeftButton == ButtonState.Released;
	}

	public bool Button(string text, Rectangle inRect, bool hilight = false, bool underline = false) {
		var rect = OffsetRect(inRect);
		var hovered = rect.Contains(mouseState.Position);

		// Draw background
		spriteBatch.Draw(square, rect, hovered || hilight ? bgColorHover : bgColor);
		if (underline)
			spriteBatch.Draw(square, new Rectangle(rect.X, rect.Y + rect.Height - 3, rect.Width, 3), accentColor);

		// Draw Text
		try {
			Vector2 size = font.MeasureString(text);
			if (size.X > rect.Width - 10) {
				text = text.Substring(0, 2);
				size.X = rect.Width / 2.0f;
			}

			var textOffset = new Vector2(rect.Center.X - size.X / 2, rect.Center.Y - size.Y / 4 - 2);
			spriteBatch.DrawString(font, text, textOffset, hovered ? accentColor : Color.Gray, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 1);
		} catch {
			Vector2 size = font.MeasureString("???");
			var textOffset = new Vector2(rect.Center.X - size.X / 4, rect.Center.Y - size.Y / 4 - 2);
			spriteBatch.DrawString(font, "???", textOffset, hovered ? accentColor : Color.Gray, 0, Vector2.Zero, 0.5f, SpriteEffects.None, 1);
		}

		MoveCursor(inRect);
		return hovered && mouseState.LeftButton == ButtonState.Pressed && lastMouseState.LeftButton == ButtonState.Released;
	}

	public float Slider(float value, Rectangle inRect) {
		var rect = OffsetRect(inRect);

		var hovered = rect.Contains(mouseState.Position);
		spriteBatch.Draw(square, rect, bgColor);
		var barRect = rect;
		barRect.Width = (int)(rect.Width * value);
		spriteBatch.Draw(square, barRect, hovered ? accentColor : new Color(0.1f, 0.1f, 0.1f, 1.0f));

		if (hovered && mouseState.LeftButton == ButtonState.Pressed)
			return (float)(mouseState.Position.X - rect.Left) / rect.Width;

		MoveCursor(inRect);
		return value;
	}

	public void BeginUpdate() {
		mouseState = Mouse.GetState();
	}

	public void EndUpdate() {
		lastMouseState = mouseState;
	}



}