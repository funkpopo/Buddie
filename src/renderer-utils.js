/**
 * 渲染工具模块 - 支持 Markdown、LaTeX、Mermaid 渲染
 * 使用 markdown-it 替代 marked
 */

let MarkdownIt, hljs, katex, mermaid;

try {
  MarkdownIt = window.require ? window.require('markdown-it') : require('markdown-it');
  hljs = window.require ? window.require('highlight.js') : require('highlight.js');
  katex = window.require ? window.require('katex') : require('katex');
  mermaid = window.require ? window.require('mermaid') : require('mermaid');
} catch (error) {
  console.error('Failed to load rendering dependencies:', error);
  // 如果无法加载依赖，设置为null，后续代码会处理
  MarkdownIt = null;
  hljs = null;
  katex = null;
  mermaid = null;
}

class ContentRenderer {
  constructor() {
    this.dependenciesLoaded = MarkdownIt && katex && mermaid;
    this.initialized = false;
    this.md = null;
    this.mermaid = mermaid; // 添加mermaid引用
    if (this.dependenciesLoaded) {
      this.initializeMarkdownIt();
      this.initializeMermaid();
      this.initialized = true;
    } else {
      console.warn('Some rendering dependencies not loaded, fallback mode enabled');
    }
  }

  // 初始化 markdown-it
  initializeMarkdownIt() {
    if (!MarkdownIt) return;
    
    // 创建 markdown-it 实例
    this.md = new MarkdownIt({
      html: true,        // 启用HTML标签
      xhtmlOut: false,   // 使用HTML样式的闭合标签
      breaks: true,      // 换行符转换为<br>
      langPrefix: 'language-',  // CSS语言前缀
      linkify: true,     // 自动识别链接
      typographer: true, // 启用排版替换
      quotes: '""\'\''   // 智能引号
    });

    // 配置代码高亮
    if (hljs) {
      this.md.set({
        highlight: function (str, lang) {
          if (lang && hljs.getLanguage(lang)) {
            try {
              return '<pre class="hljs"><code class="language-' + lang + '">' +
                     hljs.highlight(str, { language: lang, ignoreIllegals: true }).value +
                     '</code></pre>';
            } catch (__) {}
          }
          
          return '<pre class="hljs"><code>' + this.md.utils.escapeHtml(str) + '</code></pre>';
        }.bind(this)
      });
    }

    // 自定义表格渲染
    const defaultTableOpen = this.md.renderer.rules.table_open || function(tokens, idx, options, env) {
      return '<table>';
    };
    
    this.md.renderer.rules.table_open = function(tokens, idx, options, env) {
      return '<div class="table-container"><table class="markdown-table">';
    };

    const defaultTableClose = this.md.renderer.rules.table_close || function(tokens, idx, options, env) {
      return '</table>';
    };
    
    this.md.renderer.rules.table_close = function(tokens, idx, options, env) {
      return '</table></div>';
    };
  }

  // 初始化 Mermaid
  initializeMermaid() {
    if (!mermaid || typeof window === 'undefined') return;
    
    mermaid.initialize({
      startOnLoad: false,
      theme: 'default',
      securityLevel: 'loose',
      fontFamily: 'inherit',
      fontSize: '14px'
    });
  }

  // HTML 转义
  escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  // 处理 LaTeX 数学公式
  renderMath(content) {
    if (!katex) return content;
    
    // 处理块级数学公式 $$...$$
    content = content.replace(/\$\$([^$]+?)\$\$/g, (match, formula) => {
      try {
        return katex.renderToString(formula.trim(), {
          displayMode: true,
          throwOnError: false,
          trust: false
        });
      } catch (error) {
        console.warn('LaTeX rendering error (block):', error);
        return `<div class="math-error">数学公式渲染错误: ${this.escapeHtml(formula)}</div>`;
      }
    });

    // 处理内联数学公式 $...$
    content = content.replace(/\$([^$\n]+?)\$/g, (match, formula) => {
      try {
        return katex.renderToString(formula.trim(), {
          displayMode: false,
          throwOnError: false,
          trust: false
        });
      } catch (error) {
        console.warn('LaTeX rendering error (inline):', error);
        return `<span class="math-error">\$${this.escapeHtml(formula)}\$</span>`;
      }
    });

    return content;
  }

  // 处理 Mermaid 图表
  async renderMermaid(content) {
    // 匹配 mermaid 代码块
    const mermaidRegex = /```mermaid\n([\s\S]*?)\n```/g;
    const promises = [];
    const placeholders = [];

    let match;
    let index = 0;

    while ((match = mermaidRegex.exec(content)) !== null) {
      const mermaidCode = match[1].trim();
      const placeholder = `MERMAID_PLACEHOLDER_${index}`;
      placeholders.push(placeholder);

      const promise = this.renderSingleMermaid(mermaidCode, `mermaid-${Date.now()}-${index}`);
      promises.push(promise);
      
      content = content.replace(match[0], placeholder);
      index++;
    }

    // 等待所有 mermaid 图表渲染完成
    if (promises.length > 0) {
      try {
        const renderedMermaidDivs = await Promise.all(promises);
        
        // 替换占位符
        placeholders.forEach((placeholder, i) => {
          content = content.replace(placeholder, renderedMermaidDivs[i]);
        });
      } catch (error) {
        console.error('Mermaid rendering error:', error);
        // 如果渲染失败，恢复原始代码块
        placeholders.forEach((placeholder, i) => {
          content = content.replace(placeholder, `<pre><code class="language-mermaid">渲染失败</code></pre>`);
        });
      }
    }

    return content;
  }

