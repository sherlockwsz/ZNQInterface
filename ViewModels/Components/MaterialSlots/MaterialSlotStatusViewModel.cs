using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;

namespace ZNQInterface.ViewModels.Components.MaterialSlots
{
    /// <summary>
    /// 设备总览页面中的料位状态区域。
    /// 
    /// 负责管理：
    /// 1. 主料位A1～D6；
    /// 2. 不合格料位A1～D6；
    /// 3. 后续可以继续增加料位统计、当前调整料位和缓冲料位。
    /// </summary>
    public sealed class MaterialSlotStatusViewModel : BindableBase
    {
        /// <summary>
        /// 构造料位状态区域。
        /// </summary>
        public MaterialSlotStatusViewModel()
        {
            // 创建主料位和不合格料位。
            CreateSlots();

            // 第二步：设置每个料位的初始状态。
            InitializeSlots();

            // 第三步：初始化完成后监听状态变化。
            SubscribeMaterialSlotStateChanges();
        }

        /// <summary>
        /// 主料位集合。
        /// 包含A1～D6共24个料位。
        /// </summary>
        public ObservableCollection<MaterialSlotViewModel>
            MaterialSlots
        {
            get;
        } = new ObservableCollection<MaterialSlotViewModel>();

        /// <summary>
        /// 不合格料位集合。
        /// 当前同样包含A1～D6共24个料位。
        /// </summary>
        public ObservableCollection<MaterialSlotViewModel>
            UnqualifiedMaterialSlots
        {
            get;
        } = new ObservableCollection<MaterialSlotViewModel>();
        /// <summary>
        /// 缓冲料位集合。
        /// 当前包含缓冲料位1～4。
        /// </summary>
        public ObservableCollection<MaterialSlotViewModel>
            BufferSlots
        {
            get;
        } = new ObservableCollection<MaterialSlotViewModel>();
        /// <summary>
        /// 初始化各区域的显示状态。
        /// </summary>
        private void InitializeSlots()
        {
            // 主料盘24个位置需要等待PLC状态，因此先显示“未知”。
            foreach (MaterialSlotViewModel slot in MaterialSlots)
            {
                slot.State = MaterialSlotState.Unknown;
            }

            // 不合格料盘目前尚未与PLC数据绑定，先全部显示为空。
            foreach (MaterialSlotViewModel slot in UnqualifiedMaterialSlots)
            {
                slot.State = MaterialSlotState.Empty;
            }

            // 四个缓冲位都属于功能3。
            // 目前只有第1个与PLC的位置3绑定，其余三个保持为空。
            foreach (MaterialSlotViewModel slot in BufferSlots)
            {
                slot.State = MaterialSlotState.Empty;
            }

            if (BufferSlots.Count > 0)
            {
                // 第1缓冲位等待首次ADS数据。
                BufferSlots[0].State = MaterialSlotState.Unknown;
            }
        }
        private void CreateSlots()
        {
            string[] rowLabels =
            {
                "A",
                "B",
                "C",
                "D"
            };
            /// <summary>
            /// 创建主料位和不合格料位。
            /// </summary>

            foreach (string rowLabel in rowLabels)
            {
                for (int columnNumber = 1;
                     columnNumber <= 6;
                     columnNumber++)
                {
                    // 创建一个主料位。
                    MaterialSlotViewModel materialSlot =
                        new MaterialSlotViewModel(
                            rowLabel,
                            columnNumber);

                    MaterialSlots.Add(materialSlot);

                    // 创建一个不合格料位。
                    MaterialSlotViewModel unqualifiedSlot =
                        new MaterialSlotViewModel(
                            rowLabel,
                            columnNumber);

                    UnqualifiedMaterialSlots.Add(
                        unqualifiedSlot);
                }
            }
            // 创建4个缓冲料位。
            for (int bufferSlotNumber = 1;
                 bufferSlotNumber <= 4;
                 bufferSlotNumber++)
            {
                MaterialSlotViewModel bufferSlot =
                    new MaterialSlotViewModel(
                        "缓冲",
                        bufferSlotNumber);

                BufferSlots.Add(bufferSlot);
            }
        }

