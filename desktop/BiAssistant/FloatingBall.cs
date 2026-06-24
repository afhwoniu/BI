using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BiAssistant;

/// <summary>
/// 科技感悬浮球窗口 - 医疗蓝主题 + 波浪涌动效果
/// </summary>
public class FloatingBall : Form
{
    private readonly System.Windows.Forms.Timer _animTimer;
    private float _wavePhase = 0f;           // 波浪相位
    private float _glowIntensity = 0.6f;     // 光晕强度
    private float _glowDirection = 0.015f;   // 光晕变化方向
    private bool _isHovering = false;
    private bool _isDragging = false;
    private Point _dragStart;

    // 医疗蓝主题色
    private readonly Color _primaryBlue = Color.FromArgb(41, 121, 255);       // 主蓝色
    private readonly Color _lightBlue = Color.FromArgb(100, 181, 246);        // 浅蓝
    private readonly Color _darkBlue = Color.FromArgb(21, 101, 192);          // 深蓝
    private readonly Color _glowBlue = Color.FromArgb(33, 150, 243);          // 发光蓝
    private readonly Color _waveBlue = Color.FromArgb(144, 202, 249);         // 波浪蓝（更浅）

    public event EventHandler? BallClicked;

    public FloatingBall()
    {
        // 窗口设置 - 更大的悬浮球
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(100, 100);  // 增大尺寸
        StartPosition = FormStartPosition.Manual;

        // 放置在屏幕右下角
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - 120, screen.Bottom - 180);

        // 透明背景 - 使用深灰色作为透明色避免与设计冲突
        BackColor = Color.FromArgb(1, 1, 1);
        TransparencyKey = Color.FromArgb(1, 1, 1);

