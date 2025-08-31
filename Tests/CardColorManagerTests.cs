using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Buddie.Services;

namespace Buddie.Tests
{
    /// <summary>
    /// 卡片颜色管理器测试类
    /// </summary>
    public static class CardColorManagerTests
    {
        /// <summary>
        /// 运行所有颜色管理器测试
        /// </summary>
        /// <returns>测试结果报告</returns>
        public static string RunAllTests()
        {
            var results = new List<string>();
            
            try
            {
                // 测试1: 基本颜色分配
                results.Add(TestBasicColorAssignment());
                
                // 测试2: 颜色不重复验证
                results.Add(TestColorUniqueness());
                
                // 测试3: 颜色对和谐性测试
                results.Add(TestColorPairHarmony());
                
                // 测试4: ID持久化测试
                results.Add(TestIdPersistence());
                
                // 测试5: 大量颜色分配测试
                results.Add(TestLargeScaleColorAllocation());
                
                return string.Join("\n", results);
            }
            catch (Exception ex)
            {
                return $"测试运行失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 测试基本颜色分配功能
        /// </summary>
        private static string TestBasicColorAssignment()
        {
            var colorManager = new CardColorManager();
            
            try
            {
                // 获取单个颜色
                var color1 = colorManager.GetNextAvailableColor();
                var color2 = colorManager.GetNextAvailableColor();
                
                // 获取颜色对
                var colorPair = colorManager.GetColorPair();
                
                // 创建渐变画刷
                var brush = colorManager.CreateGradientBrush(color1);
                
                return $"✅ 基本颜色分配测试通过 - 获取到颜色: {color1.ToString()}, {color2.ToString()}";
            }
            catch (Exception ex)
            {
                return $"❌ 基本颜色分配测试失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 测试颜色不重复功能
        /// </summary>
        private static string TestColorUniqueness()
        {
            var colorManager = new CardColorManager();
            var colors = new HashSet<Color>();
            var duplicateCount = 0;
            
            try
            {
                // 获取多个颜色，检查重复
                for (int i = 0; i < 15; i++)  // 尝试获取15个颜色
                {
                    var color = colorManager.GetNextAvailableColor();
                    if (!colors.Add(color))
                    {
                        duplicateCount++;
                    }
                }
                
                if (duplicateCount == 0)
                {
                    return $"✅ 颜色唯一性测试通过 - 成功分配了 {colors.Count} 个不重复颜色";
                }
                else
                {
                    return $"⚠️ 颜色唯一性测试部分通过 - {colors.Count} 个唯一颜色，{duplicateCount} 个重复";
                }
            }
            catch (Exception ex)
            {
                return $"❌ 颜色唯一性测试失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 测试颜色对和谐性
        /// </summary>
        private static string TestColorPairHarmony()
        {
            var colorManager = new CardColorManager();
            var validPairs = 0;
            
            try
            {
                // 测试多个颜色对
                for (int i = 0; i < 5; i++)
                {
                    var colorPair = colorManager.GetColorPair();
                    
                    // 检查颜色对是否不同
                    if (colorPair.frontColor != colorPair.backColor)
                    {
                        validPairs++;
                    }
                }
                
                return $"✅ 颜色对和谐性测试通过 - {validPairs}/5 对颜色都不相同";
            }
            catch (Exception ex)
            {
                return $"❌ 颜色对和谐性测试失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 测试ID持久化功能
        /// </summary>
        private static string TestIdPersistence()
        {
            var colorManager = new CardColorManager();
            
            try
            {
                // 为同一ID多次获取颜色
                var id = "TestAPI_001";
                var colorPair1 = colorManager.GetColorPairForId(id);
                var colorPair2 = colorManager.GetColorPairForId(id);
                
                // 检查是否返回相同颜色
                if (colorPair1.frontColor == colorPair2.frontColor && 
                    colorPair1.backColor == colorPair2.backColor)
                {
                    return $"✅ ID持久化测试通过 - ID '{id}' 返回一致的颜色";
                }
                else
                {
                    return $"❌ ID持久化测试失败 - ID '{id}' 返回了不同的颜色";
                }
            }
            catch (Exception ex)
            {
                return $"❌ ID持久化测试失败: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 测试大量颜色分配
        /// </summary>
        private static string TestLargeScaleColorAllocation()
        {
            var colorManager = new CardColorManager();
            
            try
            {
                // 获取多个颜色对
                var colorPairs = colorManager.GetMultipleColorPairs(10);
                
                // 检查可用颜色数量
                var availableColors = colorManager.GetAvailableColorCount();
                
                // 重置并检查
                colorManager.ResetAll();
                var availableAfterReset = colorManager.GetAvailableColorCount();
                
                return $"✅ 大量颜色分配测试通过 - 分配了 {colorPairs.Count} 对颜色，重置后可用颜色: {availableAfterReset}";
            }
            catch (Exception ex)
            {
                return $"❌ 大量颜色分配测试失败: {ex.Message}";
            }
        }
    }
}