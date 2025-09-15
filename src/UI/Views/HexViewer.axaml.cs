using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using System;
using System.ComponentModel;
using System.Text;

namespace CrowsNestMqtt.UI.Views
{
    public partial class HexViewer : UserControl, INotifyPropertyChanged
    {
        public static readonly StyledProperty<byte[]?> BytesProperty =
            AvaloniaProperty.Register<HexViewer, byte[]?>(nameof(Bytes), defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

        public new event PropertyChangedEventHandler? PropertyChanged;

        public byte[]? Bytes
        {
            get => GetValue(BytesProperty);
            set
            {
                SetValue(BytesProperty, value);
                OnPropertyChanged(nameof(Bytes));
                OnPropertyChanged(nameof(HexDump));
            }
        }

        public string HexDump => Bytes == null ? string.Empty : FormatHexDump(Bytes);

        public HexViewer()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private static string FormatHexDump(byte[] bytes)
        {
            if (bytes.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            int bytesPerLine = 16;
            for (int i = 0; i < bytes.Length; i += bytesPerLine)
            {
                sb.Append($"{i:X8}  ");
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < bytes.Length)
                        sb.Append($"{bytes[i + j]:X2} ");
                    else
                        sb.Append("   ");
                }
                sb.Append(" ");
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < bytes.Length)
                    {
                        var b = bytes[i + j];
                        sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                    }
                    else
                    {
                        sb.Append(' ');
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