        // 双缓冲防闪烁
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.DoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);

        // 波浪动画定时器 - 控制波浪涌动
        _animTimer = new System.Windows.Forms.Timer { Interval = 40 };
        _animTimer.Tick += (s, e) =>
        {
            // 波浪相位推进
            _wavePhase += 0.15f;
            if (_wavePhase >= Math.PI * 2) _wavePhase = 0;

            // 光晕呼吸
            _glowIntensity += _glowDirection;
            if (_glowIntensity >= 1.0f || _glowIntensity <= 0.4f)
                _glowDirection = -_glowDirection;

            Invalidate();
        };
        _animTimer.Start();

        // 鼠标事件
        MouseEnter += (s, e) => { _isHovering = true; Cursor = Cursors.Hand; };
        MouseLeave += (s, e) => { _isHovering = false; Cursor = Cursors.Default; };
        MouseDown += OnMouseDownHandler;
        MouseMove += OnMouseMoveHandler;
        MouseUp += OnMouseUpHandler;
        MouseClick += (s, e) => { if (!_isDragging) BallClicked?.Invoke(this, EventArgs.Empty); };
    }

    private void OnMouseDownHandler(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = false;
            _dragStart = e.Location;
        }
    }

    private void OnMouseMoveHandler(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var moved = Math.Abs(e.X - _dragStart.X) > 5 || Math.Abs(e.Y - _dragStart.Y) > 5;
            if (moved)
            {
                _isDragging = true;
                Location = new Point(Left + e.X - _dragStart.X, Top + e.Y - _dragStart.Y);
            }
        }
    }

    private void OnMouseUpHandler(object? sender, MouseEventArgs e)
    {
        // 吸附到屏幕边缘
        if (_isDragging)
        {
            var screen = Screen.FromPoint(Location).WorkingArea;
            int x = Left, y = Top;
            
            // 水平吸附
            if (Left < screen.Left + 20) x = screen.Left + 5;
            else if (Right > screen.Right - 20) x = screen.Right - Width - 5;
            
            // 垂直吸附
            if (Top < screen.Top + 20) y = screen.Top + 5;
            else if (Bottom > screen.Bottom - 20) y = screen.Bottom - Height - 5;
            
            Location = new Point(x, y);
        }
        _isDragging = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;

        int cx = Width / 2;
        int cy = Height / 2;
        int ballSize = 60;  // 主球体大小

        // === 第1层：外围波浪涌动效果 ===
        DrawWaveRings(g, cx, cy, ballSize);

        // === 第2层：外层光晕 ===
        int glowRadius = ballSize / 2 + (int)(8 * _glowIntensity);
        using (var glowBrush = new SolidBrush(Color.FromArgb((int)(40 * _glowIntensity), _glowBlue)))
        {
            g.FillEllipse(glowBrush, cx - glowRadius - 5, cy - glowRadius - 5,
                         (glowRadius + 5) * 2, (glowRadius + 5) * 2);
        }

        // === 第3层：悬停时增强光晕 ===
        if (_isHovering)
        {
            using var hoverBrush = new SolidBrush(Color.FromArgb(50, _lightBlue));
            g.FillEllipse(hoverBrush, cx - glowRadius - 8, cy - glowRadius - 8,
                         (glowRadius + 8) * 2, (glowRadius + 8) * 2);
        }

        // === 第4层：主球体（医疗蓝渐变）===
        int halfBall = ballSize / 2;
        using (var ballBrush = new LinearGradientBrush(
            new Rectangle(cx - halfBall, cy - halfBall, ballSize, ballSize),
            _lightBlue,    // 左上浅蓝
            _darkBlue,     // 右下深蓝
            LinearGradientMode.ForwardDiagonal))
        {
            g.FillEllipse(ballBrush, cx - halfBall, cy - halfBall, ballSize, ballSize);
        }

        // === 第5层：高光效果 ===
        int highlightSize = ballSize / 3;
        using (var highlightBrush = new LinearGradientBrush(
            new Rectangle(cx - halfBall + 8, cy - halfBall + 6, highlightSize, highlightSize),
            Color.FromArgb(120, 255, 255, 255),
            Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.ForwardDiagonal))
        {
            g.FillEllipse(highlightBrush, cx - halfBall + 10, cy - halfBall + 8, highlightSize, highlightSize / 2);
        }

        // === 第6层：中心AI图标 ===
        DrawAIIcon(g, cx, cy);
    }

    /// <summary>
    /// 绘制波浪涌动圆环
    /// </summary>
    private void DrawWaveRings(Graphics g, int cx, int cy, int ballSize)
    {
        // 绘制3层波浪圆环，每层相位错开
        for (int ring = 0; ring < 3; ring++)
        {
            float phase = _wavePhase + ring * (float)(Math.PI * 2 / 3);
            float waveOffset = (float)Math.Sin(phase) * 3;  // 波浪偏移
            int ringRadius = ballSize / 2 + 12 + ring * 6 + (int)waveOffset;

            // 透明度随波浪变化
            int alpha = (int)(30 + 20 * Math.Sin(phase + Math.PI / 2));
            if (_isHovering) alpha += 20;

            using (var ringPen = new Pen(Color.FromArgb(alpha, _waveBlue), 2))
            {
                g.DrawEllipse(ringPen, cx - ringRadius, cy - ringRadius, ringRadius * 2, ringRadius * 2);
            }
        }
    }

    /// <summary>
    /// 绘制中心AI文字
    /// </summary>
    private void DrawAIIcon(Graphics g, int cx, int cy)
    {
        // 使用粗体字绘制 "AI" 文字
        using var font = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        using var whiteBrush = new SolidBrush(Color.White);
        using var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 50, 100));

        string text = "AI";
        var textSize = g.MeasureString(text, font);
        float textX = cx - textSize.Width / 2;
        float textY = cy - textSize.Height / 2;

        // 绘制阴影
        g.DrawString(text, font, shadowBrush, textX + 1, textY + 1);
        // 绘制白色文字
        g.DrawString(text, font, whiteBrush, textX, textY);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _animTimer.Stop();
        base.OnFormClosing(e);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80000; // WS_EX_LAYERED
            return cp;
        }
    }
}

