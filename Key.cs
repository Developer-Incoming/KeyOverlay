using System;
using System.Collections.Generic;
using SFML.Graphics;
using SFML.Window;

namespace KeyOverlay
{
    public class Key
    {
        public int Hold { get; set; }
        public List<RectangleShape> BarList = new();
        public string KeyLetter = "";
        public readonly Keyboard.Key KeyboardKey;
        public readonly Mouse.Button MouseButton;
        public int Counter = 0;
        public int GlobalCounter = 0;
        public string KeyName;
        public readonly bool isKey = true;
        public Color _color;
        public Color _colorPressed;
        public uint _size = 1;

        // Press timing for accurate sub-frame bar rendering
        public float CurrentPressStart = -1f;
        public bool ActiveBarExists = false;
        public List<(float startTime, float duration)> CompletedPresses = new();

        public Key(string key)
        {
            setColor(Color.White);
            KeyLetter = key;
            KeyName = key;
            if (!Enum.TryParse(key, out KeyboardKey))
            {
                if (KeyLetter[0] == 'm')
                {
                    KeyLetter = KeyLetter.Remove(0, 1);
                }
                if (Enum.TryParse(key.Substring(1), out MouseButton))
                {
                    isKey = false;
                }
                else
                {
                    string exceptName = "Invalid key " + key;
                    throw new InvalidOperationException(exceptName);
                }

            }
        }

        public void setKeyLetter(string key)
        {
            KeyLetter = key;
        }

        public void setColor(Color c)
        {
            _color = c;
            _colorPressed = new Color(c.R, c.G, c.B, (byte)(c.A / 1.618));
        }

        public void setSize(uint size)
        {
            _size = size;
        }
    }
}