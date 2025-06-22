using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Lyxie_desktop.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyxie_desktop.Controls;

public partial class WordCloudControl : UserControl
{
    private readonly DispatcherTimer _spawnTimer;
    private readonly DispatcherTimer _particleTimer;
    private readonly Random _random = new();
    private readonly List<WordElement> _activeWords = new();
    private readonly List<ParticleElement> _activeParticles = new();
    private readonly Queue<TextBlock> _wordPool = new();
    private readonly Queue<Ellipse> _particlePool = new();
    private Canvas? _wordCanvas;
    private Canvas? _particleCanvas;
    private Canvas? _effectCanvas;

    // 圆形区域参数 - 动态计算基于实际窗口大小
    private const double CircleRadius = 200;
    private const double WordZoneRadius = 160; // 词汇活动区域半径
    private const double ButtonHideRadius = 300; // 进入此范围时词云隐藏
    
    // 动态窗口尺寸属性
    private double _currentWidth = 1280;
    private double _currentHeight = 760;
    private double _circleCenterX = 640;
    private double _circleCenterY = 380;

    // 词汇库 - AI相关术语
    private readonly string[] _words = {
        "今天天气怎么样？","设置一个明天早上7点的闹钟","播放音乐","今天有什么新闻？","讲个笑话","我今天的日程是什么？","附近有推荐的餐厅吗？","现在几点了？","设置一个10分钟的倒计时","提醒我晚上9点吃药","打电话给妈妈","发短信告诉小明我晚点到","打开客厅的灯","关掉电视","把空调温度调到26度","翻译'你好'成英文","一美元等于多少人民币？","搜索一下人工智能的定义","播放我喜欢的歌单","下一首歌","声音大一点","声音小一点","暂停播放","继续播放","今天日期是几号？","明天会下雨吗？","去公司的路况怎么样？","帮我查一下苹果的股价","最近的加油站在哪里？","帮我找个菜谱","添加到我的购物清单：牛奶和鸡蛋","我的购物清单里有什么？","设定一个重复的闹钟，周一到周五早上8点","取消所有闹钟","15的平方是多少？","地球到月球的距离是多少？","给我读一下最新的未读邮件","打开蓝牙","关闭Wi-Fi","播放一首轻松的音乐","明天的天气预报","创建一个名为'工作'的待办事项清单","在我的'工作'清单里添加'回复邮件'","我今天走了多少步？","播放最新的播客","快进30秒","后退10秒","这首歌是谁唱的？","把这首歌加入我的收藏","附近的药店几点关门？","帮我预订一张今晚的电影票","从这里到机场需要多长时间？","导航到最近的超市","打开摄像头","拍一张照片","开始录像","我的手机在哪里？","播放白噪音","今天有什么体育新闻？","告诉我一个有趣的事实","掷个骰子","抛个硬币","设置一个会议提醒，下午3点","查询一下我的快递到哪里了","打开智能插座","启动扫地机器人","空气净化器开到自动模式","窗帘拉上","播放一则睡前故事","今天有什么财经新闻？","给我解释一下什么是黑洞","推荐一本好书","100公里等于多少英里？","这个周末有什么活动？","播放古典音乐","静音","取消静音","打开'设置'应用","屏幕亮度调亮点","屏幕亮度调暗点","打开省电模式","珠穆朗玛峰有多高？","设定一个番茄钟，25分钟","我需要带伞吗？","播放下雨的声音","朗读一篇新闻头条","今天有什么娱乐八卦？","帮我找一个学习英语的App","打开音乐识别","这是什么歌？","把灯光调成暖色","电视声音调到15","我需要多长时间才能走完5公里？","最近的ATM机在哪里？","创建一条备忘录","大声朗读我的备忘录","连接到我的蓝牙耳机","断开蓝牙连接","播放一期科技播客","今天历史上的今天发生了什么？","这个周末天气好吗？"
    };

