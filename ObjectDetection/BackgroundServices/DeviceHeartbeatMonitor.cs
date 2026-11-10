using Microsoft.OpenApi;
using ObjectDetection.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System.Linq.Expressions;

namespace ObjectDetection.BackgroundServices
{
    public class DeviceHeartbeatMonitor : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DeviceHeartbeatMonitor> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
        public DeviceHeartbeatMonitor(IServiceScopeFactory scopeFactory,ILogger<DeviceHeartbeatMonitor> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var cutoff = DateTime.UtcNow - Timeout;
                    //只处理曾经开过机 现在超时的设备
                    //从未开过机的也就是LastHeartbeatAt为null 保持offline不动
                    var stale = await db.Devices.Where(d => d.Status == "Online" && d.LastHeartbeatAt != null && d.LastHeartbeatAt < cutoff).ToListAsync(stoppingToken);
                    if (stale.Count > 0)
                    {
                        foreach (var device in stale)
                            device.Status = "Offline";
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogWarning("设备心跳超时,将状态置为offline:{Codes}", string.Join(",", stale.Select(d => d.DeviceCode)));
                    }
                }
                catch(OperationCanceledException)
                {
                    //正常停机
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "心跳检测循环异常");
                }
                await Task.Delay(Interval, stoppingToken);
                }
            }
        }
    }

