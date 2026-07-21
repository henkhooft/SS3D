using System.Collections.Generic;
using SS3D.Localization;
using SS3D.Systems.Tile;

namespace SS3D.Systems.Examine
{
    /// <summary>
    /// Appends Damaged / Cracked integrity lines when examining structural Turf.
    /// </summary>
    public class StructuralIntegrityExaminable : SimpleExaminable, IExamineContentProvider
    {
        public void AppendSections(IExaminable examinable, List<ExamineSection> sections)
        {
            if (!TryGetComponent(out PlacedTileObject placed))
                return;

            StructuralIntegrityStage stage = placed.IntegrityStage;
            if (stage == StructuralIntegrityStage.Intact || stage == StructuralIntegrityStage.Destroyed)
                return;

            if (stage == StructuralIntegrityStage.Damaged)
            {
                sections.Add(new ExamineSection(LocalizedTextService.GetFormattedString(
                    ExamineCanonicalKeyGenerator.ExamineTableName,
                    ExamineStructuralIntegrityKeys.Damaged,
                    null,
                    ExamineStructuralIntegrityKeys.DamagedFallback)));
                return;
            }

            if (stage == StructuralIntegrityStage.Cracked)
            {
                sections.Add(new ExamineSection(LocalizedTextService.GetFormattedString(
                    ExamineCanonicalKeyGenerator.ExamineTableName,
                    ExamineStructuralIntegrityKeys.Cracked,
                    null,
                    ExamineStructuralIntegrityKeys.CrackedFallback)));
            }
        }
    }
}
