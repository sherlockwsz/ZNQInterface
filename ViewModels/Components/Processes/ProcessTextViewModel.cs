using Prism.Mvvm;
using System;
using System.IO;

namespace ZNQInterface.ViewModels.Components.Processes
{
    public class ProcessTextViewModel : BindableBase
    {
        private string _text = string.Empty;

        public string Text
        {
            get => _text;
            private set => SetProperty(
                ref _text,
                value);
        }

        /// <summary>
        /// 直接设置显示文字。
        /// </summary>
        public void SetText(string text)
        {
            Text = text ?? string.Empty;
        }

        /// <summary>
        /// 读取文本文件并显示。
        /// </summary>
        public void LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Text = $"文件不存在：{filePath}";
                    return;
                }

                Text = File.ReadAllText(filePath);
            }
            catch (Exception exception)
            {
                Text = $"读取文件失败：{exception.Message}";
            }
        }
    }
}