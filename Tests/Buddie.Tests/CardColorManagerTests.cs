using System.Collections.Generic;
using System.Windows.Media;
using Buddie.Services;
using FluentAssertions;
using Xunit;

namespace Buddie.Tests
{
    /// <summary>
    /// 卡片颜色管理器测试类
    /// </summary>
    public class CardColorManagerTests
    {
        [Fact]
        public void GetNextAvailableColor_ShouldReturnValidColor()
        {
            // Arrange
            var colorManager = new CardColorManager();

            // Act
            var color1 = colorManager.GetNextAvailableColor();
            var color2 = colorManager.GetNextAvailableColor();

            // Assert
            color1.Should().NotBe(default(Color));
            color2.Should().NotBe(default(Color));
            color1.Should().NotBe(color2, "consecutive colors should be different");
        }

        [Fact]
        public void GetColorPair_ShouldReturnDifferentFrontAndBackColors()
        {
            // Arrange
            var colorManager = new CardColorManager();

            // Act
            var colorPair = colorManager.GetColorPair();

            // Assert
            colorPair.frontColor.Should().NotBe(default(Color));
            colorPair.backColor.Should().NotBe(default(Color));
            colorPair.frontColor.Should().NotBe(colorPair.backColor,
                "front and back colors should be different");
        }

        [Fact]
        public void CreateGradientBrush_ShouldReturnValidBrush()
        {
            // Arrange
            var colorManager = new CardColorManager();
            var color = colorManager.GetNextAvailableColor();

            // Act
            var brush = colorManager.CreateGradientBrush(color);

            // Assert
            brush.Should().NotBeNull();
            brush.Should().BeOfType<LinearGradientBrush>();
            var gradientBrush = brush as LinearGradientBrush;
            gradientBrush.GradientStops.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public void GetNextAvailableColor_ShouldNotReturnDuplicatesWithinReasonableLimit()
        {
            // Arrange
            var colorManager = new CardColorManager();
            var colors = new HashSet<Color>();
            const int testCount = 15;

            // Act
            for (int i = 0; i < testCount; i++)
            {
                var color = colorManager.GetNextAvailableColor();
                colors.Add(color);
            }

            // Assert
            colors.Count.Should().Be(testCount,
                "all colors should be unique within reasonable limit");
        }

        [Fact]
        public void GetColorPairForId_ShouldReturnConsistentColorsForSameId()
        {
            // Arrange
            var colorManager = new CardColorManager();
            const string testId = "TestAPI_001";

            // Act
            var colorPair1 = colorManager.GetColorPairForId(testId);
            var colorPair2 = colorManager.GetColorPairForId(testId);

            // Assert
            colorPair1.frontColor.Should().Be(colorPair2.frontColor,
                "front color should be consistent for the same ID");
            colorPair1.backColor.Should().Be(colorPair2.backColor,
                "back color should be consistent for the same ID");
        }

        [Fact]
        public void GetColorPairForId_ShouldReturnDifferentColorsForDifferentIds()
        {
            // Arrange
            var colorManager = new CardColorManager();
            const string testId1 = "TestAPI_001";
            const string testId2 = "TestAPI_002";

            // Act
            var colorPair1 = colorManager.GetColorPairForId(testId1);
            var colorPair2 = colorManager.GetColorPairForId(testId2);

            // Assert
            (colorPair1.frontColor != colorPair2.frontColor ||
             colorPair1.backColor != colorPair2.backColor).Should().BeTrue(
                "different IDs should generally have different color pairs");
        }

        [Fact]
        public void GetMultipleColorPairs_ShouldReturnRequestedNumberOfPairs()
        {
            // Arrange
            var colorManager = new CardColorManager();
            const int requestedCount = 10;

            // Act
            var colorPairs = colorManager.GetMultipleColorPairs(requestedCount);

            // Assert
            colorPairs.Should().NotBeNull();
            colorPairs.Should().HaveCount(requestedCount);

            foreach (var pair in colorPairs)
            {
                pair.frontColor.Should().NotBe(default(Color));
                pair.backColor.Should().NotBe(default(Color));
                pair.frontColor.Should().NotBe(pair.backColor);
            }
        }

        [Fact]
        public void GetAvailableColorCount_ShouldReturnCorrectCount()
        {
            // Arrange
            var colorManager = new CardColorManager();

            // Act
            var initialCount = colorManager.GetAvailableColorCount();
            colorManager.GetNextAvailableColor();
            colorManager.GetNextAvailableColor();
            var afterTwoAllocations = colorManager.GetAvailableColorCount();

            // Assert
            initialCount.Should().BeGreaterThan(0);
            afterTwoAllocations.Should().Be(initialCount - 2,
                "available count should decrease after allocations");
        }

        [Fact]
        public void ResetAll_ShouldRestoreAllColors()
        {
            // Arrange
            var colorManager = new CardColorManager();
            var initialCount = colorManager.GetAvailableColorCount();

            // Act - Allocate some colors
            for (int i = 0; i < 5; i++)
            {
                colorManager.GetNextAvailableColor();
            }
            var afterAllocation = colorManager.GetAvailableColorCount();

            // Reset
            colorManager.ResetAll();
            var afterReset = colorManager.GetAvailableColorCount();

            // Assert
            afterAllocation.Should().BeLessThan(initialCount);
            afterReset.Should().Be(initialCount,
                "reset should restore all colors to available");
        }

        [Fact]
        public void GetColorPair_MultipleCallsShouldReturnValidPairs()
        {
            // Arrange
            var colorManager = new CardColorManager();
            const int testCount = 5;
            var pairs = new List<(Color frontColor, Color backColor)>();

            // Act
            for (int i = 0; i < testCount; i++)
            {
                var pair = colorManager.GetColorPair();
                pairs.Add(pair);
            }

            // Assert
            pairs.Should().HaveCount(testCount);
            foreach (var pair in pairs)
            {
                pair.frontColor.Should().NotBe(pair.backColor,
                    "each pair should have different front and back colors");
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void GetColorPairForId_ShouldHandleInvalidIds(string invalidId)
        {
            // Arrange
            var colorManager = new CardColorManager();

            // Act
            var colorPair = colorManager.GetColorPairForId(invalidId);

            // Assert
            colorPair.frontColor.Should().NotBe(default(Color));
            colorPair.backColor.Should().NotBe(default(Color));
            colorPair.frontColor.Should().NotBe(colorPair.backColor);
        }

        [Fact]
        public void GetMultipleColorPairs_WithZeroCount_ShouldReturnEmptyList()
        {
            // Arrange
            var colorManager = new CardColorManager();

            // Act
            var colorPairs = colorManager.GetMultipleColorPairs(0);

            // Assert
            colorPairs.Should().NotBeNull();
            colorPairs.Should().BeEmpty();
        }

        [Fact]
        public void GetMultipleColorPairs_WithNegativeCount_ShouldReturnEmptyList()
        {
            // Arrange
            var colorManager = new CardColorManager();

            // Act
            var colorPairs = colorManager.GetMultipleColorPairs(-5);

            // Assert
            colorPairs.Should().NotBeNull();
            colorPairs.Should().BeEmpty();
        }
    }
}