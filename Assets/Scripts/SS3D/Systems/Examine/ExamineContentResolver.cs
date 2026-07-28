using System.Collections.Generic;
using SS3D.Localization;
using UnityEngine;

namespace SS3D.Systems.Examine
{
    /// <summary>
    /// Resolves static and dynamic examine text from <see cref="ExamineData"/> and optional providers.
    /// </summary>
    public class ExamineContentResolver
    {
        public ExamineContent Resolve(IExaminable examinable)
        {
            ExamineData data = examinable?.GetData();
            if (data == null)
            {
                return ExamineContent.Empty;
            }

            string name = LocalizedTextService.GetString(data.Name);
            string description = LocalizedTextService.GetString(data.Description);

            List<ExamineSection> sections = new();
            AppendProviderSections(examinable, sections);

            return new ExamineContent(name, description, sections);
        }

        private static void AppendProviderSections(IExaminable examinable, List<ExamineSection> sections)
        {
            if (examinable is Component component)
            {
                IExamineContentProvider[] providers = component.GetComponents<IExamineContentProvider>();
                for (int i = 0; i < providers.Length; i++)
                {
                    providers[i].AppendSections(examinable, sections);
                }

                return;
            }

            if (examinable is IExamineContentProvider provider)
            {
                provider.AppendSections(examinable, sections);
            }
        }
    }
}
