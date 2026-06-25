using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using static SFML.Window.Keyboard;

namespace KeyOverlay
{
    public class AppWindow
    {
        private RenderWindow _window;
        private Vector2u _size;

        private List<Key> _keyList;
        private List<int> _keyPressFadeList;
        private int _keyFadeTime;
        private float _keyFadeExp;

        private List<RectangleShape> _squareList;

        private float _barSpeed;
        private float _ratioX;
        private float _ratioY;
        private int _outlineThickness;
        private Color _backgroundColor;
        private Color _fontColor;
        private bool _fading;
        private bool _counter;
        private bool _globalCounter;
        private bool _showStatsText;
        private List<Drawable> _staticDrawables;
        private uint _maxFPS;
        private Clock _clock = new();
        private Config _config;
        private object _lock = new object();
        private FadingTexture _fadingTexture;

        // Global counts persistence
        private GlobalCountStore _globalCounts;
        private string _datFilePath = "global_counts.dat";
        private Clock _lastKeyPressClock = new();
        private bool _pendingSave = false;

        // Session & KPS tracking
        private uint _sessionTotal = 0;
        private List<float> _kpsTimestamps = new();
        private Clock _kpsClock = new();

        // Render timing (input polls every loop iteration, render at FPS interval)
        private Clock _renderClock = new();
        private float _renderInterval;

        public AppWindow()
        {
            _window = new RenderWindow(new VideoMode(600, 800), "Key Overlay");
            _globalCounts = new GlobalCountStore(_datFilePath);
            _config = new Config("config.ini", Initialize);
            Initialize();
        }

        public void Initialize()
        {
            lock (_lock)
            {
                var general = _config["General"];

                _barSpeed = float.Parse(general["barSpeed"], CultureInfo.InvariantCulture);
                _backgroundColor = CreateItems.CreateColor(general["backgroundColor"]);
                _maxFPS = uint.Parse(general["fps"]);

				/*
                _keyFadeTime = int.Parse(general["keyFadeTime"]);
                _keyFadeExp = float.Parse(general["keyFadeExp"]);
                if (_keyFadeTime < 0)
                    _keyFadeTime = 1;
                if (_keyFadeExp < 1f)
                    _keyFadeExp = 1f;
                */

				string keyFadeTimeStr;
                if (!general.TryGetValue("keyFadeTime", out keyFadeTimeStr))
                    _keyFadeTime = 7;
                else
                    _keyFadeTime = int.Parse(general["keyFadeTime"]);
                _keyFadeExp = 1.0f;

				//create keys which will be used to create the squares and text
				_keyList = new List<Key>();
                _keyPressFadeList = new List<int>();
                foreach (var item in _config["Keys"])
                {
                    var key = new Key(item.Value);
                    key.KeyName = item.Key;

                    if (_config["Display"].ContainsKey(item.Key))
                        key.setKeyLetter(_config["Display"][item.Key]);

                    if (_config["Colors"].ContainsKey(item.Key))
                        key.setColor(CreateItems.CreateColor(_config["Colors"][item.Key]));

                    if (_config["Size"].ContainsKey(item.Key))
                        key.setSize(uint.Parse(_config["Size"][item.Key]));

                    // Load global counter for this key
                    key.GlobalCounter = _globalCounts.GetCount(key.KeyName);

                    _keyList.Add(key);
                    _keyPressFadeList.Add(0);
                }
                
                _outlineThickness = int.Parse(general["outlineThickness"]);
                var keySize = int.Parse(general["keySize"]);
                var margin = int.Parse(general["margin"]);

                var windowWidth = margin;
                foreach (var key in _keyList)
                {
                    windowWidth += keySize * (int) key._size + _outlineThickness * 2 + margin;
                }

                var windowHeight = int.Parse(general["height"]);

                // Add bottom margin for global counters + stats bar
                int bottomMargin = 70;
                _size = new Vector2u((uint) windowWidth, (uint) (windowHeight + bottomMargin));

                //calculate screen ratio relative to original program size for easy resizing
                _ratioX = windowWidth / 480f;
                _ratioY = windowHeight / 960f;

                _staticDrawables = new List<Drawable>();
                _squareList = CreateItems.CreateKeys(_keyList, keySize, _ratioX, _ratioY, margin, _outlineThickness);
                foreach (var square in _squareList) _staticDrawables.Add(square);

                //create text and add it to _staticDrawables list
                _fontColor = new Color(255, 255, 255, 255);
                for (var i = 0; i < _keyList.Count; i++)
                {
                    var text = CreateItems.CreateText(_keyList[i].KeyLetter, _squareList[i], _fontColor, false);
                    _staticDrawables.Add(text);
                }
                
                _fading = general.ContainsKey("fading") && general["fading"] == "yes";
                _counter = general.ContainsKey("counter") && general["counter"] == "yes";
                _globalCounter = general.ContainsKey("globalCounter") && general["globalCounter"] == "yes";
                _showStatsText = general.ContainsKey("showStatsText") && general["showStatsText"] == "yes";

                _fadingTexture = new FadingTexture(_backgroundColor, _size.X, _ratioY);
            }
        }

