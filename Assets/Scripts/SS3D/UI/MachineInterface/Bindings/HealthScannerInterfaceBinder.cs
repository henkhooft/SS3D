using SS3D.Systems.Health;
using SS3D.UI.MachineInterface.Components;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SS3D.UI.MachineInterface.Bindings
{
    public sealed class HealthScannerInterfaceBinder : IMachineInterfaceBinder
    {
        public event Action CloseRequested;

        public event Action<byte, bool> BoolControlChanged;

        public event Action<byte, float> NumericControlChanged;

        public event Action<byte, int> ActionControlChanged;

        private readonly DiegeticDeviceShell _shell;
        private readonly ConnectionStatusRow _connectionRow;
        private readonly DeviceIdentityBlock _identity;
        private readonly GlanceableStatusChip _statusChip;
        private readonly AnatomyBodyMap _bodyMap;
        private readonly VitalGaugeTile _bloodGauge;
        private readonly VitalGaugeTile _cnsGauge;
        private readonly VitalGaugeTile _toxinGauge;
        private readonly VitalGaugeTile _spO2Gauge;
        private readonly VisualElement _organList;
        private readonly DeviceFooter _footer;

        public HealthScannerInterfaceBinder(VisualElement root)
        {
            _shell = root.Q<DiegeticDeviceShell>("device-shell") ?? root.Q<DiegeticDeviceShell>();
            VisualElement queryRoot = _shell?.ScreenContent ?? root;

            _connectionRow = queryRoot.Q<ConnectionStatusRow>("connection-row");
            _identity = queryRoot.Q<DeviceIdentityBlock>("identity");
            _statusChip = queryRoot.Q<GlanceableStatusChip>("status-chip");
            _bodyMap = queryRoot.Q<AnatomyBodyMap>("body-map");
            _bloodGauge = queryRoot.Q<VitalGaugeTile>("blood-gauge");
            _cnsGauge = queryRoot.Q<VitalGaugeTile>("cns-gauge");
            _toxinGauge = queryRoot.Q<VitalGaugeTile>("toxin-gauge");
            _spO2Gauge = queryRoot.Q<VitalGaugeTile>("spo2-gauge");
            _organList = queryRoot.Q<VisualElement>("organ-list");
            _footer = queryRoot.Q<DeviceFooter>("footer");

            if (_shell != null)
            {
                _shell.CloseClicked += HandleCloseRequested;
            }
        }

        public void Bind(IMachineInterfaceViewModel viewModel)
        {
            if (viewModel is not HealthScannerInterfaceViewModel model)
            {
                return;
            }

            if (_shell != null)
            {
                _shell.ModelLabel = model.ModelLabel;
                _shell.PowerOk = model.ChassisPowerOk;
            }

            if (_connectionRow != null)
            {
                _connectionRow.StatusText = model.ConnectionStatus;
                _connectionRow.DotTone = model.ChassisPowerOk ? StatusTone.Info : StatusTone.Danger;
            }

            if (_identity != null)
            {
                _identity.Title = model.Title;
                _identity.Subtitle = model.Subtitle;
            }

            StatusTone statusTone = GetScenarioTone(model.Scenario);
            _statusChip?.SetContent(model.StatusHeadline, model.StatusBadgeText, statusTone, model.StatusSubline);

            if (_bodyMap != null)
            {
                foreach (KeyValuePair<BodyZone, AnatomyZoneVisual> entry in model.Zones)
                {
                    _bodyMap.SetZone(entry.Key, entry.Value);
                }

                if (model.Scenario == HealthScannerScenario.NoSubject)
                {
                    _bodyMap.ClearSelection();
                }
            }

            BindGauge(_bloodGauge, "Blood Vol.", model.BloodValueText, model.BloodFraction, model.BloodTone);
            BindGauge(_cnsGauge, "CNS", model.CnsValueText, model.CnsFraction, model.CnsTone);
            BindGauge(_toxinGauge, "Toxin", model.ToxinValueText, model.ToxinFraction, model.ToxinTone);
            BindGauge(_spO2Gauge, "SpO2", model.SpO2ValueText, model.SpO2Fraction, model.SpO2Tone);

            BindOrgans(model.Organs);

            if (_footer != null)
            {
                _footer.Text = model.FooterText;
            }
        }

        public void Disconnect()
        {
            if (_shell != null)
            {
                _shell.CloseClicked -= HandleCloseRequested;
            }
        }

        private void HandleCloseRequested()
        {
            CloseRequested?.Invoke();
        }

        private static void BindGauge(VitalGaugeTile gauge, string label, string valueText, float fraction, StatusTone tone)
        {
            if (gauge == null)
            {
                return;
            }

            gauge.Label = label;
            gauge.ValueText = valueText;
            gauge.Fraction = fraction;
            gauge.Tone = tone;
        }

        private void BindOrgans(List<OrganReadoutData> organs)
        {
            if (_organList == null)
            {
                return;
            }

            _organList.Clear();
            for (int i = 0; i < organs.Count; i++)
            {
                OrganReadoutData organ = organs[i];
                OrganReadoutRow row = new()
                {
                    Label = organ.Label,
                    ValueText = organ.ValueText,
                    Tone = organ.Tone,
                };
                _organList.Add(row);
            }
        }

        private static StatusTone GetScenarioTone(HealthScannerScenario scenario)
        {
            return scenario switch
            {
                HealthScannerScenario.Nominal => StatusTone.Success,
                HealthScannerScenario.Impaired => StatusTone.Warning,
                HealthScannerScenario.Critical => StatusTone.Danger,
                _ => StatusTone.Neutral,
            };
        }
    }
}
