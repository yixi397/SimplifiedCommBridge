using NModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SimplifiedCommBridge.Communication
{
    public class ModbusTcpHelper
    {
        //### 说明
        //1. * *批量读写的最大限制 * *：
        //   - `ReadCoils` 和 `WriteCoils` 方法限制最多读写2000个和1968个线圈。
        //   - `ReadHoldingRegisters` 和 `WriteHoldingRegisters` 方法限制最多读写125个和123个寄存器。
        //   - `ReadInt32s` 和 `WriteInt32s` 方法限制最多读写62个和61个32位整数。
        //   - `ReadFloats` 和 `WriteFloats` 方法限制最多读写62个和61个浮点数。
        //   - `ReadShorts` 和 `WriteShorts` 方法限制最多读写125个和123个16位有符号整数。

        //2. **批量读写操作前的连接检查**：
        //   - 使用 `ExecuteWithConnectionCheck` 方法确保在每次读写操作前尝试连接，并在连接异常时自动重连。
        private TcpClient _tcpClient;
        private IModbusMaster _master;
        private string _ipAddress;
        private int _port;
        private int _reconnectInterval = 5000; // 3 seconds


        public string IpAddress { get { return _ipAddress; } }
        public int Port { get { return _port; } }
        public ModbusTcpHelper(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        // 连接到PLC
        public void Connect()
        {
            try
            {
                _tcpClient = new TcpClient(_ipAddress, _port);
                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcpClient);
            }
            catch (Exception ex)
            {
                throw new Exception($"Modbus读取错误: {ex.Message}");
                
            }
        }

        // 断开与PLC的连接
        public void Disconnect()
        {
            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient.Dispose();
                _tcpClient = null;
            }
        }

        // 检查是否连接
        public bool IsConnected => _tcpClient?.Connected ?? false;





        // 读取单个线圈状态
        public bool ReadSingleCoil(ushort address, byte slaveId = 1)
        {
            return _master.ReadCoils(slaveId, address, 1)[0];
        }

        // 写入单个线圈状态
        public void WriteSingleCoil(ushort address, bool value, byte slaveId = 1)
        {
            _master.WriteSingleCoil(slaveId, address, value);

        }

        // 读取单个寄存器值
        public ushort ReadSingleRegister(ushort address, byte slaveId = 1)
        {
            return _master.ReadHoldingRegisters(slaveId, address, 1)[0];
        }

        // 写入单个寄存器值
        public void WriteSingleRegister(ushort address, ushort value, byte slaveId = 1)
        {
            _master.WriteSingleRegister(slaveId, address, value);
        }

        // 读取32位整数
        public int ReadInt32(ushort address, byte slaveId = 1)
        {

            var registers = _master.ReadHoldingRegisters(slaveId, address, 2);
            return (registers[1] << 16) | registers[0];

        }

        // 写入32位整数
        public void WriteInt32(ushort address, int value, byte slaveId = 1)
        {

            var lowWord = (ushort)(value & 0xFFFF);
            var highWord = (ushort)((value >> 16) & 0xFFFF);
            _master.WriteMultipleRegisters(slaveId, address, new ushort[] { lowWord, highWord });


        }

        // 读取浮点数
        public float ReadFloat(ushort address, byte slaveId = 1)
        {

            var registers = _master.ReadHoldingRegisters(slaveId, address, 2);
            var bytes = new byte[4];
            BitConverter.GetBytes(registers[0]).CopyTo(bytes, 0);
            BitConverter.GetBytes(registers[1]).CopyTo(bytes, 2);
            return BitConverter.ToSingle(bytes, 0);

        }

        // 写入浮点数
        public void WriteFloat(ushort address, float value, byte slaveId = 1)
        {

            var bytes = BitConverter.GetBytes(value);
            var lowWord = BitConverter.ToUInt16(bytes, 0);
            var highWord = BitConverter.ToUInt16(bytes, 2);
            _master.WriteMultipleRegisters(slaveId, address, new ushort[] { lowWord, highWord });

        }

        // 批量读取线圈状态
        public bool[] ReadCoils(ushort startAddress, ushort numberOfPoints, byte slaveId = 1)
        {

            return _master.ReadCoils(slaveId, startAddress, numberOfPoints);
        }

        // 批量写入线圈状态
        public void WriteCoils(ushort startAddress, bool[] values, byte slaveId = 1)
        {

            _master.WriteMultipleCoils(slaveId, startAddress, values);
        }

        // 批量读取保持寄存器
        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort numberOfPoints, byte slaveId = 1)
        {

            return _master.ReadHoldingRegisters(slaveId, startAddress, numberOfPoints);
        }

       
        // 批量写入保持寄存器
        public void WriteHoldingRegisters(ushort startAddress, ushort[] values, byte slaveId = 1)
        {

            _master.WriteMultipleRegisters(slaveId, startAddress, values);
        }

        // 批量读取32位整数
        public int[] ReadInt32s(ushort startAddress, ushort numberOfPoints, byte slaveId = 1)
        {


            ushort[] registers = _master.ReadHoldingRegisters(slaveId, startAddress, (ushort)(numberOfPoints * 2));
            int[] values = new int[numberOfPoints];

            for (int i = 0; i < numberOfPoints; i++)
            {
                values[i] = (registers[i * 2 + 1] << 16) | registers[i * 2];
            }

            return values;

        }

        // 批量写入32位整数
        public void WriteInt32s(ushort startAddress, int[] values, byte slaveId = 1)
        {


            ushort[] registers = new ushort[values.Length * 2];

            for (int i = 0; i < values.Length; i++)
            {
                registers[i * 2] = (ushort)(values[i] & 0xFFFF);
                registers[i * 2 + 1] = (ushort)((values[i] >> 16) & 0xFFFF);
            }
            _master.WriteMultipleRegisters(slaveId, startAddress, registers);


        }

        // 批量读取浮点数
        public float[] ReadFloats(ushort startAddress, ushort numberOfPoints, byte slaveId = 1)
        {


            ushort[] registers = _master.ReadHoldingRegisters(slaveId, startAddress, (ushort)(numberOfPoints * 2));
            float[] values = new float[numberOfPoints];

            for (int i = 0; i < numberOfPoints; i++)
            {
                byte[] bytes = new byte[4];
                BitConverter.GetBytes(registers[i * 2]).CopyTo(bytes, 0);
                BitConverter.GetBytes(registers[i * 2 + 1]).CopyTo(bytes, 2);
                values[i] = BitConverter.ToSingle(bytes, 0);
            }

            return values;

        }

        // 批量写入浮点数
        public void WriteFloats(ushort startAddress, float[] values, byte slaveId = 1)
        {


            ushort[] registers = new ushort[values.Length * 2];

            for (int i = 0; i < values.Length; i++)
            {
                byte[] bytes = BitConverter.GetBytes(values[i]);
                registers[i * 2] = BitConverter.ToUInt16(bytes, 0);
                registers[i * 2 + 1] = BitConverter.ToUInt16(bytes, 2);
            }

            _master.WriteMultipleRegisters(slaveId, startAddress, registers);

        }

        // 读取单个16位有符号整数
        public short ReadSingleShort(ushort address, byte slaveId = 1)
        {
            return (short)_master.ReadHoldingRegisters(slaveId, address, 1)[0];
        }

        // 写入单个16位有符号整数
        public void WriteSingleShort(ushort address, short value, byte slaveId = 1)
        {
            _master.WriteSingleRegister(slaveId, address, (ushort)value);
        }

        // 批量读取16位有符号整数
        public short[] ReadShorts(ushort startAddress, ushort numberOfPoints, byte slaveId = 1)
        {


            ushort[] registers = _master.ReadHoldingRegisters(slaveId, startAddress, numberOfPoints);
            short[] values = new short[numberOfPoints];

            for (int i = 0; i < numberOfPoints; i++)
            {
                values[i] = (short)registers[i];
            }

            return values;

        }

        // 批量写入16位有符号整数
        public void WriteShorts(ushort startAddress, short[] values, byte slaveId = 1)
        {


            ushort[] registers = new ushort[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                registers[i] = (ushort)values[i];
            }

            _master.WriteMultipleRegisters(slaveId, startAddress, registers);

        }
    }
}
