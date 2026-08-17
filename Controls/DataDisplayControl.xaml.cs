using System.Windows;
using System.Windows.Controls;

namespace ZNQInterface.Controls
{
    public partial class DataDisplayControl : UserControl
    {
        public DataDisplayControl()
        {
            InitializeComponent();
        }

        // 左侧描述文字
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(
                nameof(Description),
                typeof(string),
                typeof(DataDisplayControl),
                new PropertyMetadata(string.Empty));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        // 中间显示数值
        public static readonly DependencyProperty DisplayValueProperty =
            DependencyProperty.Register(
                nameof(DisplayValue),
                typeof(string),
                typeof(DataDisplayControl),
                new PropertyMetadata("--"));

        public string DisplayValue
        {
            get => (string)GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        // 右侧单位
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit),
                typeof(string),
                typeof(DataDisplayControl),
                new PropertyMetadata(string.Empty));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }
    }
}