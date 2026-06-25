using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KeyOverlay
{
    public class GlobalCountStore
    {
        private Dictionary<string, int> _counts = new();
        private string _path;

        public GlobalCountStore(string path)
        {
            _path = path;
            Load();
        }

        /// <summary>
        /// Load counts from the .dat file. If the file doesn't exist, starts empty.
        /// </summary>
        public void Load()
        {
            _counts.Clear();
            if (!File.Exists(_path))
                return;

            foreach (var line in File.ReadAllLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[1], out int count))
                {
                    _counts[parts[0]] = count;
                }
            }
        }

        /// <summary>
        /// Save all counts to the .dat file in key=count format.
        /// </summary>
        public void Save()
        {
            var lines = _counts.Select(kv => $"{kv.Key}={kv.Value}");
            File.WriteAllLines(_path, lines);
        }

        /// <summary>
        /// Increment the count for a given key name by 1.
        /// </summary>
        public void Increment(string keyName)
        {
            if (_counts.ContainsKey(keyName))
                _counts[keyName]++;
            else
                _counts[keyName] = 1;
        }

        /// <summary>
        /// Get the global count for a specific key.
        /// </summary>
        public int GetCount(string keyName)
        {
            return _counts.TryGetValue(keyName, out int count) ? count : 0;
        }

        /// <summary>
        /// Get the sum of all global counts.
        /// </summary>
        public int GetTotal()
        {
            int total = 0;
            foreach (var kv in _counts)
                total += kv.Value;
            return total;
        }
    }
}
