using HidSharp;
using HidSharp.Reports;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AMIOGamePadTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // HID デバイスの Vendor ID（VID）と Product ID（PID）を設定
        private const int VendorId = 0x0483; // STM32の例
        private const int ProductId = 0x5750; // プロジェクトに応じて変更
        private const byte ReportIdLed = 0x01; // LED 制御用の Report ID
        private HidDevice? device;
        private HidStream? stream;

        private bool _stateSW1 = false; // スイッチ1の状態
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
        private int _stateStickX = 0; // スティックXの状態
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
        private int _stateStickY = 0; // スティックYの状態
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

        public event PropertyChangedEventHandler? PropertyChanged; // 必須のイベントを実装
        public ObservableCollection<CommandClass> Commands { get; private set; }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this; // 追加: DataContextを自身に設定

            Commands = new ObservableCollection<CommandClass>
            {
                new CommandClass(0x00, "消灯"),
                new CommandClass(0x01, "点灯"),
                new CommandClass(0x02, "点滅"),
                new CommandClass(0x03, "フラッシュ(右)"),
                new CommandClass(0x83, "フラッシュ(左)"),
            };

            this.Loaded += (s, e) =>
            {
                if (cmbLedCommand.SelectedIndex < 0)
                {
                    cmbLedCommand.SelectedIndex = 0;
                }
            };

            // HID デバイスを探す
            var list = DeviceList.Local;
            device = list.GetHidDevices(VendorId, ProductId).FirstOrDefault();

            if (device == null)
            {
                Console.WriteLine("HID デバイスが見つかりません。");
                return;
            }

            Console.WriteLine("HID デバイスが見つかりました！");

            if (!device.TryOpen(out stream))
            {
                Console.WriteLine("デバイスのオープンに失敗しました。");
                return;
            }

            // 非同期でレポートを受信するループ
            Task.Run(async () =>
            {
                var inputReportLength = device.GetMaxInputReportLength();
                var buffer = new byte[inputReportLength];
                while (true)
                {
                    try
                    {
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read > 0)
                        {
                            // buffer[0] = ReportID, buffer[1] = データ
                            this.Dispatcher.Invoke(() =>
                            {
                                byte data = buffer[1];
                                StateSW1 = ((data & 0x01) == 0x01);
                                StateSW2 = ((data & 0x02) == 0x02);
                                StateSW3 = ((data & 0x04) == 0x04);
                                StateSW4 = ((data & 0x08) == 0x08);

                                StateStickX = buffer[2] + (buffer[3]&0x0f) * 256;
                                StateStickY = (buffer[3] >> 4) + buffer[4] * 16;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ReadAsync error: {ex.Message}");
                        await Task.Delay(500);
                    }
                }
            });

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private void cmbLedCommand_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (device != null && stream != null)
            {
                // LEDコマンド送信（2バイト [ReportID, コマンド]）
                int outLen = device.GetMaxOutputReportLength();
                byte[] ledCommand = new byte[outLen];
                ledCommand[0] = 0x01; // ReportID
                ledCommand[1] = 0x01; // コマンド

                CommandClass? c = cmbLedCommand.SelectedItem as CommandClass;
                if (c != null)
                {
                    ledCommand[1] = (byte)c.Command;
                }
                CommandRaw.Content = ledCommand[0].ToString("X2") + " " + ledCommand[1].ToString("X2");

                try
                {
                    // 書き込み前にストリーム位置を0にリセット（必要な場合）
                    if (stream.CanSeek) { stream.Seek(0, SeekOrigin.Begin); }

                    stream.Write(ledCommand, 0, ledCommand.Length);
                    stream.Flush();
                    Console.WriteLine("LEDコマンド送信成功: " + BitConverter.ToString(ledCommand));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"LEDコマンド送信失敗: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("デバイスが null です。");
            }

        }
    }
    public class CommandClass
    {
        public CommandClass(byte command, string name)
        {
            Command = command;
            Name = name;
        }

        public int Command { get; set; }
        public string Name { get; set; }
    }
}