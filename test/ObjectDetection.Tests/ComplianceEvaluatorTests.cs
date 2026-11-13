using ObjectDetection.Domain;
using ObjectDetection.Models;

namespace ObjectDetection.Tests;

public class ComplianceEvaluatorTests
{
    /// <summary>默认规则：要求口罩、不要求安全帽、阈值 0.5</summary>
    private static ComplianceRule MaskRule(decimal minConfidence = 0.5m) => new()
    {
        Enabled = true,
        RequireMask = true,
        RequireHelmet = false,
        MinConfidence = minConfidence
    };

    /// <summary>构造属性表：Attrs(("mask", 0.87))，或 Attrs() 表示没有任何属性</summary>
    private static IReadOnlyDictionary<string, double?> Attrs(
        params (string Key, double? Value)[] pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Value);

    // ── 守卫条件 ──────────────────────────────────────────

    [Fact]
    public void 无规则_不生成违规()
        => Assert.Empty(ComplianceEvaluator.Evaluate(Attrs(), null));

    [Fact]
    public void 规则未启用_不生成违规()
    {
        var rule = MaskRule();
        rule.Enabled = false;

        Assert.Empty(ComplianceEvaluator.Evaluate(Attrs(), rule));
    }

    // ── 口罩判定 ──────────────────────────────────────────

    [Fact]
    public void 缺少口罩属性_判定未佩戴口罩()
    {
        var result = ComplianceEvaluator.Evaluate(Attrs(), MaskRule());

        Assert.Equal(new[] { ComplianceEvaluator.NoMask }, result);
    }

    [Fact]
    public void 口罩置信度低于阈值_判定未佩戴口罩()
    {
        var result = ComplianceEvaluator.Evaluate(Attrs(("mask", 0.3)), MaskRule(0.5m));

        Assert.Equal(new[] { ComplianceEvaluator.NoMask }, result);
    }

    [Fact]
    public void 口罩置信度恰好等于阈值_视为合规()
        => Assert.Empty(ComplianceEvaluator.Evaluate(Attrs(("mask", 0.5)), MaskRule(0.5m)));

    [Fact]
    public void 口罩置信度高于阈值_视为合规()
        => Assert.Empty(ComplianceEvaluator.Evaluate(Attrs(("mask", 0.87)), MaskRule(0.5m)));

    [Fact]
    public void 口罩属性为null_判定未佩戴口罩()
    {
        var result = ComplianceEvaluator.Evaluate(Attrs(("mask", null)), MaskRule());

        Assert.Equal(new[] { ComplianceEvaluator.NoMask }, result);
    }

    // ── 规则开关 ──────────────────────────────────────────

    [Fact]
    public void 未要求口罩_不产生口罩违规()
    {
        var rule = MaskRule();
        rule.RequireMask = false;

        Assert.Empty(ComplianceEvaluator.Evaluate(Attrs(), rule));
    }

    // ── 多违规组合 ────────────────────────────────────────

    [Fact]
    public void 同时要求口罩和安全帽_返回两条违规()
    {
        var rule = MaskRule();
        rule.RequireHelmet = true;

        var result = ComplianceEvaluator.Evaluate(Attrs(), rule);

        Assert.Equal(2, result.Count);
        Assert.Contains(ComplianceEvaluator.NoMask, result);
        Assert.Contains(ComplianceEvaluator.NoHelmet, result);
    }
}