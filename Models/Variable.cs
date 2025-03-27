using NModbus;
using S7.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimplifiedCommBridge.Models
{

    /// <summary>
    /// 数据类型枚举
    /// </summary>
    public enum VarTypeEnum
    {
        Bool,
        Short,
        Int32,
        Float,
    }


    /// <summary>
    /// 变量模型（实现属性变更通知）
    /// </summary>
    public class Variable : INotifyPropertyChanged
    {
        private static int _nextId = 1; // 静态计数器
        public Variable()
        {
            ID=Interlocked.Increment(ref _nextId);
        }

        /// <summary>
        /// 唯一标识创建时自动生成
        /// </summary>
        public int ID {  get; }

        private object _value;

        /// <summary>
        /// 变量名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 协议地址（格式根据协议不同而变化）
        /// Modbus示例："1" 表示保持寄存器0001
        /// S7示例："DB1.DBD100" 表示DB块1的DBD100地址
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public VarTypeEnum DataType { get; set; }

        /// <summary>
        /// 协议名称
        /// </summary>
        public string ProtocolName { get; set; }

       

        /// <summary>
        /// 变量当前值
        /// </summary>
        public object Value
        {
            get => _value;
            set
            {
                // 先检查是否都为 null，如果都为 null 则认为值相同
                if (_value == null && value == null)
                {
                    return;
                }
                // 若一个为 null 另一个不为 null，则值不同
                if ((_value == null) != (value == null))
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                    return;
                }

                // 当两者都不为 null 时，使用 Equals 方法比较值
                if (!_value.Equals(value))
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        /// <summary>
        /// 调用写入方法是的 写入值
        /// </summary>
        public object SetValue { get; set; }

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"协议名称:{ProtocolName}-变量名称:{Name}-ID:{ID}-地址:{Address}-数据类型:{DataType}-设定值:{SetValue}-当前值:{Value}";
        }
    }








}
