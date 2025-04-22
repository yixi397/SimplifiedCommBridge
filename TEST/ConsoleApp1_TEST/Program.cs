//using SimplifiedCommBridge.Models;

using SimplifiedCommBridge.Models;
using SimplifiedCommBridge.Service;

namespace ConsoleApp1_TEST
{
    internal class Program
    {
        static CommunicationService commService;
        static void Main(string[] args)
        {

            commService = new CommunicationService();

            commService.LogEvent += CommService_LogEventHandler;
            commService.Protocols = new Dictionary<string, ICommunicationProtocol>();
            modbustcpTest();


            foreach (var v in commService.Variables)
            {
                v.PropertyChanged += Variable_PropertyChanged;
                Console.WriteLine($"变量添加[{DateTime.Now}] {v.ToString()}");
            }

            commService.StartPolling(3000);

            System.Threading.Thread.Sleep(1000);

            // 测试写入功能
            commService.Variables[0].SetValue = (UInt32)6553100;
            commService.Variables[1].SetValue = (UInt32)6553201;
            commService.Variables[2].SetValue = (UInt32)6553302;
            commService.Variables[3].SetValue = (UInt32)6553403;
            commService.Variables[4].SetValue = (UInt32)6553504;
            commService.Write(new List<Variable>() { commService.Variables[0], commService.Variables[1], commService.Variables[2], commService.Variables[3] });
            Console.ReadKey();
        }



        /// <summary>
        /// 变量变更事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void Variable_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            

            var var = (Variable)sender;
            Console.WriteLine($"值变更[{DateTime.Now}] {var.ToString()}");

        }

        /// <summary>
        /// 日志事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="logEventArgs"></param>
        static void CommService_LogEventHandler(object sender, LogEventArgs logEventArgs)
        {
            //if (logEventArgs.LogLevel == LogEventArgs.LogLevelEnum.Debug)
            //{
            //    return;
            //}
            Console.WriteLine(logEventArgs.ToString());
        }
        #region  MODbustcp测试


        static void modbustcpTest()
        {
            // 初始化协议实例

            commService.Protocols.Add("ModbusTCP", new ModbusTcpProtocol(GetModbusAddress));
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
                    Address = "D" + i * 2,
                    DataType = VarTypeEnum.UInt32,
                    ProtocolName = "ModbusTCP",

                });


            }

        }


        #region 汇川H5U地址转换

        static Dictionary<string, ushort> ModbusAddDic = new Dictionary<string, ushort>()
        {
                { "M", 0X0000 },
                { "B", 0X3000 },
                { "S", 0XE000 },
                { "X", 0XF800 },
                { "Y", 0XFC00 },
                { "D", 0X0000 },
                { "R", 0X3000 }
        };
        static ushort GetModbusAddress(string AddressName)
        {
            string addType = "";
            string addValue = "";
            foreach (var item in AddressName)
            {
                if (char.IsLetter(item))
                {
                    addType += item;
                }
                else
                {
                    addValue += item;
                }
            }

            if (ModbusAddDic.ContainsKey(addType) == false)
            {
                throw new ArgumentOutOfRangeException("未查询到该地址:" + AddressName);
            }
            int v = ModbusAddDic[addType] + Convert.ToUInt16(addValue);
            if (addType == "X" || addType == "Y")
            {

                v = ModbusAddDic[addType] + Convert.ToUInt16(addValue, 8);
            }

            return (ushort)v;


        }
        #endregion

        #endregion
    }
}

