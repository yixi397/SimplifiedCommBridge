# SimplifiedCommBridge


工业通信统一接口库 - 快速集成 Modbus TCP/Siemens S7

## 主要功能
- 同时连接多个设备服务器
- 统一读写 Modbus TCP 和 Siemens S7 数据
- 值变更自动通知（INotifyPropertyChanged）
- 自动轮询数据采集
- 实时日志监控

##基本使用
// 初始化协议实例
 var  commService = new CommunicationService();
 commService.Protocols = new Dictionary<string, ICommunicationProtocol>();
 commService.Protocols.Add("ModbusTCP", new ModbusTcpProtocol());
 commService.ConfigureProtocol("ModbusTCP", new Dictionary<string, object>
     {
         { "IP", "127.0.0.1" },
         { "Port", 502 }
     });

 //创建测试数据
 for (int i = 0; i < 100; i++)
 {
     commService.Variables.Add(new Variable
     {
         Name = "MotorEn" + i,
         Address = i.ToString(),
         DataType = VarTypeEnum.Bool,
         ProtocolName = "ModbusTCP",
     });
    //订阅值改变事件
     commService.Variables[i].PropertyChanged += Variable_PropertyChanged;
     //写单个变量
     commService.Variables[0].SetValue = true;
     commService.Write(commService.Variables[0]);
     //写多个变量
     List<Variable> variables = new List<Variable>();
     commService.Variables[0].SetValue = false;
     commService.Variables[1].SetValue = true;
     commService.Variables[2].SetValue = false;
     variables.Add(commService.Variables[0]);
     variables.Add(commService.Variables[1]);
     variables.Add(commService.Variables[2]);
     commService.Write(variables);
 }
 //启动轮询
 commService.StartPolling(100);

##日志监控
//获取日志信息
commService.LogEvent += CommService_LogEventHandler;
void CommService_LogEventHandler(object sender, LogEventArgs logEventArgs)
{
    Console.WriteLine(logEventArgs.ToString());
}



