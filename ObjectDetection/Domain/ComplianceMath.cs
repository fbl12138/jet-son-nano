namespace ObjectDetection.Domain;

/// <summary>
/// 合规口径的唯一出处。
/// 改动这里等于改业务定义，所以集中在一处、并配有单测。
/// </summary>
public static class ComplianceMath
{
    /// <summary>合规人次 = max(0, 检测人次 − 违规人次)</summary>
    public static int Compliant(int persons, int violations)
        => Math.Max(0, persons - violations);

    /// <summary>合规率，保留 4 位小数；无人次时返回 0</summary>
    public static double Rate(int persons, int violations)
        => persons <= 0
            ? 0
            : Math.Round((double)Compliant(persons, violations) / persons, 4);
}
