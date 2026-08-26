using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ZNQInterface.ViewModels.Components.Warehouse
{
    /// <summary>
    /// 管理设备总览页面的五层料仓状态。
    /// </summary>
    public sealed class WarehouseStatusViewModel :
        BindableBase
    {
        public WarehouseStatusViewModel()
        {
            // 创建五层料仓。
            InitializeWarehouse();

            // 监听每层料仓状态变化。
            SubscribeWarehouseStateChanges();
        }

        /// <summary>
        /// 五层料仓集合。
        /// 集合顺序为五层到一层。
        /// </summary>
        public ObservableCollection<WarehouseLevelViewModel>
            WarehouseLevels
        {
            get;
        } = new ObservableCollection<WarehouseLevelViewModel>();

        /// <summary>
        /// 当前正在调整的料仓层。
        /// 没有正在调整的料仓时显示“--”。
        /// </summary>
        public string CurrentAdjustingLevelText
        {
            get
            {
                string[] adjustingLevels =
                    WarehouseLevels
                        .Where(
                            level =>
                                level.State ==
                                WarehouseLevelState.Adjusting)
                        .Select(
                            level =>
                                $"第{level.LevelNumber}层")
                        .ToArray();

                if (adjustingLevels.Length == 0)
                {
                    return "--";
                }

                return string.Join(
                    "、",
                    adjustingLevels);
            }
        }

        /// <summary>
        /// 修改指定料仓层的状态。
        /// </summary>
        public bool SetWarehouseLevelState(
            int levelNumber,
            WarehouseLevelState state)
        {
            WarehouseLevelViewModel level =
                WarehouseLevels.FirstOrDefault(
                    item =>
                        item.LevelNumber ==
                        levelNumber);

            if (level == null)
            {
                return false;
            }

            // WarehouseLevelViewModel会触发PropertyChanged。
            level.State = state;

            return true;
        }

        /// <summary>
        /// 创建并初始化五层料仓。
        /// </summary>
        private void InitializeWarehouse()
        {
            // 从五层到一层创建，
            // 对应界面从上到下的显示顺序。
            for (int levelNumber = 5;
                 levelNumber >= 1;
                 levelNumber--)
            {
                WarehouseLevels.Add(
                    new WarehouseLevelViewModel(
                        levelNumber,
                        WarehouseLevelState.Empty));
            }
            // 状态示例
            SetWarehouseLevelState(
                1,
                WarehouseLevelState.Unadjusted);
            SetWarehouseLevelState(
                2,
                WarehouseLevelState.Adjusting);

        }

        /// <summary>
        /// 监听五层料仓的状态变化。
        /// </summary>
        private void SubscribeWarehouseStateChanges()
        {
            foreach (WarehouseLevelViewModel level
                     in WarehouseLevels)
            {
                level.PropertyChanged +=
                    OnWarehouseLevelPropertyChanged;
            }
        }

        /// <summary>
        /// 某一层料仓的属性发生变化时调用。
        /// </summary>
        private void OnWarehouseLevelPropertyChanged(
            object sender,
            PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName !=
                nameof(WarehouseLevelViewModel.State))
            {
                return;
            }

            // State变化后，重新计算并显示当前调整层。
            RaisePropertyChanged(
                nameof(CurrentAdjustingLevelText));
        }
    }
}