using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace Buddie.Localization
{
    public static class LocalizationManager
    {
        private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;
        private static readonly ResourceManager _resourceManager = new ResourceManager("Buddie.Resources.Strings", typeof(LocalizationManager).Assembly);

        public static event EventHandler? CultureChanged;

        public static CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (!Equals(_currentCulture, value))
                {
                    _currentCulture = value;
                    CultureChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public static string GetString(string key)
        {
            try
            {
                var value = _resourceManager.GetString(key, _currentCulture);
                return string.IsNullOrEmpty(value) ? key : value!;
            }
            catch
            {
                return key;
            }
        }
    }
}

