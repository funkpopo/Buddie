using System;
using System.Windows;
using System.Windows.Markup;

namespace Buddie.Localization
{
    [MarkupExtensionReturnType(typeof(string))]
    public class ResxExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        private WeakReference? _targetObjectRef;
        private object? _targetProperty;

        public ResxExtension() { }
        public ResxExtension(string key) { Key = key; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt)
            {
                _targetObjectRef = pvt.TargetObject != null ? new WeakReference(pvt.TargetObject) : null;
                _targetProperty = pvt.TargetProperty;
            }

            var value = LocalizationManager.GetString(Key);

            // 订阅语言变更，动态更新绑定的依赖属性
            LocalizationManager.CultureChanged -= OnCultureChanged;
            LocalizationManager.CultureChanged += OnCultureChanged;

            return value;
        }

        private void OnCultureChanged(object? sender, EventArgs e)
        {
            try
            {
                if (_targetObjectRef?.Target is DependencyObject d && _targetProperty is DependencyProperty dp)
                {
                    // 再次获取最新值并设置
                    var value = LocalizationManager.GetString(Key);
                    if (!d.Dispatcher.CheckAccess())
                    {
                        d.Dispatcher.Invoke(() => d.SetValue(dp, value));
                    }
                    else
                    {
                        d.SetValue(dp, value);
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}

