using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SimplifiedCommBridge.Models;

namespace SimplifiedCommBridge.Service
{
    /// <summary>
    /// 通信协议统一接口
    /// </summary>
    public interface ICommunicationProtocol
    {
        /// <summary>
        /// 连接状态
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接从站设备
        /// </summary>
        /// <param name="parameters">配置信息</param>
        /// <returns></returns>
        bool Connect(Dictionary<string, object> parameters);


        /// <summary>
        /// 断开连接
        /// </summary>
        //void Disconnect();

        /// <summary>
        /// 批量读取数据
        /// </summary>
        /// <param name="variables"> </param>
        void ReadVariables(IEnumerable<Variable> variables);

        /// <summary>
        /// 单个变量写入
        /// </summary>
        void WriteVariable(Variable variable);

        /// <summary>
        /// 批量写入数据
        /// </summary>
        void WriteVariable(List<Variable> variable);

        /// <summary>
        /// 日志事件
        /// </summary>
        event Action<object , LogEventArgs> LogEvent;
        //EventArgs
    }

    public class LogEventArgs : EventArgs
    {

        public LogEventArgs(string Message, LogLevelEnum logLevel = LogLevelEnum.Info)
        {
            this.Message = Message;
            this.LogLevel = logLevel;
        }
        public string Message { get; set; }
        public LogLevelEnum LogLevel { get; set; }
        public enum LogLevelEnum { Debug, Info, Warning, Error }

        public override string ToString()
        {
            return ($"[{DateTime.Now}]: {LogLevel}: {Message}");
        }
    }
}