    // 配置参数 - 优化性能和流畅度 (60fps优化)
    private const int MaxActiveWords = 25;
    private const int MaxActiveParticles = 35;
    private const int SpawnIntervalMs = 1000;
    private const int ParticleIntervalMs = 17; // 约60fps (1000/60≈16.67)
    private const double MinFontSize = 12;
    private const double MaxFontSize = 30;
    private const double MinOpacity = 0.4;
    private const double MaxOpacity = 0.9;

    // 状态管理
    private bool _isButtonHovered = false;
    private bool _isButtonPressed = false;
    private double _globalOpacityMultiplier = 1.0;

    public WordCloudControl()
    {
        InitializeComponent();
        
        _spawnTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(SpawnIntervalMs)
        };
        _spawnTimer.Tick += OnSpawnTimerTick;

        _particleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ParticleIntervalMs)
        };
        _particleTimer.Tick += OnParticleTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _wordCanvas = this.FindControl<Canvas>("WordCanvas");
        _particleCanvas = this.FindControl<Canvas>("ParticleCanvas");
        _effectCanvas = this.FindControl<Canvas>("EffectCanvas");

        // 初始化窗口尺寸
        UpdateWindowSize();

        if (_wordCanvas != null && _particleCanvas != null)
        {
            _spawnTimer.Start();
            _particleTimer.Start();

            // 立即生成初始词汇和粒子
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(300);
                for (int i = 0; i < 3; i++)
                {
                    await SpawnWord();
                    await Task.Delay(800);
                }
                
                // 启动粒子效果
                for (int i = 0; i < 10; i++)
                {
                    SpawnParticle();
                }
            });
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateWindowSize();
    }

    private void UpdateWindowSize()
    {
        // 获取实际控件大小，如果为0则使用默认值
        _currentWidth = this.Bounds.Width > 0 ? this.Bounds.Width : 1280;
        _currentHeight = this.Bounds.Height > 0 ? this.Bounds.Height : 760;
        
        // 更新中心点
        _circleCenterX = _currentWidth / 2;
        _circleCenterY = _currentHeight / 2;
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _spawnTimer.Stop();
        _particleTimer.Stop();
        ClearAllElements();
    }

    private async void OnSpawnTimerTick(object? sender, EventArgs e)
    {
        if (_wordCanvas == null || _activeWords.Count >= MaxActiveWords) return;
        await Dispatcher.UIThread.InvokeAsync(async () => await SpawnWord());
    }

    private void OnParticleTimerTick(object? sender, EventArgs e)
    {
        if (_particleCanvas == null) return;
        
        Dispatcher.UIThread.Post(() =>
        {
            // 生成新粒子 - 适应60fps优化生成频率
            if (_activeParticles.Count < MaxActiveParticles && _random.NextDouble() < 0.15)
            {
                SpawnParticle();
            }
            
            // 更新现有粒子
            UpdateParticles();
        });
    }

    private async Task SpawnWord()
    {
        if (_wordCanvas == null) return;

        var wordElement = CreateWordElement();
        if (wordElement == null) return;

        _activeWords.Add(wordElement);
        _wordCanvas.Children.Add(wordElement.TextBlock);

        // 开始词汇动画
        await AnimateWord(wordElement);
    }

    private WordElement? CreateWordElement()
    {
        var textBlock = GetOrCreateWordTextBlock();
        if (textBlock == null) return null;

        // 随机选择词汇
        var word = _words[_random.Next(_words.Length)];
        textBlock.Text = word;
        
        // 随机字体大小
        var fontSize = MinFontSize + _random.NextDouble() * (MaxFontSize - MinFontSize);
        textBlock.FontSize = fontSize;
        
        // 随机透明度
        var opacity = (MinOpacity + _random.NextDouble() * (MaxOpacity - MinOpacity)) * _globalOpacityMultiplier;
        textBlock.Opacity = opacity;
        
        // 设置颜色
        textBlock.Foreground = GetWordColor();
        textBlock.FontWeight = FontWeight.Medium;
        
        // 随机起始位置（主界面边缘）
        var startPos = GetRandomMainViewEdgePosition();
        var targetPos = GetRandomButtonApproachPosition();
        
        Canvas.SetLeft(textBlock, startPos.X);
        Canvas.SetTop(textBlock, startPos.Y);

        return new WordElement
        {
            TextBlock = textBlock,
            StartPosition = startPos,
            TargetPosition = targetPos,
            CreationTime = DateTime.Now,
            AnimationType = GetRandomAnimationType()
        };
    }

    private TextBlock? GetOrCreateWordTextBlock()
    {
        if (_wordPool.Count > 0)
        {
            return _wordPool.Dequeue();
        }

        return new TextBlock
        {
            FontWeight = FontWeight.Medium,
            TextAlignment = TextAlignment.Center
        };
    }

    private IBrush GetWordColor()
    {
        var colors = Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark
            ? new[] { "#4A9EFF", "#7B68EE", "#20B2AA", "#FF6B6B", "#FFD93D", "#6BCF7F", "#FF8C42", "#9B59B6" }
            : new[] { "#2E86AB", "#A23B72", "#F18F01", "#C73E1D", "#7209B7", "#2F9B69", "#E67E22", "#8E44AD" };
        
        var color = colors[_random.Next(colors.Length)];
        return new SolidColorBrush(Color.Parse(color));
    }

    private Point GetRandomMainViewEdgePosition()
    {
        // 从主界面的四个边缘随机选择一个位置，基于当前窗口大小
        var side = _random.Next(4); // 0=左, 1=上, 2=右, 3=下
        var edgeOffset = 50 + _random.NextDouble() * 100; // 边缘偏移距离
        
        return side switch
        {
            0 => new Point(-edgeOffset, _random.NextDouble() * _currentHeight), // 左侧
            1 => new Point(_random.NextDouble() * _currentWidth, -edgeOffset), // 上方
            2 => new Point(_currentWidth + edgeOffset, _random.NextDouble() * _currentHeight), // 右侧
            3 => new Point(_random.NextDouble() * _currentWidth, _currentHeight + edgeOffset), // 下方
            _ => new Point(-100, _currentHeight / 2)
        };
    }

    private Point GetRandomButtonApproachPosition()
    {
        // 目标位置是接近按钮但不进入隐藏范围的区域
        var angle = _random.NextDouble() * 2 * Math.PI;
        var radius = ButtonHideRadius + 20 + _random.NextDouble() * 50; // 在隐藏范围外
        
        var x = _circleCenterX + Math.Cos(angle) * radius;
        var y = _circleCenterY + Math.Sin(angle) * radius;
        
        return new Point(x, y);
    }

    private WordAnimationType GetRandomAnimationType()
    {
        var types = Enum.GetValues<WordAnimationType>();
        return types[_random.Next(types.Length)];
    }

    private async Task AnimateWord(WordElement wordElement)
    {
        if (_wordCanvas == null) return;

        try
        {
            var duration = TimeSpan.FromMilliseconds(6000 + _random.Next(3000)); // 6-9秒，更快的移动
            
            // 根据动画类型执行不同的动画
            switch (wordElement.AnimationType)
            {
                case WordAnimationType.Spiral:
                    await CreateSpiralAnimation(wordElement, duration);
                    break;
                case WordAnimationType.Wave:
                    await CreateWaveAnimation(wordElement, duration);
                    break;
                case WordAnimationType.Orbit:
                    await CreateOrbitAnimation(wordElement, duration);
                    break;
                default:
                    await CreateLinearAnimation(wordElement, duration);
                    break;
            }
        }
        catch (Exception)
        {
            // 动画被中断，清理资源
        }
        finally
        {
            RemoveWord(wordElement);
        }
    }

    private async Task CreateSpiralAnimation(WordElement wordElement, TimeSpan duration)
    {
        const int steps = 360; // 60fps * 6秒 = 360步，确保60fps流畅度
        var stepDelay = (int)(duration.TotalMilliseconds / steps);
        
        var startAngle = Math.Atan2(wordElement.StartPosition.Y - _circleCenterY, wordElement.StartPosition.X - _circleCenterX);
        var totalRotations = 2.0 + _random.NextDouble() * 2.0; // 2-4圈
        
        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var easedProgress = EaseInOutCubic(progress);
            
            // 螺旋运动
            var currentAngle = startAngle + totalRotations * 2 * Math.PI * progress;
            var currentRadius = CircleRadius * (1 - easedProgress * 0.8);
            
            var x = _circleCenterX + Math.Cos(currentAngle) * currentRadius;
            var y = _circleCenterY + Math.Sin(currentAngle) * currentRadius;
            
            Canvas.SetLeft(wordElement.TextBlock, x);
            Canvas.SetTop(wordElement.TextBlock, y);
            
            // 检查是否进入按钮隐藏范围
            var distanceToButton = Math.Sqrt(Math.Pow(x - _circleCenterX, 2) + Math.Pow(y - _circleCenterY, 2));
            
            // 透明度变化 - 接近按钮时快速淡出
            var baseOpacity = Math.Sin(progress * Math.PI) * _globalOpacityMultiplier;
            var hideOpacity = distanceToButton < ButtonHideRadius ? 
                Math.Max(0, (distanceToButton - CircleRadius) / (ButtonHideRadius - CircleRadius)) : 1.0;
            
            wordElement.TextBlock.Opacity = baseOpacity * (MinOpacity + (MaxOpacity - MinOpacity) * (1 - progress * 0.5)) * hideOpacity;
            
            // 如果完全进入按钮范围，提前结束动画
            if (distanceToButton < CircleRadius)
            {
                break;
            }
            
            if (i < steps)
            {
                await Task.Delay(stepDelay);
            }
        }
    }

    private async Task CreateWaveAnimation(WordElement wordElement, TimeSpan duration)
    {
        const int steps = 360; // 60fps * 6秒 = 360步，确保60fps流畅度
        var stepDelay = (int)(duration.TotalMilliseconds / steps);
        
        var startPos = wordElement.StartPosition;
        var endPos = wordElement.TargetPosition;
        
        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var easedProgress = EaseInOutSine(progress);
            
            // 基础线性移动
            var x = startPos.X + (endPos.X - startPos.X) * easedProgress;
            var y = startPos.Y + (endPos.Y - startPos.Y) * easedProgress;
            
            // 添加波浪效果
            var waveAmplitude = 30 * Math.Sin(progress * Math.PI);
            var waveOffset = Math.Sin(progress * Math.PI * 4) * waveAmplitude;
            
            // 垂直于移动方向的波浪
            var moveAngle = Math.Atan2(endPos.Y - startPos.Y, endPos.X - startPos.X);
            x += Math.Cos(moveAngle + Math.PI / 2) * waveOffset;
            y += Math.Sin(moveAngle + Math.PI / 2) * waveOffset;
            
            Canvas.SetLeft(wordElement.TextBlock, x);
            Canvas.SetTop(wordElement.TextBlock, y);
            
            // 检查是否进入按钮隐藏范围
            var distanceToButton = Math.Sqrt(Math.Pow(x - _circleCenterX, 2) + Math.Pow(y - _circleCenterY, 2));
            
            // 透明度变化 - 接近按钮时快速淡出
            var baseOpacity = Math.Sin(progress * Math.PI) * _globalOpacityMultiplier;
            var hideOpacity = distanceToButton < ButtonHideRadius ? 
                Math.Max(0, (distanceToButton - CircleRadius) / (ButtonHideRadius - CircleRadius)) : 1.0;
            
            wordElement.TextBlock.Opacity = baseOpacity * (MaxOpacity - progress * 0.3) * hideOpacity;
            
            // 如果完全进入按钮范围，提前结束动画
            if (distanceToButton < CircleRadius)
            {
                break;
            }
            
            if (i < steps)
            {
                await Task.Delay(stepDelay);
            }
        }
    }

    private async Task CreateOrbitAnimation(WordElement wordElement, TimeSpan duration)
    {
        const int steps = 360; // 60fps * 6秒 = 360步，确保60fps流畅度
        var stepDelay = (int)(duration.TotalMilliseconds / steps);
        
        var orbitRadius = 80 + _random.NextDouble() * 60;
        var orbitCenterX = _circleCenterX + (_random.NextDouble() - 0.5) * 100;
        var orbitCenterY = _circleCenterY + (_random.NextDouble() - 0.5) * 100;
        var startAngle = _random.NextDouble() * 2 * Math.PI;
        
        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var currentAngle = startAngle + progress * 2 * Math.PI;
            
            var x = orbitCenterX + Math.Cos(currentAngle) * orbitRadius;
            var y = orbitCenterY + Math.Sin(currentAngle) * orbitRadius;
            
            Canvas.SetLeft(wordElement.TextBlock, x);
            Canvas.SetTop(wordElement.TextBlock, y);
            
            // 检查是否进入按钮隐藏范围
            var distanceToButton = Math.Sqrt(Math.Pow(x - _circleCenterX, 2) + Math.Pow(y - _circleCenterY, 2));
            
            // 透明度变化 - 接近按钮时快速淡出
            var baseOpacity = Math.Sin(progress * Math.PI) * _globalOpacityMultiplier;
            var hideOpacity = distanceToButton < ButtonHideRadius ? 
                Math.Max(0, (distanceToButton - CircleRadius) / (ButtonHideRadius - CircleRadius)) : 1.0;
            
            wordElement.TextBlock.Opacity = baseOpacity * MaxOpacity * hideOpacity;
            
            // 如果完全进入按钮范围，提前结束动画
            if (distanceToButton < CircleRadius)
            {
                break;
            }
            
            if (i < steps)
            {
                await Task.Delay(stepDelay);
            }
        }
    }

    private async Task CreateLinearAnimation(WordElement wordElement, TimeSpan duration)
    {
        const int steps = 360; // 60fps * 6秒 = 360步，确保60fps流畅度
        var stepDelay = (int)(duration.TotalMilliseconds / steps);
        
        var startPos = wordElement.StartPosition;
        var endPos = wordElement.TargetPosition;
        
        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var easedProgress = EaseInOutCubic(progress);
            
            var x = startPos.X + (endPos.X - startPos.X) * easedProgress;
            var y = startPos.Y + (endPos.Y - startPos.Y) * easedProgress;
            
            Canvas.SetLeft(wordElement.TextBlock, x);
            Canvas.SetTop(wordElement.TextBlock, y);
            
            // 检查是否进入按钮隐藏范围
            var distanceToButton = Math.Sqrt(Math.Pow(x - _circleCenterX, 2) + Math.Pow(y - _circleCenterY, 2));
            
            // 透明度变化 - 接近按钮时快速淡出
            var baseOpacity = Math.Sin(progress * Math.PI) * _globalOpacityMultiplier;
            var hideOpacity = distanceToButton < ButtonHideRadius ? 
                Math.Max(0, (distanceToButton - CircleRadius) / (ButtonHideRadius - CircleRadius)) : 1.0;
            
            wordElement.TextBlock.Opacity = baseOpacity * (MaxOpacity - progress * 0.2) * hideOpacity;
            
            // 如果完全进入按钮范围，提前结束动画
            if (distanceToButton < CircleRadius)
            {
                break;
            }
            
            if (i < steps)
            {
                await Task.Delay(stepDelay);
            }
        }
    }

    private void SpawnParticle()
    {
        if (_particleCanvas == null) return;

        var particle = CreateParticleElement();
        if (particle == null) return;

        _activeParticles.Add(particle);
        _particleCanvas.Children.Add(particle.Element);
    }

    private ParticleElement? CreateParticleElement()
    {
        var ellipse = GetOrCreateParticleEllipse();
        if (ellipse == null) return null;

        // 重新设置粒子属性
        ellipse.Width = 1 + _random.NextDouble() * 3;
        ellipse.Height = 1 + _random.NextDouble() * 3;
        ellipse.Fill = GetParticleColor();
        ellipse.Opacity = 0.3 + _random.NextDouble() * 0.3;

        var startPos = GetRandomParticleStartPosition();
        var velocity = GetRandomParticleVelocity(startPos);

        Canvas.SetLeft(ellipse, startPos.X);
        Canvas.SetTop(ellipse, startPos.Y);

        return new ParticleElement
        {
            Element = ellipse,
            Position = startPos,
            Velocity = velocity,
            CreationTime = DateTime.Now,
            LifeSpan = TimeSpan.FromSeconds(6 + _random.NextDouble() * 4) // 优化生命周期
        };
    }

    private Ellipse? GetOrCreateParticleEllipse()
    {
        if (_particlePool.Count > 0)
        {
            return _particlePool.Dequeue();
        }

        return new Ellipse();
    }

    private Point GetRandomParticleStartPosition()
    {
        // 粒子也从主界面边缘开始，但位置更分散，基于当前窗口大小
        var side = _random.Next(4);
        var edgeOffset = 30 + _random.NextDouble() * 50; // 粒子边缘偏移距离
        
        return side switch
        {
            0 => new Point(-edgeOffset, _random.NextDouble() * _currentHeight), // 左侧
            1 => new Point(_random.NextDouble() * _currentWidth, -edgeOffset), // 上方
            2 => new Point(_currentWidth + edgeOffset, _random.NextDouble() * _currentHeight), // 右侧
            3 => new Point(_random.NextDouble() * _currentWidth, _currentHeight + edgeOffset), // 下方
            _ => new Point(-50, _currentHeight / 2)
        };
    }

    private IBrush GetParticleColor()
    {
        var colors = new[] { "#40FFFFFF", "#404A9EFF", "#407B68EE", "#4020B2AA" };
        var color = colors[_random.Next(colors.Length)];
        return new SolidColorBrush(Color.Parse(color));
    }

    private Vector GetRandomParticleVelocity(Point startPos)
    {
        // 计算从起始位置到按钮中心的方向
        var dx = _circleCenterX - startPos.X;
        var dy = _circleCenterY - startPos.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        
        if (distance == 0) return new Vector(0, 0);
        
        // 标准化方向向量
        var normalizedDx = dx / distance;
        var normalizedDy = dy / distance;
        
        // 添加一些随机偏移
        var randomAngle = (_random.NextDouble() - 0.5) * Math.PI * 0.3; // ±27度偏移
        var cosOffset = Math.Cos(randomAngle);
        var sinOffset = Math.Sin(randomAngle);
        
        var finalDx = normalizedDx * cosOffset - normalizedDy * sinOffset;
        var finalDy = normalizedDx * sinOffset + normalizedDy * cosOffset;
        
        var speed = 0.4 + _random.NextDouble() * 0.6;
        
        return new Vector(finalDx * speed, finalDy * speed);
    }

    private void UpdateParticles()
    {
        var particlesToRemove = new List<ParticleElement>();
        
        foreach (var particle in _activeParticles.ToList())
        {
            // 更新位置
            particle.Position += particle.Velocity;
            Canvas.SetLeft(particle.Element, particle.Position.X);
            Canvas.SetTop(particle.Element, particle.Position.Y);
            
            // 检查生命周期
            var age = DateTime.Now - particle.CreationTime;
            if (age > particle.LifeSpan || IsOutsideCircle(particle.Position))
            {
                particlesToRemove.Add(particle);
            }
            else
            {
                // 更新透明度
                var lifeProgress = age.TotalMilliseconds / particle.LifeSpan.TotalMilliseconds;
                var opacity = (1 - lifeProgress) * 0.6 * _globalOpacityMultiplier;
                particle.Element.Opacity = Math.Max(0, opacity);
            }
        }
        
        // 移除过期粒子
        foreach (var particle in particlesToRemove)
        {
            RemoveParticle(particle);
        }
    }

    private bool IsOutsideCircle(Point position)
    {
        var distance = Math.Sqrt(Math.Pow(position.X - _circleCenterX, 2) + Math.Pow(position.Y - _circleCenterY, 2));
        // 粒子进入按钮隐藏范围时消失，或者移动到主界面外时消失
        var margin = 200; // 边界外的安全距离
        return distance < ButtonHideRadius || 
               position.X < -margin || position.X > _currentWidth + margin || 
               position.Y < -margin || position.Y > _currentHeight + margin;
    }

    private void RemoveWord(WordElement wordElement)
    {
        if (_wordCanvas?.Children.Contains(wordElement.TextBlock) == true)
        {
            _wordCanvas.Children.Remove(wordElement.TextBlock);
        }
        
        _activeWords.Remove(wordElement);
        
        // 重置TextBlock并放回池中
        wordElement.TextBlock.Opacity = 1.0;
        Canvas.SetLeft(wordElement.TextBlock, 0);
        Canvas.SetTop(wordElement.TextBlock, 0);
        _wordPool.Enqueue(wordElement.TextBlock);
    }

    private void RemoveParticle(ParticleElement particle)
    {
        if (_particleCanvas?.Children.Contains(particle.Element) == true)
        {
            _particleCanvas.Children.Remove(particle.Element);
        }
        
        _activeParticles.Remove(particle);
        
        // 重置粒子并放回池中
        particle.Element.Opacity = 1.0;
        Canvas.SetLeft(particle.Element, 0);
        Canvas.SetTop(particle.Element, 0);
        _particlePool.Enqueue(particle.Element);
    }

    private void ClearAllElements()
    {
        _spawnTimer.Stop();
        _particleTimer.Stop();
        
        foreach (var word in _activeWords.ToList())
        {
            RemoveWord(word);
        }
        
        foreach (var particle in _activeParticles.ToList())
        {
            RemoveParticle(particle);
        }
        
        _wordCanvas?.Children.Clear();
        _particleCanvas?.Children.Clear();
        _effectCanvas?.Children.Clear();
    }

    // 缓动函数
    private static double EaseInOutCubic(double t)
    {
        return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private static double EaseInOutSine(double t)
    {
        return -(Math.Cos(Math.PI * t) - 1) / 2;
    }

    // 公共接口方法
    public void SetButtonState(bool isHovered, bool isPressed)
    {
        _isButtonHovered = isHovered;
        _isButtonPressed = isPressed;
        
        // 根据按钮状态调整全局透明度，增强响应性
        if (isPressed)
        {
            _globalOpacityMultiplier = 0.2; // 点击时词云几乎消失
        }
        else if (isHovered)
        {
            _globalOpacityMultiplier = 0.4; // 悬停时词云淡化
        }
        else
        {
            _globalOpacityMultiplier = 1.0; // 正常状态
        }
        
        // 立即更新所有元素的透明度，创建平滑过渡
        UpdateElementsOpacity();
    }

    private void UpdateElementsOpacity()
    {
        foreach (var word in _activeWords)
        {
            var currentOpacity = word.TextBlock.Opacity;
            if (currentOpacity > 0)
            {
                word.TextBlock.Opacity = currentOpacity * _globalOpacityMultiplier;
            }
        }
        
        foreach (var particle in _activeParticles)
        {
            var currentOpacity = particle.Element.Opacity;
            if (currentOpacity > 0)
            {
                particle.Element.Opacity = currentOpacity * _globalOpacityMultiplier;
            }
        }
    }

    public void Start()
    {
        _spawnTimer.Start();
        _particleTimer.Start();
    }

    public void Stop()
    {
        _spawnTimer.Stop();
        _particleTimer.Stop();
    }

    public void SetSpawnInterval(int intervalMs)
    {
        _spawnTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
    }
}

// 辅助类
public class WordElement
{
    public TextBlock TextBlock { get; set; } = null!;
    public Point StartPosition { get; set; }
    public Point TargetPosition { get; set; }
    public DateTime CreationTime { get; set; }
    public WordAnimationType AnimationType { get; set; }
}

public class ParticleElement
{
    public Ellipse Element { get; set; } = null!;
    public Point Position { get; set; }
    public Vector Velocity { get; set; }
    public DateTime CreationTime { get; set; }
    public TimeSpan LifeSpan { get; set; }
}

public enum WordAnimationType
{
    Linear,
    Spiral,
    Wave,
    Orbit
}