        private void OnClose(object sender, EventArgs e)
        {
            // Save global counts on exit
            SaveGlobalCounts();
            _window.Close();
        }

        /// <summary>
        /// Persist current global counts to the .dat file.
        /// </summary>
        private void SaveGlobalCounts()
        {
            if (_globalCounts == null) return;
            foreach (var key in _keyList)
            {
                // Overwrite the store with current in-memory totals
            }
            _globalCounts.Save();
            _pendingSave = false;
        }

        /// <summary>
        /// Poll all tracked keys and update hold/counter state.
        /// Runs every loop iteration (uncapped) so fast key taps are never missed.
        /// </summary>
        private void PollInput()
        {
            for (var i = 0; i < _keyList.Count; i++)
            {
                var key = _keyList[i];
                bool pressed = key.isKey
                    ? Keyboard.IsKeyPressed(key.KeyboardKey)
                    : Mouse.IsButtonPressed(key.MouseButton);

                if (pressed)
                {
                    if (key.Hold == 0)
                    {
                        // New press detected — record start time
                        key.CurrentPressStart = _kpsClock.ElapsedTime.AsSeconds();
                        key.ActiveBarExists = false;

                        // Update counters
                        key.Counter++;
                        key.GlobalCounter++;
                        _globalCounts.Increment(key.KeyName);
                        _sessionTotal++;

                        // Record timestamp for KPS
                        _kpsTimestamps.Add(key.CurrentPressStart);

                        // Mark for auto-save and restart the idle timer
                        _pendingSave = true;
                        _lastKeyPressClock.Restart();
                    }
                    key.Hold++;

                    _keyPressFadeList[i] = _keyFadeTime;
                }
                else
                {
                    if (key.Hold > 0)
                    {
                        // Key just released — if no bar was created yet,
                        // queue this as a completed sub-frame press
                        float now = _kpsClock.ElapsedTime.AsSeconds();
                        float duration = now - key.CurrentPressStart;
                        if (!key.ActiveBarExists)
                        {
                            key.CompletedPresses.Add((key.CurrentPressStart, duration));
                        }
                        key.CurrentPressStart = -1f;
                    }
                    key.Hold = 0;
                }
            }
        }

