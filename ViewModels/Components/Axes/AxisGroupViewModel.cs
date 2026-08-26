using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZNQInterface.Models.Axes;

namespace ZNQInterface.ViewModels.Components.Axes
{
    /// <summary>
    /// 一个功能机构对应的轴组。
    /// </summary>
    public sealed class AxisGroupViewModel
    {
        public AxisGroupViewModel(
            AxisGroupId groupId,
            string displayName,
            IEnumerable<AxisItemViewModel> axes)
        {
            GroupId = groupId;
            DisplayName = displayName ??
                          throw new ArgumentNullException(
                              nameof(displayName));

            if (axes == null)
            {
                throw new ArgumentNullException(
                    nameof(axes));
            }

            Axes = new ReadOnlyCollection<AxisItemViewModel>(
                axes.ToList());
        }

        /// <summary>
        /// 轴组唯一标识。
        /// </summary>
        public AxisGroupId GroupId { get; }

        /// <summary>
        /// 轴组显示名称。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 当前组包含的轴对象。
        /// </summary>
        public IReadOnlyList<AxisItemViewModel> Axes { get; }
    }
}