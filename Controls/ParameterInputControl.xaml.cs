using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZNQInterface.Controls
{
    public partial class ParameterInputControl : UserControl
    {
        public ParameterInputControl()
        {
            InitializeComponent();
        }

        // 描述文字
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(
                nameof(Description),
                typeof(string),
                typeof(ParameterInputControl),
                new PropertyMetadata(string.Empty));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        // 可填写范围文字
        public static readonly DependencyProperty RangeTextProperty =
            DependencyProperty.Register(
                nameof(RangeText),
                typeof(string),
                typeof(ParameterInputControl),
                new PropertyMetadata(string.Empty));

        public string RangeText
        {
            get => (string)GetValue(RangeTextProperty);
            set => SetValue(RangeTextProperty, value);
        }

        // 输入内容
        public static readonly DependencyProperty InputTextProperty =
            DependencyProperty.Register(
                nameof(InputText),
                typeof(string),
                typeof(ParameterInputControl),
                new FrameworkPropertyMetadata(
                    string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string InputText
        {
            get => (string)GetValue(InputTextProperty);
            set => SetValue(InputTextProperty, value);
        }

        // 单位
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit),
                typeof(string),
                typeof(ParameterInputControl),
                new PropertyMetadata(string.Empty));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        // 按钮文字
        public static readonly DependencyProperty ButtonTextProperty =
            DependencyProperty.Register(
                nameof(ButtonText),
                typeof(string),
                typeof(ParameterInputControl),
                new PropertyMetadata("确定"));

        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        // 按钮命令
        public static readonly DependencyProperty ButtonCommandProperty =
            DependencyProperty.Register(
                nameof(ButtonCommand),
                typeof(ICommand),
                typeof(ParameterInputControl),
                new PropertyMetadata(null));

        public ICommand ButtonCommand
        {
            get => (ICommand)GetValue(ButtonCommandProperty);
            set => SetValue(ButtonCommandProperty, value);
        }

        // 命令参数
        public static readonly DependencyProperty ButtonCommandParameterProperty =
            DependencyProperty.Register(
                nameof(ButtonCommandParameter),
                typeof(object),
                typeof(ParameterInputControl),
                new PropertyMetadata(null));

        public object ButtonCommandParameter
        {
            get => GetValue(ButtonCommandParameterProperty);
            set => SetValue(ButtonCommandParameterProperty, value);
        }

        // 按钮是否允许操作
        public static readonly DependencyProperty IsActionEnabledProperty =
            DependencyProperty.Register(
                nameof(IsActionEnabled),
                typeof(bool),
                typeof(ParameterInputControl),
                new PropertyMetadata(true));

        public bool IsActionEnabled
        {
            get => (bool)GetValue(IsActionEnabledProperty);
            set => SetValue(IsActionEnabledProperty, value);
        }
    }
}