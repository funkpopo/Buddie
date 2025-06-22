using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Lyxie_desktop.Helpers;

/// <summary>
/// 消息预处理工具类，用于处理think标签和其他特殊格式
/// </summary>
public static class MessageProcessor
{
    // 匹配 <think>...</think> 标签的正则表达式
    private static readonly Regex ThinkTagRegex = new Regex(
        @"<think>(.*?)</think>", 
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    /// 处理消息中的think标签
    /// </summary>
    /// <param name="message">原始消息</param>
    /// <returns>处理结果，包含think内容列表和清理后的消息</returns>
    public static MessageProcessResult ProcessThinkTags(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return new MessageProcessResult
            {
                CleanedMessage = message ?? "",
                ThinkBlocks = new List<string>()
            };
        }

        var thinkBlocks = new List<string>();
        var cleanedMessage = message;

        // 查找所有think标签
        var matches = ThinkTagRegex.Matches(message);
        
        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count > 1)
            {
                // 提取think内容
                var thinkContent = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(thinkContent))
                {
                    thinkBlocks.Add(thinkContent);
                }
            }
        }

        // 从消息中移除所有think标签
        cleanedMessage = ThinkTagRegex.Replace(cleanedMessage, "").Trim();

        // 清理多余的空行
        cleanedMessage = CleanExtraWhitespace(cleanedMessage);

        return new MessageProcessResult
        {
            CleanedMessage = cleanedMessage,
            ThinkBlocks = thinkBlocks
        };
    }

    /// <summary>
    /// 清理文本中的多余空白字符
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <returns>清理后的文本</returns>
    private static string CleanExtraWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // 将多个连续的换行符替换为最多两个换行符
        text = Regex.Replace(text, @"\n\s*\n\s*\n+", "\n\n", RegexOptions.Multiline);
        
        // 移除行首行尾的空白字符
        text = Regex.Replace(text, @"^\s+|\s+$", "", RegexOptions.Multiline);
        
        // 移除文本开头和结尾的空白字符
        text = text.Trim();

        return text;
    }

    /// <summary>
    /// 检查消息是否包含think标签
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <returns>是否包含think标签</returns>
    public static bool HasThinkTags(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        return ThinkTagRegex.IsMatch(message);
    }

    /// <summary>
    /// 提取think内容的预览文本
    /// </summary>
    /// <param name="thinkContent">think内容</param>
    /// <param name="maxLength">最大长度</param>
    /// <returns>预览文本</returns>
    public static string GetThinkPreview(string thinkContent, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(thinkContent))
            return "";

        var preview = thinkContent.Trim();
        
        // 移除换行符和多余空白，用单个空格替换
        preview = Regex.Replace(preview, @"\s+", " ");
        
        // 移除markdown标记符号
        preview = Regex.Replace(preview, @"[#*`_\[\]()]", "");
        
        if (preview.Length > maxLength)
        {
            // 尝试在单词边界处截断
            var truncated = preview.Substring(0, maxLength);
            var lastSpace = truncated.LastIndexOf(' ');
            if (lastSpace > maxLength * 0.7) // 如果空格位置合理
            {
                preview = truncated.Substring(0, lastSpace).Trim() + "...";
            }
            else
            {
                preview = truncated.Trim() + "...";
            }
        }

        return preview;
    }

    /// <summary>
    /// 验证think标签是否格式正确
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <returns>验证结果</returns>
    public static ThinkTagValidationResult ValidateThinkTags(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return new ThinkTagValidationResult { IsValid = true, Message = "消息为空" };
        }

        // 检查是否有未闭合的think标签
        var openTags = Regex.Matches(message, @"<think>", RegexOptions.IgnoreCase).Count;
        var closeTags = Regex.Matches(message, @"</think>", RegexOptions.IgnoreCase).Count;

        if (openTags != closeTags)
        {
            return new ThinkTagValidationResult 
            { 
                IsValid = false, 
                Message = $"think标签不匹配：开始标签{openTags}个，结束标签{closeTags}个" 
            };
        }

        // 检查是否有嵌套的think标签
        var content = message;
        var nestingLevel = 0;
        var maxNesting = 0;

        for (int i = 0; i < content.Length - 6; i++)
        {
            if (content.Substring(i, 7).Equals("<think>", StringComparison.OrdinalIgnoreCase))
            {
                nestingLevel++;
                maxNesting = Math.Max(maxNesting, nestingLevel);
                i += 6; // 跳过标签
            }
            else if (i < content.Length - 7 && content.Substring(i, 8).Equals("</think>", StringComparison.OrdinalIgnoreCase))
            {
                nestingLevel--;
                i += 7; // 跳过标签
            }
        }

        if (maxNesting > 1)
        {
            return new ThinkTagValidationResult 
            { 
                IsValid = false, 
                Message = $"检测到嵌套的think标签（最大嵌套层级：{maxNesting}）" 
            };
        }

        return new ThinkTagValidationResult { IsValid = true, Message = "think标签格式正确" };
    }

    /// <summary>
    /// 将思考内容分割为段落列表
    /// </summary>
    /// <param name="thinkContent">思考内容</param>
    /// <returns>段落列表</returns>
    public static List<string> SplitThinkContentToParagraphs(string thinkContent)
    {
        if (string.IsNullOrEmpty(thinkContent))
            return new List<string>();

        var paragraphs = new List<string>();
        
        // 按双换行符分割段落
        var rawParagraphs = thinkContent.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var paragraph in rawParagraphs)
        {
            var cleanParagraph = paragraph.Trim();
            if (!string.IsNullOrEmpty(cleanParagraph))
            {
                // 将单个换行符替换为空格，保持段落内容的连续性
                cleanParagraph = Regex.Replace(cleanParagraph, @"(?<!\n)\n(?!\n)", " ");
                // 清理多余的空格
                cleanParagraph = Regex.Replace(cleanParagraph, @"\s+", " ");
                paragraphs.Add(cleanParagraph);
            }
        }
        
        // 如果没有双换行符分割，则按单换行符分割
        if (paragraphs.Count <= 1 && !string.IsNullOrEmpty(thinkContent.Trim()))
        {
            paragraphs.Clear();
            var lines = thinkContent.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (!string.IsNullOrEmpty(cleanLine))
                {
                    paragraphs.Add(cleanLine);
                }
            }
        }
        
        return paragraphs;
    }

    /// <summary>
    /// 格式化思考内容段落
    /// </summary>
    /// <param name="paragraph">段落内容</param>
    /// <returns>格式化后的段落</returns>
    public static string FormatThinkParagraph(string paragraph)
    {
        if (string.IsNullOrEmpty(paragraph))
            return paragraph;

        var formatted = paragraph.Trim();
        
        // 移除多余的markdown标记（保留基本格式）
        // 保留**bold**和*italic*，但移除其他复杂标记
        formatted = Regex.Replace(formatted, @"```[\s\S]*?```", ""); // 移除代码块
        formatted = Regex.Replace(formatted, @"`([^`]+)`", "$1"); // 移除行内代码标记
        formatted = Regex.Replace(formatted, @"#{1,6}\s*", ""); // 移除标题标记
        
        // 清理多余空格
        formatted = Regex.Replace(formatted, @"\s+", " ");
        
        return formatted.Trim();
    }
}

/// <summary>
/// 消息处理结果
/// </summary>
public class MessageProcessResult
{
    /// <summary>
    /// 清理后的消息内容（移除think标签）
    /// </summary>
    public string CleanedMessage { get; set; } = "";

    /// <summary>
    /// 提取的think内容列表
    /// </summary>
    public List<string> ThinkBlocks { get; set; } = new List<string>();

    /// <summary>
    /// 是否包含think内容
    /// </summary>
    public bool HasThinkContent => ThinkBlocks.Count > 0;
}

/// <summary>
/// think标签验证结果
/// </summary>
public class ThinkTagValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 验证消息
    /// </summary>
    public string Message { get; set; } = "";
} 