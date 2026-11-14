using System.Security.Cryptography;
using System.Text;

namespace ObjectDetection.Domain;

/// <summary>
/// 设备 ApiKey 的哈希与校验。
/// 数据库只存哈希，不存明文；比较使用恒定时间算法，避免时序侧信道。
/// </summary>
public static class ApiKeyHasher
{
    /// <summary>SHA-256 的十六进制小写表示（固定 64 字符）</summary>
    public static string Hash(string apiKey)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    /// <summary>
    /// 校验明文是否匹配存储的哈希。
    /// 使用 <see cref="CryptographicOperations.FixedTimeEquals"/>，耗时与匹配前缀长度无关。
    /// </summary>
    public static bool Verify(string? plain, string? storedHash)
    {
        if (string.IsNullOrEmpty(plain) || string.IsNullOrEmpty(storedHash))
            return false;

        // 哈希是 ASCII 十六进制串，用 ASCII 编码取字节即可
        var computed = Encoding.ASCII.GetBytes(Hash(plain));
        var stored = Encoding.ASCII.GetBytes(storedHash);

        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }
}
