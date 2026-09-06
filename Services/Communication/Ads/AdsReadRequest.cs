using System;

namespace ZNQInterface.Services.Communication.Ads
{
    /// <summary>
    /// ADS批量读取中的一个叶子变量。
    /// 只读取PLC基础类型，避免直接封送ST结构体时产生BOOL/对齐差异。
    /// </summary>
    public sealed class AdsReadRequest
    {
        public AdsReadRequest(string symbolName, Type valueType)
        {
            if (string.IsNullOrWhiteSpace(symbolName))
            {
                throw new ArgumentException(
                    "ADS符号名不能为空。",
                    nameof(symbolName));
            }

            SymbolName = symbolName;
            ValueType = valueType
                ?? throw new ArgumentNullException(nameof(valueType));
        }

        public string SymbolName { get; }

        public Type ValueType { get; }

        public static AdsReadRequest Create<T>(string symbolName) =>
            new AdsReadRequest(symbolName, typeof(T));
    }
}
