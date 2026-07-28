using System.Collections.Generic;
using SS3D.Systems.Examine;
using UnityEngine;

namespace SS3D.Systems.Substances
{
    /// <summary>
    /// Free diegetic properties only: approximate volume, qualitative temperature, and visible color.
    /// Chemical identity is observer knowledge (chemistry gameplay) — not shown here.
    /// </summary>
    public sealed class SubstanceContainerExaminable : SimpleExaminable, IExamineContentProvider
    {
        [SerializeField]
        private SubstanceContainer _container;

        private void Awake()
        {
            if (_container == null)
            {
                _container = GetComponent<SubstanceContainer>();
            }
        }

        public void AppendSections(IExaminable examinable, List<ExamineSection> sections)
        {
            if (_container == null)
            {
                return;
            }

            string volumeLine = _container.IsEmpty
                ? "Empty."
                : $"Contains about {ApproximateVolumeLabel(_container.CurrentVolumeMl)} of liquid.";
            sections.Add(new ExamineSection(volumeLine));

            sections.Add(new ExamineSection($"Feels {TemperatureBand(_container.TemperatureKelvin)}."));

            if (!_container.IsEmpty)
            {
                Color color = _container.GetBlendColor();
                sections.Add(new ExamineSection($"The liquid looks {DescribeColor(color)}."));
            }
        }

        private static string ApproximateVolumeLabel(float volumeMl)
        {
            if (volumeMl < 5f)
            {
                return "a few milliliters";
            }

            if (volumeMl < 30f)
            {
                return "a small amount";
            }

            if (volumeMl < 80f)
            {
                return "a moderate amount";
            }

            return "a large amount";
        }

        private static string TemperatureBand(float kelvin)
        {
            if (kelvin < 260f)
            {
                return "frosted / bitterly cold";
            }

            if (kelvin < 280f)
            {
                return "cold";
            }

            if (kelvin < 310f)
            {
                return "cool";
            }

            if (kelvin < 340f)
            {
                return "warm";
            }

            if (kelvin < 370f)
            {
                return "hot";
            }

            return "scalding";
        }

        private static string DescribeColor(Color color)
        {
            float h, s, v;
            Color.RGBToHSV(color, out h, out s, out v);
            if (s < 0.15f)
            {
                return v > 0.7f ? "clear" : "murky";
            }

            if (h < 0.05f || h > 0.95f)
            {
                return "reddish";
            }

            if (h < 0.15f)
            {
                return "orange";
            }

            if (h < 0.2f)
            {
                return "yellow";
            }

            if (h < 0.45f)
            {
                return "green";
            }

            if (h < 0.7f)
            {
                return "blue";
            }

            return "purple";
        }
    }
}