        public void Run()
        {
            _window.Closed += OnClose;
            // Don't use SFML's framerate limit — we control timing manually
            _window.SetFramerateLimit(0);
            _renderInterval = 1.0f / _maxFPS;
            _renderClock.Restart();

            while (_window.IsOpen)
            {
                _window.DispatchEvents();

                lock (_lock) {
                    // Poll input every iteration (uncapped) so fast presses are caught
                    PollInput();

                    // Only render at the configured FPS
                    if (_renderClock.ElapsedTime.AsSeconds() >= _renderInterval)
                    {
                        _renderClock.Restart();
                        Render();
                    }

                    // Auto-save 3 seconds after last key press
                    if (_pendingSave && _lastKeyPressClock.ElapsedTime.AsSeconds() >= 3.0f)
                    {
                        SaveGlobalCounts();
                    }
                }

                // Sleep ~1ms to avoid busy-spinning while still polling at ~1000 Hz
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Render one frame: update visuals, draw everything, display.
        /// </summary>
        private void Render()
        {
            _window.Size = _size;
            _window.SetView(new View(new FloatRect(0, 0, _size.X, _size.Y + 35)));

            _window.Clear(_backgroundColor);

            // Update square fill colors
            for (var i = 0; i < _keyList.Count; i++)
            {
                var key = _keyList[i];

                if (key.Hold > 0)
                {
                    _squareList[i].FillColor = key._colorPressed;
                }
                else
                {
					float fadeFactor = (float)Math.Pow((float)_keyPressFadeList[i] / (float)_keyFadeTime, _keyFadeExp);
					byte r = (byte)((float)_backgroundColor.R + ((float)key._colorPressed.R - (float)_backgroundColor.R) * fadeFactor);
					byte g = (byte)((float)_backgroundColor.G + ((float)key._colorPressed.G - (float)_backgroundColor.G) * fadeFactor);
					byte b = (byte)((float)_backgroundColor.B + ((float)key._colorPressed.B - (float)_backgroundColor.B) * fadeFactor);
					byte a = (byte)((float)_backgroundColor.A + ((float)key._colorPressed.A - (float)_backgroundColor.A) * fadeFactor);

					Color _colorFaded = new Color(r, g, b, a);
					_squareList[i].FillColor = _colorFaded;

                    if (_keyPressFadeList[i] > 0)
                        _keyPressFadeList[i]--;
                }
            }

            MoveBars(_keyList, _squareList);

            foreach (var staticDrawable in _staticDrawables) _window.Draw(staticDrawable);

            for (var i = 0; i < _keyList.Count; i++)
            {
                var key = _keyList[i];

                if (_counter)
                {
                    var text = CreateItems.CreateText(
                        Convert.ToString(key.Counter),
                            _squareList[i],
                            Color.White,
                            true
                        );
                    _window.Draw(text);
                }

                // Draw per-key global counter below the key
                if (_globalCounter)
                {
                    var globalText = new Text(Convert.ToString(key.GlobalCounter), CreateItems._font);
                    globalText.CharacterSize = 14;
                    globalText.Style = Text.Styles.Bold;
                    globalText.FillColor = new Color(180, 180, 180, 200);
                    var square = _squareList[i];
                    globalText.Origin = new Vector2f(globalText.GetLocalBounds().Width / 2f, 0);
                    globalText.Position = new Vector2f(
                        square.GetGlobalBounds().Left + square.OutlineThickness + square.Size.X / 2f,
                        square.GetGlobalBounds().Top + square.OutlineThickness + square.Size.Y + 40
                    );
                    _window.Draw(globalText);
                }

                foreach (var bar in key.BarList) _window.Draw(bar);
            }

            // Draw stats bar at the bottom
            if (_globalCounter)
            {
                // Compute KPS: count timestamps within the last 1 second
                float currentTime = _kpsClock.ElapsedTime.AsSeconds();
                _kpsTimestamps.RemoveAll(t => currentTime - t > 1.0f);
                int kps = _kpsTimestamps.Count;

                int globalTotal = _globalCounts.GetTotal();
                float margin = 12f;

                string kpsString = _showStatsText ? "KPS: " : "";
                var kpsLabel = CreateItems.CreateStatsText(
                    $"{kpsString}{kps}",
                    margin, _size.Y - 45,
                    new Color(255, 255, 255, 255), 18);
                _window.Draw(kpsLabel);

                string sessionLString = _showStatsText ? "Session: " : "";
                var sessionLabel = CreateItems.CreateStatsText(
                    $"{sessionLString}{_sessionTotal}",
                    margin, _size.Y - 20,
                    new Color(205, 205, 205, 220), 18);
                _window.Draw(sessionLabel);

                string totalString = _showStatsText ? "Total: " : "";
                var totalLabel = CreateItems.CreateStatsText(
                    $"{totalString}{globalTotal}",
                    margin, _size.Y + 7,
                    new Color(155, 155, 155, 180), 14);
                _window.Draw(totalLabel);
            }

            _window.Draw(_fadingTexture.GetSprite());
            _window.Display();
        }

        /// <summary>
        /// if a key is a new input create a new bar, if it is being held stretch it and move all bars up
        /// </summary>
        private void MoveBars(List<Key> keyList, List<RectangleShape> squareList)
        {
            var moveDist = _clock.Restart().AsSeconds() * _barSpeed;
            float now = _kpsClock.ElapsedTime.AsSeconds();

            foreach (var key in keyList)
            {
                var square = squareList.ElementAt(keyList.IndexOf(key));

                // Create bars for completed sub-frame presses with accurate height and position
                foreach (var (startTime, duration) in key.CompletedPresses)
                {
                    float barHeight = Math.Max(duration * _barSpeed, 2f);
                    float timeSinceEnd = now - (startTime + duration);
                    float offset = timeSinceEnd * _barSpeed;

                    var rect = CreateItems.CreateBar(square, _outlineThickness, barHeight);
                    rect.Position = new Vector2f(rect.Position.X, rect.Position.Y - offset);
                    key.BarList.Add(rect);
                }
                key.CompletedPresses.Clear();

                // Create bar for current ongoing press if not yet created
                if (key.Hold > 0 && !key.ActiveBarExists)
                {
                    float holdSoFar = now - key.CurrentPressStart;
                    float barHeight = Math.Max(holdSoFar * _barSpeed, 2f);
                    var rect = CreateItems.CreateBar(square, _outlineThickness, barHeight);
                    key.BarList.Add(rect);
                    key.ActiveBarExists = true;
                }
                // Stretch existing bar if key is still held
                else if (key.Hold > 0 && key.ActiveBarExists)
                {
                    if (key.BarList.Count > 0)
                    {
                        var rect = key.BarList.Last();
                        rect.Size = new Vector2f(rect.Size.X, rect.Size.Y + moveDist);
                    }
                }

                foreach (var rect in key.BarList)
                    rect.Position = new Vector2f(rect.Position.X, rect.Position.Y - moveDist);
                if (key.BarList.Count > 0 && key.BarList.First().Position.Y + key.BarList.First().Size.Y < 0)
                    key.BarList.RemoveAt(0);
            }
        }
    }
}
