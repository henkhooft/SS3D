using UnityEngine.UIElements;

namespace SS3D.UI.MachineInterface.Components
{
    /// <summary>
    /// Single row in the anatomical scan's organ readout list — dot indicator, label, function %.
    /// </summary>
    [UxmlElement]
    public partial class OrganReadoutRow : VisualElement
    {
        private readonly VisualElement _dot;
        private readonly Label _label;
        private readonly Label _value;

        public OrganReadoutRow()
        {
            AddToClassList("organ-readout-row");

            _dot = new VisualElement();
            _dot.AddToClassList("organ-readout-row__dot");

            _label = new Label("ORGAN");
            _label.AddToClassList("organ-readout-row__label");
            _label.AddToClassList("font-terminal");

            _value = new Label("—");
            _value.AddToClassList("organ-readout-row__value");
            _value.AddToClassList("font-terminal");

            Add(_dot);
            Add(_label);
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
        public StatusTone Tone
        {
            get => StatusTone.Neutral;
            set
            {
                StatusToneUtility.ApplyTone(_value, value);
                _dot.RemoveFromClassList("organ-readout-row__dot--warning");
                _dot.RemoveFromClassList("organ-readout-row__dot--danger");

                if (value == StatusTone.Danger)
                {
                    _dot.AddToClassList("organ-readout-row__dot--danger");
                }
                else if (value == StatusTone.Warning)
                {
                    _dot.AddToClassList("organ-readout-row__dot--warning");
                }
            }
        }
    }
}
