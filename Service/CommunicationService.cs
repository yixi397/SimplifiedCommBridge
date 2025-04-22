using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimplifiedCommBridge.Models;

namespace SimplifiedCommBridge.Service
{
    /// <summary>
    /// 通信服务管理类
    /// </summary>
    public class CommunicationService 
    {
        public Dictionary<string, ICommunicationProtocol> Protocols;
        public ObservableCollection<Variable> Variables { get; } = new();
        private int interval;
        private CancellationTokenSource cancellationTokenSource;
        private Task pollingTask;
        public event Action<object, LogEventArgs> LogEvent;
        public CommunicationService()
        {
          
        }

        private void Protocol_LogEvent(object sender, LogEventArgs logEventArgs)
        {
          
            LogEvent?.Invoke(sender, logEventArgs);
        }

        private List<string> listProtocols = new List<string>();
        /// <summary>
        /// 配置协议连接参数
        /// </summary>
        public void ConfigureProtocol(string protocolType, Dictionary<string, object> parameters)
        {
            //判断是否出现重复配置
            foreach (var item in listProtocols)
            {
                if(protocolType== item)
                {
                    Protocol_LogEvent(this, new LogEventArgs($"{protocolType}以配置,请误重复操作",LogEventArgs.LogLevelEnum.Warning));
                    return;
                }
            }

            Protocol_LogEvent(this, new LogEventArgs($"{protocolType}配置连续参数"));
            if (Protocols.TryGetValue(protocolType, out var protocol))
            {
                protocol.LogEvent += Protocol_LogEvent;
                protocol.Connect(parameters);
            }
            listProtocols.Add(protocolType);
            Protocol_LogEvent(this, new LogEventArgs($"{protocolType}配置连续参数完成"));
        }
     

      
       
        /// <summary>
        /// 启动轮询
        /// </summary>
        /// <param name="interval">轮询间隔（毫秒）</param>
        public void StartPolling(int interval)
        {
            Protocol_LogEvent(this, new LogEventArgs("启动轮询"));
            if( cancellationTokenSource!=null)
            {
                Protocol_LogEvent(this, new LogEventArgs("轮询正转执行",LogEventArgs.LogLevelEnum.Warning));
                return;
                //StopPolling(); // 先终止旧任务并释放资源
            }
            if (interval <= 0)
            {
                Protocol_LogEvent(this, new LogEventArgs("轮询间隔小于等于0", LogEventArgs.LogLevelEnum.Error));
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
            }

            this.interval = interval;
            cancellationTokenSource = new CancellationTokenSource();
            pollingTask = Task.Run(() => PollingAsync(cancellationTokenSource.Token), cancellationTokenSource.Token);
            Protocol_LogEvent(this, new LogEventArgs("启动轮询完成"));
        }

        

        /// <summary>
        /// 停止轮询
        /// </summary>
        public void StopPolling()
        {
            Protocol_LogEvent(this, new LogEventArgs("停止轮询"));
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                try
                {
                    pollingTask?.Wait(); // 等待任务结束
                }
                catch (AggregateException ex)
                {
                    // 检查是否是任务取消异常
                    if (ex.InnerExceptions.Count == 1 && ex.InnerExceptions[0] is TaskCanceledException)
                    {
                        // 任务被取消，这是预期行为，不抛出新异常
                        Protocol_LogEvent(this, new LogEventArgs("任务已取消", LogEventArgs.LogLevelEnum.Info));
                    }
                    else
                    {
                        Protocol_LogEvent(this, new LogEventArgs("停止轮询异常", LogEventArgs.LogLevelEnum.Error));
                        throw new Exception(ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    Protocol_LogEvent(this, new LogEventArgs("停止轮询异常", LogEventArgs.LogLevelEnum.Error));
                    throw new Exception(ex.Message);
                }
                finally
                {
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                    pollingTask = null;
                }
            }

            Protocol_LogEvent(this, new LogEventArgs("停止轮询完成"));
        }
        /// <summary>
        /// 异步轮询方法，按协议类型分组读取变量，并发执行协议读取任务，直到取消令牌被触发。
        /// </summary>
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>返回表示异步操作的Task</returns>
        private async Task PollingAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 按协议类型分组读取
                    var tasks = Variables
                        .GroupBy(v => v.ProtocolName)
                        .Select(async group =>
                        {
                            if (Protocols.TryGetValue(group.Key, out var protocol))
                            {
                                var stopwatch = new Stopwatch();
                                await Task.Run(() => { protocol.ReadVariables(group); });
                                var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                            }
                        });

                    await Task.WhenAll(tasks); // 并发执行所有协议读取任务
                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message) ;
                }

                await Task.Delay(interval, cancellationToken); // 异步等待轮询间隔
            }
        }



        /// <summary>
        /// 将变量写入对应的协议。
        /// </summary>
        /// <param name="variable">要写入的变量对象。</param>
        /// <returns>无返回值。</returns>
        public void Write(Variable variable)
        {

            // 如果变量的SetValue为null，则直接返回，避免后续操作。
            if (variable.SetValue == null)
            {
                Protocol_LogEvent(this, new LogEventArgs(variable.ToString()+ "SetValue为null", LogEventArgs.LogLevelEnum.Error));
                return;
            }
            // 根据协议类型选择对应的协议实例，执行变量写入操作。
            Protocols[variable.ProtocolName].WriteVariable(variable);
        }

        public void Write(List<Variable>  variable)
        {
            
            // 按协议类型分组读取
            foreach (var group in variable.GroupBy(v => v.ProtocolName))
            {
                if (Protocols.TryGetValue(group.Key, out var protocol))
                {
                    protocol.WriteVariable(group.ToList());
                }
            }
           

        }

      
    }
}
