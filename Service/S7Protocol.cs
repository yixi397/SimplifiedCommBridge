using SimplifiedCommBridge.Models;
using S7.Net;
using S7.Net.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using SimplifiedCommBridge.Service;
using System.Diagnostics;


namespace SimplifiedCommBridge.Service
{
    /// <summary>
    /// 西门子S7协议实现
    /// </summary>
    public class S7Protocol : ICommunicationProtocol
    {
        private Plc _plc;
        private string _ipAddress;
        private short _rack;
        private short _slot;
        private int _pord;
        public event Action<object, LogEventArgs> LogEvent;

        public bool IsConnected => _plc?.IsConnected == true;

        /// <summary>
        /// 连接S7设备
        /// </summary>
        /// <param name="parameters">需要包含IP、Rack、Slot CpuType参数</param>
        public bool Connect(Dictionary<string, object> parameters)
        {
            try
            {
                

                if (!parameters.ContainsKey("IP") || !parameters.ContainsKey("Rack") || !parameters.ContainsKey("Slot"))
                    return false;

                _ipAddress = parameters["IP"].ToString();
                _rack = Convert.ToInt16(parameters["Rack"]);
                _slot = Convert.ToInt16(parameters["Slot"]);
                _pord= Convert.ToInt16(parameters["Pord"]);
                CpuType type =(CpuType)parameters["CpuType"];
                LogEvent?.Invoke(this, new LogEventArgs($"连接S7设备{_ipAddress}-{_rack}-{_slot}-{_pord}"));

                _plc = new Plc(type, _ipAddress, _pord, _rack, _slot);
                _plc.Open();

                LogEvent?.Invoke(this, new LogEventArgs($"连接S7设备{_ipAddress}-{_rack}-{_slot}-{_pord}成功"));
                return _plc.IsConnected;
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"连接S7设备{_ipAddress}-{_rack}-{_slot}-{_pord}失败-{ex.Message}",LogEventArgs.LogLevelEnum.Error));
                return false;
            }
        }

        public void Disconnect()
        {
            _plc?.Close();
        }


        /// <summary>
        /// 批量读取变量值
        /// </summary>
        public void ReadVariables(IEnumerable<Variable> variables)
        {
            // 创建Stopwatch实例
            Stopwatch stopwatch = new Stopwatch();
            // 开始计时
            stopwatch.Start();

            try
            {
                //判断是否连接
                if (!IsConnected)
                {
                    LogEvent?.Invoke(this, new LogEventArgs($"S7设备{_ipAddress}-{_rack}-{_slot}-{_pord}未连接，尝试重新连接", LogEventArgs.LogLevelEnum.Warning));
                    _plc.Open();
                }


                #region 分组 排序
                // 预编译正则表达式（提升性能）
                var dbRegex = new Regex(@"^DB(\d+)\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var valueRegex = new Regex(@"^DB\d+\.DB[^\d]+(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                // 1. 按DB编号分组
                var groupedVariables = variables
                    .GroupBy(v =>
                    {
                        var dbMatch = dbRegex.Match(v.Address);
                        return int.TryParse(dbMatch.Groups[1].Value, out int dbNum) ? dbNum : -1;
                    })
                    .Where(g => g.Key > 0); // 过滤无效分组

                // 2. 对每个分组按地址中的数值降序排序
                var processedGroups = groupedVariables
                    .Select(g => new
                    {
                        DbBlock = g.Key,
                        SortedVariables = g.OrderByDescending(v =>
                        {
                            var valueMatch = valueRegex.Match(v.Address);
                            return int.TryParse(valueMatch.Groups[1].Value, out int val) ? val : 0;
                        })
                        .ToList() // 立即执行排序操作
                    });

                #endregion

                //读取数据
                foreach (var processeds in processedGroups)
                {
                    int bytenum= int.Parse(valueRegex.Match(processeds.SortedVariables[0].Address).Groups[1].Value);
                    byte[] bytes= _plc.ReadBytes(S7.Net.DataType.DataBlock, processeds.DbBlock, 0, bytenum+4);

                    //解析结果
                    foreach(var val in processeds.SortedVariables)
                    {
                        switch (val.DataType)
                        {
                            case VarTypeEnum.Bool:
                                val.Value= AddressParser.ParseAddress<bool>(val.Address, bytes);
                                break;
                            case VarTypeEnum.Short:
                                val.Value = AddressParser.ParseAddress<short>(val.Address, bytes);
                                break;
                            case VarTypeEnum.Int32:
                                val.Value = AddressParser.ParseAddress<int>(val.Address, bytes);
                                break;
                            case VarTypeEnum.Float:
                                val.Value = AddressParser.ParseAddress<float>(val.Address, bytes);
                                break;
                            default:
                                break;
                        }

                    }
                    
                }

               
            }
            catch (Exception ex)
            {
                if (IsConnected == false)
                {
                    LogEvent?.Invoke(this, new LogEventArgs($"{variables.ToArray()[0].ProtocolName}S7服务器断线: {ex.Message}", LogEventArgs.LogLevelEnum.Error));
                    Thread.Sleep(3000);
                }
                else
                {
                    LogEvent?.Invoke(this, new LogEventArgs($"S7读取错误: {ex.Message}", LogEventArgs.LogLevelEnum.Error));
                    throw new Exception($"S7读取错误: {ex.Message}");
                }
            }

            // 停止计时
            stopwatch.Stop();
            // 获取运行时间
            TimeSpan elapsedTime = stopwatch.Elapsed;
            // 输出运行时间
            if (variables.ToList().Count > 0)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"{variables.ToList()[0].ProtocolName}轮询一次完成，耗时-{elapsedTime.TotalMilliseconds}毫秒",
                LogEventArgs.LogLevelEnum.Debug));
            }

        }

       

        public void WriteVariable(Variable variable)
        {
            // 创建Stopwatch实例
            Stopwatch stopwatch = new Stopwatch();
            // 开始计时
            stopwatch.Start();

            //判断是否连接
            if (!IsConnected)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"{variable.ToString()} 写入失败 设备未连接", LogEventArgs.LogLevelEnum.Error));
            }

            if (variable.SetValue==null)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"WriteVariable{variable.ToString() } setvalue=null", LogEventArgs.LogLevelEnum.Warning));
                return;
            }
            switch (variable.DataType)
            {
                case VarTypeEnum.Bool:
                    _plc.Write(variable.Address, (bool)variable.SetValue);
                    break;
                case VarTypeEnum.Short:
                    _plc.Write(variable.Address, short.Parse(variable.SetValue.ToString()));
                    break;
                case VarTypeEnum.UShort:
                    _plc.Write(variable.Address, ushort.Parse(variable.SetValue.ToString()));
                    break;
                case VarTypeEnum.UInt32:
                    _plc.Write(variable.Address, UInt32.Parse(variable.SetValue.ToString()));
                    break;
                case VarTypeEnum.Int32:
                    _plc.Write(variable.Address, variable.SetValue);
                    break;
                case VarTypeEnum.Float:
                    _plc.Write(variable.Address, float.Parse(variable.SetValue.ToString()));
                    break;
                default:
                    break;
            }

            // 停止计时
            stopwatch.Stop();
            // 获取运行时间
            TimeSpan elapsedTime = stopwatch.Elapsed;
            // 输出运行时间
            
            LogEvent?.Invoke(this, new LogEventArgs($"写入地址-{variable.Address}完成，耗时-{elapsedTime.TotalMilliseconds}毫秒",
            LogEventArgs.LogLevelEnum.Debug));
            




        }

        public void WriteVariable(List<Variable> variables)
        {
            // 创建Stopwatch实例
            Stopwatch stopwatch = new Stopwatch();
            // 开始计时
            stopwatch.Start();

            //判断是否连接
            if (!IsConnected)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"{variables[0].ToString()} 写入失败 设备未连接", LogEventArgs.LogLevelEnum.Error));
            }

            List<DataItem> datas = new List<DataItem>();
            foreach(var data in variables)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"WriteVariables{data.ToString()}"));
                DataItem item = ParseAddress(data.Address);
                item.DataType=S7.Net.DataType.DataBlock;

                if (data.SetValue == null)
                {
                    LogEvent?.Invoke(this, new LogEventArgs($"WriteVariable{data.ToString()} setvalue=null", LogEventArgs.LogLevelEnum.Warning));
                    data.SetValue = false;
                }
                switch (data.DataType)
                {
                    case VarTypeEnum.Bool:
                        item.VarType = VarType.Bit;
                        item.Value= (bool)data.SetValue;
                        break;
                    case VarTypeEnum.Short:
                        item.VarType = VarType.Int;
                        item.Value = (Int16)data.SetValue;
                        break;
                    case VarTypeEnum.UShort:
                        item.VarType = VarType.Word;
                        item.Value = (ushort)data.SetValue;
                        break;
                    case VarTypeEnum.UInt32:
                        item.VarType = VarType.DWord;
                        item.Value = (UInt32)data.SetValue;
                        break;
                    case VarTypeEnum.Int32:
                        item.VarType = VarType.DInt;
                        item.Value = (Int32)data.SetValue;
                        break;
                    case VarTypeEnum.Float:
                        item.VarType = VarType.Real;
                        item.Value = (float)data.SetValue;
                        break;
                    default:
                        break;
                }
            }    

            _plc.Write(datas.ToArray());

            // 停止计时
            stopwatch.Stop();
            // 获取运行时间
            TimeSpan elapsedTime = stopwatch.Elapsed;
            // 输出运行时间
            if (variables.ToList().Count > 0)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"{variables.ToList()[0].ProtocolName}批量写入完成，耗时-{elapsedTime.TotalMilliseconds}毫秒",
                LogEventArgs.LogLevelEnum.Debug));
            }
        }

      

        /// <summary>
        /// 将PLC地址字符串（如 "DB1.DBX1.0"）解析为DataItem对象
        /// </summary>
        /// <param name="address">地址字符串，支持格式：DB{块号}.DB[X|B|W|D]{字节}[.位]</param>
        /// <returns>解析后的DataItem对象</returns>
        /// <exception cref="ArgumentException">地址格式错误</exception>
        public static DataItem ParseAddress(string address)
        {
            // 正则表达式匹配PLC地址
            var match = Regex.Match(address,
                @"^DB(?<db>\d+)\.DB(?<type>[XBWD])(?<byte>\d+)(\.(?<bit>\d+))?$",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                throw new ArgumentException("Invalid address format: " + address);
            }

            // 提取数据块号
            int db = int.Parse(match.Groups["db"].Value);

            // 提取数据类型标识符（X/B/W/D）
            string typeFlag = match.Groups["type"].Value.ToUpper();

            // 提取字节地址
            int startByte = int.Parse(match.Groups["byte"].Value);

            // 提取位地址（可选）
            byte? bit = match.Groups["bit"].Success ?
                byte.Parse(match.Groups["bit"].Value) :
                null;

            // 创建DataItem并填充基础属性
            var dataItem = new DataItem
            {
                DataType = S7.Net.DataType.DataBlock,
                DB = db,               // 数据块号
                StartByteAdr = startByte  // 起始字节
            };

            // 根据数据类型设置DataType和位地址
            switch (typeFlag)
            {
                case "X":  // 位操作（Bit）
                    if (bit == null || bit < 0 || bit > 7)
                    {
                        throw new ArgumentException($"Invalid bit index ({bit}) in address: {address}");
                    }
                    dataItem.VarType=VarType.Bit;
                    dataItem.BitAdr = (byte)bit;
                    //dataItem.Value = bit.Value;
                    break;

                case "B":  // 字节（Byte）
                    dataItem.VarType= VarType.Byte;
                    break;

                case "W":  // 字（Word, 16位）
                    dataItem.VarType = VarType.Int;
                    break;

                case "D":  // 双字（DWord, 32位）或浮点数（Real）
                           // 注意：需根据PLC变量实际类型选择DataType.DWord或DataType.Real
                    dataItem.VarType = VarType.DInt;// 默认按DWord处理
                    break;

                default:
                    throw new ArgumentException($"Unsupported data type '{typeFlag}' in address: {address}");
            }

            return dataItem;
        }


    }



    // <summary>
    /// 西门子PLC地址解析器（支持泛型类型安全验证）
    /// </summary>
    public static class AddressParser
    {
        /// <summary>
        /// 解析PLC地址并转换为指定类型
        /// </summary>
        /// <typeparam name="T">目标类型（支持bool/byte/short/ushort/int/uint/float）</typeparam>
        /// <param name="address">PLC地址（示例：DB1.DBX0.1, DB1.DBW0）</param>
        /// <param name="bytes">从PLC读取的原始字节数组</param>
        /// <returns>解析后的值</returns>
        /// <exception cref="ArgumentException">地址格式错误或类型不匹配</exception>
        /// <exception cref="ArgumentOutOfRangeException">地址超出字节数组范围</exception>
        public static T ParseAddress<T>(string address, byte[] bytes)
        {
            // 解析地址基本结构
            var match = Regex.Match(
                address,
                @"^DB\d+\.DB([BXWD])(\d+)(?:\.(\d+))?$",
                RegexOptions.IgnoreCase
            );

            if (!match.Success)
                throw new ArgumentException("地址格式错误，有效格式示例：DB1.DBX0.1, DB1.DBW2");

            // 提取地址组件
            string dataType = match.Groups[1].Value.ToUpper();
            int offset = int.Parse(match.Groups[2].Value);
            int? bitIndex = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : null;

            // 验证类型与地址的匹配性
            ValidateTypeConsistency<T>(dataType);

            // 根据数据类型执行解析
            return dataType switch
            {
                "X" => ParseBit<T>(offset, bitIndex!.Value, bytes),
                "B" => ParseByte<T>(offset, bytes),
                "W" => ParseWord<T>(offset, bytes),
                "D" => ParseDWord<T>(offset, bytes),
                _ => throw new InvalidOperationException("未支持的数据类型")
            };
        }

        #region 私有解析方法
        // 解析位地址（DBX）
        private static T ParseBit<T>(int byteOffset, int bitIndex, byte[] bytes)
        {
            ValidateRange(byteOffset, bytes.Length, "字节偏移量");
            if (bitIndex is < 0 or > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "位索引必须为0-7");

            bool value = (bytes[byteOffset] & (0x01 << bitIndex)) != 0;
            return (T)(object)value; // 安全转换为泛型类型
        }

        // 解析字节地址（DBB）
        private static T ParseByte<T>(int offset, byte[] bytes)
        {
            ValidateRange(offset, bytes.Length, "字节偏移量");
            return (T)(object)bytes[offset];
        }

        // 解析字地址（DBW）
        private static T ParseWord<T>(int offset, byte[] bytes)
        {
            ValidateRange(offset + 1, bytes.Length, "字范围");
            ushort value = (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
            return typeof(T) == typeof(short) ?
                (T)(object)(short)value :
                (T)(object)value;
        }

        // 解析双字地址（DBD）
        private static T ParseDWord<T>(int offset, byte[] bytes)
        {
            ValidateRange(offset + 3, bytes.Length, "双字范围");
            uint rawValue = (uint)(
                (bytes[offset] << 24) |
                (bytes[offset + 1] << 16) |
                (bytes[offset + 2] << 8) |
                bytes[offset + 3]
            );

            return typeof(T) switch
            {
                Type t when t == typeof(int) => (T)(object)(int)rawValue,
                Type t when t == typeof(float) => ParseFloat<T>(rawValue),
                _ => (T)(object)rawValue
            };
        }

        // 解析浮点数（特殊处理字节顺序）
        private static T ParseFloat<T>(uint rawValue)
        {
            
            byte[] buffer = BitConverter.GetBytes(rawValue);
            //if (BitConverter.IsLittleEndian) Array.Reverse(buffer);
            return (T)(object)BitConverter.ToSingle(buffer, 0);
        }
        #endregion

        #region 验证方法
        // 验证地址类型与泛型类型是否匹配
        private static void ValidateTypeConsistency<T>(string dataType)
        {
            Type targetType = typeof(T);
            bool isValid = dataType switch
            {
                "X" => targetType == typeof(bool),
                "B" => targetType == typeof(byte),
                "W" => targetType == typeof(ushort) || targetType == typeof(short),
                "D" => targetType == typeof(uint) || targetType == typeof(int) ||
                       targetType == typeof(float),
                _ => false
            };

            if (!isValid)
                throw new ArgumentException($"地址类型{dataType}与目标类型{targetType.Name}不匹配");
        }

        // 验证地址范围有效性
        private static void ValidateRange(int index, int length, string paramName)
        {
            if (index < 0 || index >= length)
                throw new ArgumentOutOfRangeException(paramName,
                    $"地址越界，最大有效索引：{length - 1}");
        }
        #endregion
    }
}
