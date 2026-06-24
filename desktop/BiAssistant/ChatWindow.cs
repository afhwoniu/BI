using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BiAssistant;

/// <summary>
/// 智能分析聊天窗口
/// 使用 WebView2 加载前端智能分析页面
/// </summary>
public class ChatWindow : Form
{
    private readonly WebView2 _webView;
    private readonly Panel _titleBar;
    private readonly Label _titleLabel;
    private readonly Button _refreshBtn;  // 刷新按钮
    private readonly Button _closeBtn;
    private bool _isDragging = false;
    private Point _dragStart;
    private bool _webViewReady = false;

    // 配置 - 前端端口与 vite.config.ts 中 server.port 一致
    private const string BASE_URL = "http://localhost:5180";
    private const string AI_ANALYSIS_PATH = "/desktop/ai";

    // 主题颜色
    private readonly Color _titleBarColor = Color.FromArgb(41, 121, 255);  // 医疗蓝标题栏
    private readonly Color _textColor = Color.White;

    public ChatWindow()
    {
        // 窗口基本设置 - 占屏幕80%
        FormBorderStyle = FormBorderStyle.None;
        var screenBounds = Screen.PrimaryScreen!.WorkingArea;
        Size = new Size((int)(screenBounds.Width * 0.8), (int)(screenBounds.Height * 0.8));
        StartPosition = FormStartPosition.CenterScreen;  // 屏幕中心
        BackColor = Color.White;
        ShowInTaskbar = false;
        TopMost = true;  // 始终置顶

        // 圆角效果
        Region = CreateRoundedRegion(ClientRectangle, 12);

        // 自定义标题栏
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = _titleBarColor,
            Padding = new Padding(12, 0, 8, 0)
        };

        // 标题
        _titleLabel = new Label
        {
            Text = "🤖 BI 智能助手",
            ForeColor = _textColor,
            Font = new Font("Microsoft YaHei UI", 11, FontStyle.Regular),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };

        // 关闭按钮
        _closeBtn = new Button
        {
            Text = "✕",
            Size = new Size(40, 40),
            FlatStyle = FlatStyle.Flat,
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 12),
            Cursor = Cursors.Hand,
            Dock = DockStyle.Right
        };
        _closeBtn.FlatAppearance.BorderSize = 0;
        _closeBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 80, 80);
        _closeBtn.Click += (s, e) => Hide();

        // 刷新按钮
        _refreshBtn = new Button
        {
            Text = "🔄",
            Size = new Size(40, 40),
            FlatStyle = FlatStyle.Flat,
            ForeColor = _textColor,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 11),
            Cursor = Cursors.Hand,
            Dock = DockStyle.Right
        };
        _refreshBtn.FlatAppearance.BorderSize = 0;
        _refreshBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 140, 255);  // 悬停时稍亮的蓝色
        _refreshBtn.Click += (s, e) => RefreshPage();

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_closeBtn);
        _titleBar.Controls.Add(_refreshBtn);  // 刷新按钮在关闭按钮左边

        // 标题栏拖动
        _titleBar.MouseDown += (s, e) => { _isDragging = true; _dragStart = e.Location; };
        _titleBar.MouseMove += (s, e) => { if (_isDragging) Location = new Point(Left + e.X - _dragStart.X, Top + e.Y - _dragStart.Y); };
        _titleBar.MouseUp += (s, e) => _isDragging = false;
        _titleLabel.MouseDown += (s, e) => { _isDragging = true; _dragStart = e.Location; };
        _titleLabel.MouseMove += (s, e) => { if (_isDragging) Location = new Point(Left + e.X - _dragStart.X, Top + e.Y - _dragStart.Y); };
        _titleLabel.MouseUp += (s, e) => _isDragging = false;

        // WebView2 控件 - 设置白色背景
        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.White
        };

        // 添加控件（顺序重要：先添加的在底层）
        Controls.Add(_webView);
        Controls.Add(_titleBar);
    }

    /// <summary>
    /// 窗口首次显示时初始化 WebView2
    /// </summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!_webViewReady)
        {
            InitializeWebView();
        }
    }

    private async void InitializeWebView()
    {
        try
        {
            // 确保 WebView2 运行时已初始化
            await _webView.EnsureCoreWebView2Async(null);

            // 设置 WebView2
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;

            _webViewReady = true;

            // 导航到智能分析页面
            _webView.CoreWebView2.Navigate($"{BASE_URL}{AI_ANALYSIS_PATH}");
        }
        catch (Exception ex)
        {
            // 如果 WebView2 初始化失败，显示错误
            var errorLabel = new Label
            {
                Text = $"加载失败：{ex.Message}\n\n请确保：\n1. 系统服务已启动 (start.bat)\n2. 已安装 WebView2 运行时",
                ForeColor = Color.FromArgb(120, 120, 120),
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 10)
            };
            Controls.Clear();
            Controls.Add(_titleBar);
            Controls.Add(errorLabel);
        }
    }

    private static Region CreateRoundedRegion(Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        var oldRegion = Region;
        Region = CreateRoundedRegion(ClientRectangle, 12);
        oldRegion?.Dispose();
    }

    /// <summary>
    /// 刷新页面
    /// </summary>
    private void RefreshPage()
    {
        if (_webViewReady && _webView.CoreWebView2 != null)
        {
            _webView.CoreWebView2.Reload();
        }
    }

    /// <summary>
    /// 切换显示/隐藏，并将窗口居中显示
    /// </summary>
    public void Toggle()
    {
        if (Visible && WindowState != FormWindowState.Minimized)
        {
            Hide();
        }
        else
        {
            // 如果窗口最小化了，先恢复
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            // 每次显示时居中
            CenterToScreen();

            Show();
            Activate();
            BringToFront();
        }
    }

    /// <summary>
    /// 窗口失去焦点时自动隐藏（点击外部区域关闭）
    /// </summary>
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // 当窗口失去焦点时隐藏（点击外部区域触发）
        Hide();
    }

    /// <summary>
    /// 防止窗口被真正关闭，只是隐藏
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnFormClosing(e);
        }
    }

}

