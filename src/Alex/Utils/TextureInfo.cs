﻿using Microsoft.Xna.Framework;

namespace Alex.Utils
{
	public class TextureInfo
	{
		public int Width { get; } = 16;
		public int Height { get; } = 16;

		public Vector2 Position { get; } = Vector2.Zero;
		public bool Animated { get; }
		public bool ResolvedSuccessFully { get; }
		
		public TextureInfo(Vector2 position, int width, int height, bool animated, bool success)
		{
			Position = position;
			Width = width;
			Height = height;
			Animated = animated;
			ResolvedSuccessFully = success;
		}
	}
}