        /// <summary>
        /// 订阅主料位状态变化。
        /// 初始化完成后再执行，避免构造期间重复统计。
        /// </summary>
        private void SubscribeMaterialSlotStateChanges()
        {
            foreach (MaterialSlotViewModel slot
                     in MaterialSlots)
            {
                slot.PropertyChanged +=
                    OnMaterialSlotPropertyChanged;
            }
        }
        /// <summary>
        /// 主料位属性发生变化时调用。
        /// </summary>
        private void OnMaterialSlotPropertyChanged(
            object sender,
            PropertyChangedEventArgs eventArgs)
        {
            // 只有State变化时才重新统计。
            if (eventArgs.PropertyName !=
                nameof(MaterialSlotViewModel.State))
            {
                return;
            }
            // 料位状态变化后重新计算全部统计数据。
            RaiseStatisticsChanged();
        }
        /// <summary>
        /// 通知界面重新读取全部料位统计数据。
        /// </summary>
        private void RaiseStatisticsChanged()
        {
            RaisePropertyChanged(nameof(EmptySlotCount));// 空料位数量
            RaisePropertyChanged(nameof(UnadjustedSlotCount));// 未调整阻尼器数量
            RaisePropertyChanged(nameof(PendingAdjustmentSlotCount));// 待调整阻尼器数量
            RaisePropertyChanged(nameof(AdjustingSlotCount));// 正在调整的阻尼器数量
            RaisePropertyChanged(nameof(QualifiedSlotCount));// 合格阻尼器数量
            RaisePropertyChanged(nameof(UnqualifiedSlotCount));// 不合格阻尼器数量
            RaisePropertyChanged(nameof(CurrentAdjustingSlotText));// 当前正在调整的料位编号
        }
        /// <summary>
        /// 修改主料位状态。
        /// </summary>
        /// <param name="positionCode">
        /// 料位编号，例如A1、B3、D6。
        /// </param>
        /// <param name="state">
        /// 需要设置的新状态。
        /// </param>
        /// <returns>
        /// 找到并修改成功返回true，否则返回false。
        /// </returns>
        public bool SetMaterialSlotState(
            string positionCode,
            MaterialSlotState state)
        {
            MaterialSlotViewModel slot =
                MaterialSlots.FirstOrDefault(
                    item => item.PositionCode == positionCode);

            if (slot == null)
            {
                return false;
            }

            slot.State = state;

            return true;
        }
        /// <summary>
        /// 修改不合格料盘中的指定料位状态。
        /// </summary>
        public bool SetUnqualifiedSlotState(
            string positionCode,
            MaterialSlotState state)
        {
            MaterialSlotViewModel slot =
                UnqualifiedMaterialSlots.FirstOrDefault(
                    item => item.PositionCode == positionCode);

            if (slot == null)
            {
                return false;
            }

            slot.State = state;

            return true;
        }
        /// <summary>
        /// 修改指定缓冲料位的状态。
        /// </summary>
        /// <param name="bufferSlotNumber">
        /// 缓冲料位编号，允许范围为1～4。
        /// </param>
        /// <param name="state">
        /// 需要设置的新状态。
        /// </param>
        /// <returns>
        /// 找到并修改成功返回true；
        /// 编号不存在时返回false。
        /// </returns>
        public bool SetBufferSlotState(
            int bufferSlotNumber,
            MaterialSlotState state)
        {
            MaterialSlotViewModel slot =
                BufferSlots.FirstOrDefault(
                    item =>
                        item.ColumnNumber ==
                        bufferSlotNumber);

            if (slot == null)
            {
                return false;
            }

            slot.State = state;

            return true;
        }
        /// <summary>
        /// 主料位总数。
        /// </summary>
        public int TotalSlotCount =>
            MaterialSlots.Count;

        /// <summary>
        /// 空料位数量。
        /// </summary>
        public int EmptySlotCount =>
            MaterialSlots.Count(
                slot =>
                    slot.State == MaterialSlotState.Empty);

        /// <summary>未调整阻尼器数量。</summary>
        public int UnadjustedSlotCount =>
            MaterialSlots.Count(x => x.State == MaterialSlotState.Unadjusted);

        /// <summary>待调整阻尼器数量。</summary>
        public int PendingAdjustmentSlotCount =>
            MaterialSlots.Count(x => x.State == MaterialSlotState.PendingAdjustment);

        /// <summary>正在调整的阻尼器数量。</summary>
        public int AdjustingSlotCount =>
            MaterialSlots.Count(x => x.State == MaterialSlotState.Adjusting);

        /// <summary>合格阻尼器数量。</summary>
        public int QualifiedSlotCount =>
            MaterialSlots.Count(x => x.State == MaterialSlotState.Qualified);

        /// <summary>不合格阻尼器数量。</summary>
        public int UnqualifiedSlotCount =>
            MaterialSlots.Count(x => x.State == MaterialSlotState.Unqualified);

        /// <summary>
        /// 当前正在调整的料位编号。
        /// 没有正在调整的料位时显示“--”。
        /// </summary>
        public string CurrentAdjustingSlotText
        {
            get
            {
                string[] adjustingSlotCodes =
                    MaterialSlots
                        .Where(
                            slot =>
                                slot.State ==
                                MaterialSlotState.Adjusting)
                        .Select(
                            slot =>
                                slot.PositionCode)
                        .ToArray();

                if (adjustingSlotCodes.Length == 0)
                {
                    return "--";
                }

                return string.Join(
                    "、",
                    adjustingSlotCodes);
            }
        }
    }
}