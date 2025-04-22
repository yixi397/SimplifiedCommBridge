using SimplifiedCommBridge.Communication;
using SimplifiedCommBridge.Models;


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
                    switch (group.ToList()[0].DataType)
                    {
                            case VarTypeEnum.Bool:
                                readBIT(group);//执行读取
                            break;
                            case VarTypeEnum.Short:
                                readRegister<short>(group);//执行读取
                            break;
                            case VarTypeEnum.UShort:
                                readRegister<ushort>(group);//执行读取
                            break;

                            case VarTypeEnum.Int32:
                                readRegister<Int32>(group);//执行读取
                            break;

                            case VarTypeEnum.UInt32:
                                readRegister<uint>(group);//执行读取
                            break;
                        case VarTypeEnum.Float:
                                readRegister<float>(group);//执行读取
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
        /// 读取寄存器数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="group"></param>
        private void readRegister<T>(IGrouping<VarTypeEnum, Variable> group)
        {
            //获取地址数据
            ushort[] ushorts = new ushort[group.ToList< Variable >(). Count];
            for (ushort i = 0; i < ushorts.Length; i++)
            {
                ushorts[i] = getaddress(group.ToList<Variable>()[i].Address);
            }
            var ReadStrategy = GenerateReadStrategy(ushorts, 120);
            //获取原始数据
            Dictionary<ushort, ushort> keys = new Dictionary<ushort, ushort>();//地址和值字典
            for (ushort i = 0;i < ReadStrategy.Count; i++)
            {
                ushort[] values = _modbusTcpHelper.ReadHoldingRegisters(ReadStrategy[i].Item1,(ushort)(ReadStrategy[i].Item2+1));

                for (ushort j = 0;j<values.Length;j++)
                {
                    //将地址和值添加到字典
                    ushort address = (ushort)(ReadStrategy[i].Item1 + j);
                    if (keys.ContainsKey(address) == false)
                    {
                        keys.Add(address, values[j]);
                    }
                }
            }
            
            //解析数据
            foreach (var item in group)
            {
                ushort u1 = keys[getaddress(item.Address)];
                ushort u2 = keys[(ushort)(getaddress(item.Address) + 1)];
                switch (item.DataType)
                {
                    case VarTypeEnum.Short:
                        item.Value = (short)u1;
                        break;
                    case VarTypeEnum.UShort:
                        item.Value = (ushort)u1;
                        break;
                    case VarTypeEnum.Int32:
                        item.Value = (Int32)Convert16bitTo32bit(u2, u1);
                        break;
                    case VarTypeEnum.UInt32:
                        item.Value = (UInt32)Convert16bitTo32bit(u2, u1);
                        break;
                    case VarTypeEnum.Float:
                        item.Value = (float)Convert16bitTo32bit(u2, u1);
                        break;
                    default:
                        break;
                }
            }
        }
        /// <summary>
        /// 16位数据转换位32位数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        private UInt32 Convert16bitTo32bit(ushort value1,ushort value2)
        {
            UInt32 result = (UInt32)((value1 << 16) | value2);
            return result;
        }


        /// <summary>
        /// 读取寄存器数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="group"></param>
        private void readBIT(IGrouping<VarTypeEnum, Variable> group)
        {
            //获取地址数据
            ushort[] ushorts = new ushort[group.ToList<Variable>().Count];
            for (ushort i = 0; i < ushorts.Length; i++)
            {
                ushorts[i] = getaddress(group.ToList<Variable>()[i].Address);
            }
            var ReadStrategy = GenerateReadStrategy(ushorts, 2000);
            //获取原始数据
            Dictionary<ushort, bool> keys = new Dictionary<ushort, bool>();//地址和值字典
            for (ushort i = 0; i < ReadStrategy.Count; i++)
            {
                bool[] values = _modbusTcpHelper.ReadCoils(ReadStrategy[i].Item1, (ushort)(ReadStrategy[i].Item2 + 1));

                for (ushort j = 0; j < values.Length; j++)
                {
                    //将地址和值添加到字典
                    ushort address = (ushort)(ReadStrategy[i].Item1 + j);
                    if (keys.ContainsKey(address) == false)
                    {
                        keys.Add(address, values[j]);
                    }
                }
            }

            //解析数据
            foreach (var item in group)
            {
                item.Value = keys[getaddress(item.Address)];
            }
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
                        case VarTypeEnum.UShort:
                            wirte<ushort>(group.Variables, _modbusTcpHelper.WriteHoldingRegisters, 120);
                            break;
                        case VarTypeEnum.Int32:
                            wirte<int>(group.Variables, _modbusTcpHelper.WriteInt32s, 60);
                            break;
                        case VarTypeEnum.UInt32:
                            wirte<uint>(group.Variables, _modbusTcpHelper.WriteUInt32s, 60);
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
                    case VarTypeEnum.UShort:
                        _modbusTcpHelper.WriteSingleRegister(getaddress(variable.Address), (ushort)variable.SetValue);
                        break;
                    case VarTypeEnum.Int32:
                        _modbusTcpHelper.WriteInt32(getaddress(variable.Address), (Int32)variable.SetValue);
                        break;
                    case VarTypeEnum.UInt32:
                        _modbusTcpHelper.WriteUInt32(getaddress(variable.Address), (UInt32)variable.SetValue);
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

   

    
        /// <summary>
        /// 获取读取策略方法
        /// </summary>
        /// <param name="addresses">地址数据</param>
        /// <param name="maxQuantity">最大读取数量</param>
        /// <returns></returns>
        public List<Tuple<ushort, ushort>> GenerateReadStrategy(ushort[] addresses, ushort maxQuantity)
        {
            if (addresses == null || addresses.Length == 0)
                return new List<Tuple<ushort, ushort>>();

            // 排序并去重
            Array.Sort(addresses);
            var distinctAddresses = addresses.Distinct().ToArray();

            var strategy = new List<Tuple<ushort, ushort>>();
            int currentIndex = 0;
            int n = distinctAddresses.Length;

            while (currentIndex < n)
            {
                ushort currentStart = distinctAddresses[currentIndex];
                ushort maxEnd = (ushort)(currentStart + maxQuantity - 1);

                // 处理ushort溢出情况
                if (maxEnd < currentStart)
                    maxEnd = ushort.MaxValue;

                int lastInRange = currentIndex;
                while (lastInRange < n && distinctAddresses[lastInRange] <= maxEnd)
                    lastInRange++;

                lastInRange--;

                ushort end = distinctAddresses[lastInRange];
                int quantity = end - currentStart + 1;

                strategy.Add(Tuple.Create((ushort)currentStart, (ushort)quantity));

                currentIndex = lastInRange + 1;
            }

            return strategy;
        }




       

    }
}
