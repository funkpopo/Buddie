using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Buddie.Services
{
    /// <summary>
    /// 卡片颜色管理器 - 确保卡片颜色不重复并提供智能颜色分配
    /// </summary>
    public class CardColorManager
    {
        #region Private Fields
        private readonly List<Color> _usedColors = new List<Color>();
        private readonly Random _random = new Random();
        private readonly HashSet<string> _assignedColorIds = new HashSet<string>();
        
        // 预定义的颜色调色板，按色彩和谐度排序
        private readonly Color[] _colorPalette = new[]
        {
            // 蓝色系
            Colors.LightBlue, Colors.SkyBlue, Colors.LightSteelBlue, Colors.PowderBlue, Colors.LightCyan,
            
            // 绿色系
            Colors.LightGreen, Colors.LightSeaGreen, Colors.PaleGreen, Colors.MediumSeaGreen,
            
            // 红色系
            Colors.LightCoral, Colors.LightSalmon, Colors.LightPink, Colors.MistyRose, Colors.PeachPuff,
            
            // 紫色系
            Colors.Plum, Colors.Lavender, Colors.Thistle, Colors.MediumOrchid, Colors.Orchid,
            
            // 黄色系
            Colors.Gold, Colors.LightYellow, Colors.Khaki, Colors.Wheat, Colors.LemonChiffon,
            
            // 橙色系
            Colors.Orange, Colors.NavajoWhite, Colors.BlanchedAlmond, Colors.Moccasin,
            
            // 特殊颜色
            Colors.Silver, Colors.Gainsboro, Colors.LightGray
        };
        #endregion

        #region Public Methods
        
        /// <summary>
        /// 获取下一个可用的唯一颜色
        /// </summary>
        /// <returns>未使用的颜色</returns>
        public Color GetNextAvailableColor()
        {
            // 获取所有未使用的颜色
            var availableColors = _colorPalette.Where(c => !_usedColors.Contains(c)).ToList();
            
            // 如果所有颜色都被使用了，重置并重新开始
            if (availableColors.Count == 0)
            {
                ResetUsedColors();
                availableColors = _colorPalette.ToList();
            }
            
            // 随机选择一个可用颜色
            var selectedColor = availableColors[_random.Next(availableColors.Count)];
            _usedColors.Add(selectedColor);
            
            return selectedColor;
        }
        
        /// <summary>
        /// 获取配对的颜色（正面和背面），确保它们是和谐的但不相同
        /// </summary>
        /// <returns>颜色对</returns>
        public (Color frontColor, Color backColor) GetColorPair()
        {
            var frontColor = GetNextAvailableColor();
            var backColor = GetHarmonousColor(frontColor);
            
            return (frontColor, backColor);
        }
        
        /// <summary>
        /// 为特定ID分配唯一颜色对（支持持久化颜色分配）
        /// </summary>
        /// <param name="id">唯一标识符</param>
        /// <returns>颜色对</returns>
        public (Color frontColor, Color backColor) GetColorPairForId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return GetColorPair();
            }
            
            // 如果该ID已经分配过颜色，生成确定性颜色
            if (_assignedColorIds.Contains(id))
            {
                return GenerateDeterministicColorPair(id);
            }
            
            _assignedColorIds.Add(id);
            var colorPair = GetColorPair();
            
            return colorPair;
        }
        
        /// <summary>
        /// 创建渐变画刷
        /// </summary>
        /// <param name="primaryColor">主要颜色</param>
        /// <param name="secondaryColor">次要颜色（默认为白色）</param>
        /// <returns>线性渐变画刷</returns>
        public LinearGradientBrush CreateGradientBrush(Color primaryColor, Color? secondaryColor = null)
        {
            var secondary = secondaryColor ?? Colors.White;
            return new LinearGradientBrush(
                primaryColor, 
                secondary, 
                new System.Windows.Point(0, 0), 
                new System.Windows.Point(1, 1)
            );
        }
        
        /// <summary>
        /// 批量获取多个不重复且相邻色差明显的颜色对
        /// </summary>
        /// <param name="count">需要的颜色对数量</param>
        /// <returns>颜色对列表</returns>
        public List<(Color frontColor, Color backColor)> GetMultipleColorPairs(int count)
        {
            var colorPairs = new List<(Color, Color)>();
            var usedPrimaryColors = new List<Color>();
            
            for (int i = 0; i < count; i++)
            {
                Color frontColor;
                
                if (i == 0)
                {
                    // 第一张卡片随机选择
                    frontColor = GetNextAvailableColor();
                }
                else
                {
                    // 后续卡片确保与前一张有明显色差
                    frontColor = GetContrastingColor(usedPrimaryColors.Last());
                }
                
                var backColor = GetHarmonousColor(frontColor);
                colorPairs.Add((frontColor, backColor));
                usedPrimaryColors.Add(frontColor);
            }
            
            return colorPairs;
        }
        
        /// <summary>
        /// 获取与指定颜色有明显对比的颜色
        /// </summary>
        /// <param name="referenceColor">参考颜色</param>
        /// <returns>对比颜色</returns>
        private Color GetContrastingColor(Color referenceColor)
        {
            var refHsl = ColorToHsl(referenceColor);
            var availableColors = _colorPalette.Where(c => !_usedColors.Contains(c)).ToList();
            
            if (availableColors.Count == 0)
            {
                ResetUsedColors();
                availableColors = _colorPalette.ToList();
            }
            
            // 按色差排序，选择差异最大的颜色
            var contrastingColors = availableColors
                .Select(color => new
                {
                    Color = color,
                    HueDifference = CalculateHueDifference(refHsl.H, ColorToHsl(color).H),
                    SaturationDifference = Math.Abs(refHsl.S - ColorToHsl(color).S),
                    LightnessDifference = Math.Abs(refHsl.L - ColorToHsl(color).L)
                })
                .Where(x => x.HueDifference >= 60) // 至少60度色相差异
                .OrderByDescending(x => x.HueDifference + x.SaturationDifference + x.LightnessDifference)
                .ToList();
            
            Color selectedColor;
            if (contrastingColors.Any())
            {
                // 从前3个最对比的颜色中随机选择
                var topChoices = contrastingColors.Take(3).ToList();
                selectedColor = topChoices[_random.Next(topChoices.Count)].Color;
            }
            else
            {
                // 如果没有足够对比的颜色，选择任何可用颜色
                selectedColor = availableColors[_random.Next(availableColors.Count)];
            }
            
            _usedColors.Add(selectedColor);
            return selectedColor;
        }
        
        /// <summary>
        /// 计算两个色相之间的最小角度差异
        /// </summary>
        /// <param name="hue1">色相1 (0-360度)</param>
        /// <param name="hue2">色相2 (0-360度)</param>
        /// <returns>最小角度差异 (0-180度)</returns>
        private double CalculateHueDifference(double hue1, double hue2)
        {
            var diff = Math.Abs(hue1 - hue2);
            return Math.Min(diff, 360 - diff);
        }
        
        /// <summary>
        /// 重置所有已使用的颜色
        /// </summary>
        public void ResetUsedColors()
        {
            _usedColors.Clear();
        }
        
        /// <summary>
        /// 重置所有颜色分配状态
        /// </summary>
        public void ResetAll()
        {
            _usedColors.Clear();
            _assignedColorIds.Clear();
        }
        
        /// <summary>
        /// 检查颜色是否已被使用
        /// </summary>
        /// <param name="color">要检查的颜色</param>
        /// <returns>是否已被使用</returns>
        public bool IsColorUsed(Color color)
        {
            return _usedColors.Contains(color);
        }
        
        /// <summary>
        /// 获取当前可用颜色数量
        /// </summary>
        /// <returns>可用颜色数量</returns>
        public int GetAvailableColorCount()
        {
            return _colorPalette.Length - _usedColors.Count;
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// 获取与给定颜色和谐的颜色
        /// </summary>
        /// <param name="baseColor">基础颜色</param>
        /// <returns>和谐的颜色</returns>
        private Color GetHarmonousColor(Color baseColor)
        {
            // 找到与基础颜色和谐的颜色（避免选择相同颜色）
            var availableColors = _colorPalette.Where(c => 
                !_usedColors.Contains(c) && 
                c != baseColor &&
                AreColorsHarmonious(baseColor, c)
            ).ToList();
            
            if (availableColors.Count == 0)
            {
                // 如果没有和谐的颜色，选择任何不同的可用颜色
                availableColors = _colorPalette.Where(c => 
                    !_usedColors.Contains(c) && c != baseColor
                ).ToList();
            }
            
            if (availableColors.Count == 0)
            {
                // 如果仍然没有可用颜色，重置并选择
                ResetUsedColors();
                availableColors = _colorPalette.Where(c => c != baseColor).ToList();
            }
            
            var selectedColor = availableColors[_random.Next(availableColors.Count)];
            _usedColors.Add(selectedColor);
            
            return selectedColor;
        }
        
        /// <summary>
        /// 判断两个颜色是否和谐
        /// </summary>
        /// <param name="color1">颜色1</param>
        /// <param name="color2">颜色2</param>
        /// <returns>是否和谐</returns>
        private bool AreColorsHarmonious(Color color1, Color color2)
        {
            // 简单的颜色和谐度判断 - 基于色相差异
            var hsl1 = ColorToHsl(color1);
            var hsl2 = ColorToHsl(color2);
            
            // 计算色相差异（0-360度）
            var hueDiff = Math.Abs(hsl1.H - hsl2.H);
            if (hueDiff > 180) hueDiff = 360 - hueDiff;
            
            // 和谐的色相差异范围：30-150度
            return hueDiff >= 30 && hueDiff <= 150;
        }
        
        /// <summary>
        /// 将RGB颜色转换为HSL
        /// </summary>
        /// <param name="color">RGB颜色</param>
        /// <returns>HSL值</returns>
        private (double H, double S, double L) ColorToHsl(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            
            double h = 0, s, l = (max + min) / 2;
            
            if (max == min)
            {
                h = s = 0; // 无色
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                
                switch (max)
                {
                    case var _ when max == r:
                        h = (g - b) / d + (g < b ? 6 : 0);
                        break;
                    case var _ when max == g:
                        h = (b - r) / d + 2;
                        break;
                    case var _ when max == b:
                        h = (r - g) / d + 4;
                        break;
                }
                h /= 6;
            }
            
            return (h * 360, s, l);
        }
        
        /// <summary>
        /// 为给定ID生成确定性的颜色对
        /// </summary>
        /// <param name="id">唯一标识符</param>
        /// <returns>确定性的颜色对</returns>
        private (Color frontColor, Color backColor) GenerateDeterministicColorPair(string id)
        {
            // 使用ID的哈希值来生成确定性的颜色
            var hash = id.GetHashCode();
            var frontIndex = Math.Abs(hash) % _colorPalette.Length;
            var backIndex = Math.Abs(hash / 2) % _colorPalette.Length;
            
            // 确保正面和背面颜色不同
            if (frontIndex == backIndex)
            {
                backIndex = (backIndex + 1) % _colorPalette.Length;
            }
            
            return (_colorPalette[frontIndex], _colorPalette[backIndex]);
        }
        
        #endregion
    }
}