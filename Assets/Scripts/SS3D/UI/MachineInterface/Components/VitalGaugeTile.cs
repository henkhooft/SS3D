using UnityEngine;
using UnityEngine.UIElements;

namespace SS3D.UI.MachineInterface.Components
{
    /// <summary>
    /// Labelled linear gauge for a single systemic vital (blood volume, toxin load, SpO2, CNS function).
    /// </summary>
    [UxmlElement]
    public partial class VitalGaugeTile : VisualElement
    {
        private readonly Label _label;
        private readonly VisualElement _fill;
        private readonly Label _value;
        private float _fraction;

        public VitalGaugeTile()
        {
            AddToClassList("vital-gauge-tile");

            _label = new Label("VITAL");
            _label.AddToClassList("vital-gauge-tile__label");
            _label.AddToClassList("font-arcade");

            VisualElement track = new();
            track.AddToClassList("vital-gauge-tile__track");

            _fill = new VisualElement();
            _fill.AddToClassList("vital-gauge-tile__fill");
            track.Add(_fill);

            _value = new Label("—");
            _value.AddToClassList("vital-gauge-tile__value");
            _value.AddToClassList("font-terminal");

            Add(_label);
            Add(track);
            Add(_value);
        }

        [UxmlAttribute]
        public string Label
        {
            get => _label.text;
            set => _label.text = value;
        }

        [UxmlAttribute]
        public string ValueText
        {
            get => _value.text;
            set => _value.text = value;
        }

        [UxmlAttribute]
        public float Fraction
        {
            get => _fraction;
            set
            {
                _fraction = Mathf.Clamp01(value);
                _fill.style.width = new Length(_fraction * 100f, LengthUnit.Percent);
            }
        }

        [UxmlAttribute]
        public StatusTone Tone
        {
            get => StatusTone.Neutral;
            set
            {
                StatusToneUtility.ApplyTone(_value, value);
                _fill.RemoveFromClassList("vital-gauge-tile__fill--warning");
                _fill.RemoveFromClassList("vital-gauge-tile__fill--danger");

                if (value == StatusTone.Danger)
                {
                    _fill.AddToClassList("vital-gauge-tile__fill--danger");
                }
                else if (value == StatusTone.Warning)
                {
                    _fill.AddToClassList("vital-gauge-tile__fill--warning");
                }
            }
        }
    }
}
