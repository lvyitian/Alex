﻿using System.Collections.Generic;
using System.Globalization;

namespace Alex.API.Localization
{
    public class CultureLanguage
    {
        public CultureInfo CultureInfo { get; }
        
        public string this[string key]
        {
            get { return GetString(key); }
        }

        private readonly Dictionary<string, string> _translations = new Dictionary<string, string>();

        public CultureLanguage(CultureInfo info)
        {
            CultureInfo = info;
        }

        public void Load(IDictionary<string, string> translations)
        {
            foreach (var translation in translations)
            {
				if (_translations.ContainsKey(translation.Key)) continue;
                _translations[translation.Key] = translation.Value;
            }
        }

        public string GetString(string key)
        {
            if (_translations.TryGetValue(key, out var value))
            {
                return value;
            }

            return $"[Translation={key}]";
        }

        public string DisplayName
        {
            get
            {
                string name = GetString("language.name");
                string region = GetString("language.region");
                if (!string.IsNullOrWhiteSpace(region))
                {
                    return $"{name} ({region})";
                }

                return name;
            }
        }
    }
}
