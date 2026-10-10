using System;

namespace SPTEssentials.Client.Breacher;

internal static class ExplosiveBreacherRules
{
    internal const string Rshg2TemplateId = "676bf44c5539167c3603e869";

    internal static bool IsBreachingRocketLauncher(string templateId)
    {
        return string.Equals(
            templateId,
            Rshg2TemplateId,
            StringComparison.OrdinalIgnoreCase);
    }
}
