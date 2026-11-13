using ObjectDetection.Domain;

namespace ObjectDetection.Tests;

public class ComplianceMathTests
{
    [Theory]
    [InlineData(0, 0, 0, 0.0)]        // 无人次
    [InlineData(100, 0, 100, 1.0)]    // 全合规
    [InlineData(93, 74, 19, 0.2043)]  // 真实数据（2026-11-02）
    [InlineData(10, 10, 0, 0.0)]      // 全部违规
    [InlineData(10, 20, 0, 0.0)]      // 违规数大于人次：合规数钳到 0，不能出现负数
    public void 计算合规人次与合规率(int persons, int violations, int expectedCompliant, double expectedRate)
    {
        Assert.Equal(expectedCompliant, ComplianceMath.Compliant(persons, violations));
        Assert.Equal(expectedRate, ComplianceMath.Rate(persons, violations), 4);
    }
}