  // 渲染单个 Mermaid 图表
  async renderSingleMermaid(mermaidCode, elementId) {
    try {
      if (typeof window !== 'undefined' && mermaid) {
        // 创建一个临时 div 用于渲染
        const tempDiv = document.createElement('div');
        tempDiv.style.display = 'none';
        document.body.appendChild(tempDiv);

        const { svg } = await mermaid.render(elementId, mermaidCode);
        
        // 清理临时元素
        document.body.removeChild(tempDiv);
        
        return `<div class="mermaid-container">${svg}</div>`;
      } else {
        return `<pre><code class="language-mermaid">${this.escapeHtml(mermaidCode)}</code></pre>`;
      }
    } catch (error) {
      console.warn('Single mermaid rendering error:', error);
      return `<div class="mermaid-error">
        <p>图表渲染错误</p>
        <pre><code>${this.escapeHtml(mermaidCode)}</code></pre>
      </div>`;
    }
  }

  // 主渲染函数
  async render(content) {
    try {
      if (!this.dependenciesLoaded) {
        // 依赖项未加载，返回简单的HTML转义和换行处理
        return content
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;')
          .replace(/'/g, '&#x27;')
          .replace(/\n/g, '<br>');
      }
      
      // 1. 首先处理 Mermaid（在 markdown 处理之前）
      content = await this.renderMermaid(content);
      
      // 2. 处理 Markdown (使用 markdown-it)
      if (this.md) {
        content = this.md.render(content);
      }
      
      // 3. 处理 LaTeX 数学公式
      content = this.renderMath(content);
      
      return content;
    } catch (error) {
      console.error('Content rendering error:', error);
      return `<div class="render-error">
        <p>内容渲染失败</p>
        <pre>${this.escapeHtml(content)}</pre>
      </div>`;
    }
  }

  // 获取必要的 CSS 样式
  getStyles() {
    return `
      /* Markdown 基础样式 */
      .markdown-content {
        line-height: 1.6;
        color: #333;
      }
      
      .markdown-content h1, .markdown-content h2, .markdown-content h3,
      .markdown-content h4, .markdown-content h5, .markdown-content h6 {
        margin: 16px 0 8px 0;
        font-weight: 600;
        line-height: 1.25;
      }
      
      .markdown-content h1 { font-size: 1.4em; border-bottom: 1px solid #eee; padding-bottom: 4px; }
      .markdown-content h2 { font-size: 1.3em; }
      .markdown-content h3 { font-size: 1.2em; }
      .markdown-content h4 { font-size: 1.1em; }
      .markdown-content h5 { font-size: 1.05em; }
      .markdown-content h6 { font-size: 1em; color: #666; }
      
      .markdown-content p {
        margin: 8px 0;
      }
      
      .markdown-content ul, .markdown-content ol {
        margin: 8px 0;
        padding-left: 20px;
      }
      
      .markdown-content li {
        margin: 4px 0;
      }
      
      .markdown-content blockquote {
        margin: 12px 0;
        padding: 8px 16px;
        border-left: 4px solid #ddd;
        background: #f9f9f9;
        color: #666;
      }
      
      .markdown-content code {
        background: #f1f3f4;
        padding: 2px 4px;
        border-radius: 3px;
        font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
        font-size: 0.9em;
      }
      
      .markdown-content pre {
        background: #f8f9fa;
        border: 1px solid #e1e4e8;
        border-radius: 6px;
        padding: 12px;
        margin: 12px 0;
        overflow-x: auto;
        line-height: 1.4;
      }
      
      .markdown-content pre code {
        background: none;
        padding: 0;
        border-radius: 0;
        font-size: 0.85em;
      }
      
      /* 表格样式 */
      .table-container {
        overflow-x: auto;
        margin: 12px 0;
      }
      
      .markdown-table {
        border-collapse: collapse;
        width: 100%;
        font-size: 0.9em;
      }
      
      .markdown-table th,
      .markdown-table td {
        border: 1px solid #ddd;
        padding: 8px 12px;
        text-align: left;
      }
      
      .markdown-table th {
        background: #f1f3f4;
        font-weight: 600;
      }
      
      .markdown-table tr:nth-child(even) {
        background: #f9f9f9;
      }
      
      /* KaTeX 数学公式样式 */
      .katex {
        font-size: 1em;
      }
      
      .katex-display {
        margin: 12px 0;
        text-align: center;
      }
      
      /* Mermaid 图表样式 */
      .mermaid-container {
        margin: 16px 0;
        text-align: center;
        background: #fff;
        border-radius: 6px;
        padding: 12px;
        border: 1px solid #e1e4e8;
      }
      
      .mermaid-container svg {
        max-width: 100%;
        height: auto;
      }
      
      /* 错误样式 */
      .math-error,
      .mermaid-error,
      .render-error {
        background: #fff3cd;
        border: 1px solid #ffeeba;
        color: #856404;
        padding: 8px 12px;
        border-radius: 4px;
        margin: 8px 0;
        font-size: 0.9em;
      }
      
      .math-error {
        display: inline;
        padding: 2px 6px;
      }
      
      /* 链接样式 */
      .markdown-content a {
        color: #007bff;
        text-decoration: none;
      }
      
      .markdown-content a:hover {
        text-decoration: underline;
      }
      
      /* 强调样式 */
      .markdown-content strong {
        font-weight: 600;
      }
      
      .markdown-content em {
        font-style: italic;
      }
      
      /* 水平线样式 */
      .markdown-content hr {
        border: none;
        border-top: 1px solid #eee;
        margin: 16px 0;
      }
    `;
  }
}

// 创建全局实例
const contentRenderer = new ContentRenderer();

// 导出渲染函数
window.renderContent = async (content) => {
  return await contentRenderer.render(content);
};

// 导出样式
window.getRendererStyles = () => {
  return contentRenderer.getStyles();
};

module.exports = { ContentRenderer };
