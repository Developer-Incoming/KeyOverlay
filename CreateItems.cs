using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SFML.Graphics;
using SFML.System;

namespace KeyOverlay
{
    public static class CreateItems
    {
        public static readonly Font _font = new Font(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Resources", "consolab.ttf")));
        public static RectangleShape CreateBar(RectangleShape square, int outlineThickness, float barSpeed)
        {
            var rect = new RectangleShape(new Vector2f(square.Size.X + outlineThickness * 2, barSpeed));
            rect.Position = new Vector2f(square.Position.X - outlineThickness,
                square.Position.Y - square.Size.Y - square.OutlineThickness);
            rect.FillColor = square.FillColor;
            return rect;
        }

        public static List<RectangleShape> CreateKeys(List<Key> keys, float size, float ratioX, float ratioY,
            int margin, int outlineThickness)
        {
            var keyAmount = keys.Count;
            var xPos = margin + outlineThickness;
            var yPos = 900 * ratioY;
            var keyList = new List<RectangleShape>();

            foreach (var key in keys)
            {
                var square = new RectangleShape(new Vector2f(size * key._size, size));
                var width = (int)(size * key._size);

                square.FillColor = CreateColor("0,0,0,0");
                square.OutlineColor = key._color;
                square.OutlineThickness = outlineThickness;
                square.Origin = new Vector2f(0, size);
                square.Position = new Vector2f(xPos, yPos);
                xPos += width + outlineThickness * 2 + margin;
                keyList.Add(square);
            }
            return keyList;
        }

        public static Text CreateText(string key, RectangleShape square, Color color, bool counter)
        {
            var text = new Text(key, _font);
            text.CharacterSize = (uint)(50 * square.Size.Y / 140);
            text.Style = Text.Styles.Bold;
            text.FillColor = color;
            text.Origin = new Vector2f(text.GetLocalBounds().Width / 2f, 32 * square.Size.Y / 140f);
            if (counter)
                text.Position = new Vector2f(square.GetGlobalBounds().Left + square.OutlineThickness + square.Size.X / 2f,
                    square.GetGlobalBounds().Top + square.OutlineThickness + square.Size.Y + text.CharacterSize);
            else
                text.Position = new Vector2f(square.GetGlobalBounds().Left + square.OutlineThickness + square.Size.X / 2f,
                    square.GetGlobalBounds().Top + square.OutlineThickness + square.Size.Y / 2f);

            return text;
        }

        public static Text CreateStatsText(string content, float xPosition, float yPosition, Color color, uint charSize = 18, bool centerX = false)
        {
            var text = new Text(content, _font);
            text.CharacterSize = charSize;
            text.Style = Text.Styles.Bold;
            text.FillColor = color;
            if (centerX)
                text.Origin = new Vector2f(text.GetLocalBounds().Width / 2f, 0);
            text.Position = new Vector2f(xPosition, yPosition);
            return text;
        }

        public static Color CreateColor(string s)
        {
            var bytes = s.Split(',').Select(int.Parse).Select(Convert.ToByte).ToArray();
            return new Color(bytes[0], bytes[1], bytes[2], bytes[3]);
        }
    }
}