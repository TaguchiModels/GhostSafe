using ControlzEx.Standard;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using MahApps.Metro.Controls;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VideoPlayer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private LibVLC _libVLC;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
        DispatcherTimer _timer = new DispatcherTimer();
        string _videoPath = "";
        const int CascadeOffset = 30;
        const int StartOffset = 30;
        private bool _isDragging = false;
        private bool _wasPlayingBeforeDrag = false;
        const double frameRate = 30.0;
        const int frameCount = 5;
        int frameMs = (int)(1000.0 / frameRate * frameCount);
        private bool _isInSizeMove = false;
        private int _displayMode = 0; // 0: 通常, 1: コントロール非表示, 2: 全画面

        public MainWindow()
        {
            InitializeComponent();

            this.Title = System.IO.Path.GetFileName(App.VideoPath);
            _videoPath = App.VideoPath;

            var splash = new SplashWindow();
            splash.Show();

            Core.Initialize();

            this.KeyDown += MainWindow_KeyDown;
            this.Focusable = true;
            this.Focus(); // フォーカスを有効に

            RestoreWindowBounds();   // ウインドウサイズ確定

            this.Loaded += VideoWindow_Loaded; // ウインドウ初期ロード

            this.StateChanged += VideoWindow_StateChanged; // ウインドウ最小時に音量をゼロにする

            splash.Close();
        }

        /// <summary>
        /// コントロールコンテンツを隠す
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _displayMode = (_displayMode + 1) % 3;

                switch (_displayMode)
                {
                    case 0: // 通常表示に戻す
                        controlBar.Visibility = Visibility.Visible;
                        positionSlider.Visibility = Visibility.Visible;
                        WindowStyle = WindowStyle.SingleBorderWindow;
                        WindowState = WindowState.Normal;
                        ResizeMode = ResizeMode.CanResize;
                        break;

                    case 1: // コントロール非表示
                        WindowStyle = WindowStyle.None;
                        WindowState = WindowState.Normal;
                        controlBar.Visibility = Visibility.Collapsed;
                        positionSlider.Visibility = Visibility.Collapsed;
                        break;

                    case 2: // 全画面
                        WindowStyle = WindowStyle.None;
                        WindowState = WindowState.Maximized;
                        ResizeMode = ResizeMode.NoResize;
                        break;
                }

                // レイアウト更新を確実に反映（遅延付き）
                Dispatcher.InvokeAsync(() =>
                {
                    positionSlider.InvalidateVisual();
                    positionSlider.UpdateLayout();
                    controlBar.InvalidateVisual();
                    controlBar.UpdateLayout();
                }, DispatcherPriority.Background);

            }
        }

        /// <summary>
        /// 名前付きパイプを監視し、外部プロセスから送信されるコマンドを受信する
        /// </summary>
        /// <remarks>
        /// 指定されたパイプ名で <see cref="NamedPipeServerStream"/> を生成し、
        /// クライアントからの接続を待機します。
        /// <para>
        /// 接続後は、パイプから送信されるテキストを 1 行ずつ読み取り、
        /// 受信したコマンドを <see cref="HandleCommand(string)"/> に渡して処理します。
        /// </para>
        /// <para>
        /// コマンド処理は UI スレッドで実行する必要があるため、
        /// <see cref="Dispatcher.Invoke(Action)"/> を使用してディスパッチします。
        /// </para>
        /// <para>
        /// クライアントが切断されると読み取りループを終了し、
        /// パイプおよび関連リソースは自動的に破棄されます。
        /// </para>
        /// </remarks>
        /// <param name="pipeName">監視対象となる名前付きパイプの名前</param>
        private void ListenPipe(string pipeName)
        {
            using var server = new NamedPipeServerStream(pipeName, PipeDirection.In);
            server.WaitForConnection();

            using var reader = new StreamReader(server);
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null) break;

                Dispatcher.Invoke(() => HandleCommand(line));
            }
        }

        /// <summary>
        /// コマンド解析
        /// </summary>
        /// <param name="commandLine">close:ウインドウを閉じる
        /// minimize:ウインドウの最小化 
        /// restore:ウインドウの再表示
        /// </param>
        private void HandleCommand(string commandLine)
        {
            var parts = commandLine.Split(' ');
            switch (parts[0])
            {
                case "close":
                    this.Close();
                    break;

                case "minimize":
                    this.WindowState = WindowState.Minimized;
                    break;

                case "restore":
                    this.WindowState = WindowState.Normal;
                    break;
            }
        }

        /// <summary>
        /// 初期ロード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() => ListenPipe(App.PipeName)); // 非同期でIPC受信

            _libVLC = new LibVLC();
            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

            _mediaPlayer.CropGeometry = null; // クロップ設定も無効に

            videoView.MediaPlayer = _mediaPlayer;

            var media = new Media(_libVLC, _videoPath, FromType.FromPath);

            // レイアウト確定後に再生
            Dispatcher.BeginInvoke(() =>
            {
                _mediaPlayer.Play(media);
            }, DispatcherPriority.ContextIdle);

            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            volumeSlider.Value = _mediaPlayer.Volume;

            _mediaPlayer.EndReached += MediaPlayer_EndReached;

            Task.Run(() => MonitorParentProcess(App.ProcessId)); // 親プロセスの監視開始
        }

        /// <summary>
        /// ウインドウ位置とサイズの確定
        /// </summary>
        private void RestoreWindowBounds()
        {
            var settings = Properties.Settings.Default;

            if (settings.VideoWidth > 0 && settings.VideoHeight > 0)
            {
                this.Width = settings.VideoWidth;
                this.Height = settings.VideoHeight;
                this.Top = settings.VideoTop;
                this.Left = settings.VideoLeft;

                // 画面サイズ（プライマリ）
                double screenWidth = SystemParameters.WorkArea.Width;
                double screenHeight = SystemParameters.WorkArea.Height;

                // 次回用の座標を計算
                double nextLeft = settings.VideoLeft + CascadeOffset;
                double nextTop = settings.VideoTop + CascadeOffset;

                // 右端チェック
                if (nextLeft + this.Width > screenWidth)
                {
                    nextLeft = StartOffset;
                    nextTop += CascadeOffset;
                }

                // 下端チェック（完全に外れる前にリセット）
                if (nextTop + this.Height > screenHeight)
                {
                    nextLeft = StartOffset;
                    nextTop = StartOffset;
                }

                settings.VideoTop = nextTop;
                settings.VideoLeft = nextLeft;
                settings.Save();
            }
        }

        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;

        /// <summary>
        /// Win32 メッセージを受信するためのウインドウプロシージャ
        /// </summary>
        /// <remarks>
        /// WPF ウインドウに送られてくる Win32 メッセージをフックし、
        /// 必要なメッセージのみを処理します。
        /// <para>
        /// 本実装では、
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// WM_ENTERSIZEMOVE :
        /// ユーザーがウインドウの移動またはサイズ変更を開始した
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// WM_EXITSIZEMOVE :
        /// ユーザーがウインドウの移動またはサイズ変更を完了した
        /// </description>
        /// </item>
        /// </list>
        /// を監視し、操作完了時にウインドウ位置とサイズを保存します。
        /// </para>
        /// <para>
        /// DispatcherTimer を使わず、
        /// OS が確定と判断したタイミングのみで保存できるため、
        /// 誤保存や多重保存を防ぐことができます。
        /// </para>
        /// </remarks>
        /// <param name="hwnd">ウインドウハンドル</param>
        /// <param name="msg">受信した Win32 メッセージID</param>
        /// <param name="wParam">メッセージの追加情報</param>
        /// <param name="lParam">メッセージの追加情報</param>
        /// <param name="handled">メッセージを処理済みとするかどうか</param>
        /// <returns>既定の処理を行うため <see cref="IntPtr.Zero"/> を返します</returns>
        private IntPtr WndProc(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            switch (msg)
            {
                case WM_ENTERSIZEMOVE:
                    _isInSizeMove = true;
                    break;

                case WM_EXITSIZEMOVE:
                    _isInSizeMove = false;

                    // 通常状態のときだけ保存
                    if (WindowState == WindowState.Normal)
                    {
                        SaveWindowBounds();
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// ウインドウのネイティブハンドル（HWND）が生成された直後に呼ばれる
        /// </summary>
        /// <remarks>
        /// WPF ウインドウが内部的に使用する Win32 ウインドウハンドルが
        /// 確定したタイミングで実行されます。
        /// <para>
        /// この時点で <see cref="HwndSource"/> を取得し、
        /// Win32 メッセージを受け取るためのフック（WndProc）を登録します。
        /// </para>
        /// <para>
        /// ウインドウの移動・リサイズ終了（WM_EXITSIZEMOVE）を
        /// 正確に検出するために必要な初期化処理です。
        /// </para>
        /// </remarks>
        /// <param name="e">イベント引数</param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook(WndProc);
        }

        /// <summary>
        /// ウインドウ移動後の位置の保存
        /// </summary>
        private void SaveWindowBounds()
        {
            var bounds = this.RestoreBounds;

            //最小サイズ・画面外防止
            if (bounds.Width < 200 || bounds.Height < 200)
                return;

            //マルチモニタ対応（簡易）
            if (bounds.Left < -200 || bounds.Top < -200)
                return;

            var settings = Properties.Settings.Default;

            // 画面サイズ（プライマリ）
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;

            // 次回用の座標を計算
            double nextLeft = bounds.Left + CascadeOffset;
            double nextTop = bounds.Top + CascadeOffset;

            // 右端チェック
            if (nextLeft + this.Width > screenWidth)
            {
                nextLeft = StartOffset;
                nextTop += CascadeOffset;
            }

            // 下端チェック（完全に外れる前にリセット）
            if (nextTop + this.Height > screenHeight)
            {
                nextLeft = StartOffset;
                nextTop = StartOffset;
            }

            settings.VideoTop = nextTop;
            settings.VideoLeft = nextLeft;
            settings.VideoWidth = this.Width;
            settings.VideoHeight = this.Height;
            settings.Save();
        }

        /// <summary>
        /// 親ウインドウの監視
        /// </summary>
        /// <param name="parentPid">親プロセスのID</param>
        /// <returns></returns>
        private static async Task MonitorParentProcess(int parentPid)
        {
            try
            {
                var parentProcess = Process.GetProcessById(parentPid);
                await parentProcess.WaitForExitAsync(); // 非同期で待機
            }
            catch
            {
                // 取得失敗 = すでに終了している
            }

            // 親が死んだら自プロセスも終了
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }

        /// <summary>
        /// ウインドウのクローズ
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // タイマー停止（最重要！）
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
            }

            // メディアプレイヤー解放
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();      // 再生停止
                _mediaPlayer.Dispose();   // リソース解放
                _mediaPlayer = null;
            }

            if (_libVLC != null)
            {
                _libVLC.Dispose(); // LibVLC 解放
                _libVLC = null;
            }

            // ビデオファイル削除
            if (File.Exists(_videoPath))
                File.Delete(_videoPath);
        }

        /// <summary>
        /// 戻るボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null || _mediaPlayer.IsPlaying) return;

            long newTime = _mediaPlayer.Time - frameMs;
            if (newTime < 0) newTime = 0;

            _mediaPlayer.Time = newTime;

            // Trick: 一瞬だけ再生して画面を更新、すぐ停止
            _mediaPlayer.SetPause(false);
            Task.Delay(50).ContinueWith(_ => Dispatcher.Invoke(() => _mediaPlayer.Pause()));
        }

        /// <summary>
        /// 進むボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null || _mediaPlayer.IsPlaying) return;

            long newTime = _mediaPlayer.Time + frameMs;
            if (newTime > _mediaPlayer.Length) newTime = _mediaPlayer.Length;

            _mediaPlayer.Time = newTime;

            // Trick: 一瞬だけ再生して画面を更新、すぐ停止
            _mediaPlayer.SetPause(false);
            Task.Delay(50).ContinueWith(_ => Dispatcher.Invoke(() => _mediaPlayer.Pause()));
        }

        /// <summary>
        /// スタートボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null)
                return;

            if (_mediaPlayer.Media != null)
            {
                // メディアが既にロード済み → 一時停止からの再開
                _mediaPlayer.SetPause(false);
            }
            else
            {
                // 最初の再生
                var media = new Media(_libVLC, _videoPath, FromType.FromPath);
                _mediaPlayer.Play(media);
            }

            RewindBtn.IsEnabled = false;
            ForwardBtn.IsEnabled = false;
        }

        /// <summary>
        /// 一時停止ボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Pause(); // 再生中 → 一時停止
                RewindBtn.IsEnabled = true;
                ForwardBtn.IsEnabled = true;
            }
            else
            {
                _mediaPlayer.SetPause(false); // 停止中 → 再開（またはPause(); でも可）
                RewindBtn.IsEnabled = false;
                ForwardBtn.IsEnabled = false;
            }
        }

        /// <summary>
        /// ビデオスライダー移動時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // ドラッグ中でないときだけ反映（自動更新と競合しないため）
            if (!_isDragging && _mediaPlayer != null && _mediaPlayer.IsPlaying)
            {
                //_mediaPlayer.Position = (float)positionSlider.Value;
                positionSlider.Value = (double)_mediaPlayer.Time / _mediaPlayer.Length;
            }
        }

        /// <summary>
        /// ビデオスライダーマウスダウン時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PositionSlider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_mediaPlayer == null || _mediaPlayer.Media == null)
                return;

            _isDragging = true;
            _wasPlayingBeforeDrag = _mediaPlayer.IsPlaying;

            if (_wasPlayingBeforeDrag)
            {
                _mediaPlayer.Pause();  // 一時停止
            }

        }

        /// <summary>
        /// ビデオスライダーマウスアップ時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PositionSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_mediaPlayer == null || _mediaPlayer.Media == null)
                return;

            _isDragging = false;

            // シーク位置の指定
            float newPosition = (float)positionSlider.Value;
            _mediaPlayer.Time = (long)(newPosition * _mediaPlayer.Length); // seek

            //再生状態を戻す
            if (_wasPlayingBeforeDrag)
            {
                _mediaPlayer.SetPause(false);  // 再生再開
            }
        }

        /// <summary>
        /// ビデオスライダー自動移動
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_mediaPlayer == null || _isDragging)
                return;

            try
            {
                if (_mediaPlayer.IsPlaying)
                {
                    positionSlider.Value = _mediaPlayer.Position;

                    positionSlider.Value = _mediaPlayer.Position;

                    // 再生時間表示の更新
                    var currentTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
                    var totalTime = TimeSpan.FromMilliseconds(_mediaPlayer.Length);

                    timeLabel.Text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
                }
            }
            catch (AccessViolationException ex)
            {
                Debug.WriteLine("AccessViolation in Timer_Tick: " + ex.Message);
            }
        }

        /// <summary>
        /// 時刻を "hh:mm:ss" 表示に変換するヘルパー
        /// </summary>
        /// <param name="time">時刻</param>
        /// <returns>hh:mm:ss</returns>
        private string FormatTime(TimeSpan time)
        {
            return time.TotalHours >= 1
                ? time.ToString(@"hh\:mm\:ss")
                : time.ToString(@"mm\:ss");
        }

        /// <summary>
        /// 音量スライダー移動時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = (int)volumeSlider.Value;
            }
        }

        /// <summary>
        /// ビデオ終了時の振る舞い
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MediaPlayer_EndReached(object sender, EventArgs e)
        {
            Dispatcher.Invoke(async () =>
            {
                if (loopCheckBox.IsChecked == true)
                {
                    // 少し遅延を入れてから再生
                    await Task.Delay(200);

                    if (_mediaPlayer.IsPlaying)
                    {
                        _mediaPlayer.Stop();
                    }

                    // 再生位置を 0 にリセット（Stop だけでは無視されることがあるため）
                    _mediaPlayer.Time = 0;

                    using var media = new Media(_libVLC, _videoPath, FromType.FromPath);
                    _mediaPlayer.Play(media);
                }
            });
        }

        /// <summary>
        /// ウィンドウ最小時に音量をゼロにする
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VideoWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                if (_mediaPlayer != null)
                {
                    if (_mediaPlayer.IsPlaying)
                    {
                        _mediaPlayer.Pause(); // 再生中 → 一時停止
                        RewindBtn.IsEnabled = true;
                        ForwardBtn.IsEnabled = true;
                    }
                }
            }
            else if (this.WindowState == WindowState.Normal)
            {
                if (_mediaPlayer != null)
                {
                    if (!_mediaPlayer.IsPlaying)
                    {
                        _mediaPlayer.SetPause(false); // 停止中 → 再開（またはPause(); でも可）
                        RewindBtn.IsEnabled = false;
                        ForwardBtn.IsEnabled = false;
                    }
                }
            }
        }
    }
}