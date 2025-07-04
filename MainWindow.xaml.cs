using HidSharp;
using HidSharp.Reports;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq; // FirstOrDefault を使用するために必要
using System.Text;
using System.Threading; // CancellationTokenSource を使用するために必要
using System.Threading.Tasks; // Task.Run を使用するために必要
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media; // SolidColorBrush を使用するために必要
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AMIOGamePadTest
{
    /// <summary>
    /// MainWindow.xaml のインタラクションロジック
    /// このアプリケーションは、AMIO GamePad（USB-HID接続のゲームコントローラ）のテストツールです。
    /// デジタルスイッチ、アナログジョイスティックの入力状態表示と、LEDの点灯パターン制御を提供します。
    /// デバイスの挿抜（抜き差し）に自動的に対応します。
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // HID デバイスの Vendor ID（VID）と Product ID（PID）を設定します。
        // ご使用のAMIO GamePadの実際のIDに合わせて変更してください。
        private const int VendorId = 0x0483; // 例: STM32のVID
        private const int ProductId = 0x5750; // 例: プロジェクト固有のPID
        private const byte ReportIdLed = 0x01; // LED 制御用の Report ID

        private HidDevice? _amioGamePadDevice; // 接続中のAMIO GamePadデバイスのインスタンス
        private HidStream? _amioGamePadStream; // デバイスとの通信に使用するストリーム
        private CancellationTokenSource? _readCancellationTokenSource; // 非同期読み取りタスクをキャンセルするためのトークン
        private Task? _readTask; // デバイスからの非同期読み取りタスクの参照

        // UIバインディング用のプロパティ群
        // INotifyPropertyChanged インターフェースを実装し、プロパティ変更時にUIを更新します。

        private bool _stateSW1 = false; // スイッチ1の状態 (true: 押されている, false: 離されている)
        /// <summary>
        /// スイッチ1の現在の状態を取得または設定します。
        /// </summary>
        public bool StateSW1
        {
            get => _stateSW1;
            set
            {
                if (_stateSW1 != value)
                {
                    _stateSW1 = value;
                    OnPropertyChanged(nameof(StateSW1));
                }
            }
        }
        private bool _stateSW2 = false; // スイッチ2の状態
        /// <summary>
        /// スイッチ2の現在の状態を取得または設定します。
        /// </summary>
        public bool StateSW2
        {
            get => _stateSW2;
            set
            {
                if (_stateSW2 != value)
                {
                    _stateSW2 = value;
                    OnPropertyChanged(nameof(StateSW2));
                }
            }
        }
        private bool _stateSW3 = false; // スイッチ3の状態
        /// <summary>
        /// スイッチ3の現在の状態を取得または設定します。
        /// </summary>
        public bool StateSW3
        {
            get => _stateSW3;
            set
            {
                if (_stateSW3 != value)
                {
                    _stateSW3 = value;
                    OnPropertyChanged(nameof(StateSW3));
                }
            }
        }
        private bool _stateSW4 = false; // スイッチ4の状態
        /// <summary>
        /// スイッチ4の現在の状態を取得または設定します。
        /// </summary>
        public bool StateSW4
        {
            get => _stateSW4;
            set
            {
                if (_stateSW4 != value)
                {
                    _stateSW4 = value;
                    OnPropertyChanged(nameof(StateSW4));
                }
            }
        }
        private int _stateStickX = 0; // アナログジョイスティックX軸の状態 (0～4095)
        /// <summary>
        /// ジョイスティックX軸の現在の値を取得または設定します。
        /// </summary>
        public int StateStickX
        {
            get => _stateStickX;
            set
            {
                if (_stateStickX != value)
                {
                    _stateStickX = value;
                    OnPropertyChanged(nameof(StateStickX));
                }
            }
        }
        private int _stateStickY = 0; // アナログジョイスティックY軸の状態 (0～4095)
        /// <summary>
        /// ジョイスティックY軸の現在の値を取得または設定します。
        /// </summary>
        public int StateStickY
        {
            get => _stateStickY;
            set
            {
                if (_stateStickY != value)
                {
                    _stateStickY = value;
                    OnPropertyChanged(nameof(StateStickY));
                }
            }
        }

        private string _deviceManufacturer = "";
        public string DeviceManufacturer
        {
            get => _deviceManufacturer;
            set
            {
                if (_deviceManufacturer != value)
                {
                    _deviceManufacturer = value;
                    OnPropertyChanged(nameof(DeviceManufacturer));
                }
            }
        }

        private string _deviceProduct = "";
        public string DeviceProduct
        {
            get => _deviceProduct;
            set
            {
                if (_deviceProduct != value)
                {
                    _deviceProduct = value;
                    OnPropertyChanged(nameof(DeviceProduct));
                }
            }
        }

        private string _connectionStatus = "デバイスが接続されていません。"; // デバイスの接続ステータス表示用テキスト
        /// <summary>
        /// デバイスの接続ステータスを示すテキストを取得または設定します。
        /// </summary>
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

        private bool _isDeviceConnected = false; // デバイスが現在接続されているかどうかのフラグ
        /// <summary>
        /// デバイスが現在接続され、通信可能であるかどうかを示す値を取得または設定します。
        /// UI要素の有効/無効制御に使用されます。
        /// </summary>
        public bool IsDeviceConnected
        {
            get => _isDeviceConnected;
            set
            {
                if (_isDeviceConnected != value)
                {
                    _isDeviceConnected = value;
                    OnPropertyChanged(nameof(IsDeviceConnected));
                }
            }
        }

        private SolidColorBrush _connectionStatusColor = Brushes.Red; // 接続ステータス表示のテキスト色
        /// <summary>
        /// 接続ステータス表示のテキスト色を取得または設定します。（例: 赤:未接続, 緑:接続済み）
        /// </summary>
        public SolidColorBrush ConnectionStatusColor
        {
            get => _connectionStatusColor;
            set
            {
                if (_connectionStatusColor != value)
                {
                    _connectionStatusColor = value;
                    OnPropertyChanged(nameof(ConnectionStatusColor));
                }
            }
        }

        /// <summary>
        /// プロパティ変更通知イベント。INotifyPropertyChangedインターフェースの必須実装です。
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// LED制御コマンドのリスト。ComboBoxにバインドされます。
        /// </summary>
        public ObservableCollection<CommandClass> Commands { get; private set; }

        /// <summary>
        /// プロパティが変更されたときにUIに通知します。
        /// </summary>
        /// <param name="propertyName">変更されたプロパティの名前。</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// MainWindowクラスの新しいインスタンスを初期化します。
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this; // DataContextをこのウィンドウ自身に設定し、UIバインディングを可能にします。

            // LED制御コマンドの初期データを設定します。
            Commands = new ObservableCollection<CommandClass>
            {
                new CommandClass(0x00, "消灯"),
                new CommandClass(0x01, "点灯"),
                new CommandClass(0x02, "点滅"),
                new CommandClass(0x03, "フラッシュ(右)"),
                new CommandClass(0x83, "フラッシュ(左)"),
            };

            // ウィンドウロード時にLEDコマンドのComboBoxの選択を初期化します。
            this.Loaded += (s, e) =>
            {
                if (cmbLedCommand.SelectedIndex < 0)
                {
                    cmbLedCommand.SelectedIndex = 0;
                }
            };

            // HidSharpのDeviceListの変更イベントを購読し、デバイスの挿抜を検知します。
            DeviceList.Local.Changed += OnDeviceListChanged;

            // アプリケーション終了時のイベントを購読し、リソースを適切に解放します。
            this.Closing += MainWindow_Closing;

            // アプリケーション起動時に一度デバイスの初期状態をチェックします。
            CheckDeviceConnectionStatus();
        }

        /// <summary>
        /// HidSharpのDeviceListに変更があったときに呼び出されるイベントハンドラです。
        /// UIスレッドでデバイスの接続状態チェックをトリガーします。
        /// </summary>
        private void OnDeviceListChanged(object? sender, EventArgs e)
        {
            // UI要素の更新はUIスレッドで行う必要があるため、Dispatcher.Invokeを使用します。
            Dispatcher.Invoke(() =>
            {
                CheckDeviceConnectionStatus();
            });
        }

        /// <summary>
        /// デバイスの現在の接続状態を確認し、必要に応じて接続または切断処理を行います。
        /// </summary>
        private void CheckDeviceConnectionStatus()
        {
            // 現在、システムにAMIO GamePadが接続されているかを確認します。
            var currentDevice = DeviceList.Local.GetHidDevices(VendorId, ProductId).FirstOrDefault();

            if (currentDevice != null)
            {
                // AMIO GamePadがシステムに接続されている場合
                if (_amioGamePadDevice == null) // アプリがまだこのデバイスを認識していない場合
                {
                    ConnectAmioGamePad(currentDevice); // 新しくデバイスに接続します。
                }
                // else: 既にAMIO GamePadが接続されており、かつアプリも認識しているので何もしません。
            }
            else
            {
                // AMIO GamePadがシステムから切断されている場合
                if (_amioGamePadDevice != null) // アプリが以前このデバイスを認識していた場合
                {
                    DisconnectAmioGamePadInternal(); // デバイスの切断処理を行います。
                }
                // else: 以前からAMIO GamePadが接続されておらず、アプリも認識していないので何もしません。
            }
        }

        /// <summary>
        /// アプリケーションが終了する際に呼び出されるイベントハンドラです。
        /// 非同期でデバイスの切断処理を行い、すべてのリソースを解放します。
        /// </summary>
        private async void MainWindow_Closing(object? sender, CancelEventArgs e) // async に変更
        {
            // DeviceList.Local.Changed イベントの購読を解除し、メモリリークを防ぎます。
            DeviceList.Local.Changed -= OnDeviceListChanged;

            // 接続中のデバイスがあれば、非同期で切断処理を呼び出し、タスクの完了を待ちます。
            if (_amioGamePadDevice != null)
            {
                await DisconnectAmioGamePadAsync();
            }
        }

        /// <summary>
        /// AMIO GamePad に接続し、デバイスからのデータ読み取りを開始します。
        /// </summary>
        /// <param name="deviceToConnect">接続するHidDeviceインスタンス。</param>
        private void ConnectAmioGamePad(HidDevice deviceToConnect)
        {
            // 既にデバイスが接続されている場合は、再接続を試みずに処理を終了します。
            // （複数デバイス接続のシナリオは考慮していません。）
            if (_amioGamePadDevice != null)
            {
                ConnectionStatus = "AMIO GamePad は既に接続済みです。";
                ConnectionStatusColor = Brushes.Green;
                IsDeviceConnected = true;
                return;
            }

            try
            {
                // デバイスのオープンを試みます。
                if (deviceToConnect.TryOpen(out var stream))
                {
                    _amioGamePadDevice = deviceToConnect;
                    DeviceManufacturer = deviceToConnect.GetManufacturer() ?? "不明";
                    DeviceProduct = deviceToConnect.GetProductName() ?? "不明";
                    _amioGamePadStream = stream;

                    // 接続成功時のステータスを更新します。パス名は表示しません。
                    ConnectionStatus = "AMIO GamePad が接続されました。";
                    ConnectionStatusColor = Brushes.Green; // 接続成功時は緑色
                    IsDeviceConnected = true; // デバイス接続状態をtrueに設定
                    Console.WriteLine($"AMIO GamePad が接続されました (パス: {deviceToConnect.DevicePath ?? "不明"})"); // コンソールにはパスを出力

                    // 非同期でデバイスからのレポート（入力データ）を受信するループを開始します。
                    _readCancellationTokenSource = new CancellationTokenSource();
                    _readTask = Task.Run(async () => // 読み取りタスクの参照を保持
                    {
                        var inputReportLength = _amioGamePadDevice.GetMaxInputReportLength();
                        var buffer = new byte[inputReportLength];
                        while (!_readCancellationTokenSource.Token.IsCancellationRequested) // キャンセル要求がない限りループを継続
                        {
                            try
                            {
                                // デバイスからデータを非同期で読み取ります。キャンセル要求を受け取ります。
                                int read = await _amioGamePadStream.ReadAsync(buffer, 0, buffer.Length, _readCancellationTokenSource.Token);
                                if (read > 0)
                                {
                                    // 読み取ったデータをUIスレッドで処理します。
                                    this.Dispatcher.Invoke(() =>
                                    {
                                        // 受信したデータ（レポート）からスイッチとジョイスティックの値を解析します。
                                        // buffer[0]はReportID、buffer[1]以降がデータと仮定しています。
                                        byte data = buffer[1];
                                        StateSW1 = ((data & 0x01) == 0x01);
                                        StateSW2 = ((data & 0x02) == 0x02);
                                        StateSW3 = ((data & 0x04) == 0x04);
                                        StateSW4 = ((data & 0x08) == 0x08);

                                        // ジョイスティックの値は複数バイトにまたがるため、ビットシフトで結合します。
                                        StateStickX = buffer[2] + (buffer[3] & 0x0f) * 256;
                                        StateStickY = (buffer[3] >> 4) + buffer[4] * 16;
                                    });
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // タスクがキャンセルされた場合、ループを正常に終了します。
                                Console.WriteLine("読み取りタスクがキャンセルされました。");
                                break;
                            }
                            catch (Exception ex)
                            {
                                // 読み取り中に予期せぬエラーが発生した場合（例: デバイスの突然の切断）
                                Console.WriteLine($"ReadAsync エラー: {ex.Message}");
                                // エラーが発生した場合は、UIスレッドでデバイスの切断処理をトリガーし、ループを終了します。
                                Dispatcher.Invoke(DisconnectAmioGamePadInternal);
                                break;
                            }
                        }
                    }, _readCancellationTokenSource.Token); // ここでCancellationTokenをタスクに渡します。
                }
                else
                {
                    // デバイスのオープンに失敗した場合のステータスを更新します。
                    ConnectionStatus = "AMIO GamePad のオープンに失敗しました。";
                    ConnectionStatusColor = Brushes.Orange; // オープン失敗時はオレンジ色
                    IsDeviceConnected = false; // 接続状態をfalseに設定
                    Console.WriteLine($"AMIO GamePad のオープンに失敗しました (パス: {deviceToConnect.DevicePath ?? "不明"})"); // コンソールにはパスを出力
                    ResetInputStates(); // UI上の入力状態をリセット
                }
            }
            catch (Exception ex)
            {
                // 接続処理中に予期せぬエラーが発生した場合
                ConnectionStatus = $"AMIO GamePad 接続エラー: {ex.Message}";
                ConnectionStatusColor = Brushes.Red; // 接続エラー時は赤色
                IsDeviceConnected = false; // 接続状態をfalseに設定
                Console.WriteLine(ConnectionStatus);
                ResetInputStates(); // UI上の入力状態をリセット
            }
        }

        /// <summary>
        /// AMIO GamePad との接続を切断し、関連するリソースを解放します。
        /// 非同期の読み取りタスクが完了するのを待機します。
        /// 主にアプリケーション終了時に呼び出すことを想定しています。
        /// </summary>
        private async Task DisconnectAmioGamePadAsync()
        {
            // 読み取りタスクにキャンセルを要求します。
            if (_readCancellationTokenSource != null)
            {
                _readCancellationTokenSource.Cancel();
            }

            // 読み取りタスクが実行中であれば、その完了を最大5秒間待ちます。
            // これにより、タスクが安全に終了する時間を確保します。
            if (_readTask != null && !_readTask.IsCompleted)
            {
                try
                {
                    await Task.WhenAny(_readTask, Task.Delay(5000)); // タイムアウトしても処理は続行
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"読み取りタスクの完了待機中にエラーが発生しました: {ex.Message}");
                }
            }

            // CancellationTokenSource を解放し、nullに設定します。
            if (_readCancellationTokenSource != null)
            {
                _readCancellationTokenSource.Dispose();
                _readCancellationTokenSource = null;
            }
            _readTask = null; // タスク参照もクリア

            // HIDストリームをクローズし、リソースを解放します。
            if (_amioGamePadStream != null)
            {
                try
                {
                    _amioGamePadStream.Close();
                    _amioGamePadStream.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ストリームクローズエラー: {ex.Message}");
                }
                finally
                {
                    _amioGamePadStream = null;
                }
            }

            // デバイスの参照をクリアします。
            if (_amioGamePadDevice != null)
            {
                _amioGamePadDevice = null;
            }

            // 切断時のステータスを更新し、UI上の入力状態をリセットします。
            ConnectionStatus = "AMIO GamePad が切断されました。";
            ConnectionStatusColor = Brushes.Red; // 切断時は赤色
            IsDeviceConnected = false; // 接続状態をfalseに設定
            Console.WriteLine(ConnectionStatus);
            ResetInputStates();
        
            DeviceManufacturer = "";
            DeviceProduct = "";
        }

        /// <summary>
        /// AMIO GamePad との接続を同期的に切断し、関連するリソースを解放します。
        /// 非同期タスクの完了を待つ必要がない場合（例: 新しいデバイス接続時のクリーンアップ、読み取りエラー時）に使用します。
        /// </summary>
        private void DisconnectAmioGamePadInternal()
        {
            // 読み取りタスクにキャンセルを要求しますが、完了は待ちません。
            if (_readCancellationTokenSource != null)
            {
                _readCancellationTokenSource.Cancel();
            }

            // CancellationTokenSource を解放し、nullに設定します。
            if (_readCancellationTokenSource != null)
            {
                _readCancellationTokenSource.Dispose();
                _readCancellationTokenSource = null;
            }
            _readTask = null; // タスク参照もクリア

            // HIDストリームをクローズし、リソースを解放します。
            if (_amioGamePadStream != null)
            {
                try
                {
                    _amioGamePadStream.Close();
                    _amioGamePadStream.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ストリームクローズエラー: {ex.Message}");
                }
                finally
                {
                    _amioGamePadStream = null;
                }
            }

            // デバイスの参照をクリアします。
            if (_amioGamePadDevice != null)
            {
                _amioGamePadDevice = null;
            }

            // 切断時のステータスを更新し、UI上の入力状態をリセットします。
            // このメッセージは、直後にConnectAmioGamePadが呼び出される場合、上書きされる可能性があります。
            ConnectionStatus = "AMIO GamePad が切断されました。";
            ConnectionStatusColor = Brushes.Red;
            IsDeviceConnected = false;
            Console.WriteLine(ConnectionStatus);
            ResetInputStates();
        
            // デバイス情報クリア
            DeviceManufacturer = "";
            DeviceProduct = "";
        }


        /// <summary>
        /// UI上のデジタルスイッチとジョイスティックの入力状態を初期値にリセットします。
        /// </summary>
        private void ResetInputStates()
        {
            StateSW1 = false;
            StateSW2 = false;
            StateSW3 = false;
            StateSW4 = false;
            StateStickX = 0;
            StateStickY = 0;
        }

        /// <summary>
        /// （現在未使用）XAMLから呼び出される可能性のあるボタンクリックイベントハンドラです。
        /// 必要に応じて機能を追加してください。
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // このボタンは現在何もしていません。必要に応じて機能を追加してください。
        }

        /// <summary>
        /// LED制御コマンドのComboBoxの選択が変更されたときに呼び出されるイベントハンドラです。
        /// 選択されたコマンドをデバイスに送信します。
        /// </summary>
        private void cmbLedCommand_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // デバイスが接続されており、通信可能であることを確認します。
            if (_amioGamePadDevice != null && _amioGamePadStream != null && IsDeviceConnected)
            {
                // デバイスの最大出力レポート長を取得し、コマンドバッファを準備します。
                int outLen = _amioGamePadDevice.GetMaxOutputReportLength();
                // outLenが0の場合に備え、最低2バイトを確保します（ReportIDとコマンドバイト）。
                byte[] ledCommand = new byte[Math.Max(outLen, 2)];
                ledCommand[0] = ReportIdLed; // ReportID (0x01)
                ledCommand[1] = 0x01; // デフォルトのコマンド値

                // ComboBoxで選択されたコマンドを取得します。
                CommandClass? c = cmbLedCommand.SelectedItem as CommandClass;
                if (c != null)
                {
                    ledCommand[1] = (byte)c.Command; // 選択されたコマンド値に更新
                }
                // 送信する生コマンドをUIに表示します。
                CommandRaw.Content = ledCommand[0].ToString("X2") + " " + ledCommand[1].ToString("X2");

                try
                {
                    // デバイスにコマンドを書き込み、バッファをフラッシュします。
                    _amioGamePadStream.Write(ledCommand, 0, ledCommand.Length);
                    _amioGamePadStream.Flush();
                    Console.WriteLine("LEDコマンド送信成功: " + BitConverter.ToString(ledCommand));
                }
                catch (Exception ex)
                {
                    // コマンド送信中にエラーが発生した場合（例: デバイスが切断された）
                    Console.WriteLine($"LEDコマンド送信失敗: {ex.Message}");
                    // UIスレッドで同期的な切断処理をトリガーします。
                    Dispatcher.Invoke(DisconnectAmioGamePadInternal);
                }
            }
            else
            {
                // デバイスが未接続の場合、エラーメッセージをUIに表示します。
                ConnectionStatus = "デバイスが接続されていません。LEDコマンドを送信できません。";
                ConnectionStatusColor = Brushes.Red;
                Console.WriteLine(ConnectionStatus);
            }
        }
    }

    /// <summary>
    /// LED制御コマンドを表現するためのヘルパークラスです。
    /// ComboBoxのItemsSourceとして使用されます。
    /// </summary>
    public class CommandClass
    {
        /// <summary>
        /// CommandClassの新しいインスタンスを初期化します。
        /// </summary>
        /// <param name="command">デバイスに送信する実際のコマンドバイト値。</param>
        /// <param name="name">UIに表示するコマンドの分かりやすい名前。</param>
        public CommandClass(byte command, string name)
        {
            Command = command;
            Name = name;
        }

        /// <summary>
        /// デバイスに送信するコマンド値を取得または設定します。
        /// </summary>
        public int Command { get; set; }
        /// <summary>
        /// UIに表示するコマンドの名前を取得または設定します。
        /// </summary>
        public string Name { get; set; }
    }
}
