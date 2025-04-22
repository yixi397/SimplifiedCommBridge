           
    //初始化协议实例
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

```

