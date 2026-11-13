using ObjectDetection.Models;

namespace ObjectDetection.Domain
{
    public class ComplianceEvaluator
    {
        public const string NoMask = "no_mask";
        public const string NoHelmet = "no_helmet";
        public static IReadOnlyList<string> Evaluate(
            IReadOnlyDictionary<string, double?> attributes,ComplianceRule? rule)
        {
            if(rule is null || !rule.Enabled) return Array.Empty<string>();
            var types = new List<string>(2);
            if(rule.RequireMask && !Meet(attributes,"mask",rule.MinConfidence))
            {
                types.Add(NoMask);
            }
            if(rule.RequireHelmet && !Meet(attributes,"helmet",rule.MinConfidence))
            {
                types.Add(NoHelmet);
            }
            return types;
        }
        private static bool Meet(
            IReadOnlyDictionary<string,double?> attributes,
                string label,
                decimal minConfidence)
        {
            return attributes.GetValueOrDefault(label) is double confidence && confidence >= (double)minConfidence;
        }        
    }
}
