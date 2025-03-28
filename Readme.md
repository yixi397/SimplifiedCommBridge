# SimplifiedCommBridge

��ҵͨ��ͳһ�ӿڿ� - ���ټ��� Modbus TCP/Siemens S7


## ��Ҫ����
- ͬʱ���Ӷ���豸������
- ͳһ��д Modbus TCP �� Siemens S7 ����
- ֵ����Զ�֪ͨ��INotifyPropertyChanged��
- �Զ���ѯ���ݲɼ�
- ʵʱ��־���

## ����ʹ��
```csharp
// ��ʼ��Э��ʵ��
 var  commService = new CommunicationService();
 commService.Protocols = new Dictionary<string, ICommunicationProtocol>();
 commService.Protocols.Add("ModbusTCP", new ModbusTcpProtocol());
 commService.ConfigureProtocol("ModbusTCP", new Dictionary<string, object>
     {
         { "IP", "127.0.0.1" },
         { "Port", 502 }
     });

 //������������
 for (int i = 0; i < 100; i++)
 {
     commService.Variables.Add(new Variable
     {
         Name = "MotorEn" + i,
         Address = i.ToString(),
         DataType = VarTypeEnum.Bool,
         ProtocolName = "ModbusTCP",
     });
    //����ֵ�ı��¼�
     commService.Variables[i].PropertyChanged += Variable_PropertyChanged;
     //д��������
     commService.Variables[0].SetValue = true;
     commService.Write(commService.Variables[0]);
     //д�������
     List<Variable> variables = new List<Variable>();
     commService.Variables[0].SetValue = false;
     commService.Variables[1].SetValue = true;
     commService.Variables[2].SetValue = false;
     variables.Add(commService.Variables[0]);
     variables.Add(commService.Variables[1]);
     variables.Add(commService.Variables[2]);
     commService.Write(variables);
 }
 //������ѯ
 commService.StartPolling(100);

```


[![����Wiki](https://github.com/yixi397/SimplifiedCommBridge/wiki/SimplifiedCommBridge%E6%96%87%E6%A1%A3)]