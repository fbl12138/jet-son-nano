
using ObjectDetection.Data;
using Microsoft.EntityFrameworkCore;
using ObjectDetection.Option;
using ObjectDetection.Models;
using Microsoft.Extensions.Options;

namespace ObjectDetection.BackgroundServices
{
    public class DeviceHeartbeatMonitor : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DeviceHeartbeatMonitor> _logger;

        private readonly MonitoringOptions _options;
        public DeviceHeartbeatMonitor(IServiceScopeFactory scopeFactory,ILogger<DeviceHeartbeatMonitor> logger,
            IOptions<MonitoringOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(_options.HeartbeattimeoutSeconds);
                    //只处理曾经开过机 现在超时的设备
                    //从未开过机的也就是LastHeartbeatAt为null 保持offline不动
                    var stale = await db.Devices.Where(d => d.Status == DeviceStatus.Online && d.LastHeartbeatAt != null && d.LastHeartbeatAt < cutoff).ToListAsync(stoppingToken);
                    if (stale.Count > 0)
                    {
                        foreach (var device in stale)
                            device.Status = DeviceStatus.Offline;
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
                await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatScanIntervalSeconds), stoppingToken);
                }
            }
        }
    }

