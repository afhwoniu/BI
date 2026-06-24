using System;
using System.Threading;
using System.Windows.Forms;

namespace BiAssistant;

/// <summary>
/// BI 智能助手桌面应用程序入口
/// </summary>
static class Program
{
    private static FloatingBall? _floatingBall;
    private static ChatWindow? _chatWindow;
    private static TrayManager? _trayManager;
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        // 确保只运行一个实例
        const string mutexName = "BiAssistant_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show("BI 智能助手已在运行中！\n请查看系统托盘图标。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            // 初始化应用程序
            ApplicationConfiguration.Initialize();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // 创建托盘管理器
            _trayManager = new TrayManager();
            _trayManager.ShowWindowRequested += OnShowWindow;
            _trayManager.ToggleBallRequested += OnToggleBall;
            _trayManager.ExitRequested += OnExit;

            // 创建聊天窗口
            _chatWindow = new ChatWindow();

            // 创建悬浮球
            _floatingBall = new FloatingBall();
            _floatingBall.BallClicked += OnBallClicked;
            _floatingBall.LocationChanged += OnBallLocationChanged;
            _floatingBall.Show();

            // 显示欢迎提示
            _trayManager.ShowBalloonTip("BI 智能助手", "已启动！点击悬浮球或双击托盘图标打开智能分析。");

            // 运行消息循环
            Application.Run();
        }
        finally
        {
            _trayManager?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }

    private static void OnShowWindow(object? sender, EventArgs e)
    {
        _chatWindow?.Toggle();
    }

    private static void OnToggleBall(object? sender, EventArgs e)
    {
        if (_floatingBall != null)
        {
            _floatingBall.Visible = !_floatingBall.Visible;
        }
    }

    private static void OnBallClicked(object? sender, EventArgs e)
    {
        // 切换聊天窗口显示（窗口会自动居中）
        _chatWindow?.Toggle();
    }

    private static void OnBallLocationChanged(object? sender, EventArgs e)
    {
        // 悬浮球位置变化不影响窗口位置，窗口始终居中显示
    }

    private static void OnExit(object? sender, EventArgs e)
    {
        _floatingBall?.Close();
        _chatWindow?.Close();
        Application.Exit();
    }
}