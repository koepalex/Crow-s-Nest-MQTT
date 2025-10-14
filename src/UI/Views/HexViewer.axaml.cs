using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using System;
using System.ComponentModel;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace CrowsNestMqtt.UI.Views
{
    [ExcludeFromCodeCoverage] // UI control with Avalonia-specific initialization
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

        // Direct property for HexDump so Avalonia change notifications are reliable (no reliance on INotifyPropertyChanged)
        public static readonly DirectProperty<HexViewer, string> HexDumpProperty =
            AvaloniaProperty.RegisterDirect<HexViewer, string>(
                nameof(HexDump),
                o => o.HexDump);

        private string _hexDump = string.Empty;

        public string HexDump
        {
            get => _hexDump;
            private set => SetAndRaise(HexDumpProperty, ref _hexDump, value);
        }

        static HexViewer()
        {
            // Recompute HexDump whenever Bytes changes (binding sets the styled property directly)
            BytesProperty.Changed.AddClassHandler<HexViewer>((x, e) =>
            {
                var bytes = x.Bytes;
                x.HexDump = (bytes == null || bytes.Length == 0) ? string.Empty : FormatHexDump(bytes);
            });
        }

        public HexViewer()
        {
            InitializeComponent();
            // Removed DataContext=self to allow parent DataContext (MainViewModel) to flow for external bindings.
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
