using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace SharpScrcpy
{
  /// <summary>
  /// 封装队列方法
  /// </summary>
  /// <typeparam name="Entity"></typeparam>
  public abstract class QueueHelper<TEntity> : IDisposable
  {
    private ConcurrentQueue<TEntity> entities = new ConcurrentQueue<TEntity>();
    private ManualResetEvent _event = new ManualResetEvent(false);
    private bool _stop = false;
    public QueueHelper()
    {
      new Thread(() =>
      {
        while (!_stop)
        {
          _event.WaitOne();
          try
          {
            while (entities.Any())
            {
              if (entities.TryDequeue(out var entity))
              {
                Execute(entity);
              }
            }
          }
          catch (Exception ex)
          {

          }
          _event.Reset();
        }
      })
      { IsBackground = true }.Start();
    }

    /// <summary>
    /// 执行方法
    /// </summary>
    /// <param name="entity"></param>
    protected abstract void Execute(TEntity entity);

    /// <summary>
    /// 恢复线程
    /// </summary>
    public void Resume()
    {
      _event.Set();
    }

    /// <summary>
    /// 添加元素
    /// </summary>
    /// <param name="entity"></param>
    public void Add(TEntity entity)
    {
      entities.Enqueue(entity);
    }

    public void Stop()
    {
      _stop = true;
    }

    public void Dispose()
    {
      Stop();
    }
  }
}
