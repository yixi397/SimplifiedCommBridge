using SimplifiedCommBridge.Communication;
using SimplifiedCommBridge.Models;
using Microsoft.VisualBasic;
using NModbus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SimplifiedCommBridge.Service;

namespace SimplifiedCommBridge.Service
{
    

    /// <summary>
    /// Modbus TCP协议实现
    /// </summary>
    public class ModbusTcpProtocol : ICommunicationProtocol
    {
      
        private string _ipAddress;
        private int _port;
        private ModbusTcpHelper _modbusTcpHelper;
        private readonly Func<string, ushort> getaddress;
        public event Action<object, LogEventArgs> LogEvent;

     

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Getaddress">modbus地址转换 默认实现ushort.Parse</param>
        public ModbusTcpProtocol(Func<string,ushort> Getaddress=null)
        {
            if(Getaddress!=null)
            {
                getaddress = Getaddress;
            }
            else
            {
                getaddress = ushort.Parse ;
            }

        }

        public bool IsConnected
        {
            get 
            { 
                if(_modbusTcpHelper!=null)
                {
                   return _modbusTcpHelper.IsConnected;
                }
                else
                {
                    return false;
                }
               
            }
        }


        /// <summary>
        /// 连接Modbus设备
        /// </summary>
        /// <param name="parameters">需要包含IP和Port参数</param>
        public bool Connect(Dictionary<string, object> parameters)
        {
            LogEvent?.Invoke(this, new LogEventArgs($"连接Modbus设备{parameters["IP"]}-{parameters["Port"]}"));
            try
            {
                if (!parameters.ContainsKey("IP") || !parameters.ContainsKey("Port"))
                    return false;
              
                _ipAddress = parameters["IP"].ToString();
                _port = Convert.ToInt32(parameters["Port"]);
                if (_modbusTcpHelper == null)
                {
                    _modbusTcpHelper = new ModbusTcpHelper(_ipAddress, _port);
                }

                _modbusTcpHelper.Connect();
                LogEvent?.Invoke(this, new LogEventArgs($"连接Modbus设备{parameters["IP"]}-{parameters["Port"]}成功 ")  );
                return true;
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"连接Modbus设备{parameters["IP"]}-{parameters["Port"]}失败-{ex.Message}"));
                return false;
            }
        }

        public void Disconnect()
        {
            LogEvent?.Invoke(this, new LogEventArgs($"断开连接Modbus设备{_modbusTcpHelper.IpAddress}-{_modbusTcpHelper.Port} "));
            _modbusTcpHelper.Disconnect();
        }
       
       

        /// <summary>
        /// 批量读取变量值
        /// </summary>
        public void ReadVariables(IEnumerable<Variable> variables)
        {
            
            try
            {
                if (!IsConnected)
                {
                    //断线尝试重连
                    LogEvent?.Invoke(this, new LogEventArgs($"{variables.ToArray()[0].ProtocolName}Modbus断线尝试重连"));
                    _modbusTcpHelper.Connect();
                    LogEvent?.Invoke(this, new LogEventArgs($"{variables.ToArray()[0].ProtocolName}Modbus断线尝试重连成功"));
                    return;
                }

                // 分组排序，将 DataType 字符串转换为 int 类型
                var sortedGroups = variables
                   .GroupBy(v => v.DataType)
                   .SelectMany(g => g.OrderByDescending(s =>
                   {
                       return getaddress(s.Address);
                   } ));

              
                foreach (var group in variables.GroupBy(v => v.DataType))
                {
                    //地址排序分组
                    var va1 = group.OrderBy(g =>
                    {
                        return getaddress(g.Address);
                    });
                    List<Tuple<int, int, int>> readStrategy;
                   
                    switch (group.ToList()[0].DataType)
                    {
                            case VarTypeEnum.Bool:
                                readStrategy = GenerateReadStrategy(group.ToList(), 2000);//获取读取策略
                                read<bool>(readStrategy, group, _modbusTcpHelper.ReadCoils);//执行读取
                                break;
                            case VarTypeEnum.Short:
                                
                                readStrategy = GenerateReadStrategy(group.ToList(), 120);//获取读取策略
                                read<short>(readStrategy, group, _modbusTcpHelper.ReadShorts);//执行读取
                                break;
                            case VarTypeEnum.Int32:
                                readStrategy = GenerateReadStrategy(group.ToList(), 60);//获取读取策略
                                read<Int32>(readStrategy, group, _modbusTcpHelper.ReadInt32s);//执行读取
                            break;

                            case VarTypeEnum.Float:
                                readStrategy = GenerateReadStrategy(group.ToList(), 60);//获取读取策略
                                read<float>(readStrategy, group, _modbusTcpHelper.ReadFloats);//执行读取
                            break;
                            
                    }
                }
                
            }
            catch (Exception ex)
            {
                if (IsConnected == false)
                {
                    LogEvent?.Invoke(this, new LogEventArgs($"{variables.ToArray()[0].ProtocolName}Modbus服务器断线: {ex.Message}", LogEventArgs.LogLevelEnum.Error));
                    Thread.Sleep(3000);
                }
                else
                {
                    LogEvent?.Invoke(this, new LogEventArgs($"Modbus读取错误: {ex.Message}",LogEventArgs.LogLevelEnum.Error));
                    throw new Exception($"Modbus读取错误: {ex.Message}");
                }
            }
           
        }
       /// <summary>
       /// 
       /// </summary>
       /// <typeparam name="T">读取的数据类型</typeparam>
       /// <param name="readStrategy">读取策略 </param>
       /// <param name="group">读取集合</param>
       /// <param name="readFunc">读取方法</param>
       private void read<T>(List<Tuple<int, int, int>> readStrategy,IGrouping<VarTypeEnum,Variable> group, Func<ushort, ushort, byte, T[]> readFunc)
        {
            Dictionary<ushort, T> keys = new Dictionary<ushort, T>();//地址和值字典
            for (int i = 0; i < readStrategy.Count; i++)//读取变量并保存到字典里
            {
                T[] values = readFunc((ushort)readStrategy[i].Item1, (ushort)readStrategy[i].Item2,1);

                for (int j = 0; j < values.Length; j++)
                {
                    keys.Add((ushort)(readStrategy[i].Item1 + j), values[j]);
                }

            }

            foreach (var item in group)
            {
                item.Value = keys[getaddress(item.Address)];
            }

            //Dictionary<int,int> keys = new Dictionary<int,int>();
            //for (int i = 0; i < readStrategy.Count; i++)
            //{
            //    short[] values = _modbusTcpHelper.ReadShorts((ushort)readStrategy[i].Item1, (ushort)readStrategy[i].Item2);

            //    for (int j = 0; j < values.Length; j++)
            //    {
            //        keys.Add(readStrategy[i].Item1+j, values[j]);
            //    }

            //}

            //foreach (var item in group)
            //{
            //    item.Value = keys[int.Parse(item.Address)];
            //}
        }



        #region 写入接口实现
        /// <summary>
        /// 批量写入
        /// </summary>
        /// <param name="variables"></param>
        public void WriteVariable(List<Variable> variables)
        {
            // 输入参数校验
            if (variables == null || variables.Count == 0)
            {
                LogEvent?.Invoke(this, new LogEventArgs("变量列表为空，无需写入"));
                
                return;
            }

            if (!IsConnected)
            {
                LogEvent?.Invoke(this, new LogEventArgs("设备未连接，无法写入。"));
                return;
            }

            try
            {
                // 提前分组并排序，避免重复计算
                var sortedGroups = variables
                    .GroupBy(v => v.DataType)
                    .Select(g => new
                    {
                        DataType = g.Key,
                        Variables = g.OrderBy(v => getaddress(v.Address)).ToList()
                    })
                    .ToList();

                foreach (var group in sortedGroups)
                {
                    switch (group.DataType)
                    {
                        case VarTypeEnum.Bool:
                            wirte<bool>(group.Variables, _modbusTcpHelper.WriteCoils, 2000);
                            break;
                        case VarTypeEnum.Short:
                            wirte<short>(group.Variables, _modbusTcpHelper.WriteShorts, 120);
                            break;
                        case VarTypeEnum.Int32:
                            wirte<int>(group.Variables, _modbusTcpHelper.WriteInt32s, 60);
                            break;
                        case VarTypeEnum.Float:
                            wirte<float>(group.Variables, _modbusTcpHelper.WriteFloats, 60);
                            break;
                        default:
                            LogEvent?.Invoke(this,  new LogEventArgs($"不支持的数据类型: {group.DataType}",LogEventArgs.LogLevelEnum.Error));
                            throw new Exception($"不支持的数据类型: {group.DataType}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"写入过程中发生错误: {ex.Message}", LogEventArgs.LogLevelEnum.Error));
            }
        }

        private void wirte<T>(List<Variable> list, Action<ushort, T[], byte> writeFunc, int number)
        {
            for (int i = 0; i < list.Count; i++)
            {
                List<Variable> values = new List<Variable>();
                ushort address;

                try
                {
                    // 获取首地址
                    address = getaddress(list[i].Address);
                    values.Add(list[i]);

                    int index = 0;
                    // 计算下一个地址是否连续
                    while (i < list.Count - 1 && getaddress(list[i + 1].Address) == address + (ushort)(values.Count) && index < number)
                    {
                        values.Add(list[i + 1]);
                        i++;
                        index++;
                    }
                    T[]  values1 =new T[values.Count];
                    for (int j = 0; j < values.Count; j++)
                    {
                        // 动态处理所有数值类型（int, short, long 等）
                        dynamic value = values[j].SetValue;
                        values1[j] = (T)value; // 利用 dynamic 运行时解析类型
                    }

                    // 调用写入函数
                    LogEvent?.Invoke(this, new LogEventArgs($"执行写入-地址:{address} 数量:{values1.Length}"));
                    writeFunc(address, values1, 1);

                    // 输出日志
                    for (int j = 0; j < values.Count; j++)
                    {

                        LogEvent?.Invoke(this, new LogEventArgs($"写入地址:{address + j} 写入值:{values[j].SetValue}"));
                    }
                }
                catch (Exception ex)
                {
                    LogEvent?.Invoke(this, new LogEventArgs($"写入时发生错误: {ex.Message}", LogEventArgs.LogLevelEnum.Error));
                    throw new Exception($"写入时发生错误: {ex.Message}");
                }
            }
        }


        /// <summary>
        /// 单个写入
        /// </summary>
        /// <param name="variable"></param>
        public void WriteVariable(Variable variable)
        {
            if(IsConnected==false)
            {
                LogEvent?.Invoke(this, new LogEventArgs($"设备未连接，无法写入", LogEventArgs.LogLevelEnum.Error));
                return;
            }
            try
            {
                switch (variable.DataType)
                {
                    case VarTypeEnum.Bool:
                        _modbusTcpHelper.WriteSingleCoil(getaddress(variable.Address), (bool)variable.SetValue);
                        break;
                    case VarTypeEnum.Short:
                        _modbusTcpHelper.WriteSingleShort(getaddress(variable.Address), (short)variable.SetValue);
                        break;

                    case VarTypeEnum.Int32:
                        _modbusTcpHelper.WriteInt32(getaddress(variable.Address), (Int32)variable.SetValue);
                        break;

                    case VarTypeEnum.Float:
                        _modbusTcpHelper.WriteFloat(getaddress(variable.Address), (float)variable.SetValue);
                        break;
                    default:
                        throw new Exception($"不支持的数据类型: {variable.DataType}");
                }
            }
            catch (Exception ex)
            {

                LogEvent?.Invoke(this, new LogEventArgs($"写入过程中发生错误: {ex.Message}", LogEventArgs.LogLevelEnum.Error));
            }
           
        }

        #endregion

        #region 批量读写策略方法
        /// <summary>
        /// 
        /// </summary>
        /// <param name="variables"></param>
        /// <param name="maxQuantity"></param>
        /// <returns>地址 数量 变量个数</returns>
        public List<Tuple<int, int, int>> GenerateReadStrategy(List<Variable> variables, int maxQuantity)
        {
            int[] ints = new int[variables.Count];
            for (int i = 0; i < variables.Count; i++)
            {
                ints[i] = getaddress(variables[i].Address);
            }
            return GenerateReadStrategy(ints, maxQuantity);
        }

        // 生成读取策略的方法
        public List<Tuple<int, int, int>> GenerateReadStrategy(int[] addresses, int maxReadCount)
        {
            if (addresses == null || addresses.Length == 0)
                return new List<Tuple<int, int, int>>();

            // 去重并排序地址
            int[] sortedAddresses = addresses.Distinct().OrderBy(a => a).ToArray();
            List<Tuple<int, int, int>> readBlocks = new List<Tuple<int, int, int>>();
            int readStartAddress = sortedAddresses[0];
            int readEndAddress = sortedAddresses[0] + maxReadCount;
            int index = 0;
            for (int i = 0; i < sortedAddresses.Length; i++)
            {
                if (sortedAddresses[i] <= readEndAddress)
                {
                    index++;

                }
                else
                {
                    readBlocks.Add(Tuple.Create(readStartAddress, sortedAddresses[i - 1] - readStartAddress + 1, index));

                    index = 0;
                    readStartAddress = sortedAddresses[i];
                    readEndAddress = sortedAddresses[i] + maxReadCount;
                }

            }
            readBlocks.Add(Tuple.Create(readStartAddress, sortedAddresses[sortedAddresses.Length - 1] - readStartAddress + 1, index));



            return readBlocks;
        }

        




        #endregion

    }
}
