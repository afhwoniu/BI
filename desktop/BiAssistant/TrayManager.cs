using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BiAssistant;

/// <summary>
/// 系统托盘管理器
/// </summary>
public class TrayManager : IDisposable
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private Icon? _trayIconHandle;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? ToggleBallRequested;

    public TrayManager()
    {
        // 创建托盘图标
        _trayIconHandle = CreateTrayIcon();
        _trayIcon = new NotifyIcon
        {
            Icon = _trayIconHandle,
            Text = "BI 智能助手",
            Visible = true
        };

        // 创建右键菜单
        _contextMenu = new ContextMenuStrip();
        
        // 菜单项样式
        _contextMenu.BackColor = Color.FromArgb(30, 38, 55);
        _contextMenu.ForeColor = Color.FromArgb(200, 210, 230);
        _contextMenu.Font = new Font("Microsoft YaHei UI", 9);
        _contextMenu.Renderer = new DarkMenuRenderer();
        
        // 添加菜单项
        var showItem = new ToolStripMenuItem("🤖 打开智能助手");
        showItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        
        var toggleBallItem = new ToolStripMenuItem("⚫ 显示/隐藏悬浮球");
        toggleBallItem.Click += (s, e) => ToggleBallRequested?.Invoke(this, EventArgs.Empty);
        
        var separator = new ToolStripSeparator();
        
        var exitItem = new ToolStripMenuItem("❌ 退出");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        
        _contextMenu.Items.AddRange(new ToolStripItem[] { showItem, toggleBallItem, separator, exitItem });
        
        _trayIcon.ContextMenuStrip = _contextMenu;
        _trayIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 创建托盘图标（代码生成，无需外部资源）
    /// </summary>
    private static Icon CreateTrayIcon()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        
        // 背景圆
        using var bgBrush = new LinearGradientBrush(
            new Rectangle(0, 0, size, size),
            Color.FromArgb(64, 158, 255),
            Color.FromArgb(30, 100, 200),
            45f);
        g.FillEllipse(bgBrush, 2, 2, size - 4, size - 4);
        
        // AI文字
        using var font = new Font("Segoe UI", 10, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var text = "AI";
        var textSize = g.MeasureString(text, font);
        g.DrawString(text, font, textBrush, 
            (size - textSize.Width) / 2 + 1, 
            (size - textSize.Height) / 2 + 1);
        
        // 转换为Icon - 使用Clone避免内存泄漏
        var hIcon = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);  // 释放原始句柄
        return icon;
    }

    /// <summary>
    /// 显示气泡提示
    /// </summary>
    public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _trayIcon.ShowBalloonTip(3000, title, text, icon);
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIconHandle?.Dispose();
        _contextMenu.Dispose();
    }
}

/// <summary>
/// 深色菜单渲染器
/// </summary>
public class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected)
        {
            using var brush = new SolidBrush(Color.FromArgb(50, 100, 150));
            e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
        }
        else
        {
            base.OnRenderMenuItemBackground(e);
        }
    }
}

/// <summary>
/// 深色配色表
/// </summary>
public class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(50, 60, 80);
    public override Color MenuItemBorder => Color.FromArgb(64, 158, 255);
    public override Color MenuItemSelected => Color.FromArgb(50, 100, 150);
    public override Color MenuStripGradientBegin => Color.FromArgb(30, 38, 55);
    public override Color MenuStripGradientEnd => Color.FromArgb(30, 38, 55);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(50, 100, 150);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(50, 100, 150);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(40, 80, 120);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(40, 80, 120);
    public override Color ImageMarginGradientBegin => Color.FromArgb(30, 38, 55);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 38, 55);
    public override Color ImageMarginGradientEnd => Color.FromArgb(30, 38, 55);
    public override Color SeparatorDark => Color.FromArgb(50, 60, 80);
    public override Color SeparatorLight => Color.FromArgb(50, 60, 80);
    public override Color ToolStripDropDownBackground => Color.FromArgb(30, 38, 55);
}

